using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.CashManagement;

/// <summary>
/// Adaptador de consulta para metadatos de comprobantes y vouchers de pago web (RF-WEB-002).
/// </summary>
public class LocalVoucherStoragePort : IVoucherStoragePort
{
    private readonly SigicDbContext _dbContext;

    public LocalVoucherStoragePort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<VoucherMetadataDto?> ObtenerMetadatosVoucherAsync(
        Guid pedidoWebId,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT nombre_archivo AS NombreArchivo, formato AS Formato,
                   tamano_bytes AS TamanoBytes, fecha_subida_utc AS FechaSubidaUtc
            FROM comprobantes_voucher
            WHERE pedido_web_id = @pedidoWebId;
            """;

        var cmd = new CommandDefinition(sql, new { pedidoWebId }, tx, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<VoucherRow>(cmd);

        if (row == null)
            return null;

        string contentType = row.Formato.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or "jpg" or "jpeg" => "image/jpeg",
            ".png" or "png" => "image/png",
            ".pdf" or "pdf" => "application/pdf",
            _ => row.Formato
        };

        return new VoucherMetadataDto(
            row.NombreArchivo,
            contentType,
            row.TamanoBytes,
            row.FechaSubidaUtc);
    }

    private class VoucherRow
    {
        public string NombreArchivo { get; set; } = string.Empty;
        public string Formato { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        public DateTime FechaSubidaUtc { get; set; }
    }
}
