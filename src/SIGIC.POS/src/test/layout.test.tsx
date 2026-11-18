import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { App } from "../App";

describe("Ergonomía en Resolución 1024x768 y Legibilidad Industrial (RNF-USA-002)", () => {
  it("contiene la UI estrictamente en 1024x768 sin desbordamiento horizontal", () => {
    const { container } = render(<App />);

    const rootElement = container.firstChild as HTMLElement;
    expect(rootElement).toBeInTheDocument();
    expect(rootElement.className).toContain("w-[1024px]");
    expect(rootElement.className).toContain("h-[768px]");
    expect(rootElement.className).toContain("overflow-hidden");
    expect(rootElement.className).toContain("overflow-x-hidden");
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
});
