using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Security.Models;
using SIGIC.Application.Security.Ports;
using SIGIC.Application.Security.Services;

namespace SIGIC.Application.Tests.Security;

public class SecurityTests
{
    [Fact]
    public void CurrentUserContext_DebeExponerPropiedadesCorrectamente()
    {
        var userId = Guid.NewGuid();
        var roles = new[] { "Cajero", "Almacenero" };
        var ctx = new CurrentUserContext(userId, "usuario1", roles, "pos_terminal", "192.168.1.15");

        Assert.Equal(userId, ctx.UserId);
        Assert.Equal("usuario1", ctx.Username);
        Assert.Equal("pos_terminal", ctx.TerminalScope);
        Assert.Equal("192.168.1.15", ctx.ClientIp);
        Assert.True(ctx.HasRole("Cajero"));
        Assert.True(ctx.IsInRole("Almacenero"));
        Assert.False(ctx.HasRole("Superadmin"));
    }

    [Fact]
    public void AuthorizationService_DebePermitirSiUsuarioTieneRolRequerido()
    {
        var user = new CurrentUserContext(Guid.NewGuid(), "admin", new[] { "Superadmin" }, "admin_panel", "10.0.0.1");
        var auth = new AuthorizationService(user);

        // No debe lanzar excepción
        auth.ExigirRol("Superadmin");
        auth.ExigirUnoDeRoles("Cajero", "Superadmin");
    }

    [Fact]
    public void AuthorizationService_DebeLanzarUnauthorizedException_SiUsuarioNoTieneRol()
    {
        var user = new CurrentUserContext(Guid.NewGuid(), "cajero", new[] { "Cajero" }, "pos_terminal", "192.168.1.20");
        var auth = new AuthorizationService(user);

        var ex1 = Assert.Throws<UnauthorizedException>(() => auth.ExigirRol("Superadmin"));
        Assert.Equal("Superadmin", ex1.RolRequerido);

        var ex2 = Assert.Throws<UnauthorizedException>(() => auth.ExigirUnoDeRoles("Superadmin", "Tesoreria"));
        Assert.Contains("Superadmin,Tesoreria", ex2.RolRequerido);

        // Validaciones de argumentos
        Assert.Throws<ArgumentException>(() => auth.ExigirRol(""));
        Assert.Throws<ArgumentException>(() => auth.ExigirUnoDeRoles());
        Assert.Throws<ArgumentNullException>(() => new AuthorizationService(null!));
        Assert.False(user.HasRole(""));
    }

    [Fact]
    public void AuditLogEntry_DebeSerInmutable_Y_ContenerTodosLosCamposRequeridos()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var entry = new AuditLogEntry(id, "TEST_ACTION", "TestEntity", "123", "Detalles prueba", "127.0.0.1", "test_terminal", now);

        Assert.Equal(id, entry.UserId);
        Assert.Equal("TEST_ACTION", entry.Accion);
        Assert.Equal("TestEntity", entry.Entidad);
        Assert.Equal("123", entry.EntidadId);
        Assert.Equal("Detalles prueba", entry.Detalles);
        Assert.Equal("127.0.0.1", entry.IpAddress);
        Assert.Equal("test_terminal", entry.TerminalScope);
        Assert.Equal(now, entry.TimestampUtc);

        var defaultEntry = new AuditLogEntry();
        Assert.NotEqual(Guid.Empty, defaultEntry.Id);
    }
}
