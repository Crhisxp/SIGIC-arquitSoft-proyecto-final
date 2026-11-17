namespace SIGIC.Application.Common.Interfaces;

/// <summary>
/// Representa una transacción transaccional explícita gestionada mediante puertos puros de Clean Architecture.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
