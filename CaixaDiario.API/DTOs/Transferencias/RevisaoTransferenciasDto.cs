namespace CaixaDiario.API.DTOs.Transferencias;

/// <summary>"Transferências para revisar": o que se corrige sozinho e o que o usuário decide.</summary>
public class RevisaoTransferenciasDto
{
    // Contrapartida inventada + lançamento real solto, par duplicado, provisória sem extrato —
    // o botão "Corrigir" resolve todas de uma vez (ver CorrigirAsync).
    public List<ItemCorrecaoTransferenciaDto> Corrigiveis { get; set; } = new();
    // A conta tem extrato na data e não tem o lançamento: pra onde o dinheiro foi?
    public List<TransferenciaParaRevisarDto> ParaDecidir { get; set; } = new();
}

public class TransferenciaParaRevisarDto
{
    public Guid TransferenciaId { get; set; }
    public DateOnly Data { get; set; }
    // Com sinal, na conta onde o app criou a contrapartida.
    public decimal Valor { get; set; }
    public Guid ContaCriadaId { get; set; }
    public string ContaCriadaNome { get; set; } = string.Empty;
    public string ContaOutraNome { get; set; } = string.Empty;
    // Descrição do lançamento real, na outra conta.
    public string DescricaoOriginal { get; set; } = string.Empty;
}

public class ItemCorrecaoTransferenciaDto
{
    public Guid ClienteId { get; set; }
    public Guid TransferenciaId { get; set; }
    // "DuplicadaComReal", "ParDuplicado", "AguardandoExtrato" ou "SemLancamentoNoExtrato".
    public string Tipo { get; set; } = string.Empty;
    public string Acao { get; set; } = string.Empty;
    public string ContaNome { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public decimal Valor { get; set; }
    public string DescricaoCriada { get; set; } = string.Empty;
    public string? DescricaoReal { get; set; }
}

public class SaldoContaCorrecaoDto
{
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    public decimal SaldoAtualAntes { get; set; }
    public decimal SaldoAtualDepois { get; set; }
}

public class CorrecaoTransferenciasResultDto
{
    // false = só relatório, nada foi gravado.
    public bool Confirmado { get; set; }
    public List<ItemCorrecaoTransferenciaDto> Itens { get; set; } = new();
    public List<SaldoContaCorrecaoDto> Saldos { get; set; } = new();
}
