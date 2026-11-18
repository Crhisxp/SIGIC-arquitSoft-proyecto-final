using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Application.ProductDetailsPricing.Services;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/public/catalogo")]
public class CatalogoPublicoController : ControllerBase
{
    private readonly IProductQueryPort _productQueryPort;
    private readonly CalcularPDPYDescuentoQueryHandler _pdpQueryHandler;

    public CatalogoPublicoController(
        IProductQueryPort productQueryPort,
        CalcularPDPYDescuentoQueryHandler pdpQueryHandler)
    {
        _productQueryPort = productQueryPort ?? throw new ArgumentNullException(nameof(productQueryPort));
        _pdpQueryHandler = pdpQueryHandler ?? throw new ArgumentNullException(nameof(pdpQueryHandler));
    }

    [HttpGet("productos")]
    public async Task<IActionResult> ListarProductos(
        [FromQuery] string? search = null,
        [FromQuery] Guid? almacenId = null,
        CancellationToken ct = default)
    {
        var productos = await _productQueryPort.ListarCatalogoWebAsync(search, almacenId, ct);
        return Ok(productos);
    }

    [HttpGet("productos/{id:guid}")]
    public async Task<IActionResult> ObtenerDetalleProducto(Guid id, CancellationToken ct = default)
    {
        var producto = await _productQueryPort.ObtenerParaPDPAsync(id, ct);

        // Invariante 2 (RF-INV-001): Exclusión web absoluta (HTTP 404 si disponible_en_web == false)
        if (producto == null || !producto.DisponibleEnWeb)
        {
            throw new NotFoundException("Producto", id);
        }

        decimal stockDisponible = Math.Max(0m, producto.StockFisico - producto.StockComprometido);

        var dto = new ProductoCatalogoDto(
            producto.Id,
            producto.CodigoSku,
            producto.NombreComercial,
            producto.UnidadBase,
            producto.PrecioVenta,
            stockDisponible,
            producto.DisponibleEnWeb);

        return Ok(dto);
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
            request.RolCliente,
            request.Cantidad,
            request.UnidadMedida ?? "UND",
            EsCanalWeb: true,
            ClienteId: request.ClienteId,
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
