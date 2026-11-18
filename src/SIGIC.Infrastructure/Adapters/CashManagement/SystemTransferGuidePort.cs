using System.Text.RegularExpressions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.CashManagement.Ports;
using SIGIC.Application.Common.Exceptions;
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
    private readonly ILogger<SystemTransferGuidePort>? _logger;

    public SystemTransferGuidePort(
        SigicDbContext dbContext,
        IWebOrderRepository webOrderRepository,
        ILogger<SystemTransferGuidePort>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _webOrderRepository = webOrderRepository ?? throw new ArgumentNullException(nameof(webOrderRepository));
        _logger = logger;
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

    public Task<GuiaTrasladoDetalleDto?> ObtenerGuiaPorIdAsync(
        Guid guiaId,
        CancellationToken cancellationToken = default)
    {
        return ObtenerGuiaPorIdOCodigoAsync(guiaId.ToString(), cancellationToken);
    }

    public async Task<GuiaTrasladoDetalleDto?> ObtenerGuiaPorIdOCodigoAsync(
        string idOrCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrCode)) return null;

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        bool isGuid = Guid.TryParse(idOrCode.Trim(), out var guidVal);
        string sqlGuia = isGuid
            ? "SELECT id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc FROM guias_traslado_interno WHERE (id = @guidVal OR numero_guia = @idOrCode) LIMIT 1;"
            : "SELECT id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc FROM guias_traslado_interno WHERE numero_guia = @idOrCode LIMIT 1;";

        var cmdGuia = new CommandDefinition(sqlGuia, new { guidVal, idOrCode = idOrCode.Trim() }, tx, cancellationToken: cancellationToken);
        var guia = await conn.QuerySingleOrDefaultAsync<dynamic>(cmdGuia);
        if (guia == null) return null;

        Guid guiaId = (Guid)guia.id;
        const string sqlItems = "SELECT producto_id, cantidad FROM guias_traslado_items WHERE guia_traslado_id = @guiaId;";
        var cmdItems = new CommandDefinition(sqlItems, new { guiaId }, tx, cancellationToken: cancellationToken);
        var itemsRows = await conn.QueryAsync<dynamic>(cmdItems);
        var items = itemsRows.Select(i => new GuiaTrasladoItemDto((Guid)i.producto_id, Convert.ToDecimal(i.cantidad))).ToList();

        return new GuiaTrasladoDetalleDto(
            guiaId, (string)guia.numero_guia, (Guid)guia.pedido_web_id,
            (Guid)guia.almacen_origen_id, (Guid)guia.tienda_destino_id, (string)guia.estado,
            (DateTime)guia.fecha_emision_utc, items);
    }

    public async Task<GuiaEntregaResultadoDto> EntregarGuiaAsync(
        string idOrCode,
        string dniReceptor,
        string? codigoRetiro,
        Guid operadorId,
        CancellationToken cancellationToken = default)
    {
        string dniLimpio = (dniReceptor ?? "").Trim();
        if (!Regex.IsMatch(dniLimpio, @"^\d{8}$"))
            throw new ValidationException("El DNI del receptor debe contener exactamente 8 dígitos numéricos.");

        string idLimpio = (idOrCode ?? "").Trim();
        bool isGuid = Guid.TryParse(idLimpio, out var guidVal);
        Guid parsedId = isGuid ? guidVal : Guid.Empty;

        bool ownsTransaction = false;
        var transaction = _dbContext.Database.CurrentTransaction;
        if (transaction == null)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            ownsTransaction = true;
        }

        try
        {
            var conn = _dbContext.Database.GetDbConnection();
            var tx = transaction.GetDbTransaction();

            const string sqlGuia = """
                SELECT id, numero_guia, pedido_web_id, almacen_origen_id, tienda_destino_id, estado, fecha_emision_utc
                FROM guias_traslado_interno
                WHERE (id = @parsedId OR numero_guia = @idLimpio)
                LIMIT 1;
                """;
            var cmdGuia = new CommandDefinition(sqlGuia, new { parsedId, idLimpio }, tx, cancellationToken: cancellationToken);
            var guia = await conn.QuerySingleOrDefaultAsync<dynamic>(cmdGuia);
            if (guia == null)
                throw new NotFoundException("GuiaTrasladoInterno", idLimpio);

            const string sqlUpdate = """
                UPDATE guias_traslado_interno
                SET estado = 'ENTREGADO'
                WHERE (id = @parsedId OR numero_guia = @idLimpio) AND estado = 'Listo para Retiro';
                """;
            var cmdUpdate = new CommandDefinition(sqlUpdate, new { parsedId, idLimpio }, tx, cancellationToken: cancellationToken);
            int filasAfectadas = await conn.ExecuteAsync(cmdUpdate);

            if (filasAfectadas == 0)
            {
                throw new InvalidOperationException("La guía no existe o ya no está en estado Listo para Retiro.");
            }

            Guid guiaId = (Guid)guia.id;
            string numeroGuia = (string)guia.numero_guia;
            Guid pedidoWebId = (Guid)guia.pedido_web_id;

            await _webOrderRepository.ActualizarEstadoAsync(pedidoWebId, "ENTREGADO", cancellationToken);

            DateTime nowUtc = DateTime.UtcNow;

            _logger?.LogInformation(
                "Auditoría Despacho Web: Guía {NumeroGuia} (Pedido {PedidoId}) ENTREGADA a DNI {DniReceptor} por Operador {OperadorId} a las {FechaUtc}",
                numeroGuia, pedidoWebId, dniLimpio, operadorId, nowUtc);

            if (ownsTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new GuiaEntregaResultadoDto(
                guiaId,
                numeroGuia,
                pedidoWebId,
                "ENTREGADO",
                dniLimpio,
                nowUtc,
                $"Despacho en tienda completado exitosamente para retiro {codigoRetiro ?? numeroGuia}. DNI Receptor: {dniLimpio}.");
        }
        catch
        {
            if (ownsTransaction)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }
}
