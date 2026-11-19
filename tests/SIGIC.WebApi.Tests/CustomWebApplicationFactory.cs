using System.Net.Http.Headers;
using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SIGIC.Application.Security.Ports;
using SIGIC.Infrastructure;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.WebApi.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlTestFixture Fixture { get; } = new();
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _privateKeyPem;
    private readonly string _publicKeyPem;
    public Guid CentralWarehouseId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public CustomWebApplicationFactory()
    {
        _privateKeyPem = _rsa.ExportPkcs8PrivateKeyPem();
        _publicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem();
    }

    public async Task InitializeAsync()
    {
        await Fixture.InitializeAsync();

        // Sembrar Almacén Central canónico para las pruebas de WebApi
        await using var conn = await Fixture.CreateConnectionAsync();
        await conn.ExecuteAsync("""
            INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion)
            VALUES (@id, 'ALM-CENTRAL', 'Almacén Central WebApi Tests', TRUE, 'Av Central 100')
            ON CONFLICT (id) DO NOTHING;
            """, new { id = CentralWarehouseId });
    }

    public new async Task DisposeAsync()
    {
        _rsa.Dispose();
        await Fixture.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = Fixture.AppConnectionString,
                ["ConnectionStrings:PosConnection"] = Fixture.PosPoolConnectionString,
                ["ConnectionStrings:WebConnection"] = Fixture.WebPoolConnectionString,
                ["JwtSettings:PrivateKeyPem"] = _privateKeyPem,
                ["JwtSettings:PublicKeyPem"] = _publicKeyPem,
                ["WarehouseSettings:CentralWarehouseId"] = CentralWarehouseId.ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            // Minimal hosting captures these options before ConfigureAppConfiguration runs.
            // Replace the captured values while preserving the fixture's separate POS/Web pools.
            services.Replace(ServiceDescriptor.Singleton(new DatabasePoolOptions(
                Fixture.AppConnectionString,
                Fixture.PosPoolConnectionString,
                Fixture.WebPoolConnectionString)));

            var rsaPrivate = RSA.Create();
            rsaPrivate.ImportFromPem(_privateKeyPem);
            var signingKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsaPrivate) { KeyId = "sigic-core-signing-key" };
            var validationKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(RSA.Create(rsaPrivate.ExportParameters(false))) { KeyId = "sigic-core-signing-key" };

            var existingJwt = services.FirstOrDefault(d => d.ServiceType == typeof(IJwtTokenService));
            if (existingJwt != null) services.Remove(existingJwt);
            services.AddSingleton<IJwtTokenService>(new SIGIC.Infrastructure.Security.RsaJwtTokenService(signingKey, validationKey, "SIGIC_Backend"));
        });
    }

    public HttpClient CreateAuthenticatedClient(
        string role,
        string audience = "SIGIC_InternalStaff",
        Guid? userId = null,
        string username = "test_staff_user")
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        Guid uid = userId ?? Guid.NewGuid();
        var tokenResult = jwtService.GenerateToken(uid, username, new[] { role }, audience);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Token);
        return client;
    }
}
