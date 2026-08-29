using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Transferencias;

/// <summary>
/// Reclassifica um lançamento já existente (ex.: "Aplicação RDB" importado como saída) como
/// Transferência, criando a perna contrapartida na conta informada — sem duplicar ou inflar o DRE.
/// </summary>
public class ConverterLancamentoEmTransferenciaDto
{
    [Required] public Guid ContaId { get; set; }          // conta onde o lançamento hoje vive
    [Required] public Guid LancamentoId { get; set; }
    [Required] public DateOnly Data { get; set; }
    [Required] public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"
    [Required] public Guid ContaContrapartidaId { get; set; }   // destino (saída) ou origem (entrada)

    // Preenchidos só quando o usuário escolheu VINCULAR a um lançamento já existente na contrapartida
    // (ex.: o extrato da conta de investimento já trouxe a entrada correspondente) — nesse caso não
    // cria lançamento novo, só relabela os dois como Transferência.
    public Guid? LancamentoContrapartidaId { get; set; }
    public DateOnly? DataContrapartida { get; set; }
}
