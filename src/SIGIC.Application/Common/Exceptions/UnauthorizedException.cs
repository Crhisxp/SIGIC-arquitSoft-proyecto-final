namespace SIGIC.Application.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando el usuario no cuenta con los permisos o roles requeridos.
/// </summary>
public class UnauthorizedException : Exception
{
    public string RolRequerido { get; }

    public UnauthorizedException(string message) : base(message)
    {
        RolRequerido = string.Empty;
    }

    public UnauthorizedException(string rolRequerido, string message) : base(message)
    {
        RolRequerido = rolRequerido;
    }
}
