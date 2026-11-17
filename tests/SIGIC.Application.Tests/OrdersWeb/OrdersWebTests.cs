using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Handlers;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.Tests.OrdersWeb;

public class OrdersWebTests
{
    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "cliente_web";
        public IReadOnlyList<string> Roles { get; set; } = new List<string> { "ClienteWeb" };
        public string TerminalScope => "web_customer";
        public string ClientIp => "200.48.10.5";
        public bool HasRole(string role) => Roles.Contains(role);
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private class FakeInventoryStockPort : IInventoryStockPort
    {
        public Dictionary<(Guid AlmacenId, Guid ProductoId), StockAlmacen> Stocks { get; } = new();
        public List<Guid> LockingOrderObserved { get; } = new();

        public Task<StockAlmacen?> ObtenerStockAlmacenAsync(Guid almacenId, Guid productoId, CancellationToken cancellationToken = default)
        {
            Stocks.TryGetValue((almacenId, productoId), out var s);
            return Task.FromResult(s);
        }

        public Task<IReadOnlyList<StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(
            Guid almacenId, IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
        {
            var idList = productoIds.ToList();
            LockingOrderObserved.AddRange(idList);
            var result = idList
                .Where(id => Stocks.ContainsKey((almacenId, id)))
                .Select(id => Stocks[(almacenId, id)])
                .ToList();
            return Task.FromResult<IReadOnlyList<StockAlmacen>>(result);
        }

        public Task ActualizarStockAlmacenAsync(StockAlmacen stock, CancellationToken cancellationToken = default)
        {
            Stocks[(stock.AlmacenId, stock.ProductoId)] = stock;
            return Task.CompletedTask;
        }
    }

    private class FakeProductQueryPort : IProductQueryPort
    {
        public Dictionary<Guid, Producto> Productos { get; } = new();

        public Task<Producto?> ObtenerParaPDPAsync(Guid productoId, CancellationToken cancellationToken = default)
        {
            Productos.TryGetValue(productoId, out var p);
            return Task.FromResult(p);
        }

        public Task<IReadOnlyList<Producto>> ObtenerMultiplesParaPDPAsync(IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
        {
            var list = productoIds.Where(id => Productos.ContainsKey(id)).Select(id => Productos[id]).ToList();
            return Task.FromResult<IReadOnlyList<Producto>>(list);
        }

        public Task<decimal> ObtenerFactorConversionAsync(string origen, string destino, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1.0000m);
        }
    }

    private class FakeTariffRepository : ITariffRepository
    {
        public Task<decimal> ObtenerPorcentajeDescuentoRolAsync(string rol, CancellationToken cancellationToken = default)
            => Task.FromResult(0.00m);

        public Task<decimal> ObtenerPorcentajeDescuentoVolumenAsync(decimal cantidadBase, CancellationToken cancellationToken = default)
            => Task.FromResult(0.00m);
    }

    private class FakeCustomerProfilePort : ICustomerProfilePort
    {
        public Task<string> ObtenerPerfilClienteAsync(Guid? clienteId, CancellationToken cancellationToken = default)
            => Task.FromResult("Público General");
    }

    private class FakeWarehouseConfigPort : IWarehouseConfigurationPort
    {
        public Guid CentralId { get; set; } = Guid.NewGuid();
        public Guid ObtenerAlmacenCentralId() => CentralId;
        public bool EsAlmacenValido(Guid almacenId) => true;
    }

    private class FakeWebOrderRepository : IWebOrderRepository
    {
        public Dictionary<Guid, PedidoWebDto> Pedidos { get; } = new();

        public Task<PedidoWebDto?> ObtenerPorIdAsync(Guid pedidoId, CancellationToken cancellationToken = default)
        {
            Pedidos.TryGetValue(pedidoId, out var p);
            return Task.FromResult(p);
        }

        public Task<PedidoWebDto?> ObtenerParaBloqueoAsync(Guid pedidoId, CancellationToken cancellationToken = default)
        {
            Pedidos.TryGetValue(pedidoId, out var p);
            return Task.FromResult(p);
        }

        public Task GuardarPedidoAsync(PedidoWebDto pedido, CancellationToken cancellationToken = default)
        {
            Pedidos[pedido.Id] = pedido;
            return Task.CompletedTask;
        }

        public Task ActualizarEstadoAsync(Guid pedidoId, string nuevoEstado, CancellationToken cancellationToken = default)
        {
            if (Pedidos.TryGetValue(pedidoId, out var p))
            {
                Pedidos[pedidoId] = p with { Estado = nuevoEstado };
            }
            return Task.CompletedTask;
        }

        public Task<bool> ActualizarEstadoConcurrenteAsync(Guid pedidoId, string estadoEsperado, string nuevoEstado, CancellationToken cancellationToken = default)
        {
            if (Pedidos.TryGetValue(pedidoId, out var p))
            {
                if (p.Estado != estadoEsperado) return Task.FromResult(false);
                Pedidos[pedidoId] = p with { Estado = nuevoEstado };
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<PedidoWebDto>> ObtenerPedidosExpiradosAsync(DateTime fechaReferenciaUtc, CancellationToken cancellationToken = default)
        {
            var result = Pedidos.Values
                .Where(p => p.Estado == "PendientePago" &&
                            (p.FechaExpiracionUtc <= fechaReferenciaUtc ||
                             (p.FechaLimiteVoucherUtc.HasValue && p.FechaLimiteVoucherUtc.Value <= fechaReferenciaUtc && !p.FechaSubidaVoucherUtc.HasValue)))
                .ToList();
            return Task.FromResult<IReadOnlyList<PedidoWebDto>>(result);
        }
    }

    private class FakeAuditLogPort : IAuditLogPort
    {
        public List<AuditLogEntry> Entries { get; } = new();
        public Task RegistrarAccionAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private class FakeUnitOfWork : SIGIC.Application.Tests.TestDoubles.FakeUnitOfWork
    {
    }

    private class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    }

    private static (ReservarPedidoWebCommandHandler Handler, FakeInventoryStockPort Stock, FakeProductQueryPort ProdPort, FakeWarehouseConfigPort Warehouse, FakeWebOrderRepository Repo, FakeDateTimeProvider Clock, FakeUnitOfWork Uow)
        CrearSut()
    {
        var user = new FakeCurrentUserContext();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var warehouse = new FakeWarehouseConfigPort();
        var repo = new FakeWebOrderRepository();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new ReservarPedidoWebCommandHandler(
            user, stock, prodPort, tariff, profile, warehouse, repo, audit, clock, uow);

        return (handler, stock, prodPort, warehouse, repo, clock, uow);
    }

    [Fact]
    public async Task Debe_OrdenarItemsHavenderAscendente_AntesDeBloquearEnBaseDeDatos()
    {
        var (handler, stock, prodPort, warehouse, _, _, _) = CrearSut();

        var idC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var idA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var idB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        prodPort.Productos[idA] = new Producto(idA, "SKU-A", "Prod A", "UND", 10m, 5m, 100m, true);
        prodPort.Productos[idB] = new Producto(idB, "SKU-B", "Prod B", "UND", 20m, 10m, 100m, true);
        prodPort.Productos[idC] = new Producto(idC, "SKU-C", "Prod C", "UND", 30m, 15m, 100m, true);

        stock.Stocks[(warehouse.CentralId, idA)] = new StockAlmacen(Guid.NewGuid(), idA, warehouse.CentralId, 100m, 0m);
        stock.Stocks[(warehouse.CentralId, idB)] = new StockAlmacen(Guid.NewGuid(), idB, warehouse.CentralId, 100m, 0m);
        stock.Stocks[(warehouse.CentralId, idC)] = new StockAlmacen(Guid.NewGuid(), idC, warehouse.CentralId, 100m, 0m);

        // Envía desordenado: C, A, B
        var cmd = new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null,
            new List<PedidoWebItemDto>
            {
                new(idC, 1m, 30m),
                new(idA, 2m, 10m),
                new(idB, 1m, 20m)
            });

        var result = await handler.Handle(cmd);

        Assert.NotNull(result);
        Assert.Equal(new[] { idA, idB, idC }, stock.LockingOrderObserved);
    }

    [Fact]
    public async Task Debe_LanzarNotFoundException_CuandoProductoTieneDisponibleEnWebFalso()
    {
        var (handler, stock, prodPort, warehouse, _, _, _) = CrearSut();

        var id = Guid.NewGuid();
        prodPort.Productos[id] = new Producto(id, "SKU-FISICO", "Prod Solo POS", "UND", 50m, 30m, 100m, false);
        stock.Stocks[(warehouse.CentralId, id)] = new StockAlmacen(Guid.NewGuid(), id, warehouse.CentralId, 100m, 0m);

        var cmd = new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null,
            new List<PedidoWebItemDto> { new(id, 1m, 50m) });

        // Invariante 2: HTTP 404 absoluto
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_ExigirTiendaDestino_CuandoModalidadEsRetiroEnTienda()
    {
        var (handler, stock, prodPort, warehouse, _, _, _) = CrearSut();

        var id = Guid.NewGuid();
        prodPort.Productos[id] = new Producto(id, "SKU-WEB", "Prod Web", "UND", 50m, 30m, 10m, true);
        stock.Stocks[(warehouse.CentralId, id)] = new StockAlmacen(Guid.NewGuid(), id, warehouse.CentralId, 10m, 0m);

        // Retiro en tienda con TiendaDestinoId = null debe fallar
        var cmd = new ReservarPedidoWebCommand(
            Guid.NewGuid(), "Yape", "RetiroEnTienda", null,
            new List<PedidoWebItemDto> { new(id, 1m, 50m) });

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarStockInsuficienteException_CuandoStockDisponibleEsInsuficiente()
    {
        var (handler, stock, prodPort, warehouse, _, _, _) = CrearSut();

        var id = Guid.NewGuid();
        prodPort.Productos[id] = new Producto(id, "SKU-WEB", "Prod Web", "UND", 50m, 30m, 10m, true);
        stock.Stocks[(warehouse.CentralId, id)] = new StockAlmacen(Guid.NewGuid(), id, warehouse.CentralId, 10m, 8m); // Disponible = 2

        var cmd = new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null,
            new List<PedidoWebItemDto> { new(id, 5m, 50m) });

        await Assert.ThrowsAsync<StockInsuficienteException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_AsignarTTL20MinutosExactos_CuandoPagoEsPorPasarelaTarjeta()
    {
        var (handler, stock, prodPort, warehouse, _, clock, _) = CrearSut();

        var id = Guid.NewGuid();
        prodPort.Productos[id] = new Producto(id, "SKU-WEB", "Prod Web", "UND", 50m, 30m, 20m, true);
        stock.Stocks[(warehouse.CentralId, id)] = new StockAlmacen(Guid.NewGuid(), id, warehouse.CentralId, 20m, 0m);

        var cmd = new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null,
            new List<PedidoWebItemDto> { new(id, 2m, 50m) });

        var res = await handler.Handle(cmd);

        Assert.Equal(clock.UtcNow.AddMinutes(20), res.FechaExpiracionUtc);
        Assert.Null(res.FechaLimiteVoucherUtc);
        Assert.Equal(100m, res.Total);
        Assert.Equal("PendientePago", res.Estado);
        Assert.NotEqual(Guid.Empty, res.PedidoId);
        Assert.Equal(clock.UtcNow, res.FechaCreacionUtc);
    }

    [Fact]
    public async Task Debe_AsignarTTL240MinutosYVentanaVoucher60Minutos_CuandoPagoEsManual()
    {
        var (handler, stock, prodPort, warehouse, _, clock, _) = CrearSut();

        var id = Guid.NewGuid();
        prodPort.Productos[id] = new Producto(id, "SKU-WEB", "Prod Web", "UND", 50m, 30m, 20m, true);
        stock.Stocks[(warehouse.CentralId, id)] = new StockAlmacen(Guid.NewGuid(), id, warehouse.CentralId, 20m, 0m);

        var cmd = new ReservarPedidoWebCommand(
            Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(),
            new List<PedidoWebItemDto> { new(id, 2m, 50m) });

        var res = await handler.Handle(cmd);

        Assert.Equal(clock.UtcNow.AddMinutes(240), res.FechaExpiracionUtc);
        Assert.Equal(clock.UtcNow.AddMinutes(60), res.FechaLimiteVoucherUtc);
    }

    [Fact]
    public async Task Debe_ExpirarReservasVencidas_Y_LiberarStockComprometidoUnaSolaVez()
    {
        var stock = new FakeInventoryStockPort();
        var repo = new FakeWebOrderRepository();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider { UtcNow = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc) };
        var uow = new FakeUnitOfWork();

        var prodId = Guid.NewGuid();
        var centralId = Guid.NewGuid();
        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 20m, 5m); // 5 comprometidas
        stock.Stocks[(centralId, prodId)] = stockAlm;

        var pedidoId = Guid.NewGuid();
        var pedidoExpirado = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null, centralId,
            clock.UtcNow.AddMinutes(-30), clock.UtcNow.AddMinutes(-10), null, null,
            "PendientePago", new List<PedidoWebItemDto> { new(prodId, 5m, 50m) });

        repo.Pedidos[pedidoId] = pedidoExpirado;

        var handler = new ExpirarReservasWebCommandHandler(repo, stock, audit, clock, uow);

        var res = await handler.Handle(new ExpirarReservasWebCommand());

        Assert.Equal(1, res.CantidadPedidosExpirados);
        Assert.Equal(0m, stockAlm.StockComprometido); // Stock comprometido liberado
        Assert.Equal("Expirado", repo.Pedidos[pedidoId].Estado);

        // Segunda ejecución idempotente
        var res2 = await handler.Handle(new ExpirarReservasWebCommand());
        Assert.Equal(0, res2.CantidadPedidosExpirados);
        Assert.Equal(0m, stockAlm.StockComprometido);
    }

    [Fact]
    public async Task Validaciones_Comando_Metodo_Modalidad_Y_Margen()
    {
        var (handler, stock, prodPort, warehouse, _, _, _) = CrearSut();

        var prodId = Guid.NewGuid();
        prodPort.Productos[prodId] = new Producto(prodId, "SKU-W", "Prod", "UND", 20m, 10m, 10m, true);
        stock.Stocks[(warehouse.CentralId, prodId)] = new StockAlmacen(Guid.NewGuid(), prodId, warehouse.CentralId, 10m, 0m);

        // Sin items
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null, new List<PedidoWebItemDto>())));

        // Cantidad <= 0
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null, new List<PedidoWebItemDto> { new(prodId, 0m, 20m) })));

        // Método de pago desconocido
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReservarPedidoWebCommand(
            Guid.NewGuid(), "Cripto", "DespachoDomicilio", null, new List<PedidoWebItemDto> { new(prodId, 1m, 20m) })));

        // Modalidad desconocida
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DronAereo", null, new List<PedidoWebItemDto> { new(prodId, 1m, 20m) })));

        // Precio no coincide con tarifa vigente
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null, new List<PedidoWebItemDto> { new(prodId, 1m, 15m) })));
    }

    [Fact]
    public async Task Debe_RevertirTransaccion_AnteFalloEnPersistenciaPedidoWeb()
    {
        var (handler, stock, prodPort, warehouse, _, _, uow) = CrearSut();
        uow.FailOnCommit = true;

        var prodId = Guid.NewGuid();
        prodPort.Productos[prodId] = new Producto(prodId, "SKU-W", "Prod", "UND", 20m, 10m, 10m, true);
        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, warehouse.CentralId, 10m, 0m);
        stock.Stocks[(warehouse.CentralId, prodId)] = stockAlm;

        var cmd = new ReservarPedidoWebCommand(
            Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null,
            new List<PedidoWebItemDto> { new(prodId, 1m, 20m) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd));
    }
}
