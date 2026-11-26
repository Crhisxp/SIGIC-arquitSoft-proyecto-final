using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class PedidosWebApiTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _clienteId = Guid.NewGuid();

    public PedidosWebApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient("ClienteWeb", "SIGIC_WebClient", _clienteId);
    }

    [Fact]
    public async Task ReservarPedido_WithSufficientStock_Returns201Created()
    {
        var prodId = Guid.NewGuid();
        string sku = $"PED-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Pedido Web', 'UND', 50, 20, TRUE, 20, 0);

                INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
                VALUES (gen_random_uuid(), @central, @prodId, 20, 0);
                """, new { prodId, sku, central = _factory.CentralWarehouseId });
        }

        var response = await _client.PostAsJsonAsync("/api/v1/web/pedidos/reservar", new
        {
            ClienteId = _clienteId,
            MetodoPago = "Yape",
            ModalidadEntrega = "DespachoDomicilio",
            Items = new[]
            {
                new { ProductoId = prodId, Cantidad = 3m, PrecioUnitario = 50m, UnidadMedida = "UND", FactorConversion = 1.0m }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("pedidoId", out var pedidoIdProp));
        Assert.NotEqual(Guid.Empty, pedidoIdProp.GetGuid());
        Assert.Equal("PendientePago", body.GetProperty("estado").GetString());
        Assert.Equal(150m, body.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task ReservarPedido_WithInsufficientStock_Returns409Conflict()
    {
        var prodId = Guid.NewGuid();
        string sku = $"NOSTK-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Sin Stock', 'UND', 50, 20, TRUE, 2, 0);

                INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
                VALUES (gen_random_uuid(), @central, @prodId, 2, 0);
                """, new { prodId, sku, central = _factory.CentralWarehouseId });
        }

        // Solicita 10 unidades cuando solo hay 2 disponibles
        var response = await _client.PostAsJsonAsync("/api/v1/web/pedidos/reservar", new
        {
            ClienteId = _clienteId,
            MetodoPago = "Yape",
            ModalidadEntrega = "DespachoDomicilio",
            Items = new[]
            {
                new { ProductoId = prodId, Cantidad = 10m, PrecioUnitario = 50m, UnidadMedida = "UND", FactorConversion = 1.0m }
            }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReservarPedido_WithAnotherCustomerId_Returns403()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/web/pedidos/reservar", new
        {
            ClienteId = Guid.NewGuid(),
            MetodoPago = "Yape",
            ModalidadEntrega = "DespachoDomicilio",
            Items = new[] { new { ProductoId = Guid.NewGuid(), Cantidad = 1m, PrecioUnitario = 50m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubirVoucher_ValidImage_Returns200WithMetadata()
    {
        var pedidoId = Guid.NewGuid();
        string cod = $"WEB-{pedidoId.ToString()[..8].ToUpperInvariant()}";
        DateTime now = DateTime.UtcNow;

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO pedidos_web (
                    id, codigo, cliente_id, almacen_origen_id, tienda_destino_id, modalidad_entrega,
                    modalidad_pago, estado, total, fecha_creacion_utc, fecha_expiracion_utc,
                    fecha_limite_voucher_utc, fecha_subida_voucher_utc
                ) VALUES (
                    @pedidoId, @cod, gen_random_uuid(), @central, gen_random_uuid(), 'EnvioDomicilio',
                    'MANUAL', 'PendientePago', 100, @now, @now + INTERVAL '240 minutes',
                    @now + INTERVAL '60 minutes', NULL
                );
                """, new { pedidoId, cod, central = _factory.CentralWarehouseId, now });
        }

        using var content = new MultipartFormDataContent();
        byte[] dummyBytes = new byte[1024];
        var fileContent = new ByteArrayContent(dummyBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "voucher", "voucher.jpg");

        var response = await _client.PostAsync($"/api/v1/web/pedidos/{pedidoId}/voucher", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("voucher.jpg", body.GetProperty("nombreArchivo").GetString());
        Assert.Equal("image/jpeg", body.GetProperty("contentType").GetString());
        Assert.Equal(1024, body.GetProperty("tamanoBytes").GetInt64());
    }

    [Fact]
    public async Task SubirVoucher_InvalidFormat_Returns400BadRequest()
    {
        var pedidoId = Guid.NewGuid();
        string cod = $"WEB-{pedidoId.ToString()[..8].ToUpperInvariant()}";
        DateTime now = DateTime.UtcNow;

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO pedidos_web (
                    id, codigo, cliente_id, almacen_origen_id, tienda_destino_id, modalidad_entrega,
                    modalidad_pago, estado, total, fecha_creacion_utc, fecha_expiracion_utc,
                    fecha_limite_voucher_utc, fecha_subida_voucher_utc
                ) VALUES (
                    @pedidoId, @cod, gen_random_uuid(), @central, gen_random_uuid(), 'EnvioDomicilio',
                    'MANUAL', 'PendientePago', 100, @now, @now + INTERVAL '240 minutes',
                    @now + INTERVAL '60 minutes', NULL
                );
                """, new { pedidoId, cod, central = _factory.CentralWarehouseId, now });
        }

        using var content = new MultipartFormDataContent();
        byte[] dummyBytes = new byte[1024];
        var fileContent = new ByteArrayContent(dummyBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "voucher", "voucher.exe");

        var response = await _client.PostAsync($"/api/v1/web/pedidos/{pedidoId}/voucher", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
