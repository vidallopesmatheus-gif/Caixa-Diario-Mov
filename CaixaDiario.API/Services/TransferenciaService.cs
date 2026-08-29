using System.Text.Json;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class TransferenciaService : ITransferenciaService
{
    private readonly ITransferenciaRepository _transferenciaRepo;
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IAuditService _auditService;

    public TransferenciaService(
        ITransferenciaRepository transferenciaRepo,
        IContaBancariaRepository contaRepo,
        IRegistroRepository registroRepo,
        IAuditService auditService)
    {
        _transferenciaRepo = transferenciaRepo;
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _auditService = auditService;
    }

    public async Task<TransferenciaDto> CriarAsync(CriarTransferenciaDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(dto.ClienteId, usuarioLogadoId, perfil);

        if (dto.Data > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");
        if (dto.ContaOrigemId == dto.ContaDestinoId)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Conta de origem e destino devem ser diferentes.");

        var origem = await ObterContaDoClienteAsync(dto.ContaOrigemId, dto.ClienteId);
        var destino = await ObterContaDoClienteAsync(dto.ContaDestinoId, dto.ClienteId);
        if (!origem.Ativa || !destino.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "Conta de origem e destino devem estar ativas.");

        var transferenciaId = Guid.NewGuid();
        var descricaoOrigem = string.IsNullOrWhiteSpace(dto.Descricao) ? $"Transferência para {destino.Nome}" : dto.Descricao;
        var descricaoDestino = string.IsNullOrWhiteSpace(dto.Descricao) ? $"Transferência de {origem.Nome}" : dto.Descricao;

        var (regOrigem, origemNovo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, origem, dto.Data);
        regOrigem.Saidas.Add(new ItemFinanceiroSaida
        {
            Descricao = descricaoOrigem,
            Valor = dto.Valor,
            Categoria = "Transferência",
            TipoCusto = LancamentoFiltro.TipoTransferencia,
            TransferenciaId = transferenciaId,
        });
        regOrigem.SaldoFinal -= dto.Valor;
        regOrigem.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regOrigem, origemNovo);

        var (regDestino, destinoNovo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, destino, dto.Data);
        regDestino.Entradas.Add(new ItemFinanceiro
        {
            Descricao = descricaoDestino,
            Valor = dto.Valor,
            Categoria = "Transferência",
            TipoCusto = LancamentoFiltro.TipoTransferencia,
            TransferenciaId = transferenciaId,
        });
        regDestino.SaldoFinal += dto.Valor;
        regDestino.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regDestino, destinoNovo);

        var transferencia = new Transferencia
        {
            Id = transferenciaId,
            ClienteId = dto.ClienteId,
            ContaOrigemId = origem.Id,
            ContaDestinoId = destino.Id,
            Data = dto.Data,
            Valor = dto.Valor,
            Descricao = dto.Descricao,
            CriadoEm = DateTime.UtcNow,
        };
        var criada = await _transferenciaRepo.AdicionarAsync(transferencia);

        var resultado = MapToDto(criada, origem.Nome, destino.Nome);
        await _auditService.LogAsync(dto.ClienteId, usuarioLogadoId, "Transferencia", "Criacao",
            transferenciaId.ToString(), null, JsonSerializer.Serialize(resultado));

        return resultado;
    }

    public async Task<List<TransferenciaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var transferencias = await _transferenciaRepo.ListarPorClienteAsync(clienteId);
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var nomesPorId = contas.ToDictionary(c => c.Id, c => c.Nome);

        return transferencias
            .Select(t => MapToDto(t, nomesPorId.GetValueOrDefault(t.ContaOrigemId, "—"), nomesPorId.GetValueOrDefault(t.ContaDestinoId, "—")))
            .ToList();
    }

    public async Task EstornarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var transferencia = await _transferenciaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, "Transferência não encontrada.");
        VerificarAcesso(transferencia.ClienteId, usuarioLogadoId, perfil);

        var regOrigem = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaOrigemId, transferencia.Data);
        if (regOrigem != null)
        {
            // Reatribui a lista (em vez de só chamar RemoveAll nela) — Entradas/Saidas são jsonb sem
            // value comparer configurado, então o EF só detecta a mudança se a referência da lista
            // mudar; mutar a lista existente no lugar pode não persistir no banco real.
            regOrigem.Saidas = regOrigem.Saidas.Where(s => s.TransferenciaId != transferencia.Id).ToList();
            regOrigem.SaldoFinal += transferencia.Valor;
            regOrigem.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regOrigem);
        }

        var regDestino = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaDestinoId, transferencia.Data);
        if (regDestino != null)
        {
            regDestino.Entradas = regDestino.Entradas.Where(e => e.TransferenciaId != transferencia.Id).ToList();
            regDestino.SaldoFinal -= transferencia.Valor;
            regDestino.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regDestino);
        }

        await _transferenciaRepo.RemoverAsync(transferencia);
        await _auditService.LogAsync(transferencia.ClienteId, usuarioLogadoId, "Transferencia", "Estorno",
            transferencia.Id.ToString(), JsonSerializer.Serialize(transferencia), null);
    }

    // Reclassifica um lançamento já existente (ex.: "Aplicação RDB" importado como saída) como
    // Transferência: a ponta original só é relabelada (categoria/tipoCusto/vínculo), sem alterar seu
    // valor/efeito no saldo já aplicado. A contrapartida é vinculada a um lançamento já existente
    // (quando informado, sem criar nada novo) ou criada do zero na conta informada.
    public async Task<TransferenciaDto> ConverterLancamentoAsync(
        ConverterLancamentoEmTransferenciaDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (dto.Tipo is not ("Entrada" or "Saida"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Tipo deve ser Entrada ou Saida.");

        var conta = await _contaRepo.ObterPorIdAsync(dto.ContaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var contrapartida = await ObterContaDoClienteAsync(dto.ContaContrapartidaId, conta.ClienteId);
        if (contrapartida.Id == conta.Id)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "A conta contrapartida deve ser diferente da conta original.");
        if (!contrapartida.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "A conta contrapartida deve estar ativa.");

        var registro = await _registroRepo.ObterPorContaEDataAsync(dto.ContaId, dto.Data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");

        var transferenciaId = Guid.NewGuid();
        decimal valor;
        string descricaoOriginal;
        Guid contaOrigemId, contaDestinoId;

        if (dto.Tipo == "Saida")
        {
            var item = registro.Saidas.FirstOrDefault(s => s.Id == dto.LancamentoId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
            valor = item.Valor;
            descricaoOriginal = item.Descricao;
            item.TipoCusto = LancamentoFiltro.TipoTransferencia;
            item.TransferenciaId = transferenciaId;
            item.Categoria = "Transferência";
            item.PendenteCategorizacao = false;
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas); // força o EF a detectar a mudança

            if (dto.LancamentoContrapartidaId.HasValue)
            {
                // Vincula a uma entrada já existente (ex.: o extrato da conta de investimento já foi
                // importado e trouxe essa mesma movimentação) — não cria lançamento novo, só relabela.
                var dataContrapartida = dto.DataContrapartida ?? dto.Data;
                var regContrapartida = await _registroRepo.ObterPorContaEDataAsync(contrapartida.Id, dataContrapartida)
                    ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento contrapartida não encontrado.");
                var itemContrapartida = regContrapartida.Entradas.FirstOrDefault(e => e.Id == dto.LancamentoContrapartidaId.Value)
                    ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento contrapartida não encontrado.");
                itemContrapartida.TipoCusto = LancamentoFiltro.TipoTransferencia;
                itemContrapartida.TransferenciaId = transferenciaId;
                itemContrapartida.Categoria = "Transferência";
                itemContrapartida.PendenteCategorizacao = false;
                regContrapartida.Entradas = new List<ItemFinanceiro>(regContrapartida.Entradas);
                regContrapartida.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(regContrapartida);
            }
            else
            {
                var (regDestino, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contrapartida, dto.Data);
                regDestino.Entradas = new List<ItemFinanceiro>(regDestino.Entradas)
                {
                    new()
                    {
                        Id = Guid.NewGuid(), Descricao = descricaoOriginal, Valor = valor,
                        Categoria = "Transferência", TipoCusto = LancamentoFiltro.TipoTransferencia, TransferenciaId = transferenciaId,
                    },
                };
                regDestino.SaldoFinal += valor;
                regDestino.SalvoEm = DateTime.UtcNow;
                await RegistroDiaHelper.PersistirAsync(_registroRepo, regDestino, novo);
            }

            contaOrigemId = conta.Id;
            contaDestinoId = contrapartida.Id;
        }
        else
        {
            var item = registro.Entradas.FirstOrDefault(e => e.Id == dto.LancamentoId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
            valor = item.Valor;
            descricaoOriginal = item.Descricao;
            item.TipoCusto = LancamentoFiltro.TipoTransferencia;
            item.TransferenciaId = transferenciaId;
            item.Categoria = "Transferência";
            registro.Entradas = new List<ItemFinanceiro>(registro.Entradas);

            if (dto.LancamentoContrapartidaId.HasValue)
            {
                var dataContrapartida = dto.DataContrapartida ?? dto.Data;
                var regContrapartida = await _registroRepo.ObterPorContaEDataAsync(contrapartida.Id, dataContrapartida)
                    ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento contrapartida não encontrado.");
                var itemContrapartida = regContrapartida.Saidas.FirstOrDefault(s => s.Id == dto.LancamentoContrapartidaId.Value)
                    ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento contrapartida não encontrado.");
                itemContrapartida.TipoCusto = LancamentoFiltro.TipoTransferencia;
                itemContrapartida.TransferenciaId = transferenciaId;
                itemContrapartida.Categoria = "Transferência";
                itemContrapartida.PendenteCategorizacao = false;
                regContrapartida.Saidas = new List<ItemFinanceiroSaida>(regContrapartida.Saidas);
                regContrapartida.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(regContrapartida);
            }
            else
            {
                var (regOrigem, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contrapartida, dto.Data);
                regOrigem.Saidas = new List<ItemFinanceiroSaida>(regOrigem.Saidas)
                {
                    new()
                    {
                        Id = Guid.NewGuid(), Descricao = descricaoOriginal, Valor = valor, Subcategoria = string.Empty,
                        Categoria = "Transferência", TipoCusto = LancamentoFiltro.TipoTransferencia, TransferenciaId = transferenciaId,
                    },
                };
                regOrigem.SaldoFinal -= valor;
                regOrigem.SalvoEm = DateTime.UtcNow;
                await RegistroDiaHelper.PersistirAsync(_registroRepo, regOrigem, novo);
            }

            contaOrigemId = contrapartida.Id;
            contaDestinoId = conta.Id;
        }

        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);

        var transferencia = new Transferencia
        {
            Id = transferenciaId,
            ClienteId = conta.ClienteId,
            ContaOrigemId = contaOrigemId,
            ContaDestinoId = contaDestinoId,
            Data = dto.Data,
            Valor = valor,
            Descricao = descricaoOriginal,
            CriadoEm = DateTime.UtcNow,
        };
        var criada = await _transferenciaRepo.AdicionarAsync(transferencia);

        var contaOrigemNome = contaOrigemId == conta.Id ? conta.Nome : contrapartida.Nome;
        var contaDestinoNome = contaDestinoId == conta.Id ? conta.Nome : contrapartida.Nome;
        var resultado = MapToDto(criada, contaOrigemNome, contaDestinoNome);

        await _auditService.LogAsync(conta.ClienteId, usuarioLogadoId, "Transferencia", "ConversaoDeLancamento",
            transferenciaId.ToString(), null, JsonSerializer.Serialize(resultado));

        return resultado;
    }

    private async Task<ContaBancaria> ObterContaDoClienteAsync(Guid contaId, Guid clienteId)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (conta.ClienteId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return conta;
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    private static TransferenciaDto MapToDto(Transferencia t, string contaOrigemNome, string contaDestinoNome) => new()
    {
        Id = t.Id,
        ClienteId = t.ClienteId,
        ContaOrigemId = t.ContaOrigemId,
        ContaOrigemNome = contaOrigemNome,
        ContaDestinoId = t.ContaDestinoId,
        ContaDestinoNome = contaDestinoNome,
        Data = t.Data,
        Valor = t.Valor,
        Descricao = t.Descricao,
        CriadoEm = t.CriadoEm,
    };
}
