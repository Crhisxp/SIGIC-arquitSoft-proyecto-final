using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;
using Xunit;

namespace SIGIC.Domain.Tests;

public class StockAlmacenTests
{
    [Fact]
    [Trait("Category", "SDD")]
    [Trait("Category", "Concurrency")]
    public void StockDisponible_DebeRespetarInvarianteRNFCON003_PorAlmacen()
    {
        var prodId = Guid.NewGuid();
        var almacenCentralId = Guid.NewGuid();
        var almacenTiendaId = Guid.NewGuid();

        // Almacén Central: 100 físico, 0 comprometido
        var stockCentral = new StockAlmacen(Guid.NewGuid(), prodId, almacenCentralId, 100m);
        // Almacén Tienda: 20 físico, 0 comprometido
        var stockTienda = new StockAlmacen(Guid.NewGuid(), prodId, almacenTiendaId, 20m);

        Assert.Equal(100m, stockCentral.StockDisponible);
        Assert.Equal(20m, stockTienda.StockDisponible);

        // Reserva de 30 en Central
        stockCentral.ReservarStock(30m);
        Assert.Equal(70m, stockCentral.StockDisponible);
        Assert.Equal(30m, stockCentral.StockComprometido);
        // Tienda no se ve afectada
        Assert.Equal(20m, stockTienda.StockDisponible);
        Assert.Equal(0m, stockTienda.StockComprometido);
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void RegistrarSalidaFisica_EnAlmacen_DistingueReservaYDirecto()
    {
        var prodId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 50m);

        // Reserva 15
        stock.ReservarStock(15m);
        Assert.Equal(35m, stock.StockDisponible);

        // Salida desde reserva de 10
        stock.RegistrarSalidaFisica(10m, esDesdeReserva: true);
        Assert.Equal(40m, stock.StockFisico);
        Assert.Equal(5m, stock.StockComprometido);
        Assert.Equal(35m, stock.StockDisponible);

        // Salida directa (POS) de 5
        stock.RegistrarSalidaFisica(5m, esDesdeReserva: false);
        Assert.Equal(35m, stock.StockFisico);
        Assert.Equal(5m, stock.StockComprometido);
        Assert.Equal(30m, stock.StockDisponible);
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void AplicarAjusteFisico_EnAlmacen_ImpideBajarDeStockComprometido()
    {
        var prodId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 50m);
        stock.ReservarStock(20m); // Comprometido = 20

        // Ajuste positivo
        stock.AplicarAjusteFisico(10m);
        Assert.Equal(60m, stock.StockFisico);

        // Ajuste negativo permitido hasta 20
        stock.AplicarAjusteFisico(-40m);
        Assert.Equal(20m, stock.StockFisico);

        // Ajuste que dejaría físico < comprometido
        Assert.Throws<InvalidOperationException>(() => stock.AplicarAjusteFisico(-5m));
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void Validaciones_Constructor_Y_OperacionesInvalidadas()
    {
        var prodId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new StockAlmacen(Guid.NewGuid(), Guid.Empty, almacenId));
        Assert.Throws<ArgumentException>(() => new StockAlmacen(Guid.NewGuid(), prodId, Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockAlmacen(Guid.NewGuid(), prodId, almacenId, -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 10m, -1m));
        Assert.Throws<InvalidOperationException>(() => new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 5m, 10m));

        var stock = new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 100m, 20m);
        Assert.Throws<ArgumentOutOfRangeException>(() => stock.ReservarStock(0m));
        Assert.Throws<StockInsuficienteException>(() => stock.ReservarStock(100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => stock.LiberarReserva(0m));
        Assert.Throws<InvalidOperationException>(() => stock.LiberarReserva(50m));
        Assert.Throws<ArgumentOutOfRangeException>(() => stock.RegistrarSalidaFisica(0m));
        Assert.Throws<StockInsuficienteException>(() => stock.RegistrarSalidaFisica(90m, esDesdeReserva: false));
        Assert.Throws<InvalidOperationException>(() => stock.RegistrarSalidaFisica(30m, esDesdeReserva: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => stock.RegistrarEntradaFisica(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => stock.AplicarAjusteFisico(-200m));

        stock.RegistrarEntradaFisica(50m);
        Assert.Equal(150m, stock.StockFisico);
    }
}
