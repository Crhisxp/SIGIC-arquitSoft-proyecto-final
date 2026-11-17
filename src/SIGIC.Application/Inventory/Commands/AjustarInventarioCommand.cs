namespace SIGIC.Application.Inventory.Commands;

/// <summary>
/// Comando para realizar ajustes extraordinarios de inventario con autorización de Superadmin (RF-INV-007, RNF-CON-003).
/// Invariante: Inserta un movimiento compensatorio en Kardex sin mutar registros históricos.
/// </summary>
public record AjustarInventarioCommand(
    Guid ProductoId,
    Guid AlmacenId,
    decimal CantidadAjuste,
    string Motivo,
    string CodigoAutorizacionSuperadmin);
