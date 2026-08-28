namespace CaixaDiario.API.DTOs.Categorias;

/// <summary>Ids das categorias na nova ordem desejada (posição no array = nova Ordem).</summary>
public class ReordenarCategoriasDto
{
    public List<Guid> Ids { get; set; } = new();
}
