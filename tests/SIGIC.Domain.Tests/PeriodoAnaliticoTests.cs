using SIGIC.Domain.Analytics;

namespace SIGIC.Domain.Tests;

public class PeriodoAnaliticoTests
{
    [Fact]
    public void DiaContableLima_IniciaALasCincoUtc()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var periodo = new PeriodoAnalitico(fecha, fecha.AddDays(366));
        Assert.Equal(366, periodo.TotalDias);
        Assert.Equal(new DateTime(2026, 1, 1, 5, 0, 0, DateTimeKind.Utc), periodo.DesdeUtc);
        Assert.Equal(periodo.DesdeUtc.AddDays(366), periodo.HastaExclusivoUtc);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(367)]
    public void RechazaRangosInvalidos(int dias)
    {
        var inicio = new DateOnly(2026, 1, 1);
        Assert.Throws<ArgumentException>(() => new PeriodoAnalitico(inicio, inicio.AddDays(dias)));
    }
}
