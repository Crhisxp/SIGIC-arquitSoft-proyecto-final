namespace SIGIC.Application.Inventory.Ports;

/// <summary>
/// Puerto transaccional para registro de movimientos definitivos en Kardex (RF-INV-007, RF-POS-004, RF-WEB-002).
/// Invariante: Operación append-only dentro de la misma transacción de salida física.
/// </summary>
public interface IKardexPort
{
    Task RegistrarMovimientoSalidaAsync(
        Guid productoId,
        Guid almacenId,
        decimal cantidadBase,
        decimal costoUnitario,
        string tipoOperacion,
        string documentoReferencia,
        DateTime fechaUtc,
        CancellationToken cancellationToken = default);
}
