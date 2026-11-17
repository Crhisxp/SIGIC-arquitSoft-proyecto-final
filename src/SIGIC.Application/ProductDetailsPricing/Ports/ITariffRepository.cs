namespace SIGIC.Application.ProductDetailsPricing.Ports;

/// <summary>
/// Provee reglas de tarifas por rol y descuentos por volumen (RF-PRE-001, RF-PRE-002).
/// </summary>
public interface ITariffRepository
{
    Task<decimal> ObtenerPorcentajeDescuentoRolAsync(string rol, CancellationToken cancellationToken = default);
    Task<decimal> ObtenerPorcentajeDescuentoVolumenAsync(decimal cantidadBase, CancellationToken cancellationToken = default);
}
