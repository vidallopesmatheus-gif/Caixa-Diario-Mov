using System.Globalization;
using System.Text;

namespace CaixaDiario.API.Services.Parsers;

public record TransacaoCsv(DateOnly Data, decimal Valor, string Descricao, string Tipo);

public static class CsvParser
{
    public static List<TransacaoCsv> Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);
        var linhas = new List<string>();
        while (!reader.EndOfStream)
        {
            var l = reader.ReadLine();
            if (l != null) linhas.Add(l);
        }

        if (linhas.Count < 2)
            throw new InvalidOperationException("Arquivo CSV vazio ou sem linhas de dados.");

        // Detecta delimitador
        var delim = DetectarDelimitador(linhas[0]);

        // Encontra linha de cabeçalho (pode haver linhas de metadata antes)
        int headerIdx = EncontrarCabecalho(linhas, delim);
        if (headerIdx < 0)
            throw new InvalidOperationException("Cabeçalho do CSV não identificado. Verifique o formato do arquivo.");

        var colunas = SplitCsv(linhas[headerIdx], delim);
        var mapa = MapearColunas(colunas);

        if (mapa.Data < 0)
            throw new InvalidOperationException("Coluna de data não encontrada no CSV.");
        if (mapa.Descricao < 0)
            throw new InvalidOperationException("Coluna de descrição não encontrada no CSV.");
        if (mapa.Valor < 0 && (mapa.Credito < 0 || mapa.Debito < 0))
            throw new InvalidOperationException("Coluna de valor não encontrada no CSV.");

        var result = new List<TransacaoCsv>();
        for (int i = headerIdx + 1; i < linhas.Count; i++)
        {
            var linha = linhas[i].Trim();
            if (string.IsNullOrWhiteSpace(linha)) continue;

            var campos = SplitCsv(linha, delim);
            if (campos.Length <= Math.Max(mapa.Data, mapa.Descricao)) continue;

            var dataStr = CampoSafe(campos, mapa.Data);
            var desc = CampoSafe(campos, mapa.Descricao);
            if (string.IsNullOrWhiteSpace(dataStr) || string.IsNullOrWhiteSpace(desc)) continue;

            var data = ParseData(dataStr);
            if (data == null) continue;

            decimal valor;
            string tipo;

            if (mapa.Valor >= 0)
            {
                // Coluna única de valor (pode ser negativo para débito)
                var valorStr = CampoSafe(campos, mapa.Valor);
                if (!ParseDecimalBr(valorStr, out var v)) continue;
                tipo = v >= 0 ? "Entrada" : "Saida";
                valor = Math.Abs(v);
            }
            else
            {
                // Colunas separadas de crédito e débito
                var cred = CampoSafe(campos, mapa.Credito);
                var deb = CampoSafe(campos, mapa.Debito);
                ParseDecimalBr(cred, out var credV);
                ParseDecimalBr(deb, out var debV);
                if (credV == 0 && debV == 0) continue;
                if (credV > 0) { valor = credV; tipo = "Entrada"; }
                else { valor = debV; tipo = "Saida"; }
            }

            if (valor <= 0) continue;
            result.Add(new TransacaoCsv(data.Value, valor, desc.Trim('"', ' '), tipo));
        }

        return result;
    }

    // ── Detecção de delimitador ───────────────────────────────────────────────
    private static char DetectarDelimitador(string header)
    {
        int semicolons = header.Count(c => c == ';');
        int commas = header.Count(c => c == ',');
        return semicolons >= commas ? ';' : ',';
    }

    // ── Encontra linha com cabeçalho ─────────────────────────────────────────
    private static int EncontrarCabecalho(List<string> linhas, char delim)
    {
        for (int i = 0; i < Math.Min(linhas.Count, 15); i++)
        {
            var lower = linhas[i].ToLowerInvariant();
            if (lower.Contains("data") || lower.Contains("date") || lower.Contains("dt"))
                return i;
        }
        return -1;
    }

    // ── Mapeamento de colunas ────────────────────────────────────────────────
    private record ColMap(int Data, int Descricao, int Valor, int Credito, int Debito);

    private static ColMap MapearColunas(string[] cols)
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

        // Se não encontrou descrição, usa segunda coluna não-data/valor
        if (desc < 0)
        {
            for (int i = 0; i < cols.Length; i++)
                if (i != data && i != valor && i != cred && i != deb) { desc = i; break; }
        }

        return new ColMap(data, desc, valor, cred, deb);
    }

    // ── Split CSV respeitando aspas ──────────────────────────────────────────
    private static string[] SplitCsv(string line, char delim)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        foreach (char c in line)
        {
            if (c == '"') { inQuote = !inQuote; }
            else if (c == delim && !inQuote) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }

    // ── Parsers de data e decimal ────────────────────────────────────────────
    private static DateOnly? ParseData(string s)
    {
        s = s.Trim('"', ' ');
        string[] formatos = { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "yyyyMMdd" };
        foreach (var fmt in formatos)
            if (DateOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        return null;
    }

    private static bool ParseDecimalBr(string s, out decimal result)
    {
        result = 0;
        s = s.Trim('"', ' ').Replace("R$", "").Trim();
        if (string.IsNullOrWhiteSpace(s) || s == "-") return false;
        // Formato BR: 1.234,56 → remove pontos, troca vírgula por ponto
        if (s.Contains(','))
            s = s.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static string CampoSafe(string[] campos, int idx) =>
        idx >= 0 && idx < campos.Length ? campos[idx] : string.Empty;
}
