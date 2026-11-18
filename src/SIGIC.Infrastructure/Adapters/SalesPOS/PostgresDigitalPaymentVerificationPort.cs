using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.SalesPOS;

/// <summary>
/// Adaptador de verificación y registro atómico de unicidad de pagos digitales (RF-POS-006).
/// Invariante: Unicidad por tupla normalizada (metodo_pago, codigo_operacion, fecha).
/// </summary>
public class PostgresDigitalPaymentVerificationPort : IDigitalPaymentVerificationPort
{
    private readonly SigicDbContext _dbContext;

    public PostgresDigitalPaymentVerificationPort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<bool> ExisteCodigoOperacionAsync(
        string metodoPago,
        string codigoOperacion,
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        string normMetodo = metodoPago?.Trim().ToUpperInvariant() ?? string.Empty;
        string normCodigo = codigoOperacion?.Trim().ToUpperInvariant() ?? string.Empty;
        var fechaDt = fecha.ToDateTime(TimeOnly.MinValue);

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT COUNT(1)
            FROM pagos_digitales
            WHERE metodo_pago = @normMetodo AND codigo_operacion = @normCodigo AND fecha = @fechaDt;
            """;

        var command = new CommandDefinition(sql, new { normMetodo, normCodigo, fechaDt }, tx, cancellationToken: cancellationToken);
        int count = await conn.ExecuteScalarAsync<int>(command);
        return count > 0;
    }

    public async Task RegistrarCodigoOperacionAsync(
        string metodoPago,
        string codigoOperacion,
        DateOnly fecha,
        Guid ventaId,
        CancellationToken cancellationToken = default)
    {
        string normMetodo = metodoPago?.Trim().ToUpperInvariant() ?? string.Empty;
        string normCodigo = codigoOperacion?.Trim().ToUpperInvariant() ?? string.Empty;
        var fechaDt = fecha.ToDateTime(TimeOnly.MinValue);
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            INSERT INTO pagos_digitales (id, metodo_pago, codigo_operacion, fecha, venta_id, fecha_registro_utc)
            VALUES (@id, @normMetodo, @normCodigo, @fechaDt, @ventaId, @now);
            """;

        var command = new CommandDefinition(sql, new { id, normMetodo, normCodigo, fechaDt, ventaId, now }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(command);
    }
}
