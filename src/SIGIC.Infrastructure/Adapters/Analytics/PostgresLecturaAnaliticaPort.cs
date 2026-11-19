using Dapper;
using Npgsql;
using SIGIC.Application.Analytics.Dtos;
using SIGIC.Application.Analytics.Ports;
using SIGIC.Domain.Analytics;

namespace SIGIC.Infrastructure.Adapters.Analytics;

public sealed class PostgresLecturaAnaliticaPort(DatabasePoolOptions pools) : ILecturaAnaliticaPort
{
    private const int TimeoutSegundos = 4;

    public Task<IReadOnlyList<CanalVentaResumenDto>> ObtenerVentasYTicketsPorCanalAsync(PeriodoAnalitico periodo, string[] canales, CancellationToken ct) =>
        LeerAsync<CanalVentaResumenDto>("""
            WITH salidas_web AS (
                SELECT DISTINCT documento_referencia
                FROM kardex_movimientos
                WHERE tipo_operacion = 'VENTA_WEB' AND fecha_utc >= @DesdeUtc AND fecha_utc < @HastaExclusivoUtc
            ), ventas AS (
                SELECT 'POS' AS canal, total FROM ventas_pos
                WHERE fecha_utc >= @DesdeUtc AND fecha_utc < @HastaExclusivoUtc
                UNION ALL
                SELECT 'WEB', p.total FROM pedidos_web p
                JOIN salidas_web s ON s.documento_referencia = p.id::text
            )
            SELECT canal AS "Canal", COUNT(*) AS "Tickets", SUM(total) AS "TotalVentas"
            FROM ventas WHERE canal = ANY(@canales) GROUP BY canal ORDER BY canal
            """, new { periodo.DesdeUtc, periodo.HastaExclusivoUtc, canales }, ct);

    public Task<IReadOnlyList<HechoDto>> ObtenerQuiebresStockAsync(CancellationToken ct) =>
        LeerAsync<HechoDto>("""
            SELECT 'Posiciones de almacén sin stock disponible' AS "Etiqueta",
                   COUNT(*) FILTER (WHERE stock_fisico - stock_comprometido <= 0)::text AS "Valor",
                   'posiciones' AS "Unidad"
            FROM stock_almacen HAVING COUNT(*) > 0
            """, null, ct);

    public async Task<CierreOperativo> ObtenerCierreAsync(PeriodoAnalitico periodo, CancellationToken ct)
    {
        var filas = await LeerAsync<CierreOperativo>("""
            SELECT COUNT(*) AS "DiasCerrados", COALESCE(SUM(ingresos_netos),0) AS "IngresosNetos",
                   COALESCE(SUM(costo_ventas),0) AS "CostoVentas",
                   COALESCE(SUM(gastos_operativos),0) AS "GastosOperativos",
                   COALESCE(AVG(inventario_medio_al_costo),0) AS "InventarioMedioAlCosto"
            FROM reporting.cierres_operativos_diarios
            WHERE dia >= (@DesdeUtc AT TIME ZONE 'America/Lima')::date
              AND dia < (@HastaExclusivoUtc AT TIME ZONE 'America/Lima')::date
            """, periodo, ct);
        return filas.Single();
    }

    // El pool se selecciona por operación incluso cuando el JWT pertenece al personal LAN.
    private async Task<IReadOnlyList<T>> LeerAsync<T>(string sql, object? parametros, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(pools.WebConnection);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("SET TRANSACTION READ ONLY", transaction: tx,
            commandTimeout: TimeoutSegundos, cancellationToken: ct));
        var query = new CommandDefinition(sql, parametros, tx, commandTimeout: TimeoutSegundos, cancellationToken: ct);
        var filas = (await conn.QueryAsync<T>(query)).AsList();
        await tx.CommitAsync(ct);
        return filas;
    }
}
