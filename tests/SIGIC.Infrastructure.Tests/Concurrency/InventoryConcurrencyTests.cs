using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Handlers;
using SIGIC.Application.SalesPOS.Commands;
using SIGIC.Application.SalesPOS.Handlers;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Domain.Exceptions;
using SIGIC.Infrastructure;
using SIGIC.Infrastructure.Adapters.Inventory;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Concurrency;

[Collection("PostgreSqlDatabase")]
public class InventoryConcurrencyTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public InventoryConcurrencyTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class ScopedTestUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "operador_test";
        public IReadOnlyList<string> Roles { get; set; } = new[] { "Cajero", "Superadmin" };
        public string TerminalScope { get; set; } = "POS-01";
        public string ClientIp { get; set; } = "127.0.0.1";

        public bool HasRole(string role) => Roles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
        public bool IsInRole(string role) => HasRole(role);
    }

    private IServiceProvider BuildServiceProvider(Guid centralAlmacenId)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
                ["ConnectionStrings:PosConnection"] = _fixture.PosPoolConnectionString,
                ["ConnectionStrings:WebConnection"] = _fixture.WebPoolConnectionString,
                ["JwtSettings:PublicKeyPem"] = PostgreSqlTestFixture.TestPublicKeyPem,
                ["WarehouseSettings:CentralWarehouseId"] = centralAlmacenId.ToString()
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddScoped<ScopedTestUserContext>();
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<ScopedTestUserContext>());

        services.AddSigicInfrastructure(config);
        return services.BuildServiceProvider();
    }

    private SigicDbContext CreateDbContext()
    {
        return _fixture.CreateDbContext();
    }

    [Fact]
    public async Task HavenderOrdering_InverseMultilineOrders_DoNotDeadlock()
    {
        var almacenId = Guid.NewGuid();
        var prodA = Guid.NewGuid();
        var prodB = Guid.NewGuid();

        if (prodA.CompareTo(prodB) > 0)
            (prodA, prodB) = (prodB, prodA);

        await using var conn = await _fixture.CreateConnectionAsync();

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Havender', FALSE, 'Dir') ON CONFLICT DO NOTHING;",
            new { a = almacenId, c = $"ALM-{Guid.NewGuid().ToString()[..6]}" });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
            VALUES (@pA, @skuA, 'Prod A', 'UND', 10, 5, TRUE, 50, 0),
                   (@pB, @skuB, 'Prod B', 'UND', 20, 10, TRUE, 50, 0);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @pA, 50, 0),
                   (gen_random_uuid(), @a, @pB, 50, 0);
            """, new { a = almacenId, pA = prodA, pB = prodB, skuA = $"SKU-A-{Guid.NewGuid():N}", skuB = $"SKU-B-{Guid.NewGuid():N}" });

        var barrier = new TaskCompletionSource<bool>();

        var task1 = Task.Run(async () =>
        {
            await using var db1 = CreateDbContext();
            var port1 = new PostgresInventoryStockPort(db1);
            await using var tx1 = await db1.Database.BeginTransactionAsync();

            await barrier.Task;
            var locked = await port1.ObtenerParaBloqueoOrdenadosAsync(almacenId, new[] { prodA, prodB });
            Assert.Equal(2, locked.Count);
            await Task.Delay(20);
            await tx1.CommitAsync();
            return true;
        });

        var task2 = Task.Run(async () =>
        {
            await using var db2 = CreateDbContext();
            var port2 = new PostgresInventoryStockPort(db2);
            await using var tx2 = await db2.Database.BeginTransactionAsync();

            await barrier.Task;
            var locked = await port2.ObtenerParaBloqueoOrdenadosAsync(almacenId, new[] { prodB, prodA });
            Assert.Equal(2, locked.Count);
            await Task.Delay(20);
            await tx2.CommitAsync();
            return true;
        });

        barrier.SetResult(true);
        var results = await Task.WhenAll(task1, task2);

        Assert.True(results[0]);
        Assert.True(results[1]);
    }

    [Fact]
    public async Task HundredConcurrentOperations_OnFiveUnits_ExactlyFiveSucceedAndNinetyFiveRejected()
    {
        var almacenCentralId = Guid.NewGuid();
        var productoId = Guid.NewGuid();
        const decimal stockInicial = 5m;

        await using var conn = await _fixture.CreateConnectionAsync();

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Almacen Central Race', TRUE, 'Av Central 123');",
            new { a = almacenCentralId, c = $"ALM-R-{Guid.NewGuid():N}" });

        string sku = $"SKU-R-{Guid.NewGuid():N}";
        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod Race Handlers', 'UND', 10, 5, TRUE, @stockInicial, 0);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, @stockInicial, 0);
            """, new { a = almacenCentralId, p = productoId, sku, stockInicial });

        // Crear 50 cajeros y turnos para los 50 POS handlers independientes
        var cajeros = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var turnos = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();

        for (int i = 0; i < 50; i++)
        {
            await conn.ExecuteAsync("""
                INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc)
                VALUES (@t, gen_random_uuid(), @c, @a, 100, 'Aperturado', NOW());
                """, new { t = turnos[i], c = cajeros[i], a = almacenCentralId });
        }

        var sp = BuildServiceProvider(almacenCentralId);

        // Verificación explícita de segregación de pools en DI (pos_pool vs web_pool)
        var poolOptions = sp.GetRequiredService<DatabasePoolOptions>();
        Assert.NotEqual(poolOptions.PosConnection, poolOptions.WebConnection);
        Assert.Contains("pos_pool", poolOptions.PosConnection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("web_pool", poolOptions.WebConnection, StringComparison.OrdinalIgnoreCase);

        using var readyBarrier = new CountdownEvent(100);
        var goSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int exitosPOS = 0;
        int exitosWeb = 0;
        int rechazos = 0;

        // Concurrencia real sin límites artificiales: 100 tareas simultáneas sincronizadas por CountdownEvent
        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            readyBarrier.Signal();
            await goSignal.Task;

            try
            {
                using var scope = sp.CreateScope();
                var userCtx = scope.ServiceProvider.GetRequiredService<ScopedTestUserContext>();
                bool isPos = (i % 2 == 0);

                if (isPos)
                {
                    int posIdx = i / 2;
                    userCtx.UserId = cajeros[posIdx];
                    userCtx.Roles = new[] { "Cajero", "Superadmin" };

                    var handler = scope.ServiceProvider.GetRequiredService<RegistrarVentaPOSCommandHandler>();
                    var cmd = new RegistrarVentaPOSCommand(
                        TurnoCajaId: turnos[posIdx],
                        ClienteId: Guid.NewGuid(),
                        Items: new[] { new VentaPOSItemDto(productoId, 1m, "UND", 10m) },
                        Pagos: new[] { new VentaPOSPagoDto("Efectivo", 10m, null) },
                        TipoComprobante: "Boleta"
                    );

                    await handler.Handle(cmd);
                    Interlocked.Increment(ref exitosPOS);
                }
                else
                {
                    userCtx.UserId = Guid.NewGuid();
                    userCtx.Roles = new[] { "ClienteWeb" };

                    var handler = scope.ServiceProvider.GetRequiredService<ReservarPedidoWebCommandHandler>();
                    var cmd = new ReservarPedidoWebCommand(
                        ClienteId: userCtx.UserId,
                        MetodoPago: "PasarelaTarjeta",
                        ModalidadEntrega: "DespachoDomicilio",
                        TiendaDestinoId: null,
                        Items: new[] { new PedidoWebItemDto(productoId, 1m, 10m, "UND", 1.0m, 1m) }
                    );

                    await handler.Handle(cmd);
                    Interlocked.Increment(ref exitosWeb);
                }
            }
            catch (StockInsuficienteException)
            {
                Interlocked.Increment(ref rechazos);
            }
        })).ToArray();

        // Esperar a que los 100 hilos (50 POS + 50 Web) alcancen la barrera antes del disparo atómico
        readyBarrier.Wait();
        goSignal.SetResult();
        await Task.WhenAll(tasks);

        // Verificación rigurosa de invariantes de concurrencia (RNF-CON-003, RF-WEB-005)
        Assert.Equal(5, exitosPOS + exitosWeb);
        Assert.Equal(95, rechazos);

        // Verificar consistencia exacta en base de datos: exactamente 0 unidades disponibles
        var stockFinal = await conn.QuerySingleAsync<dynamic>(
            "SELECT stock_fisico, stock_comprometido FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = almacenCentralId, p = productoId });

        decimal fisico = Convert.ToDecimal(stockFinal.stock_fisico);
        decimal comprometido = Convert.ToDecimal(stockFinal.stock_comprometido);

        Assert.Equal(5m - exitosPOS, fisico);
        Assert.Equal((decimal)exitosWeb, comprometido);
        Assert.Equal(0m, fisico - comprometido); // Cero sobreventas
    }
}
