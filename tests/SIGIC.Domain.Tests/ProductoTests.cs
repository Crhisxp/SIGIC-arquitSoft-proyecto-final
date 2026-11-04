using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;
using Xunit;

namespace SIGIC.Domain.Tests;

public class ProductoTests
{
    private Producto CrearProductoPrueba(
        decimal stockFisico = 100m,
        decimal precioVenta = 28.50m,
        decimal costoUnitario = 22.00m,
        bool disponibleWeb = true)
    {
        return new Producto(
            id: Guid.NewGuid(),
            codigoSku: "CEM-PORT-001",
            nombreComercial: "Cemento Portland Tipo I (42.5 kg)",
            unidadBase: "Bolsa",
            precioVenta: precioVenta,
            costoUnitario: costoUnitario,
            stockFisicoInicial: stockFisico,
            disponibleEnWeb: disponibleWeb);
    }

    [Fact]
    [Trait("Category", "SDD")]
    [Trait("Category", "Concurrency")]
    public void a_StockDisponible_DebeSerIgualA_StockFisicoMenosStockComprometido()
    {
        // Arrange (Invariante RNF-CON-003)
        var producto = CrearProductoPrueba(stockFisico: 100m);

        // Act & Assert Inicial: StockDisponible == 100
        Assert.Equal(100m, producto.StockFisico);
        Assert.Equal(0m, producto.StockComprometido);
        Assert.Equal(100m, producto.StockDisponible);

        // Act: Reserva 40 bolsas vía web
        producto.ReservarStock(40m);

        // Assert: StockDisponible == 60 (100 - 40)
        Assert.Equal(100m, producto.StockFisico);
        Assert.Equal(40m, producto.StockComprometido);
        Assert.Equal(60m, producto.StockDisponible);
    }

    [Fact]
    [Trait("Category", "SDD")]
    [Trait("Category", "Concurrency")]
    public void b_ReservarStock_Exitoso_CuandoCantidadEsMenorOIgualA_StockDisponible()
    {
        // Arrange
        var producto = CrearProductoPrueba(stockFisico: 50m);

        // Act
        producto.ReservarStock(50m);

        // Assert
        Assert.Equal(50m, producto.StockComprometido);
        Assert.Equal(0m, producto.StockDisponible);
    }

    [Fact]
    [Trait("Category", "SDD")]
    [Trait("Category", "Concurrency")]
    public void c_ReservarStock_LanzaStockInsuficienteException_CuandoCantidadSuperaStockDisponible()
    {
        // Arrange (Caso del conflicto de las 40 bolsas de Cemento Portland)
        var producto = CrearProductoPrueba(stockFisico: 40m);
        producto.ReservarStock(40m); // Reserva completa de las 40 bolsas

        // Act & Assert: Intento de reservar 1 bolsa adicional debe fallar con StockInsuficienteException
        var ex = Assert.Throws<StockInsuficienteException>(() => producto.ReservarStock(1m));

        Assert.Equal(producto.Id, ex.ProductoId);
        Assert.Equal(producto.CodigoSku, ex.CodigoSku);
        Assert.Equal(1m, ex.CantidadSolicitada);
        Assert.Equal(0m, ex.StockDisponibleActual);
        Assert.Equal(40m, producto.StockComprometido); // Stock comprometido no muta
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void d_LiberarReserva_RestauraStockComprometido()
    {
        // Arrange
        var producto = CrearProductoPrueba(stockFisico: 100m);
        producto.ReservarStock(35m);
        Assert.Equal(35m, producto.StockComprometido);
        Assert.Equal(65m, producto.StockDisponible);

        // Act
        producto.LiberarReserva(20m);

        // Assert
        Assert.Equal(15m, producto.StockComprometido);
        Assert.Equal(85m, producto.StockDisponible);

        // Liberar el resto
        producto.LiberarReserva(15m);
        Assert.Equal(0m, producto.StockComprometido);
        Assert.Equal(100m, producto.StockDisponible);
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void e_ActualizarPrecios_RechazaMargenNegativo_CuandoPrecioVentaEsMenorACostoUnitario()
    {
        // Arrange
        var producto = CrearProductoPrueba(precioVenta: 30.00m, costoUnitario: 22.00m);

        // Act & Assert: Intentar fijar precio de venta (S/ 20.00) menor al costo (S/ 22.00) debe lanzar MargenInvalidoException
        var ex = Assert.Throws<MargenInvalidoException>(() => producto.ActualizarPrecios(nuevoPrecioVenta: 20.00m, nuevoCostoUnitario: 22.00m));

        Assert.Equal(20.00m, ex.PrecioVenta);
        Assert.Equal(22.00m, ex.CostoUnitario);

        // Precios anteriores deben mantenerse intactos
        Assert.Equal(30.00m, producto.PrecioVenta);
        Assert.Equal(22.00m, producto.CostoUnitario);

        // Actualizar con margen positivo debe ser exitoso
        producto.ActualizarPrecios(35.00m, 25.00m);
        Assert.Equal(35.00m, producto.PrecioVenta);
        Assert.Equal(25.00m, producto.CostoUnitario);

        Assert.Throws<ArgumentOutOfRangeException>(() => producto.ActualizarPrecios(35.00m, -1m));
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void Constructor_RechazaParametrosInvalidos()
    {
        // Margen negativo
        Assert.Throws<MargenInvalidoException>(() =>
            new Producto(Guid.NewGuid(), "SKU", "Nombre", "Bolsa", 10m, 20m));

        // SKU vacío
        Assert.Throws<ArgumentException>(() =>
            new Producto(Guid.NewGuid(), "", "Nombre", "Bolsa", 20m, 10m));

        // Nombre vacío
        Assert.Throws<ArgumentException>(() =>
            new Producto(Guid.NewGuid(), "SKU", "   ", "Bolsa", 20m, 10m));

        // Unidad vacía
        Assert.Throws<ArgumentException>(() =>
            new Producto(Guid.NewGuid(), "SKU", "Nombre", "", 20m, 10m));

        // Costo negativo
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Producto(Guid.NewGuid(), "SKU", "Nombre", "Bolsa", 20m, -5m));

        // Stock negativo inicial
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Producto(Guid.NewGuid(), "SKU", "Nombre", "Bolsa", 20m, 10m, -1m));

        // Creación con Id default genera nuevo Guid
        var p = new Producto(Guid.Empty, "SKU", "Nombre", "Bolsa", 20m, 10m);
        Assert.NotEqual(Guid.Empty, p.Id);
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void RegistrarSalidaFisica_ValidacionesYCasosBorde()
    {
        var producto = CrearProductoPrueba(stockFisico: 80m);
        producto.ReservarStock(30m);

        // Salida mayor a stock comprometido cuando es desde reserva
        Assert.Throws<InvalidOperationException>(() => producto.RegistrarSalidaFisica(40m, esDesdeReserva: true));

        // Salida mayor a stock disponible cuando es directa
        Assert.Throws<StockInsuficienteException>(() => producto.RegistrarSalidaFisica(60m, esDesdeReserva: false));

        // Salida exitosa desde reserva
        producto.RegistrarSalidaFisica(30m, esDesdeReserva: true);
        Assert.Equal(50m, producto.StockFisico);
        Assert.Equal(0m, producto.StockComprometido);

        // Salida mayor a físico / disponible
        Assert.Throws<StockInsuficienteException>(() => producto.RegistrarSalidaFisica(60m, esDesdeReserva: false));

        // Salida <= 0
        Assert.Throws<ArgumentOutOfRangeException>(() => producto.RegistrarSalidaFisica(0m));
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void RegistrarEntradaFisica_RecalculaCostoPromedioPonderado()
    {
        // 100 bolsas a S/ 20 = S/ 2000
        var producto = CrearProductoPrueba(stockFisico: 100m, costoUnitario: 20.00m, precioVenta: 30.00m);

        // Ingresan 100 a S/ 24 = S/ 2400. Total 200 a S/ 22.00
        producto.RegistrarEntradaFisica(100m, 24.00m);
        Assert.Equal(200m, producto.StockFisico);
        Assert.Equal(22.00m, producto.CostoUnitario);

        // Producto con stock 0 que recibe entrada
        var prodCero = new Producto(Guid.NewGuid(), "SKU-0", "Cero", "Und", 10m, 5m, 0m);
        prodCero.RegistrarEntradaFisica(50m, 6.00m);
        Assert.Equal(50m, prodCero.StockFisico);
        Assert.Equal(6.00m, prodCero.CostoUnitario);
    }

    [Fact]
    [Trait("Category", "SDD")]
    public void ValidacionesGenerales_Y_Exceptions()
    {
        var producto = CrearProductoPrueba();

        Assert.Throws<ArgumentOutOfRangeException>(() => producto.ReservarStock(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => producto.ReservarStock(-5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => producto.LiberarReserva(0m));
        Assert.Throws<InvalidOperationException>(() => producto.LiberarReserva(500m));
        Assert.Throws<ArgumentOutOfRangeException>(() => producto.RegistrarEntradaFisica(-10m, 20m));
        Assert.Throws<ArgumentOutOfRangeException>(() => producto.RegistrarEntradaFisica(10m, -5m));

        Assert.True(producto.EsVisibleParaWeb());
        Assert.True(producto.ValidarMargenSeguridad(25m));
        Assert.False(producto.ValidarMargenSeguridad(15m));

        // Exception message coverage
        var exStock = new StockInsuficienteException("Mensaje directo");
        Assert.Equal("Mensaje directo", exStock.Message);
    }
}
