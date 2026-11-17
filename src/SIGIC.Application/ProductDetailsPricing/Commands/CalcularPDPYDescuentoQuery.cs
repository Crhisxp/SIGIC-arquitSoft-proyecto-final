namespace SIGIC.Application.ProductDetailsPricing.Commands;

/// <summary>
/// Consulta para calcular el detalle del producto (PDP), conversiones de unidad y precios dinámicos por rol y volumen.
/// Admite resolución confiable de perfil mediante ClienteId (RF-PRE-002) y consulta por almacén específico.
/// </summary>
public record CalcularPDPYDescuentoQuery(
    Guid ProductoId,
    string? RolCliente,
    decimal Cantidad,
    string UnidadComercial,
    bool EsCanalWeb = false,
    Guid? ClienteId = null,
    Guid? AlmacenId = null);
