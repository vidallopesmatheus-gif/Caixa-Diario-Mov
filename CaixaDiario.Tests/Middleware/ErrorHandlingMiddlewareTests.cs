using System.Text.Json;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Middleware;
using CaixaDiario.API.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CaixaDiario.Tests.Middleware;

public class ErrorHandlingMiddlewareTests
{
    private static (ErrorHandlingMiddleware middleware, DefaultHttpContext context) Criar(RequestDelegate next)
    {
        var logger = new Mock<ILogger<ErrorHandlingMiddleware>>().Object;
        var middleware = new ErrorHandlingMiddleware(next, logger);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return (middleware, context);
    }

    [Fact]
    public async Task InvokeAsync_SemExcecao_ChamaNext()
    {
        bool nextChamado = false;
        var (middleware, context) = Criar(_ => { nextChamado = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextChamado);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ApiException_RetornaStatusCorreto()
    {
        var (middleware, context) = Criar(_ =>
            throw new ApiException(422, CodigoRetorno.DADOS_INVALIDOS, "Dados inválidos"));

        await middleware.InvokeAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ApiException_RetornaJsonComCodigo()
    {
        var (middleware, context) = Criar(_ =>
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Erro teste"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var erro = JsonSerializer.Deserialize<ErroResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("DADOS_INVALIDOS", erro!.Codigo);
        Assert.Equal("Erro teste", erro.Mensagem);
    }

    [Fact]
    public async Task InvokeAsync_ExcecaoGenerica_Retorna500()
    {
        var (middleware, context) = Criar(_ => throw new Exception("boom"));

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ApiExceptionComCampo_RetornaCampoNoJson()
    {
        var (middleware, context) = Criar(_ =>
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Campo inválido", "nome_usuario"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var erro = JsonSerializer.Deserialize<ErroResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("nome_usuario", erro!.Campo);
    }
}
