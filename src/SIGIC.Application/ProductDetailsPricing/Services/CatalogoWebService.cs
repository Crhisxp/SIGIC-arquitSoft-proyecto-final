using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Ports;
using SIGIC.Application.Inventory.Ports;
using SIGIC.Application.ProductDetailsPricing.Commands;
using SIGIC.Application.ProductDetailsPricing.Ports;

namespace SIGIC.Application.ProductDetailsPricing.Services;

public sealed class CatalogoWebService(
    IProductQueryPort productos,
    ICustomerProfilePort perfiles,
    ITariffRepository tarifas,
    IWarehouseConfigurationPort almacenes,
    IInventoryStockPort inventario)
{
    public async Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? busqueda, Guid? almacenId, Guid? clienteId, CancellationToken ct)
    {
        var filas = await productos.ListarCatalogoWebAsync(busqueda, almacenId ?? almacenes.ObtenerAlmacenCentralId(), ct);
        var descuento = await ObtenerDescuentoAsync(clienteId, ct);
        return filas.Select(fila => AplicarTarifa(fila.Producto, fila.CostoUnitario, descuento)).ToArray();
    }

    public async Task<ProductoCatalogoDto> ObtenerAsync(Guid id, Guid? clienteId, CancellationToken ct)
    {
        var producto = await productos.ObtenerParaPDPAsync(id, ct);
        if (producto is null || !producto.DisponibleEnWeb)
            throw new NotFoundException("Producto", id);

        var saldo = await inventario.ObtenerStockAlmacenAsync(almacenes.ObtenerAlmacenCentralId(), id, ct);
        var dto = new ProductoCatalogoDto(producto.Id, producto.CodigoSku, producto.NombreComercial,
            producto.UnidadBase, producto.PrecioVenta, Math.Max(0m, saldo?.StockDisponible ?? producto.StockDisponible),
            producto.DisponibleEnWeb, producto.ImagenUrl);
        var descuento = await ObtenerDescuentoAsync(clienteId, ct);
        return AplicarTarifa(dto, producto.CostoUnitario, descuento);
    }

    private async Task<decimal> ObtenerDescuentoAsync(Guid? clienteId, CancellationToken ct)
    {
        if (clienteId is null)
            return 0m;

        var perfil = await perfiles.ObtenerPerfilClienteAsync(clienteId, ct);
        return await tarifas.ObtenerPorcentajeDescuentoRolAsync(perfil, ct);
    }

    private static ProductoCatalogoDto AplicarTarifa(ProductoCatalogoDto producto, decimal costo, decimal descuento)
    {
        if (descuento == 0m)
            return producto;

        var precioBase = Math.Round(producto.PrecioVenta, 2, MidpointRounding.ToEven);
        var precioTarifa = precioBase - Math.Round(precioBase * descuento, 2, MidpointRounding.ToEven);
        return precioTarifa >= costo ? producto with { PrecioVenta = precioTarifa } : producto;
    }
}
