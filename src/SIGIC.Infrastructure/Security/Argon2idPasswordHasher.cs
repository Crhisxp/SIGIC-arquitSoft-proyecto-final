using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using SIGIC.Application.Security.Ports;

namespace SIGIC.Infrastructure.Security;

/// <summary>
/// Implementación criptográfica de derivación y verificación de contraseñas con Argon2id (RNF-SEG-002).
/// Parámetros de alta seguridad: m=65536 KiB (64 MiB), t=3 iteraciones, p=4 paralelismo, salt de 16 bytes.
/// Formato versionado modular: $argon2id$v=19$m=65536,t=3,p=4${salt_base64}${hash_base64}.
/// Consistencia estricta: rechaza cadenas vacías o de solo espacios tanto al hashear como al verificar.
/// </summary>
public class Argon2idPasswordHasher : IPasswordHasher
{
    private const int MemorySizeKiB = 65536;
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 4;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("La contraseña no puede ser nula, vacía ni contener solo espacios en blanco.", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hash = DeriveHash(password, salt);

        string saltBase64 = Convert.ToBase64String(salt);
        string hashBase64 = Convert.ToBase64String(hash);

        return $"$argon2id$v=19$m={MemorySizeKiB},t={Iterations},p={DegreeOfParallelism}${saltBase64}${hashBase64}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        string[] parts = passwordHash.Split('$');
        if (parts.Length < 6)
            return false;

        if (parts[1] != "argon2id" || parts[2] != "v=19")
            return false;

        string expectedParams = $"m={MemorySizeKiB},t={Iterations},p={DegreeOfParallelism}";
        if (parts[3] != expectedParams)
            return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expectedHash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != SaltSizeBytes || expectedHash.Length != HashSizeBytes)
            return false;

        byte[] computedHash = DeriveHash(password, salt);
        return CryptographicOperations.FixedTimeEquals(expectedHash, computedHash);
    }

    private static byte[] DeriveHash(string password, byte[] salt)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            MemorySize = MemorySizeKiB,
            Iterations = Iterations,
            DegreeOfParallelism = DegreeOfParallelism
        };
        return argon2.GetBytes(HashSizeBytes);
    }
}
