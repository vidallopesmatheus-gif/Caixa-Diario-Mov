namespace CaixaDiario.API.Models;

public class ItemFinanceiro
{
    // Guid.Empty para lançamentos manuais antigos (nunca precisaram de id estável).
    // Itens importados sempre recebem um Id novo, usado para localizar o item na
    // categorização posterior sem precisar de outra tabela.
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCusto { get; set; }
    // Preenchido apenas quando TipoCusto == "Transferencia": liga as duas pontas do par.
    public Guid? TransferenciaId { get; set; }
    // Preenchido apenas quando TipoCusto == "PagamentoFatura": esta entrada é a contrapartida, na
    // própria conta do cartão, de uma saída de conta corrente paga como fatura (reduz o saldo
    // devedor). Liga ao mesmo PagamentoFatura da saída original — ver FaturaCartaoService.
    public Guid? PagamentoFaturaId { get; set; }
    // Identificador único do OFX (dedup) — nulo para lançamentos manuais ou vindos de CSV/XLSX.
    public string? FitId { get; set; }
    // Verdadeiro para toda entrada importada (nunca tem sugestão automática de categoria —
    // só o usuário sabe distinguir receita real de transferência/resgate).
    public bool PendenteCategorizacao { get; set; }
    // Preenchido quando a categoria veio de uma RegraCategorizacao aplicada na importação —
    // corrigir manualmente NÃO limpa isso nem altera a regra (só marca a origem pra exibição).
    public Guid? RegraCategorizacaoId { get; set; }
    // Verdadeiro quando quem escolheu a categoria foi o próprio cliente, pelo portal de
    // conciliação (link público) — nunca setado quando o consultor categoriza pela tela normal.
    public bool ClassificadoPeloCliente { get; set; }
    // Verdadeiro só pra contrapartida de Transferência CRIADA automaticamente (TransferenciaService.
    // ConverterLancamentoAsync) antes do extrato real da conta de destino ter sido importado — ainda
    // não foi confirmada pelo banco. A importação tenta casar essa provisória com a transação real
    // (mesmo valor, mesmo sentido, ±2 dias úteis) em vez de duplicar — ver ImportacaoService.
    public bool Provisoria { get; set; }
    // Nome do favorecido/pagador já limpo (sem prefixo de banco, CPF/CNPJ, agência/conta) —
    // preenchido na importação por ContraparteExtractor. Nulo em lançamentos manuais antigos.
    public string? ContraparteNome { get; set; }
    // CPF/CNPJ parcial (mascarado — nunca completo, ver Bloco 5B) quando a descrição trazia um.
    public string? ContraparteDocumento { get; set; }
    // Chave de agrupamento: "DOC:<dígitos completos>" quando o documento foi extraído sem máscara,
    // senão "NOME:<nome normalizado>" — usada pra juntar a mesma contraparte entre bancos
    // diferentes (ex.: 3B) sem expor o documento completo na chave.
    public string? ContraparteChave { get; set; }
}
