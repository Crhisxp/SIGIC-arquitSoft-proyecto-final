using SIGIC.Application.CashManagement.Commands;

namespace SIGIC.Application.SalesPOS.Ports;

/// <summary>
/// Puerto para verificación de estado del turno de caja del cajero y almacén asignado a la caja (RF-POS-001, RF-INV-002).
/// Permite validar y coordinar transaccionalmente el turno frente a cierres concurrentes.
/// </summary>
public interface ICashShiftPort
{
    Task<bool> EsTurnoValidoYAperturadoAsync(Guid turnoCajaId, Guid cajeroId, CancellationToken cancellationToken = default);
    Task<bool> ValidarYBloquearTurnoParaVentaAsync(Guid turnoCajaId, Guid cajeroId, CancellationToken cancellationToken = default);
    Task<Guid> ObtenerAlmacenTiendaIdAsync(Guid turnoCajaId, CancellationToken cancellationToken = default);
    Task AbrirTurnoAsync(Guid turnoId, Guid cajaId, Guid cajeroId, Guid almacenId, decimal fondoInicial, DateTime fechaAperturaUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<TurnoCajaDto?> ObtenerTurnoActualAsync(Guid cajeroId, CancellationToken cancellationToken = default) => Task.FromResult<TurnoCajaDto?>(null);
}
