using SIGIC.Application.Common.Interfaces;

namespace SIGIC.Infrastructure.Adapters.Common;

/// <summary>
/// Proveedor de fecha y hora UTC desacoplado para Infrastructure y Background Workers (RF-WEB-002).
/// </summary>
public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
