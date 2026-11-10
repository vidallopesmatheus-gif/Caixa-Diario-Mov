namespace CaixaDiario.API.Middleware;

// Money Vision (performance): o Vite põe hash no nome de tudo em /assets — o conteúdo de um nome
// nunca muda, então pode ficar 1 ano no navegador sem revalidar. Já index.html, sw.js e o manifesto
// apontam pros nomes novos a cada deploy: esses nunca podem ficar em cache.
public static class CacheArquivosEstaticos
{
    public const string Imutavel = "public, max-age=31536000, immutable";
    public const string SemCache = "no-cache, no-store, must-revalidate";

    private static readonly HashSet<string> SempreRevalidar = new(StringComparer.OrdinalIgnoreCase)
    {
        "index.html", "sw.js", "registerSW.js", "manifest.webmanifest",
    };

    public static string? Politica(string caminho, string nomeArquivo)
    {
        if (SempreRevalidar.Contains(nomeArquivo)) return SemCache;
        if (caminho.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase)) return Imutavel;
        return null;
    }

    public static void Aplicar(HttpContext context, string nomeArquivo)
    {
        var politica = Politica(context.Request.Path.Value ?? string.Empty, nomeArquivo);
        if (politica != null) context.Response.Headers.CacheControl = politica;
    }
}
