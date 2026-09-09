using CaixaDiario.API.DTOs.Regras;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RegraCategorizacaoService : IRegraCategorizacaoService
{
    private readonly IRegraCategorizacaoRepository _regraRepo;
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly ICategoriaRepository _categoriaRepo;
    private readonly ITransferenciaService _transferenciaService;

    public RegraCategorizacaoService(
        IRegraCategorizacaoRepository regraRepo,
        IContaBancariaRepository contaRepo,
        IRegistroRepository registroRepo,
        ICategoriaRepository categoriaRepo,
        ITransferenciaService transferenciaService)
    {
        _regraRepo = regraRepo;
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _categoriaRepo = categoriaRepo;
        _transferenciaService = transferenciaService;
    }

    public async Task<List<RegraCategorizacaoDto>> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var regras = await _regraRepo.ListarPorClienteAsync(clienteId);
        var resultado = new List<RegraCategorizacaoDto>();
        foreach (var regra in regras)
            resultado.Add(await MapToDtoAsync(regra));
        return resultado;
    }

    public async Task<RegraCategorizacaoDto> CriarAsync(Guid clienteId, CriarRegraDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        ValidarDto(dto.Tipo, dto.AcaoTipo, dto.Categoria, dto.ContaContrapartidaId);

        var conta = await _contaRepo.ObterPorIdAsync(dto.ContaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (conta.ClienteId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (dto.AcaoTipo == "Transferencia")
            await ValidarContrapartidaAsync(dto.ContaContrapartidaId!.Value, dto.ContaBancariaId, clienteId);

        var (criterioTipo, criterioValor) = DescricaoMatcher.DeterminarCriterio(dto.DescricaoReferencia);
        var regrasExistentes = await _regraRepo.ListarPorClienteAsync(clienteId);
        var proximaOrdem = regrasExistentes.Count == 0 ? 0 : regrasExistentes.Max(r => r.Ordem) + 1;

        var regra = new RegraCategorizacao
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = dto.ContaBancariaId,
            Tipo = dto.Tipo,
            CriterioTipo = criterioTipo,
            CriterioValor = criterioValor,
            DescricaoReferencia = dto.DescricaoReferencia,
            AcaoTipo = dto.AcaoTipo,
            Categoria = dto.AcaoTipo == "Categoria" ? dto.Categoria : null,
            ContaContrapartidaId = dto.AcaoTipo == "Transferencia" ? dto.ContaContrapartidaId : null,
            Ativa = true,
            Ordem = proximaOrdem,
            CriadoEm = DateTime.UtcNow,
        };

        var criada = await _regraRepo.AdicionarAsync(regra);
        // Regra recém-criada nunca pode ter classificado nada ainda — evita uma busca de registros
        // à toa (e o mesmo vale pro nome da contrapartida, se houver).
        var contrapartidaNome = dto.AcaoTipo == "Transferencia"
            ? (await _contaRepo.ObterPorIdAsync(dto.ContaContrapartidaId!.Value))?.Nome
            : null;
        return MapToDtoSemContagem(criada, conta.Nome, contrapartidaNome, quantidadeAplicada: 0);
    }

    public async Task<RegraCategorizacaoDto> AtualizarAsync(Guid id, AtualizarRegraDto dto, Guid usuarioLogadoId, string perfil)
    {
        var regra = await ObterComAcessoAsync(id, usuarioLogadoId, perfil);
        ValidarDto(regra.Tipo, dto.AcaoTipo, dto.Categoria, dto.ContaContrapartidaId);

        if (dto.AcaoTipo == "Transferencia")
            await ValidarContrapartidaAsync(dto.ContaContrapartidaId!.Value, regra.ContaBancariaId, regra.ClienteId);

        var (criterioTipo, criterioValor) = DescricaoMatcher.DeterminarCriterio(dto.DescricaoReferencia);
        regra.DescricaoReferencia = dto.DescricaoReferencia;
        regra.CriterioTipo = criterioTipo;
        regra.CriterioValor = criterioValor;
        regra.AcaoTipo = dto.AcaoTipo;
        regra.Categoria = dto.AcaoTipo == "Categoria" ? dto.Categoria : null;
        regra.ContaContrapartidaId = dto.AcaoTipo == "Transferencia" ? dto.ContaContrapartidaId : null;
        regra.Ativa = dto.Ativa;
        regra.AtualizadoEm = DateTime.UtcNow;

        var atualizada = await _regraRepo.AtualizarAsync(regra);
        return await MapToDtoAsync(atualizada);
    }

    public async Task DesativarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var regra = await ObterComAcessoAsync(id, usuarioLogadoId, perfil);
        regra.Ativa = false;
        regra.AtualizadoEm = DateTime.UtcNow;
        await _regraRepo.AtualizarAsync(regra);
    }

    public async Task ReativarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var regra = await ObterComAcessoAsync(id, usuarioLogadoId, perfil);
        regra.Ativa = true;
        regra.AtualizadoEm = DateTime.UtcNow;
        await _regraRepo.AtualizarAsync(regra);
    }

    public async Task ExcluirAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var regra = await ObterComAcessoAsync(id, usuarioLogadoId, perfil);
        // Não apaga os lançamentos que ela já classificou — só a regra em si. RegraCategorizacaoId
        // nos itens fica órfão (mesmo padrão de TransferenciaId após excluir uma Transferencia).
        await _regraRepo.RemoverAsync(regra);
    }

    public async Task ReordenarAsync(Guid clienteId, List<Guid> ids, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var regras = await _regraRepo.ListarPorClienteAsync(clienteId);
        var porId = regras.ToDictionary(r => r.Id);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!porId.TryGetValue(ids[i], out var regra)) continue;
            regra.Ordem = i;
            await _regraRepo.AtualizarAsync(regra);
        }
    }

    public async Task<int> ContarCorrespondenciasAsync(Guid contaBancariaId, string tipo, string descricaoReferencia, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var (criterioTipo, criterioValor) = DescricaoMatcher.DeterminarCriterio(descricaoReferencia);
        var registros = await _registroRepo.ListarPorContaAsync(contaBancariaId);

        int total = 0;
        foreach (var registro in registros.Where(r => !r.Excluido))
        {
            var itens = tipo == "Entrada"
                ? registro.Entradas.Where(e => e.PendenteCategorizacao).Select(e => e.Descricao)
                : registro.Saidas.Where(s => s.PendenteCategorizacao).Select(s => s.Descricao);
            total += itens.Count(desc => DescricaoMatcher.Casa(criterioTipo, criterioValor, desc));
        }
        return total;
    }

    public async Task<AplicarRetroativoResultDto> AplicarRetroativamenteAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var regra = await ObterComAcessoAsync(id, usuarioLogadoId, perfil);
        if (!regra.Ativa)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Reative a regra antes de aplicá-la retroativamente.");

        var categoriasPorNome = regra.AcaoTipo == "Categoria"
            ? (await _categoriaRepo.ListarTodasAsync()).ToDictionary(c => c.Nome, c => c.Tipo)
            : new Dictionary<string, string>();

        var registros = await _registroRepo.ListarPorContaAsync(regra.ContaBancariaId);
        var total = 0;

        // Junta os alvos primeiro (Id do item + data do registro) — a conversão em Transferencia
        // busca o registro de novo por conta+data, então não dá pra iterar e mutar a mesma lista ao
        // mesmo tempo que ela é potencialmente recarregada por baixo pelo TransferenciaService.
        var alvos = new List<(DateOnly Data, Guid ItemId)>();
        foreach (var registro in registros.Where(r => !r.Excluido))
        {
            var itens = regra.Tipo == "Entrada"
                ? registro.Entradas.Where(e => e.PendenteCategorizacao).Select(e => (e.Id, e.Descricao))
                : registro.Saidas.Where(s => s.PendenteCategorizacao).Select(s => (s.Id, s.Descricao));
            foreach (var (itemId, descricao) in itens)
                if (DescricaoMatcher.Casa(regra.CriterioTipo, regra.CriterioValor, descricao))
                    alvos.Add((registro.Data, itemId));
        }

        if (regra.AcaoTipo == "Transferencia")
        {
            // Sequencial — mesma razão do lote manual: cada conversão lê e grava o RegistroDiario do
            // dia; paralelizar arriscaria leitura-e-escrita concorrente na mesma linha.
            foreach (var (data, itemId) in alvos)
            {
                try
                {
                    await _transferenciaService.ConverterLancamentoAsync(new ConverterLancamentoEmTransferenciaDto
                    {
                        ContaId = regra.ContaBancariaId,
                        LancamentoId = itemId,
                        Data = data,
                        Tipo = regra.Tipo,
                        ContaContrapartidaId = regra.ContaContrapartidaId!.Value,
                        RegraCategorizacaoId = regra.Id,
                    }, usuarioLogadoId, perfil);
                    total++;
                }
                catch (ApiException)
                {
                    // Item pode ter deixado de estar pendente entre o levantamento e a conversão
                    // (outra aba, outra regra) — pula sem abortar o restante do lote.
                }
            }
        }
        else
        {
            foreach (var grupo in alvos.GroupBy(a => a.Data))
            {
                var registro = await _registroRepo.ObterPorContaEDataAsync(regra.ContaBancariaId, grupo.Key);
                if (registro == null) continue;

                foreach (var (_, itemId) in grupo)
                {
                    if (regra.Tipo == "Entrada")
                    {
                        var item = registro.Entradas.FirstOrDefault(e => e.Id == itemId && e.PendenteCategorizacao);
                        if (item == null) continue;
                        item.Categoria = regra.Categoria;
                        if (regra.Categoria != null && categoriasPorNome.TryGetValue(regra.Categoria, out var tc))
                            item.TipoCusto = tc;
                        item.PendenteCategorizacao = false;
                        item.RegraCategorizacaoId = regra.Id;
                    }
                    else
                    {
                        var item = registro.Saidas.FirstOrDefault(s => s.Id == itemId && s.PendenteCategorizacao);
                        if (item == null) continue;
                        item.Categoria = regra.Categoria ?? string.Empty;
                        if (regra.Categoria != null && categoriasPorNome.TryGetValue(regra.Categoria, out var tc))
                            item.TipoCusto = tc;
                        item.PendenteCategorizacao = false;
                        item.RegraCategorizacaoId = regra.Id;
                    }
                    total++;
                }

                registro.Entradas = new List<ItemFinanceiro>(registro.Entradas);
                registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
                registro.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(registro);
            }
        }

        return new AplicarRetroativoResultDto { TotalCategorizados = total };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<RegraCategorizacaoDto> MapToDtoAsync(RegraCategorizacao regra)
    {
        var conta = regra.ContaBancaria ?? await _contaRepo.ObterPorIdAsync(regra.ContaBancariaId);
        var contrapartida = regra.ContaContrapartidaId.HasValue
            ? regra.ContaContrapartida ?? await _contaRepo.ObterPorIdAsync(regra.ContaContrapartidaId.Value)
            : null;
        var quantidadeAplicada = await ContarAplicadosAsync(regra);
        return MapToDtoSemContagem(regra, conta?.Nome ?? "—", contrapartida?.Nome, quantidadeAplicada);
    }

    private static RegraCategorizacaoDto MapToDtoSemContagem(
        RegraCategorizacao regra, string contaNome, string? contrapartidaNome, int quantidadeAplicada) => new()
    {
        Id = regra.Id,
        ContaBancariaId = regra.ContaBancariaId,
        ContaBancariaNome = contaNome,
        Tipo = regra.Tipo,
        CriterioTipo = regra.CriterioTipo,
        CriterioValor = regra.CriterioValor,
        DescricaoReferencia = regra.DescricaoReferencia,
        AcaoTipo = regra.AcaoTipo,
        Categoria = regra.Categoria,
        ContaContrapartidaId = regra.ContaContrapartidaId,
        ContaContrapartidaNome = contrapartidaNome,
        Ativa = regra.Ativa,
        Ordem = regra.Ordem,
        QuantidadeAplicada = quantidadeAplicada,
        CriadoEm = regra.CriadoEm,
    };

    private async Task<int> ContarAplicadosAsync(RegraCategorizacao regra)
    {
        var registros = await _registroRepo.ListarPorContaAsync(regra.ContaBancariaId);
        return registros.Where(r => !r.Excluido).Sum(r =>
            regra.Tipo == "Entrada"
                ? r.Entradas.Count(e => e.RegraCategorizacaoId == regra.Id)
                : r.Saidas.Count(s => s.RegraCategorizacaoId == regra.Id));
    }

    private async Task<RegraCategorizacao> ObterComAcessoAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var regra = await _regraRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGRA_CATEGORIZACAO_NAO_ENCONTRADA, "Regra não encontrada.");
        VerificarAcesso(regra.ClienteId, usuarioLogadoId, perfil);
        return regra;
    }

    private async Task ValidarContrapartidaAsync(Guid contaContrapartidaId, Guid contaBancariaId, Guid clienteId)
    {
        if (contaContrapartidaId == contaBancariaId)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "A conta contrapartida deve ser diferente da conta da regra.");
        var contrapartida = await _contaRepo.ObterPorIdAsync(contaContrapartidaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta contrapartida não encontrada.");
        if (contrapartida.ClienteId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (!contrapartida.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "A conta contrapartida deve estar ativa.");
    }

    private static void ValidarDto(string tipo, string acaoTipo, string? categoria, Guid? contaContrapartidaId)
    {
        if (tipo is not ("Entrada" or "Saida"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Tipo deve ser Entrada ou Saida.");
        if (acaoTipo is not ("Categoria" or "Transferencia"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "AcaoTipo deve ser Categoria ou Transferencia.");
        if (acaoTipo == "Categoria" && string.IsNullOrWhiteSpace(categoria))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a categoria da regra.", "categoria");
        if (acaoTipo == "Transferencia" && !contaContrapartidaId.HasValue)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a conta contrapartida da regra.", "contaContrapartidaId");
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }
}
