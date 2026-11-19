namespace SIGIC.Domain.Analytics;

/// <summary>Importes consolidados en PEN durante los días cerrados del período.</summary>
public sealed record CierreOperativo(
    long DiasCerrados, decimal IngresosNetos, decimal CostoVentas,
    decimal GastosOperativos, decimal InventarioMedioAlCosto)
{
    public decimal UtilidadOperativa => IngresosNetos - CostoVentas - GastosOperativos;
    public decimal? MargenOperativo => IngresosNetos == 0 ? null : 100 * UtilidadOperativa / IngresosNetos;
    public decimal? Rotacion => InventarioMedioAlCosto == 0 ? null : CostoVentas / InventarioMedioAlCosto;
}
