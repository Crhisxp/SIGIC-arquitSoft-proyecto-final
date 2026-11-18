using SIGIC.Application.Inventory.Commands;
using SIGIC.Domain.Entities;

namespace SIGIC.Application.Inventory.Ports;

/// <summary>
/// Puerto de persistencia y concurrencia pesimista para inventario y stock multi-almacén (RNF-CON-003, RF-INV-002, RF-INV-003, RF-INV-007, RF-WEB-005).
/// Ningún método de stock en Application debe operar sin AlmacenId explícito.
/// </summary>
public interface IInventoryStockPort
{
    Task<StockAlmacen?> ObtenerStockAlmacenAsync(
        Guid almacenId,
        Guid productoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adquiere bloqueos pesimistas ordenados ascendentemente por ID de producto (Algoritmo Havender anti-deadlock)
    /// sobre el almacén especificado (SELECT ... FOR UPDATE).
    /// </summary>
    Task<IReadOnlyList<StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(
        Guid almacenId,
        IEnumerable<Guid> productoIds,
        CancellationToken cancellationToken = default);

    Task ActualizarStockAlmacenAsync(
        StockAlmacen stock,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockAlmacenDetalleDto>> ObtenerListadoStockAsync(
        Guid? almacenId = null,
        Guid? productoId = null,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StockAlmacenDetalleDto>>(Array.Empty<StockAlmacenDetalleDto>());
}
