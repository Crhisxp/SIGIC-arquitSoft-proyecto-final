using SIGIC.Application.CashManagement.Ports;

namespace SIGIC.Application.Tests.TestDoubles;

public class FakeCashAlertNotificationPort : ICashAlertNotificationPort
{
    private readonly FakeUnitOfWork? _uow;
    public List<(Guid TurnoId, decimal Diferencia, string Justificacion)> Alertas { get; } = new();

    public FakeCashAlertNotificationPort(FakeUnitOfWork? uow = null)
    {
        _uow = uow;
    }

    public Task NotificarDiscrepanciaArqueoAsync(
        Guid turnoCajaId,
        Guid cajeroId,
        decimal diferencia,
        string justificacion,
        DateTime fechaCierreUtc,
        CancellationToken cancellationToken = default)
    {
        var item = (turnoCajaId, diferencia, justificacion);
        Alertas.Add(item);
        _uow?.RegisterRollbackAction(() => Alertas.Remove(item));
        return Task.CompletedTask;
    }
}
