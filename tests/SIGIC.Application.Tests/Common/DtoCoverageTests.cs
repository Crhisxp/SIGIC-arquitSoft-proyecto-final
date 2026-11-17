using SIGIC.Application.CashManagement.Commands;
using SIGIC.Application.Inventory.Commands;
using SIGIC.Application.OrdersWeb.Commands;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.SalesPOS.Commands;

namespace SIGIC.Application.Tests.Common;

public class DtoCoverageTests
{
    [Fact]
    public void Debe_CubrirTodasLasPropiedades_DeDtosYComandos()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // CashDtos
        var arqDen = new ArqueoDenominacionDto(100m, 5);
        Assert.Equal(100m, arqDen.ValorUnitario);
        Assert.Equal(5, arqDen.Cantidad);

        var arqRes = new ArqueoResultDto(id, id, 500m, now, "Turno cerrado");
        Assert.Equal(id, arqRes.ArqueoId);
        Assert.Equal(id, arqRes.TurnoCajaId);
        Assert.Equal(500m, arqRes.TotalDeclarado);
        Assert.Equal(now, arqRes.FechaCierreUtc);
        Assert.Equal("Turno cerrado", arqRes.Mensaje);

        var arqAudit = new ArqueoAuditoriaDetalleDto(id, id, 500m, 500m, 0m, false, now);
        Assert.Equal(id, arqAudit.ArqueoId);
        Assert.Equal(id, arqAudit.TurnoCajaId);
        Assert.Equal(500m, arqAudit.TotalDeclarado);
        Assert.Equal(500m, arqAudit.SaldoTeorico);
        Assert.Equal(0m, arqAudit.Diferencia);
        Assert.False(arqAudit.TieneDiscrepancia);
        Assert.Equal(now, arqAudit.FechaCierreUtc);

        var vchRes = new VoucherValidacionResultDto(id, true, "PagadoConfirmado", "GUIA-1");
        Assert.Equal(id, vchRes.PedidoWebId);
        Assert.True(vchRes.Aprobado);
        Assert.Equal("PagadoConfirmado", vchRes.EstadoFinal);
        Assert.Equal("GUIA-1", vchRes.GuiaTrasladoCodigo);

        var vchMeta = new VoucherMetadataDto("archivo.pdf", "application/pdf", 1024, now);
        Assert.Equal("archivo.pdf", vchMeta.NombreArchivo);
        Assert.Equal("application/pdf", vchMeta.ContentType);
        Assert.Equal(1024, vchMeta.TamanoBytes);
        Assert.Equal(now, vchMeta.FechaSubidaUtc);

        // InventoryDtos
        var invRes = new AjustarInventarioResultDto(id, id, 10m, 5m, 15m, 0m, now);
        Assert.Equal(id, invRes.AjusteId);
        Assert.Equal(id, invRes.ProductoId);
        Assert.Equal(10m, invRes.StockFisicoAnterior);
        Assert.Equal(5m, invRes.CantidadAjuste);
        Assert.Equal(15m, invRes.StockFisicoNuevo);
        Assert.Equal(0m, invRes.StockComprometido);
        Assert.Equal(now, invRes.FechaUtc);

        // WebOrderDtos
        var itemDto = new PedidoWebItemDto(id, 2m, 25m);
        Assert.Equal(id, itemDto.ProductoId);
        Assert.Equal(2m, itemDto.Cantidad);
        Assert.Equal(25m, itemDto.PrecioUnitario);

        var webOrder = new PedidoWebDto(id, id, "Yape", "RetiroEnTienda", id, id, now, now.AddHours(4), now.AddHours(1), now.AddMinutes(30), "PendientePago", new[] { itemDto });
        Assert.Equal(id, webOrder.Id);
        Assert.Equal(id, webOrder.ClienteId);
        Assert.Equal("Yape", webOrder.MetodoPago);
        Assert.Equal("RetiroEnTienda", webOrder.ModalidadEntrega);
        Assert.Equal(id, webOrder.TiendaDestinoId);
        Assert.Equal(id, webOrder.AlmacenOrigenId);
        Assert.Equal(now, webOrder.FechaCreacionUtc);
        Assert.Equal(now.AddHours(4), webOrder.FechaExpiracionUtc);
        Assert.Equal(now.AddHours(1), webOrder.FechaLimiteVoucherUtc);
        Assert.Equal(now.AddMinutes(30), webOrder.FechaSubidaVoucherUtc);
        Assert.Equal("PendientePago", webOrder.Estado);
        Assert.Single(webOrder.Items);

        var resWeb = new ReservarPedidoWebResultDto(id, now, now.AddMinutes(20), null, 50m, "PendientePago");
        Assert.Equal(id, resWeb.PedidoId);
        Assert.Equal(now, resWeb.FechaCreacionUtc);
        Assert.Equal(now.AddMinutes(20), resWeb.FechaExpiracionUtc);
        Assert.Null(resWeb.FechaLimiteVoucherUtc);
        Assert.Equal(50m, resWeb.Total);
        Assert.Equal("PendientePago", resWeb.Estado);

        // SalesPOS
        var posItem = new VentaPOSItemDto(id, 3m, "KG", 15m);
        Assert.Equal(id, posItem.ProductoId);
        Assert.Equal(3m, posItem.Cantidad);
        Assert.Equal("KG", posItem.UnidadMedida);
        Assert.Equal(15m, posItem.PrecioUnitario);

        var posPago = new VentaPOSPagoDto("Tarjeta", 45m, "AUTH-99");
        Assert.Equal("Tarjeta", posPago.MetodoPago);
        Assert.Equal(45m, posPago.Monto);
        Assert.Equal("AUTH-99", posPago.CodigoOperacionDigital);

        var posRes = new VentaPOSResultDto(id, "B001-1", 45m, now);
        Assert.Equal(id, posRes.VentaId);
        Assert.Equal("B001-1", posRes.NumeroComprobante);
        Assert.Equal(45m, posRes.TotalVenta);
        Assert.Equal(now, posRes.FechaEmisionUtc);

        var posCmd = new RegistrarVentaPOSCommand(id, id, new[] { posItem }, new[] { posPago }, "Factura", "Observacion prueba");
        Assert.Equal(id, posCmd.TurnoCajaId);
        Assert.Equal(id, posCmd.ClienteId);
        Assert.Single(posCmd.Items);
        Assert.Single(posCmd.Pagos);
        Assert.Equal("Factura", posCmd.TipoComprobante);
        Assert.Equal("Observacion prueba", posCmd.Observaciones);

        // ProductDetailsPricing
        var pdpQuery = new CalcularPDPYDescuentoQuery(id, "Maestro", 10m, "BOLSA", true);
        Assert.Equal(id, pdpQuery.ProductoId);
        Assert.Equal("Maestro", pdpQuery.RolCliente);
        Assert.Equal(10m, pdpQuery.Cantidad);
        Assert.Equal("BOLSA", pdpQuery.UnidadComercial);
        Assert.True(pdpQuery.EsCanalWeb);

        var pdpRes = new PDPResultDto(id, "SKU", "Prod", "UND", 10m, 50m, 45m, 5m, 0.10m, 450m, 20m, true);
        Assert.Equal(id, pdpRes.ProductoId);
        Assert.Equal("SKU", pdpRes.CodigoSku);
        Assert.Equal("Prod", pdpRes.NombreComercial);
        Assert.Equal("UND", pdpRes.UnidadBase);
        Assert.Equal(10m, pdpRes.CantidadBaseConvertida);
        Assert.Equal(50m, pdpRes.PrecioBaseUnitario);
        Assert.Equal(45m, pdpRes.PrecioFinalUnitario);
        Assert.Equal(5m, pdpRes.DescuentoUnitario);
        Assert.Equal(0.10m, pdpRes.PorcentajeDescuentoTotal);
        Assert.Equal(450m, pdpRes.Subtotal);
        Assert.Equal(20m, pdpRes.StockDisponible);
        Assert.True(pdpRes.AplicaDescuentoVolumen);
    }
}
