import React, { useEffect, useRef } from "react";
import { Clock, PauseCircle, PlayCircle, AlertTriangle } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { HeldTicket } from "../../types/pos";

interface HeldTicketsModalProps {
  isOpen: boolean;
  onClose: () => void;
  heldTickets: HeldTicket[];
  canHoldCurrentCart: boolean;
  onHoldCurrentCart: () => void;
  onRestoreTicket: (ticketId: string) => void | Promise<void>;
  onPurgeExpired?: () => void;
}

export const HeldTicketsModal: React.FC<HeldTicketsModalProps> = ({
  isOpen,
  onClose,
  heldTickets,
  canHoldCurrentCart,
  onHoldCurrentCart,
  onRestoreTicket,
  onPurgeExpired,
}) => {
  const purgeRef = useRef(onPurgeExpired);
  purgeRef.current = onPurgeExpired;

  useEffect(() => {
    if (isOpen) {
      purgeRef.current?.();
    }
  }, [isOpen]);

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Gestión de Tickets en Espera (RF-POS-005)"
      badge="F11"
      maxWidth="max-w-lg"
    >
      <div className="flex flex-col gap-3 text-xs">
        {/* Acción de Pausar Carrito Actual */}
        {canHoldCurrentCart && (
          <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded flex items-center justify-between">
            <div>
              <div className="font-semibold text-[#f7f8f8]">Pausar Venta en Curso</div>
              <div className="text-[#8a8f98] text-[11px]">
                Guarda los ítems actuales sin descontar Kardex (TTL máx 2h).
              </div>
            </div>
            <button
              type="button"
              onClick={() => {
                onHoldCurrentCart();
                onClose();
              }}
              className="px-3 py-1.5 font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] flex items-center gap-1.5 shrink-0"
            >
              <PauseCircle className="w-4 h-4" />
              <span>Encolar Ticket</span>
            </button>
          </div>
        )}

        {/* Lista de Tickets en Espera */}
        <div>
          <label className="text-[#8a8f98] font-mono block mb-1">
            TICKETS EN COLA DE ESPERA ({heldTickets.length})
          </label>
          <div className="border border-[#23252a] rounded overflow-hidden max-h-[280px] overflow-y-auto sigic-scrollbar">
            {heldTickets.length === 0 ? (
              <div className="p-6 text-center text-[#8a8f98]">
                No hay ventas en espera actualmente.
              </div>
            ) : (
              <div className="divide-y divide-[#18191c]">
                {heldTickets.map((t) => {
                  const elapsedMs = Date.now() - (t.createdAtMs || Date.now());
                  const isExpired = elapsedMs > 7_200_000;
                  const remainingMin = Math.max(0, Math.floor((7_200_000 - elapsedMs) / 60000));

                  return (
                    <div
                      key={t.id}
                      className="p-3 bg-[#0f1011] hover:bg-[#1c1e22] flex items-center justify-between transition-colors"
                    >
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-mono font-bold text-[#e4f222]">{t.id}</span>
                          <span className="text-[#8a8f98] flex items-center gap-1 text-[11px]">
                            <Clock className="w-3 h-3" />
                            {t.fechaGuardado}
                          </span>
                          {isExpired ? (
                            <span className="px-1.5 py-0.5 text-[10px] bg-red-900/40 text-red-400 font-mono rounded flex items-center gap-0.5">
                              <AlertTriangle className="w-3 h-3" /> EXPIRADO (&gt;2h)
                            </span>
                          ) : (
                            <span className="text-[10px] text-[#8a8f98] font-mono">
                              ({remainingMin} min rest.)
                            </span>
                          )}
                        </div>
                        <div className="font-medium text-[#f7f8f8] mt-0.5">
                          {t.cliente.razonSocial}
                        </div>
                        <div className="font-mono text-[#8a8f98] text-[11px]">
                          {t.items.length} ítems • {t.tipoComprobante}
                        </div>
                      </div>
                      <div className="flex items-center gap-3">
                        <div className="text-right font-mono tabular-nums">
                          <span className="text-[10px] text-[#8a8f98] block">TOTAL</span>
                          <span className="font-bold text-[#f7f8f8] text-sm">
                            S/ {t.total.toFixed(2)}
                          </span>
                        </div>
                        <button
                          type="button"
                          onClick={async () => {
                            await onRestoreTicket(t.id);
                            onClose();
                          }}
                          className="px-3 py-1.5 font-mono font-bold bg-[#23252a] text-[#e4f222] hover:bg-[#334155] rounded flex items-center gap-1"
                        >
                          <PlayCircle className="w-4 h-4" />
                          <span>Recuperar</span>
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Botón Cerrar */}
        <div className="flex justify-end pt-2 border-t border-[#23252a]">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-[#0f1011] text-[#8a8f98] hover:text-[#f7f8f8] border border-[#23252a] rounded"
          >
            Cerrar (Escape)
          </button>
        </div>
      </div>
    </FocusTrapModal>
  );
};
