namespace SIGIC.Infrastructure.Persistence.Entities;

public class ProductoEntity
{
    public Guid Id { get; set; }
    public string CodigoSku { get; set; } = string.Empty;
    public string Ean13 { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string UnidadBase { get; set; } = "UND";
    public Guid FamiliaId { get; set; }
    public Guid SubfamiliaId { get; set; }
    public bool DisponibleEnWeb { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal StockFisico { get; set; }
    public decimal StockComprometido { get; set; }
}

public class StockAlmacenEntity
{
    public Guid Id { get; set; }
    public Guid AlmacenId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal StockFisico { get; set; }
    public decimal StockComprometido { get; set; }
}

public class TurnoCajaEntity
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public Guid CajeroId { get; set; }
    public Guid AlmacenId { get; set; }
    public decimal FondoInicial { get; set; }
    public string Estado { get; set; } = "Aperturado";
    public DateTime FechaAperturaUtc { get; set; }
    public DateTime? FechaCierreUtc { get; set; }
}

public class CajaArqueoEntity
{
    public Guid Id { get; set; }
    public Guid TurnoCajaId { get; set; }
    public Guid CajeroId { get; set; }
    public DateTime FechaAperturaUtc { get; set; }
    public DateTime FechaCierreUtc { get; set; }
    public decimal FondoInicialSencillo { get; set; }
    public decimal TotalDigitalDeclarado { get; set; }
    public decimal TotalEfectivoDeclarado { get; set; }
    public decimal? SaldoSistemaTeorico { get; set; }
    public string? Justificacion { get; set; }
}

public class ArqueoDenominacionEntity
{
    public Guid Id { get; set; }
    public Guid ArqueoCajaId { get; set; }
    public decimal Denominacion { get; set; }
    public int Cantidad { get; set; }
    public decimal Subtotal { get; set; }
}

public class VentaPOSEntity
{
    public Guid Id { get; set; }
    public Guid TurnoCajaId { get; set; }
    public Guid CajeroId { get; set; }
    public Guid AlmacenId { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaUtc { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string? CodigoOperacionDigital { get; set; }
    public Guid? ClienteId { get; set; }
    public List<VentaPOSItemEntity> Items { get; set; } = new();
}

public class VentaPOSItemEntity
{
    public Guid Id { get; set; }
    public Guid VentaPOSId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public class PagoDigitalEntity
{
    public Guid Id { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string CodigoOperacion { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public Guid VentaId { get; set; }
    public DateTime FechaRegistroUtc { get; set; }
}

public class PedidoWebEntity
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public Guid ClienteId { get; set; }
    public Guid AlmacenOrigenId { get; set; }
    public Guid TiendaDestinoId { get; set; }
    public string ModalidadEntrega { get; set; } = string.Empty;
    public string ModalidadPago { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime FechaExpiracionUtc { get; set; }
    public DateTime? FechaLimiteVoucherUtc { get; set; }
    public DateTime? FechaSubidaVoucherUtc { get; set; }
    public string? GuiaRemisionNumero { get; set; }
    public List<PedidoWebItemEntity> Items { get; set; } = new();
}

public class PedidoWebItemEntity
{
    public Guid Id { get; set; }
    public Guid PedidoWebId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal CantidadComercial { get; set; }
    public string UnidadComercial { get; set; } = string.Empty;
    public decimal FactorConversion { get; set; }
    public decimal CantidadBaseReservada { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public class ComprobanteVoucherEntity
{
    public Guid Id { get; set; }
    public Guid PedidoWebId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string Formato { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public DateTime FechaSubidaUtc { get; set; }
    public bool Validado { get; set; }
    public DateTime? FechaValidacionUtc { get; set; }
}
