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

    public ImportacaoService(
        IContaBancariaRepository contaRepo,
        ITransacaoImportadaRepository importRepo,
        IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _importRepo = importRepo;
        _registroRepo = registroRepo;
    }

    // Representação unificada de uma linha do arquivo, independente do formato de origem.
    // Indice = posição no arquivo, na mesma ordem em que o parser encontrou — é o que o preview
    // devolve e o que a confirmação usa para "forçar inclusão" de uma linha já importada antes.
    private record TransacaoParseada(int Indice, DateOnly Data, decimal Valor, string Descricao, string Tipo, string? FitId);

    // ── Preview (não persiste nada) ──────────────────────────────────────────────
    public async Task<PreviewImportacaoDto> PreviewAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo)
    {
        await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        var parseadas = ParsearArquivoValidando(arquivo);

        var historico = await _importRepo.ListarPorContaAsync(contaBancariaId);
        var dtos = parseadas.Select(t => new PreviewTransacaoDto
        {
            Indice = t.Indice,
            Data = t.Data.ToString("yyyy-MM-dd"),
            Valor = t.Valor,
            Descricao = t.Descricao,
            Tipo = t.Tipo,
            FitId = t.FitId,
            JaImportada = JaFoiImportada(t, historico),
        }).ToList();

        return new PreviewImportacaoDto { Transacoes = dtos };
    }

    // ── Importar (lança direto no RegistroDiario — afeta saldo na hora) ──────────
    public async Task<ResultadoImportacaoDto> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim, List<int>? indicesForcarInclusao)
    {
        var conta = await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        var parseadas = ParsearArquivoValidando(arquivo);

        if (dataInicio.HasValue) parseadas = parseadas.Where(t => t.Data >= dataInicio.Value).ToList();
        if (dataFim.HasValue) parseadas = parseadas.Where(t => t.Data <= dataFim.Value).ToList();

        var forcar = (indicesForcarInclusao ?? new()).ToHashSet();
        var historico = await _importRepo.ListarPorContaAsync(contaBancariaId);

        var aImportar = parseadas
            .Where(t => forcar.Contains(t.Indice) || !JaFoiImportada(t, historico))
            .ToList();

        if (aImportar.Count == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Nenhuma transação nova para importar no intervalo selecionado.");

        var registros = (await _registroRepo.ListarPorContaAsync(contaBancariaId))
            .Where(r => !r.Excluido)
            .ToList();
        var registrosPorData = registros.ToDictionary(r => r.Data);
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

                    registro.Saidas.Add(new ItemFinanceiroSaida
                    {
                        Id = Guid.NewGuid(),
                        Descricao = t.Descricao,
                        Valor = t.Valor,
                        Categoria = categoriaSugerida ?? string.Empty,
                        Subcategoria = string.Empty,
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
                saida.PendenteCategorizacao = false;
            }

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

    // OFX: mesmo FITID já visto antes nesta conta (identificador único — não precisa de mais nada).
    // CSV/XLSX (sem FITID): heurística de sempre — mesma data, valor (±0,01) e descrição parecida.
    private static bool JaFoiImportada(TransacaoParseada t, List<TransacaoImportada> historico)
    {
        if (t.FitId != null)
            return historico.Any(h => h.FitId == t.FitId);

        return historico.Any(h =>
            h.Data == t.Data &&
            Math.Abs(h.Valor - t.Valor) < 0.01m &&
            h.Tipo == t.Tipo &&
            DescricaoSimilar(h.Descricao, t.Descricao));
    }

    private static bool DescricaoSimilar(string a, string b)
    {
        var la = a.ToUpperInvariant().Length >= 20 ? a[..20].ToUpperInvariant() : a.ToUpperInvariant();
        var lb = b.ToUpperInvariant().Length >= 20 ? b[..20].ToUpperInvariant() : b.ToUpperInvariant();
        return la == lb;
    }
}
