using CaixaDiario.API.Enums;

namespace CaixaDiario.API.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public CodigoRetorno Codigo { get; }
    public string? Campo { get; }

    public ApiException(int statusCode, CodigoRetorno codigo, string mensagem, string? campo = null)
        : base(mensagem)
    {
        StatusCode = statusCode;
        Codigo = codigo;
        Campo = campo;
    }
}
