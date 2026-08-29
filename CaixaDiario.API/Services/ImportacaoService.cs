using CaixaDiario.API.DTOs.Importacao;
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

    public ImportacaoService(
        IContaBancariaRepository contaRepo,
        ITransacaoImportadaRepository importRepo,
        IRegistroRepository registroRepo,
        ICategoriaRepository categoriaRepo)
    {
        _contaRepo = contaRepo;
        _importRepo = importRepo;
        _registroRepo = registroRepo;
        _categoriaRepo = categoriaRepo;
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

        var jaImportadas = IdentificarJaImportadas(parseadas, registrosAtivos);
        var novas = parseadas.Where(t => !jaImportadas.Contains(t.Indice)).ToList();

        return new PreviewImportacaoDto
        {
            TotalEncontradas = parseadas.Count,
            TotalJaImportadas = jaImportadas.Count,
            TotalNovas = novas.Count,
            TotalEntradas = novas.Where(t => t.Tipo == "Entrada").Sum(t => t.Valor),
            TotalSaidas = novas.Where(t => t.Tipo == "Saida").Sum(t => t.Valor),
            DataInicioArquivo = dataInicioArquivo.ToString("yyyy-MM-dd"),
            DataFimArquivo = dataFimArquivo.ToString("yyyy-MM-dd"),
        };
    }

    // ── Importar (lança direto no RegistroDiario — afeta saldo na hora) ──────────
    public async Task<ResultadoImportacaoDto> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim)
    {
        var conta = await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        var parseadas = ParsearArquivoValidando(arquivo);

        if (dataInicio.HasValue) parseadas = parseadas.Where(t => t.Data >= dataInicio.Value).ToList();
        if (dataFim.HasValue) parseadas = parseadas.Where(t => t.Data <= dataFim.Value).ToList();

        var registros = (await _registroRepo.ListarPorContaAsync(contaBancariaId))
            .Where(r => !r.Excluido)
            .ToList();

        var jaImportadas = IdentificarJaImportadas(parseadas, registros);
        var aImportar = parseadas.Where(t => !jaImportadas.Contains(t.Indice)).ToList();

        if (aImportar.Count == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Nenhuma transação nova para importar no intervalo selecionado — todas já foram importadas antes.");

        var registrosPorData = registros.ToDictionary(r => r.Data);
        var categoriasPorNome = (await _categoriaRepo.ListarTodasAsync()).ToDictionary(c => c.Nome, c => c.Tipo);
        var auditoria = new List<TransacaoImportada>();
        var pendentes = 0;

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
                // Sugestão automática só existe hoje pro lado das saídas — entrada sempre foi opcional
                // categorizar (não bloqueia nada no DRE, que só classifica "Não Classificado" no lado
                // das despesas). Por isso só a saída sem sugestão vira "pendente de categorização".
                if (t.Tipo == "Entrada")
                {
                    registro.Entradas.Add(new ItemFinanceiro
                    {
                        Id = Guid.NewGuid(),
                        Descricao = t.Descricao,
                        Valor = t.Valor,
                        FitId = t.FitId,
                    });
                    registro.SaldoFinal += t.Valor;
                }
                else
                {
                    var categoriaSugerida = SugerirCategoria(t.Tipo, t.Descricao);
                    var pendente = categoriaSugerida == null;
                    if (pendente) pendentes++;
                    // A sugestão por palavra-chave devolve um nome de categoria já cadastrado no
                    // Plano de Contas — sem resolver o TipoCusto aqui, o lançamento nunca entraria
                    // como custo fixo/variável no DRE mesmo tendo uma categoria "certa" atribuída.
                    var tipoCustoSugerido = categoriaSugerida != null && categoriasPorNome.TryGetValue(categoriaSugerida, out var tc)
                        ? tc : null;

                    registro.Saidas.Add(new ItemFinanceiroSaida
                    {
                        Id = Guid.NewGuid(),
                        Descricao = t.Descricao,
                        Valor = t.Valor,
                        Categoria = categoriaSugerida ?? string.Empty,
                        Subcategoria = string.Empty,
                        TipoCusto = tipoCustoSugerido,
                        FitId = t.FitId,
                        PendenteCategorizacao = pendente,
                    });
                    registro.SaldoFinal -= t.Valor;
                }

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

        await _importRepo.AdicionarLoteAsync(auditoria);

        return new ResultadoImportacaoDto
        {
            TotalImportadas = aImportar.Count,
            TotalPendentesCategorizacao = pendentes,
            TotalEntradas = aImportar.Where(t => t.Tipo == "Entrada").Sum(t => t.Valor),
            TotalSaidas = aImportar.Where(t => t.Tipo == "Saida").Sum(t => t.Valor),
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
            .SelectMany(r => r.Saidas
                .Where(s => s.PendenteCategorizacao)
                .Select(s => new PendenteCategorizacaoDto
                {
                    Id = s.Id,
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = s.Descricao,
                    Valor = s.Valor,
                    Tipo = "Saida",
                }))
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
                if (saida == null) continue;
                saida.Categoria = item.Categoria;
                if (categoriasPorNome.TryGetValue(item.Categoria, out var tipoCusto))
                    saida.TipoCusto = tipoCusto;
                saida.PendenteCategorizacao = false;
            }

            // Reatribui a lista — Saidas é jsonb sem value comparer configurado, então o EF só
            // detecta a mudança se a referência da lista mudar, não se um item dela for só mutado.
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
            registro.SalvoEm = DateTime.UtcNow;
            registro.AtualizadoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
        }
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
    private static HashSet<int> IdentificarJaImportadas(List<TransacaoParseada> parseadas, List<RegistroDiario> registrosAtivos)
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
