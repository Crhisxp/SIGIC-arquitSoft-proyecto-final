using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Exceptions;

namespace SIGIC.Application.ProductDetailsPricing.Services;

/// <summary>
/// Caso de uso que calcula el detalle del producto (PDP), mediciones y precios dinámicos por rol y volumen.
/// Cumple estrictamente con RF-PRE-001, RF-PRE-002, RF-INV-005, RNF-CON-003, RF-WEB-001 y RF-INV-003.
/// </summary>
public class CalcularPDPYDescuentoQueryHandler
{
    private readonly IProductQueryPort _productQueryPort;
    private readonly ITariffRepository _tariffRepository;
    private readonly ICustomerProfilePort _customerProfilePort;
    private readonly IInventoryStockPort _inventoryStockPort;
    private readonly IWarehouseConfigurationPort _warehouseConfigPort;
    private readonly ICurrentUserContext _currentUserContext;

    public CalcularPDPYDescuentoQueryHandler(
        IProductQueryPort productQueryPort,
        ITariffRepository tariffRepository,
        ICustomerProfilePort? customerProfilePort = null,
        IInventoryStockPort? inventoryStockPort = null,
        IWarehouseConfigurationPort? warehouseConfigPort = null,
        ICurrentUserContext? currentUserContext = null)
    {
        _productQueryPort = productQueryPort ?? throw new ArgumentNullException(nameof(productQueryPort));
        _tariffRepository = tariffRepository ?? throw new ArgumentNullException(nameof(tariffRepository));
        _customerProfilePort = customerProfilePort ?? new DefaultCustomerProfilePort();
        _inventoryStockPort = inventoryStockPort ?? new DefaultInventoryStockPort();
        _warehouseConfigPort = warehouseConfigPort ?? new DefaultWarehouseConfigurationPort();
        _currentUserContext = currentUserContext ?? new DefaultCurrentUserContext();
    }

    public async Task<PDPResultDto> Handle(CalcularPDPYDescuentoQuery query, CancellationToken cancellationToken = default)
    {
        var producto = await _productQueryPort.ObtenerParaPDPAsync(query.ProductoId, cancellationToken);
        if (producto == null)
            throw new NotFoundException("Producto", query.ProductoId);

        // Invariante 2 (RF-INV-001): Segregación Web estricta -> 404 absoluto si no disponible en web
        if (query.EsCanalWeb && !producto.DisponibleEnWeb)
            throw new NotFoundException("Producto", query.ProductoId);

        // Conversión a unidad base con 4 decimales y redondeo bancario (RF-INV-005)
        decimal factor = await _productQueryPort.ObtenerFactorConversionAsync(query.UnidadComercial, producto.UnidadBase, cancellationToken);
        if (factor <= 0)
            throw new InvalidOperationException("El factor de conversión debe ser estrictamente positivo.");

        decimal cantidadBase = Math.Round(query.Cantidad * factor, 4, MidpointRounding.ToEven);

        // Resolución de perfil de cliente seguro contra manipulación (RF-PRE-002)
        string rol = "Público General";
        if (query.ClienteId.HasValue && query.ClienteId.Value != Guid.Empty)
        {
            // Verificación de autorización: sólo el propio usuario o roles administrativos autorizados pueden consultar perfiles de terceros
            bool esMismoUsuario = _currentUserContext.UserId == query.ClienteId.Value;
            bool esStaffAutorizado = _currentUserContext.Roles.Any(r => r.Equals("Superadmin", StringComparison.OrdinalIgnoreCase) ||
                                                                        r.Equals("Backoffice", StringComparison.OrdinalIgnoreCase) ||
                                                                        r.Equals("Cajero", StringComparison.OrdinalIgnoreCase) ||
                                                                        r.Equals("Ventas", StringComparison.OrdinalIgnoreCase));
            if (!esMismoUsuario && !esStaffAutorizado)
            {
                throw new UnauthorizedException("Cliente", "No autorizado para consultar tarifas o perfil de una identidad ajena.");
            }

            rol = await _customerProfilePort.ObtenerPerfilClienteAsync(query.ClienteId.Value, cancellationToken);
        }

        // Descuentos por rol y volumen (RF-PRE-001)
        decimal descRol = await _tariffRepository.ObtenerPorcentajeDescuentoRolAsync(rol, cancellationToken);
        decimal descVolumen = await _tariffRepository.ObtenerPorcentajeDescuentoVolumenAsync(cantidadBase, cancellationToken);
        decimal descTotalPorcentaje = descRol + descVolumen;

        decimal precioBaseUnitario = producto.PrecioVenta;
        decimal precioUnitarioComercial = Math.Round(precioBaseUnitario * factor, 2, MidpointRounding.ToEven);
        decimal descuentoUnitario = Math.Round(precioUnitarioComercial * descTotalPorcentaje, 2, MidpointRounding.ToEven);
        decimal precioFinalUnitario = precioUnitarioComercial - descuentoUnitario;

        // Invariante RF-PRE-002: Protección de margen comercial referenciado a la unidad base
        decimal precioFinalBase = factor > 0 ? (precioFinalUnitario / factor) : precioFinalUnitario;
        if (!producto.ValidarMargenSeguridad(precioFinalBase))
            throw new MargenInvalidoException(precioFinalUnitario, producto.CostoUnitario);

        decimal subtotal = Math.Round(precioFinalUnitario * query.Cantidad, 2, MidpointRounding.ToEven);

        // Invariante 1 (RNF-CON-003): Stock Disponible = Físico - Comprometido desde StockAlmacen
        Guid almacenTargetId;
        if (query.EsCanalWeb)
        {
            Guid centralId = _warehouseConfigPort.ObtenerAlmacenCentralId();
            if (query.AlmacenId.HasValue && query.AlmacenId.Value != Guid.Empty && query.AlmacenId.Value != centralId)
            {
                throw new ValidationException("En el canal web la disponibilidad debe consultarse exclusivamente en el Almacén Central.");
            }
            almacenTargetId = centralId;
        }
        else
        {
            almacenTargetId = query.AlmacenId ?? Guid.Empty;
        }

        decimal stockDisponible = 0m;
        if (almacenTargetId != Guid.Empty)
        {
            var stockAlm = await _inventoryStockPort.ObtenerStockAlmacenAsync(almacenTargetId, producto.Id, cancellationToken);
            stockDisponible = stockAlm?.StockDisponible ?? 0m;
        }

        return new PDPResultDto(
            ProductoId: producto.Id,
            CodigoSku: producto.CodigoSku,
            NombreComercial: producto.NombreComercial,
            UnidadBase: producto.UnidadBase,
            CantidadBaseConvertida: cantidadBase,
            PrecioBaseUnitario: precioBaseUnitario,
            PrecioFinalUnitario: precioFinalUnitario,
            DescuentoUnitario: descuentoUnitario,
            PorcentajeDescuentoTotal: descTotalPorcentaje,
            Subtotal: subtotal,
            StockDisponible: stockDisponible,
            AplicaDescuentoVolumen: descVolumen > 0m,
            FactorConversion: factor,
            PrecioUnitarioComercial: precioUnitarioComercial);
    }

    private class DefaultCustomerProfilePort : ICustomerProfilePort
    {
        public Task<string> ObtenerPerfilClienteAsync(Guid? clienteId, CancellationToken cancellationToken = default)
            => Task.FromResult("Público General");
    }

    private class DefaultInventoryStockPort : IInventoryStockPort
    {
        public Task<Domain.Entities.StockAlmacen?> ObtenerStockAlmacenAsync(Guid almacenId, Guid productoId, CancellationToken cancellationToken = default)
            => Task.FromResult<Domain.Entities.StockAlmacen?>(null);

        public Task<IReadOnlyList<Domain.Entities.StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(Guid almacenId, IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Domain.Entities.StockAlmacen>>(Array.Empty<Domain.Entities.StockAlmacen>());

        public Task ActualizarStockAlmacenAsync(Domain.Entities.StockAlmacen stock, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class DefaultWarehouseConfigurationPort : IWarehouseConfigurationPort
    {
        public Guid ObtenerAlmacenCentralId() => Guid.Empty;
        public bool EsAlmacenValido(Guid almacenId) => true;
    }

    private class DefaultCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId => Guid.Empty;
        public string Username => "anonymous";
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public string TerminalScope => "system";
        public string ClientIp => "127.0.0.1";
        public bool HasRole(string role) => false;
        public bool IsInRole(string role) => false;
    }
}
