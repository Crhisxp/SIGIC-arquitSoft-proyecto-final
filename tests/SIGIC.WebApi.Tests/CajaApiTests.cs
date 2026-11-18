using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class CajaApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    public CajaApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AperturaYCierreArqueoCiego_BlindVerification_NoTheoreticalBalanceReturned()
    {
        var cajaId = Guid.NewGuid();
        var cajeroId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff", userId: cajeroId);
        var almacenId = _factory.CentralWarehouseId;

        // 1. Apertura de Turno
        var openResp = await client.PostAsJsonAsync("/api/v1/caja/turnos/apertura", new
        {
            CajaId = cajaId,
            CajeroId = cajeroId,
            AlmacenId = almacenId,
            FondoInicial = 100m
        });

        Assert.Equal(HttpStatusCode.Created, openResp.StatusCode);
        var openBody = await openResp.Content.ReadFromJsonAsync<JsonElement>();
        Guid turnoId = openBody.GetProperty("turnoId").GetGuid();

        // 2. Consulta de turno activo
        var currentResp = await client.GetAsync($"/api/v1/caja/turnos/actual?cajeroId={cajeroId}");
        Assert.Equal(HttpStatusCode.OK, currentResp.StatusCode);

        // 3. Cierre con Arqueo Ciego
        var closeResp = await client.PostAsJsonAsync("/api/v1/caja/turnos/cierre", new
        {
            TurnoCajaId = turnoId,
            FondoInicialSencillo = 100m,
            TotalDigitalDeclarado = 50m,
            DesgloseFisico = new[]
            {
                new { ValorUnitario = 100m, Cantidad = 1 },
                new { ValorUnitario = 50m, Cantidad = 1 }
            },
            Justificacion = "Cierre normal de turno"
        });

        Assert.Equal(HttpStatusCode.OK, closeResp.StatusCode);
        var closeBody = await closeResp.Content.ReadFromJsonAsync<JsonElement>();

        // Invariante Cardinal (RF-CAJ-002 / AC03): Cero visibilidad de saldos teóricos al cajero
        Assert.False(closeBody.TryGetProperty("saldoTeorico", out _), "La respuesta jamás debe exponer saldoTeorico");
        Assert.False(closeBody.TryGetProperty("saldo_sistema_teorico", out _), "La respuesta jamás debe exponer saldo_sistema_teorico");
        Assert.False(closeBody.TryGetProperty("diferencia", out _), "La respuesta jamás debe exponer diferencia al cajero");

        Assert.True(closeBody.TryGetProperty("arqueoId", out _));
        Assert.Equal(turnoId, closeBody.GetProperty("turnoCajaId").GetGuid());
    }

    [Fact]
    public async Task ValidarVoucher_WithTesoreriaRole_ApprovesOrderAndGeneratesGuide()
    {
        var tesoreriaClient = _factory.CreateAuthenticatedClient("Tesoreria", "SIGIC_InternalStaff");
        var pedidoId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        string sku = $"VCH-{Guid.NewGuid():N}"[..12];
        string cod = $"WEB-{pedidoId.ToString()[..8].ToUpperInvariant()}";
        DateTime now = DateTime.UtcNow;

        await using (var conn = await _factory.Fixture.CreateConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta, costo_unitario, disponible_en_web, stock_fisico, stock_comprometido)
                VALUES (@prodId, @sku, 'Producto Voucher Validado', 'UND', 40, 20, TRUE, 10, 2);

                INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
                VALUES (gen_random_uuid(), @central, @prodId, 10, 2);

                INSERT INTO pedidos_web (
                    id, codigo, cliente_id, almacen_origen_id, tienda_destino_id, modalidad_entrega,
                    modalidad_pago, estado, total, fecha_creacion_utc, fecha_expiracion_utc,
                    fecha_limite_voucher_utc, fecha_subida_voucher_utc
                ) VALUES (
                    @pedidoId, @cod, gen_random_uuid(), @central, gen_random_uuid(), 'RetiroEnTienda',
                    'MANUAL', 'PendienteValidacion', 80, @now, @now + INTERVAL '240 minutes',
                    @now + INTERVAL '60 minutes', @now
                );

                INSERT INTO pedidos_web_items (
                    id, pedido_web_id, producto_id, cantidad_comercial, unidad_comercial,
                    factor_conversion, cantidad_base_reservada, precio_unitario, subtotal
                ) VALUES (
                    gen_random_uuid(), @pedidoId, @prodId, 2, 'UND', 1.0, 2, 40, 80
                );

                INSERT INTO comprobantes_voucher (
                    id, pedido_web_id, nombre_archivo, formato, hash_sha256, tamano_bytes, fecha_subida_utc
                ) VALUES (
                    gen_random_uuid(), @pedidoId, 'voucher.jpg', '.jpg', 'abcdef1234567890', 2048, @now
                );
                """, new { prodId, sku, pedidoId, cod, central = _factory.CentralWarehouseId, now });
        }

        var response = await tesoreriaClient.PostAsJsonAsync($"/api/v1/caja/vouchers/{pedidoId}/validar", new
        {
            Aprobado = true,
            MotivoRechazo = (string?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("aprobado").GetBoolean());
        Assert.Equal("PagadoConfirmado", body.GetProperty("estadoFinal").GetString());
    }

    [Fact]
    public async Task ValidarVoucher_WithoutTesoreriaRole_Returns403Forbidden()
    {
        // Un cajero común no tiene autorización para validar vouchers de tesorería
        var cajeroClient = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");
        var pedidoId = Guid.NewGuid();

        var response = await cajeroClient.PostAsJsonAsync($"/api/v1/caja/vouchers/{pedidoId}/validar", new
        {
            Aprobado = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
