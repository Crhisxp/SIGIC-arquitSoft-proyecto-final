using SIGIC.Application.CashManagement.Commands;

namespace SIGIC.Application.CashManagement.Commands;

/// <summary>
/// Comando para registro de arqueo ciego inmutable al cierre de turno (AC03, RF-CAJ-002).
/// Invariante: Cero visibilidad de saldos teóricos al momento del cierre de caja.
/// </summary>
public record RegistrarArqueoCiegoCommand(
    Guid TurnoCajaId,
    IReadOnlyList<ArqueoDenominacionDto> DesgloseFisico,
    decimal TotalDigitalDeclarado,
    string? JustificacionDiscrepancia = null);
