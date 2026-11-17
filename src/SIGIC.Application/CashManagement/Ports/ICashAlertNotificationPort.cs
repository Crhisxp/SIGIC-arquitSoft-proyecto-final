namespace SIGIC.Application.CashManagement.Ports;

/// <summary>
/// Puerto para emisión de alertas al Superadmin ante discrepancias en arqueo de caja (RF-CAJ-002).
/// Invariante: Ceguera total del cajero; la alerta es asíncrona hacia el equipo de supervisión.
/// </summary>
public interface ICashAlertNotificationPort
{
    Task NotificarDiscrepanciaArqueoAsync(
        Guid turnoCajaId,
        Guid cajeroId,
        decimal diferencia,
        string justificacion,
        DateTime fechaCierreUtc,
        CancellationToken cancellationToken = default);
}
