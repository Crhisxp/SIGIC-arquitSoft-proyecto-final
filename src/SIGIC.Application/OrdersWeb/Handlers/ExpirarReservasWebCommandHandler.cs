using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;

namespace SIGIC.Application.OrdersWeb.Handlers;

/// <summary>
/// Caso de uso para liberación idempotente de reservas expiradas del canal web (RF-WEB-002, RNF-CON-003).
/// Aísla fallos mediante transacciones independientes por pedido y garantiza orden Havender para evitar deadlocks.
/// </summary>
public class ExpirarReservasWebCommandHandler
{
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private readonly IWebOrderRepository _webOrderRepository;
    private readonly IInventoryStockPort _inventoryStockPort;
    private readonly IAuditLogPort _auditLogPort;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ExpirarReservasWebCommandHandler(
        IWebOrderRepository webOrderRepository,
        IInventoryStockPort inventoryStockPort,
        IAuditLogPort auditLogPort,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _webOrderRepository = webOrderRepository ?? throw new ArgumentNullException(nameof(webOrderRepository));
        _inventoryStockPort = inventoryStockPort ?? throw new ArgumentNullException(nameof(inventoryStockPort));
        _auditLogPort = auditLogPort ?? throw new ArgumentNullException(nameof(auditLogPort));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ExpirarReservasResultDto> Handle(ExpirarReservasWebCommand command, CancellationToken cancellationToken = default)
    {
        DateTime now = _dateTimeProvider.UtcNow;
        IReadOnlyList<PedidoWebDto> pedidosExpirados;

        if (command.PedidoId.HasValue)
        {
            var p = await _webOrderRepository.ObtenerPorIdAsync(command.PedidoId.Value, cancellationToken);
            pedidosExpirados = p != null ? new[] { p } : Array.Empty<PedidoWebDto>();
        }
        else
        {
            pedidosExpirados = await _webOrderRepository.ObtenerPedidosExpiradosAsync(now, cancellationToken);
        }

        if (!pedidosExpirados.Any())
            return new ExpirarReservasResultDto(0, Array.Empty<Guid>(), Array.Empty<string>());

        var procesados = new List<Guid>();
        var errores = new List<string>();

        foreach (var pedido in pedidosExpirados)
        {
            try
            {
                bool procesado = await _unitOfWork.ExecuteInTransactionWithRetryAsync<bool>(async ct =>
                {
                    var lockedPedido = await _webOrderRepository.ObtenerParaBloqueoAsync(pedido.Id, ct);
                    if (lockedPedido == null || (!lockedPedido.Estado.Equals("PendientePago", StringComparison.OrdinalIgnoreCase) &&
                                                !lockedPedido.Estado.Equals("PendienteValidacion", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }

                    // Invariante RF-WEB-002: Expiración por TTL total o por no adjuntar comprobante en ventana de 60 min
                    bool estaExpiradoPorTtlTotal = lockedPedido.FechaExpiracionUtc <= now;
                    bool estaExpiradoPorVoucherNoSubido = lockedPedido.Estado.Equals("PendientePago", StringComparison.OrdinalIgnoreCase) &&
                                                          lockedPedido.FechaLimiteVoucherUtc.HasValue &&
                                                          lockedPedido.FechaSubidaVoucherUtc == null &&
                                                          lockedPedido.FechaLimiteVoucherUtc.Value <= now;

                    if (!estaExpiradoPorTtlTotal && !estaExpiradoPorVoucherNoSubido)
                    {
                        return false;
                    }

                    bool transitionOk = await _webOrderRepository.ActualizarEstadoConcurrenteAsync(lockedPedido.Id, lockedPedido.Estado, "Expirado", ct);
                    if (!transitionOk)
                    {
                        return false;
                    }

                    var productIds = lockedPedido.Items.Select(i => i.ProductoId).Distinct().OrderBy(id => id).ToList();
                    var stocks = await _inventoryStockPort.ObtenerParaBloqueoOrdenadosAsync(lockedPedido.AlmacenOrigenId, productIds, ct);
                    var stockDict = stocks.ToDictionary(s => s.ProductoId);

                    foreach (var item in lockedPedido.Items)
                    {
                        if (stockDict.TryGetValue(item.ProductoId, out var stockAlm))
                        {
                            stockAlm.LiberarReserva(item.CantidadBaseEfectiva);
                            await _inventoryStockPort.ActualizarStockAlmacenAsync(stockAlm, ct);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Stock no encontrado en almacén {lockedPedido.AlmacenOrigenId} para producto {item.ProductoId}.");
                        }
                    }

                    string detalleExpiracion = estaExpiradoPorVoucherNoSubido
                        ? $"Reserva web manual expirada por no adjuntar comprobante en ventana de 60 min (Límite: {lockedPedido.FechaLimiteVoucherUtc:o}). Stock liberado."
                        : $"Reserva web expirada por superar TTL total (Expiración: {lockedPedido.FechaExpiracionUtc:o}). Stock liberado.";

                    await _auditLogPort.RegistrarAccionAsync(new AuditLogEntry(
                        SystemUserId,
                        "EXPIRACION_RESERVA_WEB",
                        "PedidoWeb",
                        lockedPedido.Id.ToString(),
                        detalleExpiracion,
                        "127.0.0.1",
                        "background_worker",
                        now), ct);

                    await _unitOfWork.CommitAsync(ct);
                    return true;
                }, cancellationToken);

                if (procesado)
                {
                    procesados.Add(pedido.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errores.Add($"Error al expirar pedido {pedido.Id}: {ex.Message}");
            }
        }

        return new ExpirarReservasResultDto(procesados.Count, procesados, errores);
    }
}
