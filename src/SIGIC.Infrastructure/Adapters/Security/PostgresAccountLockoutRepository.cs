using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.Security.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.Security;

/// <summary>
/// Repositorio para gestión de cuentas de usuario y control de bloqueo por intentos fallidos (RNF-SEG-005).
/// Invariante: Bloqueo de 15 minutos tras 5 fallos consecutivos.
/// </summary>
public class PostgresAccountLockoutRepository : IAccountLockoutRepository
{
    private readonly SigicDbContext _dbContext;

    public PostgresAccountLockoutRepository(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<UserAccountStatusDto?> ObtenerEstadoCuentaAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT id AS Id, username AS Username, password_hash AS PasswordHash,
                   intentos_fallidos AS IntentosFallidos, bloqueado_hasta_utc AS BloqueadoHastaUtc,
                   esta_activo AS EstaActivo, roles_json AS RolesJson
            FROM usuarios
            WHERE UPPER(username) = UPPER(@username);
            """;

        var cmd = new CommandDefinition(sql, new { username }, tx, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<UserRow>(cmd);

        if (row == null)
            return null;

        var roles = ParseRoles(row.RolesJson);

        return new UserAccountStatusDto(
            row.Id, row.Username, row.PasswordHash, row.IntentosFallidos,
            row.BloqueadoHastaUtc, row.EstaActivo, roles);
    }

    public async Task RegistrarIntentoFallidoAsync(
        string username,
        DateTime fechaIntentoUtc,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            UPDATE usuarios
            SET intentos_fallidos = intentos_fallidos + 1,
                bloqueado_hasta_utc = CASE
                    WHEN (intentos_fallidos + 1) >= 5 THEN @fechaIntentoUtc + INTERVAL '15 minutes'
                    ELSE bloqueado_hasta_utc
                END
            WHERE UPPER(username) = UPPER(@username);
            """;

        var cmd = new CommandDefinition(sql, new { username, fechaIntentoUtc }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    public async Task RestablecerIntentosFallidosAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            UPDATE usuarios
            SET intentos_fallidos = 0,
                bloqueado_hasta_utc = NULL
            WHERE UPPER(username) = UPPER(@username);
            """;

        var cmd = new CommandDefinition(sql, new { username }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    public async Task BloquearCuentaAsync(
        string username,
        DateTime bloqueadoHastaUtc,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            UPDATE usuarios
            SET bloqueado_hasta_utc = @bloqueadoHastaUtc
            WHERE UPPER(username) = UPPER(@username);
            """;

        var cmd = new CommandDefinition(sql, new { username, bloqueadoHastaUtc }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    public async Task RegistrarUsuarioAsync(
        Guid id,
        string username,
        string passwordHash,
        IReadOnlyList<string> roles,
        string perfilCliente = "PublicoGeneral",
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        string rolesJson = JsonSerializer.Serialize(roles);

        const string sql = """
            INSERT INTO usuarios (id, username, password_hash, intentos_fallidos, bloqueado_hasta_utc, esta_activo, roles_json, perfil_cliente)
            VALUES (@id, @username, @passwordHash, 0, NULL, TRUE, @rolesJson, @perfilCliente);
            """;

        var cmd = new CommandDefinition(sql, new { id, username, passwordHash, rolesJson, perfilCliente }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private static IReadOnlyList<string> ParseRoles(string? rolesJson)
    {
        if (string.IsNullOrWhiteSpace(rolesJson))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(rolesJson) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private class UserRow
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int IntentosFallidos { get; set; }
        public DateTime? BloqueadoHastaUtc { get; set; }
        public bool EstaActivo { get; set; }
        public string RolesJson { get; set; } = "[]";
    }
}
