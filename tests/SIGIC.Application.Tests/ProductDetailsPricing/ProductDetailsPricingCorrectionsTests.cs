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

public class ProductDetailsPricingCorrectionsTests
{
    private class MockProductQueryPort : IProductQueryPort
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

    private class MockTariffRepository : ITariffRepository
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

    private class MockCustomerProfilePort : ICustomerProfilePort
    {
        public Dictionary<Guid, string> Perfiles { get; } = new();

        public Task<string> ObtenerPerfilClienteAsync(Guid? clienteId, CancellationToken cancellationToken = default)
        {
            if (clienteId.HasValue && Perfiles.TryGetValue(clienteId.Value, out var rol))
                return Task.FromResult(rol);

            return Task.FromResult("Público General");
        }
    }

    private class MockInventoryStockPort : IInventoryStockPort
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

    private class MockCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "testuser";
        public IReadOnlyList<string> Roles { get; set; } = new[] { "Cliente" };
        public string TerminalScope { get; set; } = "pos";
        public string ClientIp { get; set; } = "127.0.0.1";
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public bool IsInRole(string role) => HasRole(role);
    }

    private class MockWarehouseConfigPort : IWarehouseConfigurationPort
    {
        public Guid CentralId { get; set; } = Guid.NewGuid();
        public Guid ObtenerAlmacenCentralId() => CentralId;
        public bool EsAlmacenValido(Guid almacenId) => true;
    }

    [Fact]
    public async Task Debe_CobrarSubtotalEnBaseAUnidadComercial_CuandoFactorEsMayorAUno()
    {
        var productPort = new MockProductQueryPort();
        var tariffRepo = new MockTariffRepository();
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var producto = new Producto(
            Guid.NewGuid(), "SKU-CLAVO-3P", "Clavos 3 pulgadas", "UNIDAD",
            precioVenta: 10.00m, costoUnitario: 6.00m, stockFisicoInicial: 1000m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);
        productPort.SetFactor("CAJA_12", "UNIDAD", 12.0000m);

        // Cliente compra 2 CAJAS (24 UNIDADES en base).
        // 24 unidades >= 10 -> 2% desc volumen.
        // Precio por caja antes desc = 10.00 * 12 = 120.00 S/.
        // Desc por caja = 120.00 * 0.02 = 2.40 S/.
        // Precio por caja final = 117.60 S/.
        // Subtotal = 2 * 117.60 = 235.20 S/.
        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Publico", 2m, "CAJA_12", EsCanalWeb: false);
        var result = await handler.Handle(query);

        Assert.Equal(235.20m, result.Subtotal);
        Assert.Equal(117.60m, result.PrecioFinalUnitario);
        Assert.Equal(24.0000m, result.CantidadBaseConvertida);
        Assert.Equal(12.0000m, result.FactorConversion);
        Assert.Equal(120.00m, result.PrecioUnitarioComercial);
    }

    [Fact]
    public async Task Debe_ResolverPerfilClienteDesdePuertoSeguro_IgnorandoRolManipulado()
    {
        var productPort = new MockProductQueryPort();
        var tariffRepo = new MockTariffRepository();
        var profilePort = new MockCustomerProfilePort();
        var clienteId = Guid.NewGuid();
        var userContext = new MockCurrentUserContext { UserId = clienteId };
        profilePort.Perfiles[clienteId] = "Publico";

        var producto = new Producto(
            Guid.NewGuid(), "SKU-TUB-01", "Tubo PVC SAP", "UNIDAD",
            precioVenta: 20.00m, costoUnitario: 12.00m, stockFisicoInicial: 200m,
            disponibleEnWeb: true);
        productPort.AddProducto(producto);

        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo, profilePort, currentUserContext: userContext);

        // Parámetro manipulado en frontend dice "Contratista" (10% desc)
        var query = new CalcularPDPYDescuentoQuery(
            producto.Id, RolCliente: "Contratista", Cantidad: 1m, UnidadComercial: "UNIDAD",
            EsCanalWeb: false, ClienteId: clienteId);

        var result = await handler.Handle(query);

        // Se debió usar el perfil de base de datos (0% desc), no el parámetro manipulado
        Assert.Equal(0.00m, result.PorcentajeDescuentoTotal);
        Assert.Equal(20.00m, result.PrecioFinalUnitario);
        Assert.Equal(20.00m, result.Subtotal);
    }

    [Fact]
    public async Task Debe_GarantizarCeroErrorAcumulado_EnMovimientosReales_RF_INV_005()
    {
        var productPort = new MockProductQueryPort();
        var tariffRepo = new MockTariffRepository();
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var productoId = Guid.NewGuid();
        var producto = new Producto(
            productoId, "SKU-FRAC", "Varilla Corrugada", "METRO",
            precioVenta: 10.00m, costoUnitario: 5.00m, stockFisicoInicial: 1000m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);
        decimal factor = 3.3333m;
        productPort.SetFactor("TRAMO_FRAC", "METRO", factor);

        decimal cantidadUnitaria = 0.3000m;
        decimal sumaBaseAcumulada = 0m;
        const int iteraciones = 500;

        for (int i = 0; i < iteraciones; i++)
        {
            var query = new CalcularPDPYDescuentoQuery(
                ProductoId: productoId,
                RolCliente: "Público General",
                Cantidad: cantidadUnitaria,
                UnidadComercial: "TRAMO_FRAC");

            var result = await handler.Handle(query);
            sumaBaseAcumulada += result.CantidadBaseConvertida;
        }

        // 0.3000 * 3.3333 = 0.99999 -> redondeo bancario a 4 decimales = 1.0000m
        decimal esperada = iteraciones * 1.0000m;
        decimal delta = Math.Abs(sumaBaseAcumulada - esperada);

        Assert.Equal(0.0000m, delta);
    }

    [Fact]
    public async Task Debe_CumplirTiempoBenchmark_EnCalcularPDPYDescuento()
    {
        var productPort = new MockProductQueryPort();
        var tariffRepo = new MockTariffRepository();
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var producto = new Producto(
            Guid.NewGuid(), "SKU-BENCH", "Cobre 1/2", "METRO",
            precioVenta: 15.00m, costoUnitario: 10.00m, stockFisicoInicial: 5000m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);
        productPort.SetFactor("ROLLO_50M", "METRO", 50.0000m);

        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Maestro", 2m, "ROLLO_50M", EsCanalWeb: true);

        // Warmup
        await handler.Handle(query);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var res = await handler.Handle(query);
            Assert.NotNull(res);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2500, $"El benchmark tardó {sw.ElapsedMilliseconds} ms, debiendo ser < 2500 ms.");
    }

    [Fact]
    public async Task Debe_ConsultarStockDeAlmacenCentral_CuandoConsultaEsCanalWeb()
    {
        var productPort = new MockProductQueryPort();
        var tariffRepo = new MockTariffRepository();
        var stockPort = new MockInventoryStockPort();
        var warehouseConfig = new MockWarehouseConfigPort();

        var handler = new CalcularPDPYDescuentoQueryHandler(
            productPort, tariffRepo, customerProfilePort: null, stockPort, warehouseConfig);

        var producto = new Producto(
            Guid.NewGuid(), "SKU-TALADRO", "Taladro Percutor 750W", "UNIDAD",
            precioVenta: 250.00m, costoUnitario: 180.00m, stockFisicoInicial: 100m,
            disponibleEnWeb: true);

        productPort.AddProducto(producto);

        // Almacén Central tiene 15 físicas y 5 comprometidas -> Disponible = 10
        var stockCentral = new StockAlmacen(
            Guid.NewGuid(), producto.Id, warehouseConfig.CentralId,
            stockFisicoInicial: 15m, stockComprometidoInicial: 5m);
        stockPort.Stocks[(warehouseConfig.CentralId, producto.Id)] = stockCentral;

        var query = new CalcularPDPYDescuentoQuery(producto.Id, "Publico", 1m, "UNIDAD", EsCanalWeb: true);
        var result = await handler.Handle(query);

        Assert.Equal(10m, result.StockDisponible);
    }
}
