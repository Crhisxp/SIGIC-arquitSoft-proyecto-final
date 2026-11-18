import React, { useState, useEffect, useRef } from "react";
import { Lock, AlertTriangle, CheckCircle } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { CashDenomination, ArqueoResponse } from "../../types/pos";

interface BlindCloseModalProps {
  isOpen: boolean;
  onClose: () => void;
  turnoCajaId: string;
  onConfirmClose: (params: {
    turnoCajaId: string;
    desgloseFisico: CashDenomination[];
    totalDigitalDeclarado: number;
    justificacion?: string;
  }) => Promise<ArqueoResponse>;
}

const DENOMINATIONS: number[] = [200, 100, 50, 20, 10, 5, 2, 1, 0.5, 0.2, 0.1];

export const BlindCloseModal: React.FC<BlindCloseModalProps> = ({
  isOpen,
  onClose,
  turnoCajaId,
  onConfirmClose,
}) => {
  const [counts, setCounts] = useState<Record<number, number>>({});
  const [digitalTotal, setDigitalTotal] = useState<string>("0");
  const [justification, setJustification] = useState<string>("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [result, setResult] = useState<ArqueoResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const firstInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      const initialCounts: Record<number, number> = {};
      DENOMINATIONS.forEach((d) => (initialCounts[d] = 0));
      setCounts(initialCounts);
      setDigitalTotal("0");
      setJustification("");
      setResult(null);
      setError(null);
      setIsSubmitting(false);
      setTimeout(() => firstInputRef.current?.focus(), 30);
    }
  }, [isOpen]);

  const handleCountChange = (val: number, countStr: string) => {
    const num = Math.max(0, parseInt(countStr, 10) || 0);
    setCounts((prev) => ({ ...prev, [val]: num }));
  };

  const totalFisico = DENOMINATIONS.reduce((acc, d) => {
    return acc + d * (counts[d] || 0);
  }, 0);

  const totalDigital = parseFloat(digitalTotal) || 0;
  const totalDeclarado = Math.round((totalFisico + totalDigital) * 100) / 100;

  const handleConfirm = async () => {
    try {
      setIsSubmitting(true);
      setError(null);

      const desglose: CashDenomination[] = DENOMINATIONS.map((d) => ({
        valor: d,
        cantidad: counts[d] || 0,
      }));

      const res = await onConfirmClose({
        turnoCajaId,
        desgloseFisico: desglose,
        totalDigitalDeclarado: totalDigital,
        justificacion: justification.trim() || undefined,
      });

      setResult(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Error al registrar arqueo";
      setError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Arqueo Ciego e Inmutable de Caja (RF-CAJ-002)"
      badge="F12"
      maxWidth="max-w-2xl"
    >
      <div className="flex flex-col gap-3 text-xs">
        {/* Aviso Invariante Arqueo Ciego */}
        <div className="p-2.5 bg-[#0f1011] border border-[#23252a] rounded flex items-center gap-2 text-[#8a8f98]">
          <Lock className="w-4 h-4 text-[#e4f222] shrink-0" />
          <span>
            <strong>Arqueo Ciego:</strong> Ingrese el conteo físico real. El sistema registrará el
            arqueo inmutable de forma append-only sin mostrar saldos teóricos esperados.
          </span>
        </div>

        {result ? (
          <div className="p-4 bg-[#22c55e]/15 border border-[#22c55e] rounded text-center flex flex-col items-center gap-2">
            <CheckCircle className="w-8 h-8 text-[#22c55e]" />
            <div className="text-base font-bold text-[#f7f8f8]">
              Arqueo de Turno Registrado Exitosamente
            </div>
            <div className="font-mono text-xs text-[#8a8f98]">
              Arqueo ID: {result.arqueoId} • Estado: {result.estado}
            </div>
            <div className="text-xs text-[#f7f8f8] mt-1">{result.mensaje}</div>
            <button
              type="button"
              onClick={onClose}
              className="mt-3 px-6 py-2 font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015]"
            >
              Cerrar (Escape)
            </button>
          </div>
        ) : (
          <>
            {/* Grilla de Denominaciones */}
            <div className="border border-[#23252a] rounded p-3 bg-[#0f1011]">
              <div className="font-mono text-[#8a8f98] mb-2 uppercase text-[11px]">
                Desglose Físico por Monedas y Billetes
              </div>
              <div className="grid grid-cols-2 gap-x-4 gap-y-2">
                {DENOMINATIONS.map((d, idx) => {
                  const qty = counts[d] || 0;
                  const sub = Math.round(d * qty * 100) / 100;
                  return (
                    <div
                      key={d}
                      className="flex items-center justify-between p-1.5 bg-[#161718] border border-[#23252a] rounded"
                    >
                      <span className="font-mono font-semibold text-[#f7f8f8] w-20">
                        {d >= 10 ? `S/ ${d}` : `S/ ${d.toFixed(2)}`}
                      </span>
                      <input
                        ref={idx === 0 ? firstInputRef : undefined}
                        type="number"
                        min="0"
                        step="1"
                        value={qty === 0 ? "" : qty}
                        placeholder="0"
                        onChange={(e) => handleCountChange(d, e.target.value)}
                        className="w-20 p-1 bg-[#0f1011] border border-[#23252a] rounded text-right font-mono tabular-nums text-[#f7f8f8] focus:border-[#e4f222]"
                      />
                      <span className="font-mono tabular-nums text-right text-[#8a8f98] w-20">
                        S/ {sub.toFixed(2)}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Pagos Digitales y Justificación */}
            <div className="grid grid-cols-2 gap-3">
              <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded">
                <label htmlFor="total-digital-declarado" className="text-[#8a8f98] font-mono block mb-1">
                  TOTAL DIGITAL DECLARADO (Yape / Plin / Tarjetas)
                </label>
                <input
                  id="total-digital-declarado"
                  type="number"
                  step="any"
                  min="0"
                  value={digitalTotal}
                  onChange={(e) => setDigitalTotal(e.target.value)}
                  className="w-full p-2 bg-[#161718] border border-[#23252a] rounded text-right font-mono tabular-nums text-[#f7f8f8] text-sm focus:border-[#e4f222]"
                />
              </div>

              <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded">
                <label className="text-[#8a8f98] font-mono block mb-1">
                  JUSTIFICACIÓN / OBSERVACIONES (Opcional)
                </label>
                <input
                  type="text"
                  value={justification}
                  onChange={(e) => setJustification(e.target.value)}
                  placeholder="Detalles u observaciones del turno..."
                  className="w-full p-2 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] text-sm focus:border-[#e4f222]"
                />
              </div>
            </div>

            {/* Total Consolidado Declarado */}
            <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded flex items-center justify-between font-mono tabular-nums">
              <div>
                <span className="text-[#8a8f98] text-[11px] block">TOTAL FÍSICO</span>
                <span className="text-sm font-bold text-[#f7f8f8]">S/ {totalFisico.toFixed(2)}</span>
              </div>
              <div>
                <span className="text-[#8a8f98] text-[11px] block">TOTAL DIGITAL</span>
                <span className="text-sm font-bold text-[#f7f8f8]">S/ {totalDigital.toFixed(2)}</span>
              </div>
              <div className="text-right">
                <span className="text-[#e4f222] text-[11px] block font-bold">TOTAL DECLARADO</span>
                <span className="text-lg font-bold text-[#e4f222]">
                  S/ {totalDeclarado.toFixed(2)}
                </span>
              </div>
            </div>

            {error && (
              <div className="p-2 bg-[#ef4444]/15 border border-[#ef4444] rounded text-[#ef4444] font-medium flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {/* Botones */}
            <div className="flex justify-end gap-2 pt-2 border-t border-[#23252a]">
              <button
                type="button"
                disabled={isSubmitting}
                onClick={onClose}
                className="px-4 py-2 bg-[#0f1011] text-[#8a8f98] hover:text-[#f7f8f8] border border-[#23252a] rounded"
              >
                Cancelar (Escape)
              </button>
              <button
                type="button"
                disabled={isSubmitting}
                onClick={handleConfirm}
                className="px-4 py-2 font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] disabled:opacity-30 flex items-center gap-2"
              >
                <Lock className="w-4 h-4" />
                <span>{isSubmitting ? "Registrando..." : "Cerrar Turno con Arqueo (Enter)"}</span>
              </button>
            </div>
          </>
        )}
      </div>
    </FocusTrapModal>
  );
};
