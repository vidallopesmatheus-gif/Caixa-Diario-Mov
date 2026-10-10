namespace CaixaDiario.API.Models;

/// <summary>Fase 6: financiamento/empréstimo/consórcio/parcelamento. As parcelas em si são
/// ContaProvisionada (OrigemFinanciamentoId) — esta entidade só guarda os parâmetros do contrato
/// e os totais acumulados (adiantamentos), nunca a tabela de amortização duplicada.</summary>
public class Financiamento
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    // "Financiamento" | "Emprestimo" | "Consorcio" | "Parcelamento"
    public string Tipo { get; set; } = "Financiamento";
    public decimal ValorBem { get; set; }
    public decimal ValorEntrada { get; set; }
    public decimal ValorFinanciado { get; set; }
    public int NumeroParcelas { get; set; }
    // Informado pelo usuário OU calculado (Price) a partir da taxa — sempre o valor "de tabela"
    // da parcela regular (a 1ª e a última podem diferir por arredondamento/encargos).
    public decimal? ValorParcela { get; set; }
    // Sempre preenchida ao salvar — se o usuário não informou, foi resolvida via taxa implícita.
    public decimal? TaxaJurosMensal { get; set; }
    // "Price" | "SAC" | "SemJuros"
    public string Sistema { get; set; } = "Price";
    public DateOnly DataPrimeiraParcela { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string? BemVinculado { get; set; }
    // "Ativo" | "Quitado"
    public string Status { get; set; } = "Ativo";
    public Guid? OrigemMetaId { get; set; }
    // Totais acumulados de amortização extraordinária (Fase 6.5) — não guarda o histórico evento a
    // evento, só a soma; o extrato (ContaProvisionada com OrigemFinanciamentoId) já é o registro
    // de quais parcelas sofreram adiantamento.
    public decimal ValorAdiantado { get; set; }
    public decimal EconomiaJurosAcumulada { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria ContaBancaria { get; set; } = null!;
}
