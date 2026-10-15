namespace CaixaDiario.API.Models;

// Correção de integridade (Money Vision — cartão de crédito, Bloco 1): fatura passa a ser uma
// entidade de verdade, com identidade própria — antes era 100% calculada na hora, agrupando
// compras pela competência INFERIDA da data de cada uma (ver FaturaCartaoService.ObterCompetencia),
// o que partia uma fatura real em duas quando o dia de fechamento configurado não batia exatamente
// com o ciclo do banco. Cada importação de fatura cria UMA linha aqui; as compras daquele arquivo
// se ligam a ela por ItemFinanceiroSaida.FaturaId (nunca mais por data).
//
// ValorTotal/ValorPago/Status continuam calculados na hora (nunca persistidos) — mesmo espírito já
// usado para PagamentoFatura: a fonte de verdade é sempre a soma das compras/pagamentos vinculados,
// nunca um campo que pode dessincronizar.
public class Fatura
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; } // a conta "Cartões"
    // Texto livre (ex.: "Nubank", "C6", "Itaú") — a lista editável do front é só os valores
    // distintos já usados antes; digitar um nome novo "cria" o emissor, sem cadastro à parte.
    public string Banco { get; set; } = string.Empty;
    // "yyyy-MM" — referência (mês/ano) sugerida pelo vencimento na importação, mas editável;
    // usada só para exibição/ordenação, nunca para decidir quais compras pertencem à fatura.
    public string Competencia { get; set; } = string.Empty;
    public DateOnly? DataFechamento { get; set; }
    public DateOnly DataVencimento { get; set; }
    public DateTime CriadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria ContaBancaria { get; set; } = null!;
}
