namespace SIGIC.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando una operación de venta o reserva intenta comprometer una cantidad
/// que excede el Stock Disponible (StockFisico - StockComprometido) según la regla RNF-CON-003.
/// </summary>
public class StockInsuficienteException : Exception
{
    public Guid ProductoId { get; }
    public string CodigoSku { get; }
    public decimal CantidadSolicitada { get; }
    public decimal StockDisponibleActual { get; }

    public StockInsuficienteException(
        Guid productoId,
        string codigoSku,
        decimal cantidadSolicitada,
        decimal stockDisponibleActual)
        : base($"Stock insuficiente para el producto '{codigoSku}' (ID: {productoId}). Cantidad solicitada: {cantidadSolicitada}, Stock disponible: {stockDisponibleActual} (Físico - Comprometido).")
    {
        ProductoId = productoId;
        CodigoSku = codigoSku;
        CantidadSolicitada = cantidadSolicitada;
        StockDisponibleActual = stockDisponibleActual;
    }

    public StockInsuficienteException(string message) : base(message)
    {
        CodigoSku = string.Empty;
    }
}
