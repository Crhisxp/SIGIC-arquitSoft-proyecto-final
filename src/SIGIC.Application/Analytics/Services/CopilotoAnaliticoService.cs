using System.Globalization;
using System.Text;
using SIGIC.Application.Analytics.Dtos;
using SIGIC.Application.Analytics.Ports;
using SIGIC.Application.Common.Exceptions;
using SIGIC.Application.Common.Interfaces;
using SIGIC.Application.Security.Ports;
using SIGIC.Domain.Analytics;

namespace SIGIC.Application.Analytics.Services;

public sealed class CopilotoAnaliticoService(
    IAuthorizationService autorizacion, ILecturaAnaliticaPort lectura,
    IAuxiliarInteligenciaAnaliticaPort asistente, IDateTimeProvider reloj) : ICopilotoAnaliticoService
{
    // Solo las intenciones de este catálogo atraviesan la frontera de aplicación.
    // Las cifras, fechas y dimensiones nunca proceden de texto generado.
    private static readonly (MetricaAnalitica Metrica, string[] Palabras)[] Catalogo =
    [
        (MetricaAnalitica.VentasYTicketMedio, ["venta", "ventas", "tickets", "ticket", "facturacion", "vendimos"]),
        (MetricaAnalitica.Rotacion, ["rotacion", "roto"]),
        (MetricaAnalitica.QuiebresStock, ["quiebre", "quiebres", "agotado", "agotados", "agotadas", "faltantes"]),
        (MetricaAnalitica.Margen, ["margen", "margenes", "utilidad", "utilidades", "rentabilidad"])
    ];
    private static readonly HashSet<string> Vocabulario = new(
        ("a al cual cuales cuanto cuanta cuantos cuantas que como el la los las de del en por para " +
         "fue fueron es son estan hay hubo han sido se ha tiene tenemos nuestro nuestra nuestros nuestras " +
         "muestra muestrame dime ver dame consulta consultar calcula calcular compara comparar quiero saber " +
         "y un una total totales consolidado consolidada promedio medio importe inventario existencias " +
         "stock sin posiciones almacen almacenes articulo articulos producto productos unidades operativo " +
         "operativa operativos operativas del periodo seleccionado este rango canal canales pos web mostrador portal publico general").Split(' ')
        .Concat(Catalogo.SelectMany(c => c.Palabras)), StringComparer.Ordinal);

    public async Task<ConsultaAnaliticaResponseDto> ConsultarAsync(
        ConsultaAnaliticaRequestDto request, CancellationToken cancellationToken = default)
    {
        autorizacion.ExigirUnoDeRoles("Superadmin", "Tesoreria", "Almacenero");
        if (request.Desde is not { } desde || request.HastaExclusivo is not { } hasta)
            throw new ValidationException("Indique las dos fechas del periodo.");

        var periodo = new PeriodoAnalitico(desde, hasta);
        if (string.IsNullOrWhiteSpace(request.Pregunta) || request.Pregunta.Length > 500)
            throw new ValidationException("La pregunta debe contener entre 1 y 500 caracteres.");

        var corte = reloj.UtcNow;
        var metrica = Resolver(request.Pregunta, out var mencionados);
        var canales = request.Canales ?? (mencionados.Length == 0 ? ["POS", "WEB"] : mencionados);
        if (canales.Length is < 1 or > 2 || canales.Any(c => c is not ("POS" or "WEB")) ||
            canales.Distinct().Count() != canales.Length)
            throw new ValidationException("Canales admite POS, WEB o ambos.");
        if (mencionados.Except(canales).Any() || (canales.Length == 1 && metrica != MetricaAnalitica.VentasYTicketMedio))
            metrica = MetricaAnalitica.Desconocida;
        switch (metrica)
        {
            case MetricaAnalitica.VentasYTicketMedio:
                var ventas = await lectura.ObtenerVentasYTicketsPorCanalAsync(periodo, canales, cancellationToken);
                return await VentasAsync(ventas, request.Pregunta.Contains("ticket", StringComparison.OrdinalIgnoreCase), corte, cancellationToken);
            case MetricaAnalitica.QuiebresStock:
                var stock = await lectura.ObtenerQuiebresStockAsync(cancellationToken);
                return await ResponderAsync("quiebres_stock", stock,
                    "Disponibilidad actual por almacén al corte de la consulta.", corte, cancellationToken);
            case MetricaAnalitica.Margen:
            case MetricaAnalitica.Rotacion:
                var cierre = await lectura.ObtenerCierreAsync(periodo, cancellationToken);
                string nombre = metrica == MetricaAnalitica.Margen ? "margen_operativo" : "rotacion";
                if (cierre.DiasCerrados != periodo.TotalDias)
                    return new("datos_incompletos", nombre, [],
                        $"Contabilidad ha publicado {cierre.DiasCerrados} de los {periodo.TotalDias} cierres diarios del periodo.", corte);
                var hechos = Indicadores(cierre, metrica);
                string fuente = "Fuente: cierres contables diarios en PEN.";
                if (metrica == MetricaAnalitica.Margen && cierre.MargenOperativo is null)
                    fuente += " Los ingresos netos son cero y el porcentaje de margen es indefinido.";
                if (metrica == MetricaAnalitica.Rotacion && cierre.Rotacion is null)
                    fuente += " El inventario medio es cero y la rotación es indefinida.";
                return await ResponderAsync(nombre, hechos, fuente, corte, cancellationToken);
            default:
                return new("requiere_aclaracion", "desconocida", [],
                    "Elija una consulta de ventas, rotación, quiebres de stock o margen operativo. Use las fechas del formulario.", corte);
        }
    }

    private static MetricaAnalitica Resolver(string pregunta, out string[] canales)
    {
        canales = [];
        string normal = string.Concat(pregunta.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
        if (!normal.All(c => char.IsLetter(c) || char.IsWhiteSpace(c) || "¿?¡!.,".Contains(c)))
            return MetricaAnalitica.Desconocida;
        var palabras = normal.Split([' ', '\t', '\n', '\r', '¿', '?', '¡', '!', '.', ','], StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Any(p => !Vocabulario.Contains(p))) return MetricaAnalitica.Desconocida;
        var mencionados = new List<string>();
        if (palabras.Contains("pos") || palabras.Contains("mostrador")) mencionados.Add("POS");
        if (palabras.Contains("web") || palabras.Contains("portal")) mencionados.Add("WEB");
        canales = mencionados.ToArray();
        var coincidencias = Catalogo.Where(c => palabras.Intersect(c.Palabras).Any() ||
            (c.Metrica == MetricaAnalitica.QuiebresStock && palabras.Contains("sin") &&
             (palabras.Contains("stock") || palabras.Contains("existencias")))).ToArray();
        return coincidencias.Length == 1 ? coincidencias[0].Metrica : MetricaAnalitica.Desconocida;
    }

    private static List<HechoDto> Indicadores(CierreOperativo cierre, MetricaAnalitica metrica)
    {
        var hechos = new List<HechoDto>();
        if (metrica == MetricaAnalitica.Rotacion)
        {
            if (cierre.Rotacion is { } rotacion) hechos.Add(Hecho("Rotación de inventario", rotacion, "veces"));
            hechos.Add(Hecho("Costo de ventas", cierre.CostoVentas, "PEN"));
            hechos.Add(Hecho("Inventario medio al costo", cierre.InventarioMedioAlCosto, "PEN"));
        }
        else
        {
            if (cierre.MargenOperativo is { } margen) hechos.Add(Hecho("Margen operativo", margen, "%"));
            hechos.Add(Hecho("Utilidad operativa", cierre.UtilidadOperativa, "PEN"));
            hechos.Add(Hecho("Ingresos netos", cierre.IngresosNetos, "PEN"));
            hechos.Add(Hecho("Costo de ventas", cierre.CostoVentas, "PEN"));
            hechos.Add(Hecho("Gastos operativos", cierre.GastosOperativos, "PEN"));
        }
        return hechos;
    }

    private Task<ConsultaAnaliticaResponseDto> VentasAsync(IReadOnlyList<CanalVentaResumenDto> filas, bool pideTicket,
        DateTime corte, CancellationToken ct)
    {
        var hechos = new List<HechoDto>();
        if (filas.Count > 0)
        {
            decimal total = filas.Sum(x => x.TotalVentas);
            var promedio = Hecho("Ticket promedio consolidado", total / filas.Sum(x => x.Tickets), "PEN");
            var ventas = Hecho("Ventas consolidadas", total, "PEN");
            hechos.Add(pideTicket ? promedio : ventas);
            hechos.Add(pideTicket ? ventas : promedio);
        }
        foreach (var fila in filas)
        {
            hechos.Add(Hecho($"Ventas {fila.Canal}", fila.TotalVentas, "PEN"));
            hechos.Add(Hecho($"Tickets {fila.Canal}", fila.Tickets, "tickets"));
            hechos.Add(Hecho($"Ticket promedio {fila.Canal}", fila.TotalVentas / fila.Tickets, "PEN"));
        }

        return ResponderAsync("ventas_y_ticket_medio", hechos, "Ventas POS y pedidos web con salida por pago confirmado.", corte, ct);
    }

    private static HechoDto Hecho(string etiqueta, decimal valor, string unidad) =>
        new(etiqueta, Math.Round(valor, 2, MidpointRounding.AwayFromZero)
            .ToString(unidad == "tickets" ? "0" : "F2", CultureInfo.InvariantCulture), unidad);

    private async Task<ConsultaAnaliticaResponseDto> ResponderAsync(string metrica,
        IReadOnlyList<HechoDto> hechos, string fuente, DateTime corte, CancellationToken ct)
    {
        if (hechos.Count == 0) return new("sin_datos", metrica, [], "Sin observaciones. " + fuente, corte);
        var primero = hechos[0];
        string plantilla = await asistente.ElegirPlantillaAsync(metrica, hechos.Select(h => h.Etiqueta).ToArray(), ct);
        string dato = $"{primero.Etiqueta}: {primero.Valor} {primero.Unidad}.";
        string resumen = dato;
        if (plantilla == "con_desglose" && hechos.Count > 1)
            resumen += $" {hechos[1].Etiqueta}: {hechos[1].Valor} {hechos[1].Unidad}.";
        resumen += " " + fuente;
        return new("con_datos", metrica, hechos, resumen, corte);
    }
}
