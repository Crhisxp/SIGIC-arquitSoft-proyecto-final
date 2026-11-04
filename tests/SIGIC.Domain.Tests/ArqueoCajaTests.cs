using SIGIC.Domain.Entities;
using Xunit;

namespace SIGIC.Domain.Tests;

public class ArqueoCajaTests
{
    private List<ArqueoDenominacion> CrearDesgloseFisicoEjemplo()
    {
        return new List<ArqueoDenominacion>
        {
            // Billetes:
            new(200m, 5),   // S/ 1,000.00
            new(100m, 12),  // S/ 1,200.00
            new(50m, 10),   // S/   500.00
            new(20m, 15),   // S/   300.00
            new(10m, 20),   // S/   200.00
            // Monedas:
            new(5m, 30),    // S/   150.00
            new(2m, 25),    // S/    50.00
            new(1m, 40),    // S/    40.00
            new(0.50m, 20), // S/    10.00
            new(0.20m, 30), // S/     6.00
            new(0.10m, 40)  // S/     4.00
            // Total Efectivo: S/ 3,460.00
        };
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void a_ConteoFisicoInmutable_SumaExactamenteDenominacionesDeBilletesYMonedas()
    {
        // Arrange
        var desglose = CrearDesgloseFisicoEjemplo();
        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var fecha = DateTime.UtcNow;

        // Act
        var arqueo = new ArqueoCaja(
            id: Guid.NewGuid(),
            turnoCajaId: turnoId,
            cajeroId: cajeroId,
            fechaAperturaUtc: fecha.AddHours(-8),
            fechaCierreUtc: fecha,
            fondoInicialSencillo: 200.00m,
            desgloseFisico: desglose,
            totalDigitalDeclarado: 540.00m); // Yape / Plin / POS

        // Assert
        Assert.True(arqueo.EsAppendOnly);
        Assert.Equal(3460.00m, arqueo.TotalEfectivoDeclarado);
        Assert.Equal(540.00m, arqueo.TotalDigitalDeclarado);
        Assert.Equal(4000.00m, arqueo.TotalDeclarado); // 3460 + 540
        Assert.Equal(11, arqueo.DesgloseFisico.Count);
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void b_CalculoExactoDeFaltanteYSobrante_NoMutaElRegistroPrevio()
    {
        // Arrange
        var desglose = new List<ArqueoDenominacion>
        {
            new(100m, 10), // S/ 1,000.00
            new(50m, 4),   // S/   200.00
            new(10m, 10)   // S/   100.00
            // Total: S/ 1,300.00
        };

        var arqueoOriginal = new ArqueoCaja(
            id: Guid.NewGuid(),
            turnoCajaId: Guid.NewGuid(),
            cajeroId: Guid.NewGuid(),
            fechaAperturaUtc: DateTime.UtcNow.AddHours(-8),
            fechaCierreUtc: DateTime.UtcNow,
            fondoInicialSencillo: 100.00m,
            desgloseFisico: desglose,
            totalDigitalDeclarado: 0m,
            saldoSistemaTeorico: 0m);

        // Act 1: Simular cálculo con faltante (sistema esperaba S/ 1,350.00)
        var diferenciaFaltante = arqueoOriginal.CalcularDiferenciaContable(1350.00m);
        Assert.Equal(-50.00m, diferenciaFaltante); // Faltante de S/ 50.00

        // Verificar inmutabilidad: el registro original no cambió
        Assert.Equal(0m, arqueoOriginal.SaldoSistemaTeorico);
        Assert.Equal(1300.00m, arqueoOriginal.Diferencia);

        // Act 2: Generar nuevo registro append-only con saldo contrastado
        var arqueoContrastado = arqueoOriginal.ConSaldoTeorico(1350.00m);

        // Assert: Instancia nueva refleja la diferencia, original permanece idéntica
        Assert.NotSame(arqueoOriginal, arqueoContrastado);
        Assert.Equal(1350.00m, arqueoContrastado.SaldoSistemaTeorico);
        Assert.Equal(-50.00m, arqueoContrastado.Diferencia);
        Assert.True(arqueoContrastado.TieneDiscrepancia());

        // La instancia previa se mantuvo 100% inalterada
        Assert.Equal(0m, arqueoOriginal.SaldoSistemaTeorico);

        // Act 3: Caso de sobrante (declarado S/ 1,300.00 vs sistema esperaba S/ 1,280.00)
        var arqueoSobrante = arqueoOriginal.ConSaldoTeorico(1280.00m);
        Assert.Equal(20.00m, arqueoSobrante.Diferencia); // Sobrante de S/ 20.00
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void c_ValidacionDeTiposDeDatosDecimal_GarantizaPrecisionMonetariaSinPuntoFlotante()
    {
        // Arrange: Monedas fraccionadas en céntimos
        var desgloseCentimos = new List<ArqueoDenominacion>
        {
            new(0.10m, 1),
            new(0.20m, 1),
            new(0.50m, 1),
            new(0.10m, 2)
        };

        var arqueo = new ArqueoCaja(
            id: Guid.NewGuid(),
            turnoCajaId: Guid.NewGuid(),
            cajeroId: Guid.NewGuid(),
            fechaAperturaUtc: DateTime.UtcNow,
            fechaCierreUtc: DateTime.UtcNow,
            fondoInicialSencillo: 0m,
            desgloseFisico: desgloseCentimos,
            totalDigitalDeclarado: 0.15m,
            saldoSistemaTeorico: 1.15m);

        // 0.10 + 0.20 + 0.50 + 0.20 = 1.00m exacto
        Assert.Equal(1.00m, arqueo.TotalEfectivoDeclarado);
        Assert.Equal(1.15m, arqueo.TotalDeclarado);
        Assert.Equal(0.00m, arqueo.Diferencia); // 1.15m - 1.15m = 0.00m sin residuo infinitesimal
        Assert.False(arqueo.TieneDiscrepancia());

        // Verificación de tipos explícitos de tipo decimal
        Assert.IsType<decimal>(arqueo.TotalEfectivoDeclarado);
        Assert.IsType<decimal>(arqueo.TotalDigitalDeclarado);
        Assert.IsType<decimal>(arqueo.Diferencia);
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void ValidacionesArqueo_LanzanExcepcionesAnteArgumentosInvalidos()
    {
        Assert.Throws<ArgumentException>(() =>
            new ArqueoCaja(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, 0m, null!));

        Assert.Throws<ArgumentException>(() =>
            new ArqueoCaja(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTime.UtcNow, DateTime.UtcNow, 0m, null!));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArqueoCaja(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, -10m, null!));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArqueoCaja(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, 0m, null!, -5m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArqueoCaja(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, 0m, null!, 0m, -5m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArqueoDenominacion(0m, 5));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArqueoDenominacion(-10m, 5));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArqueoDenominacion(10m, -1));

        // Creación con Id default
        var a = new ArqueoCaja(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, 10m, null);
        Assert.NotEqual(Guid.Empty, a.Id);
    }
}
