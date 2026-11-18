import {
  ProductoCatalogo,
  CalculoPDPResultado,
  ReservarPedidoRequest,
  ReservarPedidoResponse,
  PedidoWebDetalle,
  AuthWebUser,
} from "../types/web";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api/v1";

export class ApiError extends Error {
  constructor(public statusCode: number, message: string, public errors?: string[]) {
    super(message);
    this.name = "ApiError";
  }
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let errorDetail = `Error ${res.status}: ${res.statusText}`;
    let errorsList: string[] | undefined;
    try {
      const body = await res.json();
      if (body.detail) errorDetail = body.detail;
      else if (body.message) errorDetail = body.message;
      else if (body.title) errorDetail = body.title;
      if (Array.isArray(body.errors)) errorsList = body.errors;
    } catch {
      // no json body
    }
    throw new ApiError(res.status, errorDetail, errorsList);
  }
  return (await res.json()) as T;
}

export const webApi = {
  async listarProductos(search?: string, almacenId?: string): Promise<ProductoCatalogo[]> {
    const params = new URLSearchParams();
    if (search) params.append("search", search);
    if (almacenId) params.append("almacenId", almacenId);
    const query = params.toString() ? `?${params.toString()}` : "";
    const res = await fetch(`${API_BASE}/public/catalogo/productos${query}`, {
      cache: "no-store",
    });
    return handleResponse<ProductoCatalogo[]>(res);
  },

  async obtenerProducto(id: string): Promise<ProductoCatalogo> {
    const res = await fetch(`${API_BASE}/public/catalogo/productos/${encodeURIComponent(id)}`, {
      cache: "no-store",
    });
    return handleResponse<ProductoCatalogo>(res);
  },

  async calcularPrecioPDP(params: {
    productoId: string;
    cantidad: number;
    unidadMedida?: string;
    rolCliente?: string;
    clienteId?: string;
    almacenId?: string;
  }): Promise<CalculoPDPResultado> {
    const res = await fetch(`${API_BASE}/public/catalogo/calcular-precio`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(params),
    });
    return handleResponse<CalculoPDPResultado>(res);
  },

  async login(username: string, password: string): Promise<AuthWebUser> {
    const res = await fetch(`${API_BASE}/auth/login-web`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password }),
    });
    const data = await handleResponse<any>(res);
    return {
      token: data.token,
      userId: data.userId,
      username: data.username,
      roles: data.roles || ["ClienteWeb"],
      rolComercial: "PublicoGeneral",
    };
  },

  async register(username: string, password: string, email?: string, nombres?: string): Promise<AuthWebUser> {
    const res = await fetch(`${API_BASE}/auth/register-web`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password, email, nombres }),
    });
    const data = await handleResponse<any>(res);
    return {
      token: data.token,
      userId: data.userId,
      username: data.username,
      roles: data.roles || ["ClienteWeb"],
      rolComercial: "PublicoGeneral",
    };
  },

  async reservarPedido(request: ReservarPedidoRequest, token: string): Promise<ReservarPedidoResponse> {
    const res = await fetch(`${API_BASE}/web/pedidos/reservar`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(request),
    });
    return handleResponse<ReservarPedidoResponse>(res);
  },

  async obtenerEstadoPedido(id: string, token: string): Promise<PedidoWebDetalle> {
    const res = await fetch(`${API_BASE}/web/pedidos/${encodeURIComponent(id)}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
      cache: "no-store",
    });
    return handleResponse<PedidoWebDetalle>(res);
  },

  async subirVoucher(id: string, file: File, token: string): Promise<{ pedidoId: string; mensaje: string }> {
    const formData = new FormData();
    formData.append("voucher", file);

    const res = await fetch(`${API_BASE}/web/pedidos/${encodeURIComponent(id)}/voucher`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      body: formData,
    });
    return handleResponse<{ pedidoId: string; mensaje: string }>(res);
  },
};
