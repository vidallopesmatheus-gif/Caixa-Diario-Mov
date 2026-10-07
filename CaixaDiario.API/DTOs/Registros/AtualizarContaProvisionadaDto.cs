namespace CaixaDiario.API.DTOs.Registros;

public class AtualizarContaProvisionadaDto
{
    // Edição simples do título — null = não muda.
    public string? Descricao { get; set; }
    public decimal? Valor { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public string? Categoria { get; set; }
    public Guid? ContaBancariaId { get; set; }

    // Baixa / estorno — Pago null = não muda o status atual.
    public bool? Pago { get; set; }
    // Data em que o dinheiro de fato moveu (padrão: hoje). Só usada quando Pago vira true.
    public DateOnly? DataPagamento { get; set; }
    // Só preenchido quando o valor realizado difere do Valor do título (juros/multa/desconto).
    public decimal? ValorRealizado { get; set; }
    // Quando informado na baixa, vincula a esse lançamento já existente no extrato em vez de
    // criar um novo (evita contar o mesmo dinheiro duas vezes).
    public Guid? LancamentoVinculadoId { get; set; }
}
