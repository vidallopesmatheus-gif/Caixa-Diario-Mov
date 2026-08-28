namespace CaixaDiario.API.DTOs.Importacao;

/// <summary>Um lançamento já real (afeta saldo) que ainda não tem categoria — só a saída, hoje.</summary>
public class PendenteCategorizacaoDto
{
    public Guid Id { get; set; }
    public string Data { get; set; } = string.Empty; // ISO "yyyy-MM-dd" — identifica o RegistroDiario do item
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = "Saida";
}
