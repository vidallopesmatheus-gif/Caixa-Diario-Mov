namespace CaixaDiario.Tests.Services;

/// <summary>TimeProvider parado num instante — "hoje" determinístico nos testes (meio-dia em São
/// Paulo, longe da virada de dia UTC).</summary>
public sealed class RelogioFixo : TimeProvider
{
    private readonly DateTimeOffset _agora;

    public RelogioFixo(DateOnly hoje) => _agora = new DateTimeOffset(hoje.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _agora;
}
