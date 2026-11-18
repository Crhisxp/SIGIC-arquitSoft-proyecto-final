namespace SIGIC.Application.Security.Ports;

/// <summary>
/// Abstracción pura para derivación y verificación criptográfica de contraseñas (RNF-SEG-002).
/// La implementación concreta en Infrastructure debe utilizar Argon2id con parámetros de alta seguridad.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
