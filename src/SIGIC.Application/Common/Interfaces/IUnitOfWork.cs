namespace SIGIC.Application.Common.Interfaces;

/// <summary>
/// Contrato de Unidad de Trabajo con soporte explícito para transacciones atómicas y segregación de pools en PgBouncer (RNF-CON-001).
/// En el adaptador de infraestructura de PostgreSQL 16:
/// - Inicia transacción corta explícita (BEGIN).
/// - Adquiere bloqueos de fila deterministas en orden Havender ASC (SELECT ... FOR UPDATE).
/// - Ejecuta actualizaciones condicionadas con verificación estricta de filas afectadas (rowsAffected == 1).
/// - Confirma (COMMIT) o revierte (ROLLBACK) ante cualquier fallo.
/// - Conecta al connection pool correspondiente ("sigic_pos_pool" para LAN POS o "sigic_web_pool" para WAN Web).
/// </summary>
public interface IUnitOfWork
{
    string PoolName { get; }
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionWithRetryAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
