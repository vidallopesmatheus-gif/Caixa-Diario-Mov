namespace CaixaDiario.API.DTOs.Regras;

public class RegraCategorizacaoDto
{
    public Guid Id { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string CriterioTipo { get; set; } = string.Empty;
    public string CriterioValor { get; set; } = string.Empty;
    public string DescricaoReferencia { get; set; } = string.Empty;
    public string AcaoTipo { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public Guid? ContaContrapartidaId { get; set; }
    public string? ContaContrapartidaNome { get; set; }
    public bool Ativa { get; set; }
    public int Ordem { get; set; }
    // Calculado ao vivo (itens com RegraCategorizacaoId == Id) — nunca armazenado, pra não
    // desalinhar do que realmente existe nos lançamentos.
    public int QuantidadeAplicada { get; set; }
    public DateTime CriadoEm { get; set; }
}
