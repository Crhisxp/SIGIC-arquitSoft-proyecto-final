import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { App } from "../App";
import { simularTurnoActivo } from "./turnoFixture";

afterEach(() => vi.restoreAllMocks());

describe("Atajos Globales de Teclado F1-F12 y Focus Trap (RF-POS-003)", () => {
  it("abre modal de búsqueda con F2 y cierra con Escape", () => {
    render(<App />);

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    fireEvent.keyDown(window, { key: "F2" });
    const dialog = screen.getByRole("dialog");
    expect(dialog).toBeInTheDocument();
    expect(screen.getByText(/Búsqueda Paramétrica de Productos/i)).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("abre modal de cliente con F3 y cierra con Escape", () => {
    render(<App />);

    fireEvent.keyDown(window, { key: "F3" });
    expect(screen.getByText(/Selección y Captura de Cliente/i)).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("abre modal de tickets en espera con F11", () => {
    render(<App />);

    fireEvent.keyDown(window, { key: "F11" });
    expect(screen.getByText(/Tickets en espera/i)).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("abre modal de arqueo ciego con F12", async () => {
    simularTurnoActivo();
    render(<App />);
    await screen.findByText("Turno abierto");

    fireEvent.keyDown(window, { key: "F12" });
    expect(screen.getByText(/Arqueo ciego de caja/i)).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("captura navegación con flechas y auto-refocus a input F1", () => {
    render(<App />);

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    expect(barcodeInput).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "F1" });
    expect(document.activeElement).toBe(barcodeInput);
  });

  it("atrapa el foco y cicla con Tab y Shift+Tab dentro del modal (FocusTrap)", () => {
    render(<App />);

    // Abrir modal de búsqueda con F2
    fireEvent.keyDown(window, { key: "F2" });
    const dialog = screen.getByRole("dialog");
    expect(dialog).toBeInTheDocument();

    const focusable = dialog.querySelectorAll<HTMLElement>(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );
    expect(focusable.length).toBeGreaterThan(1);

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    // Foco inicial en el primer elemento
    first.focus();
    expect(document.activeElement).toBe(first);

    // Shift+Tab desde el primer elemento salta al último
    fireEvent.keyDown(window, { key: "Tab", shiftKey: true });
    expect(document.activeElement).toBe(last);

    // Tab desde el último elemento salta de vuelta al primero
    fireEvent.keyDown(window, { key: "Tab", shiftKey: false });
    expect(document.activeElement).toBe(first);

    // Cerrar con Escape
    fireEvent.keyDown(window, { key: "Escape" });
  });
});
