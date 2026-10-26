namespace CaixaDiario.API.DTOs.Importacao;

public class ItemExcluirLoteDto
{
    public Guid Id { get; set; }
    public string Data { get; set; } = string.Empty; // ISO "yyyy-MM-dd"
}

public class ExcluirLoteDto
{
    public List<ItemExcluirLoteDto> Itens { get; set; } = new();
}
