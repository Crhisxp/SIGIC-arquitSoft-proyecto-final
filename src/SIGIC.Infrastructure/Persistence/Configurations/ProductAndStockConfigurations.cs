using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIGIC.Infrastructure.Persistence.Entities;

namespace SIGIC.Infrastructure.Persistence.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<ProductoEntity>
{
    public void Configure(EntityTypeBuilder<ProductoEntity> builder)
    {
        builder.ToTable("productos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CodigoSku).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Ean13).HasMaxLength(50).IsRequired();
        builder.Property(p => p.NombreComercial).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ImagenUrl);
        builder.Property(p => p.UnidadBase).HasMaxLength(20).IsRequired();
        builder.Property(p => p.CostoUnitario).HasPrecision(18, 4).IsRequired();
        builder.Property(p => p.PrecioVenta).HasPrecision(18, 4).IsRequired();
        builder.Property(p => p.DisponibleEnWeb).IsRequired();
        builder.Property(p => p.StockFisico).HasPrecision(18, 4).IsRequired();
        builder.Property(p => p.StockComprometido).HasPrecision(18, 4).IsRequired();
        builder.Property(p => p.FamiliaId).IsRequired();
        builder.Property(p => p.SubfamiliaId).IsRequired();
        builder.HasIndex(p => p.CodigoSku).IsUnique();
        builder.HasIndex(p => p.DisponibleEnWeb);
    }
}

public class StockAlmacenConfiguration : IEntityTypeConfiguration<StockAlmacenEntity>
{
    public void Configure(EntityTypeBuilder<StockAlmacenEntity> builder)
    {
        builder.ToTable("stock_almacen");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.AlmacenId).IsRequired();
        builder.Property(s => s.ProductoId).IsRequired();
        builder.Property(s => s.StockFisico).HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.StockComprometido).HasPrecision(18, 4).IsRequired();
        builder.HasIndex(s => new { s.AlmacenId, s.ProductoId }).IsUnique();
    }
}

public class AlmacenConfiguration : IEntityTypeConfiguration<AlmacenEntity>
{
    public void Configure(EntityTypeBuilder<AlmacenEntity> builder)
    {
        builder.ToTable("almacenes");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Direccion).HasMaxLength(250).IsRequired();
        builder.HasIndex(a => a.Codigo).IsUnique();
    }
}

public class PricingConfigurations :
    IEntityTypeConfiguration<TarifaRolEntity>,
    IEntityTypeConfiguration<DescuentoVolumenEntity>,
    IEntityTypeConfiguration<ConversionUnidadEntity>
{
    public void Configure(EntityTypeBuilder<TarifaRolEntity> builder)
    {
        builder.ToTable("tarifas_rol");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Rol).HasMaxLength(50).IsRequired();
        builder.Property(t => t.PorcentajeDescuento).HasPrecision(5, 2).IsRequired();
        builder.HasIndex(t => t.Rol).IsUnique();
    }

    public void Configure(EntityTypeBuilder<DescuentoVolumenEntity> builder)
    {
        builder.ToTable("descuentos_volumen");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.CantidadMinima).HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.PorcentajeDescuento).HasPrecision(5, 2).IsRequired();
    }

    public void Configure(EntityTypeBuilder<ConversionUnidadEntity> builder)
    {
        builder.ToTable("conversiones_unidad");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.UnidadOrigen).HasMaxLength(20).IsRequired();
        builder.Property(c => c.UnidadDestino).HasMaxLength(20).IsRequired();
        builder.Property(c => c.FactorConversion).HasPrecision(18, 6).IsRequired();
        builder.HasIndex(c => new { c.UnidadOrigen, c.UnidadDestino }).IsUnique();
    }
}
