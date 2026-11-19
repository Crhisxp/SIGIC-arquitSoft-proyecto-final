namespace SIGIC.Application.Analytics.Dtos;

public sealed record ConsultaAnaliticaRequestDto(string Pregunta, DateOnly? Desde, DateOnly? HastaExclusivo, string[]? Canales = null);
public sealed record HechoDto(string Etiqueta, string Valor, string Unidad);
public sealed record ConsultaAnaliticaResponseDto(
    string Estado, string Metrica, IReadOnlyList<HechoDto> Hechos, string Resumen, DateTime CorteUtc);
public sealed record CanalVentaResumenDto(string Canal, long Tickets, decimal TotalVentas);
