namespace SIGIC.Application.Inventory.Commands;

public record AjustarInventarioResultDto(
    Guid AjusteId,
    Guid ProductoId,
    decimal StockFisicoAnterior,
    decimal CantidadAjuste,
    decimal StockFisicoNuevo,
    decimal StockComprometido,
    DateTime FechaUtc);

public record KardexMovimientoDto(
    Guid Id,
    Guid ProductoId,
    Guid AlmacenId,
    string TipoOperacion,
    decimal CantidadBase,
    decimal CostoUnitario,
    string DocumentoReferencia,
    string? CodigoAutorizacion,
    string? Motivo,
    DateTime FechaUtc);

public record StockAlmacenDetalleDto(
    Guid ProductoId,
    string CodigoSku,
    string NombreComercial,
    Guid AlmacenId,
    string NombreAlmacen,
    decimal StockFisico,
    decimal StockComprometido,
    decimal StockDisponible);
