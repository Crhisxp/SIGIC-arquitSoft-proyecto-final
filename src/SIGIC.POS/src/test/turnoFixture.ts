import { vi } from "vitest";
import { api } from "../services/api";
import type { TurnoCaja } from "../types/pos";

export const turnoDePrueba: TurnoCaja = {
  turnoId: "11111111-1111-4111-8111-111111111111",
  cajaId: "22222222-2222-4222-8222-222222222222",
  cajeroId: "33333333-3333-4333-8333-333333333333",
  almacenId: "44444444-4444-4444-8444-444444444444",
  estado: "Aperturado",
  fondoInicial: 100,
  fechaAperturaUtc: "2026-11-26T15:00:00Z",
};

export function simularTurnoActivo() {
  vi.spyOn(api, "getToken").mockReturnValue("sesion-pos");
  vi.spyOn(api, "obtenerTurnoActual").mockResolvedValue(turnoDePrueba);
  vi.spyOn(api, "obtenerStockAlmacen").mockResolvedValue([]);
}
