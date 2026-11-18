using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Infrastructure;
using SIGIC.Infrastructure.BackgroundServices;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.BackgroundServices;

[Collection("PostgreSqlDatabase")]
public class ReservationExpirationBackgroundServiceTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public ReservationExpirationBackgroundServiceTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class TestClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private (IServiceProvider Provider, TestClock Clock) BuildServiceProvider()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
                ["ConnectionStrings:PosConnection"] = _fixture.PosPoolConnectionString,
                ["ConnectionStrings:WebConnection"] = _fixture.WebPoolConnectionString,
                ["JwtSettings:PublicKeyPem"] = PostgreSqlTestFixture.TestPublicKeyPem
            })
            .Build();

        var clock = new TestClock();
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddSigicInfrastructure(config);
        return (services.BuildServiceProvider(), clock);
    }

    [Fact]
    public async Task WorkerCycle_ExpiresGatewayOrderAt20Min_AndManualWithoutVoucherAt60Min()
    {
        var (sp, clock) = BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = NullLogger<ReservationExpirationBackgroundService>.Instance;

        var worker = new ReservationExpirationBackgroundService(scopeFactory, logger, TimeSpan.FromSeconds(1));

        await using var conn = await _fixture.CreateConnectionAsync();

        var almacenId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        string almCod = $"ALM-W-{Guid.NewGuid():N}";
        string prodSku = $"SKU-W-{Guid.NewGuid():N}";

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Worker', TRUE, 'Dir');",
            new { a = almacenId, c = almCod });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod Worker', 'UND', 10, 5, 20, 2);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, 20, 2);
            """, new { a = almacenId, p = prodId, sku = prodSku });

        var now = clock.UtcNow;

        // Pedido 1: Pasarela vencido (> 20 min)
        var p1 = Guid.NewGuid();
        var item1 = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);
        var pedido1 = new PedidoWebDto(p1, Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null, almacenId,
            now.AddMinutes(-25), now.AddMinutes(-5), null, null, "PendientePago", new[] { item1 });

        // Pedido 2: Manual vencido sin voucher (> 60 min)
        var p2 = Guid.NewGuid();
        var item2 = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);
        var pedido2 = new PedidoWebDto(p2, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, almacenId,
            now.AddMinutes(-70), now.AddMinutes(170), now.AddMinutes(-10), null, "PendientePago", new[] { item2 });

        using (var scope = scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IWebOrderRepository>();
            await repo.GuardarPedidoAsync(pedido1);
            await repo.GuardarPedidoAsync(pedido2);
        }

        // Ejecutar ciclo del worker
        int expirados = await worker.ExecuteCycleAsync();
        Assert.True(expirados >= 2);

        // Verificar estado de los pedidos
        string estadoP1 = await conn.ExecuteScalarAsync<string>("SELECT estado FROM pedidos_web WHERE id = @p1;", new { p1 }) ?? "";
        string estadoP2 = await conn.ExecuteScalarAsync<string>("SELECT estado FROM pedidos_web WHERE id = @p2;", new { p2 }) ?? "";
        Assert.Equal("Expirado", estadoP1);
        Assert.Equal("Expirado", estadoP2);

        // Verificar que stock_comprometido se liberó en stock_almacen (de 2 a 0)
        decimal stockComprometidoFinal = await conn.ExecuteScalarAsync<decimal>(
            "SELECT stock_comprometido FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = almacenId, p = prodId });

        Assert.Equal(0m, stockComprometidoFinal);
    }

    [Fact]
    public async Task WorkerCycle_ManualOrderWithVoucher_DoesNotExpireAt60Min_ExpiresAt240Min()
    {
        var (sp, clock) = BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = NullLogger<ReservationExpirationBackgroundService>.Instance;

        var worker = new ReservationExpirationBackgroundService(scopeFactory, logger, TimeSpan.FromSeconds(1));

        await using var conn = await _fixture.CreateConnectionAsync();

        var almacenId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        string almCod = $"ALM-MV-{Guid.NewGuid():N}";
        string prodSku = $"SKU-MV-{Guid.NewGuid():N}";

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Manual Voucher', TRUE, 'Dir');",
            new { a = almacenId, c = almCod });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod Manual Voucher', 'UND', 10, 5, 20, 1);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, 20, 1);
            """, new { a = almacenId, p = prodId, sku = prodSku });

        var t0 = DateTime.UtcNow;
        var pId = Guid.NewGuid();
        var item = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);

        // Pedido Manual con voucher subido a los 30 min (vigente hasta 240 min)
        var pedido = new PedidoWebDto(
            pId, Guid.NewGuid(), "Yape", "DespachoDomicilio", null, almacenId,
            FechaCreacionUtc: t0,
            FechaExpiracionUtc: t0.AddMinutes(240),
            FechaLimiteVoucherUtc: t0.AddMinutes(60),
            FechaSubidaVoucherUtc: t0.AddMinutes(30),
            Estado: "PendienteValidacion",
            Items: new[] { item });

        using (var scope = scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IWebOrderRepository>();
            await repo.GuardarPedidoAsync(pedido);
        }

        // 1. Simular reloj a los 65 min: NO debe expirar porque el voucher fue subido a tiempo
        clock.UtcNow = t0.AddMinutes(65);
        int expiradosMin65 = await worker.ExecuteCycleAsync();

        string estadoMin65 = await conn.ExecuteScalarAsync<string>(
            "SELECT estado FROM pedidos_web WHERE id = @pId;", new { pId }) ?? "";
        Assert.Equal("PendienteValidacion", estadoMin65);

        decimal stockCompMin65 = await conn.ExecuteScalarAsync<decimal>(
            "SELECT stock_comprometido FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = almacenId, p = prodId });
        Assert.Equal(1m, stockCompMin65);

        // 2. Simular reloj a los 241 min: DEBE expirar por alcanzar el TTL absoluto de 240 min
        clock.UtcNow = t0.AddMinutes(241);
        int expiradosMin241 = await worker.ExecuteCycleAsync();
        Assert.True(expiradosMin241 >= 1);

        string estadoMin241 = await conn.ExecuteScalarAsync<string>(
            "SELECT estado FROM pedidos_web WHERE id = @pId;", new { pId }) ?? "";
        Assert.Equal("Expirado", estadoMin241);

        decimal stockCompMin241 = await conn.ExecuteScalarAsync<decimal>(
            "SELECT stock_comprometido FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = almacenId, p = prodId });
        Assert.Equal(0m, stockCompMin241);
    }

    [Fact]
    public async Task TwoConcurrentWorkers_ExecuteCycle_DoNotDoubleLiberateStock()
    {
        var (sp, _) = BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = NullLogger<ReservationExpirationBackgroundService>.Instance;

        var worker1 = new ReservationExpirationBackgroundService(scopeFactory, logger, TimeSpan.FromSeconds(1));
        var worker2 = new ReservationExpirationBackgroundService(scopeFactory, logger, TimeSpan.FromSeconds(1));

        await using var conn = await _fixture.CreateConnectionAsync();

        var almacenId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        string almCod = $"ALM-RW-{Guid.NewGuid():N}";
        string prodSku = $"SKU-RW-{Guid.NewGuid():N}";

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm RaceW', TRUE, 'Dir');",
            new { a = almacenId, c = almCod });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod RaceW', 'UND', 10, 5, 20, 1);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, 20, 1);
            """, new { a = almacenId, p = prodId, sku = prodSku });

        var now = DateTime.UtcNow;
        var pId = Guid.NewGuid();
        var item = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);
        var pedido = new PedidoWebDto(pId, Guid.NewGuid(), "PasarelaTarjeta", "DespachoDomicilio", null, almacenId,
            now.AddMinutes(-30), now.AddMinutes(-10), null, null, "PendientePago", new[] { item });

        using (var scope = scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IWebOrderRepository>();
            await repo.GuardarPedidoAsync(pedido);
        }

        var barrier = new TaskCompletionSource<bool>();
        var task1 = Task.Run(async () =>
        {
            await barrier.Task;
            return await worker1.ExecuteCycleAsync();
        });
        var task2 = Task.Run(async () =>
        {
            await barrier.Task;
            return await worker2.ExecuteCycleAsync();
        });

        barrier.SetResult(true);
        var counts = await Task.WhenAll(task1, task2);

        // Idempotencia: el pedido se procesó exactamente una vez
        Assert.True(counts[0] + counts[1] >= 1);

        string estadoFinal = await conn.ExecuteScalarAsync<string>(
            "SELECT estado FROM pedidos_web WHERE id = @pId;", new { pId }) ?? "";
        Assert.Equal("Expirado", estadoFinal);

        decimal stockComprometidoFinal = await conn.ExecuteScalarAsync<decimal>(
            "SELECT stock_comprometido FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = almacenId, p = prodId });

        Assert.Equal(0m, stockComprometidoFinal);
    }
}
