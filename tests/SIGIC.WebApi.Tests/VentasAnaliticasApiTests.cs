using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class VentasAnaliticasApiTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ConsultaVentas_RespetaMedianocheLocalYPagoEfectivoSinDuplicarLineas()
    {
        var almacen = factory.CentralWarehouseId;
        // Fechas distintas por ejecución para mantener independiente el histórico append-only.
        await using var conn = await factory.Fixture.CreateConnectionAsync();
        var dia = DateOnly.FromDateTime(await conn.ExecuteScalarAsync<DateTime>("""
            SELECT (GREATEST(COALESCE(MAX(fecha_utc) AT TIME ZONE 'UTC',timestamp '2100-01-01'),
                             timestamp '2100-01-01')::date + 2)::timestamp FROM ventas_pos
            """));
        var inicioUtc = DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(5, 0)), DateTimeKind.Utc);
        var turno = Guid.NewGuid();
        var pedido = Guid.NewGuid();
        var producto = Guid.NewGuid();
        await conn.ExecuteAsync("""
            INSERT INTO turnos_caja(id,caja_id,cajero_id,almacen_id,fondo_inicial,estado,fecha_apertura_utc)
            VALUES (@turno,gen_random_uuid(),gen_random_uuid(),@almacen,0,'Cerrado',@inicioUtc);
            INSERT INTO ventas_pos(id,turno_caja_id,cajero_id,almacen_id,total,fecha_utc,metodo_pago)
            SELECT gen_random_uuid(),@turno,gen_random_uuid(),@almacen,total,fecha::timestamptz,'Efectivo'
            FROM (VALUES (999,(@inicioUtc - interval '1 second')),(20,@inicioUtc),(45,(@inicioUtc + interval '24 hours' - interval '1 second')),
                         (999,(@inicioUtc + interval '24 hours'))) AS v(total,fecha);
            INSERT INTO productos(id,codigo_sku,nombre_comercial,precio_venta) VALUES (@producto,@sku,'Fixture',10);
            INSERT INTO pedidos_web(id,codigo,cliente_id,almacen_origen_id,tienda_destino_id,modalidad_entrega,modalidad_pago,
                                    estado,total,fecha_creacion_utc,fecha_expiracion_utc)
            VALUES (@pedido,@codigo,gen_random_uuid(),@almacen,@almacen,'RetiroEnTienda','Yape',
                    'PagadoConfirmado',10,(@inicioUtc - interval '1 hour'),(@inicioUtc + interval '3 hours'));
            INSERT INTO kardex_movimientos(id,producto_id,almacen_id,tipo_operacion,cantidad_base,costo_unitario,documento_referencia,fecha_utc)
            SELECT gen_random_uuid(),@producto,@almacen,'VENTA_WEB',1,0,@codigo,(@inicioUtc + interval '30 minutes')
            FROM generate_series(1,2);
            """, new { almacen, turno, pedido, producto, inicioUtc, sku = producto.ToString(), codigo = pedido.ToString() });

        using var client = factory.CreateAuthenticatedClient("Superadmin");
        using var response = await client.PostAsJsonAsync("/api/v1/analitica/consultas",
            new { pregunta = "Ticket promedio", desde = dia, hastaExclusivo = dia.AddDays(1) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var datos = await response.Content.ReadFromJsonAsync<JsonElement>();
        var hechos = datos.GetProperty("hechos").EnumerateArray()
            .ToDictionary(h => h.GetProperty("etiqueta").GetString()!, h => h.GetProperty("valor").GetString());
        Assert.Equal("32.50", hechos["Ticket promedio POS"]);
        Assert.Equal("10.00", hechos["Ventas WEB"]);
        Assert.Equal("25.00", hechos["Ticket promedio consolidado"]);

        using var web = await client.PostAsJsonAsync("/api/v1/analitica/consultas",
            new { pregunta = "Ventas", desde = dia, hastaExclusivo = dia.AddDays(1), canales = new[] { "WEB" } });
        Assert.Equal(HttpStatusCode.OK, web.StatusCode);
        var filtro = await web.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(filtro.GetProperty("hechos").EnumerateArray(),
            hecho => hecho.GetProperty("etiqueta").GetString()!.EndsWith("POS"));
    }
}
