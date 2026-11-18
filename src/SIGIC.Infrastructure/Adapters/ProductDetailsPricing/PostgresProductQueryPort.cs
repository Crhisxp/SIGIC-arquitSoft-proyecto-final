using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.ProductDetailsPricing.Ports;
using SIGIC.Domain.Entities;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.ProductDetailsPricing;

/// <summary>
/// Puerto de lectura rápida optimizado con Dapper para catálogo y detalle de producto PDP (RF-WEB-001, RNF-CON-003).
/// Invariante: Cálculo riguroso de Stock Disponible = Stock Físico - Stock Comprometido.
/// Respeta límites modulares: consulta directamente el catálogo de productos y no realiza accesos cruzados a existencias multi-almacén.
/// </summary>
public class PostgresProductQueryPort : IProductQueryPort
{
    private readonly SigicDbContext _dbContext;

    public PostgresProductQueryPort(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Producto?> ObtenerParaPDPAsync(Guid productoId, CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        // Lectura transaccional coordinada con FOR SHARE bajo checkout/reserva; no bloqueante para catálogo ordinario
        string lockClause = tx != null ? " FOR SHARE" : string.Empty;
        string sql = $"""
            SELECT id AS Id, codigo_sku AS CodigoSku, nombre_comercial AS NombreComercial,
                   unidad_base AS UnidadBase, precio_venta AS PrecioVenta, costo_unitario AS CostoUnitario,
                   disponible_en_web AS DisponibleEnWeb, ean13 AS Ean13,
                   familia_id AS FamiliaId, subfamilia_id AS SubfamiliaId,
                   stock_fisico AS StockFisico, stock_comprometido AS StockComprometido
            FROM productos
            WHERE id = @productoId{lockClause};
            """;

        var cmd = new CommandDefinition(sql, new { productoId }, tx, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<ProductRow>(cmd);

        if (row == null)
            return null;

        return new Producto(
            row.Id, row.CodigoSku, row.NombreComercial, row.UnidadBase,
            row.PrecioVenta, row.CostoUnitario, row.StockFisico, row.DisponibleEnWeb,
            row.Ean13 ?? string.Empty, row.FamiliaId, row.SubfamiliaId,
            stockComprometidoInicial: row.StockComprometido);
    }

    public async Task<IReadOnlyList<Producto>> ObtenerMultiplesParaPDPAsync(
        IEnumerable<Guid> productoIds,
        CancellationToken cancellationToken = default)
    {
        if (productoIds == null)
            return Array.Empty<Producto>();

        var idList = productoIds.Distinct().OrderBy(id => id).ToArray();
        if (idList.Length == 0)
            return Array.Empty<Producto>();

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        // Ordenamiento ascendente Havender + FOR SHARE bajo checkout/transacción para lectura autoritativa consistente
        // Consulta no bloqueante libre de locks cuando no hay transacción activa (catálogo WAN/PDP)
        string lockClause = tx != null ? " ORDER BY id ASC FOR SHARE" : string.Empty;
        string sql = $"""
            SELECT id AS Id, codigo_sku AS CodigoSku, nombre_comercial AS NombreComercial,
                   unidad_base AS UnidadBase, precio_venta AS PrecioVenta, costo_unitario AS CostoUnitario,
                   disponible_en_web AS DisponibleEnWeb, ean13 AS Ean13,
                   familia_id AS FamiliaId, subfamilia_id AS SubfamiliaId,
                   stock_fisico AS StockFisico, stock_comprometido AS StockComprometido
            FROM productos
            WHERE id = ANY(@idList){lockClause};
            """;

        var cmd = new CommandDefinition(sql, new { idList }, tx, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<ProductRow>(cmd);

        return rows.Select(row => new Producto(
            row.Id, row.CodigoSku, row.NombreComercial, row.UnidadBase,
            row.PrecioVenta, row.CostoUnitario, row.StockFisico, row.DisponibleEnWeb,
            row.Ean13 ?? string.Empty, row.FamiliaId, row.SubfamiliaId,
            stockComprometidoInicial: row.StockComprometido)).ToList();
    }

    public async Task<decimal> ObtenerFactorConversionAsync(
        string unidadOrigen,
        string unidadDestino,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(unidadOrigen, unidadDestino, StringComparison.OrdinalIgnoreCase))
            return 1.0000m;

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT factor_conversion
            FROM conversiones_unidad
            WHERE UPPER(unidad_origen) = UPPER(@unidadOrigen)
              AND UPPER(unidad_destino) = UPPER(@unidadDestino);
            """;

        var cmd = new CommandDefinition(sql, new { unidadOrigen, unidadDestino }, tx, cancellationToken: cancellationToken);
        var factor = await conn.QuerySingleOrDefaultAsync<decimal?>(cmd);

        if (!factor.HasValue || factor.Value <= 0m)
        {
            throw new InvalidOperationException(
                $"No existe factor de conversión configurado entre '{unidadOrigen}' y '{unidadDestino}'.");
        }

        return factor.Value;
    }

    private class ProductRow
    {
        public Guid Id { get; set; }
        public string CodigoSku { get; set; } = string.Empty;
        public string NombreComercial { get; set; } = string.Empty;
        public string UnidadBase { get; set; } = string.Empty;
        public decimal PrecioVenta { get; set; }
        public decimal CostoUnitario { get; set; }
        public bool DisponibleEnWeb { get; set; }
        public string? Ean13 { get; set; }
        public Guid FamiliaId { get; set; }
        public Guid SubfamiliaId { get; set; }
        public decimal StockFisico { get; set; }
        public decimal StockComprometido { get; set; }
    }
}
