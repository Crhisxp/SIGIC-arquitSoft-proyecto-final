export type RolClienteWeb = "PublicoGeneral" | "MaestroObra" | "ContratistaB2B";

export type UnidadFerretera = "UND" | "Caja" | "Rollo" | "Saco" | "Ciento";

export interface ProductoCatalogo {
  id: string;
  codigoSku: string;
  nombreComercial: string;
  unidadBase: string;
  precioVenta: number;
  stockDisponible: number;
  disponibleEnWeb: boolean;
}

export interface CalculoPDPResultado {
  productoId: string;
  codigoSku: string;
  nombreComercial: string;
  unidadBase: string;
  cantidadBaseConvertida: number;
  precioBaseUnitario: number;
  precioFinalUnitario: number;
  descuentoUnitario: number;
  porcentajeDescuentoTotal: number;
  subtotal: number;
  stockDisponible: number;
  aplicaDescuentoVolumen: boolean;
  factorConversion: number;
  precioUnitarioComercial: number;
}

export interface CartItemWeb {
  productoId: string;
  codigoSku: string;
  nombreComercial: string;
  unidadMedida: string;
  cantidad: number;
  cantidadBase: number;
  precioUnitario: number;
  subtotal: number;
  stockDisponible: number;
}

export type ModalidadPago = "PASARELA" | "MANUAL";
export type MetodoPagoWeb = "PasarelaTarjeta" | "Yape" | "Plin" | "TransferenciaBancaria";
export type ModalidadEntrega = "RetiroEnTienda" | "DespachoDomicilio";

export interface ReservarPedidoRequest {
  clienteId: string;
  metodoPago: MetodoPagoWeb;
  modalidadEntrega: ModalidadEntrega;
  tiendaDestinoId?: string | null;
  items: {
    productoId: string;
    cantidad: number;
    precioUnitario: number;
  }[];
}

export interface ReservarPedidoResponse {
  pedidoId: string;
  codigo?: string;
  estado: string;
  total: number;
  fechaCreacionUtc: string;
  fechaExpiracionUtc: string;
  fechaLimiteVoucherUtc?: string | null;
  metodoPago?: MetodoPagoWeb;
  modalidadPago?: ModalidadPago;
  modalidadEntrega?: ModalidadEntrega;
  numeroGuia?: string | null;
  mensaje?: string;
}

export interface PedidoWebItemDetalle {
  productoId: string;
  nombreComercial?: string;
  cantidad: number;
  precioUnitario: number;
  unidadMedida?: string;
  factorConversion?: number;
  cantidadBase?: number;
  subtotal?: number;
}

export interface PedidoWebDetalle {
  id: string;
  codigo?: string;
  clienteId: string;
  almacenOrigenId: string;
  tiendaDestinoId?: string | null;
  modalidadEntrega: ModalidadEntrega;
  metodoPago: MetodoPagoWeb;
  estado: string;
  total?: number;
  fechaCreacionUtc: string;
  fechaExpiracionUtc: string;
  fechaLimiteVoucherUtc?: string | null;
  fechaSubidaVoucherUtc?: string | null;
  numeroGuia?: string | null;
  items?: PedidoWebItemDetalle[];
}

export interface AuthWebUser {
  token: string;
  userId: string;
  username: string;
  roles: string[];
  rolComercial: RolClienteWeb;
}

export const FACTORES_CONVERSION_DEFECTO: Record<string, number> = {
  UND: 1.0,
  Caja: 50.0,
  Rollo: 100.0,
  Saco: 42.5,
  Ciento: 100.0,
};

export const TTL_CONFIG = {
  PASARELA_MINUTOS: 20,
  MANUAL_RESERVA_MINUTOS: 240,
  MANUAL_VOUCHER_MINUTOS: 60,
  MAX_VOUCHER_BYTES: 5 * 1024 * 1024,
  FORMATOS_VOUCHER: [".jpg", ".jpeg", ".png", ".pdf"],
};
