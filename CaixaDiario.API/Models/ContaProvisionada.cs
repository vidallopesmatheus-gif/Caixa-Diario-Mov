namespace CaixaDiario.API.Models;

public class ContaProvisionada
{
    // Guid.Empty pra itens legados (nunca precisaram de id estável, igual ItemFinanceiro.Id).
    // Itens novos (Fase 0.4) sempre recebem um Id de verdade — é o que permite editar/excluir por
    // Id em vez de regravar o RegistroDiario inteiro, e casar o item com segurança mesmo que
    // descrição/valor mudem depois (EhMesmoItem vira só um fallback pros itens sem Id).
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public bool Pago { get; set; } = false;
    public string? Categoria { get; set; }
    public Guid? RecorrenciaId { get; set; }
    public DateOnly? DataBaixa { get; set; }
    // Preenchido só quando o valor que efetivamente moveu na baixa (juros, multa, desconto) é
    // diferente do Valor original do título — Valor nunca muda, continua sendo a referência usada
    // pra casar o item em edições futuras (EhMesmoItem).
    public decimal? ValorRealizado { get; set; }
    public Guid? ContaBancariaId { get; set; }
    // Preenchido quando a baixa foi vinculada a um lançamento (Entrada/Saída) já existente,
    // em vez de gerar um novo — evita contar o mesmo dinheiro duas vezes no saldo.
    public Guid? LancamentoVinculadoId { get; set; }
    // true só quando o lançamento em LancamentoVinculadoId foi CRIADO por esta baixa (não um já
    // existente que o usuário escolheu vincular) — é o que diferencia o que o Estorno pode apagar
    // (o criado) do que ele só pode desvincular (o que já existia e representa dinheiro real).
    public bool LancamentoCriadoPelaBaixa { get; set; }
}
