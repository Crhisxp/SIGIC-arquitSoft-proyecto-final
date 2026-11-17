namespace SIGIC.Application.Inventory.Ports;

/// <summary>
/// Puerto para registro de ajustes en Kardex de forma compensatoria y append-only (RF-INV-007).
/// </summary>
public interface IInventoryAdjustmentRepository
{
    Task<Guid> RegistrarAjusteKardexCompensatorioAsync(
        Guid productoId,
        Guid almacenId,
        decimal cantidadAjuste,
        decimal costoUnitario,
        string motivo,
        string codigoAutorizacion,
        DateTime fechaUtc,
        CancellationToken cancellationToken = default);
}
