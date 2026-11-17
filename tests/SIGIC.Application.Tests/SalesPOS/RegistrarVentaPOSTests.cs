using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.SalesPOS.Commands;
using SIGIC.Application.SalesPOS.Handlers;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Tests.TestDoubles;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.Tests.SalesPOS;

public class RegistrarVentaPOSTests
{
    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "cajero1";
        public IReadOnlyList<string> Roles { get; set; } = new List<string> { "Cajero" };
        public string TerminalScope => "pos_terminal";
        public string ClientIp => "192.168.1.50";
        public bool HasRole(string role) => Roles.Contains(role);
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private class FakeAuthService : IAuthorizationService
    {
        private readonly ICurrentUserContext _user;
        public FakeAuthService(ICurrentUserContext user) => _user = user;

        public void ExigirRol(string rolRequerido)
        {
            if (!_user.HasRole(rolRequerido))
                throw new UnauthorizedException(rolRequerido, $"Acceso denegado. Se requiere el rol '{rolRequerido}'.");
        }

        public void ExigirUnoDeRoles(params string[] rolesRequeridos)
        {
            if (!rolesRequeridos.Any(r => _user.HasRole(r)))
                throw new UnauthorizedException(string.Join(",", rolesRequeridos), "Acceso denegado.");
        }
    }

    private class FakeCashShiftPort : ICashShiftPort
    {
        public bool TurnoValido { get; set; } = true;
        public Guid AlmacenTiendaId { get; set; } = Guid.NewGuid();

        public Task<bool> EsTurnoValidoYAperturadoAsync(Guid turnoCajaId, Guid cajeroId, CancellationToken cancellationToken = default)
            => Task.FromResult(TurnoValido);

        public Task<bool> ValidarYBloquearTurnoParaVentaAsync(Guid turnoCajaId, Guid cajeroId, CancellationToken cancellationToken = default)
            => Task.FromResult(TurnoValido);

        public Task<Guid> ObtenerAlmacenTiendaIdAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
            => Task.FromResult(AlmacenTiendaId);
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

    private class FakeDigitalPaymentVerificationPort : IDigitalPaymentVerificationPort
    {
        public HashSet<(string Metodo, string Codigo, DateOnly Fecha)> Codigos { get; } = new();

        public Task<bool> ExisteCodigoOperacionAsync(string metodoPago, string codigoOperacion, DateOnly fecha, CancellationToken cancellationToken = default)
            => Task.FromResult(Codigos.Contains((metodoPago.ToUpper(), codigoOperacion.ToUpper(), fecha)));

        public Task RegistrarCodigoOperacionAsync(string metodoPago, string codigoOperacion, DateOnly fecha, Guid ventaId, CancellationToken cancellationToken = default)
        {
            Codigos.Add((metodoPago.ToUpper(), codigoOperacion.ToUpper(), fecha));
            return Task.CompletedTask;
        }
    }

    private class FakeSalesPOSRepository : ISalesPOSRepository
    {
        public Task<VentaPOSResultDto> RegistrarVentaAsync(RegistrarVentaPOSCommand command, decimal totalVenta, DateTime fechaUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VentaPOSResultDto(Guid.NewGuid(), "B001-00001234", totalVenta, fechaUtc));
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

    private class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    }

    private class FakeUnitOfWork : SIGIC.Application.Tests.TestDoubles.FakeUnitOfWork
    {
        public bool Commited => Commits > 0;
    }

    private static (RegistrarVentaPOSCommandHandler Handler, FakeInventoryStockPort Stock, FakeProductQueryPort ProdPort, FakeCashShiftPort Shift, FakeUnitOfWork Uow, FakeAuditLogPort Audit, FakeDateTimeProvider Clock)
        CrearSut(FakeCurrentUserContext? user = null, FakeCashShiftPort? shift = null, FakeUnitOfWork? uow = null)
    {
        user ??= new FakeCurrentUserContext();
        shift ??= new FakeCashShiftPort();
        uow ??= new FakeUnitOfWork();
        var auth = new FakeAuthService(user);
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var digital = new FakeDigitalPaymentVerificationPort();
        var repo = new FakeSalesPOSRepository();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();

        var handler = new RegistrarVentaPOSCommandHandler(
            auth, user, shift, stock, prodPort, tariff, profile, digital, repo, audit, uow, clock, new FakeKardexPort(uow));

        return (handler, stock, prodPort, shift, uow, audit, clock);
    }

    [Fact]
    public async Task Debe_ExigirRolCajero_ParaRegistrarVenta()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Almacenero" } };
        var (handler, _, _, _, _, _, _) = CrearSut(user);

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto> { new(Guid.NewGuid(), 1, "UND", 10m) },
            new List<VentaPOSPagoDto> { new("Efectivo", 10m) },
            "Boleta");

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarExcepcion_SiTurnoCajaNoEstaAperturado()
    {
        var shift = new FakeCashShiftPort { TurnoValido = false };
        var (handler, _, _, _, _, _, _) = CrearSut(shift: shift);

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto> { new(Guid.NewGuid(), 1, "UND", 10m) },
            new List<VentaPOSPagoDto> { new("Efectivo", 10m) },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_OrdenarItemsPorProductoIdAscendente_AntesDeBloquearEnBaseDeDatos()
    {
        var (handler, stock, prodPort, shift, _, _, _) = CrearSut();

        var id3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var p1 = new Producto(id1, "SKU-1", "Prod 1", "UND", 20m, 15m, 100m, false);
        var p2 = new Producto(id2, "SKU-2", "Prod 2", "UND", 30m, 20m, 100m, false);
        var p3 = new Producto(id3, "SKU-3", "Prod 3", "UND", 40m, 25m, 100m, false);

        prodPort.Productos[id1] = p1;
        prodPort.Productos[id2] = p2;
        prodPort.Productos[id3] = p3;

        stock.Stocks[(shift.AlmacenTiendaId, id1)] = new StockAlmacen(Guid.NewGuid(), id1, shift.AlmacenTiendaId, 100m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, id2)] = new StockAlmacen(Guid.NewGuid(), id2, shift.AlmacenTiendaId, 100m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, id3)] = new StockAlmacen(Guid.NewGuid(), id3, shift.AlmacenTiendaId, 100m, 0m);

        // Envía desordenado: id3, id1, id2
        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto>
            {
                new(id3, 1, "UND", 40m),
                new(id1, 1, "UND", 20m),
                new(id2, 1, "UND", 30m)
            },
            new List<VentaPOSPagoDto> { new("Efectivo", 90m) },
            "Boleta");

        var result = await handler.Handle(cmd);

        Assert.NotNull(result);
        Assert.Equal(new[] { id1, id2, id3 }, stock.LockingOrderObserved);
    }

    [Fact]
    public async Task Debe_LanzarStockInsuficienteException_CuandoStockDisponibleEsMenorASolicitado()
    {
        var (handler, stock, prodPort, shift, _, _, _) = CrearSut();

        var id = Guid.NewGuid();
        var prod = new Producto(id, "SKU-TEST", "Prod Test", "UND", 50m, 35m, 10m, false);
        prodPort.Productos[id] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), id, shift.AlmacenTiendaId, 10m, 8m); // StockDisponible = 2
        stock.Stocks[(shift.AlmacenTiendaId, id)] = stockAlm;

        // Solicita 5 unidades (solo hay 2 disponibles)
        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto> { new(id, 5, "UND", 50m) },
            new List<VentaPOSPagoDto> { new("Efectivo", 250m) },
            "Boleta");

        await Assert.ThrowsAsync<StockInsuficienteException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoSumaPagosNoCoincideConTotalVenta()
    {
        var (handler, stock, prodPort, shift, _, _, _) = CrearSut();

        var id = Guid.NewGuid();
        var prod = new Producto(id, "SKU-TEST", "Prod Test", "UND", 50m, 35m, 10m, false);
        prodPort.Productos[id] = prod;
        stock.Stocks[(shift.AlmacenTiendaId, id)] = new StockAlmacen(Guid.NewGuid(), id, shift.AlmacenTiendaId, 10m, 0m);

        // Total = 100m, pero pago total = 90m
        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto> { new(id, 2, "UND", 50m) },
            new List<VentaPOSPagoDto> { new("Efectivo", 90m) },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoPagosDigitalesTienenCodigoDuplicadoOVacio()
    {
        var (handler, stock, prodPort, shift, _, _, _) = CrearSut();

        var id = Guid.NewGuid();
        var prod = new Producto(id, "SKU-TEST", "Prod Test", "UND", 50m, 35m, 10m, false);
        prodPort.Productos[id] = prod;
        stock.Stocks[(shift.AlmacenTiendaId, id)] = new StockAlmacen(Guid.NewGuid(), id, shift.AlmacenTiendaId, 10m, 0m);

        // Dos pagos digitales con el mismo código de operación en la misma venta
        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto> { new(id, 2, "UND", 50m) },
            new List<VentaPOSPagoDto>
            {
                new("Yape", 50m, "OP-123456"),
                new("Plin", 50m, "OP-123456")
            },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_RegistrarVentaExitosamente_Y_GenerarAuditoriaAppendOnly()
    {
        var (handler, stock, prodPort, shift, uow, audit, clock) = CrearSut();

        var id = Guid.NewGuid();
        var prod = new Producto(id, "SKU-TEST", "Prod Test", "UND", 50m, 35m, 10m, false);
        prodPort.Productos[id] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), id, shift.AlmacenTiendaId, 10m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, id)] = stockAlm;

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto> { new(id, 2, "UND", 50m) },
            new List<VentaPOSPagoDto>
            {
                new("Efectivo", 50m),
                new("Yape", 50m, "OP-YAPE-999")
            },
            "Boleta");

        var result = await handler.Handle(cmd);

        Assert.NotNull(result);
        Assert.Equal(100m, result.TotalVenta);
        Assert.Equal(8m, stockAlm.StockFisico); // Descontado de almacén tienda
        Assert.True(uow.Commited);
        Assert.Single(audit.Entries);
        Assert.Equal("REGISTRO_VENTA_POS", audit.Entries[0].Accion);
        Assert.Equal("B001-00001234", result.NumeroComprobante);
        Assert.Equal(clock.UtcNow, result.FechaEmisionUtc);
    }

    [Fact]
    public async Task Validaciones_Items_Pagos_Margen_Y_ProductoNoEncontrado()
    {
        var (handler, stock, prodPort, shift, _, _, _) = CrearSut();

        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-P", "Prod", "UND", 50m, 40m, 10m, false);
        prodPort.Productos[prodId] = prod;
        stock.Stocks[(shift.AlmacenTiendaId, prodId)] = new StockAlmacen(Guid.NewGuid(), prodId, shift.AlmacenTiendaId, 10m, 0m);

        // Sin items
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<VentaPOSItemDto>(), new List<VentaPOSPagoDto> { new("Efectivo", 10m) }, "Boleta")));

        // Sin pagos
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<VentaPOSItemDto> { new(prodId, 1, "UND", 50m) }, new List<VentaPOSPagoDto>(), "Boleta")));

        // Cantidad <= 0
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<VentaPOSItemDto> { new(prodId, 0, "UND", 50m) }, new List<VentaPOSPagoDto> { new("Efectivo", 50m) }, "Boleta")));

        // Producto no encontrado
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<VentaPOSItemDto> { new(Guid.NewGuid(), 1, "UND", 50m) }, new List<VentaPOSPagoDto> { new("Efectivo", 50m) }, "Boleta")));

        // Margen inválido (precio unitario menor a costo 40m)
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<VentaPOSItemDto> { new(prodId, 1, "UND", 30m) }, new List<VentaPOSPagoDto> { new("Efectivo", 30m) }, "Boleta")));

        // Pago digital con código vacío
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<VentaPOSItemDto> { new(prodId, 1, "UND", 50m) }, new List<VentaPOSPagoDto> { new("Yape", 50m, "  ") }, "Boleta")));
    }

    [Fact]
    public async Task Debe_RevertirTransaccion_AnteFalloEnPersistenciaVenta()
    {
        var uow = new FakeUnitOfWork { FailOnCommit = true };
        var (handler, stock, prodPort, shift, _, _, _) = CrearSut(uow: uow);

        var id = Guid.NewGuid();
        var prod = new Producto(id, "SKU-FAIL", "Prod Falla", "UND", 50m, 35m, 10m, false);
        prodPort.Productos[id] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), id, shift.AlmacenTiendaId, 10m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, id)] = stockAlm;

        uow.RegisterRollbackAction(() => stockAlm.RegistrarEntradaFisica(2m));

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new List<VentaPOSItemDto> { new(id, 2, "UND", 50m) },
            new List<VentaPOSPagoDto> { new("Efectivo", 100m) },
            "Boleta");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd));
        Assert.Equal(10m, stockAlm.StockFisico);
    }
}
