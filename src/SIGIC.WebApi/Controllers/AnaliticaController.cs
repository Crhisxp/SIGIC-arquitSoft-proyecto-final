using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.Analytics.Dtos;
using SIGIC.Application.Analytics.Ports;
using SIGIC.Application.Security.Ports;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/analitica")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AnaliticaController(ICopilotoAnaliticoService servicio) : ControllerBase
{
    [HttpPost("consultas")]
    [RequestSizeLimit(8192)]
    public Task<ConsultaAnaliticaResponseDto> Consultar(ConsultaAnaliticaRequestDto request, CancellationToken ct) =>
        servicio.ConsultarAsync(request, ct);

    [HttpGet("sesion")]
    public IActionResult Sesion([FromServices] IAuthorizationService autorizacion, [FromServices] ICurrentUserContext usuario)
    {
        autorizacion.ExigirUnoDeRoles("Superadmin", "Tesoreria", "Almacenero");
        return Ok(new { authenticated = true, username = usuario.Username, roles = usuario.Roles });
    }
}
