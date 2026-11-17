namespace SIGIC.Application.OrdersWeb.Commands;

public record PedidoWebItemDto(
    Guid ProductoId,
    decimal Cantidad,
    decimal PrecioUnitario,
    string UnidadMedida = "UND",
    decimal FactorConversion = 1.0000m,
    decimal CantidadBase = 0m)
{
    public decimal CantidadComercial => Cantidad;
    public decimal CantidadBaseEfectiva => CantidadBase > 0m ? CantidadBase : Math.Round(Cantidad * FactorConversion, 4, MidpointRounding.ToEven);
}

public record PedidoWebDto(
    Guid Id,
    Guid ClienteId,
    string MetodoPago,
    string ModalidadEntrega,
    Guid? TiendaDestinoId,
    Guid AlmacenOrigenId,
    DateTime FechaCreacionUtc,
    DateTime FechaExpiracionUtc,
    DateTime? FechaLimiteVoucherUtc,
    DateTime? FechaSubidaVoucherUtc,
    string Estado,
    IReadOnlyList<PedidoWebItemDto> Items);

public record ReservarPedidoWebResultDto(
    Guid PedidoId,
    DateTime FechaCreacionUtc,
    DateTime FechaExpiracionUtc,
    DateTime? FechaLimiteVoucherUtc,
    decimal Total,
    string Estado);
