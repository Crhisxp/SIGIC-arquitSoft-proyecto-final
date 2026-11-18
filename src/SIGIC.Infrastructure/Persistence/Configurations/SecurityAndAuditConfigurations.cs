using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIGIC.Application.Security.Models;
using SIGIC.Infrastructure.Persistence.Entities;

namespace SIGIC.Infrastructure.Persistence.Configurations;

public class KardexMovimientoConfiguration : IEntityTypeConfiguration<KardexMovimientoEntity>
{
    public void Configure(EntityTypeBuilder<KardexMovimientoEntity> builder)
    {
        builder.ToTable("kardex_movimientos");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.TipoOperacion).HasMaxLength(50).IsRequired();
        builder.Property(k => k.CantidadBase).HasPrecision(18, 4).IsRequired();
        builder.Property(k => k.CostoUnitario).HasPrecision(18, 4).IsRequired();
        builder.Property(k => k.DocumentoReferencia).HasMaxLength(100).IsRequired();
        builder.Property(k => k.CodigoAutorizacion).HasMaxLength(100);
        builder.Property(k => k.Motivo).HasMaxLength(250);
        builder.HasIndex(k => new { k.ProductoId, k.AlmacenId, k.FechaUtc });
    }
}

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.Accion).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Entidad).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntidadId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Detalles).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(50).IsRequired();
        builder.Property(a => a.TerminalScope).HasMaxLength(50).IsRequired();
        builder.Property(a => a.TimestampUtc).IsRequired();
        builder.HasIndex(a => new { a.Entidad, a.TimestampUtc });
    }
}

public class UsuarioConfiguration : IEntityTypeConfiguration<UsuarioEntity>
{
    public void Configure(EntityTypeBuilder<UsuarioEntity> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(u => u.RolesJson).HasMaxLength(1000).IsRequired();
        builder.Property(u => u.PerfilCliente).HasMaxLength(50).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
    }
}
