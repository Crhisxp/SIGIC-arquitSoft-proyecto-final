using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.OrdersWeb.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.CashManagement;

/// <summary>
/// Adaptador para emisión y persistencia transaccional de guías internas de traslado (RF-INV-003, RF-POS-011).
/// Persiste cabecera en guias_traslado_interno, detalle de ítems en guias_traslado_items y canaliza la vinculación
/// en pedidos_web mediante el contrato público de IWebOrderRepository dentro de la misma transacción física.
/// </summary>
public class SystemTransferGuidePort : ITransferGuidePort
{
    private readonly SigicDbContext _dbContext;
    private readonly IWebOrderRepository _webOrderRepository;

    public SystemTransferGuidePort(SigicDbContext dbContext, IWebOrderRepository webOrderRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _webOrderRepository = webOrderRepository ?? throw new ArgumentNullException(nameof(webOrderRepository));
    }

    public async Task<string> GenerarGuiaTrasladoAsync(
        Guid pedidoWebId,
        Guid almacenOrigenId,
        Guid tiendaDestinoId,
        IReadOnlyList<(Guid ProductoId, decimal Cantidad)> items,
        CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("La guía de traslado debe contener al menos un ítem.", nameof(items));

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        // 1. Idempotencia: Verificar si ya existe una guía emitida para este pedido
        const string sqlCheck = "SELECT numero_guia FROM guias_traslado_interno WHERE pedido_web_id = @pedidoWebId LIMIT 1;";
        var cmdCheck = new CommandDefinition(sqlCheck, new { pedidoWebId }, tx, cancellationToken: cancellationToken);
        string? guiaExistente = await conn.QuerySingleOrDefaultAsync<string>(cmdCheck);

        if (!string.IsNullOrWhiteSpace(guiaExistente))
            return guiaExistente;

        // 2. Generar número formal de guía interna
        string shortId = pedidoWebId.ToString()[..8].ToUpperInvariant();
        string numeroGuia = $"T001-{DateTime.UtcNow:yyyyMMdd}-{shortId}";
        var guiaId = Guid.NewGuid();
        DateTime nowUtc = DateTime.UtcNow;

        // 3. Persistir cabecera de la guía de traslado interno
        const string sqlInsertGuia = """
            INSERT INTO guias_traslado_interno (
                id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc
            ) VALUES (
                @guiaId, @numeroGuia, @pedidoWebId, @almacenOrigenId, @tiendaDestinoId, 'EMITIDA', @nowUtc
            );
            """;

        var cmdGuia = new CommandDefinition(
            sqlInsertGuia,
            new { guiaId, numeroGuia, pedidoWebId, almacenOrigenId, tiendaDestinoId, nowUtc },
            tx,
            cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmdGuia);

        // 4. Persistir líneas de la guía de traslado
        const string sqlInsertItem = """
            INSERT INTO guias_traslado_items (
                id, guia_traslado_id, producto_id, cantidad
            ) VALUES (
                @id, @guiaId, @productoId, @cantidad
            );
            """;

        foreach (var (productoId, cantidad) in items)
        {
            var cmdItem = new CommandDefinition(
                sqlInsertItem,
                new { id = Guid.NewGuid(), guiaId, productoId, cantidad },
                tx,
                cancellationToken: cancellationToken);
            await conn.ExecuteAsync(cmdItem);
        }

        // 5. Vincular número de guía a través del contrato público del módulo OrdersWeb
        await _webOrderRepository.AsignarGuiaRemisionAsync(pedidoWebId, numeroGuia, cancellationToken);

        return numeroGuia;
    }

    public async Task<GuiaTrasladoDetalleDto?> ObtenerGuiaPorIdAsync(
        Guid guiaId,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sqlGuia = """
            SELECT id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc
            FROM guias_traslado_interno
            WHERE id = @guiaId;
            """;

        var cmdGuia = new CommandDefinition(sqlGuia, new { guiaId }, tx, cancellationToken: cancellationToken);
        var guia = await conn.QuerySingleOrDefaultAsync<dynamic>(cmdGuia);
        if (guia == null) return null;

        const string sqlItems = """
            SELECT producto_id, cantidad
            FROM guias_traslado_items
            WHERE guia_traslado_id = @guiaId;
            """;

        var cmdItems = new CommandDefinition(sqlItems, new { guiaId }, tx, cancellationToken: cancellationToken);
        var itemsRows = await conn.QueryAsync<dynamic>(cmdItems);
        var items = itemsRows.Select(i => new GuiaTrasladoItemDto((Guid)i.producto_id, Convert.ToDecimal(i.cantidad))).ToList();

        return new GuiaTrasladoDetalleDto(
            (Guid)guia.id, (string)guia.numero_guia, (Guid)guia.pedido_web_id,
            (Guid)guia.almacen_origen_id, (Guid)guia.tienda_destino_id, (string)guia.estado,
            (DateTime)guia.fecha_emision_utc, items);
    }
}
