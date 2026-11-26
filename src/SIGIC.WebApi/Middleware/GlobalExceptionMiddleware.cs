using System.Net;
using System.Text.Json;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Domain.Exceptions;

namespace SIGIC.WebApi.Middleware;

/// <summary>
/// Middleware global para captura determinista de excepciones de dominio y aplicación.
/// Mapea códigos HTTP según especificación y oculta detalles sensibles en errores 500.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        const string precioNoDisponible = "El precio solicitado no está disponible. Revise el precio vigente.";
        var (statusCode, title, detail, errors) = exception switch
        {
            StockInsuficienteException stockEx => (
                HttpStatusCode.Conflict,
                "Conflict",
                stockEx.Message,
                new[] { stockEx.Message }
            ),
            ValidationException valEx => (
                HttpStatusCode.BadRequest,
                "Bad Request",
                valEx.Message,
                valEx.Errores.ToArray()
            ),
            MargenInvalidoException => (
                HttpStatusCode.BadRequest,
                "Bad Request",
                precioNoDisponible,
                new[] { precioNoDisponible }
            ),
            UnauthorizedException unauthEx => (
                HttpStatusCode.Forbidden,
                "Forbidden",
                unauthEx.Message,
                new[] { unauthEx.Message }
            ),
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                "Not Found",
                notFoundEx.Message,
                new[] { notFoundEx.Message }
            ),
            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                "Bad Request",
                argEx.Message,
                new[] { argEx.Message }
            ),
            InvalidOperationException invEx => (
                HttpStatusCode.BadRequest,
                "Bad Request",
                invEx.Message,
                new[] { invEx.Message }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "Se ha producido un error interno en el servidor. Por favor, contacte al administrador.",
                Array.Empty<string>()
            )
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Error no controlado durante la ejecución de la petición.");
        }
        else
        {
            _logger.LogWarning("Excepción de negocio controlada: {Title} ({StatusCode}) - {Detail}", title, (int)statusCode, detail);
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponseDto(
            (int)statusCode,
            title,
            detail,
            errors.Length > 0 ? errors : null);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }

    public record ErrorResponseDto(
        int Status,
        string Title,
        string Detail,
        IReadOnlyList<string>? Errors = null);
}
