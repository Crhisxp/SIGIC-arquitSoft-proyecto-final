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

public class SalesPOSCorrectionsTests
{
    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username => "cajero1";
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
                throw new UnauthorizedException(rolRequerido, "Acceso denegado.");
        }
        public void ExigirUnoDeRoles(params string[] rolesRequeridos) { }
    }

    private class FakeCashShiftPort : ICashShiftPort
    {
        public Guid AlmacenTiendaId { get; set; } = Guid.NewGuid();
        public bool TurnoValido { get; set; } = true;

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

        public Task<StockAlmacen?> ObtenerStockAlmacenAsync(Guid almacenId, Guid productoId, CancellationToken cancellationToken = default)
        {
            Stocks.TryGetValue((almacenId, productoId), out var s);
            return Task.FromResult(s);
        }

        public Task<IReadOnlyList<StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(
            Guid almacenId, IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
        {
            var result = productoIds.OrderBy(id => id)
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
        public Dictionary<(string, string), decimal> Factores { get; } = new();

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
            if (string.Equals(origen, destino, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(1.0000m);
            if (Factores.TryGetValue((origen.ToUpper(), destino.ToUpper()), out var f)) return Task.FromResult(f);
            return Task.FromResult(1.0000m);
        }
    }

    private class FakeTariffRepository : ITariffRepository
    {
        public Task<decimal> ObtenerPorcentajeDescuentoRolAsync(string rol, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(rol.ToLowerInvariant() == "maestro" ? 0.05m : 0.00m);
        }

        public Task<decimal> ObtenerPorcentajeDescuentoVolumenAsync(decimal cantidadBase, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(cantidadBase >= 10m ? 0.02m : 0.00m);
        }
    }

    private class FakeCustomerProfilePort : ICustomerProfilePort
    {
        public Dictionary<Guid, string> Roles { get; } = new();
        public Task<string> ObtenerPerfilClienteAsync(Guid? clienteId, CancellationToken cancellationToken = default)
        {
            if (clienteId.HasValue && Roles.TryGetValue(clienteId.Value, out var rol))
                return Task.FromResult(rol);
            return Task.FromResult("Público General");
        }
    }

    private class FakeDigitalPaymentVerificationPort : IDigitalPaymentVerificationPort
    {
        public HashSet<(string Metodo, string Codigo, DateOnly Fecha)> Codigos { get; } = new();

        public Task<bool> ExisteCodigoOperacionAsync(string metodoPago, string codigoOperacion, DateOnly fecha, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Codigos.Contains((metodoPago.ToUpper(), codigoOperacion.ToUpper(), fecha)));
        }

        public Task RegistrarCodigoOperacionAsync(string metodoPago, string codigoOperacion, DateOnly fecha, Guid ventaId, CancellationToken cancellationToken = default)
        {
            Codigos.Add((metodoPago.ToUpper(), codigoOperacion.ToUpper(), fecha));
            return Task.CompletedTask;
        }
    }

    private class FakeSalesPOSRepository : ISalesPOSRepository
    {
        public List<RegistrarVentaPOSCommand> VentasRegistradas { get; } = new();

        public Task<VentaPOSResultDto> RegistrarVentaAsync(RegistrarVentaPOSCommand command, decimal totalVenta, DateTime fechaUtc, CancellationToken cancellationToken = default)
        {
            VentasRegistradas.Add(command);
            return Task.FromResult(new VentaPOSResultDto(Guid.NewGuid(), "B001-00005555", totalVenta, fechaUtc));
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
        public DateTime UtcNow { get; set; } = new(2026, 11, 17, 14, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task Debe_RechazarVenta_CuandoPrecioUnitarioEnviado_NoCoincideConTarifaRevalidada()
    {
        var auth = new FakeAuthService(new FakeCurrentUserContext());
        var user = new FakeCurrentUserContext();
        var shift = new FakeCashShiftPort();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var digital = new FakeDigitalPaymentVerificationPort();
        var repo = new FakeSalesPOSRepository();
        var audit = new FakeAuditLogPort();
        var uow = new FakeUnitOfWork();
        var clock = new FakeDateTimeProvider();

        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-VAL", "Valvula 1/2", "UND", 20.00m, 12.00m, 50m, true);
        prodPort.Productos[prodId] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, shift.AlmacenTiendaId, 50m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, prodId)] = stockAlm;

        var handler = new RegistrarVentaPOSCommandHandler(
            auth, user, shift, stock, prodPort, tariff, profile, digital, repo, audit, uow, clock, new FakeKardexPort(uow));

        // Cliente intenta enviar precio 10.00 cuando la tarifa es 20.00
        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 10.00m) },
            new[] { new VentaPOSPagoDto("Efectivo", 10.00m) },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_RechazarCodigoOperacionDigitalDuplicado_ConFecha_RF_POS_006()
    {
        var auth = new FakeAuthService(new FakeCurrentUserContext());
        var user = new FakeCurrentUserContext();
        var shift = new FakeCashShiftPort();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var digital = new FakeDigitalPaymentVerificationPort();
        var repo = new FakeSalesPOSRepository();
        var audit = new FakeAuditLogPort();
        var uow = new FakeUnitOfWork();
        var clock = new FakeDateTimeProvider();

        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-VAL", "Valvula 1/2", "UND", 20.00m, 12.00m, 50m, true);
        prodPort.Productos[prodId] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, shift.AlmacenTiendaId, 50m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, prodId)] = stockAlm;

        // Ya existe en base de datos la operación 998877 de Yape para hoy
        var fechaHoy = DateOnly.FromDateTime(clock.UtcNow);
        digital.Codigos.Add(("YAPE", "998877", fechaHoy));

        var handler = new RegistrarVentaPOSCommandHandler(
            auth, user, shift, stock, prodPort, tariff, profile, digital, repo, audit, uow, clock, new FakeKardexPort(uow));

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 20.00m) },
            new[] { new VentaPOSPagoDto("Yape", 20.00m, "998877") },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_DescontarStockEnUnidadBase_DelAlmacenTiendaAsignadoAlTurno()
    {
        var auth = new FakeAuthService(new FakeCurrentUserContext());
        var user = new FakeCurrentUserContext();
        var shift = new FakeCashShiftPort();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var digital = new FakeDigitalPaymentVerificationPort();
        var repo = new FakeSalesPOSRepository();
        var audit = new FakeAuditLogPort();
        var uow = new FakeUnitOfWork();
        var clock = new FakeDateTimeProvider();

        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-CLAVO", "Clavos 2 pulg", "UND", 5.00m, 3.00m, 500m, true);
        prodPort.Productos[prodId] = prod;
        prodPort.Factores[("BOLSA_10", "UND")] = 10.0000m;

        // Almacén tienda tiene 100 unidades físicas
        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, shift.AlmacenTiendaId, 100m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, prodId)] = stockAlm;

        var handler = new RegistrarVentaPOSCommandHandler(
            auth, user, shift, stock, prodPort, tariff, profile, digital, repo, audit, uow, clock, new FakeKardexPort(uow));

        // Venta de 2 BOLSAS (20 unidades en base)
        // Precio comercial: 5.00 * 10 = 50.00 S/ por bolsa. Con 2% desc volumen (20 unid >= 10) = 49.00 S/
        // Total venta = 2 * 49.00 = 98.00 S/
        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { new VentaPOSItemDto(prodId, 2m, "BOLSA_10", 49.00m) },
            new[] { new VentaPOSPagoDto("Efectivo", 98.00m) },
            "Boleta");

        var result = await handler.Handle(cmd);

        Assert.NotNull(result);
        Assert.Equal(98.00m, result.TotalVenta);
        // Stock físico descontado en base: 100 - 20 = 80 unidades
        Assert.Equal(80m, stockAlm.StockFisico);
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoAlmacenTiendaEsVacio()
    {
        var auth = new FakeAuthService(new FakeCurrentUserContext());
        var user = new FakeCurrentUserContext();
        var shift = new FakeCashShiftPort { AlmacenTiendaId = Guid.Empty };
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var digital = new FakeDigitalPaymentVerificationPort();
        var repo = new FakeSalesPOSRepository();
        var audit = new FakeAuditLogPort();
        var uow = new FakeUnitOfWork();
        var clock = new FakeDateTimeProvider();

        var handler = new RegistrarVentaPOSCommandHandler(
            auth, user, shift, stock, prodPort, tariff, profile, digital, repo, audit, uow, clock, new FakeKardexPort(uow));

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new[] { new VentaPOSItemDto(Guid.NewGuid(), 1m, "UND", 10m) },
            new[] { new VentaPOSPagoDto("Efectivo", 10m) },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoMetodoPagoNoEsReconocido_OMontoEsNegativo()
    {
        var auth = new FakeAuthService(new FakeCurrentUserContext());
        var user = new FakeCurrentUserContext();
        var shift = new FakeCashShiftPort();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var digital = new FakeDigitalPaymentVerificationPort();
        var repo = new FakeSalesPOSRepository();
        var audit = new FakeAuditLogPort();
        var uow = new FakeUnitOfWork();
        var clock = new FakeDateTimeProvider();

        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-VAL", "Valvula 1/2", "UND", 20.00m, 12.00m, 50m, true);
        prodPort.Productos[prodId] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, shift.AlmacenTiendaId, 50m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, prodId)] = stockAlm;

        var handler = new RegistrarVentaPOSCommandHandler(
            auth, user, shift, stock, prodPort, tariff, profile, digital, repo, audit, uow, clock, new FakeKardexPort(uow));

        // Método de pago inválido
        var cmdMetodoInvalido = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 20.00m) },
            new[] { new VentaPOSPagoDto("Cripto", 20.00m) },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmdMetodoInvalido));

        // Monto <= 0
        var cmdMontoCero = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 20.00m) },
            new[] { new VentaPOSPagoDto("Efectivo", -5m) },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmdMontoCero));
    }

    [Fact]
    public async Task Debe_LanzarInvalidOperationException_CuandoFactorConversionEsInvalido()
    {
        var auth = new FakeAuthService(new FakeCurrentUserContext());
        var user = new FakeCurrentUserContext();
        var shift = new FakeCashShiftPort();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var tariff = new FakeTariffRepository();
        var profile = new FakeCustomerProfilePort();
        var digital = new FakeDigitalPaymentVerificationPort();
        var repo = new FakeSalesPOSRepository();
        var audit = new FakeAuditLogPort();
        var uow = new FakeUnitOfWork();
        var clock = new FakeDateTimeProvider();

        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-VAL", "Valvula 1/2", "UND", 20.00m, 12.00m, 50m, true);
        prodPort.Productos[prodId] = prod;
        prodPort.Factores[("CAJA_ERR", "UND")] = 0m; // Factor inválido

        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, shift.AlmacenTiendaId, 50m, 0m);
        stock.Stocks[(shift.AlmacenTiendaId, prodId)] = stockAlm;

        var handler = new RegistrarVentaPOSCommandHandler(
            auth, user, shift, stock, prodPort, tariff, profile, digital, repo, audit, uow, clock, new FakeKardexPort(uow));

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            new[] { new VentaPOSItemDto(prodId, 1m, "CAJA_ERR", 20.00m) },
            new[] { new VentaPOSPagoDto("Efectivo", 20.00m) },
            "Boleta");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd));
    }
}
