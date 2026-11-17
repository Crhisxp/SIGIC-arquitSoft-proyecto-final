namespace SIGIC.Application.Common.Interfaces;

/// <summary>
/// Proveedor de fecha y hora UTC desacoplado para pruebas deterministas de TTL y fechas de arqueo/auditoría.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
