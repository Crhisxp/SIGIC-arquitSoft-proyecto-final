import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, within, waitFor } from "@testing-library/react";
import { App } from "../App";
import { api } from "../services/api";

describe("Despacho Ligero en Tienda de Pedidos Web (RF-POS-011)", () => {
  it("valida la Guía Interna de Traslado y confirma entrega con estado ENTREGADO", async () => {
    vi.spyOn(api, "consultarGuiaTraslado").mockResolvedValueOnce({
      id: "00000000-0000-0000-0000-000000000099",
      numeroGuia: "GUIA-TR-2026-001",
      pedidoWebId: "WEB-ORD-2026-0091",
      codigoRetiro: "RET-9921",
      guiaInterna: "GUIA-TR-2026-001",
      clienteNombre: "CARLOS MARIO MENDOZA",
      clienteDni: "41238910",
      estado: "Listo para Retiro",
      fechaEmisionUtc: new Date().toISOString(),
      items: [{ producto: "Perno Hexagonal Grado 5 (#12 x 2-1/2\")", cantidad: 50, unidad: "UND" }],
    });

    vi.spyOn(api, "confirmarDespachoWeb").mockResolvedValueOnce({
      status: "ENTREGADO",
      mensaje: "Despacho completado para retiro RET-9921. DNI Receptor: 41238910.",
    });

    render(<App />);

    // Abrir despacho web desde cabecera
    fireEvent.click(screen.getByText("Despacho Web"));
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Entrega de pedidos web en tienda/i)).toBeInTheDocument();

    // Buscar por Guía Interna
    const searchInput = within(dialog).getByPlaceholderText(/Escanee código o ingrese Guía Interna/i);
    fireEvent.change(searchInput, { target: { value: "GUIA-TR-2026-001" } });
    fireEvent.click(within(dialog).getByText("Buscar"));

    // Detalle del pedido encontrado en "Listo para Retiro"
    await waitFor(() => {
      expect(within(dialog).getByText("RET-9921")).toBeInTheDocument();
    });
    expect(within(dialog).getByText("CARLOS MARIO MENDOZA")).toBeInTheDocument();
    expect(within(dialog).getByText("Listo para Retiro")).toBeInTheDocument();

    // Confirmar entrega
    fireEvent.click(within(dialog).getByText(/Confirmar Entrega \(Enter\)/i));

    // Mensaje de entrega exitosa
    await waitFor(() => {
      expect(within(dialog).getByText(/ENTREGADO exitosamente/i)).toBeInTheDocument();
    });
  });

  it("exige DNI de 8 dígitos para confirmar recepción física en tienda", async () => {
    vi.spyOn(api, "consultarGuiaTraslado").mockResolvedValueOnce({
      id: "00000000-0000-0000-0000-000000000099",
      numeroGuia: "GUIA-TR-2026-002",
      codigoRetiro: "RET-9922",
      guiaInterna: "GUIA-TR-2026-002",
      clienteNombre: "JUAN PEREZ",
      clienteDni: "",
      estado: "Listo para Retiro",
      items: [{ producto: "Tubo PVC", cantidad: 2, unidad: "UND" }],
    });

    render(<App />);

    fireEvent.click(screen.getByText("Despacho Web"));
    const dialog = screen.getByRole("dialog");

    const searchInput = within(dialog).getByPlaceholderText(/Escanee código o ingrese Guía Interna/i);
    fireEvent.change(searchInput, { target: { value: "GUIA-TR-2026-002" } });
    fireEvent.click(within(dialog).getByText("Buscar"));

    await waitFor(() => {
      expect(within(dialog).getByText("RET-9922")).toBeInTheDocument();
    });

    // Intentar con DNI inválido (menos de 8 dígitos)
    const dniInput = within(dialog).getByPlaceholderText("Ingrese DNI...");
    fireEvent.change(dniInput, { target: { value: "12345" } });
    fireEvent.click(within(dialog).getByText(/Confirmar Entrega \(Enter\)/i));

    expect(within(dialog).getByText(/exactamente 8 dígitos numéricos/i)).toBeInTheDocument();
  });

  it("rechaza despacho si el pedido no se encuentra en estado 'Listo para Retiro'", async () => {
    vi.spyOn(api, "consultarGuiaTraslado").mockResolvedValueOnce({
      id: "00000000-0000-0000-0000-000000000099",
      numeroGuia: "GUIA-TR-2026-003",
      codigoRetiro: "RET-9923",
      guiaInterna: "GUIA-TR-2026-003",
      clienteNombre: "ANA GOMEZ",
      clienteDni: "44556677",
      estado: "PENDIENTE_PAGO",
      items: [],
    });

    render(<App />);

    fireEvent.click(screen.getByText("Despacho Web"));
    const dialog = screen.getByRole("dialog");

    const searchInput = within(dialog).getByPlaceholderText(/Escanee código o ingrese Guía Interna/i);
    fireEvent.change(searchInput, { target: { value: "GUIA-TR-2026-003" } });
    fireEvent.click(within(dialog).getByText("Buscar"));

    await waitFor(() => {
      expect(within(dialog).getByText(/no apto para entrega/i)).toBeInTheDocument();
    });
  });
});
