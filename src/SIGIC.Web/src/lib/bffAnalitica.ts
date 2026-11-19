const cookieName = "sigic_staff_session";
const backend = process.env.INTERNAL_BACKEND_URL || "http://127.0.0.1:5000";
const permitted = ["Superadmin", "Tesoreria", "Almacenero"];

function json(body: unknown, status: number, cookie?: string) {
  const headers = new Headers({ "Cache-Control": "no-store" });
  if (cookie) headers.set("Set-Cookie", cookie);
  return Response.json(body, { status, headers });
}

function sessionCookie(req: Request, token: string) {
  const secure = new URL(req.url).protocol === "https:" || req.headers.get("x-forwarded-proto") === "https";
  return `${cookieName}=${token}; Path=/admin/analitica; HttpOnly; SameSite=Strict; Max-Age=${token ? 900 : 0}${secure ? "; Secure" : ""}`;
}

function sameOrigin(req: Request) {
  const origin = req.headers.get("origin");
  const host = req.headers.get("host") || new URL(req.url).host;
  const protocol = req.headers.get("x-forwarded-proto") || new URL(req.url).protocol.slice(0, -1);
  return origin === `${protocol}://${host}`;
}

function upstream(req: Request, path: string, options: RequestInit) {
  return fetch(`${backend}/api/v1/${path}`, {
    ...options, cache: "no-store", redirect: "error",
    signal: AbortSignal.any([req.signal, AbortSignal.timeout(10000)]),
  });
}

export async function handleLoginStaffPost(req: Request) {
  if (!sameOrigin(req)) return json({ error: "Origen inválido." }, 403);
  const body = await req.json().catch(() => null);
  if (typeof body?.username !== "string" || typeof body.password !== "string")
    return json({ error: "Indique usuario y contraseña." }, 400);
  try {
    const response = await upstream(req, "auth/login-staff", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: body.username, password: body.password }),
    });
    if (!response.ok) return json({ error: "No se pudo iniciar sesión." }, response.status);
    const { token, roles, username } = await response.json();
    if (!Array.isArray(roles) || !roles.some((r: string) => permitted.includes(r)))
      return json({ error: "Se requiere un rol autorizado de analítica." }, 403);
    if (typeof token !== "string" || !/^[A-Za-z0-9_.-]+$/.test(token))
      return json({ error: "Sesión no disponible." }, 502);
    return json({ username, roles, authenticated: true }, 200, sessionCookie(req, token));
  } catch {
    return json({ error: "El servicio de acceso no está disponible." }, 503);
  }
}

export async function handleLogoutPost(req: Request) {
  if (!sameOrigin(req)) return json({ error: "Origen inválido." }, 403);
  return json({ authenticated: false }, 200, sessionCookie(req, ""));
}

async function authenticated(req: Request, consulta: boolean) {
  if (consulta && !sameOrigin(req)) return json({ error: "Origen inválido." }, 403);
  const token = req.headers.get("cookie")?.match(/(?:^|;\s*)sigic_staff_session=([A-Za-z0-9_.-]+)(?:;|$)/)?.[1];
  if (!token) return json({ error: "Inicie sesión con su cuenta interna." }, 401);
  try {
    const body = consulta ? await req.json().catch(() => null) : undefined;
    if (consulta && !body) return json({ error: "Consulta inválida." }, 400);
    const response = await upstream(req, consulta ? "analitica/consultas" : "analitica/sesion", {
      method: consulta ? "POST" : "GET",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: consulta ? JSON.stringify(body) : undefined,
    });
    if (!response.ok) {
      const error = response.status === 401 ? "La sesión ha caducado." : response.status === 403
        ? "Acceso denegado." : response.status === 400 ? "Revise la pregunta y el período." : "Consulta no disponible.";
      return json({ error }, response.status, response.status === 401 ? sessionCookie(req, "") : undefined);
    }
    return json(await response.json(), 200);
  } catch {
    return json({ error: "El servicio de analítica no está disponible." }, 503);
  }
}

export const handleConsultasPost = (req: Request) => authenticated(req, true);
export const handleSessionGet = (req: Request) => authenticated(req, false);
