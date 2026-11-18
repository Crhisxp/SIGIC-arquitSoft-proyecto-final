using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Infrastructure.Adapters.OrdersWeb;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Adapters;

[Collection("PostgreSqlDatabase")]
public class WebOrderExpirationAdapterTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public WebOrderExpirationAdapterTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    private SigicDbContext CreateDbContext()
    {
        return _fixture.CreateDbContext();
    }

    [Fact]
    public async Task ObtenerPedidosExpiradosAsync_EvaluatesGatewayAndManualTtlBoundaries()
    {
        await using var conn = await _fixture.CreateConnectionAsync();

        var almacenId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        string almCod = $"ALM-EXP-{Guid.NewGuid():N}";
        string prodSku = $"SKU-EXP-{Guid.NewGuid():N}";

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Exp Adapter', TRUE, 'Dir');",
            new { a = almacenId, c = almCod });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod Exp Adapter', 'UND', 10, 5, 50, 10);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, 50, 10);
            """, new { a = almacenId, p = prodId, sku = prodSku });

        var t0 = DateTime.UtcNow;

        // 1. Pasarela: expiración a los 20 min
        var idPasarela = Guid.NewGuid();
        var itemPasarela = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);
        var pedidoPasarela = new PedidoWebDto(
            idPasarela, Guid.NewGuid(), "PASARELA", "DespachoDomicilio", null, almacenId,
            t0, t0.AddMinutes(20), null, null, "PendientePago", new[] { itemPasarela });

        // 2. Manual sin voucher: límite voucher 60 min, TTL total 240 min
        var idManualSinVoucher = Guid.NewGuid();
        var itemManualSin = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);
        var pedidoManualSinVoucher = new PedidoWebDto(
            idManualSinVoucher, Guid.NewGuid(), "MANUAL", "DespachoDomicilio", null, almacenId,
            t0, t0.AddMinutes(240), t0.AddMinutes(60), null, "PendientePago", new[] { itemManualSin });

        // 3. Manual con voucher subido a los 30 min: pasa a PendienteValidacion
        var idManualConVoucher = Guid.NewGuid();
        var itemManualCon = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);
        var pedidoManualConVoucher = new PedidoWebDto(
            idManualConVoucher, Guid.NewGuid(), "MANUAL", "DespachoDomicilio", null, almacenId,
            t0, t0.AddMinutes(240), t0.AddMinutes(60), t0.AddMinutes(30), "PendienteValidacion", new[] { itemManualCon });

        await using (var db = CreateDbContext())
        {
            var repo = new PostgresWebOrderRepository(db);
            await repo.GuardarPedidoAsync(pedidoPasarela);
            await repo.GuardarPedidoAsync(pedidoManualSinVoucher);
            await repo.GuardarPedidoAsync(pedidoManualConVoucher);
        }

        await using (var db = CreateDbContext())
        {
            var repo = new PostgresWebOrderRepository(db);

            // A) En T0 + 10 min: Ninguno debe estar expirado
            var exp10 = await repo.ObtenerPedidosExpiradosAsync(t0.AddMinutes(10));
            Assert.DoesNotContain(exp10, p => p.Id == idPasarela);
            Assert.DoesNotContain(exp10, p => p.Id == idManualSinVoucher);
            Assert.DoesNotContain(exp10, p => p.Id == idManualConVoucher);

            // B) En T0 + 21 min: Solo Pasarela debe estar expirada (superó 20 min)
            var exp21 = await repo.ObtenerPedidosExpiradosAsync(t0.AddMinutes(21));
            Assert.Contains(exp21, p => p.Id == idPasarela);
            Assert.DoesNotContain(exp21, p => p.Id == idManualSinVoucher);
            Assert.DoesNotContain(exp21, p => p.Id == idManualConVoucher);

            // C) En T0 + 61 min: Pasarela Y Manual sin voucher deben estar expirados (superó 60 min sin voucher)
            // Manual CON voucher NO debe estar expirado
            var exp61 = await repo.ObtenerPedidosExpiradosAsync(t0.AddMinutes(61));
            Assert.Contains(exp61, p => p.Id == idPasarela);
            Assert.Contains(exp61, p => p.Id == idManualSinVoucher);
            Assert.DoesNotContain(exp61, p => p.Id == idManualConVoucher);

            // D) En T0 + 241 min: TODOS deben estar expirados (superaron 240 min)
            var exp241 = await repo.ObtenerPedidosExpiradosAsync(t0.AddMinutes(241));
            Assert.Contains(exp241, p => p.Id == idPasarela);
            Assert.Contains(exp241, p => p.Id == idManualSinVoucher);
            Assert.Contains(exp241, p => p.Id == idManualConVoucher);
        }
    }

    [Fact]
    public async Task ObtenerPedidosExpiradosAsync_IsIdempotent_AndExcludesAlreadyTransitionedOrders()
    {
        await using var conn = await _fixture.CreateConnectionAsync();

        var almacenId = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        await conn.ExecuteAsync("INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Alm Idempotent', TRUE, 'Dir');",
            new { a = almacenId, c = $"ALM-IDEMP-{Guid.NewGuid():N}" });

        await conn.ExecuteAsync("""
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, stock_fisico, stock_comprometido)
            VALUES (@p, @sku, 'Prod Idempotent', 'UND', 10, 5, 10, 1);
            INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
            VALUES (gen_random_uuid(), @a, @p, 10, 1);
            """, new { a = almacenId, p = prodId, sku = $"SKU-IDEMP-{Guid.NewGuid():N}" });

        var t0 = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        var item = new PedidoWebItemDto(prodId, 1, 10, "UND", 1.0m, 1);
        var pedido = new PedidoWebDto(
            orderId, Guid.NewGuid(), "PASARELA", "DespachoDomicilio", null, almacenId,
            t0.AddMinutes(-30), t0.AddMinutes(-10), null, null, "PendientePago", new[] { item });

        await using (var db = CreateDbContext())
        {
            var repo = new PostgresWebOrderRepository(db);
            await repo.GuardarPedidoAsync(pedido);

            // 1. Llamada inicial de consulta de expirados
            var firstQuery = await repo.ObtenerPedidosExpiradosAsync(t0);
            Assert.Contains(firstQuery, p => p.Id == orderId);

            // 2. Re-ejecución idempotente: mientras siga en PendientePago, sigue retornándolo
            var secondQuery = await repo.ObtenerPedidosExpiradosAsync(t0);
            Assert.Contains(secondQuery, p => p.Id == orderId);

            // 3. Transicionar a Expirado
            bool updated = await repo.ActualizarEstadoConcurrenteAsync(orderId, "PendientePago", "Expirado");
            Assert.True(updated);

            // 4. Nueva consulta: ya no debe aparecer en la lista de expirados pendientes
            var thirdQuery = await repo.ObtenerPedidosExpiradosAsync(t0);
            Assert.DoesNotContain(thirdQuery, p => p.Id == orderId);
        }
    }
}
