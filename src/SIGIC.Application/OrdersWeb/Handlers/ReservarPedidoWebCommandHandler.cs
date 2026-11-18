using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.OrdersWeb.Handlers;

/// <summary>
/// Caso de uso para reservar pedidos en el canal web. Implementa el orden Havender anti-deadlock,
/// exclusión web estricta (404), reserva exclusiva en Almacén Central, validación de tarifas y cálculo de TTL dinámico
/// (RF-WEB-001, RF-WEB-002, RF-WEB-005, RF-INV-001, RF-INV-003, RNF-CON-003).
/// Invariante RF-WEB-005 / Havender en PostgreSQL 16:
/// SELECT producto_id FROM stock_almacen WHERE almacen_id = @AlmacenCentralId ORDER BY producto_id ASC FOR UPDATE;
/// UPDATE stock_almacen SET stock_comprometido = stock_comprometido + @Cantidad WHERE (stock_fisico - stock_comprometido) >= @Cantidad;
/// </summary>
public class ReservarPedidoWebCommandHandler
{
    private static readonly HashSet<string> MetodosValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasarelaTarjeta", "Yape", "Plin", "TransferenciaBancaria"
    };

    private static readonly HashSet<string> ModalidadesValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "DespachoDomicilio", "RetiroEnTienda", "RetiroEnAlmacenCentral"
    };

    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInventoryStockPort _inventoryStockPort;
    private readonly IProductQueryPort _productQueryPort;
    private readonly ITariffRepository _tariffRepository;
    private readonly ICustomerProfilePort _customerProfilePort;
    private readonly IWarehouseConfigurationPort _warehouseConfigPort;
    private readonly IWebOrderRepository _webOrderRepository;
    private readonly IAuditLogPort _auditLogPort;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ReservarPedidoWebCommandHandler(
        ICurrentUserContext currentUserContext,
        IInventoryStockPort inventoryStockPort,
        IProductQueryPort productQueryPort,
        ITariffRepository tariffRepository,
        ICustomerProfilePort customerProfilePort,
        IWarehouseConfigurationPort warehouseConfigPort,
        IWebOrderRepository webOrderRepository,
        IAuditLogPort auditLogPort,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _inventoryStockPort = inventoryStockPort ?? throw new ArgumentNullException(nameof(inventoryStockPort));
        _productQueryPort = productQueryPort ?? throw new ArgumentNullException(nameof(productQueryPort));
        _tariffRepository = tariffRepository ?? throw new ArgumentNullException(nameof(tariffRepository));
        _customerProfilePort = customerProfilePort ?? throw new ArgumentNullException(nameof(customerProfilePort));
        _warehouseConfigPort = warehouseConfigPort ?? throw new ArgumentNullException(nameof(warehouseConfigPort));
        _webOrderRepository = webOrderRepository ?? throw new ArgumentNullException(nameof(webOrderRepository));
        _auditLogPort = auditLogPort ?? throw new ArgumentNullException(nameof(auditLogPort));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ReservarPedidoWebResultDto> Handle(ReservarPedidoWebCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Items == null || !command.Items.Any())
            throw new ValidationException("El pedido web debe contener al menos un ítem.");

        if (command.Items.Any(i => i.Cantidad <= 0))
            throw new ValidationException("La cantidad solicitada para cada ítem debe ser mayor a cero.");

        if (!MetodosValidos.Contains(command.MetodoPago))
            throw new ValidationException($"Método de pago no reconocido para canal web: '{command.MetodoPago}'.");

        if (!ModalidadesValidas.Contains(command.ModalidadEntrega))
            throw new ValidationException($"Modalidad de entrega no reconocida: '{command.ModalidadEntrega}'.");

        if (command.ModalidadEntrega.Equals("RetiroEnTienda", StringComparison.OrdinalIgnoreCase))
        {
            if (!command.TiendaDestinoId.HasValue || command.TiendaDestinoId.Value == Guid.Empty)
                throw new ValidationException("Para retiro en tienda se exige indicar la tienda física de destino (RF-INV-003).");
        }

        Guid almacenCentralId = _warehouseConfigPort.ObtenerAlmacenCentralId();
        if (almacenCentralId == Guid.Empty)
            throw new InvalidOperationException("No se encuentra configurado el Almacén Central para pedidos web.");

        return await _unitOfWork.ExecuteInTransactionWithRetryAsync(async ct =>
        {
            // Invariante 3 (RF-WEB-005): Ordenamiento Havender en memoria por ProductoId ASC antes del bloqueo
            var itemsOrdenados = command.Items.OrderBy(i => i.ProductoId).ToList();
            var productIds = itemsOrdenados.Select(i => i.ProductoId).Distinct().ToList();

            var productos = await _productQueryPort.ObtenerMultiplesParaPDPAsync(productIds, ct);
            var prodDict = productos.ToDictionary(p => p.Id);

            var stocks = await _inventoryStockPort.ObtenerParaBloqueoOrdenadosAsync(almacenCentralId, productIds, ct);
            var stockDict = stocks.ToDictionary(s => s.ProductoId);

            string rolCliente = await _customerProfilePort.ObtenerPerfilClienteAsync(command.ClienteId, ct);
            decimal descRol = await _tariffRepository.ObtenerPorcentajeDescuentoRolAsync(rolCliente, ct);

            decimal total = 0m;
            var reservas = new List<(StockAlmacen Stock, decimal CantidadBase)>();
            var itemsSnapshot = new List<PedidoWebItemDto>();

            foreach (var item in itemsOrdenados)
            {
                if (!prodDict.TryGetValue(item.ProductoId, out var prod))
                    throw new NotFoundException("Producto", item.ProductoId);

                // Invariante 2 (RF-INV-001): Segregación Web -> HTTP 404 absoluto
                if (!prod.DisponibleEnWeb)
                    throw new NotFoundException("Producto", item.ProductoId);

                if (!stockDict.TryGetValue(item.ProductoId, out var stockCentral))
                    throw new NotFoundException("StockAlmacen", item.ProductoId);

                decimal factor = await _productQueryPort.ObtenerFactorConversionAsync(item.UnidadMedida, prod.UnidadBase, ct);
                if (factor <= 0)
                    throw new InvalidOperationException($"Factor de conversión inválido para {item.UnidadMedida}.");

                decimal cantidadBase = Math.Round(item.Cantidad * factor, 4, MidpointRounding.ToEven);

                // Revalidación de tarifa comercial
                decimal descVolumen = await _tariffRepository.ObtenerPorcentajeDescuentoVolumenAsync(cantidadBase, ct);
                decimal descTotal = descRol + descVolumen;

                decimal precioBaseComercial = Math.Round(prod.PrecioVenta * factor, 2, MidpointRounding.ToEven);
                decimal descComercial = Math.Round(precioBaseComercial * descTotal, 2, MidpointRounding.ToEven);
                decimal precioEsperado = precioBaseComercial - descComercial;

                if (item.PrecioUnitario != precioEsperado)
                    throw new ValidationException($"El precio unitario ({item.PrecioUnitario:F2}) no coincide con la tarifa vigente ({precioEsperado:F2}) para el producto {prod.CodigoSku}.");

                decimal precioBaseUnitarioVenta = factor > 0 ? (item.PrecioUnitario / factor) : item.PrecioUnitario;
                if (!prod.ValidarMargenSeguridad(precioBaseUnitarioVenta))
                    throw new MargenInvalidoException(item.PrecioUnitario, prod.CostoUnitario);

                // Invariante 1 (RNF-CON-003): Stock Disponible en Almacén Central = Físico - Comprometido
                if (stockCentral.StockDisponible < cantidadBase)
                    throw new StockInsuficienteException(prod.Id, prod.CodigoSku, cantidadBase, stockCentral.StockDisponible);

                stockCentral.ReservarStock(cantidadBase);
                reservas.Add((stockCentral, cantidadBase));
                itemsSnapshot.Add(new PedidoWebItemDto(item.ProductoId, item.Cantidad, precioEsperado, item.UnidadMedida, factor, cantidadBase));

                total += Math.Round(item.Cantidad * item.PrecioUnitario, 2, MidpointRounding.ToEven);
            }

            DateTime now = _dateTimeProvider.UtcNow;
            DateTime fechaExpiracion;
            DateTime? fechaLimiteVoucher;

            // Invariante 4 (RF-WEB-002): TTL 20 min pasarela / 240 min manual con 60 min voucher
            if (command.MetodoPago.Equals("PasarelaTarjeta", StringComparison.OrdinalIgnoreCase))
            {
                fechaExpiracion = now.AddMinutes(20);
                fechaLimiteVoucher = null;
            }
            else
            {
                fechaExpiracion = now.AddMinutes(240);
                fechaLimiteVoucher = now.AddMinutes(60);
            }

            var pedidoId = Guid.NewGuid();
            var pedido = new PedidoWebDto(
                Id: pedidoId,
                ClienteId: command.ClienteId,
                MetodoPago: command.MetodoPago,
                ModalidadEntrega: command.ModalidadEntrega,
                TiendaDestinoId: command.TiendaDestinoId,
                AlmacenOrigenId: almacenCentralId,
                FechaCreacionUtc: now,
                FechaExpiracionUtc: fechaExpiracion,
                FechaLimiteVoucherUtc: fechaLimiteVoucher,
                FechaSubidaVoucherUtc: null,
                Estado: "PendientePago",
                Items: itemsSnapshot);

            await _webOrderRepository.GuardarPedidoAsync(pedido, ct);

            foreach (var (stockCentral, _) in reservas)
            {
                await _inventoryStockPort.ActualizarStockAlmacenAsync(stockCentral, ct);
            }

            await _auditLogPort.RegistrarAccionAsync(new AuditLogEntry(
                _currentUserContext.UserId,
                "RESERVA_PEDIDO_WEB",
                "PedidoWeb",
                pedidoId.ToString(),
                $"Metodo: {command.MetodoPago}, Modalidad: {command.ModalidadEntrega}, Total: {total:F2}, Almacen: {almacenCentralId}",
                _currentUserContext.ClientIp,
                _currentUserContext.TerminalScope,
                now), ct);

            await _unitOfWork.CommitAsync(ct);

            return new ReservarPedidoWebResultDto(
                PedidoId: pedidoId,
                FechaCreacionUtc: now,
                FechaExpiracionUtc: fechaExpiracion,
                FechaLimiteVoucherUtc: fechaLimiteVoucher,
                Total: total,
                Estado: "PendientePago");
        }, cancellationToken);
    }
}
