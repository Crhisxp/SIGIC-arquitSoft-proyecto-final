using SIGIC.Application.CashManagement.Commands;

namespace SIGIC.Application.CashManagement.Ports;

/// <summary>
/// Puerto para emisión automática de guías de traslado interno para pedidos con Retiro en Tienda (RF-INV-003, RF-POS-011).
/// </summary>
public interface ITransferGuidePort
{
    Task<string> GenerarGuiaTrasladoAsync(
        Guid pedidoWebId,
        Guid almacenOrigenId,
        Guid tiendaDestinoId,
        IReadOnlyList<(Guid ProductoId, decimal Cantidad)> items,
        CancellationToken cancellationToken = default);

    Task<GuiaTrasladoDetalleDto?> ObtenerGuiaPorIdAsync(
        Guid guiaId,
        CancellationToken cancellationToken = default) => Task.FromResult<GuiaTrasladoDetalleDto?>(null);
}
