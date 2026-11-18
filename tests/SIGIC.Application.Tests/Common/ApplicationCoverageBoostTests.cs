using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Handlers;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.ProductDetailsPricing.Services;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Tests.TestDoubles;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.Tests.Common;

public class ApplicationCoverageBoostTests
{
    [Fact]
    public void UserAccountStatusDto_DebeInicializarYExponerPropiedades()
    {
        var id = Guid.NewGuid();
        var lockUntil = DateTime.UtcNow.AddMinutes(15);
        var dto = new UserAccountStatusDto(id, "admin_user", "hash123", 3, lockUntil, true, new[] { "Admin", "Cajero" });

        Assert.Equal(id, dto.UserId);
        Assert.Equal("admin_user", dto.Username);
        Assert.Equal("hash123", dto.PasswordHash);
        Assert.Equal(3, dto.IntentosFallidos);
        Assert.Equal(lockUntil, dto.BloqueadoHastaUtc);
        Assert.True(dto.EstaActivo);
        Assert.Equal(2, dto.Roles.Count);
    }

    [Fact]
    public void JwtDtos_DebenInicializarYExponerPropiedades()
    {
        var id = Guid.NewGuid();
        var expires = DateTime.UtcNow.AddMinutes(15);
        var tokenRes = new JwtSecurityTokenResult("jwt_token_sample", expires, "jti-12345");

        Assert.Equal("jwt_token_sample", tokenRes.Token);
        Assert.Equal(expires, tokenRes.ExpiresAtUtc);
        Assert.Equal("jti-12345", tokenRes.Jti);

        var valResSuccess = new JwtValidationResult(true, id, "operador", new[] { "Cajero" }, "SIGIC_InternalStaff", null);
        Assert.True(valResSuccess.IsValid);
        Assert.Equal(id, valResSuccess.UserId);
        Assert.Equal("operador", valResSuccess.Username);
        Assert.Contains("Cajero", valResSuccess.Roles);
        Assert.Equal("SIGIC_InternalStaff", valResSuccess.Audience);
        Assert.Null(valResSuccess.FailureReason);

        var valResFail = new JwtValidationResult(false, null, null, Array.Empty<string>(), null, "Token expirado");
        Assert.False(valResFail.IsValid);
        Assert.Null(valResFail.UserId);
        Assert.Equal("Token expirado", valResFail.FailureReason);
    }

    private class BoostProductQueryPort : IProductQueryPort
    {
        public Producto? Prod { get; set; }
        public decimal Factor { get; set; } = 1.0m;

        public Task<Producto?> ObtenerParaPDPAsync(Guid productoId, CancellationToken cancellationToken = default)
            => Task.FromResult(Prod);

        public Task<IReadOnlyList<Producto>> ObtenerMultiplesParaPDPAsync(IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Producto>>(Prod != null ? new[] { Prod } : Array.Empty<Producto>());

        public Task<decimal> ObtenerFactorConversionAsync(string origen, string destino, CancellationToken cancellationToken = default)
            => Task.FromResult(Factor);
    }

    private class BoostTariffRepo : ITariffRepository
    {
        public Task<decimal> ObtenerPorcentajeDescuentoRolAsync(string rol, CancellationToken cancellationToken = default)
            => Task.FromResult(0.05m);

        public Task<decimal> ObtenerPorcentajeDescuentoVolumenAsync(decimal cantidadBase, CancellationToken cancellationToken = default)
            => Task.FromResult(0.02m);
    }

    [Fact]
    public async Task CalcularPDPYDescuento_ConPuertosPorDefecto_DebeEjecutarCorrectamente()
    {
        var prod = new Producto(Guid.NewGuid(), "SKU-DEF-1", "Cemento Sol", "BOLSA", 28m, 20m, 100m, true);
        var productPort = new BoostProductQueryPort { Prod = prod, Factor = 1m };
        var tariffRepo = new BoostTariffRepo();

        // Inicializamos con solo los 2 puertos obligatorios -> usa DefaultCustomerProfilePort, DefaultInventoryStockPort, etc.
        var handler = new CalcularPDPYDescuentoQueryHandler(productPort, tariffRepo);

        var query = new CalcularPDPYDescuentoQuery(prod.Id, "Público General", 10m, "BOLSA", EsCanalWeb: false);
        var result = await handler.Handle(query);

        Assert.Equal(prod.Id, result.ProductoId);
        Assert.Equal(0m, result.StockDisponible); // DefaultInventoryStockPort retorna null -> stock 0
        Assert.True(result.PrecioFinalUnitario > 0m);

        // Prueba de factor de conversión inválido <= 0
        productPort.Factor = -1.0m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(query));
    }

    private class BoostWebOrderRepo : IWebOrderRepository
    {
        public PedidoWebDto? Pedido { get; set; }
        public bool TransitionSuccess { get; set; } = true;

        public Task<PedidoWebDto?> ObtenerPorIdAsync(Guid pedidoId, CancellationToken cancellationToken = default)
            => Task.FromResult(Pedido);

        public Task<PedidoWebDto?> ObtenerParaBloqueoAsync(Guid pedidoId, CancellationToken cancellationToken = default)
            => Task.FromResult(Pedido);

        public Task GuardarPedidoAsync(PedidoWebDto pedido, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ActualizarEstadoAsync(Guid pedidoId, string nuevoEstado, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PedidoWebDto>> ObtenerPedidosExpiradosAsync(DateTime fechaReferenciaUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PedidoWebDto>>(Array.Empty<PedidoWebDto>());

        public Task<bool> ActualizarEstadoConcurrenteAsync(Guid pedidoId, string estadoEsperado, string nuevoEstado, CancellationToken cancellationToken = default)
            => Task.FromResult(TransitionSuccess);
    }

    private class BoostVoucherStorage : IVoucherStoragePort
    {
        public VoucherMetadataDto? Meta { get; set; }

        public Task GuardarVoucherAsync(Guid pedidoId, string nombreArchivo, string contentType, byte[] contenidoBytes, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<VoucherMetadataDto?> ObtenerMetadatosVoucherAsync(Guid pedidoId, CancellationToken cancellationToken = default)
            => Task.FromResult(Meta);
    }

    private class BoostInventoryStockPort : IInventoryStockPort
    {
        public Task<StockAlmacen?> ObtenerStockAlmacenAsync(Guid almacenId, Guid productoId, CancellationToken cancellationToken = default)
            => Task.FromResult<StockAlmacen?>(null);

        public Task<IReadOnlyList<StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(Guid almacenId, IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StockAlmacen>>(Array.Empty<StockAlmacen>());

        public Task ActualizarStockAlmacenAsync(StockAlmacen stock, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class BoostUserContext : ICurrentUserContext
    {
        public Guid UserId => Guid.NewGuid();
        public string Username => "cajero_boost";
        public IReadOnlyList<string> Roles => new[] { "Cajero", "Tesoreria" };
        public string TerminalScope => "terminal_1";
        public string ClientIp => "127.0.0.1";
        public bool HasRole(string role) => true;
        public bool IsInRole(string role) => true;
    }

    private class BoostAuthService : IAuthorizationService
    {
        public void ExigirRol(string rolRequerido) { }
        public void ExigirUnoDeRoles(params string[] rolesRequeridos) { }
    }

    private class BoostTransferGuide : ITransferGuidePort
    {
        public Task<string> GenerarGuiaTrasladoAsync(Guid pedidoWebId, Guid almacenOrigenId, Guid tiendaDestinoId, IReadOnlyList<(Guid ProductoId, decimal Cantidad)> items, CancellationToken cancellationToken = default)
            => Task.FromResult("GUIA-BOOST-001");
    }

    private class BoostAuditLog : IAuditLogPort
    {
        public Task RegistrarAccionAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class BoostDateTime : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    [Fact]
    public async Task ValidarVoucher_ExcepcionesDeBorde_DebenLanzarseAdecuadamente()
    {
        var auth = new BoostAuthService();
        var user = new BoostUserContext();
        var orderRepo = new BoostWebOrderRepo();
        var voucherPort = new BoostVoucherStorage();
        var guidePort = new BoostTransferGuide();
        var stockPort = new BoostInventoryStockPort();
        var audit = new BoostAuditLog();
        var clock = new BoostDateTime();
        var uow = new FakeUnitOfWork();
        var kardex = new FakeKardexPort(uow);

        var handler = new ValidarVoucherPagoCommandHandler(auth, user, orderRepo, voucherPort, guidePort, stockPort, audit, clock, uow, kardex);
        var pedidoId = Guid.NewGuid();

        // 1. Pedido no existe
        orderRepo.Pedido = null;
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ValidarVoucherPagoCommand(pedidoId, true)));

        // 2. Voucher es nulo
        var pedido = new PedidoWebDto(pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(), Guid.NewGuid(), clock.UtcNow, clock.UtcNow.AddMinutes(240), clock.UtcNow.AddMinutes(60), null, "PendientePago", new List<PedidoWebItemDto>());
        orderRepo.Pedido = pedido;
        voucherPort.Meta = null;
        var exNullVoucher = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ValidarVoucherPagoCommand(pedidoId, true)));
        Assert.Contains("No se encontró ningún voucher", exNullVoucher.Message);

        // 3. Voucher excede 5MB
        voucherPort.Meta = new VoucherMetadataDto("voucher.png", "image/png", 6 * 1024 * 1024, clock.UtcNow);
        var exBig = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ValidarVoucherPagoCommand(pedidoId, true)));
        Assert.Contains("5 MB", exBig.Message);

        // 4. Expirado (> 240m)
        voucherPort.Meta = new VoucherMetadataDto("voucher.png", "image/png", 1024, clock.UtcNow);
        clock.UtcNow = pedido.FechaExpiracionUtc.AddMinutes(1);
        var exExpired = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ValidarVoucherPagoCommand(pedidoId, true)));
        Assert.Contains("expirada", exExpired.Message);

        // 5. Stock not found
        clock.UtcNow = pedido.FechaCreacionUtc.AddMinutes(5);
        var itemProdId = Guid.NewGuid();
        orderRepo.Pedido = new PedidoWebDto(pedidoId, Guid.NewGuid(), "Yape", "RetiroEnTienda", Guid.NewGuid(), Guid.NewGuid(), clock.UtcNow, clock.UtcNow.AddMinutes(240), clock.UtcNow.AddMinutes(60), null, "PendientePago", new List<PedidoWebItemDto> { new(itemProdId, 2m, 50m) });
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ValidarVoucherPagoCommand(pedidoId, true)));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ValidarVoucherPagoCommand(pedidoId, false)));
    }
}