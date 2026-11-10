namespace CaixaDiario.API.DTOs.Admin;

public class DivergenciaSaldoContaDto
{
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    // Primeira data em que o Inicio gravado não batia com o SaldoFinal do dia anterior (ou com o
    // SaldoInicial da conta, se for o primeiro dia) — é a partir daqui que o extrato mostrava saldo
    // acumulado errado.
    public DateOnly PrimeiraDataDivergente { get; set; }
    public decimal SaldoAtualAntes { get; set; }
    public decimal SaldoAtualDepois { get; set; }
}

public class RecalculoSaldoResultDto
{
    // false = só relatório, nada foi gravado; true = a cadeia de Inicio/SaldoFinal de cada conta
    // listada já foi corrigida (RegistroService.RecalcularContaInteiraAsync, a partir do
    // SaldoInicial da conta), preservando o efeito líquido que cada dia já tinha.
    public bool Confirmado { get; set; }
    public int ContasComDivergencia { get; set; }
    public List<DivergenciaSaldoContaDto> Itens { get; set; } = new();
}

// Money Vision ("ajuste mexeu no DRE"): lançamentos de ajuste de saldo antigos (categoria/descrição
// "Ajuste de saldo" ou tipo legado "AjusteSaldo") que passam pro tipo de sistema AjusteConciliacao.
public class MigracaoAjustesConciliacaoResultDto
{
    public bool Confirmado { get; set; }
    public int LancamentosConvertidos { get; set; }
    public List<AjusteConvertidoDto> Itens { get; set; } = new();
}

public class AjusteConvertidoDto
{
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? TipoCustoAnterior { get; set; }
    public string? CategoriaAnterior { get; set; }
}

// Money Vision ("ajustes da migração que o usuário não entende"): pares de ajuste em 30/09 → 01/10
// que zeravam e reabriam a conta viram um único ajuste líquido; os que se anulam saem.
public class ConsolidacaoAjustesResultDto
{
    public bool Confirmado { get; set; }
    public List<AjusteConsolidadoDto> Itens { get; set; } = new();
}

public class AjusteConsolidadoDto
{
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    // "RemoverPar" (somam zero), "Consolidar" (vira um ajuste líquido) ou "Renomear" (descrição "saldo").
    public string Acao { get; set; } = string.Empty;
    public List<AjusteConvertidoDto> Originais { get; set; } = new();
    // Nulo quando o par some inteiro.
    public decimal? ValorFinal { get; set; }
    public DateOnly? DataFinal { get; set; }
    public string? DescricaoNova { get; set; }
    // Sempre iguais — a consolidação nunca muda o saldo atual da conta.
    public decimal SaldoAtualAntes { get; set; }
    public decimal SaldoAtualDepois { get; set; }
}
