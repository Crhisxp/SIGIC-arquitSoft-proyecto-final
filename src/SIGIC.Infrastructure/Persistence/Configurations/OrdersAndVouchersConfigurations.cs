using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIGIC.Infrastructure.Persistence.Entities;

namespace SIGIC.Infrastructure.Persistence.Configurations;

public class PedidoWebConfiguration : IEntityTypeConfiguration<PedidoWebEntity>
{
    public void Configure(EntityTypeBuilder<PedidoWebEntity> builder)
    {
        builder.ToTable("pedidos_web");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ModalidadEntrega).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ModalidadPago).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Estado).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.GuiaRemisionNumero).HasMaxLength(60);
        builder.HasIndex(p => p.Codigo).IsUnique();
        builder.HasIndex(p => new { p.Estado, p.FechaExpiracionUtc });
        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.PedidoWebId);
    }
}

public class PedidoWebItemConfiguration : IEntityTypeConfiguration<PedidoWebItemEntity>
{
    public void Configure(EntityTypeBuilder<PedidoWebItemEntity> builder)
    {
        builder.ToTable("pedidos_web_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.CantidadComercial).HasPrecision(18, 4).IsRequired();
        builder.Property(i => i.UnidadComercial).HasMaxLength(20).IsRequired();
        builder.Property(i => i.FactorConversion).HasPrecision(18, 6).IsRequired();
        builder.Property(i => i.CantidadBaseReservada).HasPrecision(18, 4).IsRequired();
        builder.Property(i => i.PrecioUnitario).HasPrecision(18, 4).IsRequired();
        builder.Property(i => i.Subtotal).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(i => new { i.PedidoWebId, i.ProductoId });
    }
}

public class GuiaTrasladoConfiguration :
    IEntityTypeConfiguration<GuiaTrasladoInternoEntity>,
    IEntityTypeConfiguration<GuiaTrasladoItemEntity>
{
    public void Configure(EntityTypeBuilder<GuiaTrasladoInternoEntity> builder)
    {
        builder.ToTable("guias_traslado_interno");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.NumeroGuia).HasMaxLength(60).IsRequired();
        builder.Property(g => g.Estado).HasMaxLength(50).IsRequired();
        builder.Property(g => g.FechaEmisionUtc).IsRequired();
        builder.HasIndex(g => g.NumeroGuia).IsUnique();
        builder.HasIndex(g => g.PedidoWebId);
        builder.HasMany(g => g.Items).WithOne().HasForeignKey(i => i.GuiaTrasladoId);
    }

    public void Configure(EntityTypeBuilder<GuiaTrasladoItemEntity> builder)
    {
        builder.ToTable("guias_traslado_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Cantidad).HasPrecision(18, 4).IsRequired();
        builder.HasIndex(i => i.GuiaTrasladoId);
    }
}

public class ComprobanteVoucherConfiguration : IEntityTypeConfiguration<ComprobanteVoucherEntity>
{
    public void Configure(EntityTypeBuilder<ComprobanteVoucherEntity> builder)
    {
        builder.ToTable("comprobantes_voucher");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.NombreArchivo).HasMaxLength(255).IsRequired();
        builder.Property(v => v.Formato).HasMaxLength(50).IsRequired();
        builder.Property(v => v.HashSha256).HasMaxLength(100).IsRequired();
        builder.HasIndex(v => v.PedidoWebId).IsUnique();
    }
}
