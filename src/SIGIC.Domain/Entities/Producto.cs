using SIGIC.Domain.Exceptions;

namespace SIGIC.Domain.Entities;

/// <summary>
/// Entidad Aggregate Root de Producto en SIGIC Ferretería v2.2.
/// Encapsula las reglas invariantes de stock (RNF-CON-003), segregación web (RF-INV-001)
/// y protección de margen comercial (RF-PRE-002, RF-INV-008).
/// </summary>
public class Producto
{
    public Guid Id { get; private set; }
    public string CodigoSku { get; private set; }
    public string Ean13 { get; private set; }
    public string NombreComercial { get; private set; }
    public string UnidadBase { get; private set; }
    public Guid FamiliaId { get; private set; }
    public Guid SubfamiliaId { get; private set; }
    public bool DisponibleEnWeb { get; private set; }
    public decimal CostoUnitario { get; private set; }
    public decimal PrecioVenta { get; private set; }
    public decimal StockFisico { get; private set; }
    public decimal StockComprometido { get; private set; }

    /// <summary>
    /// Invariante Cardinal 1 (RNF-CON-003):
    /// Stock Disponible = Stock Físico - Stock Comprometido.
    /// Prohibido calcular disponibilidad consultando únicamente el stock físico.
    /// </summary>
    public decimal StockDisponible => StockFisico - StockComprometido;

    public Producto(
        Guid id,
        string codigoSku,
        string nombreComercial,
        string unidadBase,
        decimal precioVenta,
        decimal costoUnitario,
        decimal stockFisicoInicial = 0m,
        bool disponibleEnWeb = false,
        string ean13 = "",
        Guid familiaId = default,
        Guid subfamiliaId = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSku))
            throw new ArgumentException("El código SKU no puede ser vacío.", nameof(codigoSku));

        if (string.IsNullOrWhiteSpace(nombreComercial))
            throw new ArgumentException("El nombre comercial no puede ser vacío.", nameof(nombreComercial));

        if (string.IsNullOrWhiteSpace(unidadBase))
            throw new ArgumentException("La unidad base no puede ser vacía.", nameof(unidadBase));

        if (costoUnitario < 0)
            throw new ArgumentOutOfRangeException(nameof(costoUnitario), "El costo unitario no puede ser negativo.");

        if (precioVenta < costoUnitario)
            throw new MargenInvalidoException(precioVenta, costoUnitario);

        if (stockFisicoInicial < 0)
            throw new ArgumentOutOfRangeException(nameof(stockFisicoInicial), "El stock inicial no puede ser negativo.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CodigoSku = codigoSku;
        NombreComercial = nombreComercial;
        UnidadBase = unidadBase;
        PrecioVenta = precioVenta;
        CostoUnitario = costoUnitario;
        StockFisico = stockFisicoInicial;
        StockComprometido = 0m;
        DisponibleEnWeb = disponibleEnWeb;
        Ean13 = ean13;
        FamiliaId = familiaId;
        SubfamiliaId = subfamiliaId;
    }

    /// <summary>
    /// Reserva stock temporalmente incrementando el stock comprometido (RF-WEB-002, RNF-CON-003).
    /// </summary>
    public void ReservarStock(decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad a reservar debe ser estrictamente mayor a cero.");

        if (cantidad > StockDisponible)
            throw new StockInsuficienteException(Id, CodigoSku, cantidad, StockDisponible);

        StockComprometido += cantidad;
    }

    /// <summary>
    /// Libera reservas de pedidos expirados o cancelados (RF-WEB-002).
    /// </summary>
    public void LiberarReserva(decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad a liberar debe ser mayor a cero.");

        if (cantidad > StockComprometido)
            throw new InvalidOperationException($"No se puede liberar {cantidad} unidades porque solo existen {StockComprometido} unidades comprometidas.");

        StockComprometido -= cantidad;
    }

    /// <summary>
    /// Registra la salida física de mercadería al concretarse una venta (POS o Web confirmada).
    /// </summary>
    public void RegistrarSalidaFisica(decimal cantidad, bool esDesdeReserva = false)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad de salida debe ser mayor a cero.");

        if (esDesdeReserva)
        {
            if (cantidad > StockComprometido)
                throw new InvalidOperationException($"La salida por reserva ({cantidad}) excede el stock comprometido ({StockComprometido}).");

            StockComprometido -= cantidad;
        }
        else
        {
            if (cantidad > StockDisponible)
                throw new StockInsuficienteException(Id, CodigoSku, cantidad, StockDisponible);
        }

        if (cantidad > StockFisico)
            throw new InvalidOperationException($"La cantidad a despachar ({cantidad}) excede las existencias físicas ({StockFisico}).");

        StockFisico -= cantidad;
    }

    /// <summary>
    /// Registra la entrada física de compras recalculando el Costo Promedio Ponderado (CPP) (RF-INV-006).
    /// </summary>
    public void RegistrarEntradaFisica(decimal cantidad, decimal costoUnitarioEntrante)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad de entrada debe ser mayor a cero.");

        if (costoUnitarioEntrante < 0)
            throw new ArgumentOutOfRangeException(nameof(costoUnitarioEntrante), "El costo unitario no puede ser negativo.");

        ActualizarCostoPonderado(cantidad, costoUnitarioEntrante);
        StockFisico += cantidad;
    }

    /// <summary>
    /// Actualiza los precios garantizando que el precio de venta nunca sea menor al costo (RF-PRE-002).
    /// </summary>
    public void ActualizarPrecios(decimal nuevoPrecioVenta, decimal nuevoCostoUnitario)
    {
        if (nuevoCostoUnitario < 0)
            throw new ArgumentOutOfRangeException(nameof(nuevoCostoUnitario), "El costo unitario no puede ser negativo.");

        if (nuevoPrecioVenta < nuevoCostoUnitario)
            throw new MargenInvalidoException(nuevoPrecioVenta, nuevoCostoUnitario);

        PrecioVenta = nuevoPrecioVenta;
        CostoUnitario = nuevoCostoUnitario;
    }

    /// <summary>
    /// Recalcula el Costo Promedio Ponderado contable ante nuevos ingresos de mercadería.
    /// </summary>
    public void ActualizarCostoPonderado(decimal cantEntrante, decimal costoUnitarioEntrante)
    {
        decimal stockTotalResultante = StockFisico + cantEntrante;
        if (stockTotalResultante > 0)
        {
            decimal inversionPrevia = StockFisico * CostoUnitario;
            decimal inversionEntrante = cantEntrante * costoUnitarioEntrante;
            CostoUnitario = Math.Round((inversionPrevia + inversionEntrante) / stockTotalResultante, 4);
        }
        else
        {
            CostoUnitario = costoUnitarioEntrante;
        }
    }

    /// <summary>
    /// Regla Invariante 2 (RF-INV-001): Determina si el artículo participa en el catálogo digital web.
    /// </summary>
    public bool EsVisibleParaWeb() => DisponibleEnWeb;

    /// <summary>
    /// Valida que una tarifa propuesta cumpla el umbral de seguridad sobre el costo.
    /// </summary>
    public bool ValidarMargenSeguridad(decimal precioPropuesto) => precioPropuesto >= CostoUnitario;
}
