namespace CaixaDiario.API.DTOs.Admin;

// Money Vision: "fatura do C6 dividida em duas + pagamentos não vinculados" (item 3) — migração
// de reparo: mescla faturas duplicadas (mesmo banco + competência + conta) e tenta vincular
// automaticamente um pagamento (transferência) já existente que bate com cada fatura ainda aberta.
public class FaturaMescladaDto
{
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string Banco { get; set; } = string.Empty;
    public string Competencia { get; set; } = string.Empty;
    public Guid FaturaMantidaId { get; set; }
    public Guid FaturaRemovidaId { get; set; }
}

public class CorrigirFaturasResultDto
{
    // false = só relatório, nada foi gravado; true = faturas já mescladas e pagamentos já vinculados.
    public bool Confirmado { get; set; }
    public int FaturasMescladas { get; set; }
    public int PagamentosVinculados { get; set; }
    public List<FaturaMescladaDto> Mesclagens { get; set; } = new();
}
