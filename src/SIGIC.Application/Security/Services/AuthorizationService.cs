using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Security.Ports;

namespace SIGIC.Application.Security.Services;

/// <summary>
/// Servicio de evaluación de políticas de autorización y control de acceso basado en roles (RNF-SEG-001).
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserContext _currentUserContext;

    public AuthorizationService(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    public void ExigirRol(string rolRequerido)
    {
        if (string.IsNullOrWhiteSpace(rolRequerido))
            throw new ArgumentException("El rol requerido no puede ser nulo o vacío.", nameof(rolRequerido));

        if (!_currentUserContext.HasRole(rolRequerido))
        {
            throw new UnauthorizedException(
                rolRequerido,
                $"Acceso denegado. La operación requiere el rol '{rolRequerido}'. Usuario actual: '{_currentUserContext.Username}'.");
        }
    }

    public void ExigirUnoDeRoles(params string[] rolesRequeridos)
    {
        if (rolesRequeridos == null || rolesRequeridos.Length == 0)
            throw new ArgumentException("Debe especificarse al menos un rol requerido.", nameof(rolesRequeridos));

        if (!rolesRequeridos.Any(rol => _currentUserContext.HasRole(rol)))
        {
            string rolesStr = string.Join(",", rolesRequeridos);
            throw new UnauthorizedException(
                rolesStr,
                $"Acceso denegado. La operación requiere al menos uno de los siguientes roles: [{rolesStr}]. Usuario actual: '{_currentUserContext.Username}'.");
        }
    }
}
