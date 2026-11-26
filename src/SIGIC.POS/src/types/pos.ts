export type TipoDocumentoCliente = "DNI" | "RUC" | "VARIOS";
export type TarifaCliente = "PUBLICO" | "MAESTRO" | "CONTRATISTA";
export type TipoComprobante = "BOLETA" | "FACTURA" | "TICKET";
export type MetodoPago = "EFECTIVO" | "TARJETA" | "YAPE" | "PLIN" | "TRANSFERENCIA";

export interface Product {
  id: string;
  codigoSku: string;
  codigoBarras?: string;
  nombreComercial: string;
  dimensiones?: string;
  unidadBase: string;
  precioVenta: number;
  precioMayorista?: number;
  precioEspecial?: number;
  stockFisico: number;
  stockComprometido: number;
  stockDisponible: number;
  disponibleEnWeb: boolean;
  familia?: string;
  marca?: string;
}

export interface CartItem {
  id: string;
  producto: Product;
  cantidad: number;
  unidadMedida: string;
  precioUnitario: number;
  descuentoPorcentaje: number;
  subtotal: number;
}

export interface Customer {
  id: string;
  tipoDocumento: TipoDocumentoCliente;
  numeroDocumento: string;
  razonSocial: string;
  direccion?: string;
  tarifa: TarifaCliente;
}

export interface PaymentEntry {
  metodo: MetodoPago;
  monto: number;
  codigoOperacionDigital?: string;
}

export interface CashDenomination {
  valor: number;
  cantidad: number;
}

export interface TurnoCaja {
  turnoId: string;
  cajaId: string;
  cajeroId: string;
  cajeroNombre?: string;
  almacenId: string;
  almacenNombre?: string;
  fondoInicial: number;
  estado: "Aperturado" | "Cerrado";
  fechaAperturaUtc: string;
}

export interface StockAlmacenDetalle {
  productoId: string;
  codigoSku: string;
  nombreComercial: string;
  almacenId: string;
  nombreAlmacen: string;
  stockFisico: number;
  stockComprometido: number;
  stockDisponible: number;
}

export interface HeldTicket {
  id: string;
  cliente: Customer;
  items: CartItem[];
  tipoComprobante: TipoComprobante;
  fechaGuardado: string;
  createdAtMs: number;
  total: number;
}

export interface RestoreTicketResult {
  success: boolean;
  warnings?: string[];
  error?: string;
}

export interface DespachoWebPedido {
  pedidoId: string;
  codigoRetiro: string;
  guiaInterna: string;
  clienteNombre: string;
  clienteDni: string;
  estado: "Listo para Retiro" | "ENTREGADO";
  fechaListo: string;
  items: { producto: string; cantidad: number; unidad: string }[];
}

export interface VentaPOSResponse {
  ventaId: string;
  numeroComprobante: string;
  totalVenta: number;
  fechaEmisionUtc: string;
}

export interface ArqueoResponse {
  arqueoId: string;
  turnoCajaId: string;
  fechaArqueoUtc: string;
  estado: string;
  mensaje: string;
}
