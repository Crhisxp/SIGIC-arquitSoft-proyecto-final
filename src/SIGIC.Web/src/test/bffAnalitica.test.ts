// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { handleConsultasPost, handleLoginStaffPost, handleLogoutPost, handleSessionGet } from "../lib/bffAnalitica";

const url = "https://sigic.local";
const token = "firmado.por.backend";
function post(body: unknown, headers: Record<string, string> = {}) {
  return new Request(url + "/admin/analitica", {
    method: "POST", body: JSON.stringify(body),
    headers: { origin: url, host: "sigic.local", ...headers },
  });
}

afterEach(() => vi.unstubAllGlobals());

describe("Sesión interna del copiloto", () => {
  it("rechaza POST sin origen y de otro origen antes de llamar al backend", async () => {
    const fetch = vi.fn(); vi.stubGlobal("fetch", fetch);
    for (const origin of ["", "https://otro.local", "http://sigic.local"]) {
      expect((await handleConsultasPost(post({}, { origin }))).status).toBe(403);
    }
    expect(fetch).not.toHaveBeenCalled();
  });
  it("entrega el JWT únicamente en la cookie interna y la caduca al salir", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({ token, roles: ["Tesoreria"], username: "interno" })));
    const res = await handleLoginStaffPost(post({ username: "interno", password: "fixture" }));
    expect(res.status).toBe(200);
    for (const atributo of ["HttpOnly", "Secure", "SameSite=Strict", "Path=/admin/analitica"]) {
      expect(res.headers.get("set-cookie")).toContain(atributo);
    }
    expect(await res.text()).not.toContain(token);
    expect((await handleLogoutPost(post({}))).headers.get("set-cookie")).toContain("Max-Age=0");
  });
  it("valida la sesión mediante la API incluso si la cookie parece un JWT", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 401 })); vi.stubGlobal("fetch", fetch);
    const res = await handleSessionGet(new Request(url, { headers: { cookie: "sigic_staff_session=" + token } }));
    expect(res.status).toBe(401);
    expect(fetch.mock.calls[0][0]).toMatch(/\/api\/v1\/analitica\/sesion$/);
    expect(fetch.mock.calls[0][1].headers.Authorization).toBe("Bearer " + token);
    expect(res.headers.get("cache-control")).toBe("no-store");
  });
  it("exige la sesión interna para consultar", async () => {
    expect((await handleConsultasPost(post({}, { cookie: "sigic_web_user=fixture" }))).status).toBe(401);
  });
});
