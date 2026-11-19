using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using SIGIC.Application.Analytics.Ports;

namespace SIGIC.Infrastructure.Adapters.Analytics;

/// <summary>El proveedor elige una plantilla a partir de etiquetas de indicadores controladas por la aplicación.</summary>
public sealed class GeminiAnaliticaPort(HttpClient http, IConfiguration config) : IAuxiliarInteligenciaAnaliticaPort
{
    public async Task<string> ElegirPlantillaAsync(string metrica, IReadOnlyList<string> indicadores, CancellationToken ct)
    {
        const string fallback = "indicador_principal";
        string? key = config["GeminiSettings:ApiKey"];
        string? modelo = config["GeminiSettings:Model"];
        if (string.IsNullOrWhiteSpace(key) || modelo is null || !Regex.IsMatch(modelo, @"\A[a-zA-Z0-9._-]{1,80}\z"))
            return fallback;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(4));
        using var solicitud = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent");
        solicitud.Headers.Add("x-goog-api-key", key);
        solicitud.Content = JsonContent.Create(new {
            contents = new[] { new { parts = new[] { new { text =
                $"Métrica: {metrica}. Indicadores disponibles: {string.Join(", ", indicadores)}. Elige con_desglose si el segundo indicador aporta contexto al principal (importe, costo o base del porcentaje). Para una lectura puntual elige indicador_principal." } } } },
            generationConfig = new { responseMimeType = "application/json", maxOutputTokens = 40,
                responseSchema = new { type = "STRING", @enum = new[] { fallback, "con_desglose" } } }
        });
        try
        {
            using var respuesta = await http.SendAsync(solicitud, limite.Token);
            if (!respuesta.IsSuccessStatusCode) return fallback;
            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsByteArrayAsync(limite.Token));
            var texto = json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString();
            return texto?.Trim() == "\"con_desglose\"" ? "con_desglose" : fallback;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return fallback; }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException
            or InvalidOperationException or IndexOutOfRangeException) { return fallback; }
    }
}
