import React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import Panel from "../app/admin/analitica/page";
import { adminAnaliticaService as api, AnaliticaHttpError } from "../services/adminAnaliticaService";

afterEach(() => vi.restoreAllMocks());

it("selecciona una pregunta y presenta los hechos del backend con sus unidades", async () => {
  vi.spyOn(api, "sesion").mockResolvedValue({ username: "tesoreria", roles: ["Tesoreria"] });
  const consulta = vi.spyOn(api, "consultar").mockResolvedValue({
    estado: "con_datos", metrica: "rotacion", hechos: [{ etiqueta: "Rotación", valor: "3.00", unidad: "veces" }],
    resumen: "El inventario rotó tres veces.", corteUtc: "2026-11-01T05:00:00Z",
  });
  render(<Panel />);
  fireEvent.click(await screen.findByRole("button", { name: "Rotación de inventario" }));
  expect(screen.getByLabelText("Pregunta")).toHaveFocus();
  fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
  expect(await screen.findByText("El inventario rotó tres veces.")).toBeVisible();
  expect(within(screen.getByRole("table")).getByText("3.00")).toBeVisible();
  expect(consulta.mock.calls[0][0].pregunta).toContain("rotación");
});

it("ofrece acceso staff al caducar una consulta y descarta los resultados", async () => {
  vi.spyOn(api, "sesion").mockResolvedValue({ username: "admin", roles: ["Superadmin"] });
  vi.spyOn(api, "consultar").mockRejectedValue(new AnaliticaHttpError(401, "La sesión ha caducado."));
  render(<Panel />);
  fireEvent.click(await screen.findByRole("button", { name: "Ventas" }));
  fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
  expect(await screen.findByRole("heading", { name: "Acceso del personal" })).toBeVisible();
  expect(screen.getByRole("alert")).toHaveTextContent("caducado");
  expect(screen.queryByRole("table")).toBeNull();
});
