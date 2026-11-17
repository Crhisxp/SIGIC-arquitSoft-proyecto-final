namespace SIGIC.Application.Inventory.Commands;

public record AjustarInventarioResultDto(
    Guid AjusteId,
    Guid ProductoId,
    decimal StockFisicoAnterior,
    decimal CantidadAjuste,
    decimal StockFisicoNuevo,
    decimal StockComprometido,
    DateTime FechaUtc);
