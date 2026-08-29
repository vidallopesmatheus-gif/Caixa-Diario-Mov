using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CaixaDiario.API.Services.Parsers;

public record TransacaoOfx(DateOnly Data, decimal Valor, string Descricao, string Tipo, string? FitId);

public static class OfxParser
{
    public static List<TransacaoOfx> Parse(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        // Usa o CHARSET/ENCODING declarado no cabeçalho OFX; sem declaração, detecta pelo
        // conteúdo (extratos brasileiros vêm tanto em UTF-8 quanto em Latin-1/Windows-1252).
        var encoding = EncodingDetector.DetectarOfx(bytes);
        var content = encoding.GetString(bytes).TrimStart('﻿');

        // OFX 2.x começa com declaração XML
        if (content.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return ParseXml(content);

        return ParseSgml(content);
    }

    // ── OFX 1.x SGML ────────────────────────────────────────────────────────
    private static List<TransacaoOfx> ParseSgml(string content)
    {
        var result = new List<TransacaoOfx>();

        // Extrai cada bloco <STMTTRN>...</STMTTRN>
        var blocos = Regex.Matches(content, @"<STMTTRN>(.*?)</STMTTRN>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match bloco in blocos)
        {
            var texto = bloco.Groups[1].Value;
            var trnType = ExtrairValor(texto, "TRNTYPE") ?? "CREDIT";
            var dtPosted = ExtrairValor(texto, "DTPOSTED");
            var trnAmt = ExtrairValor(texto, "TRNAMT");
            var fitId = ExtrairValor(texto, "FITID");
            var memo = ExtrairValor(texto, "MEMO") ?? ExtrairValor(texto, "NAME") ?? "";

            if (dtPosted == null || trnAmt == null) continue;

            var data = ParseOfxDate(dtPosted);
            if (data == null) continue;

            // Remove separadores de milhar e normaliza decimal
            var valorStr = trnAmt.Replace(",", ".");
            if (!decimal.TryParse(valorStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor)) continue;

            var tipo = valor >= 0 ? "Entrada" : "Saida";
            result.Add(new TransacaoOfx(data.Value, Math.Abs(valor), LimparDescricao(memo), tipo, fitId));
        }

        return result;
    }

    // ── OFX 2.x XML ─────────────────────────────────────────────────────────
    private static List<TransacaoOfx> ParseXml(string content)
    {
        var result = new List<TransacaoOfx>();
        var doc = new XmlDocument();
        doc.LoadXml(content);

        var transacoes = doc.GetElementsByTagName("STMTTRN");
        foreach (XmlNode node in transacoes)
        {
            var trnType = node["TRNTYPE"]?.InnerText ?? "CREDIT";
            var dtPosted = node["DTPOSTED"]?.InnerText;
            var trnAmt = node["TRNAMT"]?.InnerText;
            var fitId = node["FITID"]?.InnerText;
            var memo = node["MEMO"]?.InnerText ?? node["NAME"]?.InnerText ?? "";

            if (dtPosted == null || trnAmt == null) continue;

            var data = ParseOfxDate(dtPosted);
            if (data == null) continue;

            var valorStr = trnAmt.Replace(",", ".");
            if (!decimal.TryParse(valorStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor)) continue;

            var tipo = valor >= 0 ? "Entrada" : "Saida";
            result.Add(new TransacaoOfx(data.Value, Math.Abs(valor), LimparDescricao(memo), tipo, fitId));
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static string? ExtrairValor(string bloco, string tag)
    {
        var m = Regex.Match(bloco, $@"<{tag}>\s*([^\r\n<]+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static DateOnly? ParseOfxDate(string raw)
    {
        // Formatos: YYYYMMDDHHMMSS[-3:BRT] ou YYYYMMDD
        var m = Regex.Match(raw, @"^(\d{4})(\d{2})(\d{2})");
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups[1].Value, out var y)) return null;
        if (!int.TryParse(m.Groups[2].Value, out var mo)) return null;
        if (!int.TryParse(m.Groups[3].Value, out var d)) return null;
        try { return new DateOnly(y, mo, d); } catch { return null; }
    }

    private static string LimparDescricao(string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ");
}
