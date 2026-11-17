namespace SIGIC.Application.OrdersWeb.Commands;

/// <summary>
/// Comando para reservar productos desde el canal web con cálculo dinámico de TTL (RF-WEB-001, RF-WEB-002, RF-WEB-005).
/// </summary>
public record ReservarPedidoWebCommand(
    Guid ClienteId,
    string MetodoPago,
    string ModalidadEntrega,
    Guid? TiendaDestinoId,
    IReadOnlyList<PedidoWebItemDto> Items);
