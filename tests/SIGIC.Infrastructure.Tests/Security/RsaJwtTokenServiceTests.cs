using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using SIGIC.Application.Security.Ports;
using SIGIC.Infrastructure.Security;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Security;

public class RsaJwtTokenServiceTests
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _publicKey;
    private readonly RsaJwtTokenService _jwtService;

    public RsaJwtTokenServiceTests()
    {
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "sigic-test-key-01" };
        _publicKey = new RsaSecurityKey(_rsa.ExportParameters(false)) { KeyId = "sigic-test-key-01" };
        _jwtService = new RsaJwtTokenService(_signingKey, _publicKey, issuer: "SIGIC_Backend");
    }

    [Fact]
    public void GenerateToken_ProducesValidRS256TokenWith15MinTtl()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        string username = "cajero_pos_01";
        string[] roles = new[] { "Cajero", "Ventas" };
        string audience = "SIGIC_InternalStaff";

        // Act
        JwtSecurityTokenResult result = _jwtService.GenerateToken(userId, username, roles, audience);

        // Assert
        Assert.NotNull(result.Token);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(14));
        Assert.True(result.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(16));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Equal("SIGIC_Backend", jwt.Issuer);
        Assert.Contains(audience, jwt.Audiences);
    }

    [Fact]
    public void ValidateToken_CorrectTokenAndAudience_ReturnsSuccess()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        string username = "admin_ferretero";
        string[] roles = new[] { "Administrador" };
        string audience = "SIGIC_InternalStaff";
        var tokenResult = _jwtService.GenerateToken(userId, username, roles, audience);

        // Act
        JwtValidationResult valResult = _jwtService.ValidateToken(tokenResult.Token, audience);

        // Assert
        Assert.True(valResult.IsValid);
        Assert.Equal(userId, valResult.UserId);
        Assert.Equal(username, valResult.Username);
        Assert.Contains("Administrador", valResult.Roles);
        Assert.Equal(audience, valResult.Audience);
        Assert.Null(valResult.FailureReason);
    }

    [Fact]
    public void ValidateToken_CrossAudienceAccess_IsRejected()
    {
        // Arrange: Token issued for Web Client trying to access Internal Staff
        Guid userId = Guid.NewGuid();
        var tokenResult = _jwtService.GenerateToken(userId, "cliente_web", new[] { "ClienteWeb" }, "SIGIC_WebClient");

        // Act
        JwtValidationResult valResult = _jwtService.ValidateToken(tokenResult.Token, "SIGIC_InternalStaff");

        // Assert
        Assert.False(valResult.IsValid);
        Assert.NotNull(valResult.FailureReason);
        Assert.Contains("Audiencia", valResult.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateToken_TamperedToken_FailsSignature()
    {
        // Arrange
        var tokenResult = _jwtService.GenerateToken(Guid.NewGuid(), "user", new[] { "ClienteWeb" }, "SIGIC_WebClient");
        string tampered = tokenResult.Token + "tampered";

        // Act
        JwtValidationResult valResult = _jwtService.ValidateToken(tampered, "SIGIC_WebClient");

        // Assert
        Assert.False(valResult.IsValid);
        Assert.NotNull(valResult.FailureReason);
    }

    [Fact]
    public void ValidateToken_TokenSignedWithDifferentKey_FailsValidation()
    {
        // Arrange: Generate token with a rogue key
        using var rogueRsa = RSA.Create(2048);
        var rogueKey = new RsaSecurityKey(rogueRsa);
        var rogueService = new RsaJwtTokenService(rogueKey, rogueKey, issuer: "SIGIC_Backend");
        var tokenResult = rogueService.GenerateToken(Guid.NewGuid(), "hacker", new[] { "Admin" }, "SIGIC_InternalStaff");

        // Act: Validate with our official public key
        JwtValidationResult valResult = _jwtService.ValidateToken(tokenResult.Token, "SIGIC_InternalStaff");

        // Assert
        Assert.False(valResult.IsValid);
        Assert.NotNull(valResult.FailureReason);
    }

    [Fact]
    public void PublicKeyOnlyInstance_CannotGenerateToken_ButValidatesSuccessfully()
    {
        // Arrange: Servicio inicializado ÚNICAMENTE con clave pública
        var publicOnlyService = new RsaJwtTokenService(null, _publicKey, issuer: "SIGIC_Backend");

        // Act & Assert 1: Intentar firmar debe fallar de inmediato
        Assert.Throws<InvalidOperationException>(() =>
            publicOnlyService.GenerateToken(Guid.NewGuid(), "usuario", new[] { "Cajero" }, "SIGIC_InternalStaff"));

        // Act & Assert 2: Puede validar tokens legítimos firmados por el servicio emisor
        var tokenResult = _jwtService.GenerateToken(Guid.NewGuid(), "cajero01", new[] { "Cajero" }, "SIGIC_InternalStaff");
        var valResult = publicOnlyService.ValidateToken(tokenResult.Token, "SIGIC_InternalStaff");

        Assert.True(valResult.IsValid);
        Assert.Equal("cajero01", valResult.Username);
    }

    [Fact]
    public void ScopesAndRoles_EnforceDifferentiation_InternalStaffVsWebCustomer()
    {
        // 1. Internal Staff
        var staffResult = _jwtService.GenerateToken(
            Guid.NewGuid(), "cajero_pos", new[] { "Cajero", "Ventas" }, "SIGIC_InternalStaff");
        var handler = new JwtSecurityTokenHandler();
        var staffJwt = handler.ReadJwtToken(staffResult.Token);

        Assert.Contains(staffJwt.Claims, c => c.Type == "scope" && c.Value == "pos_terminal");
        Assert.Contains(staffJwt.Claims, c => c.Type == "role" && c.Value == "Cajero");

        // 2. Web Customer: lista blanca estricta (solo ClienteWeb, PublicoGeneral, MaestroObra, ContratistaB2B)
        var webResult = _jwtService.GenerateToken(
            Guid.NewGuid(), "cliente_online",
            new[] { "ClienteWeb", "MaestroObra" },
            "SIGIC_WebClient");
        var webJwt = handler.ReadJwtToken(webResult.Token);

        Assert.Contains(webJwt.Claims, c => c.Type == "scope" && c.Value == "web_customer");
        Assert.Contains(webJwt.Claims, c => c.Type == "role" && c.Value == "ClienteWeb");
        Assert.Contains(webJwt.Claims, c => c.Type == "role" && c.Value == "MaestroObra");

        // 3. Web Customer: emisión con roles fuera de lista blanca debe lanzar ArgumentException
        Assert.Throws<ArgumentException>(() =>
            _jwtService.GenerateToken(
                Guid.NewGuid(), "cliente_malicioso",
                new[] { "ClienteWeb", "Cajero" },
                "SIGIC_WebClient"));

        Assert.Throws<ArgumentException>(() =>
            _jwtService.GenerateToken(
                Guid.NewGuid(), "cliente_malicioso",
                new[] { "Superadmin" },
                "SIGIC_WebClient"));
    }
}
