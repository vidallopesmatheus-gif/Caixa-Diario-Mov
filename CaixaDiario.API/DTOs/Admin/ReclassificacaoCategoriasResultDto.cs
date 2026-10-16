namespace CaixaDiario.API.DTOs.Admin;

public class ReclassificacaoCategoriaItemDto
{
    public Guid ClienteId { get; set; }
    public Guid CategoriaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string TipoMovimentoAtual { get; set; } = string.Empty;
    public string TipoMovimentoSugerido { get; set; } = string.Empty;
    public int QuantidadeEntradas { get; set; }
    public int QuantidadeSaidas { get; set; }
}

public class ReclassificacaoCategoriasResultDto
{
    // false = só relatório, nada foi gravado; true = as mudanças abaixo já foram persistidas.
    public bool Confirmado { get; set; }
    public int CategoriasAnalisadas { get; set; }
    public int CategoriasReclassificadas { get; set; }
    public List<ReclassificacaoCategoriaItemDto> Itens { get; set; } = new();
}
