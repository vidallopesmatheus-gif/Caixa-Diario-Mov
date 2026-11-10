namespace CaixaDiario.API.Middleware;

// Money Vision (segurança): cabeçalhos em TODA resposta — API, index.html e assets estáticos. Fica
// antes do UseStaticFiles no pipeline, senão os arquivos estáticos saíam sem nenhum deles.
public class CabecalhosSegurancaMiddleware
{
    // Google Fonts (index.html) e a API pública do Banco Central (Selic, api/selic.ts) são as únicas
    // origens externas do app. 'unsafe-inline' em style-src: React/recharts usam style="" inline.
    public const string PoliticaConteudo =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self' https://api.bcb.gov.br; " +
        "worker-src 'self'; " +
        "manifest-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    private readonly RequestDelegate _next;
    private readonly bool _https;

    // HSTS só fora de desenvolvimento: em http://localhost ele faria o navegador forçar https
    // em todas as portas locais por um ano.
    public CabecalhosSegurancaMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _https = !env.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        if (_https) headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers.ContentSecurityPolicy = PoliticaConteudo;
        return _next(context);
    }
}
