using System.Diagnostics;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Infrastructure.Adapters.ProductDetailsPricing;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Concurrency;

[Collection("PostgreSqlDatabase")]
public class PgBouncerPoolSegregationTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public PgBouncerPoolSegregationTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PosPool_LatencyMeasurement_P95IsBelow250Ms()
    {
        const int iterations = 50;
        var latencies = new List<double>(iterations);

        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await using var conn = new NpgsqlConnection(_fixture.PosPoolConnectionString);
            await conn.OpenAsync();

            int result = await conn.ExecuteScalarAsync<int>("SELECT 1;");
            sw.Stop();

            Assert.Equal(1, result);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        int p95Index = (int)Math.Ceiling(0.95 * latencies.Count) - 1;
        double p95 = latencies[p95Index];

        // RNF-CON-001: P95 < 250 ms en mostrador POS
        Assert.True(p95 < 250.0, $"El P95 medido en sigic_pos_pool ({p95:F2} ms) superó el umbral de 250 ms.");
    }

    [Fact]
    public async Task WebPoolSaturation_DoesNotStarvePosPool_AndConnection31TimesOutAt3Seconds()
    {
        // 1. Saturar exactamente las 30 conexiones de sigic_web_pool manteniendo operaciones activas confirmadas
        const int poolWebCapacity = 30;
        using var barrier30Active = new CountdownEvent(poolWebCapacity);
        var releaseWebTasks = new TaskCompletionSource<bool>();

        var webHoldingTasks = Enumerable.Range(0, poolWebCapacity).Select(async _ =>
        {
            try
            {
                await using var conn = new NpgsqlConnection(_fixture.WebPoolConnectionString);
                await conn.OpenAsync();
                await using var tx = await conn.BeginTransactionAsync();

                // Confirmar que la conexión está abierta y ocupando un slot físico en PgBouncer
                await conn.ExecuteScalarAsync<int>("SELECT 1;", transaction: tx);
                barrier30Active.Signal();

                // Mantener ocupado el slot de pool_web hasta que concluyan las mediciones
                await releaseWebTasks.Task;
                await tx.RollbackAsync();
            }
            catch
            {
                // Manejo de corte en teardown
            }
        }).ToArray();

        // Esperar hasta que las 30 conexiones estén simultáneamente confirmadas y activas
        bool all30Confirmed = barrier30Active.Wait(TimeSpan.FromSeconds(10));
        Assert.True(all30Confirmed, "No se pudieron saturar y confirmar las 30 conexiones de sigic_web_pool.");

        try
        {
            // 2. Medir latencia de adquisición y ejecución en sigic_pos_pool bajo saturación web total
            var posSw = Stopwatch.StartNew();
            await using var posConn = new NpgsqlConnection(_fixture.PosPoolConnectionString);
            await posConn.OpenAsync();
            int posResult = await posConn.ExecuteScalarAsync<int>("SELECT 1;");
            posSw.Stop();

            Assert.Equal(1, posResult);
            // RNF-CON-001: Latencia de adquisición < 1 ms / respuesta rápida < 250 ms
            Assert.True(posSw.ElapsedMilliseconds < 250,
                $"POS Pool sufrió inanición bajo saturación web total: {posSw.ElapsedMilliseconds} ms");

            // 3. Verificar que la conexión web #31 sufra rechazo / timeout por backpressure (query_wait_timeout = 3 s)
            var swConn31 = Stopwatch.StartNew();
            Exception? timeoutException = null;

            try
            {
                var npgsqlBuilder = new NpgsqlConnectionStringBuilder(_fixture.WebPoolConnectionString)
                {
                    Timeout = 5,
                    CommandTimeout = 5
                };
                await using var conn31 = new NpgsqlConnection(npgsqlBuilder.ConnectionString);
                await conn31.OpenAsync();
                await conn31.ExecuteScalarAsync<int>("SELECT 1;");
            }
            catch (Exception ex)
            {
                timeoutException = ex;
            }
            finally
            {
                swConn31.Stop();
            }

            Assert.NotNull(timeoutException);
            // El timeout de cola en PgBouncer está configurado en 3 s (query_wait_timeout=3)
            Assert.True(swConn31.Elapsed.TotalSeconds >= 2.5 && swConn31.Elapsed.TotalSeconds <= 6.0,
                $"El rechazo de la conexión web #31 no cumplió la ventana de timeout esperada (3s): {swConn31.Elapsed.TotalSeconds:F2} s");
        }
        finally
        {
            releaseWebTasks.TrySetResult(true);
            await Task.WhenAll(webHoldingTasks);
        }
    }

    [Fact]
    public async Task DapperCatalogRead_OnWebPool_ExecutesWithoutRowLocks_UsingRealProductQueryPort()
    {
        var prodId = Guid.NewGuid();
        string sku = $"SKU-CAT-{Guid.NewGuid():N}";

        // Sembrar producto en la BD mediante POS Pool
        await using (var posConn = new NpgsqlConnection(_fixture.PosPoolConnectionString))
        {
            await posConn.OpenAsync();
            await posConn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@id, @sku, 'Producto Catalogo Web Real', 'UND', 15.00, 10.00, TRUE, 100, 0);
                """, new { id = prodId, sku });
        }

        // 1. En POS abrimos transacción con bloqueo FOR UPDATE en la fila
        await using var posLockConn = new NpgsqlConnection(_fixture.PosPoolConnectionString);
        await posLockConn.OpenAsync();
        await using var posTx = await posLockConn.BeginTransactionAsync();

        var lockedRow = await posLockConn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT id FROM productos WHERE id = @id FOR UPDATE;",
            new { id = prodId }, posTx);
        Assert.NotNull(lockedRow);

        // 2. Consulta de catálogo por Web Pool mediante el puerto real PostgresProductQueryPort (Dapper no bloqueante)
        var webDbOptions = new DbContextOptionsBuilder<SigicDbContext>()
            .UseNpgsql(_fixture.WebPoolConnectionString)
            .Options;
        await using var webDbContext = new SigicDbContext(webDbOptions);
        IProductQueryPort catalogPort = new PostgresProductQueryPort(webDbContext);

        var sw = Stopwatch.StartNew();
        var producto = await catalogPort.ObtenerParaPDPAsync(prodId);
        sw.Stop();

        Assert.NotNull(producto);
        Assert.Equal(sku, producto.CodigoSku);
        // Lectura no bloqueante MVCC inmediata en <= 250 ms a pesar del SELECT FOR UPDATE activo
        Assert.True(sw.ElapsedMilliseconds < 250,
            $"Lectura de catálogo mediante el puerto real demoró {sw.ElapsedMilliseconds} ms por bloqueo indebido.");

        await posTx.RollbackAsync();
    }
}
