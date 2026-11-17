namespace SIGIC.Application.Security.Ports;

/// <summary>
/// Provee información del usuario autenticado en la solicitud actual (RNF-SEG-001).
/// </summary>
public interface ICurrentUserContext
{
    Guid UserId { get; }
    string Username { get; }
    IReadOnlyList<string> Roles { get; }
    string TerminalScope { get; }
    string ClientIp { get; }

    bool HasRole(string role);
    bool IsInRole(string role);
}
