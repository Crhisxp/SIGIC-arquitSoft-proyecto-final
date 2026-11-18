using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SIGIC.Domain.Entities;
using SIGIC.Infrastructure.Adapters.Inventory;
using SIGIC.Infrastructure.Persistence;
using SIGIC.Infrastructure.Persistence.Entities;
using SIGIC.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SIGIC.Infrastructure.Tests.Persistence;

[Collection("PostgreSqlDatabase")]
public class EfCoreDapperAlignmentTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public EfCoreDapperAlignmentTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    private SigicDbContext CreateDbContext()
    {
        return _fixture.CreateDbContext();
    }

    [Fact]
    public async Task EfCore_PascalCaseToSnakeCaseMapping_QueriesAndPersistsCorrectly()
    {
        await using var db = CreateDbContext();
        var prodId = Guid.NewGuid();
        string sku = $"SKU-SNAKE-{Guid.NewGuid():N}";

        var entity = new ProductoEntity
        {
            Id = prodId,
            CodigoSku = sku,
            NombreComercial = "Producto Snake Case Test",
            UnidadBase = "UND",
            PrecioVenta = 125.50m,
            CostoUnitario = 80.00m,
            DisponibleEnWeb = true,
            StockFisico = 10,
            StockComprometido = 2
        };

        db.Productos.Add(entity);
        await db.SaveChangesAsync();

        // 1. Consultar vía EF Core
        var loadedEf = await db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == prodId);
        Assert.NotNull(loadedEf);
        Assert.Equal(sku, loadedEf.CodigoSku);
        Assert.Equal(125.50m, loadedEf.PrecioVenta);
        Assert.True(loadedEf.DisponibleEnWeb);

        // 2. Verificar vía Dapper directo que las columnas en PostgreSQL son snake_case
        await using var conn = new NpgsqlConnection(_fixture.AppConnectionString);
        await conn.OpenAsync();
        var dapperRow = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT codigo_sku, nombre_comercial, unidad_base, precio_venta, disponible_en_web, stock_fisico, stock_comprometido FROM productos WHERE id = @id;",
            new { id = prodId });

        Assert.NotNull((object?)dapperRow);
        Assert.Equal(sku, (string)dapperRow!.codigo_sku);
        Assert.Equal(125.50m, Convert.ToDecimal(dapperRow!.precio_venta));
        Assert.True((bool)dapperRow!.disponible_en_web);
    }

    [Fact]
    public async Task EfCoreAndDapper_ShareSameTransaction_RollbackLeavesNoData()
    {
        await using var db = CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        var prodId = Guid.NewGuid();
        string sku = $"SKU-TX-{Guid.NewGuid():N}";

        // 1. EF Core inserta producto
        var entity = new ProductoEntity
        {
            Id = prodId,
            CodigoSku = sku,
            NombreComercial = "Producto Shared Tx",
            UnidadBase = "UND",
            PrecioVenta = 50m,
            CostoUnitario = 30m,
            DisponibleEnWeb = false,
            StockFisico = 15,
            StockComprometido = 0
        };
        db.Productos.Add(entity);
        await db.SaveChangesAsync();

        // 2. Dapper inserta en stock_almacen dentro de la misma transacción
        var conn = db.Database.GetDbConnection();
        var dbTx = tx.GetDbTransaction();

        var almacenId = Guid.NewGuid();
        string almCod = $"ALM-TX-{Guid.NewGuid():N}";
        await conn.ExecuteAsync(
            "INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Almacen Shared Tx', TRUE, 'Av Central');",
            new { a = almacenId, c = almCod }, dbTx);

        await conn.ExecuteAsync(
            "INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido) VALUES (gen_random_uuid(), @a, @p, 15, 0);",
            new { a = almacenId, p = prodId }, dbTx);

        // 3. Rollback
        await tx.RollbackAsync();

        // 4. Verificar en conexión limpia que nada se persistió
        await using var verifyConn = new NpgsqlConnection(_fixture.AppConnectionString);
        await verifyConn.OpenAsync();

        int prodCount = await verifyConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM productos WHERE id = @id;", new { id = prodId });
        int almCount = await verifyConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM almacenes WHERE id = @id;", new { id = almacenId });

        Assert.Equal(0, prodCount);
        Assert.Equal(0, almCount);
    }

    [Fact]
    public async Task EfCoreAndDapper_ShareSameTransaction_CommitPersistsAll()
    {
        await using var db = CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        var prodId = Guid.NewGuid();
        string sku = $"SKU-TX-C-{Guid.NewGuid():N}";

        // 1. EF Core inserta producto
        var entity = new ProductoEntity
        {
            Id = prodId,
            CodigoSku = sku,
            NombreComercial = "Producto Commit Tx",
            UnidadBase = "UND",
            PrecioVenta = 90m,
            CostoUnitario = 60m,
            DisponibleEnWeb = true,
            StockFisico = 20,
            StockComprometido = 0
        };
        db.Productos.Add(entity);
        await db.SaveChangesAsync();

        // 2. Dapper inserta almacén en la misma transacción
        var conn = db.Database.GetDbConnection();
        var dbTx = tx.GetDbTransaction();

        var almacenId = Guid.NewGuid();
        string almCod = $"ALM-TX-C-{Guid.NewGuid():N}";
        await conn.ExecuteAsync(
            "INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES (@a, @c, 'Almacen Commit Tx', FALSE, 'Av Secundaria');",
            new { a = almacenId, c = almCod }, dbTx);

        // 3. Commit
        await tx.CommitAsync();

        // 4. Verificar en conexión limpia que ambos existen
        await using var verifyConn = new NpgsqlConnection(_fixture.AppConnectionString);
        await verifyConn.OpenAsync();

        int prodCount = await verifyConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM productos WHERE id = @id;", new { id = prodId });
        int almCount = await verifyConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM almacenes WHERE id = @id;", new { id = almacenId });

        Assert.Equal(1, prodCount);
        Assert.Equal(1, almCount);
    }

    [Fact]
    public async Task ConfigurationWarehousePort_ValidatesWarehousesCorrectly()
    {
        await using var conn = new NpgsqlConnection(_fixture.AppConnectionString);
        await conn.OpenAsync();

        var centralId = Guid.NewGuid();
        var tiendaId = Guid.NewGuid();

        await conn.ExecuteAsync("""
            INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion)
            VALUES (@cId, @cCod, 'Almacén Central Test', TRUE, 'Av Central 123'),
                   (@tId, @tCod, 'Tienda Huamanga', FALSE, 'Jr 28 de Julio 456');
            """, new
        {
            cId = centralId,
            cCod = $"ALM-C-{Guid.NewGuid():N}",
            tId = tiendaId,
            tCod = $"ALM-T-{Guid.NewGuid():N}"
        });

        await using var db = CreateDbContext();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WarehouseSettings:CentralWarehouseId"] = centralId.ToString()
            })
            .Build();

        var port = new ConfigurationWarehousePort(db, config);

        // 1. Almacén Central válido
        bool esCentralValido = port.EsAlmacenValido(centralId);
        Assert.True(esCentralValido);

        // 2. Tienda/Almacén no central válido
        bool esTiendaValida = port.EsAlmacenValido(tiendaId);
        Assert.True(esTiendaValida);

        // 3. Almacén inexistente inválido
        bool esInexistenteValido = port.EsAlmacenValido(Guid.NewGuid());
        Assert.False(esInexistenteValido);
    }
}
