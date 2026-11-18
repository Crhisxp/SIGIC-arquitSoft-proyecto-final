using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Inventory.Commands;
using SIGIC.Application.Inventory.Handlers;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.Security.Ports;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/inventario")]
public class InventarioController : ControllerBase
{
    private readonly AjustarInventarioCommandHandler _ajustarHandler;
    private readonly IKardexPort _kardexPort;
    private readonly ITransferGuidePort _transferGuidePort;
    private readonly IInventoryStockPort _inventoryStockPort;
    private readonly ICurrentUserContext _userContext;

    public InventarioController(
        AjustarInventarioCommandHandler ajustarHandler,
        IKardexPort kardexPort,
        ITransferGuidePort transferGuidePort,
        IInventoryStockPort inventoryStockPort,
        ICurrentUserContext userContext)
    {
        _ajustarHandler = ajustarHandler ?? throw new ArgumentNullException(nameof(ajustarHandler));
        _kardexPort = kardexPort ?? throw new ArgumentNullException(nameof(kardexPort));
        _transferGuidePort = transferGuidePort ?? throw new ArgumentNullException(nameof(transferGuidePort));
        _inventoryStockPort = inventoryStockPort ?? throw new ArgumentNullException(nameof(inventoryStockPort));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    private IActionResult? ValidarAccesoStaffInterno()
    {
        if (_userContext.UserId == Guid.Empty)
            return Unauthorized(new { status = 401, title = "Unauthorized", detail = "Se requiere usuario autenticado para acceder al módulo de inventario." });

        if (_userContext.TerminalScope != "LAN-POS" || !_userContext.Roles.Any())
            return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, title = "Forbidden", detail = "Audiencia o rol no autorizado para módulo de inventario." });

        return null;
    }

    [HttpPost("ajustes")]
    public async Task<IActionResult> RegistrarAjuste(
        [FromBody] AjustarInventarioCommand command,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        if (command.ProductoId == Guid.Empty || command.AlmacenId == Guid.Empty)
            throw new ValidationException("ProductoId y AlmacenId son obligatorios.");

        if (string.IsNullOrWhiteSpace(command.Motivo))
            throw new ValidationException("El motivo del ajuste es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.CodigoAutorizacionSuperadmin))
            throw new ValidationException("El código de autorización del Superadmin es requerido.");

        var result = await _ajustarHandler.Handle(command, ct);
        return Ok(result);
    }

    [HttpGet("kardex/{productoId:guid}")]
    public async Task<IActionResult> ObtenerKardex(
        Guid productoId,
        [FromQuery] Guid? almacenId = null,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        var movimientos = await _kardexPort.ObtenerMovimientosAsync(productoId, almacenId, ct);
        return Ok(movimientos);
    }

    [HttpGet("guias-traslado/{idOrCode}")]
    public async Task<IActionResult> ObtenerGuiaTraslado(
        string idOrCode,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        var guia = await _transferGuidePort.ObtenerGuiaPorIdOCodigoAsync(idOrCode, ct);
        if (guia == null)
            throw new NotFoundException("GuiaTrasladoInterno", idOrCode);

        return Ok(guia);
    }

    [HttpPost("guias-traslado/{idOrCode}/entregar")]
    public async Task<IActionResult> EntregarGuiaTraslado(
        string idOrCode,
        [FromBody] EntregarGuiaRequestDto request,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        if (request == null)
            throw new ValidationException("El cuerpo de la solicitud no puede ser nulo.");

        var resultado = await _transferGuidePort.EntregarGuiaAsync(
            idOrCode,
            request.DniReceptor,
            request.CodigoRetiro,
            _userContext.UserId,
            ct);

        return Ok(resultado);
    }

    public record EntregarGuiaRequestDto(string DniReceptor, string? CodigoRetiro);

    [HttpGet("stock")]
    public async Task<IActionResult> ObtenerStock(
        [FromQuery] Guid? almacenId = null,
        [FromQuery] Guid? productoId = null,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoStaffInterno();
        if (authCheck != null) return authCheck;

        // RNF-CON-003: Retorna stock físico, comprometido y disponible = (físico - comprometido)
        var stockList = await _inventoryStockPort.ObtenerListadoStockAsync(almacenId, productoId, ct);
        return Ok(stockList);
    }
}
