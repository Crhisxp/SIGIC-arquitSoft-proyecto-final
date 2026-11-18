using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.CashManagement;

/// <summary>
/// Adaptador de emisión de alertas de arqueo asíncronas vía outbox transaccional (RF-CAJ-002).
/// Invariante: Ceguera total del cajero; la alerta se persiste en outbox para supervisión del Superadmin.
/// </summary>
public class OutboxCashAlertNotificationPort : ICashAlertNotificationPort
{
    private readonly SigicDbContext _dbContext;

    public OutboxCashAlertNotificationPort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task NotificarDiscrepanciaArqueoAsync(
        Guid turnoCajaId,
        Guid cajeroId,
        decimal diferencia,
        string justificacion,
        DateTime fechaCierreUtc,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            INSERT INTO alertas_caja_outbox (id, turno_caja_id, cajero_id, diferencia, justificacion, fecha_utc, procesado)
            VALUES (@id, @turnoCajaId, @cajeroId, @diferencia, @justificacion, @fechaCierreUtc, FALSE);
            """;

        var cmd = new CommandDefinition(
            sql,
            new { id, turnoCajaId, cajeroId, diferencia, justificacion, fechaCierreUtc },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmd);
    }
}
