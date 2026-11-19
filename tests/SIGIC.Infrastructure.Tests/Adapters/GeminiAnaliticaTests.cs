using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using SIGIC.Infrastructure.Adapters.Analytics;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Adapters;

public class GeminiAnaliticaTests
{
    [Theory]
    [InlineData(200, "\"con_desglose\"", "con_desglose")]
    [InlineData(200, "Una cifra inventada", "indicador_principal")]
    [InlineData(429, "", "indicador_principal")]
    public async Task SoloAceptaPlantillasConocidas(int status, string texto, string esperado)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["GeminiSettings:ApiKey"] = "prueba-local", ["GeminiSettings:Model"] = "modelo-prueba" }).Build();
        using var client = new HttpClient(new RespuestaSimulada(status, texto));
        var plantilla = await new GeminiAnaliticaPort(client, config)
            .ElegirPlantillaAsync("ventas", ["Ventas POS"], CancellationToken.None);
        Assert.Equal(esperado, plantilla);
    }

    private sealed class RespuestaSimulada(int status, string texto) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Empty(request.RequestUri!.Query);
            Assert.True(request.Headers.Contains("x-goog-api-key"));
            Assert.Contains("Ventas POS", await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage((HttpStatusCode)status) { Content = JsonContent.Create(new {
                candidates = new[] { new { content = new { parts = new[] { new { text = texto } } } } }
            }) };
        }
    }
}
