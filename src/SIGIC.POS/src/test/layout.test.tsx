import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import { App } from "../App";
import { api } from "../services/api";

afterEach(() => vi.restoreAllMocks());

describe("Distribución fluida y legibilidad del POS", () => {
  it("ocupa el viewport y mantiene las zonas desplazables dentro del área central", () => {
    const { container } = render(<App />);

    const rootElement = container.firstChild as HTMLElement;
    expect(rootElement).toHaveClass("w-full", "h-screen", "flex", "flex-col", "overflow-hidden");
    expect(rootElement.className).not.toMatch(/1024px|768px/);
    expect(screen.getByRole("main")).toHaveClass("flex-1", "min-h-0", "min-w-0");
    const carrito = screen.getByRole("region", { name: "Detalle de facturación" });
    expect(within(carrito).getByRole("table").parentElement).toHaveClass("min-h-0", "overflow-auto");
    expect(screen.getByRole("complementary", { name: "Resumen de venta" })).toHaveClass("min-h-0", "min-w-0");

    const atajos = screen.getByRole("navigation", { name: "Atajos de caja" });
    const acciones = within(atajos).getAllByRole("button");
    expect(acciones).toHaveLength(12);
    acciones.forEach((accion) => expect(accion).toHaveClass("min-h-[44px]", "min-w-[44px]"));
  });

  it("aplica reglas de densidad informativa: line-clamp-2, shrink-0 y tabular-nums", () => {
    render(<App />);

    const barcodeInput = screen.getByPlaceholderText(/Escanear EAN-13/i);
    // Agregar Perno Hexagonal Grado 5
    fireEvent.change(barcodeInput, { target: { value: "7751234000101" } });
    fireEvent.submit(barcodeInput.closest("form")!);

    // Verificar truncamiento en 2 líneas
    const descElements = screen.getAllByText(/Perno Hexagonal Grado 5/i);
    expect(descElements[0].className).toContain("line-clamp-2");

    // Verificar badge con shrink-0
    const badgeElements = screen.getAllByText('#12 x 2-1/2"');
    expect(badgeElements[0].className).toContain("shrink-0");

    // Verificar formato numérico tabular
    const priceElements = document.querySelectorAll(".tabular-nums");
    expect(priceElements.length).toBeGreaterThan(0);
  });

  it("muestra Sin turno abierto y bloquea el cierre de caja cuando el turno no existe", async () => {
    vi.spyOn(api, "getToken").mockReturnValue("sesion-pos");
    vi.spyOn(api, "obtenerTurnoActual").mockResolvedValue(null);
    render(<App />);

    expect(await screen.findByText("Sin turno abierto")).toBeInTheDocument();
    fireEvent.keyDown(window, { key: "F12" });
    expect(screen.getByRole("alert")).toHaveTextContent("Sin turno abierto para cerrar la caja.");
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("F5 muestra las existencias devueltas por almacén y resta las reservas", async () => {
    const consultar = vi.spyOn(api, "obtenerStock").mockResolvedValue([{
      productoId: "demo", codigoSku: "PRN-HEX-001", nombreComercial: "Perno",
      almacenId: "tienda", nombreAlmacen: "Tienda Ayacucho",
      stockFisico: 20, stockComprometido: 7, stockDisponible: 13,
    }]);
    render(<App />);
    await screen.findByText("Sin turno abierto");
    const escaner = screen.getByPlaceholderText(/Escanear EAN-13/i);
    fireEvent.change(escaner, { target: { value: "7751234000101" } });
    fireEvent.submit(escaner.closest("form")!);
    fireEvent.keyDown(window, { key: "F5" });

    const dialogo = screen.getByRole("dialog", { name: /Disponibilidad por almacén/i });
    const fila = await within(dialogo).findByRole("row", { name: /Tienda Ayacucho/i });
    expect(consultar).toHaveBeenCalledWith(undefined, expect.any(String), expect.any(AbortSignal));
    expect(within(fila).getAllByRole("cell").map((celda) => celda.textContent)).toEqual(["20 UND", "7 UND", "13 UND"]);
  });
});
