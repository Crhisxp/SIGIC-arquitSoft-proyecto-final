using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.ProductDetailsPricing.Services;
using SIGIC.Application.Security.Ports;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/public/catalogo")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CatalogoPublicoController : ControllerBase
{
    private readonly CatalogoWebService _catalogo;
    private readonly CalcularPDPYDescuentoQueryHandler _pdpQueryHandler;
    private readonly ICurrentUserContext _usuario;

    public CatalogoPublicoController(
        CatalogoWebService catalogo,
        CalcularPDPYDescuentoQueryHandler pdpQueryHandler,
        ICurrentUserContext usuario)
    {
        _catalogo = catalogo;
        _pdpQueryHandler = pdpQueryHandler ?? throw new ArgumentNullException(nameof(pdpQueryHandler));
        _usuario = usuario;
    }

    [HttpGet("productos")]
    public async Task<IActionResult> ListarProductos(
        [FromQuery] string? search = null,
        [FromQuery] Guid? almacenId = null,
        CancellationToken ct = default)
    {
        var productos = await _catalogo.ListarAsync(search, almacenId, _usuario.ClienteWebAutenticadoId, ct);
        return Ok(productos);
    }

    [HttpGet("productos/{id:guid}")]
    public async Task<IActionResult> ObtenerDetalleProducto(Guid id, CancellationToken ct = default)
    {
        return Ok(await _catalogo.ObtenerAsync(id, _usuario.ClienteWebAutenticadoId, ct));
    }

    [HttpPost("calcular-precio")]
    public async Task<IActionResult> CalcularPrecio(
        [FromBody] CalcularPrecioRequestDto request,
        CancellationToken ct = default)
    {
        if (request.Cantidad <= 0m)
            throw new ValidationException("La cantidad solicitada debe ser mayor a cero.");

        var query = new CalcularPDPYDescuentoQuery(
            request.ProductoId,
            null,
            request.Cantidad,
            request.UnidadMedida ?? "UND",
            EsCanalWeb: true,
            ClienteId: _usuario.ClienteWebAutenticadoId,
            AlmacenId: request.AlmacenId);

        var result = await _pdpQueryHandler.Handle(query, ct);
        return Ok(result);
    }

    public record CalcularPrecioRequestDto(
        Guid ProductoId,
        decimal Cantidad,
        string? UnidadMedida = null,
        string? RolCliente = null,
        Guid? ClienteId = null,
        Guid? AlmacenId = null);
}
