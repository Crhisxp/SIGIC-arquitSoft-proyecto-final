using SIGIC.Domain.Entities;

namespace SIGIC.Application.CashManagement.Ports;

/// <summary>
/// Puerto de persistencia inmutable para arqueo de caja (AC03, RF-CAJ-002).
/// Invariante: La base de datos deniega operaciones UPDATE y DELETE sobre arqueos.
/// </summary>
public interface ICashRegisterRepository
{
    Task<bool> EsTurnoAperturadoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default);
    Task<decimal> ObtenerSaldoTeoricoSistemaAsync(Guid turnoCajaId, CancellationToken cancellationToken = default);
    Task<Guid> ObtenerCajeroDeTurnoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default);
    Task<DateTime> ObtenerFechaAperturaTurnoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default);
    Task<decimal> ObtenerFondoInicialAsync(Guid turnoCajaId, CancellationToken cancellationToken = default);
    Task CerrarTurnoCajaAsync(Guid turnoCajaId, CancellationToken cancellationToken = default);
    Task<bool> CerrarTurnoCajaConEstadoEsperadoAsync(Guid turnoCajaId, string estadoEsperado, CancellationToken cancellationToken = default);
    Task GuardarArqueoCajaAppendOnlyAsync(ArqueoCaja arqueo, string? justificacion, CancellationToken cancellationToken = default);
}
