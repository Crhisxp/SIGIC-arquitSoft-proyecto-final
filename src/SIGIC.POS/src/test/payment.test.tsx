import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { App } from "../App";
import { simularTurnoActivo } from "./turnoFixture";
import { api } from "../services/api";

afterEach(() => vi.restoreAllMocks());

describe("Liquidación y Cobro con Pagos Mixtos (RF-POS-004, RF-POS-006)", () => {
  it("abre modal F9 y calcula vuelto en efectivo en tiempo real", async () => {
    simularTurnoActivo();
    render(<App />);
    await screen.findByText("Turno abierto");

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    // Tubo PVC: S/ 18.00
    fireEvent.change(barcodeInput, { target: { value: "7751234000103" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    // F9 para cobrar
    fireEvent.keyDown(window, { key: "F9" });
    expect(screen.getByText(/Cobro y Liquidación de Venta POS/i)).toBeInTheDocument();

    // Por defecto pago en efectivo por el total S/ 18.00
    expect(screen.getAllByText("S/ 18.00").length).toBeGreaterThan(0);

    // Ingresar que el cliente paga con un billete de S/ 50.00
    const efectivoInput = screen.getByDisplayValue("18");
    fireEvent.change(efectivoInput, { target: { value: "50" } });

    // Vuelto calculado: 50 - 18 = S/ 32.00
    expect(screen.getByText("S/ 32.00")).toBeInTheDocument();
  });

  it("exige código de operación digital para pagos con Yape o Tarjeta", async () => {
    simularTurnoActivo();
    render(<App />);
    await screen.findByText("Turno abierto");

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    fireEvent.change(barcodeInput, { target: { value: "7751234000103" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    fireEvent.keyDown(window, { key: "F9" });

    // Eliminar el pago en efectivo inicial
    fireEvent.click(screen.getByText("X"));

    // Intentar agregar pago Yape sin código de operación
    const metodoSelect = screen.getByDisplayValue("EFECTIVO");
    fireEvent.change(metodoSelect, { target: { value: "YAPE" } });

    const montoInput = screen.getByPlaceholderText("Monto");
    fireEvent.change(montoInput, { target: { value: "18.00" } });

    fireEvent.click(screen.getByText("+ Añadir"));

    // Muestra error exigiendo código de operación
    expect(screen.getByText(/exige ingresar el código de operación digital/i)).toBeInTheDocument();

    // Ingresar código digital válido
    const codigoInput = screen.getByPlaceholderText("Código Op.");
    fireEvent.change(codigoInput, { target: { value: "OP-987654" } });

    fireEvent.click(screen.getByText("+ Añadir"));

    // Se agrega a la tabla
    expect(screen.getByText("OP-987654")).toBeInTheDocument();
    expect(screen.getByText("YAPE")).toBeInTheDocument();

    // Mock de API exitosa
    vi.spyOn(api, "registrarVenta").mockResolvedValueOnce({
      ventaId: "00000000-0000-0000-0000-000000000001",
      numeroComprobante: "B001-000123",
      totalVenta: 18.0,
      fechaEmisionUtc: new Date().toISOString(),
    });

    // Confirmar venta
    fireEvent.click(screen.getByText(/Confirmar y Cobrar \(Enter\)/i));

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });
  });

  it("rechazo de API en F9 conserva el carrito y muestra error en modal", async () => {
    vi.spyOn(api, "registrarVenta").mockRejectedValueOnce(
      new Error("Stock insuficiente para completar la venta (HTTP 409 Conflict)")
    );

    simularTurnoActivo();
    render(<App />);
    await screen.findByText("Turno abierto");

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    fireEvent.change(barcodeInput, { target: { value: "7751234000103" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    // F9 para cobrar
    fireEvent.keyDown(window, { key: "F9" });
    expect(screen.getByText(/Cobro y Liquidación de Venta POS/i)).toBeInTheDocument();

    // Confirmar venta fallará en la API
    fireEvent.click(screen.getByText(/Confirmar y Cobrar \(Enter\)/i));

    // Muestra error devuelto por la API en el modal
    await waitFor(() => {
      expect(
        screen.getByText(/Stock insuficiente para completar la venta \(HTTP 409 Conflict\)/i)
      ).toBeInTheDocument();
    });

    // El modal de pago permanece abierto para permitir corrección
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    // Cerrar modal con Escape
    fireEvent.keyDown(window, { key: "Escape" });
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });

    // El ítem permanece intacto en el carrito sin pérdida
    expect(screen.getAllByText(/Tubo PVC/i).length).toBeGreaterThan(0);
  });

  it("alterna comprobante entre Boleta y Factura con F10", () => {
    render(<App />);

    expect(screen.getByText("BOLETA")).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "F10" });
    expect(screen.getByText("FACTURA")).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "F10" });
    expect(screen.getByText("BOLETA")).toBeInTheDocument();
  });
});
