namespace CaixaDiario.API.DTOs.Categorias;

public class CategoriaDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Grupo { get; set; }
    public int Ordem { get; set; }
    public bool Ativa { get; set; }
}
