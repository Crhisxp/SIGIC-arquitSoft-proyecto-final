namespace SIGIC.Application.SalesPOS.Ports;

/// <summary>
/// Puerto de verificación de unicidad de pagos digitales (RF-POS-006).
/// Invariante de unicidad por tupla (metodoPago, codigoOperacion, fecha).
/// </summary>
public interface IDigitalPaymentVerificationPort
{
    Task<bool> ExisteCodigoOperacionAsync(
        string metodoPago,
        string codigoOperacion,
        DateOnly fecha,
        CancellationToken cancellationToken = default);

    Task RegistrarCodigoOperacionAsync(
        string metodoPago,
        string codigoOperacion,
        DateOnly fecha,
        Guid ventaId,
        CancellationToken cancellationToken = default);
}
