namespace SIGIC.Application.Security.Models;

/// <summary>
/// Registro inmutable de auditoría append-only para operaciones críticas (RNF-SEG-004, RF-CAJ-002, AC03).
/// </summary>
public record AuditLogEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string Accion { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;
    public string EntidadId { get; init; } = string.Empty;
    public string Detalles { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string TerminalScope { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }

    public AuditLogEntry() { }

    public AuditLogEntry(
        Guid userId,
        string accion,
        string entidad,
        string entidadId,
        string detalles,
        string ipAddress,
        string terminalScope,
        DateTime timestampUtc)
    {
        UserId = userId;
        Accion = accion;
        Entidad = entidad;
        EntidadId = entidadId;
        Detalles = detalles;
        IpAddress = ipAddress;
        TerminalScope = terminalScope;
        TimestampUtc = timestampUtc;
    }
}
