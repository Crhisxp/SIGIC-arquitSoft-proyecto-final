namespace SIGIC.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta fijar un precio de venta menor al costo unitario
/// infringiendo la regla de margen de seguridad comercial (RF-PRE-002, RF-INV-008).
/// </summary>
public class MargenInvalidoException : Exception
{
    public decimal PrecioVenta { get; }
    public decimal CostoUnitario { get; }

    public MargenInvalidoException(decimal precioVenta, decimal costoUnitario)
        : base($"Operación rechazada: Margen de seguridad comercial negativo. El precio de venta (S/ {precioVenta:N2}) no puede ser inferior al costo unitario (S/ {costoUnitario:N2}).")
    {
        PrecioVenta = precioVenta;
        CostoUnitario = costoUnitario;
    }
}
