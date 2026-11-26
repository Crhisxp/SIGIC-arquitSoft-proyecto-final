import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import CheckoutPage from "../app/checkout/page";
import PedidoTrackingPage from "../app/pedidos/[id]/page";
import { webApi, ApiError } from "../services/api";
import { AuthProvider } from "../context/AuthContext";
import { CartProvider, useCart } from "../context/CartContext";
import { TTL_CONFIG, CartItemWeb } from "../types/web";
import React, { useEffect } from "react";

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: "00000000-0000-0000-0000-000000000055" }),
  useRouter: () => ({ push: vi.fn() }),
}));

// Componente auxiliar para poblar el carrito en pruebas
const CartPopulator: React.FC<{ items: CartItemWeb[] }> = ({ items }) => {
  const { addItem } = useCart();
  useEffect(() => {
    items.forEach((it) => addItem(it));
  }, [addItem, items]);
  return <CheckoutPage />;
};

describe("Checkout y Reservas Web (RF-WEB-002, RF-WEB-005, RNF-CON-003)", () => {
  beforeEach(() => {
    vi.spyOn(webApi, "sesion").mockResolvedValue({
      token: "fake-jwt-token", userId: "00000000-0000-0000-0000-000000000022",
      username: "cliente_test", roles: ["ClienteWeb"], rolComercial: "PublicoGeneral",
    });
  });
  afterEach(() => vi.restoreAllMocks());

  it("valida parámetros de TTL: 20 min Pasarela vs 240 min Manual con ventana 60 min voucher", () => {
    expect(TTL_CONFIG.PASARELA_MINUTOS).toBe(20);
    expect(TTL_CONFIG.MANUAL_RESERVA_MINUTOS).toBe(240);
    expect(TTL_CONFIG.MANUAL_VOUCHER_MINUTOS).toBe(60);
    expect(TTL_CONFIG.MAX_VOUCHER_BYTES).toBe(5 * 1024 * 1024);
  });

  it("bloquea checkout y muestra error real si la API responde HTTP 409 Conflict por stock insuficiente", async () => {
    // Simular usuario autenticado en localStorage
    localStorage.setItem(
      "sigic_web_user",
      JSON.stringify({
        token: "fake-jwt-token",
        userId: "00000000-0000-0000-0000-000000000022",
        username: "cliente_test",
        roles: ["ClienteWeb"],
        rolComercial: "PublicoGeneral",
      })
    );

    // Mock de API lanzando ApiError 409 Conflict (Havender / PostgreSQL pesimista)
    vi.spyOn(webApi, "reservarPedido").mockRejectedValue(
      new ApiError(409, "Stock insuficiente para el producto Perno Hexagonal Grado 5.")
    );

    const testItem: CartItemWeb = {
      productoId: "00000000-0000-0000-0000-000000000010",
      codigoSku: "PRN-001",
      nombreComercial: "Perno Hexagonal Grado 5",
      unidadMedida: "UND",
      cantidad: 10,
      cantidadBase: 10,
      precioUnitario: 1.5,
      subtotal: 15.0,
      stockDisponible: 10,
    };

    render(
      <AuthProvider>
        <CartProvider>
          <CartPopulator items={[testItem]} />
        </CartProvider>
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/Confirmar pedido/i)).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText(/Confirmar pedido/i));

    await waitFor(() => {
      expect(screen.getByText(/El precio o la disponibilidad cambió durante su compra/i)).toBeInTheDocument();
    });
  });

  it("valida tamaño máximo de comprobante de pago <= 5 MB y rechaza archivos sobredimensionados", async () => {
    localStorage.setItem(
      "sigic_web_user",
      JSON.stringify({
        token: "fake-jwt-token",
        userId: "00000000-0000-0000-0000-000000000022",
        username: "cliente_test",
        roles: ["ClienteWeb"],
        rolComercial: "PublicoGeneral",
      })
    );

    const now = new Date();
    const expUtc = new Date(now.getTime() + 240 * 60000).toISOString();
    const limVoucherUtc = new Date(now.getTime() + 60 * 60000).toISOString();

    vi.spyOn(webApi, "obtenerEstadoPedido").mockResolvedValue({
      id: "00000000-0000-0000-0000-000000000055",
      codigo: "W-20261118-001",
      clienteId: "00000000-0000-0000-0000-000000000022",
      almacenOrigenId: "00000000-0000-0000-0000-000000000001",
      modalidadEntrega: "RetiroEnTienda",
      metodoPago: "Yape",
      estado: "PendientePago",
      fechaCreacionUtc: now.toISOString(),
      fechaExpiracionUtc: expUtc,
      fechaLimiteVoucherUtc: limVoucherUtc,
      numeroGuia: "T001-20261118-RET01",
      items: [
        {
          productoId: "00000000-0000-0000-0000-000000000010",
          cantidad: 10,
          precioUnitario: 15.0,
        },
      ],
    });

    render(
      <AuthProvider>
        <CartProvider>
          <PedidoTrackingPage />
        </CartProvider>
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("W-20261118-001")).toBeInTheDocument();
      // Verifica cálculo del total sin campo 'total' en raíz del DTO
      expect(screen.getByText("S/ 150.00")).toBeInTheDocument();
    });

    // Simular archivo mayor a 5 MB (6 MB)
    const fileGrande = new File([new ArrayBuffer(6 * 1024 * 1024)], "voucher_gigante.png", {
      type: "image/png",
    });

    const fileInput = document.querySelector("input[type='file']") as HTMLInputElement;
    expect(fileInput).toBeInTheDocument();
    fireEvent.change(fileInput, { target: { files: [fileGrande] } });
    await waitFor(() => {
      expect(screen.getByText(/no debe superar los 5 MB/i)).toBeInTheDocument();
    });
  });

  it("envía metodoPago exacto (PasarelaTarjeta vs Yape vs TransferenciaBancaria) en checkout", async () => {
    localStorage.setItem(
      "sigic_web_user",
      JSON.stringify({
        token: "fake-jwt-token",
        userId: "00000000-0000-0000-0000-000000000022",
        username: "cliente_test",
        roles: ["ClienteWeb"],
        rolComercial: "PublicoGeneral",
      })
    );

    const reservarSpy = vi.spyOn(webApi, "reservarPedido").mockResolvedValue({
      pedidoId: "00000000-0000-0000-0000-000000000077",
      estado: "PendientePago",
      total: 15.0,
      fechaCreacionUtc: new Date().toISOString(),
      fechaExpiracionUtc: new Date(Date.now() + 240 * 60000).toISOString(),
    });

    const testItem: CartItemWeb = {
      productoId: "00000000-0000-0000-0000-000000000010",
      codigoSku: "PRN-001",
      nombreComercial: "Perno Hexagonal Grado 5",
      unidadMedida: "UND",
      cantidad: 10,
      cantidadBase: 10,
      precioUnitario: 1.5,
      subtotal: 15.0,
      stockDisponible: 10,
    };

    render(
      <AuthProvider>
        <CartProvider>
          <CartPopulator items={[testItem]} />
        </CartProvider>
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/Confirmar pedido/i)).toBeInTheDocument();
    });

    // Seleccionar Pago Manual
    fireEvent.click(screen.getByText(/Yape, Plin o transferencia/i));

    // Seleccionar Transferencia
    await waitFor(() => {
      expect(screen.getByText("Transferencia")).toBeInTheDocument();
    });
    fireEvent.click(screen.getByText("Transferencia"));

    // Confirmar pedido
    fireEvent.click(screen.getByText(/Confirmar pedido/i));

    await waitFor(() => {
      expect(reservarSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          metodoPago: "TransferenciaBancaria",
        }),
        "fake-jwt-token"
      );
    });
  });
});
