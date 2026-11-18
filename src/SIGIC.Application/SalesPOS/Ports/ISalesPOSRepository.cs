using SIGIC.Application.SalesPOS.Commands;

namespace SIGIC.Application.SalesPOS.Ports;

/// <summary>
/// Puerto de persistencia de ventas en mostrador POS.
/// </summary>
public interface ISalesPOSRepository
{
    Task<VentaPOSResultDto> RegistrarVentaAsync(
        RegistrarVentaPOSCommand command,
        decimal totalVenta,
        DateTime fechaUtc,
        CancellationToken cancellationToken = default);

    Task<decimal> ObtenerTotalRecaudadoTurnoAsync(
        Guid turnoCajaId,
        CancellationToken cancellationToken = default) => Task.FromResult(0m);

    Task<VentaPOSDetalleDto?> ObtenerPorIdAsync(
        Guid ventaId,
        CancellationToken cancellationToken = default) => Task.FromResult<VentaPOSDetalleDto?>(null);
}
