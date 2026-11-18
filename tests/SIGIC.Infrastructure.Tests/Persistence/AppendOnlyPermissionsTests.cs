using Dapper;
using Npgsql;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Persistence;

[Collection("PostgreSqlDatabase")]
public class AppendOnlyPermissionsTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public AppendOnlyPermissionsTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CajaArqueo_InsertAllowed_UpdateAndDeleteRejectedWith42501()
    {
        await using var conn = await _fixture.CreateConnectionAsync();

        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();
        string almCodigo = $"ALM-{Guid.NewGuid():N}";

        await conn.ExecuteAsync(
            "INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Almacén Test', FALSE, 'Av. Test');",
            new { a = almacenId, c = almCodigo });

        await conn.ExecuteAsync(
            "INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc) VALUES (@t, gen_random_uuid(), @caj, @a, 100, 'Cerrado', NOW());",
            new { t = turnoId, caj = cajeroId, a = almacenId });

        var arqueoId = Guid.NewGuid();

        // 1. INSERT permitido para el rol sigic_app
        await using (var cmdInsert = conn.CreateCommand())
        {
            cmdInsert.CommandText = """
                INSERT INTO caja_arqueo (id, turno_caja_id, cajero_id, fecha_apertura_utc, fecha_cierre_utc, fondo_inicial_sencillo, total_digital_declarado, total_efectivo_declarado, saldo_sistema_teorico, justificacion)
                VALUES (@id, @t, @caj, NOW() - INTERVAL '4 hours', NOW(), 100, 0, 100, 100, 'Arqueo Test');
                """;
            cmdInsert.Parameters.AddWithValue("id", arqueoId);
            cmdInsert.Parameters.AddWithValue("t", turnoId);
            cmdInsert.Parameters.AddWithValue("caj", cajeroId);
            int rows = await cmdInsert.ExecuteNonQueryAsync();
            Assert.Equal(1, rows);
        }

        // 2. UPDATE rechazado con 42501
        await using (var cmdUpdate = conn.CreateCommand())
        {
            cmdUpdate.CommandText = "UPDATE caja_arqueo SET saldo_sistema_teorico = 9999 WHERE id = @id;";
            cmdUpdate.Parameters.AddWithValue("id", arqueoId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdUpdate.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DELETE rechazado con 42501
        await using (var cmdDelete = conn.CreateCommand())
        {
            cmdDelete.CommandText = "DELETE FROM caja_arqueo WHERE id = @id;";
            cmdDelete.Parameters.AddWithValue("id", arqueoId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdDelete.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 4. Integridad intacta
        await using (var cmdSelect = conn.CreateCommand())
        {
            cmdSelect.CommandText = "SELECT saldo_sistema_teorico FROM caja_arqueo WHERE id = @id;";
            cmdSelect.Parameters.AddWithValue("id", arqueoId);
            var saldo = await cmdSelect.ExecuteScalarAsync();
            Assert.Equal(100m, Convert.ToDecimal(saldo));
        }
    }

    [Fact]
    public async Task CajaArqueoDenominacion_InsertAllowed_UpdateAndDeleteRejectedWith42501()
    {
        await using var conn = await _fixture.CreateConnectionAsync();

        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var almacenId = Guid.NewGuid();
        var arqueoId = Guid.NewGuid();
        var denomId = Guid.NewGuid();

        await conn.ExecuteAsync(
            "INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Almacén Denom', FALSE, 'Av. Denom');",
            new { a = almacenId, c = $"ALM-D-{Guid.NewGuid():N}" });

        await conn.ExecuteAsync(
            "INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc) VALUES (@t, gen_random_uuid(), @caj, @a, 100, 'Cerrado', NOW());",
            new { t = turnoId, caj = cajeroId, a = almacenId });

        await conn.ExecuteAsync("""
            INSERT INTO caja_arqueo (id, turno_caja_id, cajero_id, fecha_apertura_utc, fecha_cierre_utc, fondo_inicial_sencillo, total_digital_declarado, total_efectivo_declarado, saldo_sistema_teorico, justificacion)
            VALUES (@id, @t, @caj, NOW() - INTERVAL '2 hours', NOW(), 100, 0, 100, 100, 'Arqueo Denom Test');
            """, new { id = arqueoId, t = turnoId, caj = cajeroId });

        // 1. INSERT permitido
        await using (var cmdInsert = conn.CreateCommand())
        {
            cmdInsert.CommandText = """
                INSERT INTO caja_arqueo_denominacion (id, arqueo_caja_id, denominacion, cantidad, subtotal)
                VALUES (@id, @arqId, 50.00, 2, 100.00);
                """;
            cmdInsert.Parameters.AddWithValue("id", denomId);
            cmdInsert.Parameters.AddWithValue("arqId", arqueoId);
            int rows = await cmdInsert.ExecuteNonQueryAsync();
            Assert.Equal(1, rows);
        }

        // 2. UPDATE rechazado con 42501
        await using (var cmdUpdate = conn.CreateCommand())
        {
            cmdUpdate.CommandText = "UPDATE caja_arqueo_denominacion SET cantidad = 10 WHERE id = @id;";
            cmdUpdate.Parameters.AddWithValue("id", denomId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdUpdate.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DELETE rechazado con 42501
        await using (var cmdDelete = conn.CreateCommand())
        {
            cmdDelete.CommandText = "DELETE FROM caja_arqueo_denominacion WHERE id = @id;";
            cmdDelete.Parameters.AddWithValue("id", denomId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdDelete.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task AuditLog_InsertAllowed_UpdateAndDeleteRejectedWith42501()
    {
        await using var conn = await _fixture.CreateConnectionAsync();

        var logId = Guid.NewGuid();

        // 1. INSERT permitido
        await using (var cmdInsert = conn.CreateCommand())
        {
            cmdInsert.CommandText = """
                INSERT INTO audit_log (id, user_id, accion, entidad, entidad_id, detalles, ip_address, terminal_scope, timestamp_utc)
                VALUES (@id, @u, 'CIERRE_TURNO', 'TurnoCaja', @u_str, 'Detalle inmutable', '127.0.0.1', 'POS-01', NOW());
                """;
            cmdInsert.Parameters.AddWithValue("id", logId);
            cmdInsert.Parameters.AddWithValue("u", Guid.NewGuid());
            cmdInsert.Parameters.AddWithValue("u_str", Guid.NewGuid().ToString());
            int rows = await cmdInsert.ExecuteNonQueryAsync();
            Assert.Equal(1, rows);
        }

        // 2. UPDATE rechazado con 42501
        await using (var cmdUpdate = conn.CreateCommand())
        {
            cmdUpdate.CommandText = "UPDATE audit_log SET accion = 'MODIFICADO' WHERE id = @id;";
            cmdUpdate.Parameters.AddWithValue("id", logId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdUpdate.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DELETE rechazado con 42501
        await using (var cmdDelete = conn.CreateCommand())
        {
            cmdDelete.CommandText = "DELETE FROM audit_log WHERE id = @id;";
            cmdDelete.Parameters.AddWithValue("id", logId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdDelete.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task KardexMovimientos_InsertAllowed_UpdateAndDeleteRejectedWith42501()
    {
        await using var conn = await _fixture.CreateConnectionAsync();

        var movId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var almId = Guid.NewGuid();
        string almCodigo = $"ALM-K-{Guid.NewGuid():N}";
        string skuCodigo = $"SKU-K-{Guid.NewGuid():N}";

        await conn.ExecuteAsync("""
            INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion)
            VALUES (@almId, @almCodigo, 'Almacén Kardex', FALSE, 'Dir K');
            INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
            VALUES (@prodId, @skuCodigo, 'Prod Kardex', 'UND', 10, 5, TRUE, 100, 0);
            """, new { almId, almCodigo, prodId, skuCodigo });

        // 1. INSERT permitido
        await using (var cmdInsert = conn.CreateCommand())
        {
            cmdInsert.CommandText = """
                INSERT INTO kardex_movimientos (id, producto_id, almacen_id, tipo_operacion, cantidad_base, costo_unitario, documento_referencia, fecha_utc)
                VALUES (@id, @prodId, @almId, 'VENTA_POS', 1, 5, 'B001-0001', NOW());
                """;
            cmdInsert.Parameters.AddWithValue("id", movId);
            cmdInsert.Parameters.AddWithValue("prodId", prodId);
            cmdInsert.Parameters.AddWithValue("almId", almId);
            int rows = await cmdInsert.ExecuteNonQueryAsync();
            Assert.Equal(1, rows);
        }

        // 2. UPDATE rechazado con 42501
        await using (var cmdUpdate = conn.CreateCommand())
        {
            cmdUpdate.CommandText = "UPDATE kardex_movimientos SET cantidad_base = 999 WHERE id = @id;";
            cmdUpdate.Parameters.AddWithValue("id", movId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdUpdate.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DELETE rechazado con 42501
        await using (var cmdDelete = conn.CreateCommand())
        {
            cmdDelete.CommandText = "DELETE FROM kardex_movimientos WHERE id = @id;";
            cmdDelete.Parameters.AddWithValue("id", movId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdDelete.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task CajaArqueoDetalle_SelectAllowed_UpdateAndDeleteRejectedWith42501()
    {
        await using var conn = await _fixture.CreateConnectionAsync();

        // 1. SELECT permitido en la vista protegida
        await using (var cmdSelect = conn.CreateCommand())
        {
            cmdSelect.CommandText = "SELECT COUNT(*) FROM caja_arqueo_detalle;";
            var count = await cmdSelect.ExecuteScalarAsync();
            Assert.NotNull(count);
        }

        // 2. UPDATE rechazado en la vista con 42501
        await using (var cmdUpdate = conn.CreateCommand())
        {
            cmdUpdate.CommandText = "UPDATE caja_arqueo_detalle SET cantidad = 999;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdUpdate.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DELETE rechazado en la vista con 42501
        await using (var cmdDelete = conn.CreateCommand())
        {
            cmdDelete.CommandText = "DELETE FROM caja_arqueo_detalle;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdDelete.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }
}
