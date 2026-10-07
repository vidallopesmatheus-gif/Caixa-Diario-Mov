namespace CaixaDiario.API.DTOs.Relatorios;

// Fase 1.5: compara, por categoria e mês, o valor que foi previsto quando a ocorrência recorrente
// foi materializada (ContaProvisionada.Valor — já é o valor do extrato recalculado, ver Fase 1.6)
// com o que de fato aconteceu (ValorRealizado quando pago, senão 0 — ainda não é dinheiro real).
public class PrevistoRealizadoDto
{
    public List<LinhaPrevistoRealizadoDto> Linhas { get; set; } = new();
}

public class LinhaPrevistoRealizadoDto
{
    public string Categoria { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty; // "Receber" ou "Pagar"
    public List<PontoPrevistoRealizadoDto> Meses { get; set; } = new();
}

public class PontoPrevistoRealizadoDto
{
    public string Mes { get; set; } = string.Empty; // "yyyy-MM"
    public decimal Previsto { get; set; }
    public decimal Realizado { get; set; }
    public decimal? VariacaoPercentual { get; set; }
    public bool VariacaoAlta { get; set; }
    public bool SubiuTresMesesSeguidos { get; set; }
}
