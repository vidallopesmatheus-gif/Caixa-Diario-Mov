namespace CaixaDiario.API.DTOs.Metricas;

public class DreCategoriaDto
{
    public string Nome { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class DreLinhaDto
{
    public string Grupo { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<DreCategoriaDto> Categorias { get; set; } = new();
}

public class DreDto
{
    public decimal ReceitaBruta { get; set; }
    public List<DreLinhaDto> GruposDespesa { get; set; } = new();
    public decimal TotalDespesas { get; set; }
    public decimal Resultado { get; set; }
    public decimal? Margem { get; set; }
}
