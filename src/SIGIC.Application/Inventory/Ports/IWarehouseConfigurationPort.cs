namespace SIGIC.Application.Inventory.Ports;

/// <summary>
/// Provee identificadores y configuración de almacenes físicos del sistema (RF-INV-002, RF-INV-003).
/// </summary>
public interface IWarehouseConfigurationPort
{
    Guid ObtenerAlmacenCentralId();
    bool EsAlmacenValido(Guid almacenId);
}
