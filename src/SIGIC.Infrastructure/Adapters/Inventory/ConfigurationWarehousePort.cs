using Microsoft.Extensions.Configuration;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.Inventory;

/// <summary>
/// Proveedor de configuración e identificadores de almacenes físicos (RF-INV-002, RF-INV-003).
/// Invariante: Centralización de reservas web sobre el Almacén Central.
/// </summary>
public class ConfigurationWarehousePort : IWarehouseConfigurationPort
{
    private readonly Guid _almacenCentralId;
    private readonly SigicDbContext _dbContext;

    public ConfigurationWarehousePort(
        SigicDbContext dbContext,
        IConfiguration? configuration = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

        string? configCentral = configuration?["WarehouseSettings:CentralWarehouseId"];
        if (!string.IsNullOrWhiteSpace(configCentral) && Guid.TryParse(configCentral, out Guid parsedId))
        {
            _almacenCentralId = parsedId;
        }
        else
        {
            // ID canónico por defecto para Almacén Central de Ayacucho
            _almacenCentralId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        }
    }

    public Guid ObtenerAlmacenCentralId()
    {
        // AlmacenCentral / RF-INV-003
        return _almacenCentralId;
    }

    public bool EsAlmacenValido(Guid almacenId)
    {
        if (almacenId == Guid.Empty)
            return false;

        if (almacenId == _almacenCentralId)
            return true;

        return _dbContext.Almacenes.Any(a => a.Id == almacenId);
    }
}
