import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { App } from "../App";
import { api } from "../services/api";

describe("Arqueo Ciego e Inmutable de Caja (RF-CAJ-002)", () => {
  it("computa denominaciones y oculta estrictamente cualquier saldo teórico esperado", async () => {
    render(<App />);

    // F12 para abrir Arqueo Ciego
    fireEvent.keyDown(window, { key: "F12" });
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Arqueo Ciego e Inmutable de Caja/i)).toBeInTheDocument();

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
      turnoCajaId: "TRN-AYAC-8841",
      fechaArqueoUtc: new Date().toISOString(),
      estado: "CERRADO_CONCILIADO",
      mensaje: "Arqueo ciego registrado e inmutable append-only.",
    });

    // Confirmar arqueo ciego append-only
    fireEvent.click(within(dialog).getByText(/Cerrar Turno con Arqueo \(Enter\)/i));

    await waitFor(() => {
      expect(screen.getByText(/Arqueo de Turno Registrado Exitosamente/i)).toBeInTheDocument();
    });

    // Aún tras el cierre no se revela saldo teórico al cajero
    expect(screen.queryByText(/saldo teórico/i)).not.toBeInTheDocument();
  });

  it("rechazo de API en F12 muestra error y no confirma cierre", async () => {
    vi.spyOn(api, "cerrarTurnoArqueoCiego").mockRejectedValueOnce(
      new Error("Error 500 al persistir arqueo inmutable en PostgreSQL 16")
    );

    render(<App />);

    fireEvent.keyDown(window, { key: "F12" });
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Arqueo Ciego e Inmutable de Caja/i)).toBeInTheDocument();

    const inputs = within(dialog).getAllByPlaceholderText("0");
    fireEvent.change(inputs[1], { target: { value: "1" } });

    // Confirmar arqueo que fallará
    fireEvent.click(within(dialog).getByText(/Cerrar Turno con Arqueo \(Enter\)/i));

    // Muestra error en el modal
    await waitFor(() => {
      expect(
        within(dialog).getByText(/Error 500 al persistir arqueo inmutable en PostgreSQL 16/i)
      ).toBeInTheDocument();
    });

    // No debe confirmar cierre exitoso
    expect(screen.queryByText(/Arqueo de Turno Registrado Exitosamente/i)).not.toBeInTheDocument();

    // Modal debe mantenerse abierto
    expect(dialog).toBeInTheDocument();
  });
});
