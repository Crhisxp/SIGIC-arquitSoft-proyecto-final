using SIGIC.Application.Security.Models;

namespace SIGIC.Application.Security.Ports;

/// <summary>
/// Puerto para registro inmutable append-only de auditoría (RNF-SEG-004).
/// </summary>
public interface IAuditLogPort
{
    Task RegistrarAccionAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}
