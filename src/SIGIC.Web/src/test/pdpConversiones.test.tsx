import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import ProductDetailPage from "../app/pdp/[id]/page";
import { webApi } from "../services/api";
import { AuthProvider } from "../context/AuthContext";
import { CartProvider } from "../context/CartContext";
import { FACTORES_CONVERSION_DEFECTO } from "../types/web";

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: "00000000-0000-0000-0000-000000000010" }),
  useRouter: () => ({ push: vi.fn() }),
}));

describe("PDP: Calculadora de Conversiones y Precios Dinámicos (RF-INV-005, RF-PRE-002, RF-INV-001)", () => {
  it("calcula factores de conversión precisos hacia la unidad base (Caja, Ciento, Rollo, Saco)", () => {
    expect(FACTORES_CONVERSION_DEFECTO["UND"]).toBe(1.0);
    expect(FACTORES_CONVERSION_DEFECTO["Caja"]).toBe(50.0);
    expect(FACTORES_CONVERSION_DEFECTO["Ciento"]).toBe(100.0);
    expect(FACTORES_CONVERSION_DEFECTO["Rollo"]).toBe(100.0);
    expect(FACTORES_CONVERSION_DEFECTO["Saco"]).toBe(42.5);

    // Conversión de 3 Cajas a UND: 3 * 50 = 150 UND
    const cajas = 3;
    const factorCaja = FACTORES_CONVERSION_DEFECTO["Caja"];
    expect(cajas * factorCaja).toBe(150);

    // Conversión de 2 Sacos a kg: 2 * 42.5 = 85 kg
    const sacos = 2;
    const factorSaco = FACTORES_CONVERSION_DEFECTO["Saco"];
    expect(sacos * factorSaco).toBe(85);
  });

  it("renderiza PDP y actualiza conversión interactiva al cambiar unidad de medida", async () => {
    vi.spyOn(webApi, "obtenerProducto").mockResolvedValue({
      id: "00000000-0000-0000-0000-000000000010",
      codigoSku: "PRN-HEX-001",
      nombreComercial: "Perno Hexagonal Grado 5 (#12 x 2-1/2)",
      unidadBase: "UND",
      precioVenta: 1.5,
      stockDisponible: 500,
      disponibleEnWeb: true,
    });

    vi.spyOn(webApi, "calcularPrecioPDP").mockResolvedValue({
      productoId: "00000000-0000-0000-0000-000000000010",
      codigoSku: "PRN-HEX-001",
      nombreComercial: "Perno Hexagonal Grado 5 (#12 x 2-1/2)",
      unidadBase: "UND",
      cantidadBaseConvertida: 100,
      precioBaseUnitario: 1.5,
      precioFinalUnitario: 75.0,
      descuentoUnitario: 0,
      porcentajeDescuentoTotal: 0,
      subtotal: 150.0,
      stockDisponible: 500,
      aplicaDescuentoVolumen: false,
      factorConversion: 50.0,
      precioUnitarioComercial: 75.0,
    });

    render(
      <AuthProvider>
        <CartProvider>
          <ProductDetailPage />
        </CartProvider>
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/Perno Hexagonal Grado 5/i)).toBeInTheDocument();
    });

    // Seleccionar unidad 'Caja'
    const select = screen.getByRole("combobox");
    fireEvent.change(select, { target: { value: "Caja" } });

    // Modificar cantidad a 2 cajas
    const inputCantidad = screen.getByRole("spinbutton");
    fireEvent.change(inputCantidad, { target: { value: "2" } });

    await waitFor(() => {
      expect(screen.getByText(/CONVERSIÓN BASE/i)).toBeInTheDocument();
    });
  });

  it("excluye terminantemente productos con disponibleEnWeb == false retornando 404 (RF-INV-001)", async () => {
    vi.spyOn(webApi, "obtenerProducto").mockResolvedValue({
      id: "00000000-0000-0000-0000-000000000099",
      codigoSku: "INT-EXCL-01",
      nombreComercial: "Artículo Exclusivo Mostrador Físico",
      unidadBase: "UND",
      precioVenta: 25.0,
      stockDisponible: 100,
      disponibleEnWeb: false, // EXCLUSIÓN WEB
    });

    render(
      <AuthProvider>
        <CartProvider>
          <ProductDetailPage />
        </CartProvider>
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/producto no disponible/i)).toBeInTheDocument();
    });
    // No permite agregar al carrito
    expect(screen.queryByText(/Agregar al Carrito/i)).not.toBeInTheDocument();
  });

  it("muestra error real y bloquea agregar al carrito si calcularPrecioPDP falla en el servidor sin fabricar precios", async () => {
    vi.spyOn(webApi, "obtenerProducto").mockResolvedValue({
      id: "00000000-0000-0000-0000-000000000010",
      codigoSku: "PRN-HEX-001",
      nombreComercial: "Perno Hexagonal Grado 5 (#12 x 2-1/2)",
      unidadBase: "UND",
      precioVenta: 1.5,
      stockDisponible: 500,
      disponibleEnWeb: true,
    });

    vi.spyOn(webApi, "calcularPrecioPDP").mockRejectedValue(
      new Error("Servicio de tarifas no disponible en servidor")
    );

    render(
      <AuthProvider>
        <CartProvider>
          <ProductDetailPage />
        </CartProvider>
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/No pudimos calcular el precio/i)).toBeInTheDocument();
    });

    // El botón debe mostrar "Precio No Disponible" y estar deshabilitado
    const boton = screen.getByRole("button", { name: /Precio No Disponible/i });
    expect(boton).toBeDisabled();
  });
});
