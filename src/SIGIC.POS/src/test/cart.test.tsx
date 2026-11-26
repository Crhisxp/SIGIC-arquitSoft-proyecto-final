import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, within, renderHook, act, waitFor } from "@testing-library/react";
import { App } from "../App";
import { useCart } from "../hooks/useCart";
import { INITIAL_PRODUCTS } from "../data/mockProducts";
import { Product } from "../types/pos";
import { api } from "../services/api";

describe("Operaciones del Carrito de Ventas Mostrador (RF-POS-001, RF-POS-003, RF-POS-005)", () => {
  it("agrega ítem por código de barras EAN-13 y actualiza los totales", () => {
    render(<App />);

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);

    // EAN-13 del Perno Grado 5: 7751234000101 (Precio Venta: S/ 1.50)
    fireEvent.change(barcodeInput, { target: { value: "7751234000101" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    expect(screen.getAllByText(/Perno Hexagonal Grado 5/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText("S/ 1.50").length).toBeGreaterThan(0);

    // Agregar segundo ítem idéntico incrementa cantidad a 2 y total a S/ 3.00
    fireEvent.change(barcodeInput, { target: { value: "7751234000101" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    expect(screen.getAllByText("S/ 3.00").length).toBeGreaterThan(0);
  });

  it("permite editar la cantidad con F4 validando stock disponible", () => {
    render(<App />);

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    fireEvent.change(barcodeInput, { target: { value: "7751234000101" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    // Presionar F4 para editar cantidad
    fireEvent.keyDown(window, { key: "F4" });
    expect(screen.getByText(/Modificar Cantidad de Ítem/i)).toBeInTheDocument();

    const qtyInput = screen.getByTestId("input-nueva-cantidad");
    fireEvent.change(qtyInput, { target: { value: "10" } });
    fireEvent.click(screen.getByText(/Actualizar \(Enter\)/i));

    // 10 * 1.50 = 15.00
    expect(screen.getAllByText("S/ 15.00").length).toBeGreaterThan(0);
  });

  it("aplica descuento porcentual de línea con F6", () => {
    render(<App />);

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    // Tubo PVC: 7751234000103 (Precio Venta: S/ 18.00)
    fireEvent.change(barcodeInput, { target: { value: "7751234000103" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    // F6 para abrir descuento
    fireEvent.keyDown(window, { key: "F6" });
    expect(screen.getByText(/Aplicar Descuento \/ Tarifa Especial/i)).toBeInTheDocument();

    // Seleccionar botón preset 10%
    const dialog = screen.getByRole("dialog");
    fireEvent.click(within(dialog).getByText("10%"));
    fireEvent.click(within(dialog).getByText(/Aplicar Descuento \(Enter\)/i));

    // 18.00 - 10% = 16.20
    expect(screen.getAllByText("S/ 16.20").length).toBeGreaterThan(0);
    expect(screen.getByText("-10%")).toBeInTheDocument();
  });

  it("elimina ítem seleccionado con atajo F8", () => {
    render(<App />);

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    fireEvent.change(barcodeInput, { target: { value: "7751234000101" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    expect(screen.getAllByText(/Perno Hexagonal Grado 5/i).length).toBeGreaterThan(0);

    // Eliminar con F8
    fireEvent.keyDown(window, { key: "F8" });
    expect(screen.getByText(/El carrito está vacío/i)).toBeInTheDocument();
  });

  it("permite pausar un ticket con F11 y recuperarlo sin pérdida de ítems (RF-POS-005)", async () => {
    vi.spyOn(api, "obtenerStockAlmacen").mockResolvedValue([
      {
        id: INITIAL_PRODUCTS[0].id,
        productoId: INITIAL_PRODUCTS[0].id,
        codigoSku: INITIAL_PRODUCTS[0].codigoSku,
        stockFisico: INITIAL_PRODUCTS[0].stockFisico,
        stockComprometido: INITIAL_PRODUCTS[0].stockComprometido,
      },
    ]);

    render(<App />);

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    fireEvent.change(barcodeInput, { target: { value: "7751234000101" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    expect(screen.getAllByText(/Perno Hexagonal Grado 5/i).length).toBeGreaterThan(0);

    // Abrir F11 y encolar ticket
    fireEvent.keyDown(window, { key: "F11" });
    expect(screen.getByRole("dialog", { name: "Tickets en espera" })).toBeInTheDocument();

    const dialog = screen.getByRole("dialog");
    fireEvent.click(within(dialog).getByText(/Encolar Ticket/i));

    // Carrito queda vacío para atender al siguiente cliente
    expect(screen.getByText(/El carrito está vacío/i)).toBeInTheDocument();

    // Reabrir F11 y recuperar el ticket
    fireEvent.keyDown(window, { key: "F11" });
    const dialog2 = screen.getByRole("dialog");
    expect(within(dialog2).getByText(/TICKETS EN COLA DE ESPERA \(1\)/i)).toBeInTheDocument();

    fireEvent.click(within(dialog2).getByText(/Recuperar/i));

    // El ítem vuelve al carrito
    await waitFor(() => {
      expect(screen.getAllByText(/Perno Hexagonal Grado 5/i).length).toBeGreaterThan(0);
    });
  });

  it("retorna error y no restaura el ticket si la API de stock falla sin stockOverride", async () => {
    vi.spyOn(api, "obtenerStockAlmacen").mockRejectedValue(new Error("Network Error"));
    const { result } = renderHook(() => useCart());

    act(() => {
      result.current.addItem(INITIAL_PRODUCTS[0], 2);
    });
    act(() => {
      result.current.holdTicket();
    });

    const ticketId = result.current.heldTickets[0].id;

    let res: any;
    await act(async () => {
      res = await result.current.restoreTicket(ticketId);
    });

    expect(res.success).toBe(false);
    expect(res.error).toBe("No se pudo verificar el stock vigente en el servidor.");
    // El ticket se mantiene en cola y el carrito vacío
    expect(result.current.heldTickets.length).toBe(1);
    expect(result.current.items.length).toBe(0);
  });

  it("trata como stock 0 ítems no encontrados en stockVigente (sin fallback a stock en memoria)", async () => {
    const { result } = renderHook(() => useCart());

    act(() => {
      result.current.addItem(INITIAL_PRODUCTS[0], 5);
    });
    act(() => {
      result.current.holdTicket();
    });

    const ticketId = result.current.heldTickets[0].id;

    // stockVigente vacío o con otro producto diferente
    const stockSinNuestroProducto = [
      {
        id: "OTRO-PRODUCTO",
        productoId: "OTRO-PRODUCTO",
        codigoSku: "SKU-DUMMY",
        stockFisico: 100,
        stockComprometido: 0,
      },
    ];

    let res: any;
    await act(async () => {
      res = await result.current.restoreTicket(ticketId, stockSinNuestroProducto);
    });

    // Al no tener stock (disponible = 0), se descarta el ítem
    expect(res.success).toBe(false);
    expect(res.error).toContain("No hay stock disponible");
    expect(result.current.items.length).toBe(0);
  });

  it("purga tickets con antigüedad mayor a 2 horas (7_200_000 ms) alertando al cajero (RF-POS-005)", async () => {
    const { result } = renderHook(() => useCart());

    act(() => {
      result.current.addItem(INITIAL_PRODUCTS[0], 5);
    });
    act(() => {
      result.current.holdTicket();
    });

    expect(result.current.heldTickets.length).toBe(1);
    const ticketId = result.current.heldTickets[0].id;

    // Simular que pasaron 2 horas y 1 minuto (7_260_000 ms)
    const originalNow = Date.now;
    try {
      Date.now = () => originalNow() + 7_260_000;

      let res: any;
      await act(async () => {
        res = await result.current.restoreTicket(ticketId, INITIAL_PRODUCTS);
      });

      expect(res.success).toBe(false);
      expect(res.error).toContain("expiró por superar las 2 horas de espera");
      expect(result.current.heldTickets.length).toBe(0);
      expect(result.current.items.length).toBe(0);
    } finally {
      Date.now = originalNow;
    }
  });

  it("revalida stock disponible (stock_fisico - stock_comprometido) y ajusta ítems al recuperar ticket (RF-POS-005, RNF-CON-003)", async () => {
    const { result } = renderHook(() => useCart());

    act(() => {
      result.current.addItem(INITIAL_PRODUCTS[0], 10);
    });
    act(() => {
      result.current.holdTicket();
    });

    expect(result.current.heldTickets.length).toBe(1);
    const ticketId = result.current.heldTickets[0].id;

    // Simular que el stock disponible del producto cayó a 3 unidades
    const catalogConStockReducido: Product[] = [
      {
        ...INITIAL_PRODUCTS[0],
        stockFisico: 5,
        stockComprometido: 2, // Stock disponible: 5 - 2 = 3
      },
    ];

    let res: any;
    await act(async () => {
      res = await result.current.restoreTicket(ticketId, catalogConStockReducido);
    });

    expect(res.success).toBe(true);
    expect(res.warnings).toBeDefined();
    expect(res.warnings![0]).toContain("Se ajustó la cantidad");
    // Cantidad en el carrito debe ser exactamente el stock disponible (3)
    expect(result.current.items[0].cantidad).toBe(3);
    // Subtotal recalculado: 3 * 1.50 = 4.50
    expect(result.current.items[0].subtotal).toBe(4.5);
  });
});
