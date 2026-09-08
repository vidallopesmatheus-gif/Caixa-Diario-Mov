namespace CaixaDiario.API.DTOs.Categorias;

public class CategoriaDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public Guid GrupoId { get; set; }
    public string GrupoNome { get; set; } = string.Empty;
    public string Bloco { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativa { get; set; }
}
