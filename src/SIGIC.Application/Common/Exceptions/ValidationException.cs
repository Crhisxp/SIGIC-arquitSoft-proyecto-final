namespace SIGIC.Application.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando los datos de entrada o reglas de negocio de aplicación no son válidas.
/// </summary>
public class ValidationException : Exception
{
    public IReadOnlyList<string> Errores { get; }

    public ValidationException(string mensaje) : base(mensaje)
    {
        Errores = new List<string> { mensaje }.AsReadOnly();
    }

    public ValidationException(IEnumerable<string> errores)
        : base("Se presentaron uno o más errores de validación.")
    {
        Errores = errores.ToList().AsReadOnly();
    }
}
