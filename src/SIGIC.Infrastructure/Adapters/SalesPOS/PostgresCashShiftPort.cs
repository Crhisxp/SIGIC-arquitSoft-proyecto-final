using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.SalesPOS;

/// <summary>
/// Adaptador para verificación transaccional y bloqueo de turnos de caja en mostrador POS (RF-POS-001, RF-INV-002).
/// Invariante: Bloqueo pesimista para evitar ventas concurrentes sobre un turno en proceso de cierre.
/// </summary>
public class PostgresCashShiftPort : ICashShiftPort
{
    private readonly SigicDbContext _dbContext;

    public PostgresCashShiftPort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<bool> EsTurnoValidoYAperturadoAsync(
        Guid turnoCajaId,
        Guid cajeroId,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT COUNT(1)
            FROM turnos_caja
            WHERE id = @turnoCajaId AND cajero_id = @cajeroId AND estado = 'Aperturado';
            """;

        var command = new CommandDefinition(sql, new { turnoCajaId, cajeroId }, tx, cancellationToken: cancellationToken);
        int count = await conn.ExecuteScalarAsync<int>(command);
        return count > 0;
    }

    public async Task<bool> ValidarYBloquearTurnoParaVentaAsync(
        Guid turnoCajaId,
        Guid cajeroId,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT id
            FROM turnos_caja
            WHERE id = @turnoCajaId AND cajero_id = @cajeroId AND estado = 'Aperturado'
            FOR UPDATE;
            """;

        var command = new CommandDefinition(sql, new { turnoCajaId, cajeroId }, tx, cancellationToken: cancellationToken);
        var id = await conn.QuerySingleOrDefaultAsync<Guid?>(command);
        return id.HasValue;
    }

    public async Task<Guid> ObtenerAlmacenTiendaIdAsync(
        Guid turnoCajaId,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT almacen_id
            FROM turnos_caja
            WHERE id = @turnoCajaId;
            """;

        var command = new CommandDefinition(sql, new { turnoCajaId }, tx, cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid>(command);
    }
}
