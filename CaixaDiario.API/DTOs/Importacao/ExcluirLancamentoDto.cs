namespace CaixaDiario.API.DTOs.Importacao;

public class ExcluirLancamentoDto
{
    public Guid Id { get; set; }
    public string Data { get; set; } = string.Empty; // ISO "yyyy-MM-dd"
}
