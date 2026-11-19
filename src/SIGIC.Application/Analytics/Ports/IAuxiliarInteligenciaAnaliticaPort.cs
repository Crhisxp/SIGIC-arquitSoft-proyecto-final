namespace SIGIC.Application.Analytics.Ports;

public interface IAuxiliarInteligenciaAnaliticaPort
{
    Task<string> ElegirPlantillaAsync(string metrica, IReadOnlyList<string> indicadores, CancellationToken ct);
}
