using SIGIC.Domain.Entities;

namespace SIGIC.Application.ProductDetailsPricing.Ports;

/// <summary>
/// Puerto de lectura rápida optimizado para catálogo y detalle de producto (PDP).
/// </summary>
public interface IProductQueryPort
{
    Task<Producto?> ObtenerParaPDPAsync(Guid productoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Producto>> ObtenerMultiplesParaPDPAsync(IEnumerable<Guid> productoIds, CancellationToken cancellationToken = default);
    Task<decimal> ObtenerFactorConversionAsync(string unidadOrigen, string unidadDestino, CancellationToken cancellationToken = default);
}
