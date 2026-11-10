namespace CaixaDiario.API.DTOs.ConciliacaoSaldo;

/// <summary>Saldo do banco numa data × saldo do app no fim daquele dia (sempre recalculado na leitura).</summary>
public class PontoConciliacaoDto
{
    public DateOnly Data { get; set; }
    public decimal SaldoBanco { get; set; }
    public decimal SaldoApp { get; set; }
    // Banco − app: o quanto falta (positivo) ou sobra (negativo) no app pra bater.
    public decimal Diferenca { get; set; }
    public bool Conciliado { get; set; }
    public string Origem { get; set; } = string.Empty;
}

/// <summary>"Extrato até 07/11/2026 · Saldo do app R$ X · Banco em 07/11 R$ X ✅".</summary>
public class StatusConciliacaoDto
{
    // Último dia com lançamento vindo de extrato importado.
    public DateOnly? ExtratoAte { get; set; }
    // Dias corridos entre ExtratoAte e hoje — a tela avisa "faltam lançamentos" a partir de 5.
    public int? DiasSemExtrato { get; set; }
    public decimal SaldoApp { get; set; }
    public PontoConciliacaoDto? UltimoPonto { get; set; }
    // Primeiro ponto que diverge depois do último ponto conciliado (só quando o último diverge).
    public DateOnly? DivergenteDesde { get; set; }
}

public class SaldoDoDiaDto
{
    public DateOnly Data { get; set; }
    public decimal SaldoFinal { get; set; }
}

/// <summary>Linha do tempo do extrato: dias cobertos, pontos de conciliação e saldo por dia.</summary>
public class CoberturaExtratoDto
{
    public List<DateOnly> DiasComExtrato { get; set; } = new();
    public List<PontoConciliacaoDto> Pontos { get; set; } = new();
    public List<SaldoDoDiaDto> SaldosPorDia { get; set; } = new();
    public StatusConciliacaoDto Status { get; set; } = new();
}

public class LinhaDiagnosticoDto
{
    public Guid Id { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    // Com sinal: entrada positiva, saída negativa (igual ao extrato).
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    // Quanto o saldo do app muda se esta linha for corrigida (excluída, ou sinal invertido).
    public decimal EfeitoSeCorrigir { get; set; }
}

public class PeriodoDto
{
    public DateOnly De { get; set; }
    public DateOnly Ate { get; set; }
}

public class GrupoDuplicataDto
{
    // A primeira fica; as demais são as suspeitas de duplicata.
    public List<LinhaDiagnosticoDto> Itens { get; set; } = new();
    public decimal SomaExcedente { get; set; }
}

public class CombinacaoDiagnosticoDto
{
    // "Excluir estas 2 duplicatas zera a diferença".
    public string Resumo { get; set; } = string.Empty;
    public List<LinhaDiagnosticoDto> Itens { get; set; } = new();
}

/// <summary>"Diferença de R$ 684,06 em 07/11. Possíveis causas: …" — antes de qualquer ajuste.</summary>
public class DiagnosticoDiferencaDto
{
    public DateOnly Data { get; set; }
    // Nulos quando não há saldo do banco informado nem registrado nesta data — as causas
    // (lacunas, duplicatas…) continuam listadas.
    public decimal? SaldoBanco { get; set; }
    public decimal SaldoApp { get; set; }
    public decimal? Diferenca { get; set; }
    // Período analisado: do dia seguinte ao último ponto conciliado até Data.
    public DateOnly AnalisadoDesde { get; set; }
    public List<PeriodoDto> Lacunas { get; set; } = new();
    public List<GrupoDuplicataDto> Duplicatas { get; set; } = new();
    public List<LinhaDiagnosticoDto> SinalSuspeito { get; set; } = new();
    public List<LinhaDiagnosticoDto> TransferenciasSemPar { get; set; } = new();
    public List<LinhaDiagnosticoDto> Manuais { get; set; } = new();
    // Ajustes de saldo no período — escondem dado faltando (mês sem extrato, transferência sem
    // par); EfeitoSeCorrigir = desfazer o ajuste.
    public List<LinhaDiagnosticoDto> Ajustes { get; set; } = new();
    public CombinacaoDiagnosticoDto? Combinacao { get; set; }
}

/// <summary>Um ajuste de saldo da conta, na tela "Conferir saldo" — pra confirmar ou desfazer.</summary>
public class AjusteSaldoDto
{
    public Guid Id { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    // Com sinal: entrada positiva, saída negativa.
    public decimal Valor { get; set; }
    // Saldo no fim do dia sem este ajuste e com ele — "levou o saldo de X para Y".
    public decimal SaldoDoDiaSemAjuste { get; set; }
    public decimal SaldoDoDia { get; set; }
    public bool Confirmado { get; set; }
}

public class RegistrarSaldoBancoDto
{
    public DateOnly Data { get; set; }
    public decimal SaldoBanco { get; set; }
}
