using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services.Parsers;
using Microsoft.AspNetCore.Http;

namespace CaixaDiario.API.Services;

public class ImportacaoService : IImportacaoService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly ITransacaoImportadaRepository _importRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly ICategoriaRepository _categoriaRepo;
    private readonly IRegraCategorizacaoRepository _regraRepo;
    private readonly ITransferenciaService _transferenciaService;
    private readonly IConciliacaoService _conciliacaoService;

    public ImportacaoService(
        IContaBancariaRepository contaRepo,
        ITransacaoImportadaRepository importRepo,
        IRegistroRepository registroRepo,
        ICategoriaRepository categoriaRepo,
        IRegraCategorizacaoRepository regraRepo,
        ITransferenciaService transferenciaService,
        IConciliacaoService conciliacaoService)
    {
        _contaRepo = contaRepo;
        _importRepo = importRepo;
        _registroRepo = registroRepo;
        _categoriaRepo = categoriaRepo;
        _regraRepo = regraRepo;
        _transferenciaService = transferenciaService;
        _conciliacaoService = conciliacaoService;
    }

    // Representação unificada de uma linha do arquivo, independente do formato de origem.
    // Indice = posição no arquivo, na mesma ordem em que o parser encontrou.
    private record TransacaoParseada(int Indice, DateOnly Data, decimal Valor, string Descricao, string Tipo, string? FitId);

    // ── Preview (não persiste nada) — resumo agregado, sem listar linha por linha ────────────
    public async Task<PreviewImportacaoDto> PreviewAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim)
    {
        await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        var todasParseadas = ParsearArquivoValidando(arquivo);

        var dataInicioArquivo = todasParseadas.Min(t => t.Data);
        var dataFimArquivo = todasParseadas.Max(t => t.Data);

        var parseadas = todasParseadas;
        if (dataInicio.HasValue) parseadas = parseadas.Where(t => t.Data >= dataInicio.Value).ToList();
        if (dataFim.HasValue) parseadas = parseadas.Where(t => t.Data <= dataFim.Value).ToList();

        var registrosAtivos = (await _registroRepo.ListarPorContaAsync(contaBancariaId))
            .Where(r => !r.Excluido)
            .ToList();
        var historico = await _importRepo.ListarPorContaAsync(contaBancariaId);
        var ignoradas = historico.Where(t => t.Status == "Ignorada").ToList();
        var chavesImportadasAntes = ColetarChavesImportadas(historico);

        var jaImportadas = IdentificarJaImportadas(parseadas, registrosAtivos, ignoradas);
        var novas = parseadas.Where(t => !jaImportadas.Contains(t.Indice)).ToList();
        var conciliaveis = IdentificarConciliaveis(novas, registrosAtivos);
        var consumidoPelaHeuristica = ContarConsumidoPelaHeuristica(parseadas, jaImportadas);

        var candidatosDuplicata = IdentificarCandidatosDuplicata(
            novas.Where(t => !conciliaveis.Contains(t.Indice)).ToList(), registrosAtivos, chavesImportadasAntes, consumidoPelaHeuristica);

        return new PreviewImportacaoDto
        {
            TotalEncontradas = parseadas.Count,
            TotalJaImportadas = jaImportadas.Count,
            TotalNovas = novas.Count,
            TotalConciliarAoImportar = conciliaveis.Count,
            TotalEntradas = novas.Where(t => t.Tipo == "Entrada").Sum(t => t.Valor),
            TotalSaidas = novas.Where(t => t.Tipo == "Saida").Sum(t => t.Valor),
            DataInicioArquivo = dataInicioArquivo.ToString("yyyy-MM-dd"),
            DataFimArquivo = dataFimArquivo.ToString("yyyy-MM-dd"),
            DuplicatasManuais = MapearDuplicatasManuais(novas, candidatosDuplicata),
            DuplicatasEntreArquivos = MapearDuplicatasEntreArquivos(novas, candidatosDuplicata),
        };
    }

    private static List<DuplicataManualDto> MapearDuplicatasManuais(
        List<TransacaoParseada> novas, Dictionary<int, (ItemSemFitId Candidato, bool VeioDeImportacaoAnterior)> candidatosDuplicata) =>
        candidatosDuplicata.Where(kv => !kv.Value.VeioDeImportacaoAnterior)
            .Select(kv =>
            {
                var t = novas.First(x => x.Indice == kv.Key);
                return new DuplicataManualDto
                {
                    TransacaoIndice = kv.Key, DescricaoBanco = t.Descricao, DataBanco = t.Data.ToString("yyyy-MM-dd"),
                    Valor = t.Valor, Tipo = t.Tipo, LancamentoManualId = kv.Value.Candidato.ItemId,
                    DescricaoManual = kv.Value.Candidato.Descricao, DataManual = kv.Value.Candidato.Data.ToString("yyyy-MM-dd"),
                };
            })
            .OrderBy(d => d.TransacaoIndice)
            .ToList();

    private static List<DuplicataEntreArquivosDto> MapearDuplicatasEntreArquivos(
        List<TransacaoParseada> novas, Dictionary<int, (ItemSemFitId Candidato, bool VeioDeImportacaoAnterior)> candidatosDuplicata) =>
        candidatosDuplicata.Where(kv => kv.Value.VeioDeImportacaoAnterior)
            .Select(kv =>
            {
                var t = novas.First(x => x.Indice == kv.Key);
                return new DuplicataEntreArquivosDto
                {
                    TransacaoIndice = kv.Key, DescricaoBanco = t.Descricao, DataBanco = t.Data.ToString("yyyy-MM-dd"),
                    Valor = t.Valor, Tipo = t.Tipo,
                    DescricaoJaImportada = kv.Value.Candidato.Descricao, DataJaImportada = kv.Value.Candidato.Data.ToString("yyyy-MM-dd"),
                };
            })
            .OrderBy(d => d.TransacaoIndice)
            .ToList();

    // Versão só-leitura de ColetarProvisorias+ReconciliarProvisoria, pro Preview não precisar
    // persistir nada — identifica quais das "novas" vão casar com uma provisória 1:1 ao importar
    // de verdade (mesma regra: só concilia quando há exatamente 1 candidata).
    private static HashSet<int> IdentificarConciliaveis(List<TransacaoParseada> novas, List<RegistroDiario> registrosAtivos)
    {
        var pool = ColetarProvisorias(registrosAtivos);
        var indices = new HashSet<int>();
        foreach (var t in novas)
        {
            var candidatas = pool.Where(p => p.Tipo == t.Tipo
                && Math.Abs(p.Valor - t.Valor) < 0.01m
                && DiferencaDiasUteis(p.Registro.Data, t.Data) <= 2)
                .ToList();
            if (candidatas.Count != 1) continue;
            pool.Remove(candidatas[0]);
            indices.Add(t.Indice);
        }
        return indices;
    }

    // ── Fase 1.7: duplicata contra lançamento manual / entre dois arquivos importados ───────────
    // Candidato = item real (Entrada/Saída) sem FitId, na mesma conta, do mesmo sentido, valor
    // ±R$0,01 e data ±2 dias de uma transação do arquivo. Dois cenários (distinguidos por já ter
    // ou não uma linha correspondente no histórico de TransacaoImportada):
    // - Sem histórico de importação => é genuinamente MANUAL => oferece Mesclar/Importar como novo.
    // - Com histórico de importação (ex.: veio de um CSV/XLSX anterior, sem FitId) => é uma
    //   provável duplicata ENTRE ARQUIVOS (ex.: CSV + OFX do mesmo banco) => só sinaliza, sempre importa.
    private sealed record ItemSemFitId(RegistroDiario Registro, string Tipo, Guid ItemId, decimal Valor, string Descricao, DateOnly Data);

    // "consumidoPelaHeuristica" são as instâncias que IdentificarJaImportadas já casou com outra
    // transação do PRÓPRIO arquivo via multiset exato (ver ChaveOcorrencia) — sem isso, o excedente
    // de duas transações idênticas no arquivo (uma já no razão, outra genuinamente nova) acharia a
    // mesma instância do razão como "duplicata manual" da segunda, quando ela já foi "gasta" pela
    // primeira. Descontar aqui é o que impede a mescla indevida.
    private static List<ItemSemFitId> ColetarItensSemFitId(
        List<RegistroDiario> registros, Dictionary<(DateOnly, string, decimal, string), int> consumidoPelaHeuristica)
    {
        var restante = new Dictionary<(DateOnly, string, decimal, string), int>(consumidoPelaHeuristica);
        var lista = new List<ItemSemFitId>();
        foreach (var r in registros)
        {
            foreach (var e in r.Entradas.Where(e => e.FitId == null && !e.Provisoria))
            {
                var chave = ChaveOcorrencia(r.Data, "Entrada", e.Valor, e.Descricao);
                if (restante.TryGetValue(chave, out var qtd) && qtd > 0) { restante[chave] = qtd - 1; continue; }
                lista.Add(new ItemSemFitId(r, "Entrada", e.Id, e.Valor, e.Descricao, r.Data));
            }
            foreach (var s in r.Saidas.Where(s => s.FitId == null && !s.Provisoria))
            {
                var chave = ChaveOcorrencia(r.Data, "Saida", s.Valor, s.Descricao);
                if (restante.TryGetValue(chave, out var qtd) && qtd > 0) { restante[chave] = qtd - 1; continue; }
                lista.Add(new ItemSemFitId(r, "Saida", s.Id, s.Valor, s.Descricao, r.Data));
            }
        }
        return lista;
    }

    private static Dictionary<(DateOnly, string, decimal, string), int> ContarConsumidoPelaHeuristica(
        List<TransacaoParseada> parseadas, HashSet<int> jaImportadas)
    {
        var mapa = new Dictionary<(DateOnly, string, decimal, string), int>();
        foreach (var t in parseadas.Where(t => t.FitId == null && jaImportadas.Contains(t.Indice)))
        {
            var chave = ChaveOcorrencia(t.Data, t.Tipo, t.Valor, t.Descricao);
            mapa[chave] = mapa.GetValueOrDefault(chave) + 1;
        }
        return mapa;
    }

    private static HashSet<(DateOnly, string, decimal, string)> ColetarChavesImportadas(List<TransacaoImportada> historico) =>
        historico.Where(t => t.FitId == null)
            .Select(t => ChaveOcorrencia(t.Data, t.Tipo, t.Valor, t.Descricao))
            .ToHashSet();

    // Pool em memória (igual ColetarProvisorias) — cada lançamento sem FitId só pode ser
    // reivindicado por UMA transação do arquivo, a de melhor match (menor diferença de dias, depois
    // de valor), pra não sugerir o mesmo manual pra duas linhas do arquivo.
    private static Dictionary<int, (ItemSemFitId Candidato, bool VeioDeImportacaoAnterior)> IdentificarCandidatosDuplicata(
        List<TransacaoParseada> transacoes, List<RegistroDiario> registros,
        HashSet<(DateOnly, string, decimal, string)> chavesImportadasAntes,
        Dictionary<(DateOnly, string, decimal, string), int> consumidoPelaHeuristica)
    {
        var disponiveis = ColetarItensSemFitId(registros, consumidoPelaHeuristica);
        var resultado = new Dictionary<int, (ItemSemFitId, bool)>();

        foreach (var t in transacoes.OrderBy(t => t.Indice))
        {
            var candidatas = disponiveis.Where(c => c.Tipo == t.Tipo
                && Math.Abs(c.Valor - t.Valor) < 0.01m
                && Math.Abs(c.Data.DayNumber - t.Data.DayNumber) <= 2)
                .ToList();
            if (candidatas.Count == 0) continue;

            var melhor = candidatas
                .OrderBy(c => Math.Abs(c.Data.DayNumber - t.Data.DayNumber))
                .ThenBy(c => Math.Abs(c.Valor - t.Valor))
                .First();

            var veioDeImportacaoAnterior = chavesImportadasAntes.Contains(ChaveOcorrencia(melhor.Data, melhor.Tipo, melhor.Valor, melhor.Descricao));
            resultado[t.Indice] = (melhor, veioDeImportacaoAnterior);
            disponiveis.Remove(melhor);
        }

        return resultado;
    }

    // Move/atualiza o item manual em vez de criar um novo: mantém Categoria/TipoCusto/vínculos/
    // Valor do manual, grava FitId/Descricao do banco. Quando a data muda, move o item entre os
    // dois RegistroDiario (saldo é só recalculado no fim, de uma vez, por RecalcularSaldosDesde).
    private static void AplicarMergeComManual(
        RegistroDiario registroDestino, TransacaoParseada t, ItemSemFitId candidato, ref DateOnly? menorDataAfetadaPorMerge)
    {
        var moveu = candidato.Registro.Data != t.Data;

        if (candidato.Tipo == "Entrada")
        {
            var item = candidato.Registro.Entradas.First(e => e.Id == candidato.ItemId);
            item.FitId = t.FitId;
            item.Descricao = t.Descricao;
            if (moveu)
            {
                candidato.Registro.Entradas = candidato.Registro.Entradas.Where(e => e.Id != item.Id).ToList();
                registroDestino.Entradas = new List<ItemFinanceiro>(registroDestino.Entradas) { item };
            }
            else
            {
                candidato.Registro.Entradas = new List<ItemFinanceiro>(candidato.Registro.Entradas);
            }
        }
        else
        {
            var item = candidato.Registro.Saidas.First(s => s.Id == candidato.ItemId);
            item.FitId = t.FitId;
            item.Descricao = t.Descricao;
            if (moveu)
            {
                candidato.Registro.Saidas = candidato.Registro.Saidas.Where(s => s.Id != item.Id).ToList();
                registroDestino.Saidas = new List<ItemFinanceiroSaida>(registroDestino.Saidas) { item };
            }
            else
            {
                candidato.Registro.Saidas = new List<ItemFinanceiroSaida>(candidato.Registro.Saidas);
            }
        }

        candidato.Registro.SalvoEm = DateTime.UtcNow;
        registroDestino.SalvoEm = DateTime.UtcNow;

        if (moveu)
        {
            var menor = candidato.Registro.Data < t.Data ? candidato.Registro.Data : t.Data;
            if (!menorDataAfetadaPorMerge.HasValue || menor < menorDataAfetadaPorMerge.Value)
                menorDataAfetadaPorMerge = menor;
        }
    }

    // ── Importar (lança direto no RegistroDiario — afeta saldo na hora) ──────────
    public async Task<ResultadoImportacaoDto> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim, List<ResolucaoDuplicataDto>? resolucoesDuplicatas = null)
    {
        var conta = await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        var parseadas = ParsearArquivoValidando(arquivo);

        if (dataInicio.HasValue) parseadas = parseadas.Where(t => t.Data >= dataInicio.Value).ToList();
        if (dataFim.HasValue) parseadas = parseadas.Where(t => t.Data <= dataFim.Value).ToList();

        var registros = (await _registroRepo.ListarPorContaAsync(contaBancariaId))
            .Where(r => !r.Excluido)
            .ToList();
        var historico = await _importRepo.ListarPorContaAsync(contaBancariaId);
        var ignoradas = historico.Where(t => t.Status == "Ignorada").ToList();
        var chavesImportadasAntes = ColetarChavesImportadas(historico);

        var jaImportadas = IdentificarJaImportadas(parseadas, registros, ignoradas);
        var aImportar = parseadas.Where(t => !jaImportadas.Contains(t.Indice)).ToList();

        if (aImportar.Count == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Nenhuma transação nova para importar no intervalo selecionado — todas já foram importadas antes.");

        var conciliaveis = IdentificarConciliaveis(aImportar, registros);
        var consumidoPelaHeuristica = ContarConsumidoPelaHeuristica(parseadas, jaImportadas);
        var candidatosDuplicata = IdentificarCandidatosDuplicata(
            aImportar.Where(t => !conciliaveis.Contains(t.Indice)).ToList(), registros, chavesImportadasAntes, consumidoPelaHeuristica);
        var resolucoesPorIndice = (resolucoesDuplicatas ?? new List<ResolucaoDuplicataDto>())
            .ToDictionary(r => r.TransacaoIndice, r => r.Acao);
        DateOnly? menorDataAfetadaPorMerge = null;
        var totalMescladasComManual = 0;
        var totalDuplicatasEntreArquivosSinalizadas = 0;

        var registrosPorData = registros.ToDictionary(r => r.Data);
        var categoriasPorNome = (await _categoriaRepo.ListarTodasAsync()).ToDictionary(c => c.Nome, c => c.Tipo);
        var regrasPorTipo = (await _regraRepo.ListarAtivasPorContaAsync(contaBancariaId))
            .GroupBy(r => r.Tipo)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Ordem).ToList());
        var auditoria = new List<TransacaoImportada>();
        // (data, itemId) dos itens que uma regra de Transferência casou — convertidos só depois que
        // TODOS os registros do lote já foram salvos (ConverterLancamentoAsync precisa achar o
        // lançamento por conta+data já persistido).
        var aConverterEmTransferencia = new List<(DateOnly Data, Guid ItemId, RegraCategorizacao Regra)>();
        var pendentes = 0;
        var categorizadasPorRegra = 0;
        var conciliadasTransferencia = 0;
        var ambiguasTransferencia = 0;

        // Provisórias já existentes nesta conta (contrapartida de Transferência criada antes do
        // extrato real chegar — ver TransferenciaService.ConverterLancamentoAsync). Cada uma só pode
        // casar com UMA transação do arquivo — é removida do pool assim que conciliada.
        var provisorias = ColetarProvisorias(registros);
        var registrosTocadosPorConciliacao = new HashSet<RegistroDiario>();

        foreach (var grupo in aImportar.GroupBy(t => t.Data).OrderBy(g => g.Key))
        {
            var data = grupo.Key;
            bool novo;
            RegistroDiario registro;
            if (registrosPorData.TryGetValue(data, out var existente))
            {
                registro = existente;
                novo = false;
            }
            else
            {
                var anterior = registros
                    .Where(r => r.Data < data)
                    .OrderByDescending(r => r.Data)
                    .FirstOrDefault();
                var saldoBase = anterior?.SaldoFinal ?? conta.SaldoInicial;

                registro = new RegistroDiario
                {
                    Id = Guid.NewGuid(),
                    ClienteId = conta.ClienteId,
                    ContaBancariaId = conta.Id,
                    Data = data,
                    Inicio = saldoBase,
                    Entradas = new(),
                    Saidas = new(),
                    ContasReceber = new(),
                    ContasPagar = new(),
                    SaldoFinal = saldoBase,
                    CriadoEm = DateTime.UtcNow,
                    SalvoEm = DateTime.UtcNow,
                    UsuarioAtualizacao = "importacao",
                };
                novo = true;
                registrosPorData[data] = registro;
                registros.Add(registro);
            }

            foreach (var t in grupo)
            {
                var candidatas = provisorias.Where(p => p.Tipo == t.Tipo
                    && Math.Abs(p.Valor - t.Valor) < 0.01m
                    && DiferencaDiasUteis(p.Registro.Data, t.Data) <= 2)
                    .ToList();

                if (candidatas.Count == 1)
                {
                    // Concilia: a provisória assume os dados reais (descrição, FitId) em vez de criar
                    // um lançamento novo — é isso que impede o Pix de virar 2 entradas (bug original).
                    ReconciliarProvisoria(candidatas[0], t);
                    provisorias.Remove(candidatas[0]);
                    registrosTocadosPorConciliacao.Add(candidatas[0].Registro);
                    conciliadasTransferencia++;
                    auditoria.Add(new TransacaoImportada
                    {
                        Id = Guid.NewGuid(), ContaBancariaId = conta.Id, ClienteId = conta.ClienteId,
                        Data = t.Data, Valor = t.Valor, Descricao = t.Descricao, FitId = t.FitId,
                        Tipo = t.Tipo, Status = "Confirmada", ImportadoEm = DateTime.UtcNow,
                    });
                    continue;
                }
                if (candidatas.Count > 1)
                {
                    // Mais de uma candidata pro mesmo valor/sentido/janela — não decide sozinho
                    // (ex.: dois Pix iguais no mesmo dia). Importa normal, pendente de classificação.
                    ambiguasTransferencia++;
                }

                if (candidatosDuplicata.TryGetValue(t.Indice, out var duplicata))
                {
                    if (duplicata.VeioDeImportacaoAnterior)
                    {
                        // Provável duplicata entre dois arquivos (ex.: CSV + OFX do mesmo banco) —
                        // não decide sozinho, só sinaliza; segue o fluxo normal abaixo e importa.
                        totalDuplicatasEntreArquivosSinalizadas++;
                    }
                    else if (resolucoesPorIndice.GetValueOrDefault(t.Indice, "Mesclar") == "Mesclar")
                    {
                        // Duplicata de lançamento MANUAL — padrão é mesclar: mantém Categoria/
                        // TipoCusto/vínculos/Valor do manual, só grava FitId/Descrição/Data do banco.
                        AplicarMergeComManual(registro, t, duplicata.Candidato, ref menorDataAfetadaPorMerge);
                        totalMescladasComManual++;
                        auditoria.Add(new TransacaoImportada
                        {
                            Id = Guid.NewGuid(), ContaBancariaId = conta.Id, ClienteId = conta.ClienteId,
                            Data = t.Data, Valor = t.Valor, Descricao = t.Descricao, FitId = t.FitId,
                            Tipo = t.Tipo, Status = "Confirmada", ImportadoEm = DateTime.UtcNow,
                        });
                        continue;
                    }
                    // "ImportarComoNovo": segue o fluxo normal abaixo, cria um lançamento novo.
                }

                var regraCorrespondente = regrasPorTipo.TryGetValue(t.Tipo, out var regrasDoTipo)
                    ? regrasDoTipo.FirstOrDefault(r => DescricaoMatcher.Casa(r.CriterioTipo, r.CriterioValor, t.Descricao))
                    : null;
                var itemId = Guid.NewGuid();

                if (t.Tipo == "Entrada")
                {
                    // Entrada não tem sugestão por palavra-chave (venda, serviço, transferência
                    // recebida e resgate de investimento têm o mesmo verbo "recebido"/"pix" no
                    // extrato — só o usuário sabe distinguir, ou uma regra que ele mesmo criou).
                    // Regra de Transferência entra pendente por ora — a conversão de verdade só
                    // acontece depois que este registro for salvo (precisa de um Id persistido).
                    var categorizadaPorRegra = regraCorrespondente is { AcaoTipo: "Categoria" };
                    var pendenteEntrada = !categorizadaPorRegra;
                    if (categorizadaPorRegra) categorizadasPorRegra++; else pendentes++;

                    registro.Entradas.Add(new ItemFinanceiro
                    {
                        Id = itemId,
                        Descricao = t.Descricao,
                        Valor = t.Valor,
                        FitId = t.FitId,
                        Categoria = categorizadaPorRegra ? regraCorrespondente!.Categoria : null,
                        TipoCusto = categorizadaPorRegra && regraCorrespondente!.Categoria != null
                            && categoriasPorNome.TryGetValue(regraCorrespondente.Categoria, out var tcEntrada) ? tcEntrada : null,
                        RegraCategorizacaoId = categorizadaPorRegra ? regraCorrespondente!.Id : null,
                        PendenteCategorizacao = pendenteEntrada,
                    });
                    registro.SaldoFinal += t.Valor;
                }
                else
                {
                    string categoriaFinal; string? tipoCustoFinal; bool pendente; Guid? regraIdAplicada = null;

                    if (regraCorrespondente is { AcaoTipo: "Categoria" })
                    {
                        categoriaFinal = regraCorrespondente.Categoria ?? string.Empty;
                        tipoCustoFinal = regraCorrespondente.Categoria != null && categoriasPorNome.TryGetValue(regraCorrespondente.Categoria, out var tcRegra) ? tcRegra : null;
                        pendente = false;
                        regraIdAplicada = regraCorrespondente.Id;
                        categorizadasPorRegra++;
                    }
                    else if (regraCorrespondente is { AcaoTipo: "Transferencia" })
                    {
                        // Fica pendente por ora — a conversão de verdade (Fase 2) só acontece depois
                        // que este registro estiver salvo, e é ela quem marca Categoria/PendenteCategorizacao.
                        categoriaFinal = string.Empty;
                        tipoCustoFinal = null;
                        pendente = true;
                        pendentes++;
                    }
                    else
                    {
                        var categoriaSugerida = SugerirCategoria(t.Tipo, t.Descricao);
                        categoriaFinal = categoriaSugerida ?? string.Empty;
                        pendente = categoriaSugerida == null;
                        if (pendente) pendentes++;
                        // A sugestão por palavra-chave devolve um nome de categoria já cadastrado no
                        // Plano de Contas — sem resolver o TipoCusto aqui, o lançamento nunca entraria
                        // como custo fixo/variável no DRE mesmo tendo uma categoria "certa" atribuída.
                        tipoCustoFinal = categoriaSugerida != null && categoriasPorNome.TryGetValue(categoriaSugerida, out var tc) ? tc : null;
                    }

                    registro.Saidas.Add(new ItemFinanceiroSaida
                    {
                        Id = itemId,
                        Descricao = t.Descricao,
                        Valor = t.Valor,
                        Categoria = categoriaFinal,
                        Subcategoria = string.Empty,
                        TipoCusto = tipoCustoFinal,
                        FitId = t.FitId,
                        RegraCategorizacaoId = regraIdAplicada,
                        PendenteCategorizacao = pendente,
                    });
                    registro.SaldoFinal -= t.Valor;
                }

                if (regraCorrespondente is { AcaoTipo: "Transferencia" })
                    aConverterEmTransferencia.Add((data, itemId, regraCorrespondente));

                auditoria.Add(new TransacaoImportada
                {
                    Id = Guid.NewGuid(),
                    ContaBancariaId = conta.Id,
                    ClienteId = conta.ClienteId,
                    Data = t.Data,
                    Valor = t.Valor,
                    Descricao = t.Descricao,
                    FitId = t.FitId,
                    Tipo = t.Tipo,
                    Status = "Confirmada",
                    ImportadoEm = DateTime.UtcNow,
                });
            }

            registro.SalvoEm = DateTime.UtcNow;
            registro.AtualizadoEm = DateTime.UtcNow;
            if (novo) await _registroRepo.AdicionarAsync(registro);
            else await _registroRepo.AtualizarAsync(registro);
        }

        // Registros tocados só pela conciliação (a provisória vivia num dia fora do lote de
        // dias que o loop acima processou) ainda não foram persistidos — salva agora.
        foreach (var r in registrosTocadosPorConciliacao)
            await _registroRepo.AtualizarAsync(r);

        // Mesclas que MOVERAM um item manual pra outro dia: o dia de origem só foi tocado aqui, não
        // pelo loop principal — recalcula saldo de toda a conta a partir do dia mais antigo afetado
        // (origem ou destino) de uma vez, em vez de tentar propagar delta a delta (poderiam ter
        // ocorrido duas mesclas tocando dias diferentes na mesma janela).
        if (menorDataAfetadaPorMerge.HasValue)
        {
            var saldoCorrente = registros
                .Where(r => r.Data < menorDataAfetadaPorMerge.Value)
                .OrderByDescending(r => r.Data)
                .Select(r => r.SaldoFinal)
                .FirstOrDefault(conta.SaldoInicial);

            foreach (var r in registros.Where(r => r.Data >= menorDataAfetadaPorMerge.Value).OrderBy(r => r.Data))
            {
                r.Inicio = saldoCorrente;
                r.SaldoFinal = RegistroService.CalcularSaldoFinal(r.Inicio, r.Entradas, r.Saidas, 0m);
                saldoCorrente = r.SaldoFinal;
                await _registroRepo.AtualizarAsync(r);
            }
        }

        await _importRepo.AdicionarLoteAsync(auditoria);

        // Regras de Transferência — só agora, com todos os registros do lote já persistidos (o
        // lançamento precisa existir de verdade, com Id salvo, pra ConverterLancamentoAsync achá-lo
        // por conta+data). Sequencial: cada conversão lê e grava o RegistroDiario do dia inteiro.
        foreach (var (data, itemId, regra) in aConverterEmTransferencia)
        {
            try
            {
                await _transferenciaService.ConverterLancamentoAsync(new ConverterLancamentoEmTransferenciaDto
                {
                    ContaId = contaBancariaId,
                    LancamentoId = itemId,
                    Data = data,
                    Tipo = regra.Tipo,
                    ContaContrapartidaId = regra.ContaContrapartidaId!.Value,
                    RegraCategorizacaoId = regra.Id,
                }, usuarioLogadoId, perfil);
                categorizadasPorRegra++;
                pendentes--;
            }
            catch (ApiException)
            {
                // Fica pendente — o usuário categoriza manualmente na tela de Categorizar Lançamentos.
            }
        }

        // Fase 1.2/1.7: depois da deduplicação, roda o motor de sugestão de vínculo no intervalo do
        // próprio arquivo — a tela resume "X contas previstas encontradas: confirmar todas / revisar".
        var totalSugestoesVinculo = 0;
        try
        {
            var dataMin = aImportar.Min(t => t.Data);
            var dataMax = aImportar.Max(t => t.Data);
            totalSugestoesVinculo = (await _conciliacaoService.ListarSugestoesAsync(
                conta.ClienteId, dataMin, dataMax, usuarioLogadoId, perfil, contaBancariaId)).Count;
        }
        catch (ApiException)
        {
            // Nunca falha a importação (já persistida) por causa da sugestão — só não resume.
        }

        return new ResultadoImportacaoDto
        {
            TotalImportadas = aImportar.Count - conciliadasTransferencia - totalMescladasComManual,
            TotalPendentesCategorizacao = pendentes,
            TotalCategorizadasPorRegra = categorizadasPorRegra,
            TotalConciliadasTransferencia = conciliadasTransferencia,
            TotalAmbiguasTransferencia = ambiguasTransferencia,
            TotalEntradas = aImportar.Where(t => t.Tipo == "Entrada").Sum(t => t.Valor),
            TotalSaidas = aImportar.Where(t => t.Tipo == "Saida").Sum(t => t.Valor),
            TotalMescladasComManual = totalMescladasComManual,
            TotalDuplicatasEntreArquivosSinalizadas = totalDuplicatasEntreArquivosSinalizadas,
            TotalSugestoesVinculo = totalSugestoesVinculo,
        };
    }

    // ── Categorização pendente (lançamentos já reais, só falta a categoria) ──────
    public async Task<List<PendenteCategorizacaoDto>> ListarPendentesCategorizacaoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil)
    {
        await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        var registros = await _registroRepo.ListarPorContaAsync(contaBancariaId);

        return registros
            .Where(r => !r.Excluido)
            .OrderBy(r => r.Data)
            .SelectMany(r => r.Entradas
                .Where(e => e.PendenteCategorizacao)
                .Select(e => new PendenteCategorizacaoDto
                {
                    Id = e.Id,
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = e.Descricao,
                    Valor = e.Valor,
                    Tipo = "Entrada",
                })
                .Concat(r.Saidas
                    .Where(s => s.PendenteCategorizacao)
                    .Select(s => new PendenteCategorizacaoDto
                    {
                        Id = s.Id,
                        Data = r.Data.ToString("yyyy-MM-dd"),
                        Descricao = s.Descricao,
                        Valor = s.Valor,
                        Tipo = "Saida",
                    })))
            .ToList();
    }

    public async Task AtualizarCategoriasAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, AtualizarCategoriaDto dto)
    {
        await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);

        // O TipoCusto (Receita/CustoFixo/CustoVariavel) é resolvido aqui a partir do Plano de
        // Contas, nunca confiado ao payload do cliente — é ele, e não o nome da categoria, que
        // o DRE/Indicadores usam pra classificar o lançamento (ver LancamentoFiltro/MetricasService).
        // Sem isso, categorizar (ou criar categoria nova) marcava o nome certo mas o lançamento
        // continuava sem TipoCusto, "perdendo" a classificação escolhida.
        var categoriasPorNome = (await _categoriaRepo.ListarTodasAsync()).ToDictionary(c => c.Nome, c => c.Tipo);

        var porData = dto.Itens
            .Where(i => DateOnly.TryParse(i.Data, out _))
            .GroupBy(i => DateOnly.Parse(i.Data))
            .ToList();

        foreach (var grupo in porData)
        {
            var registro = await _registroRepo.ObterPorContaEDataAsync(contaBancariaId, grupo.Key);
            if (registro == null) continue;

            foreach (var item in grupo)
            {
                var saida = registro.Saidas.FirstOrDefault(s => s.Id == item.Id);
                if (saida != null)
                {
                    saida.Categoria = item.Categoria;
                    if (categoriasPorNome.TryGetValue(item.Categoria, out var tipoCustoSaida))
                        saida.TipoCusto = tipoCustoSaida;
                    saida.PendenteCategorizacao = false;
                    continue;
                }

                var entrada = registro.Entradas.FirstOrDefault(e => e.Id == item.Id);
                if (entrada == null) continue;
                entrada.Categoria = item.Categoria;
                if (categoriasPorNome.TryGetValue(item.Categoria, out var tipoCustoEntrada))
                    entrada.TipoCusto = tipoCustoEntrada;
                entrada.PendenteCategorizacao = false;
            }

            // Reatribui as listas — Entradas/Saidas são jsonb sem value comparer configurado, então
            // o EF só detecta a mudança se a referência da lista mudar, não se um item dela for só mutado.
            registro.Entradas = new List<ItemFinanceiro>(registro.Entradas);
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
            registro.SalvoEm = DateTime.UtcNow;
            registro.AtualizadoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
        }
    }

    public async Task<ExcluirLancamentoResultDto> ExcluirLancamentoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, ExcluirLancamentoDto dto)
    {
        var conta = await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        if (!DateOnly.TryParse(dto.Data, out var data))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Data inválida.", "data");

        var registro = await _registroRepo.ObterPorContaEDataAsync(contaBancariaId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");

        var saida = registro.Saidas.FirstOrDefault(s => s.Id == dto.Id);
        var entrada = saida == null ? registro.Entradas.FirstOrDefault(e => e.Id == dto.Id) : null;
        if (saida == null && entrada == null)
            throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");

        // Ponta de Transferência: as duas pontas são o mesmo evento financeiro — excluir só uma
        // deixaria a outra órfã, então delega pro TransferenciaService excluir as duas juntas.
        var transferenciaId = saida?.TransferenciaId ?? entrada?.TransferenciaId;
        if (transferenciaId.HasValue)
        {
            await _transferenciaService.ExcluirAsync(transferenciaId.Value, usuarioLogadoId, perfil);
            return new ExcluirLancamentoResultDto { TransferenciaExcluida = true };
        }

        // Reabre qualquer título (em qualquer conta do cliente) cuja baixa foi vinculada a este
        // lançamento, em vez de gerar um lançamento novo — ver RegistroService/ContaProvisionada.
        string? tituloReaberto = null;
        foreach (var r in await _registroRepo.ListarPorClienteAsync(conta.ClienteId))
        {
            var pendenciaReceber = r.ContasReceber.FirstOrDefault(p => p.LancamentoVinculadoId == dto.Id);
            var pendenciaPagar = r.ContasPagar.FirstOrDefault(p => p.LancamentoVinculadoId == dto.Id);
            if (pendenciaReceber == null && pendenciaPagar == null) continue;

            if (pendenciaReceber != null)
            {
                tituloReaberto = pendenciaReceber.Descricao;
                pendenciaReceber.Pago = false;
                pendenciaReceber.DataBaixa = null;
                pendenciaReceber.LancamentoVinculadoId = null;
                r.ContasReceber = new List<ContaProvisionada>(r.ContasReceber);
            }
            if (pendenciaPagar != null)
            {
                tituloReaberto = pendenciaPagar.Descricao;
                pendenciaPagar.Pago = false;
                pendenciaPagar.DataBaixa = null;
                pendenciaPagar.LancamentoVinculadoId = null;
                r.ContasPagar = new List<ContaProvisionada>(r.ContasPagar);
            }
            r.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(r);
        }

        if (saida != null)
        {
            registro.Saidas = registro.Saidas.Where(s => s.Id != dto.Id).ToList();
            registro.SaldoFinal += saida.Valor;
            await MarcarIgnoradaSeImportadaAsync(contaBancariaId, data, "Saida", saida.Valor, saida.Descricao, saida.FitId);
        }
        else
        {
            registro.Entradas = registro.Entradas.Where(e => e.Id != dto.Id).ToList();
            registro.SaldoFinal -= entrada!.Valor;
            await MarcarIgnoradaSeImportadaAsync(contaBancariaId, data, "Entrada", entrada.Valor, entrada.Descricao, entrada.FitId);
        }

        registro.SalvoEm = DateTime.UtcNow;
        registro.AtualizadoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);

        return new ExcluirLancamentoResultDto { TituloReaberto = tituloReaberto };
    }

    // Marca a transação de origem como "Ignorada" no histórico de importação (TransacaoImportada),
    // se houver uma — é o que impede o item excluído de "ressuscitar" numa reimportação do mesmo
    // arquivo (ver IdentificarJaImportadas). Sem FitId (CSV/XLSX), casa pela mesma heurística de
    // data+tipo+valor+descrição normalizada usada no resto da deduplicação.
    private async Task MarcarIgnoradaSeImportadaAsync(
        Guid contaBancariaId, DateOnly data, string tipo, decimal valor, string descricao, string? fitId)
    {
        var candidatas = (await _importRepo.ListarPorContaAsync(contaBancariaId))
            .Where(t => t.Data == data && t.Tipo == tipo && t.Status != "Ignorada");

        var alvo = fitId != null
            ? candidatas.FirstOrDefault(t => t.FitId == fitId)
            : candidatas.FirstOrDefault(t => t.FitId == null
                && Math.Round(t.Valor, 2) == Math.Round(valor, 2)
                && NormalizarDescricao(t.Descricao) == NormalizarDescricao(descricao));

        if (alvo == null) return;
        alvo.Status = "Ignorada";
        await _importRepo.AtualizarAsync(alvo);
    }

    // ── Conciliação com contrapartida provisória de Transferência (bug: Pix duplicado) ──────────
    // Ver TransferenciaService.ConverterLancamentoAsync (cria a provisória) e Models/ItemFinanceiro.
    private sealed record ProvisoriaCandidata(RegistroDiario Registro, string Tipo, Guid ItemId, decimal Valor);

    private static List<ProvisoriaCandidata> ColetarProvisorias(List<RegistroDiario> registros)
    {
        var lista = new List<ProvisoriaCandidata>();
        foreach (var r in registros)
        {
            lista.AddRange(r.Entradas.Where(e => e.Provisoria).Select(e => new ProvisoriaCandidata(r, "Entrada", e.Id, e.Valor)));
            lista.AddRange(r.Saidas.Where(s => s.Provisoria).Select(s => new ProvisoriaCandidata(r, "Saida", s.Id, s.Valor)));
        }
        return lista;
    }

    private static void ReconciliarProvisoria(ProvisoriaCandidata candidata, TransacaoParseada t)
    {
        if (candidata.Tipo == "Entrada")
        {
            var item = candidata.Registro.Entradas.First(e => e.Id == candidata.ItemId);
            item.Descricao = t.Descricao;
            item.FitId = t.FitId;
            item.Provisoria = false;
            candidata.Registro.Entradas = new List<ItemFinanceiro>(candidata.Registro.Entradas);
        }
        else
        {
            var item = candidata.Registro.Saidas.First(s => s.Id == candidata.ItemId);
            item.Descricao = t.Descricao;
            item.FitId = t.FitId;
            item.Provisoria = false;
            candidata.Registro.Saidas = new List<ItemFinanceiroSaida>(candidata.Registro.Saidas);
        }
        candidata.Registro.SalvoEm = DateTime.UtcNow;
    }

    // Conta dias úteis (seg-sex, sem calendário de feriados) estritamente ENTRE as duas datas —
    // aproximação razoável de "±2 dias úteis" sem precisar de uma tabela de feriados no sistema.
    private static int DiferencaDiasUteis(DateOnly a, DateOnly b)
    {
        if (a == b) return 0;
        var (inicio, fim) = a < b ? (a, b) : (b, a);
        var dias = 0;
        for (var d = inicio.AddDays(1); d < fim; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dias++;
        return dias;
    }

    // ── Sugestão de categoria por palavra-chave ────────────────────────────────
    // Dicionário simples e estático (palavra-chave → categoria já existente em /api/categorias).
    // Só sugere para Saídas: o usuário confirma/troca depois se quiser.
    private static readonly (string Palavra, string Categoria)[] SugestoesPorPalavraChave =
    {
        ("posto", "Manutenção"),
        ("combust", "Manutenção"),
        ("gasolina", "Manutenção"),
        ("etanol", "Manutenção"),
        ("mercado", "Insumos/Mercadoria"),
        ("supermercado", "Insumos/Mercadoria"),
        ("atacad", "Insumos/Mercadoria"),
        ("farmacia", "Benefícios"),
        ("farmácia", "Benefícios"),
        ("drogaria", "Benefícios"),
        ("aluguel", "Aluguel"),
        ("energia", "Energia/Água/Internet"),
        ("eletrica", "Energia/Água/Internet"),
        ("agua", "Energia/Água/Internet"),
        ("internet", "Energia/Água/Internet"),
        ("telefonia", "Energia/Água/Internet"),
        ("salario", "Salários/Folha"),
        ("salário", "Salários/Folha"),
        ("folha de pagamento", "Salários/Folha"),
        ("simples nacional", "Simples/DAS"),
        ("das ", "Simples/DAS"),
        ("tarifa", "Tarifas bancárias"),
        ("juros", "Juros"),
        ("seguro", "Seguros"),
        ("publicidade", "Mídia paga"),
        ("ads", "Mídia paga"),
        ("papelaria", "Material de Escritório"),
        ("escritorio", "Material de Escritório"),
    };

    private static string? SugerirCategoria(string tipo, string descricao)
    {
        if (tipo != "Saida" || string.IsNullOrWhiteSpace(descricao))
            return null;

        var descNormalizada = descricao.ToLowerInvariant();
        foreach (var (palavra, categoria) in SugestoesPorPalavraChave)
            if (descNormalizada.Contains(palavra))
                return categoria;

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<ContaBancaria> ObterContaComAcesso(Guid contaBancariaId, Guid usuarioId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (perfil == "cliente" && usuarioId != conta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return conta;
    }

    private static List<TransacaoParseada> ParsearArquivoValidando(IFormFile arquivo)
    {
        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (ext is not ".ofx" and not ".csv" and not ".xlsx")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Formato inválido. Envie um arquivo .ofx, .csv ou .xlsx.");

        List<TransacaoParseada> parseadas;
        using (var stream = arquivo.OpenReadStream())
        {
            try
            {
                parseadas = ext switch
                {
                    ".ofx" => OfxParser.Parse(stream)
                        .Select((t, i) => new TransacaoParseada(i, t.Data, t.Valor, t.Descricao, t.Tipo, t.FitId))
                        .ToList(),
                    ".xlsx" => XlsxParser.Parse(stream)
                        .Select((t, i) => new TransacaoParseada(i, t.Data, t.Valor, t.Descricao, t.Tipo, null))
                        .ToList(),
                    _ => CsvParser.Parse(stream)
                        .Select((t, i) => new TransacaoParseada(i, t.Data, t.Valor, t.Descricao, t.Tipo, null))
                        .ToList(),
                };
            }
            catch (InvalidOperationException ex)
            {
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, ex.Message);
            }
        }

        if (parseadas.Count == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Nenhuma transação encontrada no arquivo. Verifique o formato.");

        return parseadas;
    }

    // "Já importada" é decidido contra o LANÇAMENTO REAL (Entradas/Saidas dos registros não
    // excluídos), nunca contra um histórico à parte — assim, se o registro foi excluído (o usuário
    // desfez o dia), a transação deixa de contar como já importada e pode ser trazida de novo.
    //
    // OFX: mesmo FITID já presente num lançamento real (identificador único — não precisa de mais nada).
    //
    // CSV/XLSX (sem FITID): heurística de data + valor + descrição — mas por CONTAGEM de
    // ocorrências, não por simples existência. Duas transações legítimas idênticas no mesmo dia
    // (duas consultas de mesmo valor, dois Pix iguais) só viram duplicata até o limite de quantas
    // JÁ EXISTEM de fato no razão da conta; a partir daí, ocorrências extras no arquivo entram
    // como novas transações. Um `.Any()` simples (o que existia antes) marcaria a segunda
    // ocorrência legítima como duplicata só por ela parecer com a primeira — a diferença de
    // quantidade (multiset) é o que resolve isso sem precisar perguntar nada ao usuário.
    private static HashSet<int> IdentificarJaImportadas(
        List<TransacaoParseada> parseadas, List<RegistroDiario> registrosAtivos, List<TransacaoImportada> ignoradas)
    {
        var jaImportadas = new HashSet<int>();

        foreach (var t in parseadas.Where(t => t.FitId != null))
        {
            var existe = t.Tipo == "Entrada"
                ? registrosAtivos.Any(r => r.Entradas.Any(e => e.FitId == t.FitId))
                : registrosAtivos.Any(r => r.Saidas.Any(s => s.FitId == t.FitId));
            if (existe) jaImportadas.Add(t.Indice);
        }

        var disponivelNoRazao = new Dictionary<(DateOnly Data, string Tipo, decimal Valor, string Descricao), int>();
        foreach (var r in registrosAtivos)
        {
            foreach (var e in r.Entradas)
                IncrementarOcorrencia(disponivelNoRazao, r.Data, "Entrada", e.Valor, e.Descricao);
            foreach (var s in r.Saidas)
                IncrementarOcorrencia(disponivelNoRazao, r.Data, "Saida", s.Valor, s.Descricao);
        }

        foreach (var t in parseadas.Where(t => t.FitId == null))
        {
            var chave = ChaveOcorrencia(t.Data, t.Tipo, t.Valor, t.Descricao);
            if (disponivelNoRazao.TryGetValue(chave, out var restante) && restante > 0)
            {
                disponivelNoRazao[chave] = restante - 1;
                jaImportadas.Add(t.Indice);
            }
        }

        // Lançamentos que o usuário excluiu do extrato não contam mais como "no razão" (os dois
        // blocos acima não os encontram mais), mas continuam marcados "Ignorada" no histórico de
        // importação justamente pra não reaparecer aqui — ver ExcluirLancamentoAsync.
        foreach (var t in parseadas.Where(t => !jaImportadas.Contains(t.Indice)))
        {
            var ignorada = t.FitId != null
                ? ignoradas.Any(i => i.FitId == t.FitId)
                : ignoradas.Any(i => i.FitId == null && i.Data == t.Data && i.Tipo == t.Tipo
                    && Math.Round(i.Valor, 2) == Math.Round(t.Valor, 2)
                    && NormalizarDescricao(i.Descricao) == NormalizarDescricao(t.Descricao));
            if (ignorada) jaImportadas.Add(t.Indice);
        }

        return jaImportadas;
    }

    private static void IncrementarOcorrencia(
        Dictionary<(DateOnly, string, decimal, string), int> mapa, DateOnly data, string tipo, decimal valor, string descricao)
    {
        var chave = ChaveOcorrencia(data, tipo, valor, descricao);
        mapa[chave] = mapa.GetValueOrDefault(chave) + 1;
    }

    // Arredondar o valor pra 2 casas equivale à antiga tolerância de ±0,01 usada na comparação
    // (valores monetários já vêm nessa precisão) — e permite usar a tupla como chave de dicionário.
    private static (DateOnly, string, decimal, string) ChaveOcorrencia(DateOnly data, string tipo, decimal valor, string descricao) =>
        (data, tipo, Math.Round(valor, 2), NormalizarDescricao(descricao));

    private static string NormalizarDescricao(string descricao)
    {
        var upper = descricao.ToUpperInvariant();
        return upper.Length >= 20 ? upper[..20] : upper;
    }
}
