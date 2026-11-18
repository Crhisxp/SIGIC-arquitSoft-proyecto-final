using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SIGIC.Application.Security.Ports;

namespace SIGIC.Infrastructure.Security;

/// <summary>
/// Emisión y validación criptográfica asimétrica de tokens JWT con algoritmo RS256 (RNF-SEG-002, RNF-SEG-004).
/// Invariante: Tiempo de expiración estricto de 15 minutos, segregación inviolable de audiencias
/// ('SIGIC_WebClient' vs 'SIGIC_InternalStaff'), scopes canónicos y validación con clave exclusivamente pública.
/// </summary>
public class RsaJwtTokenService : IJwtTokenService
{
    private readonly SecurityKey? _signingKey;
    private readonly SecurityKey _validationKey;
    private readonly string _issuer;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);

    public const string WebAudience = "SIGIC_WebClient";
    public const string InternalAudience = "SIGIC_InternalStaff";

    public static readonly HashSet<string> RolesWebPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClienteWeb", "PublicoGeneral", "MaestroObra", "ContratistaB2B"
    };

    public RsaJwtTokenService(
        SecurityKey? signingKey,
        SecurityKey validationKey,
        string issuer = "SIGIC_Backend")
    {
        _signingKey = signingKey;
        _validationKey = validationKey ?? throw new ArgumentNullException(nameof(validationKey));
        _issuer = string.IsNullOrWhiteSpace(issuer) ? "SIGIC_Backend" : issuer;
    }

    public JwtSecurityTokenResult GenerateToken(
        Guid userId,
        string username,
        IEnumerable<string> roles,
        string audience)
    {
        if (_signingKey == null)
            throw new InvalidOperationException("Esta instancia de IJwtTokenService está configurada en modo exclusivo de validación con clave pública.");

        if (userId == Guid.Empty)
            throw new ArgumentException("El identificador de usuario 'sub' no puede ser Guid.Empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(audience))
            throw new ArgumentException("La audiencia es obligatoria.", nameof(audience));

        if (audience != WebAudience && audience != InternalAudience)
            throw new ArgumentException($"Audiencia '{audience}' no reconocida por la política de seguridad.", nameof(audience));

        var now = DateTime.UtcNow;
        var expires = now.Add(TokenLifetime);
        string jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iss, _issuer),
            new(JwtRegisteredClaimNames.Aud, audience),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (audience == InternalAudience)
        {
            claims.Add(new Claim("scope", "pos_terminal"));
            claims.Add(new Claim("terminal_scope", "pos_terminal"));

            foreach (var role in roles ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(role))
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else // WebAudience
        {
            claims.Add(new Claim("scope", "web_customer"));
            claims.Add(new Claim("terminal_scope", "web_client"));

            // Lista blanca estricta para clientes web (RNF-SEG-002, RNF-SEG-004)
            foreach (var role in roles ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(role))
                    continue;

                if (!RolesWebPermitidos.Contains(role))
                {
                    throw new ArgumentException($"El rol '{role}' no está autorizado para tokens del canal web (audiencia '{WebAudience}'). Solo se permiten: {string.Join(", ", RolesWebPermitidos)}.", nameof(roles));
                }

                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = audience,
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = signingCredentials
        };

        var handler = new JwtSecurityTokenHandler();
        var securityToken = handler.CreateJwtSecurityToken(tokenDescriptor);
        string tokenString = handler.WriteToken(securityToken);

        return new JwtSecurityTokenResult(tokenString, expires, jti);
    }

    public JwtValidationResult ValidateToken(string token, string expectedAudience)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, "Token nulo o vacío");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _validationKey,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            ClockSkew = AllowedClockSkew,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            ClaimsPrincipal principal = handler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, "Algoritmo criptográfico inválido (debe ser RS256)");
            }

            string? subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(subClaim) || !Guid.TryParse(subClaim, out Guid userId) || userId == Guid.Empty)
            {
                return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, "Identificador 'sub' inválido (Guid.Empty o formato erróneo)");
            }

            string? scopeClaim = principal.FindFirst("scope")?.Value;
            string expectedScope = expectedAudience == WebAudience ? "web_customer" : "pos_terminal";
            if (scopeClaim != expectedScope)
            {
                return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, $"Scope inválido para audiencia '{expectedAudience}': se esperaba '{expectedScope}', se obtuvo '{scopeClaim}'");
            }

            string? username = principal.FindFirst(ClaimTypes.Name)?.Value
                               ?? principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;

            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();
            if (expectedAudience == WebAudience && roles.Any(r => !RolesWebPermitidos.Contains(r)))
            {
                return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, "Token Web contiene roles no autorizados fuera de la lista blanca.");
            }

            string? audience = jwtToken.Audiences.FirstOrDefault();

            return new JwtValidationResult(true, userId, username, roles.AsReadOnly(), audience, null);
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, $"Audiencia inválida: {ex.Message}");
        }
        catch (SecurityTokenExpiredException ex)
        {
            return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, $"Token expirado: {ex.Message}");
        }
        catch (SecurityTokenValidationException ex)
        {
            return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, $"Firma o token inválido: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new JwtValidationResult(false, null, null, Array.Empty<string>(), null, $"Error de validación: {ex.Message}");
        }
    }
}
