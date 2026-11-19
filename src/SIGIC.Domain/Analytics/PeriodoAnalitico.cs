namespace SIGIC.Domain.Analytics;

/// <summary>Intervalo de días contables en Lima, con límite superior exclusivo.</summary>
public sealed record PeriodoAnalitico
{
    private static readonly TimeZoneInfo Lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
    public int TotalDias { get; }
    public DateTime DesdeUtc { get; }
    public DateTime HastaExclusivoUtc { get; }

    public PeriodoAnalitico(DateOnly desde, DateOnly hastaExclusivo)
    {
        TotalDias = hastaExclusivo.DayNumber - desde.DayNumber;
        if (TotalDias is < 1 or > 366 || hastaExclusivo == DateOnly.MaxValue)
            throw new ArgumentException("Indique un periodo de 1 a 366 días.");
        DesdeUtc = TimeZoneInfo.ConvertTimeToUtc(desde.ToDateTime(TimeOnly.MinValue), Lima);
        HastaExclusivoUtc = TimeZoneInfo.ConvertTimeToUtc(hastaExclusivo.ToDateTime(TimeOnly.MinValue), Lima);
    }
}
