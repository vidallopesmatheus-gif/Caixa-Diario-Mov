using System.Text.RegularExpressions;

namespace CaixaDiario.API.Services;

public static class TopicGuard
{
    private static readonly Regex[] _blockedPatterns =
    [
        new(@"receita(s)?\s+de\s+(bolo|frango|carne|pão|macarrão|torta|comida|sopa|arroz|feijão)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bculin[aá]ria\b|\bprato\s+t[íi]pico\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"previs[aã]o\s+do\s+tempo", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bclima\s+em\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\btemperatura\s+em\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bfutebol\b|\bbasquete\b|\bv[oô]lei\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bpol[ií]tica\b|\belei[cç][aã]o\b|\bpresidente\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bnotícia(s)?\b|\bjornal\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"como\s+instalar\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"tutorial\s+de\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"programar\s+em\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public static bool IsOffTopic(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return _blockedPatterns.Any(p => p.IsMatch(message));
    }
}
