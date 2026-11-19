using SIGIC.Application.Analytics.Dtos;
using SIGIC.Domain.Analytics;

namespace SIGIC.Application.Analytics.Ports;

public interface ILecturaAnaliticaPort
{
    Task<IReadOnlyList<CanalVentaResumenDto>> ObtenerVentasYTicketsPorCanalAsync(PeriodoAnalitico periodo, string[] canales, CancellationToken ct);
    Task<IReadOnlyList<HechoDto>> ObtenerQuiebresStockAsync(CancellationToken ct);
    Task<CierreOperativo> ObtenerCierreAsync(PeriodoAnalitico periodo, CancellationToken ct);
}
