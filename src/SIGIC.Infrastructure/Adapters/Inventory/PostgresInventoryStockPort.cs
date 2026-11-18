using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.Inventory;

/// <summary>
/// Adaptador transaccional de persistencia para stock de inventario multi-almacén sobre PostgreSQL 16 (RNF-CON-003, RF-INV-002, RF-WEB-005).
/// Cumple la regla Havender: ordenamiento determinista ascendente por ProductoId antes de adquirir bloqueos pesimistas.
/// Garantiza reintentos acotados con backoff exponencial y jitter (RNF-CON-002).
/// </summary>
public class PostgresInventoryStockPort : IInventoryStockPort
{
    private readonly SigicDbContext _dbContext;

    public PostgresInventoryStockPort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StockAlmacen?> ObtenerStockAlmacenAsync(
        Guid almacenId,
        Guid productoId,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT id AS Id, almacen_id AS AlmacenId, producto_id AS ProductoId,
                   stock_fisico AS StockFisico, stock_comprometido AS StockComprometido
            FROM stock_almacen
            WHERE almacen_id = @almacenId AND producto_id = @productoId;
            """;

        var command = new CommandDefinition(sql, new { almacenId, productoId }, tx, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<StockAlmacenDto>(command);

        if (row == null)
            return null;

        return new StockAlmacen(row.Id, row.ProductoId, row.AlmacenId, row.StockFisico, row.StockComprometido);
    }

    public async Task<IReadOnlyList<StockAlmacen>> ObtenerParaBloqueoOrdenadosAsync(
        Guid almacenId,
        IEnumerable<Guid> productoIds,
        CancellationToken cancellationToken = default)
    {
        if (productoIds == null)
            throw new ArgumentNullException(nameof(productoIds));

        if (_dbContext.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Se requiere una transacción activa para adquirir bloqueos pesimistas de stock (RNF-CON-003, RF-WEB-005).");

        // Ordenamiento determinista ascendente anti-deadlock (Algoritmo Havender / RF-WEB-005)
        var orderedProductIds = productoIds.Distinct().OrderBy(id => id).ToArray();
        if (orderedProductIds.Length == 0)
            return Array.Empty<StockAlmacen>();

        const string sql = """
            SELECT id AS Id, almacen_id AS AlmacenId, producto_id AS ProductoId,
                   stock_fisico AS StockFisico, stock_comprometido AS StockComprometido
            FROM stock_almacen
            WHERE almacen_id = @almacenId AND producto_id = ANY(@orderedProductIds)
            ORDER BY producto_id ASC
            FOR UPDATE;
            """;

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        var command = new CommandDefinition(sql, new { almacenId, orderedProductIds }, tx, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<StockAlmacenDto>(command);
        return rows.Select(r => new StockAlmacen(r.Id, r.ProductoId, r.AlmacenId, r.StockFisico, r.StockComprometido)).ToList();
    }

    public async Task ActualizarStockAlmacenAsync(
        StockAlmacen stock,
        CancellationToken cancellationToken = default)
    {
        if (stock == null)
            throw new ArgumentNullException(nameof(stock));

        if (_dbContext.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Se requiere una transacción activa para actualizar stock de inventario.");

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        // 1. Obtener estado actual bloqueado bajo la transacción
        const string sqlSelect = """
            SELECT id AS Id, almacen_id AS AlmacenId, producto_id AS ProductoId,
                   stock_fisico AS StockFisico, stock_comprometido AS StockComprometido
            FROM stock_almacen
            WHERE id = @Id
            FOR UPDATE;
            """;

        var current = await conn.QuerySingleOrDefaultAsync<StockAlmacenDto>(
            new CommandDefinition(sqlSelect, new { stock.Id }, tx, cancellationToken: cancellationToken));

        if (current == null)
            throw new InvalidOperationException($"No se encontró registro de stock_almacen con id '{stock.Id}'.");

        decimal deltaFisico = stock.StockFisico - current.StockFisico;
        decimal deltaComprometido = stock.StockComprometido - current.StockComprometido;

        string sqlUpdate;
        object parameters;

        if (deltaComprometido > 0 && deltaFisico == 0)
        {
            // Caso A: Reserva nueva de stock (Web Checkout / RF-WEB-002)
            decimal cantidad = deltaComprometido;
            sqlUpdate = """
                UPDATE stock_almacen
                SET stock_comprometido = stock_comprometido + @cantidad
                WHERE id = @Id AND (stock_fisico - stock_comprometido) >= @cantidad;
                """;
            parameters = new { stock.Id, cantidad };
        }
        else if (deltaFisico < 0 && deltaComprometido < 0 && deltaFisico == deltaComprometido)
        {
            // Caso B: Despacho físico desde reserva previa (Voucher Aprobado / RF-INV-003)
            decimal cantidad = -deltaFisico;
            sqlUpdate = """
                UPDATE stock_almacen
                SET stock_fisico = stock_fisico - @cantidad,
                    stock_comprometido = stock_comprometido - @cantidad
                WHERE id = @Id AND stock_fisico >= @cantidad AND stock_comprometido >= @cantidad;
                """;
            parameters = new { stock.Id, cantidad };
        }
        else if (deltaComprometido < 0 && deltaFisico == 0)
        {
            // Caso C: Liberación de reserva previa (Expiración o Voucher Rechazado / RF-WEB-002)
            decimal cantidad = -deltaComprometido;
            sqlUpdate = """
                UPDATE stock_almacen
                SET stock_comprometido = stock_comprometido - @cantidad
                WHERE id = @Id AND stock_comprometido >= @cantidad;
                """;
            parameters = new { stock.Id, cantidad };
        }
        else if (deltaFisico < 0 && deltaComprometido == 0)
        {
            // Caso D: Salida física directa sin reserva previa (Venta POS / RF-POS-004)
            decimal cantidad = -deltaFisico;
            sqlUpdate = """
                UPDATE stock_almacen
                SET stock_fisico = stock_fisico - @cantidad
                WHERE id = @Id AND (stock_fisico - stock_comprometido) >= @cantidad;
                """;
            parameters = new { stock.Id, cantidad };
        }
        else
        {
            // Caso E: Ajuste físico o entrada de inventario (RF-INV-007)
            sqlUpdate = """
                UPDATE stock_almacen
                SET stock_fisico = @StockFisico,
                    stock_comprometido = @StockComprometido
                WHERE id = @Id AND @StockFisico >= @StockComprometido AND @StockComprometido >= 0;
                """;
            parameters = new { stock.Id, stock.StockFisico, stock.StockComprometido };
        }

        var cmd = new CommandDefinition(sqlUpdate, parameters, tx, cancellationToken: cancellationToken);
        int rowsAffected = await conn.ExecuteAsync(cmd);

        if (rowsAffected != 1)
        {
            decimal cantRequerida = Math.Abs(deltaComprometido != 0 ? deltaComprometido : deltaFisico);
            decimal disponibleActual = current.StockFisico - current.StockComprometido;
            throw new StockInsuficienteException(
                stock.ProductoId,
                string.Empty,
                cantRequerida,
                disponibleActual);
        }
    }

    private class StockAlmacenDto
    {
        public Guid Id { get; set; }
        public Guid AlmacenId { get; set; }
        public Guid ProductoId { get; set; }
        public decimal StockFisico { get; set; }
        public decimal StockComprometido { get; set; }
    }
}
