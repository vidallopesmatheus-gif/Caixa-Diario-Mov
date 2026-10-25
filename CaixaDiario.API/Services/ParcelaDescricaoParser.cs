using System.Text.RegularExpressions;

namespace CaixaDiario.API.Services;

// Bloco 5 (Money Vision, item 7) + rodada de parcelamentos: detecta compras parceladas no PRÓPRIO
// extrato importado do cartão, pelos padrões reais observados em bancos diferentes — "<comerciante>
// - Parcela N/M" (confirmado no extrato Nubank real do cliente teste: "Trocafone - Parcela 1/10",
// "Airbnb * Hmmanqmbm2 - Parcela 5/6"), "<comerciante> PARC N/M", "<comerciante> (N de M)" e, por
// último (mais arriscado — sem palavra-chave, só decidido pela posição no fim da descrição e pelos
// mesmos limites numéricos dos outros), "<comerciante> N/M" solto. Tentados nessa ordem: do mais
// específico (palavra-chave clara) pro mais genérico, parando no primeiro que casar. Nunca o
// parcelamento lançado manualmente (que já tem NumeroParcela/TotalParcelas gravados por
// LancarComprasParceladaAsync, sem precisar de regex).
public static partial class ParcelaDescricaoParser
{
    [GeneratedRegex(@"^(?<comerciante>.+?)\s*-\s*Parcela\s+(?<numero>\d{1,3})\s*/\s*(?<total>\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PadraoParcela();

    [GeneratedRegex(@"^(?<comerciante>.+?)\s+PARC\.?\s*(?<numero>\d{1,3})\s*/\s*(?<total>\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PadraoParc();

    [GeneratedRegex(@"^(?<comerciante>.+?)\s*\(\s*(?<numero>\d{1,3})\s+de\s+(?<total>\d{1,3})\s*\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PadraoDe();

    [GeneratedRegex(@"^(?<comerciante>.+?)\s+(?<numero>\d{1,3})\s*/\s*(?<total>\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PadraoBarraSolta();

    private static readonly Regex[] Padroes = { PadraoParcela(), PadraoParc(), PadraoDe(), PadraoBarraSolta() };

    public record Resultado(string Comerciante, int Numero, int Total);

    public static Resultado? TentarExtrair(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return null;

        foreach (var padrao in Padroes)
        {
            var m = padrao.Match(descricao);
            if (!m.Success) continue;

            var numero = int.Parse(m.Groups["numero"].Value);
            var total = int.Parse(m.Groups["total"].Value);
            // Parcela "0/N", numero > total, ou "1/1" (não é parcelamento de verdade) não fazem
            // sentido — descarta silenciosamente (trata como descrição comum, tenta o próximo padrão).
            if (numero < 1 || total < 2 || total < numero) continue;

            return new Resultado(m.Groups["comerciante"].Value.Trim(), numero, total);
        }

        return null;
    }
}
