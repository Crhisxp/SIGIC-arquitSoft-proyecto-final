using SIGIC.Application.CashManagement.Commands;

namespace SIGIC.Application.CashManagement.Ports;

/// <summary>
/// Puerto para consulta de metadatos de comprobantes/vouchers de pago (RF-WEB-002).
/// </summary>
public interface IVoucherStoragePort
{
    Task<VoucherMetadataDto?> ObtenerMetadatosVoucherAsync(Guid pedidoWebId, CancellationToken cancellationToken = default);
}
