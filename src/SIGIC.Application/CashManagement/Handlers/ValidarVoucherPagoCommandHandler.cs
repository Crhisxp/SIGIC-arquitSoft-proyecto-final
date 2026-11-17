using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;

namespace SIGIC.Application.CashManagement.Handlers;

/// <summary>
/// Caso de uso para validación y conciliación de comprobantes de pago web (RF-WEB-002, RF-INV-003).
/// Si es aprobado con modalidad Retiro en Tienda, emite automáticamente la Guía Interna de Traslado
/// y liquida el stock comprometido en el Almacén Central.
/// </summary>
public class ValidarVoucherPagoCommandHandler
{
    private static readonly HashSet<string> FormatosPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private static readonly HashSet<string> MimeTypesPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "application/pdf"
    };

    private readonly IAuthorizationService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWebOrderRepository _webOrderRepository;
    private readonly IVoucherStoragePort _voucherStoragePort;
    private readonly ITransferGuidePort _transferGuidePort;
    private readonly IInventoryStockPort _inventoryStockPort;
    private readonly IAuditLogPort _auditLogPort;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKardexPort _kardexPort;

    public ValidarVoucherPagoCommandHandler(
        IAuthorizationService authService,
        ICurrentUserContext currentUserContext,
        IWebOrderRepository webOrderRepository,
        IVoucherStoragePort voucherStoragePort,
        ITransferGuidePort transferGuidePort,
        IInventoryStockPort inventoryStockPort,
        IAuditLogPort auditLogPort,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IKardexPort kardexPort)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _webOrderRepository = webOrderRepository ?? throw new ArgumentNullException(nameof(webOrderRepository));
        _voucherStoragePort = voucherStoragePort ?? throw new ArgumentNullException(nameof(voucherStoragePort));
        _transferGuidePort = transferGuidePort ?? throw new ArgumentNullException(nameof(transferGuidePort));
        _inventoryStockPort = inventoryStockPort ?? throw new ArgumentNullException(nameof(inventoryStockPort));
        _auditLogPort = auditLogPort ?? throw new ArgumentNullException(nameof(auditLogPort));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _kardexPort = kardexPort ?? throw new ArgumentNullException(nameof(kardexPort));
    }

    public async Task<VoucherValidacionResultDto> Handle(ValidarVoucherPagoCommand command, CancellationToken cancellationToken = default)
    {
        _authService.ExigirUnoDeRoles("Tesoreria", "Superadmin");

        await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var pedido = await _webOrderRepository.ObtenerParaBloqueoAsync(command.PedidoWebId, cancellationToken);
            if (pedido == null)
                throw new NotFoundException("PedidoWeb", command.PedidoWebId);

            // Rechaza validación manual en pasarelas de tarjeta
            if (pedido.MetodoPago.Equals("PasarelaTarjeta", StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Las órdenes con pasarela de tarjeta no admiten validación manual de voucher.");

            // Admite estados válidos para validación de comprobante (RF-WEB-002)
            if (!pedido.Estado.Equals("PendientePago", StringComparison.OrdinalIgnoreCase) &&
                !pedido.Estado.Equals("PendienteValidacion", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException($"El pedido {command.PedidoWebId} está en estado '{pedido.Estado}' y no admite validación de comprobante.");
            }

            var voucher = await _voucherStoragePort.ObtenerMetadatosVoucherAsync(command.PedidoWebId, cancellationToken);
            if (voucher == null)
                throw new ValidationException("No se encontró ningún voucher registrado para este pedido.");

            if (voucher.TamanoBytes <= 0)
                throw new ValidationException("El archivo del voucher no puede estar vacío.");

            if (voucher.TamanoBytes > 5 * 1024 * 1024)
                throw new ValidationException("El archivo del voucher excede el tamaño máximo permitido de 5 MB.");

            string ext = Path.GetExtension(voucher.NombreArchivo).ToLowerInvariant();
            if (!FormatosPermitidos.Contains(ext))
                throw new ValidationException($"El formato '{ext}' no es permitido. Solo se admiten archivos JPG, PNG o PDF.");

            if (!string.IsNullOrWhiteSpace(voucher.ContentType) && !MimeTypesPermitidos.Contains(voucher.ContentType))
                throw new ValidationException($"El tipo MIME '{voucher.ContentType}' no es válido para el voucher.");

            // Ventana máxima de subida de 60 minutos desde la creación del pedido (RF-WEB-002)
            if (voucher.FechaSubidaUtc > pedido.FechaCreacionUtc.AddMinutes(60))
                throw new ValidationException("El voucher fue cargado fuera de la ventana permitida de 60 minutos desde la creación del pedido.");

            // TTL total de hasta 240 minutos
            DateTime nowUtc = _dateTimeProvider.UtcNow;
            if (nowUtc > pedido.FechaExpiracionUtc || nowUtc > pedido.FechaCreacionUtc.AddMinutes(240))
                throw new ValidationException("La reserva del pedido superó el tiempo máximo de 240 minutos y se encuentra expirada.");

            // Havender ordering y bloqueo sobre Almacén Central
            var productIds = pedido.Items.Select(i => i.ProductoId).Distinct().OrderBy(id => id).ToList();
            var stocks = await _inventoryStockPort.ObtenerParaBloqueoOrdenadosAsync(pedido.AlmacenOrigenId, productIds, cancellationToken);
            var stockDict = stocks.ToDictionary(s => s.ProductoId);

            string? guiaCodigo = null;
            string estadoFinal;

            if (command.Aprobado)
            {
                foreach (var item in pedido.Items)
                {
                    if (!stockDict.TryGetValue(item.ProductoId, out var stockAlm))
                        throw new NotFoundException("StockAlmacen", item.ProductoId);

                    decimal cantidadBase = item.CantidadBaseEfectiva;
                    stockAlm.RegistrarSalidaFisica(cantidadBase, esDesdeReserva: true);
                    await _inventoryStockPort.ActualizarStockAlmacenAsync(stockAlm, cancellationToken);

                    await _kardexPort.RegistrarMovimientoSalidaAsync(
                        item.ProductoId, pedido.AlmacenOrigenId, cantidadBase, 0m,
                        "VENTA_WEB", pedido.Id.ToString(), nowUtc, cancellationToken);
                }

                // Invariante 5 (RF-INV-003): Si es Retiro en Tienda física, emitir Guía Interna de Traslado
                if (pedido.ModalidadEntrega.Equals("RetiroEnTienda", StringComparison.OrdinalIgnoreCase))
                {
                    var itemsGuia = pedido.Items.Select(i => (i.ProductoId, i.CantidadBaseEfectiva)).ToList();
                    guiaCodigo = await _transferGuidePort.GenerarGuiaTrasladoAsync(
                        pedido.Id,
                        pedido.AlmacenOrigenId,
                        pedido.TiendaDestinoId ?? Guid.Empty,
                        itemsGuia,
                        cancellationToken);
                }

                estadoFinal = "PagadoConfirmado";
            }
            else
            {
                foreach (var item in pedido.Items)
                {
                    if (!stockDict.TryGetValue(item.ProductoId, out var stockAlm))
                        throw new NotFoundException("StockAlmacen", item.ProductoId);

                    decimal cantidadBase = item.CantidadBaseEfectiva;
                    stockAlm.LiberarReserva(cantidadBase);
                    await _inventoryStockPort.ActualizarStockAlmacenAsync(stockAlm, cancellationToken);
                }

                estadoFinal = "Rechazado";
            }

            bool transitionOk = await _webOrderRepository.ActualizarEstadoConcurrenteAsync(pedido.Id, pedido.Estado, estadoFinal, cancellationToken);
            if (!transitionOk)
            {
                throw new ValidationException("El pedido web cambió de estado concurrentemente.");
            }

            await _auditLogPort.RegistrarAccionAsync(new AuditLogEntry(
                _currentUserContext.UserId,
                command.Aprobado ? "VOUCHER_APROBADO" : "VOUCHER_RECHAZADO",
                "PedidoWeb",
                pedido.Id.ToString(),
                $"Estado: {estadoFinal}, Guia: {guiaCodigo ?? "N/A"}, Motivo: {command.MotivoRechazo ?? "Aprobado"}",
                _currentUserContext.ClientIp,
                _currentUserContext.TerminalScope,
                nowUtc), cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new VoucherValidacionResultDto(pedido.Id, command.Aprobado, estadoFinal, guiaCodigo);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
