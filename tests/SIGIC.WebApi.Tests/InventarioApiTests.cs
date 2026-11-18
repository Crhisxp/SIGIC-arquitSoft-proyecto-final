using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class InventarioApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    public InventarioApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegistrarAjuste_WithSuperadminRole_SucceedsAndUpdatesStock()
    {
        var superadminClient = _factory.CreateAuthenticatedClient("Superadmin", "SIGIC_InternalStaff");
        var prodId = Guid.NewGuid();
        var almacenId = _factory.CentralWarehouseId;
        string sku = $"INV-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Inventario Test', 'UND', 100, 50, TRUE, 50, 10);

                INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
                VALUES (gen_random_uuid(), @almacenId, @prodId, 50, 10);
                """, new { prodId, sku, almacenId });
        }

        var response = await superadminClient.PostAsJsonAsync("/api/v1/inventario/ajustes", new
        {
            ProductoId = prodId,
            AlmacenId = almacenId,
            CantidadAjuste = 5m,
            Motivo = "Ajuste físico extraordinario",
            CodigoAutorizacionSuperadmin = "AUTH-SUP-2026"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(55m, body.GetProperty("stockFisicoNuevo").GetDecimal());

        // Verificar consulta de Kardex
        var kardexResp = await superadminClient.GetAsync($"/api/v1/inventario/kardex/{prodId}");
        Assert.Equal(HttpStatusCode.OK, kardexResp.StatusCode);
        var kardexList = await kardexResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(kardexList.GetArrayLength() >= 1);

        // Verificar formula RNF-CON-003 en endpoint /api/v1/inventario/stock
        var stockResp = await superadminClient.GetAsync($"/api/v1/inventario/stock?productoId={prodId}&almacenId={almacenId}");
        Assert.Equal(HttpStatusCode.OK, stockResp.StatusCode);
        var stockList = await stockResp.Content.ReadFromJsonAsync<JsonElement>();
        var firstStock = stockList[0];
        decimal fisico = firstStock.GetProperty("stockFisico").GetDecimal();
        decimal comprometido = firstStock.GetProperty("stockComprometido").GetDecimal();
        decimal disponible = firstStock.GetProperty("stockDisponible").GetDecimal();

        Assert.Equal(55m, fisico);
        Assert.Equal(10m, comprometido);
        Assert.Equal(45m, disponible);
        Assert.Equal(fisico - comprometido, disponible);
    }

    [Fact]
    public async Task RegistrarAjuste_WithoutSuperadminRole_Returns403Forbidden()
    {
        var cajeroClient = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");
        var prodId = Guid.NewGuid();
        var almacenId = _factory.CentralWarehouseId;

        var response = await cajeroClient.PostAsJsonAsync("/api/v1/inventario/ajustes", new
        {
            ProductoId = prodId,
            AlmacenId = almacenId,
            CantidadAjuste = 10m,
            Motivo = "Intento no autorizado",
            CodigoAutorizacionSuperadmin = "FAKE-AUTH"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ObtenerGuiaTraslado_NonexistentGuia_Returns404NotFound()
    {
        var client = _factory.CreateAuthenticatedClient("Almacenero", "SIGIC_InternalStaff");
        var guiaIdInexistente = Guid.NewGuid();

        var response = await client.GetAsync($"/api/v1/inventario/guias-traslado/{guiaIdInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var errorBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, errorBody.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ObtenerGuia_ByAlphanumericCode_Returns200Ok()
    {
        var client = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");
        var pedidoId = Guid.NewGuid();
        var guiaId = Guid.NewGuid();
        string cod = $"W-{Guid.NewGuid():N}"[..10];
        string numeroGuia = $"T001-20261118-{Guid.NewGuid():N}"[..22];
        DateTime now = DateTime.UtcNow;

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO pedidos_web (
                    id, codigo, cliente_id, almacen_origen_id, tienda_destino_id, modalidad_entrega,
                    modalidad_pago, estado, total, fecha_creacion_utc, fecha_expiracion_utc,
                    fecha_limite_voucher_utc
                ) VALUES (
                    @pedidoId, @cod, gen_random_uuid(), @central, gen_random_uuid(), 'RetiroEnTienda',
                    'MANUAL', 'Listo para Retiro', 150, @now, @now + INTERVAL '240 minutes',
                    @now + INTERVAL '60 minutes'
                );
                INSERT INTO guias_traslado_interno (
                    id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc
                ) VALUES (
                    @guiaId, @numeroGuia, @pedidoId, @central, gen_random_uuid(), 'Listo para Retiro', @now
                );
                """, new { pedidoId, guiaId, cod, numeroGuia, central = _factory.CentralWarehouseId, now });
        }

        var response = await client.GetAsync($"/api/v1/inventario/guias-traslado/{numeroGuia}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(numeroGuia, body.GetProperty("numeroGuia").GetString());
        Assert.Equal("Listo para Retiro", body.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task EntregarGuia_InvalidDni_Returns400BadRequest()
    {
        var client = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");
        var response = await client.PostAsJsonAsync("/api/v1/inventario/guias-traslado/GUIA-DUMMY/entregar", new
        {
            DniReceptor = "12345",
            CodigoRetiro = "RET-1234"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EntregarGuia_EstadoNoListoParaRetiro_Returns400BadRequest()
    {
        var client = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");
        var pedidoId = Guid.NewGuid();
        var guiaId = Guid.NewGuid();
        string cod = $"W-{Guid.NewGuid():N}"[..10];
        string numeroGuia = $"T001-20261118-{Guid.NewGuid():N}"[..22];
        DateTime now = DateTime.UtcNow;

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO pedidos_web (
                    id, codigo, cliente_id, almacen_origen_id, tienda_destino_id, modalidad_entrega,
                    modalidad_pago, estado, total, fecha_creacion_utc, fecha_expiracion_utc,
                    fecha_limite_voucher_utc
                ) VALUES (
                    @pedidoId, @cod, gen_random_uuid(), @central, gen_random_uuid(), 'RetiroEnTienda',
                    'MANUAL', 'PendientePago', 150, @now, @now + INTERVAL '240 minutes',
                    @now + INTERVAL '60 minutes'
                );
                INSERT INTO guias_traslado_interno (
                    id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc
                ) VALUES (
                    @guiaId, @numeroGuia, @pedidoId, @central, gen_random_uuid(), 'EMITIDA', @now
                );
                """, new { pedidoId, guiaId, cod, numeroGuia, central = _factory.CentralWarehouseId, now });
        }

        var response = await client.PostAsJsonAsync($"/api/v1/inventario/guias-traslado/{numeroGuia}/entregar", new
        {
            DniReceptor = "41238910",
            CodigoRetiro = "RET-9921"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EntregarGuia_ListoParaRetiro_Returns200OkAndEntregado()
    {
        var client = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");
        var pedidoId = Guid.NewGuid();
        var guiaId = Guid.NewGuid();
        string cod = $"W-{Guid.NewGuid():N}"[..10];
        string numeroGuia = $"T001-20261118-{Guid.NewGuid():N}"[..22];
        DateTime now = DateTime.UtcNow;

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO pedidos_web (
                    id, codigo, cliente_id, almacen_origen_id, tienda_destino_id, modalidad_entrega,
                    modalidad_pago, estado, total, fecha_creacion_utc, fecha_expiracion_utc,
                    fecha_limite_voucher_utc
                ) VALUES (
                    @pedidoId, @cod, gen_random_uuid(), @central, gen_random_uuid(), 'RetiroEnTienda',
                    'MANUAL', 'Listo para Retiro', 150, @now, @now + INTERVAL '240 minutes',
                    @now + INTERVAL '60 minutes'
                );
                INSERT INTO guias_traslado_interno (
                    id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc
                ) VALUES (
                    @guiaId, @numeroGuia, @pedidoId, @central, gen_random_uuid(), 'Listo para Retiro', @now
                );
                """, new { pedidoId, guiaId, cod, numeroGuia, central = _factory.CentralWarehouseId, now });
        }

        var response = await client.PostAsJsonAsync($"/api/v1/inventario/guias-traslado/{numeroGuia}/entregar", new
        {
            DniReceptor = "41238910",
            CodigoRetiro = "RET-9921"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ENTREGADO", body.GetProperty("estado").GetString());
        Assert.Equal("41238910", body.GetProperty("dniReceptor").GetString());
    }
}
