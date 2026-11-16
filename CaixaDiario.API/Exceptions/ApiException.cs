using CaixaDiario.API.Enums;

namespace CaixaDiario.API.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public CodigoRetorno Codigo { get; }
    public string? Campo { get; }
    // Dados extras pra tela agir sobre o erro (ex.: PERIODO_CONFERIDO → conta e data da trava).
    public object? Detalhes { get; }

    public ApiException(int statusCode, CodigoRetorno codigo, string mensagem, string? campo = null, object? detalhes = null)
        : base(mensagem)
    {
        StatusCode = statusCode;
        Codigo = codigo;
        Campo = campo;
        Detalhes = detalhes;
    }
}
