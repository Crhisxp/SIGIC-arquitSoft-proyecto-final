namespace SIGIC.Application.Security.Ports;

/// <summary>
/// Servicio de autorización de permisos y roles para casos de uso de aplicación (RNF-SEG-001).
/// </summary>
public interface IAuthorizationService
{
    void ExigirRol(string rolRequerido);
    void ExigirUnoDeRoles(params string[] rolesRequeridos);
}
