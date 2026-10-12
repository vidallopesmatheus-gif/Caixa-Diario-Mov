namespace CaixaDiario.API.DTOs.ContasRecorrentes;

// Item 2 do Bloco 4 ("Visão de Futuro"): um padrão de lançamento repetido no histórico que ainda
// não tem ContaRecorrente cadastrada — pronto pra virar uma em 2 cliques ("Criar Contas a Pagar?").
public class SugestaoRecorrenciaDto
{
    // Identifica o PADRÃO (não uma ocorrência) — usada pra ignorar e nunca mais sugerir.
    public string Chave { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Receber" | "Pagar"
    public decimal ValorMedio { get; set; }
    public int QuantidadeOcorrencias { get; set; }
    public Guid? ContaBancariaId { get; set; }
    public DateOnly UltimaOcorrencia { get; set; }
}

public class IgnorarSugestaoRecorrenciaDto
{
    public string Chave { get; set; } = string.Empty;
}
