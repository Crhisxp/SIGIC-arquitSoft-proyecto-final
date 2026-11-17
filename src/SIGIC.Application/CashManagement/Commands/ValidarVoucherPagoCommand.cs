namespace SIGIC.Application.CashManagement.Commands;

/// <summary>
/// Comando para validación administrativa de vouchers de pago manual (RF-WEB-002).
/// </summary>
public record ValidarVoucherPagoCommand(
    Guid PedidoWebId,
    bool Aprobado,
    string? MotivoRechazo = null);
