using CaixaDiario.API.Enums;

namespace CaixaDiario.API.Responses;

public class ApiResponse<T>
{
    public string Codigo { get; set; } = CodigoRetorno.SUCESSO.ToString();
    public T? Dados { get; set; }
}
