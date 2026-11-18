using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Handlers;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Application.Security.Ports;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/web/pedidos")]
public class PedidosWebController : ControllerBase
{
    private static readonly HashSet<string> FormatosPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private readonly ReservarPedidoWebCommandHandler _reservarHandler;
    private readonly IWebOrderRepository _webOrderRepository;
    private readonly IVoucherStoragePort _voucherStoragePort;
    private readonly ICurrentUserContext _userContext;

    public PedidosWebController(
        ReservarPedidoWebCommandHandler reservarHandler,
        IWebOrderRepository webOrderRepository,
        IVoucherStoragePort voucherStoragePort,
        ICurrentUserContext userContext)
    {
        _reservarHandler = reservarHandler ?? throw new ArgumentNullException(nameof(reservarHandler));
        _webOrderRepository = webOrderRepository ?? throw new ArgumentNullException(nameof(webOrderRepository));
        _voucherStoragePort = voucherStoragePort ?? throw new ArgumentNullException(nameof(voucherStoragePort));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    private IActionResult? ValidarAccesoClienteWeb()
    {
        if (_userContext.UserId == Guid.Empty)
            return Unauthorized(new { status = 401, title = "Unauthorized", detail = "Se requiere usuario autenticado para acceder al canal web." });

        if (_userContext.TerminalScope != "WAN-WEB")
            return StatusCode(StatusCodes.Status403Forbidden, new { status = 403, title = "Forbidden", detail = "Audiencia no autorizada para operaciones de cliente web." });

        return null;
    }

    [HttpPost("reservar")]
    public async Task<IActionResult> ReservarPedido(
        [FromBody] ReservarPedidoWebRequestDto request,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoClienteWeb();
        if (authCheck != null) return authCheck;

        if (request.Items == null || !request.Items.Any())
            throw new ValidationException("El pedido debe contener al menos un ítem.");

        var command = new ReservarPedidoWebCommand(
            request.ClienteId,
            request.MetodoPago,
            request.ModalidadEntrega,
            request.TiendaDestinoId,
            request.Items);

        var result = await _reservarHandler.Handle(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/voucher")]
    public async Task<IActionResult> SubirVoucher(
        Guid id,
        IFormFile? voucher,
        CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoClienteWeb();
        if (authCheck != null) return authCheck;

        if (voucher == null || voucher.Length == 0)
            throw new ValidationException("Debe adjuntar un archivo de comprobante válido.");

        if (voucher.Length > 5 * 1024 * 1024)
            throw new ValidationException("El archivo del comprobante no debe superar los 5 MB.");

        string ext = Path.GetExtension(voucher.FileName).ToLowerInvariant();
        if (!FormatosPermitidos.Contains(ext))
            throw new ValidationException($"Formato '{ext}' no permitido. Solo se aceptan archivos JPG, PNG y PDF.");

        var pedido = await _webOrderRepository.ObtenerPorIdAsync(id, ct);
        if (pedido == null)
            throw new NotFoundException("PedidoWeb", id);

        if (!pedido.Estado.Equals("PendientePago", StringComparison.OrdinalIgnoreCase) &&
            !pedido.Estado.Equals("PendienteValidacion", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"El pedido en estado '{pedido.Estado}' no admite subida de comprobante.");
        }

        DateTime now = DateTime.UtcNow;
        if (pedido.FechaLimiteVoucherUtc.HasValue && now > pedido.FechaLimiteVoucherUtc.Value)
        {
            throw new ValidationException("La ventana máxima de 60 minutos para adjuntar el comprobante ha expirado.");
        }

        await using var stream = voucher.OpenReadStream();
        var metadata = await _voucherStoragePort.GuardarVoucherAsync(
            pedido.Id,
            stream,
            voucher.FileName,
            voucher.ContentType,
            ct);

        await _webOrderRepository.ActualizarEstadoAsync(pedido.Id, "PendienteValidacion", ct);

        return Ok(new VoucherUploadResultDto(
            pedido.Id,
            metadata.NombreArchivo,
            metadata.ContentType,
            metadata.TamanoBytes,
            "Comprobante recibido y en espera de validación administrativa."));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerEstado(Guid id, CancellationToken ct = default)
    {
        var authCheck = ValidarAccesoClienteWeb();
        if (authCheck != null) return authCheck;

        var pedido = await _webOrderRepository.ObtenerPorIdAsync(id, ct);
        if (pedido == null)
            throw new NotFoundException("PedidoWeb", id);

        return Ok(pedido);
    }

    public record ReservarPedidoWebRequestDto(
        Guid ClienteId,
        string MetodoPago,
        string ModalidadEntrega,
        Guid? TiendaDestinoId,
        IReadOnlyList<PedidoWebItemDto> Items);

    public record VoucherUploadResultDto(
        Guid PedidoId,
        string NombreArchivo,
        string ContentType,
        long TamanoBytes,
        string Mensaje);
}
