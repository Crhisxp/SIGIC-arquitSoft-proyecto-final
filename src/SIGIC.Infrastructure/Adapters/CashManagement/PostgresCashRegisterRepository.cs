using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.CashManagement;

/// <summary>
/// Adaptador de persistencia para caja y arqueo ciego inmutable sobre PostgreSQL 16 (RF-CAJ-002, AC03).
/// Invariante: Cálculo exacto de saldo teórico sumando fondo inicial y recaudación de ventas exactamente una vez.
/// Encapsulamiento modular: La consulta de recaudación de ventas POS se canaliza a través de ISalesPOSRepository.
/// </summary>
public class PostgresCashRegisterRepository : ICashRegisterRepository
{
    private readonly SigicDbContext _dbContext;
    private readonly ISalesPOSRepository _salesPOSRepository;

    public PostgresCashRegisterRepository(SigicDbContext dbContext, ISalesPOSRepository salesPOSRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _salesPOSRepository = salesPOSRepository ?? throw new ArgumentNullException(nameof(salesPOSRepository));
    }

    public async Task<bool> EsTurnoAperturadoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = "SELECT COUNT(1) FROM turnos_caja WHERE id = @turnoCajaId AND estado = 'Aperturado';";
        var cmd = new CommandDefinition(sql, new { turnoCajaId }, tx, cancellationToken: cancellationToken);
        int count = await conn.ExecuteScalarAsync<int>(cmd);
        return count > 0;
    }

    public async Task<decimal> ObtenerSaldoTeoricoSistemaAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
    {
        // Encapsulamiento modular: Consulta la recaudación de ventas POS a través del contrato público ISalesPOSRepository
        // compartiendo la misma conexión física y transacción activa
        return await _salesPOSRepository.ObtenerTotalRecaudadoTurnoAsync(turnoCajaId, cancellationToken);
    }

    public async Task<Guid> ObtenerCajeroDeTurnoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = "SELECT cajero_id FROM turnos_caja WHERE id = @turnoCajaId;";
        var cmd = new CommandDefinition(sql, new { turnoCajaId }, tx, cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid>(cmd);
    }

    public async Task<DateTime> ObtenerFechaAperturaTurnoAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = "SELECT fecha_apertura_utc FROM turnos_caja WHERE id = @turnoCajaId;";
        var cmd = new CommandDefinition(sql, new { turnoCajaId }, tx, cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DateTime>(cmd);
    }

    public async Task<decimal> ObtenerFondoInicialAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = "SELECT fondo_inicial FROM turnos_caja WHERE id = @turnoCajaId;";
        var cmd = new CommandDefinition(sql, new { turnoCajaId }, tx, cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<decimal>(cmd);
    }

    public async Task CerrarTurnoCajaAsync(Guid turnoCajaId, CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = "UPDATE turnos_caja SET estado = 'Cerrado', fecha_cierre_utc = @now WHERE id = @turnoCajaId;";
        var cmd = new CommandDefinition(sql, new { turnoCajaId, now = DateTime.UtcNow }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    public async Task<bool> CerrarTurnoCajaConEstadoEsperadoAsync(
        Guid turnoCajaId,
        string estadoEsperado,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            UPDATE turnos_caja
            SET estado = 'Cerrado',
                fecha_cierre_utc = @now
            WHERE id = @turnoCajaId AND estado = @estadoEsperado;
            """;

        var cmd = new CommandDefinition(sql, new { turnoCajaId, estadoEsperado, now = DateTime.UtcNow }, tx, cancellationToken: cancellationToken);
        int rowsAffected = await conn.ExecuteAsync(cmd);
        return rowsAffected == 1;
    }

    public async Task GuardarArqueoCajaAppendOnlyAsync(
        ArqueoCaja arqueo,
        string? justificacion,
        CancellationToken cancellationToken = default)
    {
        if (arqueo == null)
            throw new ArgumentNullException(nameof(arqueo));

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sqlArqueo = """
            INSERT INTO caja_arqueo (
                id, turno_caja_id, cajero_id, fecha_apertura_utc, fecha_cierre_utc,
                fondo_inicial_sencillo, total_digital_declarado, total_efectivo_declarado,
                saldo_sistema_teorico, justificacion
            ) VALUES (
                @Id, @TurnoCajaId, @CajeroId, @FechaAperturaUtc, @FechaCierreUtc,
                @FondoInicialSencillo, @TotalDigitalDeclarado, @TotalEfectivoDeclarado,
                @SaldoSistemaTeorico, @justificacion
            );
            """;

        var cmdArqueo = new CommandDefinition(
            sqlArqueo,
            new
            {
                arqueo.Id,
                arqueo.TurnoCajaId,
                arqueo.CajeroId,
                arqueo.FechaAperturaUtc,
                arqueo.FechaCierreUtc,
                arqueo.FondoInicialSencillo,
                arqueo.TotalDigitalDeclarado,
                arqueo.TotalEfectivoDeclarado,
                arqueo.SaldoSistemaTeorico,
                justificacion
            },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmdArqueo);

        const string sqlDenom = """
            INSERT INTO caja_arqueo_denominacion (id, arqueo_caja_id, denominacion, cantidad, subtotal)
            VALUES (@id, @arqueoCajaId, @ValorUnitario, @Cantidad, @Subtotal);
            """;

        foreach (var d in arqueo.DesgloseFisico)
        {
            var id = Guid.NewGuid();
            var cmdDenom = new CommandDefinition(
                sqlDenom,
                new { id, arqueoCajaId = arqueo.Id, d.ValorUnitario, d.Cantidad, d.Subtotal },
                tx,
                cancellationToken: cancellationToken);

            await conn.ExecuteAsync(cmdDenom);
        }
    }
}
