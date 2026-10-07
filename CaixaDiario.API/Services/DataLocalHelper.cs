namespace CaixaDiario.API.Services;

// O negócio é sempre de clientes brasileiros — "hoje" tem que ser o dia em América/São Paulo,
// nunca UTC (DateTime.UtcNow vira o dia seguinte a partir das 21h local). Nunca usar isso para
// timestamps de auditoria (CriadoEm/SalvoEm/AtualizadoEm) — esses continuam em UTC.
public static class DataLocalHelper
{
    private static readonly TimeZoneInfo FusoBrasil = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateOnly Hoje() => Hoje(DateTime.UtcNow);

    // Overload com o instante UTC explícito — existe só pra dar um ponto de injeção nos testes
    // (determinismo: sem isso, testar a virada de dia perto da meia-noite dependeria do relógio
    // real da máquina que roda o teste).
    public static DateOnly Hoje(DateTime instanteUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(instanteUtc, FusoBrasil));
}
