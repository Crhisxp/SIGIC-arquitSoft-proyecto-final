using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Inventory.Commands;
using SIGIC.Application.Inventory.Handlers;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;

namespace SIGIC.Application.Tests.Inventory;

public class AjustarInventarioTests
{
    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username => "superadmin_test";
        public IReadOnlyList<string> Roles { get; set; } = new List<string> { "Superadmin" };
        public string TerminalScope => "MAIN_OFFICE";
        public string ClientIp => "10.0.0.1";
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
                throw new UnauthorizedException(rolRequerido, $"Se requiere el rol '{rolRequerido}'.");
        }

        public void ExigirUnoDeRoles(params string[] rolesRequeridos)
        {
            if (!rolesRequeridos.Any(r => _user.HasRole(r)))
                throw new UnauthorizedException(string.Join(",", rolesRequeridos), "Acceso denegado.");
        }
    }

    private class FakeInventoryAdjustmentRepository : IInventoryAdjustmentRepository
    {
        public List<(Guid ProductoId, decimal Cantidad, string Motivo)> KardexEntries { get; } = new();

        public Task<Guid> RegistrarAjusteKardexCompensatorioAsync(
            Guid productoId, Guid almacenId, decimal cantidadAjuste, decimal costoUnitario,
            string motivo, string codigoAutorizacion, DateTime fechaUtc, CancellationToken cancellationToken = default)
        {
            KardexEntries.Add((productoId, cantidadAjuste, motivo));
            return Task.FromResult(Guid.NewGuid());
        }
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
            var res = productoIds
                .OrderBy(id => id)
                .Where(id => Stocks.ContainsKey((almacenId, id)))
                .Select(id => Stocks[(almacenId, id)])
                .ToList();
            return Task.FromResult<IReadOnlyList<StockAlmacen>>(res);
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

        public Task<decimal> ObtenerFactorConversionAsync(string unidadOrigen, string unidadDestino, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1.0000m);
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
        public DateTime UtcNow { get; set; } = new(2026, 11, 17, 11, 0, 0, DateTimeKind.Utc);
    }

    private class FakeUnitOfWork : SIGIC.Application.Tests.TestDoubles.FakeUnitOfWork
    {
    }

    [Fact]
    public async Task Debe_ExigirRolSuperadmin_ParaAjustarInventario()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Almacenero" } };
        var auth = new FakeAuthService(user);
        var repo = new FakeInventoryAdjustmentRepository();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new AjustarInventarioCommandHandler(auth, user, repo, stock, prodPort, audit, clock, uow);
        var cmd = new AjustarInventarioCommand(Guid.NewGuid(), Guid.NewGuid(), 10m, "Ajuste conteo", "AUTH-123");

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoMotivoOCodigoAutorizacionSonVacios()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeInventoryAdjustmentRepository();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new AjustarInventarioCommandHandler(auth, user, repo, stock, prodPort, audit, clock, uow);

        var cmdSinMotivo = new AjustarInventarioCommand(Guid.NewGuid(), Guid.NewGuid(), 10m, "", "AUTH-123");
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmdSinMotivo));

        var cmdSinAuth = new AjustarInventarioCommand(Guid.NewGuid(), Guid.NewGuid(), 10m, "Merma", "");
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmdSinAuth));

        var cmdSinAlmacen = new AjustarInventarioCommand(Guid.NewGuid(), Guid.Empty, 10m, "Merma", "AUTH-123");
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmdSinAlmacen));
    }

    [Fact]
    public async Task Debe_LanzarNotFoundException_CuandoProductoNoExiste()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeInventoryAdjustmentRepository();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new AjustarInventarioCommandHandler(auth, user, repo, stock, prodPort, audit, clock, uow);

        var cmd = new AjustarInventarioCommand(Guid.NewGuid(), Guid.NewGuid(), 5m, "Conteo físico", "AUTH-999");
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarExcepcion_CuandoAjusteDejariaStockFisicoMenorAStockComprometido()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeInventoryAdjustmentRepository();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var prodId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-AJUSTE", "Prod Ajuste", "UND", 100m, 50m, 30m, true);
        prodPort.Productos[prodId] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 30m, 20m); // Comprometido = 20, Disponible = 10
        stock.Stocks[(almacenId, prodId)] = stockAlm;

        var handler = new AjustarInventarioCommandHandler(auth, user, repo, stock, prodPort, audit, clock, uow);

        // Intento de ajustar -15 (resultaría en 15 físico, menor a 20 comprometido)
        var cmd = new AjustarInventarioCommand(prodId, almacenId, -15m, "Ajuste por merma", "AUTH-ADM");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_RegistrarMovimientoKardexCompensatorio_Y_ActualizarStockFisicoExitosamente()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeInventoryAdjustmentRepository();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var prodId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-AJUSTE", "Prod Ajuste", "UND", 100m, 50m, 30m, true);
        prodPort.Productos[prodId] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 30m, 10m); // Comprometido = 10, Disponible = 20
        stock.Stocks[(almacenId, prodId)] = stockAlm;

        var handler = new AjustarInventarioCommandHandler(auth, user, repo, stock, prodPort, audit, clock, uow);

        // Ajuste de +15 unidades
        var cmd = new AjustarInventarioCommand(prodId, almacenId, 15m, "Ingreso por sobrante físico", "AUTH-SUPER");

        var result = await handler.Handle(cmd);

        Assert.NotNull(result);
        Assert.Equal(30m, result.StockFisicoAnterior);
        Assert.Equal(45m, result.StockFisicoNuevo);
        Assert.Equal(10m, result.StockComprometido);
        Assert.Equal(45m, stockAlm.StockFisico);
        Assert.Single(repo.KardexEntries);
        Assert.Equal(15m, repo.KardexEntries[0].Cantidad);
        Assert.Single(audit.Entries);
        Assert.Equal("AJUSTE_INVENTARIO", audit.Entries[0].Accion);
        Assert.NotEqual(Guid.Empty, result.AjusteId);
        Assert.Equal(prodId, result.ProductoId);
        Assert.Equal(15m, result.CantidadAjuste);
        Assert.Equal(clock.UtcNow, result.FechaUtc);

        Assert.Throws<ArgumentNullException>(() => new AjustarInventarioCommandHandler(null!, user, repo, stock, prodPort, audit, clock, uow));
    }

    [Fact]
    public async Task Debe_RevertirTransaccion_AnteFalloEnPersistencia_GarantizandoRollback()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeInventoryAdjustmentRepository();
        var stock = new FakeInventoryStockPort();
        var prodPort = new FakeProductQueryPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork { FailOnCommit = true };

        var prodId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-FAIL", "Prod Falla", "UND", 100m, 50m, 30m, true);
        prodPort.Productos[prodId] = prod;

        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, almacenId, 30m, 5m);
        stock.Stocks[(almacenId, prodId)] = stockAlm;

        decimal originalStock = stockAlm.StockFisico;
        uow.RegisterRollbackAction(() => stockAlm.AplicarAjusteFisico(originalStock - stockAlm.StockFisico));

        var handler = new AjustarInventarioCommandHandler(auth, user, repo, stock, prodPort, audit, clock, uow);
        var cmd = new AjustarInventarioCommand(prodId, almacenId, 10m, "Ajuste con fallo", "AUTH-FAIL");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd));
        Assert.Equal(30m, stockAlm.StockFisico);
    }
}
