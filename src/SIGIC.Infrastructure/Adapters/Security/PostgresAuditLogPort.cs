using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.Security;

/// <summary>
/// Adaptador para registro inmutable append-only de auditoría (RNF-SEG-004).
/// Invariante: Permisos revocados de UPDATE, DELETE y TRUNCATE en PostgreSQL 16.
/// </summary>
public class PostgresAuditLogPort : IAuditLogPort
{
    private readonly SigicDbContext _dbContext;

    public PostgresAuditLogPort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task RegistrarAccionAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        var id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id;
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            INSERT INTO audit_log (id, user_id, accion, entidad, entidad_id, detalles, ip_address, terminal_scope, timestamp_utc)
            VALUES (@id, @UserId, @Accion, @Entidad, @EntidadId, @Detalles, @IpAddress, @TerminalScope, @TimestampUtc);
            """;

        var cmd = new CommandDefinition(
            sql,
            new
            {
                id,
                entry.UserId,
                entry.Accion,
                entry.Entidad,
                entry.EntidadId,
                entry.Detalles,
                entry.IpAddress,
                entry.TerminalScope,
                entry.TimestampUtc
            },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmd);
    }
}
