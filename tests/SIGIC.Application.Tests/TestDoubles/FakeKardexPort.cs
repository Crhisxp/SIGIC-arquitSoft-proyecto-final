using SIGIC.Application.Inventory.Ports;

namespace SIGIC.Application.Tests.TestDoubles;

public class FakeKardexPort : IKardexPort
{
    private readonly FakeUnitOfWork? _uow;
    public List<(Guid ProductoId, Guid AlmacenId, decimal CantidadBase, string TipoOperacion)> Movimientos { get; } = new();

    public FakeKardexPort(FakeUnitOfWork? uow = null)
    {
        _uow = uow;
    }

    public Task RegistrarMovimientoSalidaAsync(
        Guid productoId,
        Guid almacenId,
        decimal cantidadBase,
        decimal costoUnitario,
        string tipoOperacion,
        string documentoReferencia,
        DateTime fechaUtc,
        CancellationToken cancellationToken = default)
    {
        var item = (productoId, almacenId, cantidadBase, tipoOperacion);
        Movimientos.Add(item);
        _uow?.RegisterRollbackAction(() => Movimientos.Remove(item));
        return Task.CompletedTask;
    }
}
