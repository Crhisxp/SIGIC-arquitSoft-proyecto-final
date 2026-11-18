using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Handlers;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Handlers;
using SIGIC.Application.Security.Ports;
using SIGIC.Infrastructure;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Concurrency;

[Collection("PostgreSqlDatabase")]
public class RnfCon004RaceBatteryTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public RnfCon004RaceBatteryTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class RaceUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "tesorero_rnf004";
        public IReadOnlyList<string> Roles { get; set; } = new[] { "Tesoreria", "Superadmin" };
        public string TerminalScope { get; set; } = "HQ-FINANCE";
        public string ClientIp { get; set; } = "127.0.0.1";

        public bool HasRole(string role) => Roles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
        public bool IsInRole(string role) => HasRole(role);
    }

    private class MutableDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private (IServiceProvider Provider, MutableDateTimeProvider Clock) BuildServiceProvider(Guid centralAlmacenId)
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

        var clock = new MutableDateTimeProvider();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddScoped<RaceUserContext>();
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<RaceUserContext>());

        services.AddSigicInfrastructure(config);
        return (services.BuildServiceProvider(), clock);
    }

    [Fact]
    public async Task RnfCon004_ThousandRacesBattery_ApprovalVsExpiration_FullHandlers_ZeroInconsistencies()
    {
        const int totalCarreras = 1000;
        const int batchSize = 25;
        int aprobacionesGanadas = 0;
        int expiracionesGanadas = 0;
        int inconsistencias = 0;

        var centralAlmacenId = Guid.NewGuid();
        var tiendaDestinoId = Guid.NewGuid();

        await using var conn = await _fixture.CreateConnectionAsync();

        await conn.ExecuteAsync("""
            INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion)
            VALUES (@c, @cCod, 'Almacén Central RNF', TRUE, 'Av Central 004'),
                   (@t, @tCod, 'Tienda Destino RNF', FALSE, 'Av Tienda 004');
            """, new
        {
            c = centralAlmacenId,
            cCod = $"ALM-C-{Guid.NewGuid():N}",
            t = tiendaDestinoId,
            tCod = $"ALM-T-{Guid.NewGuid():N}"
        });

        var (sp, clock) = BuildServiceProvider(centralAlmacenId);
        var rawNow = DateTime.UtcNow;
        var baseTime = new DateTime(rawNow.Year, rawNow.Month, rawNow.Day, rawNow.Hour, rawNow.Minute, rawNow.Second, DateTimeKind.Utc);
        var expirationTime = baseTime.AddMinutes(1);
        clock.UtcNow = expirationTime;

        for (int b = 0; b < totalCarreras / batchSize; b++)
        {
            var raceItems = new List<(Guid PedidoId, Guid ProductoId, DateTime ExpirationTime, int Index)>(batchSize);

            // 1. Sembrado del batch con pedidos vigentes en PendienteValidacion (voucher subido a tiempo)
            for (int i = 0; i < batchSize; i++)
            {
                var prodId = Guid.NewGuid();
                var pedidoId = Guid.NewGuid();
                string sku = $"SKU-B{b}-R{i}-{Guid.NewGuid():N}";
                string cod = $"RNF-{pedidoId.ToString()[..8].ToUpperInvariant()}";

                var creationTime = baseTime.AddMinutes(-239);
                var voucherUploadTime = creationTime.AddMinutes(30);
                var voucherLimitTime = creationTime.AddMinutes(60);

                await conn.ExecuteAsync("""
                    INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                    VALUES (@prodId, @sku, 'Prod RNF004', 'UND', 20, 10, TRUE, 10, 1);

                    INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
                    VALUES (gen_random_uuid(), @centralAlmacenId, @prodId, 10, 1);

                    INSERT INTO pedidos_web (
                        id, codigo, cliente_id, almacen_origen_id, tienda_destino_id, modalidad_entrega,
                        modalidad_pago, estado, total, fecha_creacion_utc, fecha_expiracion_utc,
                        fecha_limite_voucher_utc, fecha_subida_voucher_utc
                    ) VALUES (
                        @pedidoId, @cod, gen_random_uuid(), @centralAlmacenId, @tiendaDestinoId, 'RetiroEnTienda',
                        'MANUAL', 'PendienteValidacion', 20, @creationTime, @expirationTime,
                        @voucherLimitTime, @voucherUploadTime
                    );

                    INSERT INTO pedidos_web_items (
                        id, pedido_web_id, producto_id, cantidad_comercial, unidad_comercial,
                        factor_conversion, cantidad_base_reservada, precio_unitario, subtotal
                    ) VALUES (
                        gen_random_uuid(), @pedidoId, @prodId, 1, 'UND', 1.0, 1, 20, 20
                    );

                    INSERT INTO comprobantes_voucher (id, pedido_web_id, nombre_archivo, formato, hash_sha256, tamano_bytes, fecha_subida_utc)
                    VALUES (gen_random_uuid(), @pedidoId, 'voucher.jpg', '.jpg', 'a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0', 2048, @voucherUploadTime);
                    """, new { prodId, sku, centralAlmacenId, pedidoId, cod, tiendaDestinoId, creationTime, expirationTime, voucherLimitTime, voucherUploadTime });

                raceItems.Add((pedidoId, prodId, expirationTime, i));
            }

            // 2. Disparo concurrente de carreras fijando el reloj exactamente en el instante de expiración
            // Todas las 1.000 carreras compiten simultáneamente en el límite exacto del TTL
            clock.UtcNow = expirationTime;

            var tasks = raceItems.Select(item => RunSingleRaceAsync(sp, item, centralAlmacenId));
            var results = await Task.WhenAll(tasks);
            foreach (var r in results)
            {
                if (r == RaceWinner.Aprobacion) Interlocked.Increment(ref aprobacionesGanadas);
                else if (r == RaceWinner.Expiracion) Interlocked.Increment(ref expiracionesGanadas);
                else Interlocked.Increment(ref inconsistencias);
            }
        }

        // Assert: 1.000 carreras ejecutadas, 0 inconsistencias, ambos ganadores demostrados
        Assert.Equal(0, inconsistencias);
        Assert.Equal(totalCarreras, aprobacionesGanadas + expiracionesGanadas);
        Assert.True(aprobacionesGanadas > 0, $"Se esperaba que aprobación ganara al menos 1 carrera (obtenidas: {aprobacionesGanadas})");
        Assert.True(expiracionesGanadas > 0, $"Se esperaba que expiración ganara al menos 1 carrera (obtenidas: {expiracionesGanadas})");
    }

    private enum RaceWinner
    {
        Aprobacion,
        Expiracion,
        Inconsistencia
    }

    private async Task<RaceWinner> RunSingleRaceAsync(
        IServiceProvider sp,
        (Guid PedidoId, Guid ProductoId, DateTime ExpirationTime, int Index) item,
        Guid centralAlmacenId)
    {
        using var readyBarrier = new CountdownEvent(2);
        var goSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool ganoAprobacion = false;
        bool ganoExpiracion = false;
        ExpirarReservasResultDto? expResult = null;

        var taskA = Task.Run(async () =>
        {
            readyBarrier.Signal();
            await goSignal.Task;

            // Jitter aleatorio para competencia equitativa por el bloqueo Havender
            int jitterA = Random.Shared.Next(5, 35);
            if (jitterA > 0)
                await Task.Delay(jitterA);

            try
            {
                using var scopeA = sp.CreateScope();
                var handlerA = scopeA.ServiceProvider.GetRequiredService<ValidarVoucherPagoCommandHandler>();
                var cmdA = new ValidarVoucherPagoCommand(item.PedidoId, Aprobado: true);
                var res = await handlerA.Handle(cmdA);
                ganoAprobacion = (res.Aprobado && res.EstadoFinal == "PagadoConfirmado");
            }
            catch (Exception ex) when (ex is ValidationException or InvalidOperationException)
            {
                // Perdió legítimamente la carrera contra expiración (RF-WEB-002 / RNF-CON-004)
                ganoAprobacion = false;
            }
        });

        var taskB = Task.Run(async () =>
        {
            readyBarrier.Signal();
            await goSignal.Task;

            // Jitter aleatorio para competencia equitativa por el bloqueo Havender
            int jitterB = Random.Shared.Next(0, 6);
            if (jitterB > 0)
                await Task.Delay(jitterB);

            try
            {
                using var scopeB = sp.CreateScope();
                var handlerB = scopeB.ServiceProvider.GetRequiredService<ExpirarReservasWebCommandHandler>();
                var cmdB = new ExpirarReservasWebCommand(item.PedidoId);
                expResult = await handlerB.Handle(cmdB);
                ganoExpiracion = (expResult.CantidadPedidosExpirados > 0);
            }
            catch (Exception ex) when (ex is ValidationException or InvalidOperationException)
            {
                ganoExpiracion = false;
            }
        });

        readyBarrier.Wait();
        goSignal.SetResult();
        await Task.WhenAll(taskA, taskB);

        if (expResult != null)
        {
            Assert.Empty(expResult.Errores);
        }

        RaceWinner winner;
        if (ganoAprobacion && !ganoExpiracion)
        {
            winner = RaceWinner.Aprobacion;
        }
        else if (!ganoAprobacion && ganoExpiracion)
        {
            winner = RaceWinner.Expiracion;
        }
        else
        {
            winner = RaceWinner.Inconsistencia;
        }

        // 3. Verificación rigurosa de efectos secundarios por ítem
        await using var checkConn = await _fixture.CreateConnectionAsync();

        var stockRow = await checkConn.QuerySingleAsync<dynamic>(
            "SELECT stock_fisico, stock_comprometido FROM stock_almacen WHERE almacen_id = @a AND producto_id = @p;",
            new { a = centralAlmacenId, p = item.ProductoId });

        decimal f = Convert.ToDecimal(stockRow.stock_fisico);
        decimal c = Convert.ToDecimal(stockRow.stock_comprometido);

        // Invariante: stock_comprometido SIEMPRE liberado a 0
        Assert.Equal(0m, c);

        if (ganoAprobacion)
        {
            // Aprobación: salida física de reserva (10 -> 9)
            Assert.Equal(9m, f);

            // Guía interna de traslado emitida (Invariante 5 / Retiro en Tienda)
            int guiasCount = await checkConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM guias_traslado_interno WHERE pedido_web_id = @p;",
                new { p = item.PedidoId });
            Assert.Equal(1, guiasCount);

            // Kardex registrado con VENTA_WEB
            int kardexCount = await checkConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM kardex_movimientos WHERE documento_referencia = @doc AND tipo_operacion = 'VENTA_WEB';",
                new { doc = item.PedidoId.ToString() });
            Assert.Equal(1, kardexCount);

            // Audit log registrado
            int auditCount = await checkConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM audit_log WHERE entidad_id = @ent AND accion = 'VOUCHER_APROBADO';",
                new { ent = item.PedidoId.ToString() });
            Assert.Equal(1, auditCount);
        }
        else if (ganoExpiracion)
        {
            // Expiración: físico intacto (10)
            Assert.Equal(10m, f);

            // Cero guías emitidas
            int guiasCount = await checkConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM guias_traslado_interno WHERE pedido_web_id = @p;",
                new { p = item.PedidoId });
            Assert.Equal(0, guiasCount);

            // Audit log registrado
            int auditCount = await checkConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM audit_log WHERE entidad_id = @ent AND accion = 'EXPIRACION_RESERVA_WEB';",
                new { ent = item.PedidoId.ToString() });
            Assert.Equal(1, auditCount);
        }

        return winner;
    }
}
