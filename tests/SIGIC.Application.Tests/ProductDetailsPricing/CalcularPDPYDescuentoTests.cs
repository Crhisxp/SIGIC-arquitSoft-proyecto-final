using System.Diagnostics;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.ProductDetailsPricing.Services;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.Tests.ProductDetailsPricing;

public class CalcularPDPYDescuentoTests
{
    private class FakeProductQueryPort : IProductQueryPort
    {
        private readonly Dictionary<Guid, Producto> _productos = new();
        private readonly Dictionary<(string, string), decimal> _factores = new();

        public void AddProducto(Producto p) => _productos[p.Id] = p;
        public void SetFactor(string origen, string destino, decimal factor) => _factores[(origen.ToUpper(), destino.ToUpper())] = factor;

        public Task<Producto?> ObtenerParaPDPAsync(Guid productoId, CancellationToken cancellationToken = default)
        {
            _productos.TryGetValue(productoId, out var p);
            return Task.FromResult(p);
        }

        public Task<IReadOnlyList<Producto>> ObtenerMultiplesParaPDPAsync(IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
        {
            var list = productoIds.Where(id => _productos.ContainsKey(id)).Select(id => _productos[id]).ToList();
            return Task.FromResult<IReadOnlyList<Producto>>(list);
        }

        public Task<decimal> ObtenerFactorConversionAsync(string origen, string destino, CancellationToken cancellationToken = default)
        {
            if (string.Equals(origen, destino, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(1.0000m);

            if (_factores.TryGetValue((origen.ToUpper(), destino.ToUpper()), out var f))
                return Task.FromResult(f);

            return Task.FromResult(1.0000m);
        }
    }

    private class FakeTariffRepository : ITariffRepository
    {
        public Task<decimal> ObtenerPorcentajeDescuentoRolAsync(string rol, CancellationToken cancellationToken = default)
        {
            decimal desc = rol.ToLowerInvariant() switch
            {
                "maestro" => 0.05m,
                "contratista" => 0.10m,
                _ => 0.00m
            };
            return Task.FromResult(desc);
        }

        public Task<decimal> ObtenerPorcentajeDescuentoVolumenAsync(decimal cantidadBase, CancellationToken cancellationToken = default)
        {
            if (cantidadBase >= 100m) return Task.FromResult(0.08m);
            if (cantidadBase >= 50m) return Task.FromResult(0.05m);
            if (cantidadBase >= 10m) return Task.FromResult(0.02m);
            return Task.FromResult(0.00m);
        }
    }

    private class FakeCustomerProfilePort : ICustomerProfilePort
    {
        public Dictionary<Guid, string> Perfiles { get; } = new();

        public Task<string> ObtenerPerfilClienteAsync(Guid? clienteId, CancellationToken cancellationToken = default)
        {
            if (clienteId.HasValue && Perfiles.TryGetValue(clienteId.Value, out var rol))
                return Task.FromResult(rol);

            return Task.FromResult("Público General");
        }
    }

    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "testuser";
        public IReadOnlyList<string> Roles { get; set; } = new[] { "Cliente" };
        public string TerminalScope { get; set; } = "pos";
        public string ClientIp { get; set; } = "127.0.0.1";
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public bool IsInRole(string role) => HasRole(role);
    }

    private class FakeInventoryStockPort : IInventoryStockPort
    {
        public Dictionary<(Guid AlmacenId, Guid ProductoId), StockAlmacen> Stocks { get; } = new();

        public Task<StockAlmacen?> ObtenerStockAlmacenAsync(Guid almacenId, Guid productoId, CancellationToken cancellationToken = default)
        {
            Stocks.TryGetValue((almacenId, productoId), out var stock);
            return Task.FromResult(stock);
        }

        public Task<IReadOnlyList<StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(
            Guid almacenId, IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
        {
            var list = productoIds
                .OrderBy(id => id)
                .Where(id => Stocks.ContainsKey((almacenId, id)))
                .Select(id => Stocks[(almacenId, id)])
                .ToList();
            return Task.FromResult<IReadOnlyList<StockAlmacen>>(list);
        }

        public Task ActualizarStockAlmacenAsync(StockAlmacen stock, CancellationToken cancellationToken = default)
        {
            Stocks[(stock.AlmacenId, stock.ProductoId)] = stock;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Debe_RetornarNotFound_Cuando_ProductoEsExclusivoFisico_Y_ConsultaEsCanalWeb()
    {
        var productPort = new FakeProductQueryPort();
        var tariffRepo = new FakeTariffRepository();
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var productoFisico = new Producto(
            Guid.NewGuid(), "SKU-CEMENTO-ESP", "Cemento Especial", "BOLSA",
            precioVenta: 35.00m, costoUnitario: 28.00m, stockFisicoInicial: 100m,
            disponibleEnWeb: false);

        productPort.AddProducto(productoFisico);

        var query = new CalcularPDPYDescuentoQuery(productoFisico.Id, "Publico", 1, "BOLSA", EsCanalWeb: true);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query));
    }

    [Fact]
    public async Task Debe_ConvertirUnidadComercialABase_ConRedondeoBancario_4Decimales()
    {
        var productPort = new FakeProductQueryPort();
        var tariffRepo = new FakeTariffRepository();
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var producto = new Producto(
            Guid.NewGuid(), "SKU-ALAMBRE", "Alambre Galvanizado", "KG",
            precioVenta: 12.00m, costoUnitario: 8.00m, stockFisicoInicial: 500m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);
        productPort.SetFactor("ROLLO_100M", "KG", 12.34567m);

        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Publico", 2.5m, "ROLLO_100M", EsCanalWeb: false);
        var result = await handler.Handle(query);

        // 2.5 * 12.34567 = 30.864175 -> redondeo bancario a 4 dec = 30.8642
        Assert.Equal(30.8642m, result.CantidadBaseConvertida);
    }

    [Fact]
    public async Task Debe_EjecutarBenchmark_10000Conversiones_SinDegradar()
    {
        var productPort = new FakeProductQueryPort();
        var tariffRepo = new FakeTariffRepository();
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var producto = new Producto(
            Guid.NewGuid(), "SKU-VARILLA", "Varilla Fierro 1/2", "VARILLA",
            precioVenta: 45.00m, costoUnitario: 32.00m, stockFisicoInicial: 2000m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);
        productPort.SetFactor("ATADO", "VARILLA", 10.0000m);

        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Maestro", 5, "ATADO", EsCanalWeb: true);

        // Warmup
        await handler.Handle(query);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var res = await handler.Handle(query);
            Assert.NotNull(res);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000, $"El benchmark de 10,000 conversiones tardó {sw.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task Debe_CalcularDescuentoPorRol_Y_Volumen_Correctamente()
    {
        var productPort = new FakeProductQueryPort();
        var tariffRepo = new FakeTariffRepository();
        var profilePort = new FakeCustomerProfilePort();
        var clienteId = Guid.NewGuid();
        profilePort.Perfiles[clienteId] = "Contratista";
        var userContext = new FakeCurrentUserContext { UserId = clienteId };

        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo, profilePort, currentUserContext: userContext);

        var producto = new Producto(
            Guid.NewGuid(), "SKU-PINTURA", "Esmalte Sintético", "GALON",
            precioVenta: 50.00m, costoUnitario: 30.00m, stockFisicoInicial: 200m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);

        // Contratista = 10% descuento, Cantidad 60 >= 50 = 5% volumen -> Total 15% desc
        // Precio base = 50.00 -> 15% desc = 7.50 desc -> Precio final = 42.50
        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Contratista", 60, "GALON", EsCanalWeb: false, ClienteId: clienteId);
        var result = await handler.Handle(query);

        Assert.Equal(0.15m, result.PorcentajeDescuentoTotal);
        Assert.Equal(42.50m, result.PrecioFinalUnitario);
        Assert.Equal(2550.00m, result.Subtotal);
        Assert.True(result.AplicaDescuentoVolumen);
    }

    [Fact]
    public async Task Debe_LanzarMargenInvalidoException_Cuando_PrecioConDescuento_EsMenorAlCosto()
    {
        var productPort = new FakeProductQueryPort();
        var tariffRepo = new FakeTariffRepository();
        var profilePort = new FakeCustomerProfilePort();
        var clienteId = Guid.NewGuid();
        profilePort.Perfiles[clienteId] = "Contratista";
        var userContext = new FakeCurrentUserContext { UserId = clienteId };

        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo, profilePort, currentUserContext: userContext);

        // Producto con margen muy estrecho: Venta = 10.00, Costo = 9.50
        var producto = new Producto(
            Guid.NewGuid(), "SKU-TUB-PVC", "Tubo PVC 1/2", "UNIDAD",
            precioVenta: 10.00m, costoUnitario: 9.50m, stockFisicoInicial: 50m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);

        // Contratista (10%) + Volumen >= 10 (2%) = 12% desc -> Precio final = 10.00 - 1.20 = 8.80 < Costo (9.50)
        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Contratista", 15, "UNIDAD", EsCanalWeb: false, ClienteId: clienteId);

        await Assert.ThrowsAsync<MargenInvalidoException>(() => handler.Handle(query));
    }

    [Fact]
    public async Task Debe_CalcularStockDisponible_UsandoFormulaInvariante()
    {
        var productPort = new FakeProductQueryPort();
        var tariffRepo = new FakeTariffRepository();
        var stockPort = new FakeInventoryStockPort();
        var almacenId = Guid.NewGuid();

        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo, inventoryStockPort: stockPort);

        var producto = new Producto(
            Guid.NewGuid(), "SKU-LADRILLO", "Ladrillo King Kong", "MILLAR",
            precioVenta: 800.00m, costoUnitario: 600.00m, stockFisicoInicial: 10m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);

        // Invariante: StockDisponible = 10 - 3 = 7
        var stockAlm = new StockAlmacen(Guid.NewGuid(), producto.Id, almacenId, stockFisicoInicial: 10m, stockComprometidoInicial: 3m);
        stockPort.Stocks[(almacenId, producto.Id)] = stockAlm;

        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Publico", 1, "MILLAR", EsCanalWeb: false, AlmacenId: almacenId);
        var result = await handler.Handle(query);

        // Invariante: StockDisponible = 10 - 3 = 7
        Assert.Equal(7m, result.StockDisponible);
        Assert.Equal(producto.Id, result.ProductoId);
        Assert.Equal(producto.CodigoSku, result.CodigoSku);
        Assert.Equal(producto.NombreComercial, result.NombreComercial);
        Assert.Equal(producto.UnidadBase, result.UnidadBase);
        Assert.Equal(800.00m, result.PrecioBaseUnitario);
        Assert.Equal(0m, result.DescuentoUnitario);
    }

    [Fact]
    public async Task Debe_LanzarNotFoundException_CuandoProductoNoExiste()
    {
        var productPort = new FakeProductQueryPort();
        var tariffRepo = new FakeTariffRepository();
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var query = new CalcularPDPYDescuentoQuery(Guid.NewGuid(), "Publico", 1, "UND");
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query));

        Assert.Throws<ArgumentNullException>(() => new CalcularPDPYDescuentoQueryHandler(null!, tariffRepo));
        Assert.Throws<ArgumentNullException>(() => new CalcularPDPYDescuentoQueryHandler(productPort, null!));
    }
}
