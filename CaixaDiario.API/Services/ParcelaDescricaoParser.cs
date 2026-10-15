using System.Text.RegularExpressions;

namespace CaixaDiario.API.Services;

// Bloco 5 (Money Vision, item 7 — opcional): detecta compras parceladas no PRÓPRIO extrato
// importado do cartão, pelo padrão real observado nos bancos ("<comerciante> - Parcela N/M",
// confirmado no extrato Nubank real do cliente teste: "Trocafone - Parcela 1/10", "Airbnb *
// Hmmanqmbm2 - Parcela 5/6" etc.) — nunca o parcelamento lançado manualmente (que já tem
// NumeroParcela/TotalParcelas gravados por LancarComprasParceladaAsync, sem precisar de regex).
public static partial class ParcelaDescricaoParser
{
    [GeneratedRegex(@"^(?<comerciante>.+?)\s*-\s*Parcela\s+(?<numero>\d{1,3})\s*/\s*(?<total>\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Padrao();

    public record Resultado(string Comerciante, int Numero, int Total);

    public static Resultado? TentarExtrair(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return null;
        var m = Padrao().Match(descricao);
        if (!m.Success) return null;

        var numero = int.Parse(m.Groups["numero"].Value);
        var total = int.Parse(m.Groups["total"].Value);
        // Parcela "0/N" ou numero > total não fazem sentido — descarta silenciosamente (trata
        // como descrição comum, sem dado de parcelamento).
        if (numero < 1 || total < numero) return null;

        return new Resultado(m.Groups["comerciante"].Value.Trim(), numero, total);
    }
}
