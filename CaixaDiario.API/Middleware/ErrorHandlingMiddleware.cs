using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;

namespace CaixaDiario.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErroResponse
            {
                Status = ex.StatusCode,
                Codigo = ex.Codigo.ToString(),
                Mensagem = ex.Message,
                Campo = ex.Campo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro nao tratado");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErroResponse
            {
                Status = 500,
                Codigo = CodigoRetorno.ERRO_INTERNO.ToString(),
                Mensagem = "Ocorreu um erro interno. Tente novamente mais tarde."
            });
        }
    }
}
