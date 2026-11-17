using SIGIC.Application.Security.Ports;

namespace SIGIC.Application.Security.Services;

/// <summary>
/// Implementación inmutable del contexto del usuario autenticado en la solicitud actual.
/// </summary>
public class CurrentUserContext : ICurrentUserContext
{
    public Guid UserId { get; }
    public string Username { get; }
    public IReadOnlyList<string> Roles { get; }
    public string TerminalScope { get; }
    public string ClientIp { get; }

    public CurrentUserContext(
        Guid userId,
        string username,
        IEnumerable<string> roles,
        string terminalScope,
        string clientIp)
    {
        UserId = userId;
        Username = username ?? string.Empty;
        Roles = roles?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        TerminalScope = terminalScope ?? string.Empty;
        ClientIp = clientIp ?? string.Empty;
    }

    public bool HasRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return Roles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsInRole(string role) => HasRole(role);
}
