using ClosedXML.Excel;

namespace CaixaDiario.API.Services.Parsers;

public static class XlsxParser
{
    public static List<TransacaoCsv> Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Planilha vazia ou sem abas.");

        var usedRange = ws.RangeUsed();
        if (usedRange == null)
            throw new InvalidOperationException("Planilha vazia ou sem linhas de dados.");

        var linhasCelulas = new List<IXLCell[]>();
        var linhasTexto = new List<string>();
        foreach (var row in usedRange.RowsUsed())
        {
            var celulas = row.Cells(1, usedRange.ColumnCount()).ToArray();
            linhasCelulas.Add(celulas);
            linhasTexto.Add(string.Join("|", celulas.Select(c => c.GetString())));
        }

        if (linhasCelulas.Count < 2)
            throw new InvalidOperationException("Planilha vazia ou sem linhas de dados.");

        int headerIdx = ColunaMapper.EncontrarCabecalho(linhasTexto);
        if (headerIdx < 0)
            throw new InvalidOperationException("Cabeçalho da planilha não identificado. Verifique o formato do arquivo.");

        var colunasTexto = linhasCelulas[headerIdx].Select(c => c.GetString()).ToArray();
        var mapa = ColunaMapper.MapearColunas(colunasTexto);

        if (mapa.Data < 0)
            throw new InvalidOperationException("Coluna de data não encontrada na planilha.");
        if (mapa.Descricao < 0)
            throw new InvalidOperationException("Coluna de descrição não encontrada na planilha.");
        if (mapa.Valor < 0 && (mapa.Credito < 0 || mapa.Debito < 0))
            throw new InvalidOperationException("Coluna de valor não encontrada na planilha.");

        var result = new List<TransacaoCsv>();
        for (int i = headerIdx + 1; i < linhasCelulas.Count; i++)
        {
            var celulas = linhasCelulas[i];
            if (celulas.Length <= Math.Max(mapa.Data, mapa.Descricao)) continue;

            var desc = ColunaMapper.CampoSafe(GetStrings(celulas), mapa.Descricao).Trim();
            if (string.IsNullOrWhiteSpace(desc)) continue;

            var data = LerData(celulas, mapa.Data);
            if (data == null) continue;

            decimal valor;
            string tipo;

            if (mapa.Valor >= 0)
            {
                var v = LerDecimal(celulas, mapa.Valor);
                if (v == null) continue;
                tipo = v.Value >= 0 ? "Entrada" : "Saida";
                valor = Math.Abs(v.Value);
            }
            else
            {
                var credV = LerDecimal(celulas, mapa.Credito) ?? 0m;
                var debV = LerDecimal(celulas, mapa.Debito) ?? 0m;
                if (credV == 0 && debV == 0) continue;
                if (credV > 0) { valor = credV; tipo = "Entrada"; }
                else { valor = Math.Abs(debV); tipo = "Saida"; }
            }

            if (valor <= 0) continue;
            result.Add(new TransacaoCsv(data.Value, valor, desc, tipo));
        }

        return result;
    }

    private static string[] GetStrings(IXLCell[] celulas) => celulas.Select(c => c.GetString()).ToArray();

    private static DateOnly? LerData(IXLCell[] celulas, int idx)
    {
        if (idx < 0 || idx >= celulas.Length) return null;
        var cell = celulas[idx];
        if (cell.DataType == XLDataType.DateTime)
        {
            var dt = cell.GetDateTime();
            return DateOnly.FromDateTime(dt);
        }
        return ColunaMapper.ParseData(cell.GetString());
    }

    private static decimal? LerDecimal(IXLCell[] celulas, int idx)
    {
        if (idx < 0 || idx >= celulas.Length) return null;
        var cell = celulas[idx];
        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();
        return ColunaMapper.ParseDecimalBr(cell.GetString(), out var v) ? v : null;
    }
}
