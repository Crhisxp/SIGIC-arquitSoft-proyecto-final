namespace SIGIC.Application.SalesPOS.Commands;

public record VentaPOSItemDto(
    Guid ProductoId,
    decimal Cantidad,
    string UnidadMedida,
    decimal PrecioUnitario);

public record VentaPOSPagoDto(
    string MetodoPago,
    decimal Monto,
    string? CodigoOperacionDigital = null);

public record VentaPOSResultDto(
    Guid VentaId,
    string NumeroComprobante,
    decimal TotalVenta,
    DateTime FechaEmisionUtc);
