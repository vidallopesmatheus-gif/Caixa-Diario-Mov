namespace CaixaDiario.API.DTOs.Importacao;

public class AtualizarCategoriaDto
{
    public List<AtualizarCategoriaItem> Itens { get; set; } = new();
}

public class AtualizarCategoriaItem
{
    public Guid Id { get; set; }
    public string Data { get; set; } = string.Empty; // ISO "yyyy-MM-dd"
    public string Categoria { get; set; } = string.Empty;
}
