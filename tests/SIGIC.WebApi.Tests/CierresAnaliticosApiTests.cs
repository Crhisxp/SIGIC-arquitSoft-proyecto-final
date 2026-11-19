using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class CierresAnaliticosApiTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task PeriodoCerrado_UsaGastosRealesYPromedioDiarioDeInventario()
    {
        using var client = factory.CreateAuthenticatedClient("Tesoreria");
        await using var admin = await factory.Fixture.CreateConnectionAsync(factory.Fixture.AdminConnectionString);
        await admin.ExecuteAsync("""
            INSERT INTO reporting.cierres_operativos_diarios VALUES
                ('2002-06-01', 600, 400, 100, 300), ('2002-06-02', 400, 200, 150, 100)
            """);
        try
        {
            foreach (var (pregunta, etiqueta, valor) in new[] {
                ("Margen operativo", "Margen operativo", "15.00"),
                ("Rotación de inventario", "Rotación de inventario", "3.00") })
            {
                var json = await Consultar(client, pregunta, "2002-06-03");
                Assert.Equal("con_datos", json.GetProperty("estado").GetString());
                var hecho = json.GetProperty("hechos").EnumerateArray()
                    .Single(h => h.GetProperty("etiqueta").GetString() == etiqueta);
                Assert.Equal(valor, hecho.GetProperty("valor").GetString());
            }
            var parcial = await Consultar(client, "Margen operativo", "2002-06-04");
            Assert.Equal("datos_incompletos", parcial.GetProperty("estado").GetString());
            Assert.Empty(parcial.GetProperty("hechos").EnumerateArray());
        }
        finally
        {
            await admin.ExecuteAsync("DELETE FROM reporting.cierres_operativos_diarios WHERE dia IN ('2002-06-01','2002-06-02')");
        }
    }

    private static async Task<JsonElement> Consultar(HttpClient client, string pregunta, string hastaExclusivo)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/analitica/consultas",
            new { pregunta, desde = "2002-06-01", hastaExclusivo });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
