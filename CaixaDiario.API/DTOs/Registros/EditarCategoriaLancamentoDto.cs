using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Registros;

/// <summary>Item E2 do bundle D6JWEAJa: reclassificar um lançamento já existente a partir do
/// drill-down da DRE (diferente de AtualizarCategoriaDto/ImportacaoService, que só atende itens
/// ainda pendentes de uma conta só — este edita qualquer lançamento e grava auditoria).</summary>
public class EditarCategoriaLancamentoDto
{
    [Required] public Guid ContaBancariaId { get; set; }
    [Required] public Guid LancamentoId { get; set; }
    // "Entrada" | "Saida"
    [Required] public string Tipo { get; set; } = "Saida";
    [Required] public DateOnly Data { get; set; }
    [Required] public string CategoriaNova { get; set; } = string.Empty;
    // Aplica a mesma categoria a outros lançamentos do cliente com a mesma descrição (normalizada)
    // que ainda estejam sem categoria — nunca sobrescreve um lançamento já classificado por outra mão.
    public bool AplicarASimilares { get; set; }
}

public class EditarCategoriaLancamentoResultDto
{
    public string? CategoriaAnterior { get; set; }
    public int SimilaresAtualizados { get; set; }
}
