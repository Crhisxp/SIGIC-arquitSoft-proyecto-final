namespace SIGIC.Application.CashManagement.Commands;

public record ArqueoDenominacionDto(decimal ValorUnitario, int Cantidad);

/// <summary>
/// DTO de respuesta al cajero al finalizar el arqueo ciego (AC03, RF-CAJ-002).
/// Invariante cardinal: Cero visibilidad de saldos teóricos o discrepancias contables al cajero.
/// </summary>
public record ArqueoResultDto(
    Guid ArqueoId,
    Guid TurnoCajaId,
    decimal TotalDeclarado,
    DateTime FechaCierreUtc,
    string Mensaje = "Arqueo ciego registrado y turno cerrado exitosamente.");

/// <summary>
/// DTO exclusivo para Backoffice y reportes de auditoría contable.
/// </summary>
public record ArqueoAuditoriaDetalleDto(
    Guid ArqueoId,
    Guid TurnoCajaId,
    decimal TotalDeclarado,
    decimal SaldoTeorico,
    decimal Diferencia,
    bool TieneDiscrepancia,
    DateTime FechaCierreUtc);

public record VoucherValidacionResultDto(
    Guid PedidoWebId,
    bool Aprobado,
    string EstadoFinal,
    string? GuiaTrasladoCodigo = null);

public record VoucherMetadataDto(
    string NombreArchivo,
    string ContentType,
    long TamanoBytes,
    DateTime FechaSubidaUtc);

public record TurnoCajaDto(
    Guid TurnoId,
    Guid CajaId,
    Guid CajeroId,
    Guid AlmacenId,
    decimal FondoInicial,
    string Estado,
    DateTime FechaAperturaUtc,
    DateTime? FechaCierreUtc);

public record GuiaTrasladoItemDto(Guid ProductoId, decimal Cantidad);

public record GuiaTrasladoDetalleDto(
    Guid GuiaId,
    string NumeroGuia,
    Guid PedidoWebId,
    Guid AlmacenOrigenId,
    Guid TiendaDestinoId,
    string Estado,
    DateTime FechaEmisionUtc,
    IReadOnlyList<GuiaTrasladoItemDto> Items);

public record GuiaEntregaResultadoDto(
    Guid GuiaId,
    string NumeroGuia,
    Guid PedidoWebId,
    string Estado,
    string DniReceptor,
    DateTime FechaEntregaUtc,
    string Mensaje)
{
    public string Status => Estado;
}
