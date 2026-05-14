namespace CaixaDiario.API.Responses;

public class ErroResponse
{
    public int Status { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public string? Campo { get; set; }
}
