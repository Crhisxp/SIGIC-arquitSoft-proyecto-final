import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { Navbar } from "../components/Navbar";
import { ProductImage } from "../components/ProductImage";
import { AuthProvider, useAuth } from "../context/AuthContext";
import { CartProvider } from "../context/CartContext";
import { adminAnaliticaService, staffSessionChanged } from "../services/adminAnaliticaService";
import { webApi } from "../services/api";

afterEach(() => vi.restoreAllMocks());

function PerfilActual() {
  const { rolComercial, logout } = useAuth();
  return <button onClick={logout}>{rolComercial}</button>;
}

it("restaura el perfil confirmado y vuelve a público al salir", async () => {
  localStorage.setItem("sigic_web_rol", "ContratistaB2B");
  localStorage.setItem("sigic_web_user", JSON.stringify({ token: "session-token", rolComercial: "ContratistaB2B" }));
  const sesion = vi.spyOn(webApi, "sesion").mockResolvedValue({
    token: "session-token", userId: "maestro", username: "cliente", roles: ["ClienteWeb"], rolComercial: "MaestroObra",
  });

  render(<AuthProvider><PerfilActual /></AuthProvider>);
  expect(screen.getByRole("button", { name: "PublicoGeneral" })).toBeInTheDocument();
  expect(sesion).toHaveBeenCalledWith("session-token", expect.any(AbortSignal));
  fireEvent.click(await screen.findByRole("button", { name: "MaestroObra" }));
  expect(screen.getByRole("button", { name: "PublicoGeneral" })).toBeInTheDocument();
  expect(localStorage.getItem("sigic_web_user")).toBeNull();
});

it("muestra Analítica solo mientras el servidor confirma la sesión interna", async () => {
  const sesion = vi.spyOn(adminAnaliticaService, "sesion")
    .mockRejectedValueOnce(new Error("Sesión ausente"))
    .mockResolvedValueOnce({ username: "personal", roles: ["Superadmin"] })
    .mockRejectedValue(new Error("Sesión cerrada"));
  render(<AuthProvider><CartProvider><Navbar /></CartProvider></AuthProvider>);
  expect(screen.queryByRole("link", { name: "Analítica" })).toBeNull();
  expect(screen.queryByRole("combobox")).toBeNull();
  expect(sesion).toHaveBeenCalledTimes(1);

  act(() => window.dispatchEvent(new Event(staffSessionChanged)));
  expect(await screen.findByRole("link", { name: "Analítica" })).toBeInTheDocument();
  act(() => window.dispatchEvent(new Event(staffSessionChanged)));
  await waitFor(() => expect(screen.queryByRole("link", { name: "Analítica" })).toBeNull());
});

it("usa una imagen de la categoría cuando falla la del producto", () => {
  render(<ProductImage nombre="Cemento Portland" imagenUrl="/productos/imagen-ausente.webp" />);
  const imagen = screen.getByRole("img", { name: "Cemento Portland" });
  expect(new URL((imagen as HTMLImageElement).src).pathname).toBe("/productos/imagen-ausente.webp");
  fireEvent.error(imagen);
  const fallback = screen.getByRole<HTMLImageElement>("img", { name: "Imagen referencial de Cemento Portland" });
  expect(new URL(fallback.src).pathname).toBe("/productos/cemento.webp");
});
