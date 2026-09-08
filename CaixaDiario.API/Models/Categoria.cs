namespace CaixaDiario.API.Models;

public class Categoria
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    // "Receita" | "CustoVariavel" | "CustoFixo" | "DespesaNaoOperacional" | "Investimento" | "Financiamento"
    // Derivado automaticamente do Bloco do Grupo escolhido (ver Blocos.TipoPadrao) — não é mais
    // escolhido diretamente na criação da categoria.
    public string Tipo { get; set; } = string.Empty;
    public Guid GrupoId { get; set; }
    public int Ordem { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; }

    public Grupo Grupo { get; set; } = null!;
}
