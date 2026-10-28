namespace CaixaDiario.API.DTOs.FaturasCartao;

// Money Vision: "parcelamentos que já existem não aparecem" (item 5) — "Procurar parcelamentos"
// varre as compras já importadas ANTES de a detecção existir e as liga a um Parcelamento
// retroativamente, igual já acontece pra toda importação nova.
public class ParcelamentoEncontradoRetroativamenteDto
{
    public Guid ParcelamentoId { get; set; }
    public string Comerciante { get; set; } = string.Empty;
    public int TotalParcelas { get; set; }
    public int MaiorParcelaJaReal { get; set; }
    public int ParcelasRestantes { get; set; }
    public decimal ValorRestante { get; set; }
    public DateOnly? DataFinal { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DeteccaoRetroativaParcelamentoResultDto
{
    public int ParcelamentosEncontrados { get; set; }
    public decimal ValorRestanteTotal { get; set; }
    public DateOnly? DataFinal { get; set; }
    public List<ParcelamentoEncontradoRetroativamenteDto> Parcelamentos { get; set; } = new();
}
