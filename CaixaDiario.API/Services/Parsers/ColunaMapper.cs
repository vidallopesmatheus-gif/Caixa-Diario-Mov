using System.Globalization;

namespace CaixaDiario.API.Services.Parsers;

public record ColMap(int Data, int Descricao, int Valor, int Credito, int Debito);

/// <summary>
/// Heurística de identificação de colunas (data/descrição/valor ou crédito+débito) e parsing de
/// data/decimal em formato BR, compartilhada entre <see cref="CsvParser"/> e <see cref="XlsxParser"/>.
/// </summary>
public static class ColunaMapper
{
    public static int EncontrarCabecalho(IReadOnlyList<string> linhas, int max = 15)
    {
        for (int i = 0; i < Math.Min(linhas.Count, max); i++)
        {
            var lower = linhas[i].ToLowerInvariant();
            if (lower.Contains("data") || lower.Contains("date") || lower.Contains("dt"))
                return i;
        }
        return -1;
    }

    public static ColMap MapearColunas(string[] cols)
    {
        int data = -1, desc = -1, valor = -1, cred = -1, deb = -1;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i].ToLowerInvariant().Trim('"', ' ');
            if (data < 0 && (c.Contains("data") || c.Contains("date") || c == "dt")) data = i;
            else if (desc < 0 && (c.Contains("hist") || c.Contains("descr") || c.Contains("title")
                || c.Contains("memo") || c.Contains("lancamento"))) desc = i;
            else if (valor < 0 && (c == "valor" || c == "value" || c == "amount" || c.Contains("vlr"))) valor = i;
            else if (cred < 0 && (c.Contains("cred") || c.Contains("entrada") || c.Contains("recebido"))) cred = i;
            else if (deb < 0 && (c.Contains("deb") || c.Contains("saida") || c.Contains("pagamento"))) deb = i;
        }

        // Se não encontrou descrição, usa primeira coluna não-data/valor/crédito/débito
        if (desc < 0)
        {
            for (int i = 0; i < cols.Length; i++)
                if (i != data && i != valor && i != cred && i != deb) { desc = i; break; }
        }

        return new ColMap(data, desc, valor, cred, deb);
    }

    public static DateOnly? ParseData(string s)
    {
        s = s.Trim('"', ' ');
        string[] formatos = { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "yyyyMMdd" };
        foreach (var fmt in formatos)
            if (DateOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        return null;
    }

    public static bool ParseDecimalBr(string s, out decimal result)
    {
        result = 0;
        s = s.Trim('"', ' ').Replace("R$", "").Trim();
        if (string.IsNullOrWhiteSpace(s) || s == "-") return false;
        // Formato BR: 1.234,56 → remove pontos, troca vírgula por ponto
        if (s.Contains(','))
            s = s.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    public static string CampoSafe(string[] campos, int idx) =>
        idx >= 0 && idx < campos.Length ? campos[idx] : string.Empty;
}
