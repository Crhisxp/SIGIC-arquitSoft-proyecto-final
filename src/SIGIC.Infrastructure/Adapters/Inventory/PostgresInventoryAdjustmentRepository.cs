using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.Inventory;

/// <summary>
/// Repositorio para registro de ajustes en Kardex de forma compensatoria y append-only (RF-INV-007).
/// </summary>
public class PostgresInventoryAdjustmentRepository : IInventoryAdjustmentRepository
{
    private readonly SigicDbContext _dbContext;

    public PostgresInventoryAdjustmentRepository(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Guid> RegistrarAjusteKardexCompensatorioAsync(
        Guid productoId,
        Guid almacenId,
        decimal cantidadAjuste,
        decimal costoUnitario,
        string motivo,
        string codigoAutorizacion,
        DateTime fechaUtc,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            INSERT INTO kardex_movimientos (
                id, producto_id, almacen_id, tipo_operacion, cantidad_base,
                costo_unitario, documento_referencia, codigo_autorizacion, motivo, fecha_utc
            ) VALUES (
                @id, @productoId, @almacenId, 'AJUSTE_COMPENSATORIO', @cantidadAjuste,
                @costoUnitario, 'AJUSTE-KARDEX', @codigoAutorizacion, @motivo, @fechaUtc
            );
            """;

        var command = new CommandDefinition(
            sql,
            new { id, productoId, almacenId, cantidadAjuste, costoUnitario, codigoAutorizacion, motivo, fechaUtc },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(command);
        return id;
    }
}
