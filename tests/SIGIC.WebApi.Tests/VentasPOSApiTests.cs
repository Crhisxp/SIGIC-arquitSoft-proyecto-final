using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class VentasPOSApiTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public VentasPOSApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");
    }

    [Fact]
    public async Task RegistrarVentaPOS_ValidOpenShift_Returns201Created()
    {
        var prodId = Guid.NewGuid();
        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff", userId: cajeroId);
        var almacenId = _factory.CentralWarehouseId;
        string sku = $"POS-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Venta POS', 'UND', 30, 15, TRUE, 50, 0);

                INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
                VALUES (gen_random_uuid(), @almacenId, @prodId, 50, 0);

                INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc)
                VALUES (@turnoId, gen_random_uuid(), @cajeroId, @almacenId, 100, 'Aperturado', NOW());
                """, new { prodId, sku, turnoId, cajeroId, almacenId });
        }

        string opCode = $"YP-{Guid.NewGuid():N}"[..12];

        var response = await client.PostAsJsonAsync("/api/v1/pos/ventas", new
        {
            TurnoCajaId = turnoId,
            ClienteId = Guid.NewGuid(),
            Items = new[]
            {
                new { ProductoId = prodId, Cantidad = 2m, UnidadMedida = "UND", PrecioUnitario = 30m }
            },
            Pagos = new[]
            {
                new { MetodoPago = "Efectivo", Monto = 40m, CodigoOperacionDigital = (string?)null },
                new { MetodoPago = "Yape", Monto = 20m, CodigoOperacionDigital = (string?)opCode }
            },
            TipoComprobante = "Boleta"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("ventaId", out var ventaIdProp));
        Assert.NotEqual(Guid.Empty, ventaIdProp.GetGuid());
        Assert.Equal(60m, body.GetProperty("totalVenta").GetDecimal());

        // Verificar consulta posterior por ID
        Guid ventaId = ventaIdProp.GetGuid();
        var getResp = await client.GetAsync($"/api/v1/pos/ventas/{ventaId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
    }

    [Fact]
    public async Task RegistrarVentaPOS_MismatchedPayments_Returns400BadRequest()
    {
        var prodId = Guid.NewGuid();
        var turnoId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff", userId: cajeroId);
        var almacenId = _factory.CentralWarehouseId;
        string sku = $"BADP-{Guid.NewGuid():N}"[..12];

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Mismatch', 'UND', 50, 20, TRUE, 20, 0);

                INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
                VALUES (gen_random_uuid(), @almacenId, @prodId, 20, 0);

                INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc)
                VALUES (@turnoId, gen_random_uuid(), @cajeroId, @almacenId, 100, 'Aperturado', NOW());
                """, new { prodId, sku, turnoId, cajeroId, almacenId });
        }

        // Total requerido: 50. Total pagado: 40 -> Inconsistencia de pago
        var response = await client.PostAsJsonAsync("/api/v1/pos/ventas", new
        {
            TurnoCajaId = turnoId,
            ClienteId = Guid.NewGuid(),
            Items = new[]
            {
                new { ProductoId = prodId, Cantidad = 1m, UnidadMedida = "UND", PrecioUnitario = 50m }
            },
            Pagos = new[]
            {
                new { MetodoPago = "Efectivo", Monto = 40m }
            },
            TipoComprobante = "Boleta"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
