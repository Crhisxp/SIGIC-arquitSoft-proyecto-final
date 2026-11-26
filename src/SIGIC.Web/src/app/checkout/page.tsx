"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { CreditCard, Truck, Store, Clock, AlertTriangle, ArrowLeft, Trash2 } from "lucide-react";
import { useCart } from "../../context/CartContext";
import { useAuth } from "../../context/AuthContext";
import { MetodoPagoWeb, ModalidadEntrega } from "../../types/web";
import { webApi, ApiError } from "../../services/api";

export default function CheckoutPage() {
  const router = useRouter();
  const { items, total, totalItemsCount, removeItem, clearCart } = useCart();
  const { user, isAuthenticated } = useAuth();

  const [modalidadPago, setModalidadPago] = useState<"PASARELA" | "MANUAL">("PASARELA");
  const [metodoManual, setMetodoManual] = useState<"Yape" | "Plin" | "TransferenciaBancaria">("Yape");
  const [modalidadEntrega, setModalidadEntrega] = useState<ModalidadEntrega>("RetiroEnTienda");
  const [tiendaDestinoId, setTiendaDestinoId] = useState<string>("00000000-0000-0000-0000-000000000002");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const metodoPagoFinal: MetodoPagoWeb = modalidadPago === "PASARELA" ? "PasarelaTarjeta" : metodoManual;

  const handleConfirmOrder = async () => {
    if (items.length === 0) return;
    if (!isAuthenticated || !user) {
      setError("Inicie sesión para confirmar su pedido.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      // Revalidación y ordenamiento ascendente por ID (Havender anti-deadlock)
      const itemsOrdenados = [...items]
        .sort((a, b) => a.productoId.localeCompare(b.productoId))
        .map((i) => ({
          productoId: i.productoId,
          cantidad: i.cantidad,
          unidadMedida: i.unidadMedida,
          precioUnitario: i.precioUnitario,
        }));

      const response = await webApi.reservarPedido(
        {
          clienteId: user.userId,
          metodoPago: metodoPagoFinal,
          modalidadEntrega,
          tiendaDestinoId: modalidadEntrega === "RetiroEnTienda" ? tiendaDestinoId : null,
          items: itemsOrdenados,
        },
        user.token
      );

      clearCart();
      router.push(`/pedidos/${response.pedidoId}`);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(err.statusCode === 409
          ? "El precio o la disponibilidad cambió durante su compra. Revise su carrito antes de confirmar."
          : "No pudimos confirmar su pedido. Revise los datos e inténtelo nuevamente.");
      } else {
        setError("No pudimos conectar con la tienda. Inténtelo nuevamente.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (items.length === 0) {
    return (
      <div className="p-12 text-center text-xs font-mono border border-dashed border-[#23252a] rounded flex flex-col items-center gap-4 max-w-xl mx-auto">
        <AlertTriangle className="w-8 h-8 text-[#e4f222]" />
        <div className="text-sm font-bold text-[#f7f8f8]">Su carrito de compras está vacío</div>
        <p className="text-[#8a8f98]">Agregue productos desde el catálogo para hacer su pedido.</p>
        <Link href="/" className="px-4 py-2 bg-[#e4f222] text-[#08090a] font-bold rounded flex items-center gap-2">
          <ArrowLeft className="w-4 h-4" /> Ir al Catálogo Web
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 text-xs max-w-5xl mx-auto">
      <div className="flex justify-between items-center">
        <Link href="/" className="inline-flex items-center gap-1.5 text-[#8a8f98] hover:text-[#f7f8f8] font-mono transition-colors">
          <ArrowLeft className="w-4 h-4" /> Volver al Catálogo
        </Link>
        <span className="font-mono text-[#8a8f98]">Finalizar compra</span>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 flex flex-col gap-6">
          {/* Modalidad de Pago */}
          <div className="p-5 bg-[#0f1011] border border-[#23252a] rounded-lg space-y-3">
            <h2 className="font-bold text-sm text-[#f7f8f8] font-mono flex items-center gap-2">
              <CreditCard className="w-4 h-4 text-[#e4f222]" /> <span>1. Forma de pago</span>
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <button
                type="button" onClick={() => setModalidadPago("PASARELA")}
                className={`p-4 border rounded text-left transition-colors flex flex-col justify-between ${
                  modalidadPago === "PASARELA" ? "border-[#e4f222] bg-[#161718]" : "border-[#23252a] bg-[#0f1011] hover:border-[#334155]"
                }`}
              >
                <div>
                  <div className="font-bold text-[#f7f8f8] flex items-center justify-between">
                    <span>Tarjeta en línea</span>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-[#e4f222]/20 text-[#e4f222] font-mono">20 min de reserva</span>
                  </div>
                  <p className="text-[11px] text-[#8a8f98] mt-1">Pague con tarjeta antes de que venza la reserva.</p>
                </div>
                <div className="mt-3 flex items-center gap-1 text-[10px] text-[#22c55e] font-mono">
                  <Clock className="w-3.5 h-3.5" /> La reserva se cancela al vencer
                </div>
              </button>

              <button
                type="button" onClick={() => setModalidadPago("MANUAL")}
                className={`p-4 border rounded text-left transition-colors flex flex-col justify-between ${
                  modalidadPago === "MANUAL" ? "border-[#e4f222] bg-[#161718]" : "border-[#23252a] bg-[#0f1011] hover:border-[#334155]"
                }`}
              >
                <div>
                  <div className="font-bold text-[#f7f8f8] flex items-center justify-between">
                    <span>Yape, Plin o transferencia</span>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-[#38bdf8]/20 text-[#38bdf8] font-mono">Hasta 4 h de reserva</span>
                  </div>
                  <p className="text-[11px] text-[#8a8f98] mt-1">Adjunte su comprobante para que podamos confirmar el pago.</p>
                </div>
                <div className="mt-3 flex items-center gap-1 text-[10px] text-[#f59e0b] font-mono">
                  <Clock className="w-3.5 h-3.5" /> 60 minutos para adjuntar su comprobante
                </div>
              </button>
            </div>

            {modalidadPago === "MANUAL" && (
              <div className="pt-3 border-t border-[#23252a]">
                <label className="text-[#8a8f98] font-mono block mb-1.5 text-[11px]">
                  SELECCIONE MEDIO MANUAL DE PAGO:
                </label>
                <div className="grid grid-cols-3 gap-2">
                  {(["Yape", "Plin", "TransferenciaBancaria"] as const).map((m) => (
                    <button
                      key={m}
                      type="button"
                      onClick={() => setMetodoManual(m)}
                      className={`p-2 rounded text-center font-mono font-bold text-xs border transition-colors ${
                        metodoManual === m
                          ? "border-[#e4f222] bg-[#161718] text-[#e4f222]"
                          : "border-[#23252a] text-[#8a8f98] hover:text-[#f7f8f8]"
                      }`}
                    >
                      {m === "TransferenciaBancaria" ? "Transferencia" : m}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Modalidad de Entrega */}
          <div className="p-5 bg-[#0f1011] border border-[#23252a] rounded-lg space-y-3">
            <h2 className="font-bold text-sm text-[#f7f8f8] font-mono flex items-center gap-2">
              <Truck className="w-4 h-4 text-[#e4f222]" /> <span>2. Entrega</span>
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <button
                type="button" onClick={() => setModalidadEntrega("RetiroEnTienda")}
                className={`p-4 border rounded text-left transition-colors ${
                  modalidadEntrega === "RetiroEnTienda" ? "border-[#e4f222] bg-[#161718]" : "border-[#23252a] bg-[#0f1011] hover:border-[#334155]"
                }`}
              >
                <div className="font-bold text-[#f7f8f8] flex items-center gap-1.5">
                  <Store className="w-4 h-4 text-[#e4f222]" /> Retiro en Tienda (Ayacucho)
                </div>
                <p className="text-[11px] text-[#8a8f98] mt-1">Elija el local donde desea recoger sus materiales.</p>
              </button>

              <button
                type="button" onClick={() => setModalidadEntrega("DespachoDomicilio")}
                className={`p-4 border rounded text-left transition-colors ${
                  modalidadEntrega === "DespachoDomicilio" ? "border-[#e4f222] bg-[#161718]" : "border-[#23252a] bg-[#0f1011] hover:border-[#334155]"
                }`}
              >
                <div className="font-bold text-[#f7f8f8] flex items-center gap-1.5">
                  <Truck className="w-4 h-4 text-[#e4f222]" /> Despacho a Domicilio
                </div>
                <p className="text-[11px] text-[#8a8f98] mt-1">Reciba los materiales en su obra o domicilio.</p>
              </button>
            </div>

            {modalidadEntrega === "RetiroEnTienda" && (
              <div className="pt-2">
                <label className="text-[#8a8f98] font-mono block mb-1">TIENDA DESTINO PARA RETIRO:</label>
                <select
                  value={tiendaDestinoId} onChange={(e) => setTiendaDestinoId(e.target.value)}
                  className="w-full p-2 bg-[#161718] border border-[#23252a] rounded font-mono text-[#f7f8f8] focus:border-[#e4f222]"
                >
                  <option value="00000000-0000-0000-0000-000000000002">Tienda Mostrador 01 — Jr. 28 de Julio, Ayacucho</option>
                  <option value="00000000-0000-0000-0000-000000000003">Tienda Mostrador 02 — Av. Mariscal Cáceres, Ayacucho</option>
                </select>
              </div>
            )}
          </div>
        </div>

        {/* Resumen de Carrito */}
        <div className="flex flex-col gap-4">
          <div className="p-5 bg-[#0f1011] border border-[#23252a] rounded-lg flex flex-col justify-between">
            <div>
              <h2 className="font-bold text-sm text-[#f7f8f8] font-mono mb-3">Resumen ({totalItemsCount} ítems)</h2>
              <div className="divide-y divide-[#18191c] max-h-[220px] overflow-y-auto mb-4">
                {items.map((i) => (
                  <div key={`${i.productoId}:${i.unidadMedida}`} className="py-2 flex justify-between items-center text-[11px]">
                    <div>
                      <div className="font-semibold text-[#f7f8f8] max-w-[150px] truncate">{i.nombreComercial}</div>
                      <div className="font-mono text-[#8a8f98]">{i.cantidad} {i.unidadMedida}</div>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="font-mono sigic-tabular text-[#f7f8f8]">S/ {i.subtotal.toFixed(2)}</span>
                      <button type="button" onClick={() => removeItem(i.productoId, i.unidadMedida)} className="text-[#8a8f98] hover:text-[#ef4444]" title="Quitar">
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              <div className="space-y-1.5 pt-3 border-t border-[#23252a] font-mono text-[11px]">
                <div className="flex justify-between text-[#8a8f98]"><span>Subtotal Neto:</span><span className="sigic-tabular">S/ {(total / 1.18).toFixed(2)}</span></div>
                <div className="flex justify-between text-[#8a8f98]"><span>IGV (18%):</span><span className="sigic-tabular">S/ {(total - total / 1.18).toFixed(2)}</span></div>
                <div className="flex justify-between text-sm font-bold text-[#f7f8f8] pt-2 border-t border-[#23252a]">
                  <span>Total:</span><span className="text-[#e4f222] sigic-tabular">S/ {total.toFixed(2)}</span>
                </div>
              </div>
            </div>

            {error && (
              <div className="mt-4 p-2.5 bg-[#ef4444]/15 border border-[#ef4444] rounded text-[11px] text-[#ef4444] font-mono flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" /> <span>{error}</span>
              </div>
            )}

            {!isAuthenticated ? (
              <div className="mt-4 p-3 bg-[#161718] border border-[#23252a] rounded text-center">
                <span className="text-[#8a8f98] block mb-2 font-mono text-[11px]">Inicie sesión para hacer su pedido</span>
                <Link href="/login" className="w-full py-2 bg-[#e4f222] text-[#08090a] font-mono font-bold text-xs rounded hover:bg-[#d3e015] block">
                  Iniciar Sesión para Reservar
                </Link>
              </div>
            ) : (
              <button
                type="button" disabled={isSubmitting} onClick={handleConfirmOrder}
                className="mt-4 w-full py-3 bg-[#e4f222] text-[#08090a] font-mono font-bold text-sm rounded hover:bg-[#d3e015] disabled:opacity-40 transition-colors"
              >
                {isSubmitting ? "Confirmando pedido…" : "Confirmar pedido"}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
