using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.SalesPOS.Commands;
using SIGIC.Application.SalesPOS.Handlers;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Application.Security.Ports;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/pos/ventas")]
public class VentasPOSController : ControllerBase
{
    private readonly RegistrarVentaPOSCommandHandler _registrarVentaHandler;
    private readonly ISalesPOSRepository _salesPOSRepository;
    private readonly ICurrentUserContext _userContext;

    public VentasPOSController(
        RegistrarVentaPOSCommandHandler registrarVentaHandler,
        ISalesPOSRepository salesPOSRepository,
        ICurrentUserContext userContext)
    {
        _registrarVentaHandler = registrarVentaHandler ?? throw new ArgumentNullException(nameof(registrarVentaHandler));
        _salesPOSRepository = salesPOSRepository ?? throw new ArgumentNullException(nameof(salesPOSRepository));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    private IActionResult? ValidarAccesoStaffInterno()
    {
        if (_userContext.UserId == Guid.Empty)
            return Unauthorized(new { status = 401, title = "Unauthorized", detail = "Se requiere usuario autenticado para acceder al módulo de ventas POS." });

        if (_userContext.TerminalScope != "LAN-POS" || !_userContext.Roles.Any())
            return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, title = "Forbidden", detail = "Audiencia o rol no autorizado para módulo de ventas POS." });

        return null;
    }

    [HttpPost]
    public async Task<IActionResult> RegistrarVenta(
        [FromBody] RegistrarVentaPOSCommand command,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        if (command.TurnoCajaId == Guid.Empty)
            throw new ValidationException("El TurnoCajaId es obligatorio.");

        if (command.Items == null || !command.Items.Any())
            throw new ValidationException("La venta debe contener al menos un ítem.");

        if (command.Pagos == null || !command.Pagos.Any())
            throw new ValidationException("La venta debe registrar al menos un método de pago.");

        var result = await _registrarVentaHandler.Handle(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerVenta(Guid id, CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        var venta = await _salesPOSRepository.ObtenerPorIdAsync(id, ct);
        if (venta == null)
            throw new NotFoundException("VentaPOS", id);

        return Ok(venta);
    }
}
