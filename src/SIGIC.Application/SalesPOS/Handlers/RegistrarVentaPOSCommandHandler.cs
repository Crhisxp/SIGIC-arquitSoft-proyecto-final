using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.SalesPOS.Commands;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.SalesPOS.Handlers;

/// <summary>
/// Caso de uso que procesa la venta en mostrador POS garantizando el algoritmo Havender anti-deadlock,
/// revalidación atómica de tarifas por rol/volumen, deducción multi-almacén en unidad base, pagos mixtos
/// con unicidad digital y auditoría append-only (RF-POS-001, RF-POS-006, RF-PRE-001, RF-PRE-002, RF-INV-002, RF-WEB-005).
/// Invariante RF-WEB-005 / Havender en PostgreSQL 16:
/// SELECT producto_id FROM stock_almacen WHERE almacen_id = @AlmacenTiendaId ORDER BY producto_id ASC FOR UPDATE;
/// UPDATE stock_almacen SET stock_fisico = stock_fisico - @Cantidad WHERE (stock_fisico - stock_comprometido) >= @Cantidad;
/// </summary>
public class RegistrarVentaPOSCommandHandler
{
    private static readonly HashSet<string> MetodosValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Efectivo", "Yape", "Plin", "Tarjeta", "Transferencia"
    };

    private readonly IAuthorizationService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICashShiftPort _cashShiftPort;
    private readonly IInventoryStockPort _inventoryStockPort;
    private readonly IProductQueryPort _productQueryPort;
    private readonly ITariffRepository _tariffRepository;
    private readonly ICustomerProfilePort _customerProfilePort;
    private readonly IDigitalPaymentVerificationPort _digitalPaymentPort;
    private readonly ISalesPOSRepository _salesPOSRepository;
    private readonly IAuditLogPort _auditLogPort;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IKardexPort _kardexPort;

    public RegistrarVentaPOSCommandHandler(
        IAuthorizationService authService,
        ICurrentUserContext currentUserContext,
        ICashShiftPort cashShiftPort,
        IInventoryStockPort inventoryStockPort,
        IProductQueryPort productQueryPort,
        ITariffRepository tariffRepository,
        ICustomerProfilePort customerProfilePort,
        IDigitalPaymentVerificationPort digitalPaymentPort,
        ISalesPOSRepository salesPOSRepository,
        IAuditLogPort auditLogPort,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IKardexPort kardexPort)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _cashShiftPort = cashShiftPort ?? throw new ArgumentNullException(nameof(cashShiftPort));
        _inventoryStockPort = inventoryStockPort ?? throw new ArgumentNullException(nameof(inventoryStockPort));
        _productQueryPort = productQueryPort ?? throw new ArgumentNullException(nameof(productQueryPort));
        _tariffRepository = tariffRepository ?? throw new ArgumentNullException(nameof(tariffRepository));
        _customerProfilePort = customerProfilePort ?? throw new ArgumentNullException(nameof(customerProfilePort));
        _digitalPaymentPort = digitalPaymentPort ?? throw new ArgumentNullException(nameof(digitalPaymentPort));
        _salesPOSRepository = salesPOSRepository ?? throw new ArgumentNullException(nameof(salesPOSRepository));
        _auditLogPort = auditLogPort ?? throw new ArgumentNullException(nameof(auditLogPort));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _kardexPort = kardexPort ?? throw new ArgumentNullException(nameof(kardexPort));
    }

    public async Task<VentaPOSResultDto> Handle(RegistrarVentaPOSCommand command, CancellationToken cancellationToken = default)
    {
        _authService.ExigirRol("Cajero");

        bool turnoValido = await _cashShiftPort.EsTurnoValidoYAperturadoAsync(command.TurnoCajaId, _currentUserContext.UserId, cancellationToken);
        if (!turnoValido)
            throw new ValidationException("El turno de caja especificado no existe o no se encuentra en estado Aperturada.");

        Guid almacenTiendaId = await _cashShiftPort.ObtenerAlmacenTiendaIdAsync(command.TurnoCajaId, cancellationToken);
        if (almacenTiendaId == Guid.Empty)
            throw new ValidationException("El turno de caja no tiene asignado un almacén físico de tienda válido.");

        if (command.Items == null || !command.Items.Any())
            throw new ValidationException("La venta debe contener al menos un ítem.");

        if (command.Pagos == null || !command.Pagos.Any())
            throw new ValidationException("La venta debe registrar al menos un método de pago.");

        if (command.Items.Any(i => i.Cantidad <= 0))
            throw new ValidationException("La cantidad de cada ítem debe ser estrictamente mayor a cero.");

        return await _unitOfWork.ExecuteInTransactionWithRetryAsync(async ct =>
        {
            // Coordinación atómica de turno: verificar y bloquear turno contra cierres concurrentes
            bool turnoBloqueado = await _cashShiftPort.ValidarYBloquearTurnoParaVentaAsync(command.TurnoCajaId, _currentUserContext.UserId, ct);
            if (!turnoBloqueado)
                throw new ValidationException("El turno de caja especificado no existe, no está aperturado o fue cerrado concurrentemente.");

            // Invariante 3 (RF-WEB-005): Algoritmo Havender anti-deadlock -> Ordenar por ProductoId ASC en memoria
            var itemsOrdenados = command.Items.OrderBy(i => i.ProductoId).ToList();
            var productIdsOrdenados = itemsOrdenados.Select(i => i.ProductoId).Distinct().ToList();

            var productos = await _productQueryPort.ObtenerMultiplesParaPDPAsync(productIdsOrdenados, ct);
            var prodDict = productos.ToDictionary(p => p.Id);

            var stocks = await _inventoryStockPort.ObtenerParaBloqueoOrdenadosAsync(almacenTiendaId, productIdsOrdenados, ct);
            var stockDict = stocks.ToDictionary(s => s.ProductoId);

            string rolCliente = await _customerProfilePort.ObtenerPerfilClienteAsync(command.ClienteId, ct);
            decimal descRol = await _tariffRepository.ObtenerPorcentajeDescuentoRolAsync(rolCliente, ct);

            decimal totalVenta = 0m;
            var deduccionesStock = new List<(StockAlmacen Stock, decimal CantidadBase)>();

            foreach (var item in itemsOrdenados)
            {
                if (!prodDict.TryGetValue(item.ProductoId, out var producto))
                    throw new NotFoundException("Producto", item.ProductoId);

                if (!stockDict.TryGetValue(item.ProductoId, out var stockAlm))
                    throw new NotFoundException("StockAlmacen", item.ProductoId);

                decimal factor = await _productQueryPort.ObtenerFactorConversionAsync(item.UnidadMedida, producto.UnidadBase, ct);
                if (factor <= 0)
                    throw new InvalidOperationException($"Factor de conversión inválido para unidad {item.UnidadMedida}.");

                decimal cantidadBase = Math.Round(item.Cantidad * factor, 4, MidpointRounding.ToEven);

                // Revalidación de tarifa y descuento autoritativo dentro de transacción
                decimal descVolumen = await _tariffRepository.ObtenerPorcentajeDescuentoVolumenAsync(cantidadBase, ct);
                decimal descTotal = descRol + descVolumen;

                decimal precioBaseComercial = Math.Round(producto.PrecioVenta * factor, 2, MidpointRounding.ToEven);
                decimal descComercial = Math.Round(precioBaseComercial * descTotal, 2, MidpointRounding.ToEven);
                decimal precioEsperado = precioBaseComercial - descComercial;

                if (item.PrecioUnitario != precioEsperado)
                    throw new ValidationException($"El precio unitario ({item.PrecioUnitario:F2}) no coincide con la tarifa vigente ({precioEsperado:F2}) para el producto {producto.CodigoSku}.");

                decimal precioBaseUnitarioVenta = factor > 0 ? (item.PrecioUnitario / factor) : item.PrecioUnitario;
                if (!producto.ValidarMargenSeguridad(precioBaseUnitarioVenta))
                    throw new MargenInvalidoException(item.PrecioUnitario, producto.CostoUnitario);

                // Invariante 1 (RNF-CON-003): Stock Disponible = Físico - Comprometido en Almacén Tienda
                if (stockAlm.StockDisponible < cantidadBase)
                    throw new StockInsuficienteException(producto.Id, producto.CodigoSku, cantidadBase, stockAlm.StockDisponible);

                stockAlm.RegistrarSalidaFisica(cantidadBase, esDesdeReserva: false);
                deduccionesStock.Add((stockAlm, cantidadBase));

                totalVenta += Math.Round(item.Cantidad * item.PrecioUnitario, 2, MidpointRounding.ToEven);
            }

            // Validación de pagos mixtos y unicidad digital (RF-POS-006)
            decimal totalPagado = 0m;
            var codigosDigitalesEnRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime fechaUtc = _dateTimeProvider.UtcNow;
            DateOnly fechaHoy = DateOnly.FromDateTime(fechaUtc);

            foreach (var pago in command.Pagos)
            {
                if (!MetodosValidos.Contains(pago.MetodoPago))
                    throw new ValidationException($"Método de pago no reconocido: '{pago.MetodoPago}'.");

                if (pago.Monto <= 0)
                    throw new ValidationException("El monto de cada pago debe ser estrictamente positivo.");

                totalPagado += pago.Monto;

                if (!pago.MetodoPago.Equals("Efectivo", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(pago.CodigoOperacionDigital))
                        throw new ValidationException($"El pago digital mediante {pago.MetodoPago} exige un código de operación no vacío.");

                    string codigoLimpio = pago.CodigoOperacionDigital.Trim();
                    if (!codigosDigitalesEnRequest.Add(codigoLimpio))
                        throw new ValidationException($"Código de operación digital duplicado en la solicitud: '{codigoLimpio}'.");

                    bool yaExiste = await _digitalPaymentPort.ExisteCodigoOperacionAsync(pago.MetodoPago, codigoLimpio, fechaHoy, ct);
                    if (yaExiste)
                        throw new ValidationException($"El código de operación digital '{codigoLimpio}' ya fue utilizado previamente para {pago.MetodoPago} en la fecha actual (RF-POS-006).");
                }
            }

            if (Math.Abs(totalPagado - totalVenta) > 0.001m)
                throw new ValidationException($"El total pagado ({totalPagado:F2}) no coincide con el total de la venta ({totalVenta:F2}).");

            var result = await _salesPOSRepository.RegistrarVentaAsync(command, totalVenta, fechaUtc, ct);

            foreach (var (stockAlm, _) in deduccionesStock)
            {
                await _inventoryStockPort.ActualizarStockAlmacenAsync(stockAlm, ct);
            }

            foreach (var item in itemsOrdenados)
            {
                var prod = prodDict[item.ProductoId];
                decimal factor = await _productQueryPort.ObtenerFactorConversionAsync(item.UnidadMedida, prod.UnidadBase, ct);
                decimal cantidadBase = Math.Round(item.Cantidad * factor, 4, MidpointRounding.ToEven);
                await _kardexPort.RegistrarMovimientoSalidaAsync(
                    item.ProductoId, almacenTiendaId, cantidadBase, prod.CostoUnitario,
                    "VENTA_POS", result.NumeroComprobante, fechaUtc, ct);
            }

            foreach (var pago in command.Pagos.Where(p => !p.MetodoPago.Equals("Efectivo", StringComparison.OrdinalIgnoreCase)))
            {
                await _digitalPaymentPort.RegistrarCodigoOperacionAsync(pago.MetodoPago, pago.CodigoOperacionDigital!.Trim(), fechaHoy, result.VentaId, ct);
            }

            // Auditoría Append-Only (RNF-SEG-004)
            await _auditLogPort.RegistrarAccionAsync(new AuditLogEntry(
                _currentUserContext.UserId,
                "REGISTRO_VENTA_POS",
                "VentaPOS",
                result.VentaId.ToString(),
                $"Comprobante: {result.NumeroComprobante}, Total: {totalVenta:F2}, Almacen: {almacenTiendaId}",
                _currentUserContext.ClientIp,
                _currentUserContext.TerminalScope,
                fechaUtc), ct);

            await _unitOfWork.CommitAsync(ct);

            return result;
        }, cancellationToken);
    }
}
