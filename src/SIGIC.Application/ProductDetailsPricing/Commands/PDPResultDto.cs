namespace SIGIC.Application.ProductDetailsPricing.Commands;

/// <summary>
/// Resultado del cálculo de detalle de producto, mediciones y descuentos aplicados.
/// Cumple con RF-INV-005, RF-PRE-001, RF-PRE-002 y RNF-CON-003.
/// </summary>
public record PDPResultDto(
    Guid ProductoId,
    string CodigoSku,
    string NombreComercial,
    string UnidadBase,
    decimal CantidadBaseConvertida,
    decimal PrecioBaseUnitario,
    decimal PrecioFinalUnitario,
    decimal DescuentoUnitario,
    decimal PorcentajeDescuentoTotal,
    decimal Subtotal,
    decimal StockDisponible,
    bool AplicaDescuentoVolumen,
    decimal FactorConversion = 1.0000m,
    decimal PrecioUnitarioComercial = 0m);

public record ProductoCatalogoDto(
    Guid Id,
    string CodigoSku,
    string NombreComercial,
    string UnidadBase,
    decimal PrecioVenta,
    decimal StockDisponible,
    bool DisponibleEnWeb);
