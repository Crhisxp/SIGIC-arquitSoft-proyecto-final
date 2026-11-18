using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.Common.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.Common;

/// <summary>
/// Adaptador para resolución de perfil homologado de clientes sobre PostgreSQL 16 (RF-PRE-001, RF-PRE-002).
/// Invariante: Previene escalamiento malicioso de roles de precio desde clientes HTTP o terminales POS.
/// </summary>
public class PostgresCustomerProfilePort : ICustomerProfilePort
{
    private readonly SigicDbContext _dbContext;

    public PostgresCustomerProfilePort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<string> ObtenerPerfilClienteAsync(
        Guid? clienteId,
        CancellationToken cancellationToken = default)
    {
        if (!clienteId.HasValue || clienteId.Value == Guid.Empty)
            return "PublicoGeneral";

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT perfil_cliente
            FROM usuarios
            WHERE id = @clienteId;
            """;

        var cmd = new CommandDefinition(sql, new { clienteId = clienteId.Value }, tx, cancellationToken: cancellationToken);
        var perfil = await conn.QuerySingleOrDefaultAsync<string?>(cmd);

        return string.IsNullOrWhiteSpace(perfil) ? "PublicoGeneral" : perfil;
    }
}
