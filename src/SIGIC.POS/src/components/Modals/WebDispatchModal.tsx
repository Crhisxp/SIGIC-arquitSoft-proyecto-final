import React, { useState, useEffect, useRef } from "react";
import { PackageCheck, AlertCircle, CheckCircle2 } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { DespachoWebPedido } from "../../types/pos";
import { api } from "../../services/api";

interface WebDispatchModalProps {
  isOpen: boolean;
  onClose: () => void;
  pedidosDisponibles?: DespachoWebPedido[];
  onConfirmarEntrega: (pedidoId: string, dniReceptor: string) => void;
}

export const WebDispatchModal: React.FC<WebDispatchModalProps> = ({
  isOpen,
  onClose,
  onConfirmarEntrega,
}) => {
  const [codigoBusqueda, setCodigoBusqueda] = useState("");
  const [pedidoSeleccionado, setPedidoSeleccionado] = useState<DespachoWebPedido | null>(null);
  const [dniReceptor, setDniReceptor] = useState("");
  const [mensajeExito, setMensajeExito] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const scanInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setCodigoBusqueda(""); setPedidoSeleccionado(null); setDniReceptor("");
      setMensajeExito(null); setError(null); setIsLoading(false); setIsSubmitting(false);
      setTimeout(() => scanInputRef.current?.focus(), 30);
    }
  }, [isOpen]);

  const handleBuscar = async () => {
    const q = codigoBusqueda.trim();
    if (!q) return;

    setIsLoading(true);
    setError(null);
    setMensajeExito(null);

    try {
      const data = await api.consultarGuiaTraslado(q);
      const estado = typeof data.estado === "string" ? data.estado.trim() : "";
      if (estado !== "Listo para Retiro") {
        setError(`El pedido se encuentra en estado '${estado || "DESCONOCIDO"}', no apto para entrega.`);
        setPedidoSeleccionado(null);
        return;
      }

      const encontrado: DespachoWebPedido = {
        pedidoId: data.pedidoWebId || data.pedidoId || data.id || q,
        codigoRetiro: data.codigoRetiro || data.numeroGuia || q,
        guiaInterna: data.guiaInterna || data.numeroGuia || q,
        clienteNombre: data.clienteNombre || "CLIENTE WEB RETIRO",
        clienteDni: data.clienteDni || "",
        estado: "Listo para Retiro",
        fechaListo: data.fechaEmisionUtc ? new Date(data.fechaEmisionUtc).toLocaleTimeString() : "Hoy",
        items: (data.items || []).map((it: any) => ({
          producto: it.producto || it.productoId || "Ítem de traslado",
          cantidad: it.cantidad || 1,
          unidad: it.unidad || "UND",
        })),
      };

      setPedidoSeleccionado(encontrado);
      setDniReceptor(encontrado.clienteDni);
      setError(null);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Error al consultar guía";
      setError(msg);
      setPedidoSeleccionado(null);
    } finally {
      setIsLoading(false);
    }
  };

  const handleConfirmarEntrega = async () => {
    if (!pedidoSeleccionado) return;

    const dniLimpio = dniReceptor.trim();
    if (!/^\d{8}$/.test(dniLimpio)) {
      setError("Debe registrar un DNI válido de recepción (exactamente 8 dígitos numéricos).");
      return;
    }

    if (pedidoSeleccionado.estado !== "Listo para Retiro") {
      setError(`El pedido se encuentra en estado '${pedidoSeleccionado.estado}', no apto para entrega.`);
      return;
    }

    try {
      setIsSubmitting(true);
      setError(null);

      await api.confirmarDespachoWeb({
        guiaId: pedidoSeleccionado.guiaInterna,
        codigoRetiro: pedidoSeleccionado.codigoRetiro,
        dniReceptor: dniLimpio,
      });

      onConfirmarEntrega(pedidoSeleccionado.pedidoId, dniLimpio);
      setMensajeExito(`Pedido ${pedidoSeleccionado.codigoRetiro} ENTREGADO exitosamente.`);
      setPedidoSeleccionado(null);
      setCodigoBusqueda("");
      setDniReceptor("");
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Error al confirmar entrega";
      setError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      e.preventDefault();
      if (!pedidoSeleccionado) {
        handleBuscar();
      } else {
        handleConfirmarEntrega();
      }
    }
  };

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Entrega de pedidos web en tienda"
      badge="F6"
      maxWidth="max-w-xl"
    >
      <div className="flex flex-col gap-3 text-xs overflow-hidden" onKeyDown={handleKeyDown}>
        <div className="p-2.5 bg-[#0f1011] border border-[#23252a] rounded text-[#8a8f98]">
          Busca el pedido y confirma la entrega con el DNI de quien lo recibe.
        </div>

        {/* Input Escáner / Código de Retiro / Guía Interna */}
        <div>
          <label className="text-[#8a8f98] font-mono block mb-1">
            ESCANEAR CÓDIGO QR / GUÍA INTERNA DE TRASLADO
          </label>
          <div className="flex gap-2">
            <input
              ref={scanInputRef}
              type="text"
              value={codigoBusqueda}
              onChange={(e) => setCodigoBusqueda(e.target.value)}
              placeholder="Escanee código o ingrese Guía Interna (ej. GUIA-TR-2026-001)..."
              className="flex-1 p-2 bg-[#0f1011] border border-[#23252a] rounded text-[#f7f8f8] font-mono uppercase focus:border-[#e4f222]"
            />
            <button
              type="button"
              disabled={isLoading}
              onClick={handleBuscar}
              className="px-4 py-2 font-mono font-bold bg-[#23252a] text-[#e4f222] hover:bg-[#334155] rounded disabled:opacity-40"
            >
              {isLoading ? "Buscando..." : "Buscar"}
            </button>
          </div>
        </div>

        {/* Detalle del Pedido Encontrado */}
        {pedidoSeleccionado && (
          <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded flex flex-col gap-2">
            <div className="flex justify-between items-center border-b border-[#23252a] pb-2">
              <div>
                <span className="font-mono font-bold text-sm text-[#e4f222]">
                  {pedidoSeleccionado.codigoRetiro}
                </span>
                <div className="font-mono text-[#8a8f98] text-[11px]">
                  Guía Interna: {pedidoSeleccionado.guiaInterna}
                </div>
              </div>
              <span className="px-2 py-0.5 rounded text-[11px] font-mono font-bold bg-[#22c55e]/20 text-[#22c55e] shrink-0">
                {pedidoSeleccionado.estado}
              </span>
            </div>

            <div className="grid grid-cols-2 gap-2 text-[11px]">
              <div>
                <span className="text-[#8a8f98] block">TITULAR DEL PEDIDO:</span>
                <span className="font-semibold text-[#f7f8f8]">
                  {pedidoSeleccionado.clienteNombre}
                </span>
              </div>
              <div>
                <span className="text-[#8a8f98] block">DNI REGISTRADO:</span>
                <span className="font-mono tabular-nums text-[#f7f8f8]">
                  {pedidoSeleccionado.clienteDni}
                </span>
              </div>
            </div>

            {/* Ítems a Entregar */}
            <div className="mt-1 border-t border-[#23252a] pt-2">
              <span className="text-[#8a8f98] font-mono block mb-1">ÍTEMS A ENTREGAR:</span>
              <div className="divide-y divide-[#18191c]">
                {pedidoSeleccionado.items.map((it, idx) => (
                  <div key={idx} className="py-1 flex justify-between font-mono text-[11px]">
                    <span className="text-[#f7f8f8]">{it.producto}</span>
                    <span className="tabular-nums text-[#e4f222]">
                      {it.cantidad} {it.unidad}
                    </span>
                  </div>
                ))}
              </div>
            </div>

            {/* DNI del Receptor */}
            <div className="mt-2 pt-2 border-t border-[#23252a]">
              <label className="text-[#8a8f98] font-mono block mb-1">
                DNI DE LA PERSONA QUE RECEPCIONA EN TIENDA
              </label>
              <input
                type="text"
                value={dniReceptor}
                onChange={(e) => setDniReceptor(e.target.value)}
                placeholder="Ingrese DNI..."
                className="w-full p-2 bg-[#161718] border border-[#23252a] rounded font-mono tabular-nums text-[#f7f8f8] focus:border-[#e4f222]"
              />
            </div>
          </div>
        )}

        {mensajeExito && (
          <div className="p-3 bg-[#22c55e]/15 border border-[#22c55e] rounded text-[#22c55e] flex items-center gap-2">
            <CheckCircle2 className="w-5 h-5 shrink-0" />
            <span className="font-semibold">{mensajeExito}</span>
          </div>
        )}

        {error && (
          <div className="p-2 bg-[#ef4444]/15 border border-[#ef4444] rounded text-[#ef4444] flex items-center gap-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {/* Botones */}
        <div className="flex justify-end gap-2 pt-2 border-t border-[#23252a]">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-[#0f1011] text-[#8a8f98] hover:text-[#f7f8f8] border border-[#23252a] rounded"
          >
            Cerrar (Escape)
          </button>
          {pedidoSeleccionado && (
            <button
              type="button"
              disabled={isSubmitting}
              onClick={handleConfirmarEntrega}
              className="px-4 py-2 font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] disabled:opacity-40 flex items-center gap-2"
            >
              <PackageCheck className="w-4 h-4" />
              <span>{isSubmitting ? "Despachando..." : "Confirmar Entrega (Enter)"}</span>
            </button>
          )}
        </div>
      </div>
    </FocusTrapModal>
  );
};
