namespace SIGIC.Domain.Entities;

/// <summary>
/// Representa el desglose físico de una denominación monetaria (billetes o monedas) en el arqueo de caja.
/// </summary>
public record ArqueoDenominacion
{
    public decimal ValorUnitario { get; init; }
    public int Cantidad { get; init; }
    public decimal Subtotal => ValorUnitario * Cantidad;

    public ArqueoDenominacion(decimal valorUnitario, int cantidad)
    {
        if (valorUnitario <= 0)
            throw new ArgumentOutOfRangeException(nameof(valorUnitario), "El valor unitario de la denominación debe ser mayor a cero.");

        if (cantidad < 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad de billetes/monedas no puede ser negativa.");

        ValorUnitario = valorUnitario;
        Cantidad = cantidad;
    }
}

/// <summary>
/// Entidad inmutable de Arqueo Ciego de Caja en SIGIC Ferretería v2.2 (RF-CAJ-002, AC03, RNF-SEG-004).
/// Estructura Append-Only: una vez registrado, sus valores no pueden ser alterados ni eliminados.
/// Utiliza precisión decimal exacta en todas las operaciones monetarias para evitar errores de redondeo de punto flotante.
/// </summary>
public class ArqueoCaja
{
    public Guid Id { get; }
    public Guid TurnoCajaId { get; }
    public Guid CajeroId { get; }
    public DateTime FechaAperturaUtc { get; }
    public DateTime FechaCierreUtc { get; }
    public decimal FondoInicialSencillo { get; }
    public IReadOnlyList<ArqueoDenominacion> DesgloseFisico { get; }
    public decimal TotalEfectivoDeclarado { get; }
    public decimal TotalDigitalDeclarado { get; }
    public decimal TotalDeclarado => TotalEfectivoDeclarado + TotalDigitalDeclarado;
    public decimal SaldoSistemaTeorico { get; }
    public decimal Diferencia => TotalDeclarado - SaldoSistemaTeorico;
    public bool EsAppendOnly => true;

    public ArqueoCaja(
        Guid id,
        Guid turnoCajaId,
        Guid cajeroId,
        DateTime fechaAperturaUtc,
        DateTime fechaCierreUtc,
        decimal fondoInicialSencillo,
        IEnumerable<ArqueoDenominacion>? desgloseFisico = null,
        decimal totalDigitalDeclarado = 0m,
        decimal saldoSistemaTeorico = 0m)
    {
        if (turnoCajaId == Guid.Empty)
            throw new ArgumentException("El ID de turno de caja es obligatorio.", nameof(turnoCajaId));

        if (cajeroId == Guid.Empty)
            throw new ArgumentException("El ID de cajero es obligatorio.", nameof(cajeroId));

        if (fondoInicialSencillo < 0)
            throw new ArgumentOutOfRangeException(nameof(fondoInicialSencillo), "El fondo inicial no puede ser negativo.");

        if (totalDigitalDeclarado < 0)
            throw new ArgumentOutOfRangeException(nameof(totalDigitalDeclarado), "El total digital declarado no puede ser negativo.");

        if (saldoSistemaTeorico < 0)
            throw new ArgumentOutOfRangeException(nameof(saldoSistemaTeorico), "El saldo del sistema teórico no puede ser negativo.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TurnoCajaId = turnoCajaId;
        CajeroId = cajeroId;
        FechaAperturaUtc = fechaAperturaUtc;
        FechaCierreUtc = fechaCierreUtc;
        FondoInicialSencillo = fondoInicialSencillo;

        var desgloseList = desgloseFisico?.ToList() ?? new List<ArqueoDenominacion>();
        DesgloseFisico = desgloseList.AsReadOnly();
        TotalEfectivoDeclarado = desgloseList.Sum(d => d.Subtotal);
        TotalDigitalDeclarado = totalDigitalDeclarado;
        SaldoSistemaTeorico = saldoSistemaTeorico;
    }

    /// <summary>
    /// Genera una nueva instancia inmutable con el saldo teórico contrastado (sin mutar el registro previo).
    /// </summary>
    public ArqueoCaja ConSaldoTeorico(decimal saldoTeoricoSistema)
    {
        return new ArqueoCaja(
            Id,
            TurnoCajaId,
            CajeroId,
            FechaAperturaUtc,
            FechaCierreUtc,
            FondoInicialSencillo,
            DesgloseFisico,
            TotalDigitalDeclarado,
            saldoTeoricoSistema);
    }

    /// <summary>
    /// Calcula de manera pura y determinista la diferencia contable (positivo = sobrante, negativo = faltante).
    /// </summary>
    public decimal CalcularDiferenciaContable(decimal saldoTeorico)
    {
        return TotalDeclarado - saldoTeorico;
    }

    /// <summary>
    /// Indica si existe discrepancia monetaria frente al saldo esperado del sistema.
    /// </summary>
    public bool TieneDiscrepancia() => Diferencia != 0m;
}
