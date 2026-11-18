using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.Inventory.Commands;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.Inventory;

/// <summary>
/// Puerto transaccional para registro de movimientos definitivos en Kardex (RF-INV-007, RF-POS-004, RF-WEB-002).
/// Invariante: Operación append-only dentro de la misma transacción de salida física.
/// </summary>
public class PostgresKardexPort : IKardexPort
{
    private readonly SigicDbContext _dbContext;

    public PostgresKardexPort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task RegistrarMovimientoSalidaAsync(
        Guid productoId,
        Guid almacenId,
        decimal cantidadBase,
        decimal costoUnitario,
        string tipoOperacion,
        string documentoReferencia,
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
                @id, @productoId, @almacenId, @tipoOperacion, @cantidadBase,
                @costoUnitario, @documentoReferencia, NULL, NULL, @fechaUtc
            );
            """;

        var command = new CommandDefinition(
            sql,
            new { id, productoId, almacenId, tipoOperacion, cantidadBase, costoUnitario, documentoReferencia, fechaUtc },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<KardexMovimientoDto>> ObtenerMovimientosAsync(
        Guid productoId,
        Guid? almacenId = null,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT id AS Id, producto_id AS ProductoId, almacen_id AS AlmacenId,
                   tipo_operacion AS TipoOperacion, cantidad_base AS CantidadBase,
                   costo_unitario AS CostoUnitario, documento_referencia AS DocumentoReferencia,
                   codigo_autorizacion AS CodigoAutorizacion, motivo AS Motivo, fecha_utc AS FechaUtc
            FROM kardex_movimientos
            WHERE producto_id = @productoId AND (@almacenId IS NULL OR almacen_id = @almacenId)
            ORDER BY fecha_utc DESC;
            """;

        var command = new CommandDefinition(sql, new { productoId, almacenId }, tx, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<KardexMovimientoDto>(command);
        return rows.ToList();
    }
}
