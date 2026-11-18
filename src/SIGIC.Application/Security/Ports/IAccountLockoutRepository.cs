namespace SIGIC.Application.Security.Ports;

public record UserAccountStatusDto(
    Guid UserId,
    string Username,
    string PasswordHash,
    int IntentosFallidos,
    DateTime? BloqueadoHastaUtc,
    bool EstaActivo,
    IReadOnlyList<string> Roles);

/// <summary>
/// Puerto para persistencia y control de bloqueo de cuenta por intentos fallidos (RNF-SEG-005).
/// Bloquea temporalmente por 15 minutos tras 5 intentos fallidos consecutivos.
/// </summary>
public interface IAccountLockoutRepository
{
    Task<UserAccountStatusDto?> ObtenerEstadoCuentaAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task RegistrarIntentoFallidoAsync(
        string username,
        DateTime fechaIntentoUtc,
        CancellationToken cancellationToken = default);

    Task RestablecerIntentosFallidosAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task BloquearCuentaAsync(
        string username,
        DateTime bloqueadoHastaUtc,
        CancellationToken cancellationToken = default);

    Task RegistrarUsuarioAsync(
        Guid id,
        string username,
        string passwordHash,
        IReadOnlyList<string> roles,
        string perfilCliente = "PublicoGeneral",
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
