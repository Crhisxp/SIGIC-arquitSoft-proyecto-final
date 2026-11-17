using SIGIC.Application.Common.Exceptions;

namespace SIGIC.Application.Tests.Common;

public class CommonExceptionsTests
{
    [Fact]
    public void NotFoundException_DebeCapturarEntityNameYKey()
    {
        var ex1 = new NotFoundException("Mensaje simple");
        Assert.Equal("Mensaje simple", ex1.Message);
        Assert.Empty(ex1.EntityName);
        Assert.Null(ex1.Key);

        var key = Guid.NewGuid();
        var ex2 = new NotFoundException("Producto", key);
        Assert.Equal("Producto", ex2.EntityName);
        Assert.Equal(key, ex2.Key);
        Assert.Contains("Producto", ex2.Message);
        Assert.Contains(key.ToString(), ex2.Message);
    }

    [Fact]
    public void UnauthorizedException_DebeCapturarRolRequerido()
    {
        var ex1 = new UnauthorizedException("No autorizado");
        Assert.Equal("No autorizado", ex1.Message);
        Assert.Empty(ex1.RolRequerido);

        var ex2 = new UnauthorizedException("Superadmin", "Permiso denegado");
        Assert.Equal("Superadmin", ex2.RolRequerido);
        Assert.Equal("Permiso denegado", ex2.Message);
    }

    [Fact]
    public void ValidationException_DebeCapturarErroresMultiples()
    {
        var ex1 = new ValidationException("Error simple");
        Assert.Single(ex1.Errores);
        Assert.Equal("Error simple", ex1.Errores[0]);

        var lista = new List<string> { "Error 1", "Error 2", "Error 3" };
        var ex2 = new ValidationException(lista);
        Assert.Equal(3, ex2.Errores.Count);
        Assert.Contains("Error 2", ex2.Errores);
    }
}
