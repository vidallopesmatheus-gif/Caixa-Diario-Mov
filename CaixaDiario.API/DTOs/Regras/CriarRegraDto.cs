using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Regras;

public class CriarRegraDto
{
    [Required] public Guid ContaBancariaId { get; set; }
    [Required] public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"
    // Descrição do lançamento que originou a regra — o critério (Documento/DescricaoExata) é
    // derivado dela no servidor via DescricaoMatcher, nunca confiado ao cliente.
    [Required] public string DescricaoReferencia { get; set; } = string.Empty;
    [Required] public string AcaoTipo { get; set; } = string.Empty; // "Categoria" | "Transferencia"
    public string? Categoria { get; set; }
    public Guid? ContaContrapartidaId { get; set; }
}
