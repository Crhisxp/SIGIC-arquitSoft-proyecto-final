using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIGIC.Infrastructure.Persistence;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Fixtures;

public class PostgreSqlTestFixture : IAsyncLifetime
{
    public string Host { get; }
    public int Port { get; }
    public string Database { get; }
    public string AppUsername { get; }
    public string AppPassword { get; }
    public string AdminUsername { get; }
    public string AdminPassword { get; }

    public string AppConnectionString { get; }
    public string AdminConnectionString { get; }
    public string PosPoolConnectionString { get; }
    public string WebPoolConnectionString { get; }

    public const string TestPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAkrXkEAolTj0pOYpFVEK4
        Jw7reg0LHUdKcAeGLCG/bYzTz5MjUDjxBWd8A8DLXK6s0GZ1Z+SLgxET179a5lqD
        KWsuKyE9xAPYW+2M0RGVSgnvQl0hV+DfkNRFP5T6l1/6cY9MaGMoVbYrYTTBzjPq
        qY4dTh6FZVExmRCM+X1UpNyv+vkOhbpWAZyq59+wAe6tRFEkHGMxsu4QcC/6rZmK
        ONvsyvIM689xOwUQitR9oOXkjWUSfbkaigQ9G5qlTkpARyPIs0UTES+k8ZnIf6SM
        /nAsZvZYGrAJgmHkR/Vhrr2cmPj3dNfBKnSNJkvvOyL5Q0QuNsiS6Fv9qQ/QiaTL
        RwIDAQAB
        -----END PUBLIC KEY-----
        """;

    public PostgreSqlTestFixture()
    {
        Host = Environment.GetEnvironmentVariable("SIGIC_DB_HOST") ?? "localhost";
        Port = int.TryParse(Environment.GetEnvironmentVariable("SIGIC_DB_PORT"), out int p) ? p : 54329;
        Database = Environment.GetEnvironmentVariable("SIGIC_DB_NAME") ?? "postgres";
        AppUsername = Environment.GetEnvironmentVariable("SIGIC_DB_USER") ?? "sigic_app";
        AppPassword = Environment.GetEnvironmentVariable("SIGIC_DB_PASSWORD") ?? "sigic_app_password_2026";
        AdminUsername = Environment.GetEnvironmentVariable("SIGIC_DB_ADMIN_USER") ?? "postgres";
        AdminPassword = Environment.GetEnvironmentVariable("SIGIC_DB_ADMIN_PASSWORD") ?? "postgres";

        AppConnectionString = $"Host={Host};Port={Port};Database={Database};Username={AppUsername};Password={AppPassword};Pooling=true;Maximum Pool Size=120;";
        AdminConnectionString = $"Host={Host};Port={Port};Database={Database};Username={AdminUsername};Password={AdminPassword};Pooling=true;Maximum Pool Size=50;";

        PosPoolConnectionString = Environment.GetEnvironmentVariable("SIGIC_POS_CONNECTION_STRING")
            ?? $"Host={Host};Port=6432;Database=sigic_pos_pool;Username={AppUsername};Password={AppPassword};Pooling=false;";

        WebPoolConnectionString = Environment.GetEnvironmentVariable("SIGIC_WEB_CONNECTION_STRING")
            ?? $"Host={Host};Port=6432;Database=sigic_web_pool;Username={AppUsername};Password={AppPassword};Pooling=false;";
    }

    public async Task InitializeAsync()
    {
        // Aplicar automáticamente el DDL inicial y permisos sobre base limpia/vacía de forma reproducible
        await using var adminConn = new NpgsqlConnection(AdminConnectionString);
        await adminConn.OpenAsync();

        bool schemaExists = await adminConn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'productos');");

        string scriptsDir = FindScriptsDirectory();

        if (!schemaExists)
        {
            string ddlPath = Path.Combine(scriptsDir, "00_init_schema.sql");
            if (File.Exists(ddlPath))
            {
                string ddlSql = await File.ReadAllTextAsync(ddlPath);
                await adminConn.ExecuteAsync(ddlSql);
            }
        }

        string permPath = Path.Combine(scriptsDir, "01_append_only_permissions.sql");
        if (File.Exists(permPath))
        {
            string permSql = await File.ReadAllTextAsync(permPath);
            await adminConn.ExecuteAsync(permSql);
        }

        string reportingSql = await File.ReadAllTextAsync(Path.Combine(scriptsDir, "02_cierres_analiticos.sql"));
        await adminConn.ExecuteAsync(reportingSql);
        string imagenSql = await File.ReadAllTextAsync(Path.Combine(scriptsDir, "03_producto_imagen.sql"));
        await adminConn.ExecuteAsync(imagenSql);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public SigicDbContext CreateDbContext(string? connectionString = null)
    {
        var options = new DbContextOptionsBuilder<SigicDbContext>()
            .UseNpgsql(connectionString ?? AppConnectionString)
            .Options;
        return new SigicDbContext(options);
    }

    public async Task<NpgsqlConnection> CreateConnectionAsync(string? connectionString = null)
    {
        var conn = new NpgsqlConnection(connectionString ?? AppConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static string FindScriptsDirectory()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "src", "SIGIC.Infrastructure", "Persistence", "Scripts");
            if (Directory.Exists(candidate))
                return candidate;

            string parent = Path.GetDirectoryName(current)!;
            if (parent == current) break;
            current = parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "src", "SIGIC.Infrastructure", "Persistence", "Scripts");
    }
}
