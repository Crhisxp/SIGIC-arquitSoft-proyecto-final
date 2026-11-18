using Microsoft.EntityFrameworkCore;
using SIGIC.Application.Common.Interfaces;

namespace SIGIC.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly SigicDbContext _dbContext;
    private readonly string _poolName;
    private TransactionWrapper? _currentTransaction;

    public string PoolName => _poolName;

    public UnitOfWork(SigicDbContext dbContext, IOperationChannelContext? channelContext = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _poolName = channelContext?.PoolName ?? "sigic_pos_pool";
    }

    public UnitOfWork(SigicDbContext dbContext, string poolName)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _poolName = string.IsNullOrWhiteSpace(poolName) ? "sigic_pos_pool" : poolName;
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
            return _currentTransaction;

        var efTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _currentTransaction = new TransactionWrapper(efTransaction, () => _currentTransaction = null);
        return _currentTransaction;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
            catch
            {
                // Supresión segura si PostgreSQL ya abortó la transacción internamente
            }
            finally
            {
                _currentTransaction = null;
            }
        }
        _dbContext.ChangeTracker.Clear();
    }

    public async Task<T> ExecuteInTransactionWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await using var tx = await BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientPostgresError(ex))
            {
                await RollbackAsync(cancellationToken);
                int delayMs = (int)(Math.Pow(2, attempt) * 25) + Random.Shared.Next(10, 30);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch
            {
                await RollbackAsync(cancellationToken);
                throw;
            }
        }

        throw new InvalidOperationException("Se superó el número máximo de reintentos para la operación transaccional.");
    }

    public Task ExecuteInTransactionWithRetryAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteInTransactionWithRetryAsync<bool>(async ct =>
        {
            await operation(ct);
            return true;
        }, cancellationToken);
    }

    private static bool IsTransientPostgresError(Exception ex)
    {
        if (ex is Npgsql.PostgresException pex && (pex.SqlState == "40P01" || pex.SqlState == "57014"))
            return true;
        if (ex.InnerException is Npgsql.PostgresException innerPex && (innerPex.SqlState == "40P01" || innerPex.SqlState == "57014"))
            return true;
        var baseEx = ex.GetBaseException() as Npgsql.PostgresException;
        return baseEx != null && (baseEx.SqlState == "40P01" || baseEx.SqlState == "57014");
    }
}
