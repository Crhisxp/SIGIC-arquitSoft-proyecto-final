using System.IO;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Domain.Exceptions;
using SIGIC.WebApi.Middleware;
using Xunit;

namespace SIGIC.WebApi.Tests;

public class ExceptionMiddlewareTests
{
    private static async Task<(int StatusCode, JsonDocument Json)> ExecuteMiddlewareWithException(Exception thrownException)
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw thrownException,
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        await middleware.InvokeAsync(context);

        responseBodyStream.Seek(0, SeekOrigin.Begin);
        var jsonDoc = await JsonDocument.ParseAsync(responseBodyStream);
        return (context.Response.StatusCode, jsonDoc);
    }

    [Fact]
    public async Task InvokeAsync_StockInsuficienteException_Returns409Conflict()
    {
        var ex = new StockInsuficienteException("Stock disponible insuficiente para el producto.");
        var (status, json) = await ExecuteMiddlewareWithException(ex);

        Assert.Equal((int)HttpStatusCode.Conflict, status);
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Conflict", json.RootElement.GetProperty("title").GetString());
        Assert.Contains("Stock disponible insuficiente", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400BadRequestWithErrors()
    {
        var ex = new ValidationException(new[] { "Error de validación 1", "Error de validación 2" });
        var (status, json) = await ExecuteMiddlewareWithException(ex);

        Assert.Equal((int)HttpStatusCode.BadRequest, status);
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Bad Request", json.RootElement.GetProperty("title").GetString());
        var errors = json.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Error de validación 1", errors);
        Assert.Contains("Error de validación 2", errors);
    }

    [Fact]
    public async Task InvokeAsync_MargenInvalidoException_Returns400BadRequest()
    {
        var ex = new MargenInvalidoException(10m, 50m);
        var (status, json) = await ExecuteMiddlewareWithException(ex);

        Assert.Equal((int)HttpStatusCode.BadRequest, status);
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Bad Request", json.RootElement.GetProperty("title").GetString());
        const string expected = "El precio solicitado no está disponible. Revise el precio vigente.";
        Assert.Equal(expected, json.RootElement.GetProperty("detail").GetString());
        Assert.Equal(expected, Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedException_Returns403Forbidden()
    {
        var ex = new UnauthorizedException("Tesoreria", "Acceso denegado a módulo de tesorería.");
        var (status, json) = await ExecuteMiddlewareWithException(ex);

        Assert.Equal((int)HttpStatusCode.Forbidden, status);
        Assert.Equal(403, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Forbidden", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404NotFound()
    {
        var ex = new NotFoundException("Producto", Guid.NewGuid());
        var (status, json) = await ExecuteMiddlewareWithException(ex);

        Assert.Equal((int)HttpStatusCode.NotFound, status);
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400BadRequest()
    {
        var ex = new ArgumentException("Parámetro inválido.");
        var (status, json) = await ExecuteMiddlewareWithException(ex);

        Assert.Equal((int)HttpStatusCode.BadRequest, status);
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Bad Request", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithoutLeakingSensitiveDetails()
    {
        var ex = new Exception("PostgresException: connection to server on port 5432 failed; password=supersecretpassword123; at Npgsql.NpgsqlConnector.Open()");
        var (status, json) = await ExecuteMiddlewareWithException(ex);

        Assert.Equal((int)HttpStatusCode.InternalServerError, status);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Internal Server Error", json.RootElement.GetProperty("title").GetString());
        string detail = json.RootElement.GetProperty("detail").GetString()!;

        // Invariante de seguridad: jamás revelar stack traces, contraseñas o detalles internos en 500
        Assert.DoesNotContain("supersecretpassword123", detail);
        Assert.DoesNotContain("PostgresException", detail);
        Assert.DoesNotContain("NpgsqlConnector", detail);
        Assert.Equal("Se ha producido un error interno en el servidor. Por favor, contacte al administrador.", detail);
    }
}
