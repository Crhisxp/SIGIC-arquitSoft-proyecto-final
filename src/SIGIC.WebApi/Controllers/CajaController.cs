using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Handlers;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Application.Security.Ports;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/caja")]
public class CajaController : ControllerBase
{
    private readonly RegistrarArqueoCiegoCommandHandler _arqueoCiegoHandler;
    private readonly ValidarVoucherPagoCommandHandler _validarVoucherHandler;
    private readonly ICashShiftPort _cashShiftPort;
    private readonly ICurrentUserContext _userContext;

    public CajaController(
        RegistrarArqueoCiegoCommandHandler arqueoCiegoHandler,
        ValidarVoucherPagoCommandHandler validarVoucherHandler,
        ICashShiftPort cashShiftPort,
        ICurrentUserContext userContext)
    {
        _arqueoCiegoHandler = arqueoCiegoHandler ?? throw new ArgumentNullException(nameof(arqueoCiegoHandler));
        _validarVoucherHandler = validarVoucherHandler ?? throw new ArgumentNullException(nameof(validarVoucherHandler));
        _cashShiftPort = cashShiftPort ?? throw new ArgumentNullException(nameof(cashShiftPort));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    private IActionResult? ValidarAccesoStaffInterno()
    {
        if (_userContext.UserId == Guid.Empty)
            return Unauthorized(new { status = 401, title = "Unauthorized", detail = "Se requiere usuario autenticado para acceder al módulo de caja." });

        if (_userContext.TerminalScope != "LAN-POS" || !_userContext.Roles.Any())
            return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, title = "Forbidden", detail = "Audiencia o rol no autorizado para módulo de caja." });

        return null;
    }

    [HttpPost("turnos/apertura")]
    public async Task<IActionResult> AperturarTurno(
        [FromBody] AperturaTurnoRequestDto request,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        if (request.CajaId == Guid.Empty || request.CajeroId == Guid.Empty || request.AlmacenId == Guid.Empty)
            throw new ValidationException("CajaId, CajeroId y AlmacenId son obligatorios.");

        if (request.FondoInicial < 0m)
            throw new ValidationException("El fondo inicial no puede ser negativo.");

        var turnoActivo = await _cashShiftPort.ObtenerTurnoActualAsync(request.CajeroId, ct);
        if (turnoActivo != null)
            throw new ValidationException($"El cajero ya cuenta con el turno activo '{turnoActivo.TurnoId}'.");

        Guid turnoId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        await _cashShiftPort.AbrirTurnoAsync(
            turnoId, request.CajaId, request.CajeroId, request.AlmacenId, request.FondoInicial, now, ct);

        return StatusCode(StatusCodes.Status201Created, new
        {
            TurnoId = turnoId,
            CajaId = request.CajaId,
            CajeroId = request.CajeroId,
            AlmacenId = request.AlmacenId,
            FondoInicial = request.FondoInicial,
            Estado = "Aperturado",
            FechaAperturaUtc = now
        });
    }

    [HttpPost("turnos/cierre")]
    public async Task<IActionResult> CerrarTurnoConArqueoCiego(
        [FromBody] CierreTurnoRequestDto request,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        if (request.TurnoCajaId == Guid.Empty)
            throw new ValidationException("El TurnoCajaId es obligatorio para el cierre de turno.");

        if (request.DesgloseFisico == null || !request.DesgloseFisico.Any())
            throw new ValidationException("Debe ingresar el desglose físico de efectivo por denominación.");

        var command = new RegistrarArqueoCiegoCommand(
            request.TurnoCajaId,
            request.DesgloseFisico,
            request.TotalDigitalDeclarado,
            request.Justificacion);

        // Invariante Arqueo Ciego (RF-CAJ-002): Cero visibilidad de saldos teóricos al cajero
        var result = await _arqueoCiegoHandler.Handle(command, ct);
        return Ok(result);
    }

    [HttpPost("vouchers/{pedidoId:guid}/validar")]
    public async Task<IActionResult> ValidarVoucherPago(
        Guid pedidoId,
        [FromBody] ValidarVoucherRequestDto request,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        var command = new ValidarVoucherPagoCommand(
            pedidoId,
            request.Aprobado,
            request.MotivoRechazo);

        var result = await _validarVoucherHandler.Handle(command, ct);
        return Ok(result);
    }

    [HttpGet("turnos/actual")]
    public async Task<IActionResult> ObtenerTurnoActual(
        [FromQuery] Guid cajeroId,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        if (cajeroId == Guid.Empty)
            throw new ValidationException("El parámetro cajeroId es obligatorio.");

        var turno = await _cashShiftPort.ObtenerTurnoActualAsync(cajeroId, ct);
        if (turno == null)
            throw new NotFoundException("TurnoCaja", cajeroId);

        return Ok(turno);
    }

    public record AperturaTurnoRequestDto(
        Guid CajaId,
        Guid CajeroId,
        Guid AlmacenId,
        decimal FondoInicial);

    public record CierreTurnoRequestDto(
        Guid TurnoCajaId,
        IReadOnlyList<ArqueoDenominacionDto> DesgloseFisico,
        decimal TotalDigitalDeclarado,
        string? Justificacion = null);

    public record ValidarVoucherRequestDto(
        bool Aprobado,
        string? MotivoRechazo = null);
}
