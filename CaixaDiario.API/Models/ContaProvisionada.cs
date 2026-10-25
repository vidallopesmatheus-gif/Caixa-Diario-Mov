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
    // Fase 5: preenchido quando esta conta a pagar foi criada por "🏆 Conquistei" (forma Boleto) —
    // "Desfazer conquista" usa isso pra achar e remover as parcelas ainda não pagas.
    public Guid? OrigemMetaId { get; set; }

    // ── Fase 6: parcela de financiamento/empréstimo/consórcio ───────────────────────────────────
    // Preenchido quando esta conta a pagar é uma parcela gerada por FinanciamentoService — a
    // tabela de amortização "ao vivo" é só a lista de ContaProvisionada com este Id em comum,
    // nunca duplicada em outro lugar.
    public Guid? OrigemFinanciamentoId { get; set; }
    // Decomposição planejada da parcela (preenchida na criação, igual vem da tabela de
    // amortização) — Valor continua sendo o total (Juros + Amortizacao + Encargos).
    public decimal? ValorJuros { get; set; }
    public decimal? ValorAmortizacao { get; set; }
    public decimal? ValorEncargos { get; set; }
    // Preenchidos na baixa — diferente de LancamentoVinculadoId (singular, usado pelo fluxo
    // genérico de ContaProvisionada), uma parcela de financiamento baixa em ATÉ 3 lançamentos
    // (juros entra no resultado operacional, amortização fica em Atividades de Financiamento,
    // encargos é uma despesa comum) — por isso precisa de um slot por componente.
    public Guid? LancamentoJurosId { get; set; }
    public Guid? LancamentoAmortizacaoId { get; set; }
    public Guid? LancamentoEncargosId { get; set; }

    // ── Money Vision: parcela futura de um parcelamento de cartão, ainda só prevista ───────────
    // Preenchido quando esta conta a pagar é uma parcela de Parcelamento ainda não confirmada por
    // nenhuma fatura real (ver ImportacaoService.ProcessarParcelaDetectadaAsync). Quando a fatura
    // do mês correspondente chega e traz essa mesma parcela real, esta linha é removida (nunca
    // fica paga/baixada — a parcela real em ItemFinanceiroSaida é que passa a representar o gasto).
    public Guid? ParcelamentoId { get; set; }
    public int? NumeroParcela { get; set; }
}
