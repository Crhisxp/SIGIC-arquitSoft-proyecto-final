using SIGIC.Application.CashManagement.Commands;

namespace SIGIC.Application.CashManagement.Ports;

/// <summary>
/// Puerto para consulta de metadatos de comprobantes/vouchers de pago (RF-WEB-002).
/// </summary>
public interface IVoucherStoragePort
{
    Task<VoucherMetadataDto?> ObtenerMetadatosVoucherAsync(Guid pedidoWebId, CancellationToken cancellationToken = default);

    Task<VoucherMetadataDto> GuardarVoucherAsync(
        Guid pedidoWebId,
        Stream contenidoArchivo,
        string nombreArchivo,
        string contentType,
        CancellationToken cancellationToken = default) => Task.FromResult(new VoucherMetadataDto(nombreArchivo, contentType, 0, DateTime.UtcNow));
}
