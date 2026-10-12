using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Transferencias;

/// <summary>
/// Descarta a sugestão "parece transferência" (Bloco 4, item 4) — o lançamento volta a ser um
/// pendente comum (sem a categoria "Transferência"), nunca é excluído.
/// </summary>
public class DescartarSugestaoTransferenciaDto
{
    [Required] public Guid ContaId { get; set; }
    [Required] public Guid LancamentoId { get; set; }
    [Required] public DateOnly Data { get; set; }
}
