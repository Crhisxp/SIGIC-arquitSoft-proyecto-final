using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Entities;

namespace SIGIC.Application.CashManagement.Handlers;

/// <summary>
/// Caso de uso para arqueo ciego e inmutable de cierre de caja (AC03, RF-CAJ-002, RNF-SEG-004).
/// Garantiza cero visibilidad de saldos teóricos al cajero y registro append-only inmutable.
/// </summary>
public class RegistrarArqueoCiegoCommandHandler
{
    private static readonly HashSet<decimal> DenominacionesOficiales = new()
    {
        0.10m, 0.20m, 0.50m, 1.00m, 2.00m, 5.00m, 10.00m, 20.00m, 50.00m, 100.00m, 200.00m
    };

    private readonly IAuthorizationService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICashRegisterRepository _cashRegisterRepository;
    private readonly IAuditLogPort _auditLogPort;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashAlertNotificationPort _alertNotificationPort;

    public RegistrarArqueoCiegoCommandHandler(
        IAuthorizationService authService,
        ICurrentUserContext currentUserContext,
        ICashRegisterRepository cashRegisterRepository,
        IAuditLogPort auditLogPort,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ICashAlertNotificationPort alertNotificationPort)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _cashRegisterRepository = cashRegisterRepository ?? throw new ArgumentNullException(nameof(cashRegisterRepository));
        _auditLogPort = auditLogPort ?? throw new ArgumentNullException(nameof(auditLogPort));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _alertNotificationPort = alertNotificationPort ?? throw new ArgumentNullException(nameof(alertNotificationPort));
    }

    public async Task<ArqueoResultDto> Handle(RegistrarArqueoCiegoCommand command, CancellationToken cancellationToken = default)
    {
        _authService.ExigirRol("Cajero");

        if (command.TurnoCajaId == Guid.Empty)
            throw new ValidationException("El ID de turno de caja es obligatorio.");

        if (command.DesgloseFisico == null || !command.DesgloseFisico.Any())
            throw new ValidationException("El arqueo exige el conteo físico de denominaciones.");

        foreach (var d in command.DesgloseFisico)
        {
            if (!DenominacionesOficiales.Contains(d.ValorUnitario) || d.Cantidad < 0)
                throw new ValidationException($"La denominación declarada S/ {d.ValorUnitario:F2} no corresponde a una denominación oficial del Cono Monetario del Perú o tiene cantidad negativa.");
        }

        bool turnoAperturado = await _cashRegisterRepository.EsTurnoAperturadoAsync(command.TurnoCajaId, cancellationToken);
        if (!turnoAperturado)
            throw new ValidationException("El turno de caja especificado no existe, no está aperturado o ya fue cerrado previamente.");

        Guid cajeroDeTurno = await _cashRegisterRepository.ObtenerCajeroDeTurnoAsync(command.TurnoCajaId, cancellationToken);
        if (cajeroDeTurno != _currentUserContext.UserId)
            throw new UnauthorizedException("Cajero", "El turno de caja pertenece a otro cajero.");

        await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // Invariante RF-CAJ-002: Transición atómica del turno ANTES de calcular el saldo para evitar intercalación de ventas POS
            bool cerradoOk = await _cashRegisterRepository.CerrarTurnoCajaConEstadoEsperadoAsync(command.TurnoCajaId, "Aperturado", cancellationToken);
            if (!cerradoOk)
            {
                throw new ValidationException("El turno de caja especificado no existe, no está aperturado o ya fue cerrado concurrentemente.");
            }

            decimal saldoTeorico = await _cashRegisterRepository.ObtenerSaldoTeoricoSistemaAsync(command.TurnoCajaId, cancellationToken);
            DateTime fechaAperturaUtc = await _cashRegisterRepository.ObtenerFechaAperturaTurnoAsync(command.TurnoCajaId, cancellationToken);
            decimal fondoInicial = await _cashRegisterRepository.ObtenerFondoInicialAsync(command.TurnoCajaId, cancellationToken);

            var desglose = command.DesgloseFisico
                .Select(d => new ArqueoDenominacion(d.ValorUnitario, d.Cantidad))
                .ToList();

            DateTime fechaCierreUtc = _dateTimeProvider.UtcNow;

            decimal totalTeoricoEsperado = fondoInicial + saldoTeorico;

            var arqueo = new ArqueoCaja(
                Guid.NewGuid(),
                command.TurnoCajaId,
                cajeroDeTurno,
                fechaAperturaUtc,
                fechaCierreUtc,
                fondoInicial,
                desglose,
                command.TotalDigitalDeclarado,
                totalTeoricoEsperado);

            // Invariante RF-CAJ-002: Justificación obligatoria y alerta a supervisión ante discrepancias
            if (Math.Abs(arqueo.Diferencia) > 0.00m)
            {
                if (string.IsNullOrWhiteSpace(command.JustificacionDiscrepancia))
                {
                    throw new ValidationException("Se exige una justificación obligatoria ante cualquier discrepancia detectada en el arqueo físico de caja.");
                }

                await _alertNotificationPort.NotificarDiscrepanciaArqueoAsync(
                    command.TurnoCajaId,
                    cajeroDeTurno,
                    arqueo.Diferencia,
                    command.JustificacionDiscrepancia,
                    fechaCierreUtc,
                    cancellationToken);
            }

            await _cashRegisterRepository.GuardarArqueoCajaAppendOnlyAsync(arqueo, command.JustificacionDiscrepancia, cancellationToken);

            // Auditoría Append-Only (RNF-SEG-004) - Se registra en auditoría interna para Backoffice
            await _auditLogPort.RegistrarAccionAsync(new AuditLogEntry(
                _currentUserContext.UserId,
                "REGISTRO_ARQUEO_CIEGO",
                "ArqueoCaja",
                arqueo.Id.ToString(),
                $"Turno: {command.TurnoCajaId}, TotalDeclarado: {arqueo.TotalDeclarado:F2}, Diferencia: {arqueo.Diferencia:F2}",
                _currentUserContext.ClientIp,
                _currentUserContext.TerminalScope,
                fechaCierreUtc), cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            // Cero visibilidad de saldos teóricos o discrepancias en la respuesta al cajero (AC03)
            return new ArqueoResultDto(
                ArqueoId: arqueo.Id,
                TurnoCajaId: command.TurnoCajaId,
                TotalDeclarado: arqueo.TotalDeclarado,
                FechaCierreUtc: fechaCierreUtc);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
