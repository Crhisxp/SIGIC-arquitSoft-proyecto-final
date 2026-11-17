namespace SIGIC.Application.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando una entidad no es encontrada o no debe revelarse su existencia (HTTP 404).
/// </summary>
public class NotFoundException : Exception
{
    public string EntityName { get; }
    public object? Key { get; }

    public NotFoundException(string message) : base(message)
    {
        EntityName = string.Empty;
    }

    public NotFoundException(string entityName, object key)
        : base($"La entidad '{entityName}' con clave '{key}' no fue encontrada.")
    {
        EntityName = entityName;
        Key = key;
    }
}
