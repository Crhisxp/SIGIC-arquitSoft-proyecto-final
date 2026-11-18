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

    public async Task<VoucherMetadataDto> GuardarVoucherAsync(
        Guid pedidoWebId,
        Stream contenidoArchivo,
        string nombreArchivo,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        using var ms = new MemoryStream();
        await contenidoArchivo.CopyToAsync(ms, cancellationToken);
        byte[] bytes = ms.ToArray();

        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes = sha.ComputeHash(bytes);
        string hashSha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();

        string ext = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        DateTime now = DateTime.UtcNow;
        long tamanoBytes = bytes.Length;

        const string sqlVoucher = """
            INSERT INTO comprobantes_voucher (
                id, pedido_web_id, nombre_archivo, formato, hash_sha256, tamano_bytes, fecha_subida_utc
            ) VALUES (
                gen_random_uuid(), @pedidoWebId, @nombreArchivo, @ext, @hashSha256, @tamanoBytes, @now
            )
            ON CONFLICT (pedido_web_id) DO UPDATE
            SET nombre_archivo = EXCLUDED.nombre_archivo,
                formato = EXCLUDED.formato,
                hash_sha256 = EXCLUDED.hash_sha256,
                tamano_bytes = EXCLUDED.tamano_bytes,
                fecha_subida_utc = EXCLUDED.fecha_subida_utc;

            UPDATE pedidos_web
            SET fecha_subida_voucher_utc = @now,
                estado = CASE WHEN estado = 'PendientePago' THEN 'PendienteValidacion' ELSE estado END
            WHERE id = @pedidoWebId;
            """;

        var cmd = new CommandDefinition(
            sqlVoucher,
            new { pedidoWebId, nombreArchivo, ext, hashSha256, tamanoBytes, now },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmd);

        return new VoucherMetadataDto(nombreArchivo, contentType, tamanoBytes, now);
    }

    private class VoucherRow
    {
        public string NombreArchivo { get; set; } = string.Empty;
        public string Formato { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        public DateTime FechaSubidaUtc { get; set; }
    }
}
