using SIGIC.Application.OrdersWeb.Commands;

namespace SIGIC.Application.OrdersWeb.Ports;

/// <summary>
/// Puerto para persistencia y consulta de pedidos y reservas del canal Web.
/// Incluye contratos para bloqueo pesimista y transiciones de estado atómicas (RNF-CON-004).
/// </summary>
public interface IWebOrderRepository
{
    Task<PedidoWebDto?> ObtenerPorIdAsync(Guid pedidoId, CancellationToken cancellationToken = default);
    Task<PedidoWebDto?> ObtenerParaBloqueoAsync(Guid pedidoId, CancellationToken cancellationToken = default);
    Task GuardarPedidoAsync(PedidoWebDto pedido, CancellationToken cancellationToken = default);
    Task ActualizarEstadoAsync(Guid pedidoId, string nuevoEstado, CancellationToken cancellationToken = default);
    Task<bool> ActualizarEstadoConcurrenteAsync(Guid pedidoId, string estadoEsperado, string nuevoEstado, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PedidoWebDto>> ObtenerPedidosExpiradosAsync(DateTime fechaReferenciaUtc, CancellationToken cancellationToken = default);
    Task AsignarGuiaRemisionAsync(Guid pedidoId, string numeroGuia, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
