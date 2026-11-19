using SIGIC.Application.Analytics.Dtos;

namespace SIGIC.Application.Analytics.Ports;

/// <summary>
/// Puerto primario del Copiloto Analítico IA (RF-ANA-001).
/// Orquesta la autorización interna, resolución segura de intenciones, extracción de snapshots y síntesis de respuesta.
/// </summary>
public interface ICopilotoAnaliticoService
{
    Task<ConsultaAnaliticaResponseDto> ConsultarAsync(
        ConsultaAnaliticaRequestDto request,
        CancellationToken cancellationToken = default);
}
