namespace SIGIC.Infrastructure.Persistence.Entities;

public class KardexMovimientoEntity
{
    public Guid Id { get; set; }
    public Guid ProductoId { get; set; }
    public Guid AlmacenId { get; set; }
    public string TipoOperacion { get; set; } = string.Empty;
    public decimal CantidadBase { get; set; }
    public decimal CostoUnitario { get; set; }
    public string DocumentoReferencia { get; set; } = string.Empty;
    public string? CodigoAutorizacion { get; set; }
    public string? Motivo { get; set; }
    public DateTime FechaUtc { get; set; }
}

public class AuditLogEntryEntity
{
    public Guid Id { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string TerminalScope { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; }
}

public class AlertaCajaOutboxEntity
{
    public Guid Id { get; set; }
    public Guid TurnoCajaId { get; set; }
    public Guid CajeroId { get; set; }
    public decimal Diferencia { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; }
    public bool Procesado { get; set; }
}

public class TarifaRolEntity
{
    public Guid Id { get; set; }
    public string Rol { get; set; } = string.Empty;
    public decimal PorcentajeDescuento { get; set; }
}

public class DescuentoVolumenEntity
{
    public Guid Id { get; set; }
    public decimal CantidadMinima { get; set; }
    public decimal PorcentajeDescuento { get; set; }
}

public class ConversionUnidadEntity
{
    public Guid Id { get; set; }
    public string UnidadOrigen { get; set; } = string.Empty;
    public string UnidadDestino { get; set; } = string.Empty;
    public decimal FactorConversion { get; set; }
}

public class UsuarioEntity
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
    public bool EstaActivo { get; set; } = true;
    public string RolesJson { get; set; } = "[]";
    public string PerfilCliente { get; set; } = "PublicoGeneral";
}

public class AlmacenEntity
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsAlmacenCentral { get; set; }
    public string Direccion { get; set; } = string.Empty;
}

public class VentaPOSPagoEntity
{
    public Guid Id { get; set; }
    public Guid VentaPOSId { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? CodigoOperacionDigital { get; set; }
}

public class GuiaTrasladoInternoEntity
{
    public Guid Id { get; set; }
    public string NumeroGuia { get; set; } = string.Empty;
    public Guid PedidoWebId { get; set; }
    public Guid AlmacenOrigenId { get; set; }
    public Guid TiendaDestinoId { get; set; }
    public string Estado { get; set; } = "EMITIDA";
    public DateTime FechaEmisionUtc { get; set; }
    public List<GuiaTrasladoItemEntity> Items { get; set; } = new();
}

public class GuiaTrasladoItemEntity
{
    public Guid Id { get; set; }
    public Guid GuiaTrasladoId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal Cantidad { get; set; }
}
