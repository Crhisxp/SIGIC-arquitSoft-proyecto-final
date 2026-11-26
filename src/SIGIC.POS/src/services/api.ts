import {
  TurnoCaja,
  CashDenomination,
  CartItem,
  PaymentEntry,
  TipoComprobante,
  VentaPOSResponse,
  ArqueoResponse,
  StockAlmacenDetalle,
} from "../types/pos";

const API_BASE = "/api/v1";

class ApiService {
  private token: string | null = null;

  constructor() {
    this.token = typeof window !== "undefined" ? sessionStorage.getItem("sigic_pos_token") : null;
  }

  setToken(token: string | null) {
    this.token = token;
    if (typeof window !== "undefined") {
      if (token) {
        sessionStorage.setItem("sigic_pos_token", token);
      } else {
        sessionStorage.removeItem("sigic_pos_token");
      }
    }
  }

  getToken(): string | null {
    return this.token;
  }

  private getHeaders(): HeadersInit {
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };
    if (this.token) {
      headers["Authorization"] = `Bearer ${this.token}`;
    }
    return headers;
  }

  async loginStaff(username: string, password: string) {
    const res = await fetch(`${API_BASE}/auth/login-staff`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password }),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: "Error de autenticación" }));
      throw new Error(err.detail || "Error en login de staff");
    }
    const data = await res.json();
    this.setToken(data.token);
    return data;
  }

  async registrarVenta(params: {
    turnoCajaId: string;
    clienteId: string;
    items: CartItem[];
    pagos: PaymentEntry[];
    tipoComprobante: TipoComprobante;
    observaciones?: string;
  }): Promise<VentaPOSResponse> {
    const body = {
      turnoCajaId: params.turnoCajaId,
      clienteId: params.clienteId,
      items: params.items.map((i) => ({
        productoId: i.producto.id,
        cantidad: i.cantidad,
        unidadMedida: i.unidadMedida,
        precioUnitario: i.precioUnitario * (1 - i.descuentoPorcentaje / 100),
      })),
      pagos: params.pagos.map((p) => ({
        metodoPago: p.metodo,
        monto: p.monto,
        codigoOperacionDigital: p.codigoOperacionDigital || null,
      })),
      tipoComprobante: params.tipoComprobante,
      observaciones: params.observaciones || null,
    };

    const res = await fetch(`${API_BASE}/pos/ventas`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: "Error al registrar venta" }));
      throw new Error(err.detail || "Fallo en registro de venta");
    }

    return await res.json();
  }

  async obtenerTurnoActual(cajeroId = this.cajeroDeSesion(), signal?: AbortSignal): Promise<TurnoCaja | null> {
    if (!cajeroId) return null;

    const res = await fetch(`${API_BASE}/caja/turnos/actual?cajeroId=${encodeURIComponent(cajeroId)}`, {
      headers: this.getHeaders(),
      cache: "no-store",
      signal,
    });
    if (res.status === 401 || res.status === 404) return null;
    if (!res.ok) {
      throw new Error("No se pudo obtener el turno de caja actual.");
    }
    return await res.json();
  }

  private cajeroDeSesion(): string {
    const payload = this.token?.split(".")[1];
    if (!payload) return "";
    try {
      // Identifica la consulta. La API verifica el token de personal.
      const { sub } = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")));
      return typeof sub === "string" ? sub : "";
    } catch {
      return "";
    }
  }

  async cerrarTurnoArqueoCiego(params: {
    turnoCajaId: string;
    desgloseFisico: CashDenomination[];
    totalDigitalDeclarado: number;
    justificacion?: string;
  }): Promise<ArqueoResponse> {
    const body = {
      turnoCajaId: params.turnoCajaId,
      desgloseFisico: params.desgloseFisico.map((d) => ({
        valorUnitario: d.valor,
        cantidad: d.cantidad,
      })),
      totalDigitalDeclarado: params.totalDigitalDeclarado,
      justificacion: params.justificacion || null,
    };

    const res = await fetch(`${API_BASE}/caja/turnos/cierre`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: "Error en arqueo ciego" }));
      throw new Error(err.detail || "Fallo en cierre ciego de turno");
    }

    return await res.json();
  }

  async obtenerStock(almacenId?: string, productoId?: string, signal?: AbortSignal): Promise<StockAlmacenDetalle[]> {
    let url = `${API_BASE}/inventario/stock`;
    const params = new URLSearchParams();
    if (almacenId) params.append("almacenId", almacenId);
    if (productoId) params.append("productoId", productoId);
    if (params.toString()) url += `?${params.toString()}`;

    const res = await fetch(url, { headers: this.getHeaders(), cache: "no-store", signal });
    if (!res.ok) throw new Error("Error al consultar stock");
    return await res.json();
  }

  async obtenerStockAlmacen(almacenId?: string): Promise<any[]> {
    let url = `${API_BASE}/inventario/stock`;
    if (almacenId) url += `?almacenId=${encodeURIComponent(almacenId)}`;
    const res = await fetch(url, { headers: this.getHeaders() });
    if (!res.ok) throw new Error("Error al consultar stock del almacén");
    return await res.json();
  }

  async consultarGuiaTraslado(guiaId: string): Promise<any> {
    const res = await fetch(`${API_BASE}/inventario/guias-traslado/${encodeURIComponent(guiaId)}`, {
      headers: this.getHeaders(),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: "Guía de traslado no encontrada." }));
      throw new Error(err.detail || `Guía no encontrada o no válida: ${guiaId}`);
    }
    return await res.json();
  }

  async confirmarDespachoWeb(params: {
    guiaId: string;
    dniReceptor: string;
    codigoRetiro?: string;
  }): Promise<{ status: string; mensaje: string }> {
    if (!/^\d{8}$/.test(params.dniReceptor)) {
      throw new Error("El DNI del receptor debe contener exactamente 8 dígitos numéricos.");
    }
    const idOrCode = params.guiaId || params.codigoRetiro || "";
    const res = await fetch(`${API_BASE}/inventario/guias-traslado/${encodeURIComponent(idOrCode)}/entregar`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify({
        dniReceptor: params.dniReceptor,
        codigoRetiro: params.codigoRetiro || null,
      }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: "Error al confirmar despacho" }));
      throw new Error(err.detail || "Fallo al confirmar despacho de la guía");
    }

    const data = await res.json();
    const status = data.status;
    if (status !== "ENTREGADO") {
      throw new Error(`El despacho no pudo ser confirmado (estado devuelto: ${status || "DESCONOCIDO"}).`);
    }

    return {
      status: "ENTREGADO",
      mensaje: data.mensaje || `Despacho completado para retiro ${params.codigoRetiro || idOrCode}. DNI Receptor: ${params.dniReceptor}.`,
    };
  }
}

export const api = new ApiService();
