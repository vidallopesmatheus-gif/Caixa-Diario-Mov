namespace CaixaDiario.API.DTOs.Usuarios;

// Bloco 3 da rodada "Visão de Futuro": preferências só da tela "Meu Futuro" — nunca influenciam
// nenhum outro cálculo financeiro do app.
public class PreferenciasFuturoDto
{
    public decimal? ReservaMinima { get; set; }
    // "PF" | "PJ" — null = ainda não configurado (a tela trata como "PJ").
    public string? TipoPessoa { get; set; }
}

public class AtualizarPreferenciasFuturoDto
{
    public decimal? ReservaMinima { get; set; }
    public string? TipoPessoa { get; set; }
}
