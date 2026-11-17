using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Handlers;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Tests.TestDoubles;
using SIGIC.Domain.Entities;

namespace SIGIC.Application.Tests.CashManagement;

public class CashManagementTests
{
    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "cajero1";
        public IReadOnlyList<string> Roles { get; set; } = new List<string> { "Cajero" };
        public string TerminalScope => "pos_terminal";
        public string ClientIp => "192.168.1.10";
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

    private class FakeCashRegisterRepository : ICashRegisterRepository
    {
        public bool EsAperturado { get; set; } = true;
        public decimal SaldoTeorico { get; set; } = 500m;
        public Guid CajeroId { get; set; } = Guid.NewGuid();
        public DateTime FechaApertura { get; set; } = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        public decimal FondoInicial { get; set; } = 0m;
        public bool TurnoCerrado { get; private set; }
        public ArqueoCaja? ArqueoGuardado { get; private set; }
        public string? JustificacionGuardada { get; private set; }

        public Task<bool> EsTurnoAperturadoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
            => Task.FromResult(EsAperturado && !TurnoCerrado);

        public Task<decimal> ObtenerSaldoTeoricoSistemaAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
            => Task.FromResult(SaldoTeorico);

        public Task<Guid> ObtenerCajeroDeTurnoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
            => Task.FromResult(CajeroId);

        public Task<DateTime> ObtenerFechaAperturaTurnoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
            => Task.FromResult(FechaApertura);

        public Task<decimal> ObtenerFondoInicialAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
            => Task.FromResult(FondoInicial);

        public Task CerrarTurnoCajaAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
        {
            TurnoCerrado = true;
            return Task.CompletedTask;
        }

        public Task<bool> CerrarTurnoCajaConEstadoEsperadoAsync(Guid turnoCajaId, string estadoEsperado, CancellationToken cancellationToken = default)
        {
            if (TurnoCerrado || !EsAperturado) return Task.FromResult(false);
            TurnoCerrado = true;
            return Task.FromResult(true);
        }

        public Task GuardarArqueoCajaAppendOnlyAsync(ArqueoCaja arqueo, string? justificacion, CancellationToken cancellationToken = default)
        {
            ArqueoGuardado = arqueo;
            JustificacionGuardada = justificacion;
            return Task.CompletedTask;
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
        public DateTime UtcNow { get; set; } = new(2026, 11, 17, 16, 0, 0, DateTimeKind.Utc);
    }

    private class FakeUnitOfWork : SIGIC.Application.Tests.TestDoubles.FakeUnitOfWork
    {
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
            var result = Pedidos.Values.Where(p =>
                (p.Estado == "PendientePago" || p.Estado == "PendienteValidacion") &&
                (p.FechaExpiracionUtc <= fechaReferenciaUtc ||
                 (p.Estado == "PendientePago" && p.FechaLimiteVoucherUtc.HasValue && p.FechaSubidaVoucherUtc == null && p.FechaLimiteVoucherUtc.Value <= fechaReferenciaUtc))
            ).ToList();
            return Task.FromResult<IReadOnlyList<PedidoWebDto>>(result);
        }
    }

    private class FakeVoucherStoragePort : IVoucherStoragePort
    {
        public VoucherMetadataDto? Metadata { get; set; }
        public Task<VoucherMetadataDto?> ObtenerMetadatosVoucherAsync(Guid pedidoWebId, CancellationToken cancellationToken = default)
            => Task.FromResult(Metadata);
    }

    private class FakeTransferGuidePort : ITransferGuidePort
    {
        public string GuiaGenerada { get; set; } = "T001-00000042";
        public Task<string> GenerarGuiaTrasladoAsync(Guid pedidoWebId, Guid almacenOrigenId, Guid tiendaDestinoId, IReadOnlyList<(Guid ProductoId, decimal Cantidad)> items, CancellationToken cancellationToken = default)
            => Task.FromResult(GuiaGenerada);
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
            var res = productoIds.Where(id => Stocks.ContainsKey((almacenId, id)))
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

    [Fact]
    public async Task Debe_ExigirRolCajero_ParaRegistrarArqueoCiego()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Cliente" } };
        var auth = new FakeAuthService(user);
        var repo = new FakeCashRegisterRepository();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new RegistrarArqueoCiegoCommandHandler(auth, user, repo, audit, clock, uow, new FakeCashAlertNotificationPort(uow));
        var cmd = new RegistrarArqueoCiegoCommand(Guid.NewGuid(), new List<ArqueoDenominacionDto> { new(100m, 5) }, 0m);

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_CerrarTurno_Y_RegistrarArqueo_SinExponerSaldoTeoricoNiDiferenciaAlCajero()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var turnoId = Guid.NewGuid();
        var repo = new FakeCashRegisterRepository { SaldoTeorico = 500m, CajeroId = user.UserId };
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new RegistrarArqueoCiegoCommandHandler(auth, user, repo, audit, clock, uow, new FakeCashAlertNotificationPort(uow));

        // Cajero declara 5 billetes de 100 = 500
        var cmd = new RegistrarArqueoCiegoCommand(turnoId, new List<ArqueoDenominacionDto> { new(100m, 5) }, 0m);

        var result = await handler.Handle(cmd);

        Assert.NotNull(result);
        Assert.Equal(500m, result.TotalDeclarado);
        Assert.Equal(turnoId, result.TurnoCajaId);
        Assert.True(repo.TurnoCerrado);
        Assert.NotNull(repo.ArqueoGuardado);
        Assert.True(repo.ArqueoGuardado.EsAppendOnly);
        // Backoffice sí puede ver el saldo teórico y la diferencia en el registro persistido
        Assert.Equal(500m, repo.ArqueoGuardado.SaldoSistemaTeorico);
        Assert.Equal(0m, repo.ArqueoGuardado.Diferencia);
    }

    [Fact]
    public async Task Debe_ExigirQueTurnoPertenezcaAlCajeroAutenticado()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        // Turno pertenece a otro cajero
        var repo = new FakeCashRegisterRepository { SaldoTeorico = 500m, CajeroId = Guid.NewGuid() };
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new RegistrarArqueoCiegoCommandHandler(auth, user, repo, audit, clock, uow, new FakeCashAlertNotificationPort(uow));
        var cmd = new RegistrarArqueoCiegoCommand(Guid.NewGuid(), new List<ArqueoDenominacionDto> { new(100m, 5) }, 0m);

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_ExigirQueTurnoEsteAperturado_EvitandoDobleCierre()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeCashRegisterRepository { SaldoTeorico = 500m, CajeroId = user.UserId, EsAperturado = false };
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new RegistrarArqueoCiegoCommandHandler(auth, user, repo, audit, clock, uow, new FakeCashAlertNotificationPort(uow));
        var cmd = new RegistrarArqueoCiegoCommand(Guid.NewGuid(), new List<ArqueoDenominacionDto> { new(100m, 5) }, 0m);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_RechazarValidacionManualVoucher_ParaPasarelaTarjeta()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var pedidoId = Guid.NewGuid();
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null, Guid.NewGuid(),
            clock.UtcNow.AddMinutes(-10), clock.UtcNow.AddMinutes(10), null, null,
            "PendientePago", new List<PedidoWebItemDto>());

        orderRepo.Pedidos[pedidoId] = pedido;

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: true);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_AceptarValidacionVoucher_CuandoEstadoEsPendienteValidacion()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var prodId = Guid.NewGuid();
        var centralId = Guid.NewGuid();
        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 5m);
        stockPort.Stocks[(centralId, prodId)] = stockAlm;

        var pedidoId = Guid.NewGuid();
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, centralId,
            clock.UtcNow.AddMinutes(-30), clock.UtcNow.AddMinutes(210), clock.UtcNow.AddMinutes(30), clock.UtcNow.AddMinutes(-5),
            "PendienteValidacion", new List<PedidoWebItemDto> { new(prodId, 5m, 100m) });

        orderRepo.Pedidos[pedidoId] = pedido;
        voucherPort.Metadata = new VoucherMetadataDto("voucher.png", "image/png", 50_000, clock.UtcNow.AddMinutes(-5));

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: true);

        var result = await handler.Handle(cmd);

        Assert.True(result.Aprobado);
        Assert.Equal("PagadoConfirmado", result.EstadoFinal);
        Assert.Equal(45m, stockAlm.StockFisico);
        Assert.Equal(0m, stockAlm.StockComprometido);
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoVoucherExcedeTamanoOFormatoInvalido()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var pedidoId = Guid.NewGuid();
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(), Guid.NewGuid(),
            clock.UtcNow.AddMinutes(-30), clock.UtcNow.AddMinutes(210), clock.UtcNow.AddMinutes(30), clock.UtcNow.AddMinutes(-10),
            "PendientePago", new List<PedidoWebItemDto>());

        orderRepo.Pedidos[pedidoId] = pedido;
        voucherPort.Metadata = new VoucherMetadataDto("comprobante.exe", "application/x-msdownload", 1024, clock.UtcNow.AddMinutes(-10));

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: true);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoTamanoVoucherEsCero()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var pedidoId = Guid.NewGuid();
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(), Guid.NewGuid(),
            clock.UtcNow.AddMinutes(-30), clock.UtcNow.AddMinutes(210), clock.UtcNow.AddMinutes(30), clock.UtcNow.AddMinutes(-10),
            "PendientePago", new List<PedidoWebItemDto>());

        orderRepo.Pedidos[pedidoId] = pedido;
        voucherPort.Metadata = new VoucherMetadataDto("voucher.jpg", "image/jpeg", 0, clock.UtcNow.AddMinutes(-10));

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: true);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoVoucherSeSubioDespuesDe60Minutos()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var pedidoId = Guid.NewGuid();
        var fechaCreacion = clock.UtcNow.AddMinutes(-70);
        var limiteVoucher = fechaCreacion.AddMinutes(60);
        var subidaVoucher = fechaCreacion.AddMinutes(65);

        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(), Guid.NewGuid(),
            fechaCreacion, fechaCreacion.AddMinutes(240), limiteVoucher, subidaVoucher,
            "PendientePago", new List<PedidoWebItemDto>());

        orderRepo.Pedidos[pedidoId] = pedido;
        voucherPort.Metadata = new VoucherMetadataDto("voucher.png", "image/png", 2048, subidaVoucher);

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: true);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_ConsolidarSalidaFisica_Y_GenerarGuiaTraslado_CuandoVoucherEsAprobadoConRetiroEnTienda()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var prodId = Guid.NewGuid();
        var almacenCentralId = Guid.NewGuid();
        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, almacenCentralId, 50m, 5m);
        stockPort.Stocks[(almacenCentralId, prodId)] = stockAlm;

        var pedidoId = Guid.NewGuid();
        var tiendaDestinoId = Guid.NewGuid();

        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", tiendaDestinoId, almacenCentralId,
            clock.UtcNow.AddMinutes(-30), clock.UtcNow.AddMinutes(210), clock.UtcNow.AddMinutes(30), clock.UtcNow.AddMinutes(-10),
            "PendientePago", new List<PedidoWebItemDto> { new(prodId, 5m, 100m) });

        orderRepo.Pedidos[pedidoId] = pedido;
        voucherPort.Metadata = new VoucherMetadataDto("voucher.pdf", "application/pdf", 100_000, clock.UtcNow.AddMinutes(-10));

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: true);

        var result = await handler.Handle(cmd);

        Assert.True(result.Aprobado);
        Assert.Equal("PagadoConfirmado", result.EstadoFinal);
        Assert.Equal("T001-00000042", result.GuiaTrasladoCodigo);
        Assert.Equal(45m, stockAlm.StockFisico);
        Assert.Equal(0m, stockAlm.StockComprometido);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task Debe_LiberarStockComprometido_CuandoVoucherEsRechazado()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var prodId = Guid.NewGuid();
        var centralId = Guid.NewGuid();
        var stockAlm = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 5m);
        stockPort.Stocks[(centralId, prodId)] = stockAlm;

        var pedidoId = Guid.NewGuid();
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(), centralId,
            clock.UtcNow.AddMinutes(-30), clock.UtcNow.AddMinutes(210), clock.UtcNow.AddMinutes(30), clock.UtcNow.AddMinutes(-10),
            "PendientePago", new List<PedidoWebItemDto> { new(prodId, 5m, 100m) });

        orderRepo.Pedidos[pedidoId] = pedido;
        voucherPort.Metadata = new VoucherMetadataDto("voucher.jpg", "image/jpeg", 50_000, clock.UtcNow.AddMinutes(-10));

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: false, MotivoRechazo: "Voucher no legible");

        var result = await handler.Handle(cmd);

        Assert.False(result.Aprobado);
        Assert.Equal("Rechazado", result.EstadoFinal);
        Assert.Equal(50m, stockAlm.StockFisico);
        Assert.Equal(0m, stockAlm.StockComprometido);
        Assert.Null(result.GuiaTrasladoCodigo);
        Assert.Equal(pedidoId, result.PedidoWebId);
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoIdTurnoEsVacio_O_DesgloseVacio()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeCashRegisterRepository();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var handler = new RegistrarArqueoCiegoCommandHandler(auth, user, repo, audit, clock, uow, new FakeCashAlertNotificationPort(uow));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RegistrarArqueoCiegoCommand(
            Guid.Empty, new List<ArqueoDenominacionDto> { new(100m, 1) }, 0m)));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RegistrarArqueoCiegoCommand(
            Guid.NewGuid(), new List<ArqueoDenominacionDto>(), 0m)));
    }

    [Fact]
    public async Task Debe_LanzarValidationException_CuandoTipoMimeEsInvalido()
    {
        var user = new FakeCurrentUserContext { Roles = new List<string> { "Tesoreria" } };
        var auth = new FakeAuthService(user);
        var orderRepo = new FakeWebOrderRepository();
        var voucherPort = new FakeVoucherStoragePort();
        var guidePort = new FakeTransferGuidePort();
        var stockPort = new FakeInventoryStockPort();
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork();

        var pedidoId = Guid.NewGuid();
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(), Guid.NewGuid(),
            clock.UtcNow.AddMinutes(-30), clock.UtcNow.AddMinutes(210), clock.UtcNow.AddMinutes(30), clock.UtcNow.AddMinutes(-10),
            "PendientePago", new List<PedidoWebItemDto>());

        orderRepo.Pedidos[pedidoId] = pedido;
        // Extensión png pero MIME type audio/mp3
        voucherPort.Metadata = new VoucherMetadataDto("voucher.png", "audio/mp3", 50_000, clock.UtcNow.AddMinutes(-10));

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, new FakeKardexPort(uow));
        var cmd = new ValidarVoucherPagoCommand(pedidoId, Aprobado: true);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    [Fact]
    public async Task Debe_RevertirTransaccion_AnteFalloEnPersistenciaArqueo()
    {
        var user = new FakeCurrentUserContext();
        var auth = new FakeAuthService(user);
        var repo = new FakeCashRegisterRepository { SaldoTeorico = 500m, CajeroId = user.UserId };
        var audit = new FakeAuditLogPort();
        var clock = new FakeDateTimeProvider();
        var uow = new FakeUnitOfWork { FailOnCommit = true };

        var handler = new RegistrarArqueoCiegoCommandHandler(auth, user, repo, audit, clock, uow, new FakeCashAlertNotificationPort(uow));
        var cmd = new RegistrarArqueoCiegoCommand(Guid.NewGuid(), new List<ArqueoDenominacionDto> { new(100m, 5) }, 0m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd));
    }
}
