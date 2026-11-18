using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.OrdersWeb;

/// <summary>
/// Adaptador de persistencia para pedidos web con bloqueo pesimista y transiciones atómicas (RF-WEB-002, RNF-CON-004).
/// </summary>
public class PostgresWebOrderRepository : IWebOrderRepository
{
    private readonly SigicDbContext _dbContext;

    public PostgresWebOrderRepository(SigicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PedidoWebDto?> ObtenerPorIdAsync(Guid pedidoId, CancellationToken cancellationToken = default)
    {
        return await QueryPedidoInternalAsync(pedidoId, forUpdate: false, cancellationToken);
    }

    public async Task<PedidoWebDto?> ObtenerParaBloqueoAsync(Guid pedidoId, CancellationToken cancellationToken = default)
    {
        return await QueryPedidoInternalAsync(pedidoId, forUpdate: true, cancellationToken);
    }

    public async Task GuardarPedidoAsync(PedidoWebDto pedido, CancellationToken cancellationToken = default)
    {
        if (pedido == null)
            throw new ArgumentNullException(nameof(pedido));

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        string codigo = $"WEB-{pedido.FechaCreacionUtc:yyyyMMdd}-{pedido.Id.ToString()[..8].ToUpperInvariant()}";
        decimal total = pedido.Items.Sum(i => i.Cantidad * i.PrecioUnitario);

        const string sqlPedido = """
            INSERT INTO pedidos_web (
                id, codigo, cliente_id, almacen_origen_id, tienda_destino_id,
                modalidad_entrega, modalidad_pago, estado, total,
                fecha_creacion_utc, fecha_expiracion_utc, fecha_limite_voucher_utc,
                fecha_subida_voucher_utc, guia_remision_numero
            ) VALUES (
                @Id, @codigo, @ClienteId, @AlmacenOrigenId, @TiendaDestinoId,
                @ModalidadEntrega, @MetodoPago, @Estado, @total,
                @FechaCreacionUtc, @FechaExpiracionUtc, @FechaLimiteVoucherUtc,
                @FechaSubidaVoucherUtc, NULL
            );
            """;

        var cmdPedido = new CommandDefinition(
            sqlPedido,
            new
            {
                pedido.Id,
                codigo,
                pedido.ClienteId,
                pedido.AlmacenOrigenId,
                TiendaDestinoId = pedido.TiendaDestinoId ?? Guid.Empty,
                pedido.ModalidadEntrega,
                pedido.MetodoPago,
                pedido.Estado,
                total,
                pedido.FechaCreacionUtc,
                pedido.FechaExpiracionUtc,
                pedido.FechaLimiteVoucherUtc,
                pedido.FechaSubidaVoucherUtc
            },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmdPedido);

        const string sqlItem = """
            INSERT INTO pedidos_web_items (
                id, pedido_web_id, producto_id, cantidad_comercial, unidad_comercial,
                factor_conversion, cantidad_base_reservada, precio_unitario, subtotal
            ) VALUES (
                @Id, @PedidoWebId, @ProductoId, @CantidadComercial, @UnidadComercial,
                @FactorConversion, @CantidadBaseReservada, @PrecioUnitario, @Subtotal
            );
            """;

        foreach (var item in pedido.Items)
        {
            var itemId = Guid.NewGuid();
            decimal subtotal = Math.Round(item.Cantidad * item.PrecioUnitario, 2, MidpointRounding.AwayFromZero);
            var cmdItem = new CommandDefinition(
                sqlItem,
                new
                {
                    Id = itemId,
                    PedidoWebId = pedido.Id,
                    item.ProductoId,
                    CantidadComercial = item.Cantidad,
                    UnidadComercial = item.UnidadMedida,
                    item.FactorConversion,
                    CantidadBaseReservada = item.CantidadBaseEfectiva,
                    item.PrecioUnitario,
                    Subtotal = subtotal
                },
                tx,
                cancellationToken: cancellationToken);

            await conn.ExecuteAsync(cmdItem);
        }
    }

    public async Task ActualizarEstadoAsync(Guid pedidoId, string nuevoEstado, CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = "UPDATE pedidos_web SET estado = @nuevoEstado WHERE id = @pedidoId;";
        var cmd = new CommandDefinition(sql, new { nuevoEstado, pedidoId }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    public async Task<bool> ActualizarEstadoConcurrenteAsync(
        Guid pedidoId,
        string estadoEsperado,
        string nuevoEstado,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            UPDATE pedidos_web
            SET estado = @nuevoEstado
            WHERE id = @pedidoId AND estado = @estadoEsperado;
            """;

        var cmd = new CommandDefinition(sql, new { nuevoEstado, pedidoId, estadoEsperado }, tx, cancellationToken: cancellationToken);
        int rowsAffected = await conn.ExecuteAsync(cmd);
        return rowsAffected == 1;
    }

    public async Task AsignarGuiaRemisionAsync(
        Guid pedidoId,
        string numeroGuia,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            UPDATE pedidos_web
            SET guia_remision_numero = @numeroGuia
            WHERE id = @pedidoId;
            """;

        var cmd = new CommandDefinition(sql, new { numeroGuia, pedidoId }, tx, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    public async Task<IReadOnlyList<PedidoWebDto>> ObtenerPedidosExpiradosAsync(
        DateTime fechaReferenciaUtc,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT id
            FROM pedidos_web
            WHERE (
                    estado = 'PendientePago'
                    AND (
                        fecha_expiracion_utc <= @fechaReferenciaUtc
                        OR (fecha_limite_voucher_utc IS NOT NULL AND fecha_subida_voucher_utc IS NULL AND fecha_limite_voucher_utc <= @fechaReferenciaUtc)
                    )
                  )
               OR (
                    estado = 'PendienteValidacion'
                    AND fecha_expiracion_utc <= @fechaReferenciaUtc
                  )
            ORDER BY id ASC;
            """;

        var cmd = new CommandDefinition(sql, new { fechaReferenciaUtc }, tx, cancellationToken: cancellationToken);
        var ids = (await conn.QueryAsync<Guid>(cmd)).ToList();

        var result = new List<PedidoWebDto>();
        foreach (var id in ids)
        {
            var p = await QueryPedidoInternalAsync(id, forUpdate: false, cancellationToken);
            if (p != null) result.Add(p);
        }
        return result;
    }

    private async Task<PedidoWebDto?> QueryPedidoInternalAsync(Guid pedidoId, bool forUpdate, CancellationToken cancellationToken)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        string sqlPedido = $"""
            SELECT id AS Id, cliente_id AS ClienteId, modalidad_pago AS MetodoPago,
                   modalidad_entrega AS ModalidadEntrega, tienda_destino_id AS TiendaDestinoId,
                   almacen_origen_id AS AlmacenOrigenId, fecha_creacion_utc AS FechaCreacionUtc,
                   fecha_expiracion_utc AS FechaExpiracionUtc, fecha_limite_voucher_utc AS FechaLimiteVoucherUtc,
                   fecha_subida_voucher_utc AS FechaSubidaVoucherUtc, estado AS Estado
            FROM pedidos_web
            WHERE id = @pedidoId
            {(forUpdate ? "FOR UPDATE" : "")};
            """;

        var cmdPedido = new CommandDefinition(sqlPedido, new { pedidoId }, tx, cancellationToken: cancellationToken);
        var pedido = await conn.QuerySingleOrDefaultAsync<PedidoRow>(cmdPedido);
        if (pedido == null)
            return null;

        const string sqlItems = """
            SELECT producto_id AS ProductoId, cantidad_comercial AS Cantidad,
                   precio_unitario AS PrecioUnitario, unidad_comercial AS UnidadMedida,
                   factor_conversion AS FactorConversion, cantidad_base_reservada AS CantidadBase
            FROM pedidos_web_items
            WHERE pedido_web_id = @pedidoId;
            """;

        var cmdItems = new CommandDefinition(sqlItems, new { pedidoId }, tx, cancellationToken: cancellationToken);
        var items = (await conn.QueryAsync<PedidoItemRow>(cmdItems)).ToList();

        var itemDtos = items.Select(i => new PedidoWebItemDto(
            i.ProductoId, i.Cantidad, i.PrecioUnitario, i.UnidadMedida, i.FactorConversion, i.CantidadBase)).ToList();

        return new PedidoWebDto(
            pedido.Id, pedido.ClienteId, pedido.MetodoPago, pedido.ModalidadEntrega,
            pedido.TiendaDestinoId == Guid.Empty ? null : pedido.TiendaDestinoId,
            pedido.AlmacenOrigenId, pedido.FechaCreacionUtc, pedido.FechaExpiracionUtc,
            pedido.FechaLimiteVoucherUtc, pedido.FechaSubidaVoucherUtc, pedido.Estado,
            itemDtos);
    }

    private class PedidoRow
    {
        public Guid Id { get; set; }
        public Guid ClienteId { get; set; }
        public string MetodoPago { get; set; } = string.Empty;
        public string ModalidadEntrega { get; set; } = string.Empty;
        public Guid? TiendaDestinoId { get; set; }
        public Guid AlmacenOrigenId { get; set; }
        public DateTime FechaCreacionUtc { get; set; }
        public DateTime FechaExpiracionUtc { get; set; }
        public DateTime? FechaLimiteVoucherUtc { get; set; }
        public DateTime? FechaSubidaVoucherUtc { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    private class PedidoItemRow
    {
        public Guid ProductoId { get; set; }
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public string UnidadMedida { get; set; } = "UND";
        public decimal FactorConversion { get; set; }
        public decimal CantidadBase { get; set; }
    }
}
