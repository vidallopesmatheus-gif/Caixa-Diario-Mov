using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class DataLocalHelperTests
{
    [Theory]
    // 02:59 UTC de 11/out = 23:59 em São Paulo (UTC-3) no dia 10/out — ainda é "ontem" lá.
    [InlineData(2026, 10, 11, 2, 59, 2026, 10, 10)]
    // 03:00 UTC de 11/out = 00:00 em São Paulo no dia 11/out — já virou o dia.
    [InlineData(2026, 10, 11, 3, 0, 2026, 10, 11)]
    // Meio do dia: sem ambiguidade de fuso.
    [InlineData(2026, 6, 15, 12, 0, 2026, 6, 15)]
    public void Hoje_ComInstanteUtc_RetornaDiaCorretoEmSaoPaulo(
        int ano, int mes, int dia, int hora, int minuto,
        int anoEsperado, int mesEsperado, int diaEsperado)
    {
        var instanteUtc = new DateTime(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

        var resultado = DataLocalHelper.Hoje(instanteUtc);

        Assert.Equal(new DateOnly(anoEsperado, mesEsperado, diaEsperado), resultado);
    }
}
