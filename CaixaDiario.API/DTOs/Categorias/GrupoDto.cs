using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Categorias;

public class GrupoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Bloco { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; }
    public int QuantidadeCategorias { get; set; }
}

public class CriarGrupoDto
{
    [Required, MaxLength(100)] public string Nome { get; set; } = string.Empty;
    [Required] public string Bloco { get; set; } = string.Empty;
}

public class AtualizarGrupoDto
{
    [Required, MaxLength(100)] public string Nome { get; set; } = string.Empty;
    [Required] public string Bloco { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

public class ReordenarGruposDto
{
    public List<Guid> Ids { get; set; } = new();
}
