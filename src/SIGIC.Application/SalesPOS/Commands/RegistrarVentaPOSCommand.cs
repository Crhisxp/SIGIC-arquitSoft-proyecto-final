namespace SIGIC.Application.SalesPOS.Commands;

/// <summary>
/// Comando para registrar una venta en mostrador POS con pagos mixtos y control anti-deadlock (RF-POS-001, RF-POS-006, RF-WEB-005).
/// </summary>
public record RegistrarVentaPOSCommand(
    Guid TurnoCajaId,
    Guid ClienteId,
    IReadOnlyList<VentaPOSItemDto> Items,
    IReadOnlyList<VentaPOSPagoDto> Pagos,
    string TipoComprobante,
    string? Observaciones = null);
