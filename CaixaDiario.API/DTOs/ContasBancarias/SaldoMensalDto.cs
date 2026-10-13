namespace CaixaDiario.API.DTOs.ContasBancarias;

/// <summary>Um ponto do gráfico "saldo mês a mês" (Money Vision — Bloco 5, item 3 da Parte 3).</summary>
public class SaldoMensalDto
{
    public string Mes { get; set; } = string.Empty; // "yyyy-MM"
    public decimal Saldo { get; set; }
}
