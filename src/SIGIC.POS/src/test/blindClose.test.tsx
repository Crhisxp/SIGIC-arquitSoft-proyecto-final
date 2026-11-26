import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { App } from "../App";
import { simularTurnoActivo, turnoDePrueba } from "./turnoFixture";
import { api } from "../services/api";

afterEach(() => vi.restoreAllMocks());

describe("Arqueo ciego de caja (RF-CAJ-002)", () => {
  it("envía la denominación con el campo que espera la API", async () => {
    const request = vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({}));
    await api.cerrarTurnoArqueoCiego({
      turnoCajaId: turnoDePrueba.turnoId,
      desgloseFisico: [{ valor: 100, cantidad: 2 }],
      totalDigitalDeclarado: 85.5,
    });

    const [, opciones] = request.mock.calls[0];
    expect(JSON.parse(String(opciones?.body)).desgloseFisico).toEqual([{ valorUnitario: 100, cantidad: 2 }]);
  });

  it("computa denominaciones y oculta estrictamente cualquier saldo teórico esperado", async () => {
    simularTurnoActivo();
    render(<App />);
    await screen.findByText("Turno abierto");

    // F12 para abrir Arqueo Ciego
    fireEvent.keyDown(window, { key: "F12" });
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Arqueo ciego de caja/i)).toBeInTheDocument();

    // Verificación estricta de Invariante Arqueo Ciego: Cero visibilidad de saldos teóricos
    expect(within(dialog).queryByText(/saldo teórico/i)).not.toBeInTheDocument();
    expect(within(dialog).queryByText(/monto esperado/i)).not.toBeInTheDocument();
    expect(within(dialog).queryByText(/diferencia/i)).not.toBeInTheDocument();

    // Contar 2 billetes de S/ 100
    const inputs = within(dialog).getAllByPlaceholderText("0");
    // El segundo input corresponde a S/ 100 (después de S/ 200)
    fireEvent.change(inputs[1], { target: { value: "2" } });

    // Contar 1 billete de S/ 50
    fireEvent.change(inputs[2], { target: { value: "1" } });

    // Total físico: (2 * 100) + (1 * 50) = S/ 250.00
    expect(within(dialog).getAllByText("S/ 250.00").length).toBeGreaterThan(0);

    // Declarar monto digital: S/ 85.50
    const digitalInput = within(dialog).getByLabelText(/TOTAL DIGITAL DECLARADO/i);
    fireEvent.change(digitalInput, { target: { value: "85.50" } });

    // Total consolidado declarado: 250 + 85.50 = S/ 335.50
    expect(within(dialog).getByText("S/ 335.50")).toBeInTheDocument();

    // Mock de API exitosa para arqueo ciego
    vi.spyOn(api, "cerrarTurnoArqueoCiego").mockResolvedValueOnce({
      arqueoId: "ARQ-AYAC-2026-001",
      turnoCajaId: turnoDePrueba.turnoId,
      fechaArqueoUtc: new Date().toISOString(),
      estado: "CERRADO_CONCILIADO",
      mensaje: "Arqueo ciego registrado e inmutable append-only.",
    });

    // Confirmar arqueo ciego append-only
    fireEvent.click(within(dialog).getByText(/Cerrar Turno con Arqueo \(Enter\)/i));

    await waitFor(() => {
      expect(screen.getByText(/Arqueo de Turno Registrado Exitosamente/i)).toBeInTheDocument();
    });

    expect(api.cerrarTurnoArqueoCiego).toHaveBeenCalledWith(expect.objectContaining({ turnoCajaId: turnoDePrueba.turnoId }));
    expect(screen.getByText("Sin turno abierto")).toBeInTheDocument();

    // Aún tras el cierre no se revela saldo teórico al cajero
    expect(screen.queryByText(/saldo teórico/i)).not.toBeInTheDocument();
  });

  it("rechazo de API en F12 muestra error y no confirma cierre", async () => {
    vi.spyOn(api, "cerrarTurnoArqueoCiego").mockRejectedValueOnce(
      new Error("Error 500 al persistir arqueo inmutable en PostgreSQL 16")
    );

    simularTurnoActivo();
    render(<App />);
    await screen.findByText("Turno abierto");

    fireEvent.keyDown(window, { key: "F12" });
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Arqueo ciego de caja/i)).toBeInTheDocument();

    const inputs = within(dialog).getAllByPlaceholderText("0");
    fireEvent.change(inputs[1], { target: { value: "1" } });

    // Confirmar arqueo que fallará
    fireEvent.click(within(dialog).getByText(/Cerrar Turno con Arqueo \(Enter\)/i));

    // Muestra error en el modal
    await waitFor(() => {
      expect(
        within(dialog).getByText(/No se pudo cerrar la caja/i)
      ).toBeInTheDocument();
    });

    // No debe confirmar cierre exitoso
    expect(screen.queryByText(/Arqueo de Turno Registrado Exitosamente/i)).not.toBeInTheDocument();

    // Modal debe mantenerse abierto
    expect(dialog).toBeInTheDocument();
  });
});
