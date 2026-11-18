using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Handlers;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.SalesPOS.Commands;
using SIGIC.Application.SalesPOS.Handlers;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Infrastructure;
using SIGIC.Infrastructure.Adapters.CashManagement;
using SIGIC.Infrastructure.Adapters.SalesPOS;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Concurrency;

[Collection("PostgreSqlDatabase")]
public class DigitalPaymentAndShiftTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public DigitalPaymentAndShiftTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class ShiftTestUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "cajero_pos";
        public IReadOnlyList<string> Roles { get; set; } = new[] { "Cajero", "Superadmin" };
        public string TerminalScope { get; set; } = "POS-01";
        public string ClientIp { get; set; } = "127.0.0.1";

        public bool HasRole(string role) => Roles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
        public bool IsInRole(string role) => HasRole(role);
    }

    private IServiceProvider BuildServiceProvider(Guid almacenId)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
                ["ConnectionStrings:PosConnection"] = _fixture.PosPoolConnectionString,
                ["ConnectionStrings:WebConnection"] = _fixture.WebPoolConnectionString,
                ["JwtSettings:PublicKeyPem"] = PostgreSqlTestFixture.TestPublicKeyPem,
                ["WarehouseSettings:CentralWarehouseId"] = almacenId.ToString()
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddScoped<ShiftTestUserContext>();
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<ShiftTestUserContext>());

        services.AddSigicInfrastructure(config);
        return services.BuildServiceProvider();
    }

    private SigicDbContext CreateDbContext()
    {
        return _fixture.CreateDbContext();
    }

    [Fact]
    public async Task DigitalPayment_ConcurrentIdenticalCode_EnforcesUniqueness()
    {
        string metodo = "YAPE";
        string codigo = $"OP-{Guid.NewGuid():N}";
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow);

        int exitos = 0;
        int fallosDuplicado = 0;
        var barrier = new TaskCompletionSource<bool>();

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            await barrier.Task;
            await using var db = CreateDbContext();
            var port = new PostgresDigitalPaymentVerificationPort(db);

            try
            {
                await port.RegistrarCodigoOperacionAsync(metodo, codigo, fecha, Guid.NewGuid());
                Interlocked.Increment(ref exitos);
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                Interlocked.Increment(ref fallosDuplicado);
            }
        })).ToArray();

        barrier.SetResult(true);
        await Task.WhenAll(tasks);

        Assert.Equal(1, exitos);
        Assert.Equal(9, fallosDuplicado);
    }

    [Fact]
    public async Task CashShift_DoubleCloseRace_ExactlyOneWins()
    {
        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();

        await using var conn = await _fixture.CreateConnectionAsync();

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Shift', FALSE, 'Dir') ON CONFLICT DO NOTHING;",
            new { a = almacenId, c = $"ALM-S-{Guid.NewGuid().ToString()[..6]}" });

        await conn.ExecuteAsync("""
            INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc)
            VALUES (@t, gen_random_uuid(), @caj, @a, 150, 'Aperturado', NOW());
            """, new { t = turnoId, caj = cajeroId, a = almacenId });

        int cierresExitosos = 0;
        int cierresRechazados = 0;
        var barrier = new TaskCompletionSource<bool>();

        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await barrier.Task;
            await using var db = CreateDbContext();
            var salesRepo = new PostgresSalesPOSRepository(db);
            var repo = new PostgresCashRegisterRepository(db, salesRepo);

            bool cerrado = await repo.CerrarTurnoCajaConEstadoEsperadoAsync(turnoId, "Aperturado");
            if (cerrado)
                Interlocked.Increment(ref cierresExitosos);
            else
                Interlocked.Increment(ref cierresRechazados);
        })).ToArray();

        barrier.SetResult(true);
        await Task.WhenAll(tasks);

        Assert.Equal(1, cierresExitosos);
        Assert.Equal(1, cierresRechazados);

        string estadoFinal = await conn.ExecuteScalarAsync<string>(
            "SELECT estado FROM turnos_caja WHERE id = @t;", new { t = turnoId }) ?? "";
        Assert.Equal("Cerrado", estadoFinal);
    }

    [Fact]
    public async Task CashShift_BlindClose_CalculatesTheoreticalBalance_CountingInitialFundOnlyOnce_AndMixedPayments()
    {
        var almacenId = Guid.NewGuid();
        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        await using var conn = await _fixture.CreateConnectionAsync();

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Arqueo', FALSE, 'Dir');",
            new { a = almacenId, c = $"ALM-A-{Guid.NewGuid():N}" });

        // Turno con fondo inicial de 150
        await conn.ExecuteAsync("""
            INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc)
            VALUES (@t, gen_random_uuid(), @caj, @a, 150.00, 'Aperturado', NOW() - INTERVAL '3 hours');
            """, new { t = turnoId, caj = cajeroId, a = almacenId });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod Arqueo', 'UND', 10.00, 5.00, FALSE, 100, 0);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, 100, 0);
            """, new { a = almacenId, p = prodId, sku = $"SKU-ARQ-{Guid.NewGuid():N}" });

        var sp = BuildServiceProvider(almacenId);

        using (var scope = sp.CreateScope())
        {
            var userCtx = scope.ServiceProvider.GetRequiredService<ShiftTestUserContext>();
            userCtx.UserId = cajeroId;

            var posHandler = scope.ServiceProvider.GetRequiredService<RegistrarVentaPOSCommandHandler>();

            // Venta 1: 50.00 en Efectivo puro
            await posHandler.Handle(new RegistrarVentaPOSCommand(
                TurnoCajaId: turnoId,
                ClienteId: Guid.NewGuid(),
                Items: new[] { new VentaPOSItemDto(prodId, 5m, "UND", 10.00m) },
                Pagos: new[] { new VentaPOSPagoDto("Efectivo", 50.00m, null) },
                TipoComprobante: "Boleta"
            ));

            // Venta 2: 70.00 con Pago Mixto (30.00 Efectivo + 40.00 Yape)
            await posHandler.Handle(new RegistrarVentaPOSCommand(
                TurnoCajaId: turnoId,
                ClienteId: Guid.NewGuid(),
                Items: new[] { new VentaPOSItemDto(prodId, 7m, "UND", 10.00m) },
                Pagos: new[]
                {
                    new VentaPOSPagoDto("Efectivo", 30.00m, null),
                    new VentaPOSPagoDto("Yape", 40.00m, $"YAPE-{Guid.NewGuid():N}")
                },
                TipoComprobante: "Boleta"
            ));
        }

        // Cierre Ciego: el cajero declara conteo físico sin conocer el saldo teórico
        ArqueoResultDto arqueoResult;
        using (var scope = sp.CreateScope())
        {
            var userCtx = scope.ServiceProvider.GetRequiredService<ShiftTestUserContext>();
            userCtx.UserId = cajeroId;

            var arqueoHandler = scope.ServiceProvider.GetRequiredService<RegistrarArqueoCiegoCommandHandler>();
            var arqueoCmd = new RegistrarArqueoCiegoCommand(
                TurnoCajaId: turnoId,
                DesgloseFisico: new[]
                {
                    new ArqueoDenominacionDto(100m, 2), // 200 en efectivo físico
                    new ArqueoDenominacionDto(20m, 1),  // 20
                    new ArqueoDenominacionDto(10m, 1)   // 10 -> Total Efectivo: 230
                },
                TotalDigitalDeclarado: 40.00m, // 40 digital
                JustificacionDiscrepancia: "Cierre conforme"
            );

            arqueoResult = await arqueoHandler.Handle(arqueoCmd);
        }

        // Ceguera del cajero: el resultado devuelto al cajero NO debe exponer el saldo teórico
        Assert.NotEqual(Guid.Empty, arqueoResult.ArqueoId);

        // Verificación en base de datos:
        // Saldo teórico = fondo inicial (150) + ventas (50 + 70) = 270.00 (el fondo inicial contado EXACTAMENTE una sola vez)
        var row = await conn.QuerySingleAsync<dynamic>(
            "SELECT saldo_sistema_teorico, total_efectivo_declarado, total_digital_declarado FROM caja_arqueo WHERE id = @id;",
            new { id = arqueoResult.ArqueoId });

        decimal saldoTeorico = Convert.ToDecimal(row.saldo_sistema_teorico);
        Assert.Equal(270.00m, saldoTeorico);
    }

    [Fact]
    public async Task CashShift_ConcurrentSaleAndCloseRace_ConsistentFinalBalance()
    {
        var almacenId = Guid.NewGuid();
        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        await using var conn = await _fixture.CreateConnectionAsync();

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Race Shift', FALSE, 'Dir');",
            new { a = almacenId, c = $"ALM-RS-{Guid.NewGuid():N}" });

        await conn.ExecuteAsync("""
            INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc)
            VALUES (@t, gen_random_uuid(), @caj, @a, 100.00, 'Aperturado', NOW() - INTERVAL '1 hour');
            """, new { t = turnoId, caj = cajeroId, a = almacenId });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod Race Shift', 'UND', 10.00, 5.00, FALSE, 100, 0);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, 100, 0);
            """, new { a = almacenId, p = prodId, sku = $"SKU-RS-{Guid.NewGuid():N}" });

        var sp = BuildServiceProvider(almacenId);
        var barrier = new TaskCompletionSource<bool>();

        bool ventaExitosa = false;
        bool cierreExitoso = false;

        var taskVenta = Task.Run(async () =>
        {
            await barrier.Task;
            try
            {
                using var scope = sp.CreateScope();
                var userCtx = scope.ServiceProvider.GetRequiredService<ShiftTestUserContext>();
                userCtx.UserId = cajeroId;

                var posHandler = scope.ServiceProvider.GetRequiredService<RegistrarVentaPOSCommandHandler>();
                await posHandler.Handle(new RegistrarVentaPOSCommand(
                    TurnoCajaId: turnoId,
                    ClienteId: Guid.NewGuid(),
                    Items: new[] { new VentaPOSItemDto(prodId, 1m, "UND", 10.00m) },
                    Pagos: new[] { new VentaPOSPagoDto("Efectivo", 10.00m, null) },
                    TipoComprobante: "Boleta"
                ));
                ventaExitosa = true;
            }
            catch (ValidationException)
            {
                // El turno ya fue cerrado por la tarea de cierre
                ventaExitosa = false;
            }
        });

        var taskCierre = Task.Run(async () =>
        {
            await barrier.Task;
            using var scope = sp.CreateScope();
            var userCtx = scope.ServiceProvider.GetRequiredService<ShiftTestUserContext>();
            userCtx.UserId = cajeroId;

            var arqueoHandler = scope.ServiceProvider.GetRequiredService<RegistrarArqueoCiegoCommandHandler>();
            var arqueoCmd = new RegistrarArqueoCiegoCommand(
                TurnoCajaId: turnoId,
                DesgloseFisico: new[] { new ArqueoDenominacionDto(100m, 1) },
                TotalDigitalDeclarado: 0m,
                JustificacionDiscrepancia: "Cierre simultáneo"
            );
            await arqueoHandler.Handle(arqueoCmd);
            cierreExitoso = true;
        });

        barrier.SetResult(true);
        await Task.WhenAll(taskVenta, taskCierre);

        Assert.True(cierreExitoso, "El cierre de turno debe completarse");

        // Si la venta ganó, el saldo teórico es 110.00 (100 fondo + 10 venta).
        // Si el cierre ganó antes que la venta, la venta fue rechazada y el saldo teórico es 100.00.
        var saldoFinal = await conn.ExecuteScalarAsync<decimal>(
            "SELECT saldo_sistema_teorico FROM caja_arqueo WHERE turno_caja_id = @t;", new { t = turnoId });

        if (ventaExitosa)
        {
            Assert.Equal(110.00m, saldoFinal);
        }
        else
        {
            Assert.Equal(100.00m, saldoFinal);
        }
    }
}
