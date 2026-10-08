namespace CaixaDiario.API.DTOs.Registros;

public class RegistroDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid? ContaBancariaId { get; set; }
    public DateOnly Data { get; set; }
    public decimal Inicio { get; set; }
    public List<ItemFinanceiroDto> Entradas { get; set; } = new();
    public List<ItemFinanceiroSaidaDto> Saidas { get; set; } = new();
    public List<ContaProvisionadaDto> ContasReceber { get; set; } = new();
    public List<ContaProvisionadaDto> ContasPagar { get; set; } = new();
    public decimal SaldoFinal { get; set; }
    public DateTime SalvoEm { get; set; }

    // Item 3.3: só preenchido pela resposta de "salvar" — quantas sugestões de vínculo (título
    // pendente × lançamento deste dia/conta) o motor de conciliação encontrou depois deste save.
    // Zero/padrão em qualquer outra leitura (listar, obter por data) — não vale a pena recalcular
    // isso pra cada registro de uma listagem inteira.
    public int TotalSugestoesVinculo { get; set; }
}
