using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIGIC.Infrastructure.Adapters.Security;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Security;

[Collection("PostgreSqlDatabase")]
public class AccountLockoutRepositoryTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public AccountLockoutRepositoryTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    private SigicDbContext CreateDbContext()
    {
        return _fixture.CreateDbContext();
    }

    [Fact]
    public async Task AccountLockout_FiveConsecutiveFailures_LocksFor15Minutes()
    {
        string username = $"user_{Guid.NewGuid():N}";
        var userId = Guid.NewGuid();

        await using var conn = await _fixture.CreateConnectionAsync();

        await conn.ExecuteAsync("""
            INSERT INTO usuarios (id, username, password_hash, intentos_fallidos, bloqueado_hasta_utc, esta_activo, roles_json, perfil_cliente)
            VALUES (@userId, @username, '$argon2id$v=19$dummy', 0, NULL, TRUE, '["Vendedor"]', 'PublicoGeneral');
            """, new { userId, username });

        await using var db = CreateDbContext();
        var repo = new PostgresAccountLockoutRepository(db);

        var now = DateTime.UtcNow;

        // Intentos 1 a 4: no debe bloquearse
        for (int i = 1; i <= 4; i++)
        {
            await repo.RegistrarIntentoFallidoAsync(username, now);
            var status = await repo.ObtenerEstadoCuentaAsync(username);
            Assert.NotNull(status);
            Assert.Equal(i, status.IntentosFallidos);
            Assert.Null(status.BloqueadoHastaUtc);
        }

        // Intento 5: debe bloquearse por 15 minutos (RNF-SEG-005)
        await repo.RegistrarIntentoFallidoAsync(username, now);
        var statusLocked = await repo.ObtenerEstadoCuentaAsync(username);
        Assert.NotNull(statusLocked);
        Assert.Equal(5, statusLocked.IntentosFallidos);
        Assert.NotNull(statusLocked.BloqueadoHastaUtc);
        Assert.True(statusLocked.BloqueadoHastaUtc >= now.AddMinutes(14));

        // Restablecimiento al ingresar exitosamente
        await repo.RestablecerIntentosFallidosAsync(username);
        var statusReset = await repo.ObtenerEstadoCuentaAsync(username);
        Assert.NotNull(statusReset);
        Assert.Equal(0, statusReset.IntentosFallidos);
        Assert.Null(statusReset.BloqueadoHastaUtc);
    }
}
