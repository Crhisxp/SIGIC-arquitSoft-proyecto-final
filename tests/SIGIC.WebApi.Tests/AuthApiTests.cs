using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using SIGIC.Application.Security.Ports;
using Xunit;

namespace SIGIC.WebApi.Tests;

[Collection("WebApiTestCollection")]
public class AuthApiTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LoginStaff_WithValidCredentials_Returns200WithJwtToken()
    {
        string username = $"cajero_{Guid.NewGuid():N}"[..18];
        string password = "Password123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var repo = scope.ServiceProvider.GetRequiredService<IAccountLockoutRepository>();
            string hash = hasher.HashPassword(password);
            await repo.RegistrarUsuarioAsync(Guid.NewGuid(), username, hash, new[] { "Cajero" }, "PublicoGeneral");
        }

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login-staff", new
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("token", out var tokenProp));
        Assert.False(string.IsNullOrWhiteSpace(tokenProp.GetString()));
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.Equal(15, body.GetProperty("expiresInMinutes").GetInt32());
    }

    [Fact]
    public async Task LoginStaff_WithInvalidPassword_Returns400()
    {
        string username = $"staff_bad_{Guid.NewGuid():N}"[..18];
        using (var scope = _factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var repo = scope.ServiceProvider.GetRequiredService<IAccountLockoutRepository>();
            string hash = hasher.HashPassword("CorrectPassword!");
            await repo.RegistrarUsuarioAsync(Guid.NewGuid(), username, hash, new[] { "Cajero" }, "PublicoGeneral");
        }

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login-staff", new
        {
            Username = username,
            Password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoginStaff_After5FailedAttempts_LocksAccountFor15Minutes()
    {
        string username = $"brute_{Guid.NewGuid():N}"[..18];
        using (var scope = _factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var repo = scope.ServiceProvider.GetRequiredService<IAccountLockoutRepository>();
            string hash = hasher.HashPassword("RealPass123!");
            await repo.RegistrarUsuarioAsync(Guid.NewGuid(), username, hash, new[] { "Cajero" }, "PublicoGeneral");
        }

        // 5 intentos fallidos
        for (int i = 0; i < 5; i++)
        {
            var failResp = await _client.PostAsJsonAsync("/api/v1/auth/login-staff", new
            {
                Username = username,
                Password = "BadPassword!"
            });
            Assert.Equal(HttpStatusCode.BadRequest, failResp.StatusCode);
        }

        // 6to intento: debe ser rechazado con 403 Forbidden por bloqueo temporal
        var lockedResp = await _client.PostAsJsonAsync("/api/v1/auth/login-staff", new
        {
            Username = username,
            Password = "RealPass123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, lockedResp.StatusCode);
        string text = await lockedResp.Content.ReadAsStringAsync();
        Assert.Contains("bloqueada por exceso de intentos fallidos", text);
    }

    [Fact]
    public async Task LoginStaff_WithNonStaffRole_Returns403()
    {
        string username = $"webuser_{Guid.NewGuid():N}"[..18];
        using (var scope = _factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var repo = scope.ServiceProvider.GetRequiredService<IAccountLockoutRepository>();
            string hash = hasher.HashPassword("ValidWebPass!");
            await repo.RegistrarUsuarioAsync(Guid.NewGuid(), username, hash, new[] { "ClienteWeb" }, "PublicoGeneral");
        }

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login-staff", new
        {
            Username = username,
            Password = "ValidWebPass!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAndLoginWeb_FullFlow_Succeeds()
    {
        string username = $"cliente_{Guid.NewGuid():N}"[..18];
        string password = "ClientePassword123!";

        // Registro
        var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register-web", new
        {
            Username = username,
            Password = password,
            Email = $"{username}@test.com",
            Nombres = "Cliente Prueba"
        });

        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);
        var regBody = await regResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(regBody.TryGetProperty("token", out var regToken));
        Assert.False(string.IsNullOrWhiteSpace(regToken.GetString()));

        // Login posterior
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login-web", new
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(loginBody.TryGetProperty("token", out var loginToken));
        Assert.False(string.IsNullOrWhiteSpace(loginToken.GetString()));
    }
}
