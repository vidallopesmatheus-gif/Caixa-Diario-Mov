namespace CaixaDiario.API.Models;

/// <summary>
/// Histórico da trava de período de uma conta: cada "Conferir saldo" que bateu com o banco
/// (Tipo = Conferencia) e cada reabertura (Tipo = Reabertura). A trava vigente fica em
/// ContaBancaria.TravadoAte; aqui é a auditoria.
/// </summary>
public class ConferenciaSaldo
{
    public const string TipoConferencia = "Conferencia";
    public const string TipoReabertura = "Reabertura";

    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string Tipo { get; set; } = TipoConferencia;
    // Conferência: o dia conferido. Reabertura: a data a partir da qual reabriu (null = tudo).
    public DateOnly? Data { get; set; }
    public decimal? SaldoBanco { get; set; }
    public DateOnly? TravadoAteAntes { get; set; }
    public DateOnly? TravadoAteDepois { get; set; }
    public Guid UsuarioId { get; set; }
    public string Perfil { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    // Conferência: os lançamentos da conta na janela antes da data (JSON), pra dizer o que mudou
    // se a próxima conferência não bater.
    public string? Retrato { get; set; }
    public DateTime OcorridoEm { get; set; }
}
