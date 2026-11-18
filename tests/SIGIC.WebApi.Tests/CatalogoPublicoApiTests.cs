using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class CatalogoPublicoApiTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CatalogoPublicoApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListarProductos_ReturnsOnlyWebEnabledProducts()
    {
        var prodWebId = Guid.NewGuid();
        var prodNonWebId = Guid.NewGuid();
        string skuWeb = $"WEB-{Guid.NewGuid():N}"[..12];
        string skuNonWeb = $"POS-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodWebId, @skuWeb, 'Producto Web Test', 'UND', 100, 50, TRUE, 20, 5),
                       (@prodNonWebId, @skuNonWeb, 'Producto Solo POS Test', 'UND', 120, 60, FALSE, 10, 0);
                """, new { prodWebId, skuWeb, prodNonWebId, skuNonWeb });
        }

        var response = await _client.GetAsync($"/api/v1/public/catalogo/productos?search={skuWeb}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(items);
        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == prodWebId);
        Assert.DoesNotContain(items, i => i.GetProperty("id").GetGuid() == prodNonWebId);
    }

    [Fact]
    public async Task ObtenerDetalleProducto_NonWebProduct_Returns404AbsoluteNotFound()
    {
        // Invariante 2 (RF-INV-001): Los productos no web retornan HTTP 404 absoluto
        var prodNonWebId = Guid.NewGuid();
        string sku = $"NOWEB-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodNonWebId, @sku, 'Producto Exclusivo POS', 'UND', 50, 20, FALSE, 10, 0);
                """, new { prodNonWebId, sku });
        }

        var response = await _client.GetAsync($"/api/v1/public/catalogo/productos/{prodNonWebId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ObtenerDetalleProducto_WebProduct_ReturnsStockDisponibleCorrectly()
    {
        var prodId = Guid.NewGuid();
        string sku = $"DET-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Detalle Test', 'UND', 80, 40, TRUE, 15, 3);
                """, new { prodId, sku });
        }

        var response = await _client.GetAsync($"/api/v1/public/catalogo/productos/{prodId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(prodId, body.GetProperty("id").GetGuid());
        // RNF-CON-003: 15 - 3 = 12
        Assert.Equal(12m, body.GetProperty("stockDisponible").GetDecimal());
    }

    [Fact]
    public async Task CalcularPrecio_WithQuantityAndProfile_ReturnsPDPResult()
    {
        var prodId = Guid.NewGuid();
        string sku = $"CALC-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Calculo PDP', 'UND', 100, 50, TRUE, 50, 0);
                """, new { prodId, sku });
        }

        var response = await _client.PostAsJsonAsync("/api/v1/public/catalogo/calcular-precio", new
        {
            ProductoId = prodId,
            Cantidad = 5m,
            UnidadMedida = "UND"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(prodId, body.GetProperty("productoId").GetGuid());
        Assert.Equal(100m, body.GetProperty("precioBaseUnitario").GetDecimal());
        Assert.Equal(500m, body.GetProperty("subtotal").GetDecimal());
    }
}
