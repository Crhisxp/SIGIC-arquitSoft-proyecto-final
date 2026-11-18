namespace SIGIC.Application.OrdersWeb.Commands;

/// <summary>
/// Comando para evaluar y liberar reservas web vencidas de manera idempotente (RF-WEB-002).
/// </summary>
public record ExpirarReservasWebCommand(Guid? PedidoId = null);

public record ExpirarReservasResultDto(
    int CantidadPedidosExpirados,
    IReadOnlyList<Guid> PedidosExpiradosIds,
    IReadOnlyList<string>? Errores = null)
{
    public IReadOnlyList<string> Errores { get; init; } = Errores ?? Array.Empty<string>();
}
