using System.Text;

namespace CaixaDiario.API.Services.Parsers;

public record TransacaoCsv(DateOnly Data, decimal Valor, string Descricao, string Tipo);

public static class CsvParser
{
    public static List<TransacaoCsv> Parse(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        // CSV não tem cabeçalho de encoding como o OFX — detecta pelo conteúdo (UTF-8 válido
        // ou Latin-1/Windows-1252 legado, evitando forçar Latin-1 e corromper arquivos UTF-8).
        var encoding = EncodingDetector.DetectarPorConteudo(bytes);
        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);
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
        int headerIdx = ColunaMapper.EncontrarCabecalho(linhas);
        if (headerIdx < 0)
            throw new InvalidOperationException("Cabeçalho do CSV não identificado. Verifique o formato do arquivo.");

        var colunas = SplitCsv(linhas[headerIdx], delim);
        var mapa = ColunaMapper.MapearColunas(colunas);

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

            var dataStr = ColunaMapper.CampoSafe(campos, mapa.Data);
            var desc = ColunaMapper.CampoSafe(campos, mapa.Descricao);
            if (string.IsNullOrWhiteSpace(dataStr) || string.IsNullOrWhiteSpace(desc)) continue;

            var data = ColunaMapper.ParseData(dataStr);
            if (data == null) continue;

            decimal valor;
            string tipo;

            if (mapa.Valor >= 0)
            {
                // Coluna única de valor (pode ser negativo para débito)
                var valorStr = ColunaMapper.CampoSafe(campos, mapa.Valor);
                if (!ColunaMapper.ParseDecimalBr(valorStr, out var v)) continue;
                tipo = v >= 0 ? "Entrada" : "Saida";
                valor = Math.Abs(v);
            }
            else
            {
                // Colunas separadas de crédito e débito
                var cred = ColunaMapper.CampoSafe(campos, mapa.Credito);
                var deb = ColunaMapper.CampoSafe(campos, mapa.Debito);
                ColunaMapper.ParseDecimalBr(cred, out var credV);
                ColunaMapper.ParseDecimalBr(deb, out var debV);
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
}
