namespace SIGIC.Application.Security.Ports;

public record JwtSecurityTokenResult(
    string Token,
    DateTime ExpiresAtUtc,
    string Jti);

public record JwtValidationResult(
    bool IsValid,
    Guid? UserId,
    string? Username,
    IReadOnlyList<string> Roles,
    string? Audience,
    string? FailureReason);

/// <summary>
/// Abstracción pura para emisión y validación de tokens JWT asimétricos RS256 (RNF-SEG-002, RNF-SEG-004).
/// La implementación concreta en Infrastructure utiliza firma RSA >= 2048 bits con claves asimétricas.
/// </summary>
public interface IJwtTokenService
{
    JwtSecurityTokenResult GenerateToken(
        Guid userId,
        string username,
        IEnumerable<string> roles,
        string audience);

    JwtValidationResult ValidateToken(
        string token,
        string expectedAudience);
}
