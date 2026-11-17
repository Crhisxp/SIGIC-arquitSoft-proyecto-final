using SIGIC.Application.Common.Interfaces;

namespace SIGIC.Application.Tests.TestDoubles;

public class FakeTransaction : ITransaction
{
    private readonly FakeUnitOfWork _uow;
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public FakeTransaction(FakeUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_uow.FailOnTransactionCommit)
            throw new InvalidOperationException("Fallo inyectado en Commit de transacción.");

        IsCommitted = true;
        _uow.ExecuteCommitActions();
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        IsRolledBack = true;
        _uow.ExecuteRollbackActions();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!IsCommitted && !IsRolledBack)
        {
            _uow.ExecuteRollbackActions();
        }
        _uow.ExecuteCleanupActions();
        return ValueTask.CompletedTask;
    }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public string PoolName { get; set; } = "sigic_pos_pool";
    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }
    public bool FailOnCommit { get; set; }
    public bool FailOnTransactionCommit { get; set; }

    private readonly List<Action> _rollbackActions = new();
    private readonly List<Action> _commitActions = new();
    private readonly List<Action> _cleanupActions = new();

    public void RegisterRollbackAction(Action action)
    {
        _rollbackActions.Add(action);
    }

    public void RegisterCommitAction(Action action)
    {
        _commitActions.Add(action);
    }

    public void RegisterCleanupAction(Action action)
    {
        _cleanupActions.Add(action);
    }

    public void ExecuteRollbackActions()
    {
        for (int i = _rollbackActions.Count - 1; i >= 0; i--)
        {
            _rollbackActions[i]();
        }
        _rollbackActions.Clear();
    }

    public void ExecuteCommitActions()
    {
        foreach (var action in _commitActions)
        {
            action();
        }
        _commitActions.Clear();
        _rollbackActions.Clear();
    }

    public void ExecuteCleanupActions()
    {
        foreach (var action in _cleanupActions)
        {
            action();
        }
        _cleanupActions.Clear();
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ITransaction>(new FakeTransaction(this));
    }

    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (FailOnCommit)
            throw new InvalidOperationException("Fallo inyectado durante UnitOfWork.CommitAsync.");

        Commits++;
        return Task.FromResult(1);
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        Rollbacks++;
        ExecuteRollbackActions();
        return Task.CompletedTask;
    }
}
