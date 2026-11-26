using Microsoft.AspNetCore.Mvc;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Security.Ports;

namespace SIGIC.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private static readonly HashSet<string> RolesStaffPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cajero", "VendedorPOS", "Superadmin", "Tesoreria", "Almacenero", "SupervisorCaja"
    };

    private readonly IAccountLockoutRepository _accountLockoutRepo;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICustomerProfilePort _perfiles;

    public AuthController(
        IAccountLockoutRepository accountLockoutRepo,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ICustomerProfilePort perfiles)
    {
        _accountLockoutRepo = accountLockoutRepo ?? throw new ArgumentNullException(nameof(accountLockoutRepo));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _perfiles = perfiles;
    }

    [HttpPost("login-staff")]
    public async Task<IActionResult> LoginStaff([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("El usuario y la contraseña son requeridos.");

        var user = await _accountLockoutRepo.ObtenerEstadoCuentaAsync(request.Username, ct);
        if (user == null || !user.EstaActivo)
            throw new ValidationException("Credenciales inválidas.");

        DateTime now = DateTime.UtcNow;
        if (user.BloqueadoHastaUtc.HasValue && user.BloqueadoHastaUtc.Value > now)
        {
            var restante = user.BloqueadoHastaUtc.Value - now;
            throw new UnauthorizedException($"La cuenta está temporalmente bloqueada por exceso de intentos fallidos. Intente nuevamente en {Math.Ceiling(restante.TotalMinutes)} minutos.");
        }

        bool passwordValido = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!passwordValido)
        {
            await _accountLockoutRepo.RegistrarIntentoFallidoAsync(request.Username, now, ct);
            throw new ValidationException("Credenciales inválidas.");
        }

        await _accountLockoutRepo.RestablecerIntentosFallidosAsync(request.Username, ct);

        if (!user.Roles.Any(r => RolesStaffPermitidos.Contains(r)))
            throw new UnauthorizedException("El usuario no cuenta con un rol de staff interno autorizado.");

        var tokenResult = _jwtTokenService.GenerateToken(
            user.UserId, user.Username, user.Roles, "SIGIC_InternalStaff");

        return Ok(new AuthResponseDto(
            tokenResult.Token, "Bearer", 15, user.UserId, user.Username, user.Roles));
    }

    [HttpPost("login-web")]
    public async Task<IActionResult> LoginWeb([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("El usuario y la contraseña son requeridos.");

        var user = await _accountLockoutRepo.ObtenerEstadoCuentaAsync(request.Username, ct);
        if (user == null || !user.EstaActivo)
            throw new ValidationException("Credenciales inválidas.");

        DateTime now = DateTime.UtcNow;
        if (user.BloqueadoHastaUtc.HasValue && user.BloqueadoHastaUtc.Value > now)
        {
            var restante = user.BloqueadoHastaUtc.Value - now;
            throw new UnauthorizedException($"La cuenta está temporalmente bloqueada por exceso de intentos fallidos. Intente nuevamente en {Math.Ceiling(restante.TotalMinutes)} minutos.");
        }

        bool passwordValido = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!passwordValido)
        {
            await _accountLockoutRepo.RegistrarIntentoFallidoAsync(request.Username, now, ct);
            throw new ValidationException("Credenciales inválidas.");
        }

        await _accountLockoutRepo.RestablecerIntentosFallidosAsync(request.Username, ct);

        if (!user.Roles.Any(r => r.Equals("ClienteWeb", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedException("La cuenta no tiene asignado el rol 'ClienteWeb'.");

        var tokenResult = _jwtTokenService.GenerateToken(
            user.UserId, user.Username, user.Roles, "SIGIC_WebClient");

        var perfil = await _perfiles.ObtenerPerfilClienteAsync(user.UserId, ct);
        return Ok(new AuthResponseDto(
            tokenResult.Token, "Bearer", 15, user.UserId, user.Username, user.Roles, perfil));
    }

    [HttpGet("sesion-web")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SesionWeb([FromServices] ICurrentUserContext usuario, CancellationToken ct)
    {
        if (usuario.ClienteWebAutenticadoId is not { } clienteId)
            return Unauthorized();

        var perfil = await _perfiles.ObtenerPerfilClienteAsync(clienteId, ct);
        return Ok(new PerfilWebResponseDto(clienteId, usuario.Username, usuario.Roles, perfil));
    }

    [HttpPost("register-web")]
    public async Task<IActionResult> RegisterWeb([FromBody] RegisterWebRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ValidationException("El nombre de usuario es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ValidationException("La contraseña debe contener al menos 8 caracteres.");

        var existente = await _accountLockoutRepo.ObtenerEstadoCuentaAsync(request.Username, ct);
        if (existente != null)
            throw new ValidationException($"El nombre de usuario '{request.Username}' ya se encuentra registrado.");

        Guid newUserId = Guid.NewGuid();
        string passwordHash = _passwordHasher.HashPassword(request.Password);
        var roles = new[] { "ClienteWeb" };

        await _accountLockoutRepo.RegistrarUsuarioAsync(
            newUserId, request.Username, passwordHash, roles, "PublicoGeneral", ct);

        var tokenResult = _jwtTokenService.GenerateToken(
            newUserId, request.Username, roles, "SIGIC_WebClient");

        return StatusCode(StatusCodes.Status201Created, new AuthResponseDto(
            tokenResult.Token, "Bearer", 15, newUserId, request.Username, roles, "PublicoGeneral"));
    }

    public record LoginRequestDto(string Username, string Password);
    public record RegisterWebRequestDto(string Username, string Password, string? Email = null, string? Nombres = null);
    public record PerfilWebResponseDto(Guid UserId, string Username, IReadOnlyList<string> Roles, string RolComercial);
    public record AuthResponseDto(
        string Token,
        string TokenType,
        int ExpiresInMinutes,
        Guid UserId,
        string Username,
        IReadOnlyList<string> Roles,
        string? RolComercial = null);
}
