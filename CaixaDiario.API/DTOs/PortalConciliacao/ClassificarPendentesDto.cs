namespace CaixaDiario.API.DTOs.PortalConciliacao;

public class ClassificarPendentesDto
{
    public List<ClassificarPendenteItem> Itens { get; set; } = new();
}

public class ClassificarPendenteItem
{
    public Guid Id { get; set; }
    public string Data { get; set; } = string.Empty; // ISO "yyyy-MM-dd"
    public Guid ContaBancariaId { get; set; }
    public string Categoria { get; set; } = string.Empty;
}
