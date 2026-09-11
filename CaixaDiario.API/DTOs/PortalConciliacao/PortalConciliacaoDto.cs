using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.DTOs.Importacao;

namespace CaixaDiario.API.DTOs.PortalConciliacao;

public class PortalConciliacaoDto
{
    public DateTime ExpiraEm { get; set; }
    public List<ContaPendentesDto> Contas { get; set; } = new();
    public List<CategoriaItemDto> CategoriasEntrada { get; set; } = new();
    public List<CategoriaItemDto> CategoriasSaida { get; set; } = new();
}

public class ContaPendentesDto
{
    public Guid ContaBancariaId { get; set; }
    public string ContaNome { get; set; } = string.Empty;
    public List<PendenteCategorizacaoDto> Itens { get; set; } = new();
}
