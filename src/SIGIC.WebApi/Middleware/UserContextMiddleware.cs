using System.Net;
using System.Security.Claims;
using System.Text.Json;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Security.Services;
using SIGIC.Infrastructure.Security;

namespace SIGIC.WebApi.Middleware;

/// <summary>
/// Middleware para extracción de credenciales JWT RS256, segregación estricta de audiencias
/// ('SIGIC_InternalStaff' vs 'SIGIC_WebClient') y protección de rutas operativas internas (RNF-SEG-002, RNF-SEG-005).
/// </summary>
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context, IJwtTokenService jwtTokenService)
    {
        string path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        bool isInternalStaffRoute = path.StartsWith("/api/v1/pos") ||
                                    path.StartsWith("/api/v1/caja") ||
                                    path.StartsWith("/api/v1/inventario");

        bool isWebRoute = path.StartsWith("/api/v1/web");

        string authHeader = context.Request.Headers.Authorization.FirstOrDefault() ?? string.Empty;

        // 1. Protección estricta de rutas internas (POS, Caja, Inventario): Exige SIGIC_InternalStaff
        if (isInternalStaffRoute)
        {
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                    "Acceso no autorizado. Se requiere un token Bearer válido para rutas operativas internas.",
                    "Token de autenticación no proporcionado.");
                return;
            }

            string token = authHeader["Bearer ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                    "Token de autenticación vacío.", "Token Bearer no contiene credenciales.");
                return;
            }

            var staffValidation = jwtTokenService.ValidateToken(token, "SIGIC_InternalStaff");
            if (staffValidation.IsValid)
            {
                var userContext = CreateUserContext(context, staffValidation, "LAN-POS", clientIp);
                await ExecuteWithContextAsync(context, userContext);
                return;
            }

            // Verificar si el token pertenece a cliente web (audiencia cruzada prohibida)
            var webValidation = jwtTokenService.ValidateToken(token, "SIGIC_WebClient");
            if (webValidation.IsValid)
            {
                await WriteErrorResponseAsync(context, HttpStatusCode.Forbidden, "Forbidden",
                    "Acceso denegado. Se requiere un token de personal interno con audiencia 'SIGIC_InternalStaff'.",
                    "Audiencia cruzada no autorizada para rutas operativas internas.");
                return;
            }

            await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                "Token de autenticación inválido o expirado.",
                staffValidation.FailureReason ?? "Firma o token inválido.");
            return;
        }

        // 2. Protección de rutas web exclusivas de cliente: Exige SIGIC_WebClient
        if (isWebRoute)
        {
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                    "Acceso no autorizado. Se requiere un token Bearer de cliente web.",
                    "Token de autenticación no proporcionado.");
                return;
            }

            string token = authHeader["Bearer ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                    "Token de autenticación vacío.", "Token Bearer no contiene credenciales.");
                return;
            }

            var webValidation = jwtTokenService.ValidateToken(token, "SIGIC_WebClient");
            if (webValidation.IsValid)
            {
                var userContext = CreateUserContext(context, webValidation, "WAN-WEB", clientIp);
                await ExecuteWithContextAsync(context, userContext);
                return;
            }

            // Verificar si el token pertenece a personal interno (audiencia cruzada en checkout web)
            var staffValidation = jwtTokenService.ValidateToken(token, "SIGIC_InternalStaff");
            if (staffValidation.IsValid)
            {
                await WriteErrorResponseAsync(context, HttpStatusCode.Forbidden, "Forbidden",
                    "Acceso denegado. Se requiere un token de cliente web con audiencia 'SIGIC_WebClient'.",
                    "Audiencia cruzada no autorizada para rutas de cliente web.");
                return;
            }

            await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                "Token de autenticación inválido o expirado.",
                webValidation.FailureReason ?? "Firma o token inválido.");
            return;
        }

        // 3. Rutas públicas y de autenticación (/api/v1/public/*, /api/v1/auth/*)
        ICurrentUserContext publicUserContext;
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            string token = authHeader["Bearer ".Length..].Trim();
            var val = jwtTokenService.ValidateToken(token, "SIGIC_WebClient");
            if (!val.IsValid)
            {
                val = jwtTokenService.ValidateToken(token, "SIGIC_InternalStaff");
            }

            if (val.IsValid)
            {
                string scope = val.Audience == "SIGIC_InternalStaff" ? "LAN-POS" : "WAN-WEB";
                publicUserContext = CreateUserContext(context, val, scope, clientIp);
            }
            else
            {
                publicUserContext = new CurrentUserContext(Guid.Empty, "anonymous", Array.Empty<string>(), "WAN-WEB", clientIp);
            }
        }
        else
        {
            publicUserContext = new CurrentUserContext(Guid.Empty, "anonymous", Array.Empty<string>(), "WAN-WEB", clientIp);
        }

        await ExecuteWithContextAsync(context, publicUserContext);
    }

    private static ICurrentUserContext CreateUserContext(
        HttpContext context,
        JwtValidationResult validation,
        string terminalScope,
        string clientIp)
    {
        Guid userId = validation.UserId ?? Guid.Empty;
        string username = validation.Username ?? "authenticated_user";
        var roles = validation.Roles;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username)
        };
        foreach (var r in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }

        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);
        return new CurrentUserContext(userId, username, roles, terminalScope, clientIp);
    }

    private async Task ExecuteWithContextAsync(HttpContext context, ICurrentUserContext userContext)
    {
        CurrentUserContextAccessor.Current = userContext;
        try
        {
            await _next(context);
        }
        finally
        {
            CurrentUserContextAccessor.Current = null;
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string title,
        string detail,
        string? error = null)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = (int)statusCode;

        var errors = string.IsNullOrWhiteSpace(error) ? null : new[] { error };
        var response = new
        {
            status = (int)statusCode,
            title = title,
            detail = detail,
            errors = errors
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }
}
