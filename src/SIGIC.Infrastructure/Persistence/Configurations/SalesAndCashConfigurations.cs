using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIGIC.Infrastructure.Persistence.Entities;

namespace SIGIC.Infrastructure.Persistence.Configurations;

public class TurnoCajaConfiguration : IEntityTypeConfiguration<TurnoCajaEntity>
{
    public void Configure(EntityTypeBuilder<TurnoCajaEntity> builder)
    {
        builder.ToTable("turnos_caja");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.FondoInicial).HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.Estado).HasMaxLength(30).IsRequired();
        builder.Property(t => t.FechaAperturaUtc).IsRequired();
        builder.HasIndex(t => new { t.CajaId, t.Estado });
    }
}

public class CajaArqueoConfiguration : IEntityTypeConfiguration<CajaArqueoEntity>
{
    public void Configure(EntityTypeBuilder<CajaArqueoEntity> builder)
    {
        builder.ToTable("caja_arqueo");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.TurnoCajaId).IsRequired();
        builder.Property(a => a.CajeroId).IsRequired();
        builder.Property(a => a.FechaAperturaUtc).IsRequired();
        builder.Property(a => a.FechaCierreUtc).IsRequired();
        builder.Property(a => a.FondoInicialSencillo).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.TotalDigitalDeclarado).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.TotalEfectivoDeclarado).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.SaldoSistemaTeorico).HasPrecision(18, 2);
        builder.Property(a => a.Justificacion).HasMaxLength(500);
        builder.HasIndex(a => a.TurnoCajaId);
    }
}

public class ArqueoDenominacionConfiguration : IEntityTypeConfiguration<ArqueoDenominacionEntity>
{
    public void Configure(EntityTypeBuilder<ArqueoDenominacionEntity> builder)
    {
        builder.ToTable("caja_arqueo_denominacion");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Denominacion).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.Subtotal).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(d => d.ArqueoCajaId);
    }
}

public class VentaPOSConfiguration :
    IEntityTypeConfiguration<VentaPOSEntity>,
    IEntityTypeConfiguration<VentaPOSItemEntity>,
    IEntityTypeConfiguration<VentaPOSPagoEntity>,
    IEntityTypeConfiguration<PagoDigitalEntity>,
    IEntityTypeConfiguration<AlertaCajaOutboxEntity>
{
    public void Configure(EntityTypeBuilder<VentaPOSEntity> builder)
    {
        builder.ToTable("ventas_pos");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(v => v.MetodoPago).HasMaxLength(50).IsRequired();
        builder.Property(v => v.CodigoOperacionDigital).HasMaxLength(100);
        builder.HasMany(v => v.Items).WithOne().HasForeignKey(i => i.VentaPOSId);
    }

    public void Configure(EntityTypeBuilder<VentaPOSItemEntity> builder)
    {
        builder.ToTable("ventas_pos_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Cantidad).HasPrecision(18, 4).IsRequired();
        builder.Property(i => i.PrecioUnitario).HasPrecision(18, 4).IsRequired();
        builder.Property(i => i.Subtotal).HasPrecision(18, 2).IsRequired();
    }

    public void Configure(EntityTypeBuilder<VentaPOSPagoEntity> builder)
    {
        builder.ToTable("ventas_pos_pagos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.MetodoPago).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Monto).HasPrecision(18, 4).IsRequired();
        builder.Property(p => p.CodigoOperacionDigital).HasMaxLength(100);
        builder.HasIndex(p => p.VentaPOSId);
    }

    public void Configure(EntityTypeBuilder<PagoDigitalEntity> builder)
    {
        builder.ToTable("pagos_digitales");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.MetodoPago).HasMaxLength(50).IsRequired();
        builder.Property(p => p.CodigoOperacion).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Fecha).IsRequired();
        builder.HasIndex(p => new { p.MetodoPago, p.CodigoOperacion, p.Fecha }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<AlertaCajaOutboxEntity> builder)
    {
        builder.ToTable("alertas_caja_outbox");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Diferencia).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.Justificacion).HasMaxLength(500);
    }
}
