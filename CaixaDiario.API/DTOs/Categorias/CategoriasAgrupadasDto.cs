namespace CaixaDiario.API.DTOs.Categorias;

/// <summary>Formato legado consumido pelos formulários de lançamento (GET /api/categorias).</summary>
public class CategoriaItemDto
{
    public string Nome { get; set; } = string.Empty;
    public string TipoCusto { get; set; } = string.Empty;
    public string? Grupo { get; set; }
}

public class CategoriasAgrupadasDto
{
    public List<CategoriaItemDto> Entradas { get; set; } = new();
    public List<CategoriaItemDto> Saidas { get; set; } = new();
}
