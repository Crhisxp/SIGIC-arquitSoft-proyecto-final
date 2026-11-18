using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIGIC.Application.Security.Models;
using SIGIC.Infrastructure.Persistence.Entities;

namespace SIGIC.Infrastructure.Persistence;

public class SigicDbContext : DbContext
{
    public SigicDbContext(DbContextOptions<SigicDbContext> options) : base(options)
    {
    }

    public DbSet<ProductoEntity> Productos => Set<ProductoEntity>();
    public DbSet<StockAlmacenEntity> StockAlmacenes => Set<StockAlmacenEntity>();
    public DbSet<AlmacenEntity> Almacenes => Set<AlmacenEntity>();
    public DbSet<TurnoCajaEntity> TurnosCaja => Set<TurnoCajaEntity>();
    public DbSet<CajaArqueoEntity> ArqueosCaja => Set<CajaArqueoEntity>();
    public DbSet<ArqueoDenominacionEntity> ArqueoDenominaciones => Set<ArqueoDenominacionEntity>();
    public DbSet<VentaPOSEntity> VentasPOS => Set<VentaPOSEntity>();
    public DbSet<VentaPOSItemEntity> VentasPOSItems => Set<VentaPOSItemEntity>();
    public DbSet<VentaPOSPagoEntity> VentasPOSPagos => Set<VentaPOSPagoEntity>();
    public DbSet<PagoDigitalEntity> PagosDigitales => Set<PagoDigitalEntity>();
    public DbSet<PedidoWebEntity> PedidosWeb => Set<PedidoWebEntity>();
    public DbSet<PedidoWebItemEntity> PedidosWebItems => Set<PedidoWebItemEntity>();
    public DbSet<GuiaTrasladoInternoEntity> GuiasTraslado => Set<GuiaTrasladoInternoEntity>();
    public DbSet<GuiaTrasladoItemEntity> GuiasTrasladoItems => Set<GuiaTrasladoItemEntity>();
    public DbSet<ComprobanteVoucherEntity> ComprobantesVouchers => Set<ComprobanteVoucherEntity>();
    public DbSet<KardexMovimientoEntity> KardexMovimientos => Set<KardexMovimientoEntity>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<AlertaCajaOutboxEntity> AlertasCajaOutbox => Set<AlertaCajaOutboxEntity>();
    public DbSet<TarifaRolEntity> TarifasRol => Set<TarifaRolEntity>();
    public DbSet<DescuentoVolumenEntity> DescuentosVolumen => Set<DescuentoVolumenEntity>();
    public DbSet<ConversionUnidadEntity> ConversionesUnidad => Set<ConversionUnidadEntity>();
    public DbSet<UsuarioEntity> Usuarios => Set<UsuarioEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SigicDbContext).Assembly);

        // Mapeo unificado estricto PascalCase -> snake_case para PostgreSQL 16
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var storeObject = StoreObjectIdentifier.Create(entity, StoreObjectType.Table);
            if (!storeObject.HasValue)
                continue;

            foreach (var property in entity.GetProperties())
            {
                var currentColumn = property.GetColumnName(storeObject.Value);
                if (string.IsNullOrEmpty(currentColumn) || currentColumn == property.Name)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var s = System.Text.RegularExpressions.Regex.Replace(input, @"([A-Z]+)([A-Z][a-z])", "$1_$2");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"([a-z0-9])([A-Z])", "$1_$2");
        return s.ToLowerInvariant();
    }
}
