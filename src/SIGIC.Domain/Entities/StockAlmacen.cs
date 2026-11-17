using SIGIC.Domain.Exceptions;

namespace SIGIC.Domain.Entities;

/// <summary>
/// Representa el stock de un producto en un almacén específico (RF-INV-002, RF-INV-007, RNF-CON-003).
/// Invariante cardinal: Stock Disponible = Stock Físico - Stock Comprometido.
/// </summary>
public class StockAlmacen
{
    public Guid Id { get; }
    public Guid ProductoId { get; }
    public Guid AlmacenId { get; }
    public decimal StockFisico { get; private set; }
    public decimal StockComprometido { get; private set; }

    public decimal StockDisponible => StockFisico - StockComprometido;

    public StockAlmacen(
        Guid id,
        Guid productoId,
        Guid almacenId,
        decimal stockFisicoInicial = 0m,
        decimal stockComprometidoInicial = 0m)
    {
        if (productoId == Guid.Empty)
            throw new ArgumentException("El ID de producto es obligatorio.", nameof(productoId));

        if (almacenId == Guid.Empty)
            throw new ArgumentException("El ID de almacén es obligatorio.", nameof(almacenId));

        if (stockFisicoInicial < 0)
            throw new ArgumentOutOfRangeException(nameof(stockFisicoInicial), "El stock inicial no puede ser negativo.");

        if (stockComprometidoInicial < 0)
            throw new ArgumentOutOfRangeException(nameof(stockComprometidoInicial), "El stock comprometido no puede ser negativo.");

        if (stockFisicoInicial < stockComprometidoInicial)
            throw new InvalidOperationException("El stock físico no puede ser inferior al stock comprometido.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProductoId = productoId;
        AlmacenId = almacenId;
        StockFisico = stockFisicoInicial;
        StockComprometido = stockComprometidoInicial;
    }

    public void ReservarStock(decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad a reservar debe ser mayor a cero.");

        if (cantidad > StockDisponible)
            throw new StockInsuficienteException(ProductoId, string.Empty, cantidad, StockDisponible);

        StockComprometido += cantidad;
    }

    public void LiberarReserva(decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad a liberar debe ser mayor a cero.");

        if (cantidad > StockComprometido)
            throw new InvalidOperationException($"No se puede liberar {cantidad} porque solo existen {StockComprometido} unidades comprometidas.");

        StockComprometido -= cantidad;
    }

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
                throw new StockInsuficienteException(ProductoId, string.Empty, cantidad, StockDisponible);
        }

        if (cantidad > StockFisico)
            throw new InvalidOperationException($"La salida ({cantidad}) excede las existencias físicas ({StockFisico}).");

        StockFisico -= cantidad;
    }

    public void RegistrarEntradaFisica(decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad de entrada debe ser mayor a cero.");

        StockFisico += cantidad;
    }

    public void AplicarAjusteFisico(decimal delta)
    {
        decimal resultante = StockFisico + delta;
        if (resultante < 0)
            throw new ArgumentOutOfRangeException(nameof(delta), "El ajuste físico no puede resultar en existencias negativas.");

        if (resultante < StockComprometido)
            throw new InvalidOperationException($"El ajuste físico ({resultante}) no puede ser inferior al stock comprometido ({StockComprometido}).");

        StockFisico = resultante;
    }
}
