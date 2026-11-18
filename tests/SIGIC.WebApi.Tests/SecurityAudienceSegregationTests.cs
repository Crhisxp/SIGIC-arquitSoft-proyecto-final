using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SIGIC.WebApi.Tests;

/// <summary>
/// Pruebas de integración de segregación estricta de audiencias y seguridad en API endpoints (RNF-SEG-002, RNF-SEG-005, RF-INV-001).
/// Invariante: Rutas operativas (/pos, /caja, /inventario) exigen 'SIGIC_InternalStaff'.
/// Acceso anónimo o con audiencia 'SIGIC_WebClient' es inmediatamente rechazado (401 / 403).
/// </summary>
[Collection("WebApiTestCollection")]
public class SecurityAudienceSegregationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityAudienceSegregationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AccesoAnonimo_ARutasInternas_Responde401Unauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        // a) /api/v1/inventario/stock sin token -> 401 Unauthorized
        var stockResp = await anonymousClient.GetAsync("/api/v1/inventario/stock");
        Assert.Equal(HttpStatusCode.Unauthorized, stockResp.StatusCode);
        var stockBody = await stockResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, stockBody.GetProperty("status").GetInt32());

        // b) /api/v1/caja/* sin token -> 401 Unauthorized
        var cajaTurnoResp = await anonymousClient.GetAsync($"/api/v1/caja/turnos/actual?cajeroId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, cajaTurnoResp.StatusCode);

        var cajaAperturaResp = await anonymousClient.PostAsJsonAsync("/api/v1/caja/turnos/apertura", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, cajaAperturaResp.StatusCode);

        var cajaCierreResp = await anonymousClient.PostAsJsonAsync("/api/v1/caja/turnos/cierre", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, cajaCierreResp.StatusCode);

        // c) /api/v1/pos/* sin token -> 401 Unauthorized
        var posVentaResp = await anonymousClient.PostAsJsonAsync("/api/v1/pos/ventas", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, posVentaResp.StatusCode);

        var posGetResp = await anonymousClient.GetAsync($"/api/v1/pos/ventas/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, posGetResp.StatusCode);

        // d) /api/v1/web/pedidos/* sin token -> 401 Unauthorized
        var webOrderResp = await anonymousClient.PostAsJsonAsync("/api/v1/web/pedidos/reservar", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, webOrderResp.StatusCode);
    }

    [Fact]
    public async Task TokenClienteWeb_IntentandoConsultarRutasInternas_Responde403Forbidden()
    {
        // Token legítimo emitido para cliente web (audiencia 'SIGIC_WebClient')
        var webClient = _factory.CreateAuthenticatedClient("ClienteWeb", "SIGIC_WebClient");

        // a) Intento de consultar stock interno -> 403 Forbidden
        var stockResp = await webClient.GetAsync("/api/v1/inventario/stock");
        Assert.Equal(HttpStatusCode.Forbidden, stockResp.StatusCode);
        var stockBody = await stockResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, stockBody.GetProperty("status").GetInt32());
        Assert.Contains("audiencia 'SIGIC_InternalStaff'", stockBody.GetProperty("detail").GetString()!);

        // b) Intento de ejecutar venta POS en mostrador -> 403 Forbidden
        var posResp = await webClient.PostAsJsonAsync("/api/v1/pos/ventas", new
        {
            TurnoCajaId = Guid.NewGuid(),
            ClienteId = Guid.NewGuid(),
            Items = new[] { new { ProductoId = Guid.NewGuid(), Cantidad = 1m, UnidadMedida = "UND", PrecioUnitario = 10m } },
            Pagos = new[] { new { MetodoPago = "Efectivo", Monto = 10m } },
            TipoComprobante = "Boleta"
        });
        Assert.Equal(HttpStatusCode.Forbidden, posResp.StatusCode);

        // c) Intento de consultar turnos de caja -> 403 Forbidden
        var cajaResp = await webClient.GetAsync($"/api/v1/caja/turnos/actual?cajeroId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, cajaResp.StatusCode);

        // d) Intento de ajuste de inventario -> 403 Forbidden
        var ajusteResp = await webClient.PostAsJsonAsync("/api/v1/inventario/ajustes", new
        {
            ProductoId = Guid.NewGuid(),
            AlmacenId = _factory.CentralWarehouseId,
            CantidadAjuste = 1m,
            Motivo = "Intento ilegal",
            CodigoAutorizacionSuperadmin = "SUP-1"
        });
        Assert.Equal(HttpStatusCode.Forbidden, ajusteResp.StatusCode);
    }

    [Fact]
    public async Task TokenStaffInterno_IntentandoRutasClienteWeb_Responde403Forbidden()
    {
        // Token legítimo de personal interno (audiencia 'SIGIC_InternalStaff')
        var staffClient = _factory.CreateAuthenticatedClient("Cajero", "SIGIC_InternalStaff");

        // Intento de reservar en canal web con token interno de mostrador -> 403 Forbidden
        var webResp = await staffClient.PostAsJsonAsync("/api/v1/web/pedidos/reservar", new
        {
            ClienteId = Guid.NewGuid(),
            MetodoPago = "Yape",
            ModalidadEntrega = "DespachoDomicilio",
            Items = new[] { new { ProductoId = Guid.NewGuid(), Cantidad = 1m, UnidadMedida = "UND", FactorConversion = 1.0m, PrecioUnitario = 10m } }
        });

        Assert.Equal(HttpStatusCode.Forbidden, webResp.StatusCode);
        var body = await webResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Contains("audiencia 'SIGIC_WebClient'", body.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task TokenStaffInterno_ConsultaInventarioStock_Responde200OK()
    {
        // Token válido de staff interno (audiencia 'SIGIC_InternalStaff')
        var staffClient = _factory.CreateAuthenticatedClient("Almacenero", "SIGIC_InternalStaff");

        var response = await staffClient.GetAsync("/api/v1/inventario/stock");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
    }

    [Fact]
    public async Task AccesoAnonimo_ACatalogoPublicoYAuth_PermitidoSinToken()
    {
        var anonymousClient = _factory.CreateClient();

        // Catálogo público accesible anónimamente
        var catResp = await anonymousClient.GetAsync("/api/v1/public/catalogo/productos");
        Assert.Equal(HttpStatusCode.OK, catResp.StatusCode);

        // Login staff accesible anónimamente (responde 400 por validación de credenciales vacías, no 401 ni 403)
        var authResp = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login-staff", new
        {
            Username = "",
            Password = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, authResp.StatusCode);
    }
}
