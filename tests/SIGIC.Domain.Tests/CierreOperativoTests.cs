using SIGIC.Domain.Analytics;

namespace SIGIC.Domain.Tests;

public class CierreOperativoTests
{
    [Fact]
    public void CalculaResultadoDespuesDeGastosYRotacionAlCosto()
    {
        var cierre = new CierreOperativo(2, 1000m, 600m, 250m, 200m);
        Assert.Equal(2, cierre.DiasCerrados);
        Assert.Equal(150m, cierre.UtilidadOperativa);
        Assert.Equal(15m, cierre.MargenOperativo);
        Assert.Equal(3m, cierre.Rotacion);
    }

    [Fact]
    public void SinDenominadorConservaLaPerdidaYOmiteRatiosIndefinidos()
    {
        var cierre = new CierreOperativo(1, 0m, 0m, 50m, 0m);
        Assert.Equal(-50m, cierre.UtilidadOperativa);
        Assert.Null(cierre.MargenOperativo);
        Assert.Null(cierre.Rotacion);
    }
}
