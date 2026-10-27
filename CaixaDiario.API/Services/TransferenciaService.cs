using System.Text.Json;
using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Services;

public class TransferenciaService : ITransferenciaService
{
    private readonly ITransferenciaRepository _transferenciaRepo;
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IAuditService _auditService;
    private readonly AppDbContext _context;

    public TransferenciaService(
        ITransferenciaRepository transferenciaRepo,
        IContaBancariaRepository contaRepo,
        IRegistroRepository registroRepo,
        IAuditService auditService,
        AppDbContext context)
    {
        _transferenciaRepo = transferenciaRepo;
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _auditService = auditService;
        _context = context;
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

    // Reverte a classificação. Dois casos bem diferentes, segundo Provisoria:
    // - Provisoria=false: o lado já existia antes de virar Transferência (veio de um extrato real
    //   já importado, ou foi reconciliado depois — ver ImportacaoService.ReconciliarProvisoria).
    //   Apagar destruiria dinheiro que realmente se moveu no banco — só tira o rótulo, volta a
    //   "Sem categoria", pendente. Saldo não muda: o valor do lançamento nunca foi alterado.
    // - Provisoria=true: o lado foi inventado do zero pelo próprio app só pra existir a contrapartida
    //   (ver ConverterLancamentoAsync/CriarAsync) — nunca existiu no extrato real. Desfazer tem que
    //   apagar ele mesmo, senão sobra um lançamento fantasma "Sem categoria" que nunca aconteceu, com
    //   o saldo do dia ainda contando o efeito dele (bug: saldo errado depois de desfazer).
    public async Task DesfazerClassificacaoAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var transferencia = await _transferenciaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, "Transferência não encontrada.");
        VerificarAcesso(transferencia.ClienteId, usuarioLogadoId, perfil);

        // Transacional: as duas pontas + a remoção do registro de Transferencia têm que valer todas
        // juntas ou nenhuma — uma falha no meio (ex.: perda de conexão) não pode deixar o par
        // "meio desfeito" (uma ponta revertida, a outra ainda marcada como transferência).
        // IsRelational() evita quebrar os testes, que usam o provider InMemory (sem suporte a transação).
        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var regOrigem = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaOrigemId, transferencia.Data);
        var itemOrigem = regOrigem?.Saidas.FirstOrDefault(s => s.TransferenciaId == transferencia.Id);
        if (regOrigem != null && itemOrigem != null)
        {
            if (itemOrigem.Provisoria)
            {
                regOrigem.Saidas = regOrigem.Saidas.Where(s => s.Id != itemOrigem.Id).ToList();
                regOrigem.SaldoFinal += itemOrigem.Valor;
            }
            else
            {
                itemOrigem.Categoria = string.Empty;
                itemOrigem.TipoCusto = null;
                itemOrigem.TransferenciaId = null;
                itemOrigem.PendenteCategorizacao = true;
                // Reatribui a lista — Entradas/Saidas são jsonb sem value comparer configurado, então
                // o EF só detecta a mudança se a referência da lista mudar, não se um item for mutado.
                regOrigem.Saidas = new List<ItemFinanceiroSaida>(regOrigem.Saidas);
            }
            regOrigem.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regOrigem);
        }

        var regDestino = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaDestinoId, transferencia.Data);
        var itemDestino = regDestino?.Entradas.FirstOrDefault(e => e.TransferenciaId == transferencia.Id);
        if (regDestino != null && itemDestino != null)
        {
            if (itemDestino.Provisoria)
            {
                regDestino.Entradas = regDestino.Entradas.Where(e => e.Id != itemDestino.Id).ToList();
                regDestino.SaldoFinal -= itemDestino.Valor;
            }
            else
            {
                itemDestino.Categoria = null;
                itemDestino.TipoCusto = null;
                itemDestino.TransferenciaId = null;
                itemDestino.PendenteCategorizacao = true;
                regDestino.Entradas = new List<ItemFinanceiro>(regDestino.Entradas);
            }
            regDestino.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regDestino);
        }

        await _transferenciaRepo.RemoverAsync(transferencia);
        if (transacao != null) await transacao.CommitAsync();

        // Cascata de saldo nas duas contas — essencial quando uma contrapartida provisória foi
        // apagada acima (muda SaldoFinal do dia); inofensiva no caso "só desvinculou" (delta zero,
        // RecalcularDiasSeguintesAsync para na primeira divergência zerada, não escreve nada à toa).
        if (regOrigem != null && itemOrigem != null)
        {
            var registrosOrigem = await _registroRepo.ListarPorContaAsync(transferencia.ContaOrigemId);
            await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, transferencia.ContaOrigemId, transferencia.Data, regOrigem.SaldoFinal, registrosOrigem);
        }
        if (regDestino != null && itemDestino != null)
        {
            var registrosDestino = await _registroRepo.ListarPorContaAsync(transferencia.ContaDestinoId);
            await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, transferencia.ContaDestinoId, transferencia.Data, regDestino.SaldoFinal, registrosDestino);
        }

        await _auditService.LogAsync(transferencia.ClienteId, usuarioLogadoId, "Transferencia", "DesfazerClassificacao",
            transferencia.Id.ToString(), JsonSerializer.Serialize(transferencia), null);
    }

    // Exclui de verdade as duas pontas — chamado quando o usuário pede "Excluir" num lançamento
    // que é parte de uma Transferência (ação do extrato, não a de reclassificar/desfazer acima).
    public async Task ExcluirAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var transferencia = await _transferenciaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, "Transferência não encontrada.");
        VerificarAcesso(transferencia.ClienteId, usuarioLogadoId, perfil);

        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var regOrigem = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaOrigemId, transferencia.Data);
        var itemOrigem = regOrigem?.Saidas.FirstOrDefault(s => s.TransferenciaId == transferencia.Id);
        if (regOrigem != null && itemOrigem != null)
        {
            regOrigem.Saidas = regOrigem.Saidas.Where(s => s.Id != itemOrigem.Id).ToList();
            regOrigem.SaldoFinal += itemOrigem.Valor;
            regOrigem.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regOrigem);
        }

        var regDestino = await _registroRepo.ObterPorContaEDataAsync(transferencia.ContaDestinoId, transferencia.Data);
        var itemDestino = regDestino?.Entradas.FirstOrDefault(e => e.TransferenciaId == transferencia.Id);
        if (regDestino != null && itemDestino != null)
        {
            regDestino.Entradas = regDestino.Entradas.Where(e => e.Id != itemDestino.Id).ToList();
            regDestino.SaldoFinal -= itemDestino.Valor;
            regDestino.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(regDestino);
        }

        await _transferenciaRepo.RemoverAsync(transferencia);
        if (transacao != null) await transacao.CommitAsync();

        // Cascata de saldo nas duas contas — sem isso, qualquer dia seguinte já existente (mesma
        // conta) ficava com um Inicio/SaldoFinal baseado num valor que não existe mais depois da
        // exclusão (mesma garantia que CriarAsync já aplica na criação).
        if (regOrigem != null && itemOrigem != null)
        {
            var registrosOrigem = await _registroRepo.ListarPorContaAsync(transferencia.ContaOrigemId);
            await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, transferencia.ContaOrigemId, transferencia.Data, regOrigem.SaldoFinal, registrosOrigem);
        }
        if (regDestino != null && itemDestino != null)
        {
            var registrosDestino = await _registroRepo.ListarPorContaAsync(transferencia.ContaDestinoId);
            await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, transferencia.ContaDestinoId, transferencia.Data, regDestino.SaldoFinal, registrosDestino);
        }

        await _auditService.LogAsync(transferencia.ClienteId, usuarioLogadoId, "Transferencia", "Exclusao",
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

        // Transacional: contrapartida, ponta original e o registro de Transferencia têm que ser
        // gravados juntos — sem isso, uma falha no meio deixa resíduo (ex.: contrapartida criada mas
        // a ponta original nunca marcada, ou vice-versa) e um retry do usuário, sem essa marca, cria
        // um SEGUNDO par inteiro em vez de completar o primeiro (era assim que o lote duplicava tudo).
        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        if (dto.Tipo == "Saida")
        {
            var item = registro.Saidas.FirstOrDefault(s => s.Id == dto.LancamentoId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
            if (item.TransferenciaId.HasValue)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Este lançamento já foi classificado como transferência.");
            // Money Vision: "Pix recebido entrando como saída" — nunca propaga um valor corrompido
            // pra uma contrapartida nova. Saidas/Entradas sempre guardam magnitude positiva; um
            // valor <= 0 aqui só pode ser dado já corrompido antes desta conversão.
            if (item.Valor <= 0)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Este lançamento tem um valor inválido e não pode ser convertido em transferência.", "lancamentoId");
            valor = item.Valor;
            descricaoOriginal = item.Descricao;
            item.TipoCusto = LancamentoFiltro.TipoTransferencia;
            item.TransferenciaId = transferenciaId;
            item.Categoria = "Transferência";
            item.PendenteCategorizacao = false;
            item.RegraCategorizacaoId = dto.RegraCategorizacaoId;
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
                itemContrapartida.RegraCategorizacaoId = dto.RegraCategorizacaoId;
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
                        // Mesma descrição do lançamento original — Provisoria=true já sinaliza que é
                        // provisório; trocar a descrição por um rótulo genérico só dificultava
                        // reconhecer de qual movimentação essa contrapartida veio.
                        Id = Guid.NewGuid(), Descricao = descricaoOriginal, Valor = valor,
                        Categoria = "Transferência", TipoCusto = LancamentoFiltro.TipoTransferencia, TransferenciaId = transferenciaId,
                        RegraCategorizacaoId = dto.RegraCategorizacaoId, Provisoria = true,
                    },
                };
                regDestino.SaldoFinal += valor;
                regDestino.SalvoEm = DateTime.UtcNow;
                await RegistroDiaHelper.PersistirAsync(_registroRepo, regDestino, novo);
                // A contrapartida provisória pode ter entrado no meio da cadeia de dias já existente
                // na conta destino (ex.: dias futuros materializados antes) — sem isso, o Inicio deles
                // ficava desatualizado (bug real em produção: saldo do card não fechava com o extrato).
                await RegistroService.RecalcularContaInteiraAsync(_registroRepo, contrapartida.Id, contrapartida.SaldoInicial);
            }

            contaOrigemId = conta.Id;
            contaDestinoId = contrapartida.Id;
        }
        else
        {
            var item = registro.Entradas.FirstOrDefault(e => e.Id == dto.LancamentoId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
            if (item.TransferenciaId.HasValue)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Este lançamento já foi classificado como transferência.");
            // Ver comentário equivalente no ramo de Saída acima.
            if (item.Valor <= 0)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Este lançamento tem um valor inválido e não pode ser convertido em transferência.", "lancamentoId");
            valor = item.Valor;
            descricaoOriginal = item.Descricao;
            item.TipoCusto = LancamentoFiltro.TipoTransferencia;
            item.TransferenciaId = transferenciaId;
            item.Categoria = "Transferência";
            item.PendenteCategorizacao = false;
            item.RegraCategorizacaoId = dto.RegraCategorizacaoId;
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
                itemContrapartida.RegraCategorizacaoId = dto.RegraCategorizacaoId;
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
                        // Mesma descrição do lançamento original — ver comentário equivalente no
                        // ramo de Saída acima.
                        Id = Guid.NewGuid(), Descricao = descricaoOriginal, Valor = valor, Subcategoria = string.Empty,
                        Categoria = "Transferência", TipoCusto = LancamentoFiltro.TipoTransferencia, TransferenciaId = transferenciaId,
                        RegraCategorizacaoId = dto.RegraCategorizacaoId, Provisoria = true,
                    },
                };
                regOrigem.SaldoFinal -= valor;
                regOrigem.SalvoEm = DateTime.UtcNow;
                await RegistroDiaHelper.PersistirAsync(_registroRepo, regOrigem, novo);
                // Ver comentário equivalente no ramo de Saída acima.
                await RegistroService.RecalcularContaInteiraAsync(_registroRepo, contrapartida.Id, contrapartida.SaldoInicial);
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
        if (transacao != null) await transacao.CommitAsync();

        var contaOrigemNome = contaOrigemId == conta.Id ? conta.Nome : contrapartida.Nome;
        var contaDestinoNome = contaDestinoId == conta.Id ? conta.Nome : contrapartida.Nome;
        var resultado = MapToDto(criada, contaOrigemNome, contaDestinoNome);

        await _auditService.LogAsync(conta.ClienteId, usuarioLogadoId, "Transferencia", "ConversaoDeLancamento",
            transferenciaId.ToString(), null, JsonSerializer.Serialize(resultado));

        return resultado;
    }

    // Fase 5: cria o par do ZERO — nenhum dos dois lados existia antes. Mesmo padrão transacional
    // de ConverterLancamentoAsync; Provisoria=true nos dois lados pra reconciliar com o extrato
    // real (da conta de investimento, tipicamente) em vez de duplicar quando ele for importado.
    public async Task<TransferenciaDto> CriarAsync(
        Guid clienteId, Guid contaOrigemId, Guid contaDestinoId, DateOnly data, decimal valor, string descricao,
        Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        if (valor <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe um valor maior que zero.", "valor");
        if (contaOrigemId == contaDestinoId)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "A conta de origem deve ser diferente da conta de destino.");

        var contaOrigem = await ObterContaDoClienteAsync(contaOrigemId, clienteId);
        var contaDestino = await ObterContaDoClienteAsync(contaDestinoId, clienteId);
        if (!contaOrigem.Ativa || !contaDestino.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "As duas contas devem estar ativas.");

        var transferenciaId = Guid.NewGuid();

        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var (regOrigem, novoOrigem) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaOrigem, data);
        regOrigem.Saidas = new List<ItemFinanceiroSaida>(regOrigem.Saidas)
        {
            new()
            {
                Id = Guid.NewGuid(), Descricao = descricao, Valor = valor, Subcategoria = string.Empty,
                Categoria = "Transferência", TipoCusto = LancamentoFiltro.TipoTransferencia,
                TransferenciaId = transferenciaId, Provisoria = true,
            },
        };
        regOrigem.SaldoFinal -= valor;
        regOrigem.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regOrigem, novoOrigem);

        var (regDestino, novoDestino) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaDestino, data);
        regDestino.Entradas = new List<ItemFinanceiro>(regDestino.Entradas)
        {
            new()
            {
                Id = Guid.NewGuid(), Descricao = descricao, Valor = valor,
                Categoria = "Transferência", TipoCusto = LancamentoFiltro.TipoTransferencia,
                TransferenciaId = transferenciaId, Provisoria = true,
            },
        };
        regDestino.SaldoFinal += valor;
        regDestino.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regDestino, novoDestino);

        var transferencia = new Transferencia
        {
            Id = transferenciaId, ClienteId = clienteId, ContaOrigemId = contaOrigemId, ContaDestinoId = contaDestinoId,
            Data = data, Valor = valor, Descricao = descricao, CriadoEm = DateTime.UtcNow,
        };
        var criada = await _transferenciaRepo.AdicionarAsync(transferencia);
        if (transacao != null) await transacao.CommitAsync();

        var resultado = MapToDto(criada, contaOrigem.Nome, contaDestino.Nome);
        await _auditService.LogAsync(clienteId, usuarioLogadoId, "Transferencia", "Criacao",
            transferenciaId.ToString(), null, JsonSerializer.Serialize(resultado));

        // Cascata de saldo nas duas contas (mesma garantia do item 2) — a transferência pode cair
        // numa data com registros futuros já existentes em qualquer uma das duas contas.
        var registrosOrigem = await _registroRepo.ListarPorContaAsync(contaOrigemId);
        await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, contaOrigemId, data, regOrigem.SaldoFinal, registrosOrigem);
        var registrosDestino = await _registroRepo.ListarPorContaAsync(contaDestinoId);
        await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, contaDestinoId, data, regDestino.SaldoFinal, registrosDestino);

        return resultado;
    }

    // Money Vision — Bloco 4, item 4 da Parte 2: varre todos os registros do cliente procurando
    // lançamentos que a importação (dicionário de palavras-chave de ImportacaoService) já marcou
    // como "parece transferência" (Categoria="Transferência", CategoriaSugerida=true) mas que
    // ainda estão pendentes — nunca passaram por ConverterLancamentoAsync de verdade.
    public async Task<List<SugestaoTransferenciaSemParDto>> ListarSugestoesSemParAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);

        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var contasAtivasPorId = contas.Where(c => c.Ativa).ToDictionary(c => c.Id);
        // Só dá pra sugerir destino com confiança quando há exatamente UMA conta de investimento
        // ativa — com duas ou mais, adivinhar qual é "a certa" erraria tanto quanto não sugerir nada.
        var unicaContaInvestimento = contas.Count(c => c.Ativa && c.Tipo == "Investimento") == 1
            ? contas.First(c => c.Ativa && c.Tipo == "Investimento") : null;

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var resultado = new List<SugestaoTransferenciaSemParDto>();

        foreach (var registro in registros.Where(r => !r.Excluido))
        {
            if (!contasAtivasPorId.TryGetValue(registro.ContaBancariaId ?? Guid.Empty, out var conta)) continue;
            var destinoSugerido = unicaContaInvestimento != null && unicaContaInvestimento.Id != conta.Id ? unicaContaInvestimento : null;

            foreach (var entrada in registro.Entradas.Where(EhSugestaoSemPar))
            {
                resultado.Add(new SugestaoTransferenciaSemParDto
                {
                    ContaBancariaId = conta.Id, ContaBancariaNome = conta.Nome, LancamentoId = entrada.Id,
                    Data = registro.Data, Descricao = entrada.Descricao, Valor = entrada.Valor, Tipo = "Entrada",
                    ContaDestinoSugeridaId = destinoSugerido?.Id, ContaDestinoSugeridaNome = destinoSugerido?.Nome,
                });
            }
            foreach (var saida in registro.Saidas.Where(EhSugestaoSemPar))
            {
                resultado.Add(new SugestaoTransferenciaSemParDto
                {
                    ContaBancariaId = conta.Id, ContaBancariaNome = conta.Nome, LancamentoId = saida.Id,
                    Data = registro.Data, Descricao = saida.Descricao, Valor = -saida.Valor, Tipo = "Saida",
                    ContaDestinoSugeridaId = destinoSugerido?.Id, ContaDestinoSugeridaNome = destinoSugerido?.Nome,
                });
            }
        }

        return resultado.OrderByDescending(s => s.Data).ToList();
    }

    private static bool EhSugestaoSemPar(ItemFinanceiro e) =>
        e.Categoria == "Transferência" && e.CategoriaSugerida && e.PendenteCategorizacao;

    private static bool EhSugestaoSemPar(ItemFinanceiroSaida s) =>
        s.Categoria == "Transferência" && s.CategoriaSugerida && s.PendenteCategorizacao;

    public async Task DescartarSugestaoAsync(DescartarSugestaoTransferenciaDto dto, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(dto.ContaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var registro = await _registroRepo.ObterPorContaEDataAsync(dto.ContaId, dto.Data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");

        var saida = registro.Saidas.FirstOrDefault(s => s.Id == dto.LancamentoId);
        if (saida != null)
        {
            saida.Categoria = string.Empty;
            saida.TipoCusto = null;
            saida.CategoriaSugerida = false;
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
        }
        else
        {
            var entrada = registro.Entradas.FirstOrDefault(e => e.Id == dto.LancamentoId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
            entrada.Categoria = null;
            entrada.TipoCusto = null;
            entrada.CategoriaSugerida = false;
            registro.Entradas = new List<ItemFinanceiro>(registro.Entradas);
        }

        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);
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
