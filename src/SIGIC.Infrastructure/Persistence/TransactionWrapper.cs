using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.Common.Interfaces;

namespace SIGIC.Infrastructure.Persistence;

public class TransactionWrapper : ITransaction
{
    private readonly IDbContextTransaction _efTransaction;
    private readonly Action _onCompleted;
    private bool _isCommitted;
    private bool _isRolledBack;
    private bool _isDisposed;

    public TransactionWrapper(IDbContextTransaction efTransaction, Action onCompleted)
    {
        _efTransaction = efTransaction ?? throw new ArgumentNullException(nameof(efTransaction));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(TransactionWrapper));

        if (_isRolledBack)
            throw new InvalidOperationException("No se puede confirmar una transacción que ya ha sido revertida.");

        if (!_isCommitted)
        {
            await _efTransaction.CommitAsync(cancellationToken);
            _isCommitted = true;
            _onCompleted();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(TransactionWrapper));

        if (!_isRolledBack && !_isCommitted)
        {
            await _efTransaction.RollbackAsync(cancellationToken);
            _isRolledBack = true;
            _onCompleted();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            if (!_isCommitted && !_isRolledBack)
            {
                try
                {
                    await _efTransaction.RollbackAsync();
                }
                catch
                {
                    // Supresión segura en dispose
                }
                _onCompleted();
            }

            await _efTransaction.DisposeAsync();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
