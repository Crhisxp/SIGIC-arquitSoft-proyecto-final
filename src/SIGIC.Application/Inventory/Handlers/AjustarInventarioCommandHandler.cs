using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Inventory.Commands;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;

namespace SIGIC.Application.Inventory.Handlers;

/// <summary>
/// Caso de uso para registrar ajustes de inventario extraordinarios autorizados por Superadmin (RF-INV-007, RNF-CON-003).
/// Invariante: Registra movimientos compensatorios en Kardex sin mutar el historial previo ni violar el stock comprometido.
/// </summary>
public class AjustarInventarioCommandHandler
{
    private readonly IAuthorizationService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInventoryAdjustmentRepository _adjustmentRepository;
    private readonly IInventoryStockPort _inventoryStockPort;
    private readonly IProductQueryPort _productQueryPort;
    private readonly IAuditLogPort _auditLogPort;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AjustarInventarioCommandHandler(
        IAuthorizationService authService,
        ICurrentUserContext currentUserContext,
        IInventoryAdjustmentRepository adjustmentRepository,
        IInventoryStockPort inventoryStockPort,
        IProductQueryPort productQueryPort,
        IAuditLogPort auditLogPort,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _adjustmentRepository = adjustmentRepository ?? throw new ArgumentNullException(nameof(adjustmentRepository));
        _inventoryStockPort = inventoryStockPort ?? throw new ArgumentNullException(nameof(inventoryStockPort));
        _productQueryPort = productQueryPort ?? throw new ArgumentNullException(nameof(productQueryPort));
        _auditLogPort = auditLogPort ?? throw new ArgumentNullException(nameof(auditLogPort));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AjustarInventarioResultDto> Handle(AjustarInventarioCommand command, CancellationToken cancellationToken = default)
    {
        _authService.ExigirRol("Superadmin");

        if (command.ProductoId == Guid.Empty)
            throw new ValidationException("El ID de producto es obligatorio.");

        if (command.AlmacenId == Guid.Empty)
            throw new ValidationException("El ID de almacén es obligatorio para el ajuste físico (RF-INV-007).");

        if (string.IsNullOrWhiteSpace(command.Motivo))
            throw new ValidationException("El motivo del ajuste de inventario es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.CodigoAutorizacionSuperadmin))
            throw new ValidationException("El código de autorización de Superadmin es obligatorio.");

        var producto = await _productQueryPort.ObtenerParaPDPAsync(command.ProductoId, cancellationToken);
        if (producto == null)
            throw new NotFoundException("Producto", command.ProductoId);

        await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var stocks = await _inventoryStockPort.ObtenerParaBloqueoOrdenadosAsync(command.AlmacenId, new[] { command.ProductoId }, cancellationToken);
            var stockAlmacen = stocks.FirstOrDefault();
            if (stockAlmacen == null)
                throw new NotFoundException("StockAlmacen", command.ProductoId);

            decimal stockFisicoAnterior = stockAlmacen.StockFisico;

            // Invariante 1 (RNF-CON-003): Stock físico resultante no puede ser menor al stock comprometido
            stockAlmacen.AplicarAjusteFisico(command.CantidadAjuste);

            DateTime now = _dateTimeProvider.UtcNow;

            var ajusteId = await _adjustmentRepository.RegistrarAjusteKardexCompensatorioAsync(
                stockAlmacen.ProductoId,
                command.AlmacenId,
                command.CantidadAjuste,
                producto.CostoUnitario,
                command.Motivo,
                command.CodigoAutorizacionSuperadmin,
                now,
                cancellationToken);

            await _inventoryStockPort.ActualizarStockAlmacenAsync(stockAlmacen, cancellationToken);

            await _auditLogPort.RegistrarAccionAsync(new AuditLogEntry(
                _currentUserContext.UserId,
                "AJUSTE_INVENTARIO",
                "StockAlmacen",
                stockAlmacen.Id.ToString(),
                $"Almacen: {command.AlmacenId}, Delta: {command.CantidadAjuste}, Anterior: {stockFisicoAnterior}, Nuevo: {stockAlmacen.StockFisico}, Comprometido: {stockAlmacen.StockComprometido}, Motivo: {command.Motivo}",
                _currentUserContext.ClientIp,
                _currentUserContext.TerminalScope,
                now), cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new AjustarInventarioResultDto(
                AjusteId: ajusteId,
                ProductoId: stockAlmacen.ProductoId,
                StockFisicoAnterior: stockFisicoAnterior,
                CantidadAjuste: command.CantidadAjuste,
                StockFisicoNuevo: stockAlmacen.StockFisico,
                StockComprometido: stockAlmacen.StockComprometido,
                FechaUtc: now);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
