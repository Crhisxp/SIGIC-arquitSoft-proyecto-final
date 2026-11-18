using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.SalesPOS.Commands;
using SIGIC.Application.SalesPOS.Ports;
using SIGIC.Application.Security.Ports;
using SIGIC.Infrastructure.Persistence;

namespace SIGIC.Infrastructure.Adapters.SalesPOS;

/// <summary>
/// Adaptador de persistencia para ventas en Mostrador POS (RF-POS-001, RF-POS-004, RF-POS-006).
/// Persiste comprobante en ventas_pos, desglose en ventas_pos_items y todos los medios en ventas_pos_pagos.
/// </summary>
public class PostgresSalesPOSRepository : ISalesPOSRepository
{
    private readonly SigicDbContext _dbContext;
    private readonly ICashShiftPort? _cashShiftPort;
    private readonly ICurrentUserContext? _currentUserContext;

    public PostgresSalesPOSRepository(
        SigicDbContext dbContext,
        ICashShiftPort? cashShiftPort = null,
        ICurrentUserContext? currentUserContext = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cashShiftPort = cashShiftPort;
        _currentUserContext = currentUserContext;
    }

    public async Task<VentaPOSResultDto> RegistrarVentaAsync(
        RegistrarVentaPOSCommand command,
        decimal totalVenta,
        DateTime fechaUtc,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var ventaId = Guid.NewGuid();
        string numeroComprobante = $"B001-{fechaUtc:yyyyMMdd}-{ventaId.ToString()[..8].ToUpperInvariant()}";

        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        // Obtener almacén asignado al turno mediante el puerto público ICashShiftPort y cajero del contexto
        var almacenId = _cashShiftPort != null
            ? await _cashShiftPort.ObtenerAlmacenTiendaIdAsync(command.TurnoCajaId, cancellationToken)
            : Guid.Empty;
        var cajeroId = _currentUserContext?.UserId ?? Guid.Empty;

        string metodoPrincipal = command.Pagos.Count > 1
            ? "MIXTO"
            : (command.Pagos.FirstOrDefault()?.MetodoPago ?? "EFECTIVO");

        string? codigoDigital = command.Pagos.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.CodigoOperacionDigital))?.CodigoOperacionDigital;

        const string sqlVenta = """
            INSERT INTO ventas_pos (
                id, turno_caja_id, cajero_id, almacen_id, total, fecha_utc,
                metodo_pago, codigo_operacion_digital, cliente_id
            ) VALUES (
                @ventaId, @TurnoCajaId, @cajeroId, @almacenId, @totalVenta, @fechaUtc,
                @metodoPrincipal, @codigoDigital, @ClienteId
            );
            """;

        var cmdVenta = new CommandDefinition(
            sqlVenta,
            new
            {
                ventaId,
                command.TurnoCajaId,
                cajeroId,
                almacenId,
                totalVenta,
                fechaUtc,
                metodoPrincipal,
                codigoDigital,
                command.ClienteId
            },
            tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmdVenta);

        // Persistir todos los métodos y montos individuales de pago (efectivo, digital y mixtos)
        const string sqlPago = """
            INSERT INTO ventas_pos_pagos (
                id, venta_pos_id, metodo_pago, monto, codigo_operacion_digital
            ) VALUES (
                @pagoId, @ventaId, @metodoPago, @monto, @codigoDigital
            );
            """;

        foreach (var pago in command.Pagos)
        {
            var pagoId = Guid.NewGuid();
            var cmdPago = new CommandDefinition(
                sqlPago,
                new { pagoId, ventaId, metodoPago = pago.MetodoPago, monto = pago.Monto, codigoDigital = pago.CodigoOperacionDigital },
                tx,
                cancellationToken: cancellationToken);
            await conn.ExecuteAsync(cmdPago);
        }

        const string sqlItem = """
            INSERT INTO ventas_pos_items (
                id, venta_pos_id, producto_id, cantidad, precio_unitario, subtotal
            ) VALUES (
                @itemId, @ventaId, @ProductoId, @Cantidad, @PrecioUnitario, @subtotal
            );
            """;

        foreach (var item in command.Items)
        {
            var itemId = Guid.NewGuid();
            decimal subtotal = Math.Round(item.Cantidad * item.PrecioUnitario, 2, MidpointRounding.AwayFromZero);

            var cmdItem = new CommandDefinition(
                sqlItem,
                new { itemId, ventaId, item.ProductoId, item.Cantidad, item.PrecioUnitario, subtotal },
                tx,
                cancellationToken: cancellationToken);

            await conn.ExecuteAsync(cmdItem);
        }

        return new VentaPOSResultDto(
            ventaId,
            numeroComprobante,
            totalVenta,
            fechaUtc);
    }

    public async Task<decimal> ObtenerTotalRecaudadoTurnoAsync(
        Guid turnoCajaId,
        CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var tx = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        const string sql = """
            SELECT COALESCE(SUM(COALESCE(vpp.monto, vp.total)), 0)
            FROM ventas_pos vp
            LEFT JOIN ventas_pos_pagos vpp ON vpp.venta_pos_id = vp.id
            WHERE vp.turno_caja_id = @turnoCajaId;
            """;

        var cmd = new CommandDefinition(sql, new { turnoCajaId }, tx, cancellationToken: cancellationToken);
        var result = await conn.ExecuteScalarAsync<decimal?>(cmd);
        return result ?? 0m;
    }
}
