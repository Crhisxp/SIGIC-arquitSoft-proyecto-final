using SIGIC.Application.Security.Ports;

namespace SIGIC.Infrastructure.Security;

/// <summary>
/// Accesor de contexto ambiental para asignar scopes específicos a background services y tareas asíncronas.
/// </summary>
public static class CurrentUserContextAccessor
{
    private static readonly AsyncLocal<ICurrentUserContext?> _current = new();

    public static ICurrentUserContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
