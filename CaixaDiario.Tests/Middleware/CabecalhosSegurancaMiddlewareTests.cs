using CaixaDiario.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;

namespace CaixaDiario.Tests.Middleware;

public class CabecalhosSegurancaMiddlewareTests
{
    private static async Task<IHeaderDictionary> ExecutarAsync(string ambiente)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(ambiente);
        var nextChamado = false;
        var middleware = new CabecalhosSegurancaMiddleware(_ => { nextChamado = true; return Task.CompletedTask; }, env.Object);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextChamado);
        return context.Response.Headers;
    }

    [Fact]
    public async Task InvokeAsync_EmProducao_PoeTodosOsCabecalhosDeSeguranca()
    {
        var headers = await ExecutarAsync(Environments.Production);

        Assert.Equal("max-age=31536000; includeSubDomains", headers.StrictTransportSecurity.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"].ToString());
        Assert.Contains("frame-ancestors 'none'", headers.ContentSecurityPolicy.ToString());
        Assert.Contains("default-src 'self'", headers.ContentSecurityPolicy.ToString());
    }

    // HSTS em http://localhost faria o navegador forçar https em todas as portas locais por um ano.
    [Fact]
    public async Task InvokeAsync_EmDesenvolvimento_NaoPoeHsts()
    {
        var headers = await ExecutarAsync(Environments.Development);

        Assert.False(headers.ContainsKey("Strict-Transport-Security"));
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
    }

    [Theory]
    [InlineData("/assets/index-CFDJKfQc.js", "index-CFDJKfQc.js", CacheArquivosEstaticos.Imutavel)]
    [InlineData("/assets/index-D91e3sxI.css", "index-D91e3sxI.css", CacheArquivosEstaticos.Imutavel)]
    [InlineData("/index.html", "index.html", CacheArquivosEstaticos.SemCache)]
    [InlineData("/sw.js", "sw.js", CacheArquivosEstaticos.SemCache)]
    [InlineData("/manifest.webmanifest", "manifest.webmanifest", CacheArquivosEstaticos.SemCache)]
    [InlineData("/favicon.svg", "favicon.svg", null)]
    public void PoliticaDeCache_PorArquivo(string caminho, string arquivo, string? esperado)
    {
        Assert.Equal(esperado, CacheArquivosEstaticos.Politica(caminho, arquivo));
    }
}
