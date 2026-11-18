using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.ProductDetailsPricing;

/// <summary>
/// Provee reglas de tarifas por rol y descuentos por volumen sobre PostgreSQL 16 (RF-PRE-001, RF-PRE-002).
/// </summary>
public class PostgresTariffRepository : ITariffRepository
{
    private readonly SigicDbContext _dbContext;

    public PostgresTariffRepository(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<decimal> ObtenerPorcentajeDescuentoRolAsync(
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rol))
            return 0m;

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        string lockClause = tx != null ? " FOR SHARE" : string.Empty;
        string sql = $"""
            SELECT porcentaje_descuento
            FROM tarifas_rol
            WHERE UPPER(rol) = UPPER(@rol){lockClause};
            """;

        var cmd = new CommandDefinition(sql, new { rol }, tx, cancellationToken: cancellationToken);
        var result = await conn.QuerySingleOrDefaultAsync<decimal?>(cmd);

        return result ?? 0m;
    }

    public async Task<decimal> ObtenerPorcentajeDescuentoVolumenAsync(
        decimal cantidadBase,
        CancellationToken cancellationToken = default)
    {
        if (cantidadBase <= 0)
            return 0m;

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        string lockClause = tx != null ? " FOR SHARE" : string.Empty;
        string sql = $"""
            SELECT porcentaje_descuento
            FROM descuentos_volumen
            WHERE cantidad_minima <= @cantidadBase
            ORDER BY cantidad_minima DESC
            LIMIT 1{lockClause};
            """;

        var cmd = new CommandDefinition(sql, new { cantidadBase }, tx, cancellationToken: cancellationToken);
        var result = await conn.QuerySingleOrDefaultAsync<decimal?>(cmd);

        return result ?? 0m;
    }
}
