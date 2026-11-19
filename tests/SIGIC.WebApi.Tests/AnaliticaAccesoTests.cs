using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class AnaliticaAccesoTests(CustomWebApplicationFactory factory)
{
    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData("ClienteWeb", "SIGIC_WebClient", HttpStatusCode.Forbidden)]
    [InlineData("Cajero", "SIGIC_InternalStaff", HttpStatusCode.Forbidden)]
    [InlineData("Tesoreria", "SIGIC_InternalStaff", HttpStatusCode.OK)]
    [InlineData("Almacenero", "SIGIC_InternalStaff", HttpStatusCode.OK)]
    [InlineData("Superadmin", "SIGIC_InternalStaff", HttpStatusCode.OK)]
    public async Task VerificaAudienciaYRolEnAmbasRutas(string? rol, string? audiencia, HttpStatusCode esperado)
    {
        using var client = rol == null ? factory.CreateClient() : factory.CreateAuthenticatedClient(rol, audiencia!);
        using var consulta = await client.PostAsJsonAsync("/api/v1/analitica/consultas",
            new { pregunta = "Ventas", desde = "2001-01-01", hastaExclusivo = "2001-01-03" });
        using var sesion = await client.GetAsync("/api/v1/analitica/sesion");
        foreach (var r in new[] { consulta, sesion })
        {
            Assert.Equal(esperado, r.StatusCode);
            Assert.True(r.Headers.CacheControl?.NoStore);
        }
        if (esperado == HttpStatusCode.OK)
        {
            var json = await consulta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("sin_datos", json.GetProperty("estado").GetString());
            Assert.Empty(json.GetProperty("hechos").EnumerateArray());
        }
    }
}
