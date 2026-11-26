import type { ConsultaAnaliticaRequest, ConsultaAnaliticaResponse, StaffUser } from "../types/analitica";

export const staffSessionChanged = "sigic:staff-session-changed";

export class AnaliticaHttpError extends Error {
  constructor(readonly status: number, message: string) { super(message); }
}

async function llamar<T>(ruta: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const res = await fetch(`/admin/analitica/${ruta}`, {
    method: body === undefined ? "GET" : "POST", credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), signal,
  });
  const json = await res.json();
  if (!res.ok) throw new AnaliticaHttpError(res.status, json.error || "Servicio no disponible.");
  return json as T;
}

export const adminAnaliticaService = {
  sesion: (signal?: AbortSignal) => llamar<StaffUser>("session", undefined, signal),
  login: (username: string, password: string) => llamar<StaffUser>("login-staff", { username, password }),
  logout: () => llamar("logout", {}),
  consultar: (consulta: ConsultaAnaliticaRequest, signal: AbortSignal) =>
    llamar<ConsultaAnaliticaResponse>("consultas", consulta, signal),
};
