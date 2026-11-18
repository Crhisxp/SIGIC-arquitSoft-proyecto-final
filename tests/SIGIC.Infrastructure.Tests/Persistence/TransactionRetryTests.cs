using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Persistence;

[Collection("PostgreSqlDatabase")]
public class TransactionRetryTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public TransactionRetryTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteInTransactionWithRetryAsync_StatementTimeout57014_RecoversCleanly()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var uow = new UnitOfWork(dbContext, "sigic_pos_pool");

        int attempts = 0;
        var result = await uow.ExecuteInTransactionWithRetryAsync(async ct =>
        {
            attempts++;
            var conn = dbContext.Database.GetDbConnection();
            var tx = dbContext.Database.CurrentTransaction?.GetDbTransaction();

            if (attempts == 1)
            {
                // Provocar SQLSTATE 57014 (query_canceled por statement_timeout)
                await conn.ExecuteAsync("SET LOCAL statement_timeout = '40ms';", transaction: tx);
                await conn.ExecuteAsync("SELECT pg_sleep(0.25);", transaction: tx);
            }

            var val = await conn.ExecuteScalarAsync<int>("SELECT 42;", transaction: tx);
            return val;
        });

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteInTransactionWithRetryAsync_Deadlock40P01_RecoversCleanlyWithoutDuplicates()
    {
        var almacenId = Guid.NewGuid();
        var prodA = Guid.NewGuid();
        var prodB = Guid.NewGuid();

        await using var initConn = await _fixture.CreateConnectionAsync();
        await initConn.ExecuteAsync("""
            INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion)
            VALUES (@a, @c, 'Almacen Deadlock', TRUE, 'Av Retry') ON CONFLICT DO NOTHING;

            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
            VALUES (@pA, @skuA, 'P A Deadlock', 'UND', 10, 5, TRUE, 100, 0),
                   (@pB, @skuB, 'P B Deadlock', 'UND', 10, 5, TRUE, 100, 0);

            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @pA, 100, 0),
                   (gen_random_uuid(), @a, @pB, 100, 0);
            """, new
        {
            a = almacenId,
            c = $"ALM-DLK-{Guid.NewGuid():N}",
            pA = prodA,
            pB = prodB,
            skuA = $"SKU-DLK-A-{Guid.NewGuid():N}",
            skuB = $"SKU-DLK-B-{Guid.NewGuid():N}"
        });

        var barrier1 = new TaskCompletionSource<bool>();
        var barrier2 = new TaskCompletionSource<bool>();
        int tx1Attempts = 0;
        int tx2Attempts = 0;

        // TX1 bloquea A luego B
        var task1 = Task.Run(async () =>
        {
            await using var db1 = _fixture.CreateDbContext();
            var uow1 = new UnitOfWork(db1, "sigic_pos_pool");

            return await uow1.ExecuteInTransactionWithRetryAsync(async ct =>
            {
                tx1Attempts++;
                var conn = db1.Database.GetDbConnection();
                var tx = db1.Database.CurrentTransaction?.GetDbTransaction();

                // 1. Bloquear A
                await conn.ExecuteAsync("SELECT id FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p FOR UPDATE;",
                    new { a = almacenId, p = prodA }, tx);

                // Señalizar que A está bloqueado y esperar a que TX2 bloquee B
                barrier1.TrySetResult(true);
                await barrier2.Task;

                // Pequeña pausa para asegurar que TX2 ya esté listo para intentar bloquear A
                await Task.Delay(25, ct);

                // 2. Intentar bloquear B (cruzado)
                await conn.ExecuteAsync("SELECT id FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p FOR UPDATE;",
                    new { a = almacenId, p = prodB }, tx);

                await conn.ExecuteAsync("UPDATE stock_almacen SET stock_fisico = stock_fisico - 1 WHERE almacen_id = @a AND producto_id = @p;",
                    new { a = almacenId, p = prodA }, tx);

                return "TX1_OK";
            });
        });

        // TX2 bloquea B luego A (orden inverso intencional para forzar 40P01)
        var task2 = Task.Run(async () =>
        {
            await using var db2 = _fixture.CreateDbContext();
            var uow2 = new UnitOfWork(db2, "sigic_pos_pool");

            return await uow2.ExecuteInTransactionWithRetryAsync(async ct =>
            {
                tx2Attempts++;
                var conn = db2.Database.GetDbConnection();
                var tx = db2.Database.CurrentTransaction?.GetDbTransaction();

                // Esperar a que TX1 bloquee A antes de bloquear B
                await barrier1.Task;

                // 1. Bloquear B
                await conn.ExecuteAsync("SELECT id FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p FOR UPDATE;",
                    new { a = almacenId, p = prodB }, tx);

                // Señalizar que B está bloqueado para que TX1 intente bloquear B
                barrier2.TrySetResult(true);

                // Pequeña pausa para que TX1 alcance su intento de bloqueo sobre B
                await Task.Delay(25, ct);

                // 2. Intentar bloquear A (cruzado, dispara 40P01 en una de las dos)
                await conn.ExecuteAsync("SELECT id FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p FOR UPDATE;",
                    new { a = almacenId, p = prodA }, tx);

                await conn.ExecuteAsync("UPDATE stock_almacen SET stock_fisico = stock_fisico - 1 WHERE almacen_id = @a AND producto_id = @p;",
                    new { a = almacenId, p = prodB }, tx);

                return "TX2_OK";
            });
        });

        var results = await Task.WhenAll(task1, task2);
        Assert.Equal("TX1_OK", results[0]);
        Assert.Equal("TX2_OK", results[1]);

        // Una de las dos transacciones debió reintentar tras ser víctima del deadlock detector
        Assert.True(tx1Attempts > 1 || tx2Attempts > 1, $"Se esperaba que al menos una transacción reintentara por 40P01 (tx1: {tx1Attempts}, tx2: {tx2Attempts})");

        // Verificar que ambas operaciones se aplicaron limpiamente exactamente una vez (sin duplicados)
        await using var checkConn = await _fixture.CreateConnectionAsync();
        var stockA = await checkConn.ExecuteScalarAsync<decimal>(
            "SELECT stock_fisico FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = almacenId, p = prodA });
        var stockB = await checkConn.ExecuteScalarAsync<decimal>(
            "SELECT stock_fisico FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = almacenId, p = prodB });

        Assert.Equal(99m, stockA);
        Assert.Equal(99m, stockB);
    }
}
