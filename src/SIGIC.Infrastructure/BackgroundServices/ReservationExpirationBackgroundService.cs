using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Handlers;
using SIGIC.Application.Security.Services;
using SIGIC.Infrastructure.Security;

namespace SIGIC.Infrastructure.BackgroundServices;

/// <summary>
/// Worker en segundo plano para expiración y liberación de stock de reservas web vencidas (RF-WEB-002, RNF-CON-004).
/// Ejecuta ExpirarReservasWebCommand con scope aislado por ciclo y manejo observable de errores.
/// </summary>
public class ReservationExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public ReservationExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationExpirationBackgroundService> logger,
        TimeSpan? checkInterval = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio en segundo plano de expiración de reservas web iniciado con intervalo de {Intervalo}.", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cancelación recibida para ReservationExpirationBackgroundService. Deteniendo ciclo.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado durante el ciclo de expiración de reservas web.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Servicio en segundo plano de expiración de reservas web detenido.");
    }

    public async Task<int> ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        var prevContext = CurrentUserContextAccessor.Current;
        try
        {
            // Asigna canal WORKER (no LAN-POS) para el BackgroundService
            CurrentUserContextAccessor.Current = new CurrentUserContext(
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                "background_worker",
                new[] { "System", "Worker" },
                "WORKER",
                "127.0.0.1");

            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ExpirarReservasWebCommandHandler>();

            var command = new ExpirarReservasWebCommand();
            var resultado = await handler.Handle(command, cancellationToken);

            if (resultado.CantidadPedidosExpirados > 0)
            {
                _logger.LogInformation(
                    "Ciclo de expiración completado: {TotalExpirados} pedidos web expirados y stock comprometido liberado exitosamente.",
                    resultado.CantidadPedidosExpirados);
            }

            return resultado.CantidadPedidosExpirados;
        }
        finally
        {
            CurrentUserContextAccessor.Current = prevContext;
        }
    }
}
