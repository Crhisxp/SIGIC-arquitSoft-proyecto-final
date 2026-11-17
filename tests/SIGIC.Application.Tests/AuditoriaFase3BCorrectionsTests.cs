using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Handlers;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Handlers;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.ProductDetailsPricing.Services;
using SIGIC.Application.SalesPOS.Commands;
using SIGIC.Application.SalesPOS.Handlers;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Tests.TestDoubles;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.Tests;

public class AuditoriaFase3BCorrectionsTests
{
    private class FakeAuditLogPort : IAuditLogPort
    {
        private readonly FakeUnitOfWork? _uow;
        public List<AuditLogEntry> Entries { get; } = new();

        public FakeAuditLogPort(FakeUnitOfWork? uow = null)
        {
            _uow = uow;
        }

        public Task RegistrarAccionAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
        {
            lock (Entries)
            {
                Entries.Add(entry);
            }
            _uow?.RegisterRollbackAction(() =>
            {
                lock (Entries) { Entries.Remove(entry); }
            });
            return Task.CompletedTask;
        }
    }

    private class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    }

    private class FakeAuthService : IAuthorizationService
    {
        public void ExigirRol(string rolRequerido) { }
        public void ExigirUnoDeRoles(params string[] rolesRequeridos) { }
    }

    private class FakeWarehouseConfigPort : IWarehouseConfigurationPort
    {
        public Guid CentralId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public Guid ObtenerAlmacenCentralId() => CentralId;
        public bool EsAlmacenValido(Guid almacenId) => true;
    }

    private class FakeInventoryStockPort : IInventoryStockPort
    {
        private readonly FakeUnitOfWork? _uow;
        private readonly object _sync = new();
        public AsyncLocal<FakeUnitOfWork?> CurrentUow { get; } = new();
        public Dictionary<(Guid AlmacenId, Guid ProductoId), StockAlmacen> Stocks { get; } = new();

        public FakeInventoryStockPort(FakeUnitOfWork? uow = null)
        {
            _uow = uow;
        }

        public Task<StockAlmacen?> ObtenerStockAlmacenAsync(Guid almacenId, Guid productoId, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                Stocks.TryGetValue((almacenId, productoId), out var s);
                return Task.FromResult(s);
            }
        }

        public Task<IReadOnlyList<StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(
            Guid almacenId, IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var res = new List<StockAlmacen>();
                var uow = CurrentUow.Value ?? _uow;
                foreach (var id in productoIds)
                {
                    if (Stocks.TryGetValue((almacenId, id), out var s))
                    {
                        if (uow != null)
                        {
                            var snapFisico = s.StockFisico;
                            var snapComprometido = s.StockComprometido;
                            uow.RegisterRollbackAction(() =>
                            {
                                lock (_sync)
                                {
                                    if (s.StockFisico != snapFisico)
                                        s.AplicarAjusteFisico(snapFisico - s.StockFisico);
                                    if (s.StockComprometido < snapComprometido)
                                        s.ReservarStock(snapComprometido - s.StockComprometido);
                                    else if (s.StockComprometido > snapComprometido)
                                        s.LiberarReserva(s.StockComprometido - snapComprometido);
                                    Stocks[(almacenId, id)] = s;
                                }
                            });
                        }
                        res.Add(s);
                    }
                }
                return Task.FromResult<IReadOnlyList<StockAlmacen>>(res);
            }
        }

        public Task ActualizarStockAlmacenAsync(StockAlmacen stock, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                Stocks[(stock.AlmacenId, stock.ProductoId)] = stock;
            }
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
            if (Factores.TryGetValue((origen, destino), out var f)) return Task.FromResult(f);
            return Task.FromResult(1.0000m);
        }
    }

    private class FakeTariffRepository : ITariffRepository
    {
        public Dictionary<string, decimal> DescuentosRol { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Task<decimal> ObtenerPorcentajeDescuentoRolAsync(string rol, CancellationToken cancellationToken = default)
        {
            DescuentosRol.TryGetValue(rol, out var d);
            return Task.FromResult(d);
        }

        public Task<decimal> ObtenerPorcentajeDescuentoVolumenAsync(decimal cantidadBase, CancellationToken cancellationToken = default)
            => Task.FromResult(0.00m);
    }

    private class FakeCustomerProfilePort : ICustomerProfilePort
    {
        public Dictionary<Guid, string> Perfiles { get; } = new();
        public Task<string> ObtenerPerfilClienteAsync(Guid? clienteId, CancellationToken cancellationToken = default)
        {
            if (clienteId.HasValue && Perfiles.TryGetValue(clienteId.Value, out var rol)) return Task.FromResult(rol);
            return Task.FromResult("Público General");
        }
    }

    private class FakeWebOrderRepository : IWebOrderRepository
    {
        private readonly FakeUnitOfWork? _uow;
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
        private readonly object _sync = new();
        public AsyncLocal<FakeUnitOfWork?> CurrentUow { get; } = new();
        public Dictionary<Guid, PedidoWebDto> Pedidos { get; } = new();

        public FakeWebOrderRepository(FakeUnitOfWork? uow = null)
        {
            _uow = uow;
        }

        public Task<PedidoWebDto?> ObtenerPorIdAsync(Guid pedidoId, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                Pedidos.TryGetValue(pedidoId, out var p);
                return Task.FromResult(p);
            }
        }

        public async Task<PedidoWebDto?> ObtenerParaBloqueoAsync(Guid pedidoId, CancellationToken cancellationToken = default)
        {
            var sem = _locks.GetOrAdd(pedidoId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(cancellationToken);
            var uow = CurrentUow.Value ?? _uow;
            uow?.RegisterCleanupAction(() =>
            {
                try { sem.Release(); } catch (ObjectDisposedException) { }
            });

            lock (_sync)
            {
                Pedidos.TryGetValue(pedidoId, out var p);
                return p;
            }
        }

        public Task GuardarPedidoAsync(PedidoWebDto pedido, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                Pedidos[pedido.Id] = pedido;
            }
            return Task.CompletedTask;
        }

        public Task ActualizarEstadoAsync(Guid pedidoId, string nuevoEstado, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (Pedidos.TryGetValue(pedidoId, out var p))
                    Pedidos[pedidoId] = p with { Estado = nuevoEstado };
            }
            return Task.CompletedTask;
        }

        public Task<bool> ActualizarEstadoConcurrenteAsync(Guid pedidoId, string estadoEsperado, string nuevoEstado, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (Pedidos.TryGetValue(pedidoId, out var p))
                {
                    if (p.Estado != estadoEsperado) return Task.FromResult(false);
                    Pedidos[pedidoId] = p with { Estado = nuevoEstado };
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
        }

        public Task<IReadOnlyList<PedidoWebDto>> ObtenerPedidosExpiradosAsync(DateTime fechaReferenciaUtc, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var res = Pedidos.Values.Where(p =>
                    (p.Estado == "PendientePago" || p.Estado == "PendienteValidacion") &&
                    (p.FechaExpiracionUtc <= fechaReferenciaUtc ||
                     (p.Estado == "PendientePago" && p.FechaLimiteVoucherUtc != null && p.FechaSubidaVoucherUtc == null && p.FechaLimiteVoucherUtc <= fechaReferenciaUtc))
                ).ToList();
                return Task.FromResult<IReadOnlyList<PedidoWebDto>>(res);
            }
        }
    }

    private class FakeKardexPort : IKardexPort
    {
        private readonly FakeUnitOfWork? _uow;
        public List<(Guid ProductoId, Guid AlmacenId, decimal CantidadBase, string TipoOperacion)> Movimientos { get; } = new();

        public FakeKardexPort(FakeUnitOfWork? uow = null)
        {
            _uow = uow;
        }

        public Task RegistrarMovimientoSalidaAsync(
            Guid productoId, Guid almacenId, decimal cantidadBase, decimal costoUnitario,
            string tipoOperacion, string documentoReferencia, DateTime fechaUtc,
            CancellationToken cancellationToken = default)
        {
            var item = (productoId, almacenId, cantidadBase, tipoOperacion);
            lock (Movimientos)
            {
                Movimientos.Add(item);
            }
            _uow?.RegisterRollbackAction(() =>
            {
                lock (Movimientos) { Movimientos.Remove(item); }
            });
            return Task.CompletedTask;
        }
    }

    private class FakeCashAlertNotificationPort : ICashAlertNotificationPort
    {
        private readonly FakeUnitOfWork? _uow;
        public List<(Guid TurnoId, decimal Diferencia, string Justificacion)> Alertas { get; } = new();

        public FakeCashAlertNotificationPort(FakeUnitOfWork? uow = null)
        {
            _uow = uow;
        }

        public Task NotificarDiscrepanciaArqueoAsync(
            Guid turnoCajaId, Guid cajeroId, decimal diferencia, string justificacion,
            DateTime fechaCierreUtc, CancellationToken cancellationToken = default)
        {
            var item = (turnoCajaId, diferencia, justificacion);
            lock (Alertas)
            {
                Alertas.Add(item);
            }
            _uow?.RegisterRollbackAction(() =>
            {
                lock (Alertas) { Alertas.Remove(item); }
            });
            return Task.CompletedTask;
        }
    }

    private class FakeVoucherStoragePort : IVoucherStoragePort
    {
        public VoucherMetadataDto? Voucher { get; set; }
        public Task<VoucherMetadataDto?> ObtenerMetadatosVoucherAsync(Guid pedidoWebId, CancellationToken cancellationToken = default)
            => Task.FromResult(Voucher);
    }

    private class FakeTransferGuidePort : ITransferGuidePort
    {
        public List<(Guid PedidoId, Guid Origen, Guid Destino, int ItemsCount)> Guias { get; } = new();
        public Task<string> GenerarGuiaTrasladoAsync(Guid pedidoId, Guid almacenOrigenId, Guid tiendaDestinoId, IReadOnlyList<(Guid ProductoId, decimal Cantidad)> items, CancellationToken cancellationToken = default)
        {
            Guias.Add((pedidoId, almacenOrigenId, tiendaDestinoId, items.Count));
            return Task.FromResult("GR-001-00001");
        }
    }

    private class FakeCashRegisterRepository : ICashRegisterRepository
    {
        private readonly FakeUnitOfWork? _uow;
        public SemaphoreSlim TurnoLock { get; } = new(1, 1);
        public AsyncLocal<FakeUnitOfWork?> CurrentUow { get; } = new();
        public bool EsAperturado { get; set; } = true;
        public decimal SaldoTeorico { get; set; } = 500m;
        public Guid CajeroId { get; set; } = Guid.NewGuid();
        public DateTime FechaApertura { get; set; } = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        public decimal FondoInicial { get; set; } = 100m;
        public bool TurnoCerrado { get; private set; }
        public ArqueoCaja? ArqueoGuardado { get; private set; }
        public string? JustificacionGuardada { get; private set; }

        public FakeCashRegisterRepository(FakeUnitOfWork? uow = null)
        {
            _uow = uow;
        }

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

        public async Task CerrarTurnoCajaAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
        {
            await TurnoLock.WaitAsync(cancellationToken);
            try
            {
                TurnoCerrado = true;
                var uow = CurrentUow.Value ?? _uow;
                uow?.RegisterRollbackAction(() => TurnoCerrado = false);
            }
            finally
            {
                TurnoLock.Release();
            }
        }

        public async Task<bool> CerrarTurnoCajaConEstadoEsperadoAsync(Guid turnoCajaId, string estadoEsperado, CancellationToken cancellationToken = default)
        {
            await TurnoLock.WaitAsync(cancellationToken);
            try
            {
                if (TurnoCerrado || !EsAperturado) return false;
                TurnoCerrado = true;
                var uow = CurrentUow.Value ?? _uow;
                uow?.RegisterRollbackAction(() => TurnoCerrado = false);
                return true;
            }
            finally
            {
                TurnoLock.Release();
            }
        }

        public Task GuardarArqueoCajaAppendOnlyAsync(ArqueoCaja arqueo, string? justificacion, CancellationToken cancellationToken = default)
        {
            ArqueoGuardado = arqueo;
            JustificacionGuardada = justificacion;
            var uow = CurrentUow.Value ?? _uow;
            uow?.RegisterRollbackAction(() =>
            {
                ArqueoGuardado = null;
                JustificacionGuardada = null;
            });
            return Task.CompletedTask;
        }
    }

    private class FakeCashShiftPort : ICashShiftPort
    {
        private readonly FakeCashRegisterRepository? _cashRepo;
        public bool TurnoValido { get; set; } = true;
        public Guid AlmacenTiendaId { get; set; } = Guid.NewGuid();

        public FakeCashShiftPort(FakeCashRegisterRepository? cashRepo = null)
        {
            _cashRepo = cashRepo;
        }

        public Task<bool> EsTurnoValidoYAperturadoAsync(Guid turnoCajaId, Guid cajeroId, CancellationToken cancellationToken = default)
        {
            if (_cashRepo != null && _cashRepo.TurnoCerrado) return Task.FromResult(false);
            return Task.FromResult(TurnoValido);
        }

        public async Task<bool> ValidarYBloquearTurnoParaVentaAsync(Guid turnoCajaId, Guid cajeroId, CancellationToken cancellationToken = default)
        {
            if (_cashRepo != null)
            {
                await _cashRepo.TurnoLock.WaitAsync(cancellationToken);
                try
                {
                    if (_cashRepo.TurnoCerrado || !_cashRepo.EsAperturado) return false;
                    return TurnoValido;
                }
                finally
                {
                    _cashRepo.TurnoLock.Release();
                }
            }
            return TurnoValido;
        }

        public Task<Guid> ObtenerAlmacenTiendaIdAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
            => Task.FromResult(AlmacenTiendaId);
    }

    private class FakeDigitalPaymentVerificationPort : IDigitalPaymentVerificationPort
    {
        private readonly FakeUnitOfWork? _uow;
        private readonly object _sync = new();
        public HashSet<string> CodigosUsados { get; } = new(StringComparer.OrdinalIgnoreCase);

        public FakeDigitalPaymentVerificationPort(FakeUnitOfWork? uow = null)
        {
            _uow = uow;
        }

        public Task<bool> ExisteCodigoOperacionAsync(string metodoPago, string codigoOperacion, DateOnly fecha, CancellationToken cancellationToken = default)
        {
            string key = $"{metodoPago.Trim().ToUpperInvariant()}_{codigoOperacion.Trim().ToUpperInvariant()}_{fecha:yyyyMMdd}";
            lock (_sync)
            {
                return Task.FromResult(CodigosUsados.Contains(key));
            }
        }

        public Task RegistrarCodigoOperacionAsync(string metodoPago, string codigoOperacion, DateOnly fecha, Guid ventaId, CancellationToken cancellationToken = default)
        {
            string key = $"{metodoPago.Trim().ToUpperInvariant()}_{codigoOperacion.Trim().ToUpperInvariant()}_{fecha:yyyyMMdd}";
            lock (_sync)
            {
                CodigosUsados.Add(key);
            }
            _uow?.RegisterRollbackAction(() =>
            {
                lock (_sync) { CodigosUsados.Remove(key); }
            });
            return Task.CompletedTask;
        }
    }

    private class FakeSalesPOSRepository : ISalesPOSRepository
    {
        public Task<VentaPOSResultDto> RegistrarVentaAsync(RegistrarVentaPOSCommand command, decimal totalVenta, DateTime fechaUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new VentaPOSResultDto(Guid.NewGuid(), "B001-00000001", totalVenta, fechaUtc));
    }

    private class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "tester";
        public IReadOnlyList<string> Roles { get; set; } = new List<string> { "Cajero", "Tesoreria", "Superadmin" };
        public string TerminalScope => "test";
        public string ClientIp => "127.0.0.1";
        public bool HasRole(string role) => Roles.Contains(role);
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    // --- TEST 1: Pérdida de unidades en ciclo web (Factor > 1: Caja 12) ---
    [Fact]
    public async Task Debe_PreservarInstantaneaInmutableCantidadBase_EnCicloCompletoWeb_FactorMayorAUno()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 0m);

        var producto = new Producto(prodId, "SKU-001", "Pintura 1GL", "UND", 20.00m, 10.00m, stockFisicoInicial: 50m, disponibleEnWeb: true);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = producto;
        fakeProd.Factores[("CAJA", "UND")] = 12.0000m;

        var fakeOrderRepo = new FakeWebOrderRepository();
        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeUser = new FakeCurrentUserContext();
        var fakeUow = new FakeUnitOfWork();
        var fakeAudit = new FakeAuditLogPort();
        var fakeDate = new FakeDateTimeProvider();
        var fakeTariff = new FakeTariffRepository();
        var fakeProfile = new FakeCustomerProfilePort();

        var reservarHandler = new ReservarPedidoWebCommandHandler(
            fakeUser, fakeStock, fakeProd, fakeTariff, fakeProfile, fakeWarehouse,
            fakeOrderRepo, fakeAudit, fakeDate, fakeUow);

        // Cliente pide 2 Cajas (factor 12) => 24 base units
        var cmd = new ReservarPedidoWebCommand(
            ClienteId: fakeUser.UserId,
            MetodoPago: "Yape",
            ModalidadEntrega: "DespachoDomicilio",
            TiendaDestinoId: null,
            Items: new[] { new PedidoWebItemDto(prodId, 2m, 240.00m, "CAJA") });

        var res = await reservarHandler.Handle(cmd);
        Assert.Equal(24m, stock.StockComprometido);

        var pedidoGuardado = fakeOrderRepo.Pedidos[res.PedidoId];
        Assert.Equal(24m, pedidoGuardado.Items[0].CantidadBase);

        var voucherPort = new FakeVoucherStoragePort
        {
            Voucher = new VoucherMetadataDto("comprobante.jpg", "image/jpeg", 1024, fakeDate.UtcNow)
        };
        var transferGuidePort = new FakeTransferGuidePort();
        var kardexPort = new FakeKardexPort();

        var validarHandler = new ValidarVoucherPagoCommandHandler(
            new FakeAuthService(), fakeUser, fakeOrderRepo, voucherPort, transferGuidePort,
            fakeStock, fakeAudit, fakeDate, fakeUow, kardexPort);

        await validarHandler.Handle(new ValidarVoucherPagoCommand(res.PedidoId, true));

        Assert.Equal(26m, stock.StockFisico);
        Assert.Equal(0m, stock.StockComprometido);
        Assert.Single(kardexPort.Movimientos);
        Assert.Equal(24m, kardexPort.Movimientos[0].CantidadBase);
    }

    // --- TEST 2: Pérdida de unidades en ciclo web (Factor < 1: Fracción 0.5) ---
    [Fact]
    public async Task Debe_PreservarInstantaneaInmutableCantidadBase_EnCicloCompletoWeb_FactorMenorAUno()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 20m, 0m);

        var producto = new Producto(prodId, "SKU-002", "Cable Fraccional", "MTR", 10.00m, 5.00m, stockFisicoInicial: 20m, disponibleEnWeb: true);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = producto;
        fakeProd.Factores[("MEDIO", "MTR")] = 0.5000m;

        var fakeOrderRepo = new FakeWebOrderRepository();
        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeUser = new FakeCurrentUserContext();
        var fakeUow = new FakeUnitOfWork();
        var fakeAudit = new FakeAuditLogPort();
        var fakeDate = new FakeDateTimeProvider();
        var fakeTariff = new FakeTariffRepository();
        var fakeProfile = new FakeCustomerProfilePort();

        var reservarHandler = new ReservarPedidoWebCommandHandler(
            fakeUser, fakeStock, fakeProd, fakeTariff, fakeProfile, fakeWarehouse,
            fakeOrderRepo, fakeAudit, fakeDate, fakeUow);

        // Cliente pide 4 "MEDIO" metro => 4 * 0.5 = 2 base units
        var cmd = new ReservarPedidoWebCommand(
            ClienteId: fakeUser.UserId,
            MetodoPago: "Yape",
            ModalidadEntrega: "DespachoDomicilio",
            TiendaDestinoId: null,
            Items: new[] { new PedidoWebItemDto(prodId, 4m, 5.00m, "MEDIO") });

        var res = await reservarHandler.Handle(cmd);
        Assert.Equal(2m, stock.StockComprometido);

        var voucherPort = new FakeVoucherStoragePort
        {
            Voucher = new VoucherMetadataDto("rechazo.png", "image/png", 2048, fakeDate.UtcNow)
        };
        var transferGuidePort = new FakeTransferGuidePort();
        var kardexPort = new FakeKardexPort();

        var validarHandler = new ValidarVoucherPagoCommandHandler(
            new FakeAuthService(), fakeUser, fakeOrderRepo, voucherPort, transferGuidePort,
            fakeStock, fakeAudit, fakeDate, fakeUow, kardexPort);

        await validarHandler.Handle(new ValidarVoucherPagoCommand(res.PedidoId, false, "Pago no encontrado"));

        var stockResult = await fakeStock.ObtenerStockAlmacenAsync(centralId, prodId);
        Assert.NotNull(stockResult);
        Assert.Equal(20m, stockResult.StockFisico);
        Assert.Equal(0m, stockResult.StockComprometido);
    }

    // --- TEST 3: Líneas repetidas y cambio posterior de factor ---
    [Fact]
    public async Task Debe_PreservarCantidadBase_ConLineasRepetidas_Y_CambioPosteriorDeFactor()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 100m, 0m);

        var producto = new Producto(prodId, "SKU-003", "Clavos", "KG", 10.00m, 4.00m, stockFisicoInicial: 100m, disponibleEnWeb: true);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = producto;
        fakeProd.Factores[("CAJA", "KG")] = 10.0000m;

        var fakeOrderRepo = new FakeWebOrderRepository();
        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeUser = new FakeCurrentUserContext();
        var fakeUow = new FakeUnitOfWork();
        var fakeAudit = new FakeAuditLogPort();
        var fakeDate = new FakeDateTimeProvider();
        var fakeTariff = new FakeTariffRepository();
        var fakeProfile = new FakeCustomerProfilePort();

        var reservarHandler = new ReservarPedidoWebCommandHandler(
            fakeUser, fakeStock, fakeProd, fakeTariff, fakeProfile, fakeWarehouse,
            fakeOrderRepo, fakeAudit, fakeDate, fakeUow);

        // 2 líneas repetidas del mismo producto: 1 Caja (10 kg) y 2 Cajas (20 kg) => Total 30 kg base
        var cmd = new ReservarPedidoWebCommand(
            ClienteId: fakeUser.UserId,
            MetodoPago: "PasarelaTarjeta",
            ModalidadEntrega: "DespachoDomicilio",
            TiendaDestinoId: null,
            Items: new[]
            {
                new PedidoWebItemDto(prodId, 1m, 100.00m, "CAJA"),
                new PedidoWebItemDto(prodId, 2m, 100.00m, "CAJA")
            });

        var res = await reservarHandler.Handle(cmd);
        Assert.Equal(30m, stock.StockComprometido);

        // Cambio posterior del factor en el catálogo a 50 kg por caja
        fakeProd.Factores[("CAJA", "KG")] = 50.0000m;

        // Expirar la reserva: DEBE liberar exactamente 30 kg usando el snapshot, NO 150 kg!
        fakeDate.UtcNow = fakeDate.UtcNow.AddMinutes(25);
        var expirarHandler = new ExpirarReservasWebCommandHandler(fakeOrderRepo, fakeStock, fakeAudit, fakeDate, fakeUow);
        await expirarHandler.Handle(new ExpirarReservasWebCommand());

        Assert.Equal(0m, stock.StockComprometido);
        Assert.Equal(100m, stock.StockFisico);
    }

    // --- TEST 4: PDP Ignora Rol Manipulado de Anónimo y Aplica Público General ---
    [Fact]
    public async Task Debe_RechazarRolManipulado_EnPDP_AplicandoPerfilPublicoGeneralAAnonimos()
    {
        var prodId = Guid.NewGuid();
        var centralId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-004", "Cemento", "BOL", 25.00m, 15.00m, stockFisicoInicial: 50m, disponibleEnWeb: true);
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 0m);

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;

        var fakeTariff = new FakeTariffRepository();
        fakeTariff.DescuentosRol["Contratista"] = 0.15m;
        fakeTariff.DescuentosRol["Público General"] = 0.00m;

        var fakeProfile = new FakeCustomerProfilePort();
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;
        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeUser = new FakeCurrentUserContext { UserId = Guid.Empty };

        var handler = new CalcularPDPYDescuentoQueryHandler(
            fakeProd, fakeTariff, fakeProfile, fakeStock, fakeWarehouse, fakeUser);

        var query = new CalcularPDPYDescuentoQuery(
            ProductoId: prodId,
            RolCliente: "Contratista",
            Cantidad: 1m,
            UnidadComercial: "BOL",
            EsCanalWeb: true,
            ClienteId: null);

        var result = await handler.Handle(query);

        Assert.Equal(0.00m, result.PorcentajeDescuentoTotal);
        Assert.Equal(25.00m, result.PrecioFinalUnitario);
    }

    // --- TEST 5: PDP Lanza UnauthorizedException al consultar perfil ajeno ---
    [Fact]
    public async Task Debe_LanzarUnauthorizedException_AlConsultarTarifaDeIdentidadAjena_EnPDP()
    {
        var prodId = Guid.NewGuid();
        var centralId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-005", "Tubo PVC", "UND", 30.00m, 18.00m, stockFisicoInicial: 50m, disponibleEnWeb: true);

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;

        var fakeTariff = new FakeTariffRepository();
        var fakeProfile = new FakeCustomerProfilePort();
        var fakeStock = new FakeInventoryStockPort();
        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };

        var myUserId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUserContext
        {
            UserId = myUserId,
            Roles = new List<string> { "ClienteWeb" }
        };

        var handler = new CalcularPDPYDescuentoQueryHandler(
            fakeProd, fakeTariff, fakeProfile, fakeStock, fakeWarehouse, fakeUser);

        var otherUserId = Guid.NewGuid();

        var query = new CalcularPDPYDescuentoQuery(
            ProductoId: prodId,
            RolCliente: null,
            Cantidad: 1m,
            UnidadComercial: "UND",
            EsCanalWeb: true,
            ClienteId: otherUserId);

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(query));
    }

    // --- TEST 6: Selección indebida de almacén no central en Web PDP ---
    [Fact]
    public async Task Debe_LanzarValidationException_AlSeleccionarAlmacenNoCentral_EnCanalWebPDP()
    {
        var prodId = Guid.NewGuid();
        var centralId = Guid.NewGuid();
        var tiendaId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-006", "Martillo", "UND", 35.00m, 20.00m, stockFisicoInicial: 50m, disponibleEnWeb: true);

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;
        var fakeStock = new FakeInventoryStockPort();
        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeUser = new FakeCurrentUserContext();

        var handler = new CalcularPDPYDescuentoQueryHandler(
            fakeProd, new FakeTariffRepository(), new FakeCustomerProfilePort(),
            fakeStock, fakeWarehouse, fakeUser);

        var query = new CalcularPDPYDescuentoQuery(
            ProductoId: prodId,
            RolCliente: null,
            Cantidad: 1m,
            UnidadComercial: "UND",
            EsCanalWeb: true,
            AlmacenId: tiendaId);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(query));
    }

    // --- TEST 7: PDP consulta stock de StockAlmacen Central reflejando movimientos ---
    [Fact]
    public async Task Debe_ConsultarStockDesdeStockAlmacen_ReflejandoMovimientosReales_EnPDP()
    {
        var prodId = Guid.NewGuid();
        var centralId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-007", "Ladrillo", "MILLAR", 100.00m, 60.00m, stockFisicoInicial: 0m, disponibleEnWeb: true);

        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 15m, 3m);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;
        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeUser = new FakeCurrentUserContext();

        var handler = new CalcularPDPYDescuentoQueryHandler(
            fakeProd, new FakeTariffRepository(), new FakeCustomerProfilePort(),
            fakeStock, fakeWarehouse, fakeUser);

        var res = await handler.Handle(new CalcularPDPYDescuentoQuery(prodId, null, 1m, "MILLAR", EsCanalWeb: true));
        Assert.Equal(12m, res.StockDisponible);

        stock.ReservarStock(5m);
        var res2 = await handler.Handle(new CalcularPDPYDescuentoQuery(prodId, null, 1m, "MILLAR", EsCanalWeb: true));
        Assert.Equal(7m, res2.StockDisponible);
    }

    // --- TEST 8: RNF-CON-004 Carrera Doble Aprobación Concurrente (Task.WhenAll + Barrera) ---
    [Fact]
    public async Task Debe_EvitarCarreraDobleAprobacion_RNF_CON_004()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 20m, 5m);
        var sharedStock = new FakeInventoryStockPort();
        sharedStock.Stocks[(centralId, prodId)] = stock;

        var pedidoId = Guid.NewGuid();
        var sharedOrderRepo = new FakeWebOrderRepository();
        var now = DateTime.UtcNow;
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, centralId,
            now, now.AddMinutes(240), now.AddMinutes(60), now, "PendienteValidacion",
            new[] { new PedidoWebItemDto(prodId, 5m, 10.00m, "UND", 1m, 5m) });
        sharedOrderRepo.Pedidos[pedidoId] = pedido;

        var voucherPort = new FakeVoucherStoragePort { Voucher = new VoucherMetadataDto("v.jpg", "image/jpeg", 100, now) };
        var transferPort = new FakeTransferGuidePort();
        var sharedKardex = new FakeKardexPort();
        var sharedAudit = new FakeAuditLogPort();
        var dateProvider = new FakeDateTimeProvider { UtcNow = now };

        var uow1 = new FakeUnitOfWork();
        var uow2 = new FakeUnitOfWork();

        var handler1 = new ValidarVoucherPagoCommandHandler(
            new FakeAuthService(), new FakeCurrentUserContext(), sharedOrderRepo, voucherPort, transferPort,
            sharedStock, sharedAudit, dateProvider, uow1, sharedKardex);

        var handler2 = new ValidarVoucherPagoCommandHandler(
            new FakeAuthService(), new FakeCurrentUserContext(), sharedOrderRepo, voucherPort, transferPort,
            sharedStock, sharedAudit, dateProvider, uow2, sharedKardex);

        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var task1 = Task.Run(async () =>
        {
            await barrier.Task;
            sharedOrderRepo.CurrentUow.Value = uow1;
            sharedStock.CurrentUow.Value = uow1;
            return await handler1.Handle(new ValidarVoucherPagoCommand(pedidoId, true));
        });

        var task2 = Task.Run(async () =>
        {
            await barrier.Task;
            sharedOrderRepo.CurrentUow.Value = uow2;
            sharedStock.CurrentUow.Value = uow2;
            return await handler2.Handle(new ValidarVoucherPagoCommand(pedidoId, true));
        });

        barrier.SetResult(true);

        var exceptions = new List<Exception>();
        VoucherValidacionResultDto? successResult = null;

        await Task.WhenAll(
            task1.ContinueWith(t => { if (t.IsFaulted) lock (exceptions) exceptions.Add(t.Exception!.InnerException!); else successResult = t.Result; }),
            task2.ContinueWith(t => { if (t.IsFaulted) lock (exceptions) exceptions.Add(t.Exception!.InnerException!); else successResult = t.Result; })
        );

        Assert.Single(exceptions);
        Assert.IsType<ValidationException>(exceptions[0]);
        Assert.NotNull(successResult);
        Assert.Equal("PagadoConfirmado", successResult.EstadoFinal);

        Assert.Equal("PagadoConfirmado", sharedOrderRepo.Pedidos[pedidoId].Estado);
        Assert.Equal(15m, stock.StockFisico);
        Assert.Equal(0m, stock.StockComprometido);
        Assert.Single(sharedKardex.Movimientos);
    }

    // --- TEST 9: RNF-CON-004 Carrera Doble Expiración Concurrente (Task.WhenAll + Barrera) ---
    [Fact]
    public async Task Debe_EvitarCarreraDobleExpiracion_RNF_CON_004()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 20m, 5m);
        var sharedStock = new FakeInventoryStockPort();
        sharedStock.Stocks[(centralId, prodId)] = stock;

        var pedidoId = Guid.NewGuid();
        var sharedOrderRepo = new FakeWebOrderRepository();
        var now = DateTime.UtcNow;
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, centralId,
            now.AddMinutes(-300), now.AddMinutes(-60), null, null, "PendientePago",
            new[] { new PedidoWebItemDto(prodId, 5m, 10.00m, "UND", 1m, 5m) });
        sharedOrderRepo.Pedidos[pedidoId] = pedido;

        var sharedAudit = new FakeAuditLogPort();
        var dateProvider = new FakeDateTimeProvider { UtcNow = now };

        var uow1 = new FakeUnitOfWork();
        var uow2 = new FakeUnitOfWork();

        var handler1 = new ExpirarReservasWebCommandHandler(sharedOrderRepo, sharedStock, sharedAudit, dateProvider, uow1);
        var handler2 = new ExpirarReservasWebCommandHandler(sharedOrderRepo, sharedStock, sharedAudit, dateProvider, uow2);

        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var task1 = Task.Run(async () =>
        {
            await barrier.Task;
            sharedOrderRepo.CurrentUow.Value = uow1;
            sharedStock.CurrentUow.Value = uow1;
            return await handler1.Handle(new ExpirarReservasWebCommand());
        });

        var task2 = Task.Run(async () =>
        {
            await barrier.Task;
            sharedOrderRepo.CurrentUow.Value = uow2;
            sharedStock.CurrentUow.Value = uow2;
            return await handler2.Handle(new ExpirarReservasWebCommand());
        });

        barrier.SetResult(true);

        var results = await Task.WhenAll(task1, task2);
        int totalExpirados = results[0].CantidadPedidosExpirados + results[1].CantidadPedidosExpirados;

        Assert.Equal(1, totalExpirados);
        Assert.Equal("Expirado", sharedOrderRepo.Pedidos[pedidoId].Estado);
        Assert.Equal(20m, stock.StockFisico);
        Assert.Equal(0m, stock.StockComprometido);
    }

    // --- TEST 10: RNF-CON-004 Aprobación vs Expiración concurrente sobre mismo producto (Task.WhenAll + Barrera) ---
    [Fact]
    public async Task Debe_EvitarCarreraAprobacionVsExpiracion_SobreMismoProducto_RNF_CON_004()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 10m, 5m);
        var sharedStock = new FakeInventoryStockPort();
        sharedStock.Stocks[(centralId, prodId)] = stock;

        var pedidoId = Guid.NewGuid();
        var sharedOrderRepo = new FakeWebOrderRepository();
        var now = DateTime.UtcNow;
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, centralId,
            now.AddMinutes(-240), now, now.AddMinutes(-180), now.AddMinutes(-200), "PendienteValidacion",
            new[] { new PedidoWebItemDto(prodId, 5m, 10.00m, "UND", 1m, 5m) });
        sharedOrderRepo.Pedidos[pedidoId] = pedido;

        var voucherPort = new FakeVoucherStoragePort { Voucher = new VoucherMetadataDto("v.jpg", "image/jpeg", 100, now.AddMinutes(-200)) };
        var transferPort = new FakeTransferGuidePort();
        var sharedKardex = new FakeKardexPort();
        var sharedAudit = new FakeAuditLogPort();
        var dateProvider = new FakeDateTimeProvider { UtcNow = now };

        var uowAprobar = new FakeUnitOfWork();
        var uowExpirar = new FakeUnitOfWork();

        var aprobarHandler = new ValidarVoucherPagoCommandHandler(
            new FakeAuthService(), new FakeCurrentUserContext(), sharedOrderRepo, voucherPort, transferPort,
            sharedStock, sharedAudit, dateProvider, uowAprobar, sharedKardex);

        var expirarHandler = new ExpirarReservasWebCommandHandler(
            sharedOrderRepo, sharedStock, sharedAudit, dateProvider, uowExpirar);

        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Exception? aprobarException = null;
        VoucherValidacionResultDto? aprobarResult = null;
        ExpirarReservasResultDto? expirarResult = null;

        var taskAprobar = Task.Run(async () =>
        {
            await barrier.Task;
            sharedOrderRepo.CurrentUow.Value = uowAprobar;
            sharedStock.CurrentUow.Value = uowAprobar;
            try
            {
                aprobarResult = await aprobarHandler.Handle(new ValidarVoucherPagoCommand(pedidoId, true));
            }
            catch (Exception ex)
            {
                aprobarException = ex;
            }
        });

        var taskExpirar = Task.Run(async () =>
        {
            await barrier.Task;
            sharedOrderRepo.CurrentUow.Value = uowExpirar;
            sharedStock.CurrentUow.Value = uowExpirar;
            expirarResult = await expirarHandler.Handle(new ExpirarReservasWebCommand());
        });

        barrier.SetResult(true);
        await Task.WhenAll(taskAprobar, taskExpirar);

        var finalOrder = sharedOrderRepo.Pedidos[pedidoId];
        if (finalOrder.Estado == "PagadoConfirmado")
        {
            Assert.NotNull(aprobarResult);
            Assert.Null(aprobarException);
            Assert.Equal(0, expirarResult!.CantidadPedidosExpirados);
            Assert.Equal(5m, stock.StockFisico);
            Assert.Equal(0m, stock.StockComprometido);
            Assert.Single(sharedKardex.Movimientos);
        }
        else
        {
            Assert.Equal("Expirado", finalOrder.Estado);
            Assert.Equal(1, expirarResult!.CantidadPedidosExpirados);
            Assert.NotNull(aprobarException);
            Assert.IsType<ValidationException>(aprobarException);
            Assert.Equal(10m, stock.StockFisico);
            Assert.Equal(0m, stock.StockComprometido);
            Assert.Empty(sharedKardex.Movimientos);
        }
    }

    // --- TEST 11: Cierre de Turno Atómico y Ventas contra Turno Cerrado Concurrentemente (Task.WhenAll + Barrera) ---
    [Fact]
    public async Task Debe_ImpedirDobleCierreTurnoCaja_Y_VentasContraTurnoCerradoConcurrentemente()
    {
        var repo = new FakeCashRegisterRepository { SaldoTeorico = 500m };
        var turnoId = Guid.NewGuid();
        repo.CajeroId = Guid.NewGuid();

        var fakeUser = new FakeCurrentUserContext { UserId = repo.CajeroId };
        var fakeAudit = new FakeAuditLogPort();
        var fakeDate = new FakeDateTimeProvider();
        var fakeAlert = new FakeCashAlertNotificationPort();

        var uow1 = new FakeUnitOfWork();
        var uow2 = new FakeUnitOfWork();
        var uowSale = new FakeUnitOfWork();

        var handler1 = new RegistrarArqueoCiegoCommandHandler(
            new FakeAuthService(), fakeUser, repo, fakeAudit, fakeDate, uow1, fakeAlert);

        var handler2 = new RegistrarArqueoCiegoCommandHandler(
            new FakeAuthService(), fakeUser, repo, fakeAudit, fakeDate, uow2, fakeAlert);

        var shiftPort = new FakeCashShiftPort(repo);
        var prodId = Guid.NewGuid();
        var tiendaId = shiftPort.AlmacenTiendaId;
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, tiendaId, 10m, 0m);
        var fakeStock = new FakeInventoryStockPort(uowSale);
        fakeStock.Stocks[(tiendaId, prodId)] = stock;

        var prod = new Producto(prodId, "SKU-P1", "Pintura", "UND", 10.00m, 5.00m, stockFisicoInicial: 10m, disponibleEnWeb: true);
        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;

        var posHandler = new RegistrarVentaPOSCommandHandler(
            new FakeAuthService(), fakeUser, shiftPort, fakeStock, fakeProd,
            new FakeTariffRepository(), new FakeCustomerProfilePort(),
            new FakeDigitalPaymentVerificationPort(uowSale), new FakeSalesPOSRepository(),
            fakeAudit, uowSale, fakeDate, new FakeKardexPort(uowSale));

        var ventaCmd = new RegistrarVentaPOSCommand(
            turnoId,
            fakeUser.UserId,
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 10.00m) },
            new[] { new VentaPOSPagoDto("Efectivo", 10.00m) },
            "Boleta");

        var desglose = new[] { new ArqueoDenominacionDto(100.00m, 6) }; // 600.00 exacto (Fondo 100 + Teórico 500)
        var cmd = new RegistrarArqueoCiegoCommand(turnoId, desglose, 0m, null);

        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var task1 = Task.Run(async () =>
        {
            await barrier.Task;
            repo.CurrentUow.Value = uow1;
            return await handler1.Handle(cmd);
        });

        var task2 = Task.Run(async () =>
        {
            await barrier.Task;
            repo.CurrentUow.Value = uow2;
            return await handler2.Handle(cmd);
        });

        var task3 = Task.Run(async () =>
        {
            await barrier.Task;
            return await posHandler.Handle(ventaCmd);
        });

        barrier.SetResult(true);

        var closeExceptions = new List<Exception>();
        Exception? saleException = null;
        ArqueoResultDto? closeSuccess = null;
        VentaPOSResultDto? saleSuccess = null;

        await Task.WhenAll(
            task1.ContinueWith(t => { if (t.IsFaulted) lock (closeExceptions) closeExceptions.Add(t.Exception!.InnerException!); else closeSuccess = t.Result; }),
            task2.ContinueWith(t => { if (t.IsFaulted) lock (closeExceptions) closeExceptions.Add(t.Exception!.InnerException!); else closeSuccess = t.Result; }),
            task3.ContinueWith(t => { if (t.IsFaulted) saleException = t.Exception!.InnerException!; else saleSuccess = t.Result; })
        );

        Assert.True(repo.TurnoCerrado);
        Assert.NotNull(closeSuccess);
        Assert.Equal(turnoId, closeSuccess.TurnoCajaId);

        if (saleException != null)
        {
            // El cierre ganó la carrera: 1 cierre exitoso, 2 ValidationException (el otro cierre y la venta concurrente)
            Assert.IsType<ValidationException>(saleException);
            Assert.Single(closeExceptions);
            Assert.IsType<ValidationException>(closeExceptions[0]);
            Assert.Null(saleSuccess);
        }
        else
        {
            // La venta ganó la carrera antes del cierre: venta exitosa, 1 cierre exitoso, 1 ValidationException en el segundo cierre
            Assert.NotNull(saleSuccess);
            Assert.Single(closeExceptions);
            Assert.IsType<ValidationException>(closeExceptions[0]);
        }
    }

    // --- TEST 12 & 13: RF-CAJ-002 Justificación, Alerta Superadmin y Ceguera Total ---
    [Fact]
    public async Task Debe_ExigirJustificacionObligatoria_AnteDiscrepanciaEnArqueoCiego_Y_EmitirAlertaSuperadmin()
    {
        var fakeUow = new FakeUnitOfWork();
        var repo = new FakeCashRegisterRepository(fakeUow) { SaldoTeorico = 500m, FondoInicial = 100m };
        var turnoId = Guid.NewGuid();
        repo.CajeroId = Guid.NewGuid();

        var fakeUser = new FakeCurrentUserContext { UserId = repo.CajeroId };
        var fakeAlert = new FakeCashAlertNotificationPort(fakeUow);

        var handler = new RegistrarArqueoCiegoCommandHandler(
            new FakeAuthService(), fakeUser, repo, new FakeAuditLogPort(),
            new FakeDateTimeProvider(), fakeUow, fakeAlert);

        var desglose = new[] { new ArqueoDenominacionDto(100.00m, 5), new ArqueoDenominacionDto(20.00m, 4) }; // 580.00

        var cmdSinJust = new RegistrarArqueoCiegoCommand(turnoId, desglose, 0m, null);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmdSinJust));

        Assert.DoesNotContain("500", ex.Message);
        Assert.DoesNotContain("600", ex.Message);
        Assert.DoesNotContain("20", ex.Message);

        var cmdConJust = new RegistrarArqueoCiegoCommand(turnoId, desglose, 0m, "Error de vuelto en cliente");
        var res = await handler.Handle(cmdConJust);

        Assert.Equal(turnoId, res.TurnoCajaId);
        Assert.Equal(580m, res.TotalDeclarado);
        Assert.Single(fakeAlert.Alertas);
        Assert.Equal(-20m, fakeAlert.Alertas[0].Diferencia);
        Assert.Equal("Error de vuelto en cliente", fakeAlert.Alertas[0].Justificacion);
    }

    // --- TEST 14: Denominaciones no oficiales rechazadas ---
    [Fact]
    public async Task Debe_RechazarDenominacionesNoOficiales_EnArqueoCiego()
    {
        var repo = new FakeCashRegisterRepository();
        var fakeUser = new FakeCurrentUserContext { UserId = repo.CajeroId };
        var handler = new RegistrarArqueoCiegoCommandHandler(
            new FakeAuthService(), fakeUser, repo, new FakeAuditLogPort(),
            new FakeDateTimeProvider(), new FakeUnitOfWork(), new FakeCashAlertNotificationPort());

        var desglose = new[] { new ArqueoDenominacionDto(3.00m, 5) };
        var cmd = new RegistrarArqueoCiegoCommand(Guid.NewGuid(), desglose, 0m, null);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(cmd));
    }

    // --- TEST 15: Detener checkout ante diferencia de un centavo (S/ 0.01) ---
    [Fact]
    public async Task Debe_DetenerCheckout_AnteDiferenciaDeUnCentavo_EnPOS_Y_Web()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-015", "Tornillo", "UND", 10.00m, 5.00m, stockFisicoInicial: 50m, disponibleEnWeb: true);

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;

        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 0m);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeUser = new FakeCurrentUserContext();
        var fakeUow = new FakeUnitOfWork();

        var webHandler = new ReservarPedidoWebCommandHandler(
            fakeUser, fakeStock, fakeProd, new FakeTariffRepository(), new FakeCustomerProfilePort(),
            fakeWarehouse, new FakeWebOrderRepository(), new FakeAuditLogPort(),
            new FakeDateTimeProvider(), fakeUow);

        var cmdWeb = new ReservarPedidoWebCommand(
            fakeUser.UserId, "Yape", "DespachoDomicilio", null,
            new[] { new PedidoWebItemDto(prodId, 1m, 10.01m, "UND") });

        await Assert.ThrowsAsync<ValidationException>(() => webHandler.Handle(cmdWeb));

        var shiftPort = new FakeCashShiftPort { AlmacenTiendaId = centralId };
        var posHandler = new RegistrarVentaPOSCommandHandler(
            new FakeAuthService(), fakeUser, shiftPort, fakeStock, fakeProd,
            new FakeTariffRepository(), new FakeCustomerProfilePort(),
            new FakeDigitalPaymentVerificationPort(), new FakeSalesPOSRepository(),
            new FakeAuditLogPort(), fakeUow, new FakeDateTimeProvider(), new FakeKardexPort());

        var cmdPOS = new RegistrarVentaPOSCommand(
            Guid.NewGuid(),
            fakeUser.UserId,
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 10.01m) },
            new[] { new VentaPOSPagoDto("Efectivo", 10.01m) },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => posHandler.Handle(cmdPOS));
    }

    // --- TEST 16: Transferencia en POS con unicidad normalizada ---
    [Fact]
    public async Task Debe_AdmitirMedioTransferenciaEnPOS_ConUnicidadNormalizadaPorMedioCodigoFecha()
    {
        var tiendaId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-016", "Lija", "UND", 10.00m, 5.00m, stockFisicoInicial: 50m, disponibleEnWeb: true);

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;

        var stock = new StockAlmacen(Guid.NewGuid(), prodId, tiendaId, 50m, 0m);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(tiendaId, prodId)] = stock;

        var shiftPort = new FakeCashShiftPort { AlmacenTiendaId = tiendaId };
        var digPort = new FakeDigitalPaymentVerificationPort();
        var fakeUser = new FakeCurrentUserContext();
        var fakeUow = new FakeUnitOfWork();
        var kardex = new FakeKardexPort();

        var posHandler = new RegistrarVentaPOSCommandHandler(
            new FakeAuthService(), fakeUser, shiftPort, fakeStock, fakeProd,
            new FakeTariffRepository(), new FakeCustomerProfilePort(),
            digPort, new FakeSalesPOSRepository(), new FakeAuditLogPort(),
            fakeUow, new FakeDateTimeProvider(), kardex);

        var cmd1 = new RegistrarVentaPOSCommand(
            Guid.NewGuid(),
            fakeUser.UserId,
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 10.00m) },
            new[] { new VentaPOSPagoDto("Transferencia", 10.00m, "  bcp-12345  ") },
            "Boleta");

        var res1 = await posHandler.Handle(cmd1);
        Assert.Equal("B001-00000001", res1.NumeroComprobante);
        Assert.Single(kardex.Movimientos);

        var cmd2 = new RegistrarVentaPOSCommand(
            Guid.NewGuid(),
            fakeUser.UserId,
            new[] { new VentaPOSItemDto(prodId, 1m, "UND", 10.00m) },
            new[] { new VentaPOSPagoDto("Transferencia", 10.00m, "BCP-12345") },
            "Boleta");

        await Assert.ThrowsAsync<ValidationException>(() => posHandler.Handle(cmd2));
    }

    // --- TEST 17: Retiro en Almacén Central en Web ---
    [Fact]
    public async Task Debe_AdmitirRetiroEnAlmacenCentral_EnCanalWeb()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-017", "Foco LED", "UND", 15.00m, 8.00m, stockFisicoInicial: 50m, disponibleEnWeb: true);

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;

        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 0m);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var fakeWarehouse = new FakeWarehouseConfigPort { CentralId = centralId };
        var fakeOrderRepo = new FakeWebOrderRepository();
        var fakeUser = new FakeCurrentUserContext();
        var fakeUow = new FakeUnitOfWork();
        var fakeDate = new FakeDateTimeProvider();

        var webHandler = new ReservarPedidoWebCommandHandler(
            fakeUser, fakeStock, fakeProd, new FakeTariffRepository(), new FakeCustomerProfilePort(),
            fakeWarehouse, fakeOrderRepo, new FakeAuditLogPort(), fakeDate, fakeUow);

        var cmdWeb = new ReservarPedidoWebCommand(
            fakeUser.UserId, "Yape", "RetiroEnAlmacenCentral", null,
            new[] { new PedidoWebItemDto(prodId, 1m, 15.00m, "UND") });

        var res = await webHandler.Handle(cmdWeb);
        Assert.Equal("PendientePago", res.Estado);

        var voucherPort = new FakeVoucherStoragePort { Voucher = new VoucherMetadataDto("v.pdf", "application/pdf", 1024, fakeDate.UtcNow) };
        var transferPort = new FakeTransferGuidePort();
        var kardex = new FakeKardexPort();

        var validarHandler = new ValidarVoucherPagoCommandHandler(
            new FakeAuthService(), fakeUser, fakeOrderRepo, voucherPort, transferPort,
            fakeStock, new FakeAuditLogPort(), fakeDate, fakeUow, kardex);

        var resVal = await validarHandler.Handle(new ValidarVoucherPagoCommand(res.PedidoId, true));
        Assert.Equal("PagadoConfirmado", resVal.EstadoFinal);
        Assert.Null(resVal.GuiaTrasladoCodigo);
        Assert.Empty(transferPort.Guias);
        Assert.Single(kardex.Movimientos);
    }

    // --- TEST 18: Reversión integral en dobles ante fallo de commit ---
    [Fact]
    public async Task Debe_RevertirEstadoIntegral_EnTestDoubles_AnteFalloIntermedio_O_FalloEnCommit()
    {
        // 1. Caso Venta POS con fallo en Commit
        var tiendaId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var prod = new Producto(prodId, "SKU-018", "Cinta", "UND", 10.00m, 5.00m, stockFisicoInicial: 20m, disponibleEnWeb: true);

        var fakeProd = new FakeProductQueryPort();
        fakeProd.Productos[prodId] = prod;

        var stock = new StockAlmacen(Guid.NewGuid(), prodId, tiendaId, 20m, 0m);
        var fakeUow = new FakeUnitOfWork { FailOnTransactionCommit = true };
        var fakeStock = new FakeInventoryStockPort(fakeUow);
        fakeStock.Stocks[(tiendaId, prodId)] = stock;

        var shiftPort = new FakeCashShiftPort { AlmacenTiendaId = tiendaId };
        var digPort = new FakeDigitalPaymentVerificationPort(fakeUow);
        var fakeUser = new FakeCurrentUserContext();
        var kardex = new FakeKardexPort(fakeUow);
        var fakeAudit = new FakeAuditLogPort(fakeUow);

        var posHandler = new RegistrarVentaPOSCommandHandler(
            new FakeAuthService(), fakeUser, shiftPort, fakeStock, fakeProd,
            new FakeTariffRepository(), new FakeCustomerProfilePort(),
            digPort, new FakeSalesPOSRepository(), fakeAudit,
            fakeUow, new FakeDateTimeProvider(), kardex);

        var cmd = new RegistrarVentaPOSCommand(
            Guid.NewGuid(),
            fakeUser.UserId,
            new[] { new VentaPOSItemDto(prodId, 2m, "UND", 10.00m) },
            new[] { new VentaPOSPagoDto("Yape", 20.00m, "OP-123456") },
            "Boleta");

        await Assert.ThrowsAsync<InvalidOperationException>(() => posHandler.Handle(cmd));

        var stockResult18 = await fakeStock.ObtenerStockAlmacenAsync(tiendaId, prodId);
        Assert.NotNull(stockResult18);
        Assert.Equal(20m, stockResult18.StockFisico);
        Assert.Equal(0m, stockResult18.StockComprometido);
        Assert.Empty(kardex.Movimientos);
        Assert.Empty(fakeAudit.Entries);
        Assert.Empty(digPort.CodigosUsados);

        // 2. Caso Arqueo de Caja con discrepancia y fallo en Commit
        var cashUow = new FakeUnitOfWork { FailOnTransactionCommit = true };
        var cashRepo = new FakeCashRegisterRepository(cashUow) { SaldoTeorico = 500m, FondoInicial = 100m };
        var turnoCajaId = Guid.NewGuid();
        cashRepo.CajeroId = fakeUser.UserId;

        var cashAudit = new FakeAuditLogPort(cashUow);
        var cashAlert = new FakeCashAlertNotificationPort(cashUow);

        var arqueoHandler = new RegistrarArqueoCiegoCommandHandler(
            new FakeAuthService(), fakeUser, cashRepo, cashAudit,
            new FakeDateTimeProvider(), cashUow, cashAlert);

        var desglose = new[] { new ArqueoDenominacionDto(100.00m, 5) }; // 500 declarados vs 600 teóricos (-100 diferencia)
        var cmdArqueo = new RegistrarArqueoCiegoCommand(turnoCajaId, desglose, 0m, "Justificación por descuadre");

        await Assert.ThrowsAsync<InvalidOperationException>(() => arqueoHandler.Handle(cmdArqueo));

        Assert.False(cashRepo.TurnoCerrado);
        Assert.Null(cashRepo.ArqueoGuardado);
        Assert.Empty(cashAlert.Alertas);
        Assert.Empty(cashAudit.Entries);
    }

    // --- TEST 19: Expiración de pedidos manuales sin voucher a los 60 min (RF-WEB-002) ---
    [Fact]
    public async Task Debe_ExpirarPedidoManual_A_Los_61Min_SinVoucher_Y_LiberarStock()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 2m);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var pedidoId = Guid.NewGuid();
        var fakeOrderRepo = new FakeWebOrderRepository();
        var now = DateTime.UtcNow;

        // Creado hace 61 min, sin voucher subido, plazo voucher 60 min expiró hace 1 min, plazo total 240 min expira en 179 min
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, centralId,
            now.AddMinutes(-61), now.AddMinutes(179), now.AddMinutes(-1), null, "PendientePago",
            new[] { new PedidoWebItemDto(prodId, 2m, 10.00m, "UND", 1m, 2m) });
        fakeOrderRepo.Pedidos[pedidoId] = pedido;

        var fakeUow = new FakeUnitOfWork();
        var fakeAudit = new FakeAuditLogPort();
        var fakeDate = new FakeDateTimeProvider { UtcNow = now };

        var expirarHandler = new ExpirarReservasWebCommandHandler(fakeOrderRepo, fakeStock, fakeAudit, fakeDate, fakeUow);

        var res = await expirarHandler.Handle(new ExpirarReservasWebCommand());

        Assert.Equal(1, res.CantidadPedidosExpirados);
        Assert.Equal("Expirado", fakeOrderRepo.Pedidos[pedidoId].Estado);
        Assert.Equal(50m, stock.StockFisico);
        Assert.Equal(0m, stock.StockComprometido);
        Assert.Equal(50m, stock.StockDisponible);
    }

    // --- TEST 20: Pedido manual con voucher subido antes de 60 min se mantiene vigente hasta los 240 min (RF-WEB-002) ---
    [Fact]
    public async Task Debe_MantenerVigentePedidoManual_ConVoucherSubidoAntesDe60Min_HastaLos240Min()
    {
        var centralId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var stock = new StockAlmacen(Guid.NewGuid(), prodId, centralId, 50m, 5m);
        var fakeStock = new FakeInventoryStockPort();
        fakeStock.Stocks[(centralId, prodId)] = stock;

        var pedidoId = Guid.NewGuid();
        var fakeOrderRepo = new FakeWebOrderRepository();
        var now = DateTime.UtcNow;

        // Creado hace 61 min, voucher subido a los 50 min (hace 11 min), estado PendienteValidacion
        var pedido = new PedidoWebDto(
            pedidoId, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, centralId,
            now.AddMinutes(-61), now.AddMinutes(179), now.AddMinutes(-1), now.AddMinutes(-11), "PendienteValidacion",
            new[] { new PedidoWebItemDto(prodId, 5m, 10.00m, "UND", 1m, 5m) });
        fakeOrderRepo.Pedidos[pedidoId] = pedido;

        var fakeUow = new FakeUnitOfWork();
        var fakeAudit = new FakeAuditLogPort();
        var fakeDate = new FakeDateTimeProvider { UtcNow = now };

        var expirarHandler = new ExpirarReservasWebCommandHandler(fakeOrderRepo, fakeStock, fakeAudit, fakeDate, fakeUow);

        // A los 61 min no debe expirar porque el voucher fue subido dentro de la ventana de 60 min
        var res61 = await expirarHandler.Handle(new ExpirarReservasWebCommand());
        Assert.Equal(0, res61.CantidadPedidosExpirados);
        Assert.Equal("PendienteValidacion", fakeOrderRepo.Pedidos[pedidoId].Estado);
        Assert.Equal(5m, stock.StockComprometido);

        // Avanzar el tiempo a los 241 min desde la creación (ahora supera el TTL total de 240 min)
        fakeDate.UtcNow = now.AddMinutes(181);
        var res241 = await expirarHandler.Handle(new ExpirarReservasWebCommand());
        Assert.Equal(1, res241.CantidadPedidosExpirados);
        Assert.Equal("Expirado", fakeOrderRepo.Pedidos[pedidoId].Estado);
        Assert.Equal(0m, stock.StockComprometido);
        Assert.Equal(50m, stock.StockDisponible);
    }
}
