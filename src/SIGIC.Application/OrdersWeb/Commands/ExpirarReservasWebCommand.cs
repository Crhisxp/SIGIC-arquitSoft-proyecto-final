namespace SIGIC.Application.OrdersWeb.Commands;

/// <summary>
/// Comando para evaluar y liberar reservas web vencidas de manera idempotente (RF-WEB-002).
/// </summary>
public record ExpirarReservasWebCommand();

public record ExpirarReservasResultDto(int CantidadPedidosExpirados, IReadOnlyList<Guid> PedidosExpiradosIds);
