namespace CaixaDiario.API.DTOs.ContasRecorrentes;

public class AtualizarContaRecorrenteDto
{
    public string? Descricao { get; set; }
    public decimal? Valor { get; set; }
    public string? Categoria { get; set; }
    public DateOnly? DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string? Periodicidade { get; set; }
    public Guid? ContaBancariaId { get; set; }
    // Quando true, propaga Descricao/Valor/Categoria/ContaBancariaId pras ocorrências pendentes
    // (não pagas) já materializadas — as já pagas nunca são tocadas. DataVencimento de cada
    // ocorrência é intrínseca a quando ela foi gerada e nunca muda por aqui.
    public bool AplicarAsPendentes { get; set; }
}
