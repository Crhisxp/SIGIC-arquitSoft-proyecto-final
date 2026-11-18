import React, { useState, useEffect, useRef } from "react";
import { CheckCircle2, AlertCircle } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { PaymentEntry, MetodoPago, TipoComprobante } from "../../types/pos";

interface PaymentModalProps {
  isOpen: boolean;
  onClose: () => void;
  totalVenta: number;
  tipoComprobanteInicial: TipoComprobante;
  onConfirmSale: (pagos: PaymentEntry[], tipoComprobante: TipoComprobante) => Promise<void>;
}

export const PaymentModal: React.FC<PaymentModalProps> = ({
  isOpen,
  onClose,
  totalVenta,
  tipoComprobanteInicial,
  onConfirmSale,
}) => {
  const [tipoComp, setTipoComp] = useState<TipoComprobante>(tipoComprobanteInicial);
  const [pagos, setPagos] = useState<PaymentEntry[]>([]);
  const [metodoActual, setMetodoActual] = useState<MetodoPago>("EFECTIVO");
  const [montoActual, setMontoActual] = useState<string>("");
  const [codigoOp, setCodigoOp] = useState<string>("");
  const [efectivoRecibido, setEfectivoRecibido] = useState<string>("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const montoInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setTipoComp(tipoComprobanteInicial);
      setPagos([{ metodo: "EFECTIVO", monto: totalVenta }]);
      setMetodoActual("EFECTIVO");
      setMontoActual(totalVenta.toString());
      setCodigoOp("");
      setEfectivoRecibido(totalVenta.toString());
      setError(null);
      setIsSubmitting(false);
      setTimeout(() => montoInputRef.current?.focus(), 30);
    }
  }, [isOpen, totalVenta, tipoComprobanteInicial]);

  const totalPagado = pagos.reduce((acc, p) => acc + p.monto, 0);
  const saldoPendiente = Math.max(0, Math.round((totalVenta - totalPagado) * 100) / 100);

  const totalEfectivo = pagos
    .filter((p) => p.metodo === "EFECTIVO")
    .reduce((acc, p) => acc + p.monto, 0);
  const efRecibidoNum = parseFloat(efectivoRecibido) || totalEfectivo;
  const vuelto = Math.max(0, Math.round((efRecibidoNum - totalEfectivo) * 100) / 100);

  const handleAddPayment = () => {
    const val = parseFloat(montoActual);
    if (isNaN(val) || val <= 0) {
      setError("El monto de pago debe ser mayor a cero.");
      return;
    }
    if (metodoActual !== "EFECTIVO" && !codigoOp.trim()) {
      setError(`El método ${metodoActual} exige ingresar el código de operación digital.`);
      return;
    }

    setPagos((prev) => [
      ...prev,
      {
        metodo: metodoActual,
        monto: val,
        codigoOperacionDigital: metodoActual !== "EFECTIVO" ? codigoOp.trim() : undefined,
      },
    ]);
    setMontoActual("");
    setCodigoOp("");
    setError(null);
  };

  const handleRemovePayment = (index: number) => {
    setPagos((prev) => prev.filter((_, i) => i !== index));
  };

  const handleConfirm = async () => {
    if (totalPagado < totalVenta) {
      setError(`Falta cubrir S/ ${(totalVenta - totalPagado).toFixed(2)} del total de la venta.`);
      return;
    }
    for (const p of pagos) {
      if (p.metodo !== "EFECTIVO" && !p.codigoOperacionDigital) {
        setError(`El pago ${p.metodo} requiere código de operación digital.`);
        return;
      }
    }

    try {
      setIsSubmitting(true);
      setError(null);
      await onConfirmSale(pagos, tipoComp);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Error al procesar cobro";
      setError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Cobro y Liquidación de Venta POS (F9)"
      badge="F9"
      maxWidth="max-w-xl"
    >
      <div className="flex flex-col gap-3 text-xs">
        {/* Cabecera de Montos */}
        <div className="grid grid-cols-3 gap-2 p-3 bg-[#0f1011] border border-[#23252a] rounded text-center">
          <div>
            <span className="text-[#8a8f98] font-mono">TOTAL VENTA</span>
            <div className="text-lg font-bold font-mono tabular-nums text-[#f7f8f8]">
              S/ {totalVenta.toFixed(2)}
            </div>
          </div>
          <div>
            <span className="text-[#8a8f98] font-mono">TOTAL PAGADO</span>
            <div className="text-lg font-bold font-mono tabular-nums text-[#22c55e]">
              S/ {totalPagado.toFixed(2)}
            </div>
          </div>
          <div>
            <span className="text-[#8a8f98] font-mono">VUELTO EFECTIVO</span>
            <div className="text-lg font-bold font-mono tabular-nums text-[#e4f222]">
              S/ {vuelto.toFixed(2)}
            </div>
          </div>
        </div>

        {/* Tipo de Comprobante */}
        <div className="flex items-center gap-2">
          <span className="text-[#8a8f98] font-mono">COMPROBANTE:</span>
          {(["BOLETA", "FACTURA", "TICKET"] as TipoComprobante[]).map((t) => (
            <button
              key={t}
              type="button"
              onClick={() => setTipoComp(t)}
              className={`px-3 py-1 font-mono font-bold rounded border transition-colors ${
                tipoComp === t
                  ? "bg-[#e4f222] text-[#08090a] border-[#e4f222]"
                  : "bg-[#0f1011] text-[#8a8f98] border-[#23252a] hover:bg-[#1c1e22]"
              }`}
            >
              {t}
            </button>
          ))}
        </div>

        {/* Pagos Registrados */}
        <div className="border border-[#23252a] rounded overflow-hidden">
          <table className="w-full text-left border-collapse">
            <thead className="bg-[#0f1011] text-[#8a8f98] font-mono border-b border-[#23252a]">
              <tr>
                <th className="p-2">MÉTODO</th>
                <th className="p-2">CÓDIGO DIGITAL</th>
                <th className="p-2 text-right">MONTO</th>
                <th className="p-2 text-center">QUITAR</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#18191c]">
              {pagos.map((p, idx) => (
                <tr key={idx} className="hover:bg-[#1c1e22]">
                  <td className="p-2 font-mono font-bold text-[#f7f8f8]">{p.metodo}</td>
                  <td className="p-2 font-mono text-[#8a8f98]">
                    {p.codigoOperacionDigital || "—"}
                  </td>
                  <td className="p-2 text-right font-mono tabular-nums font-semibold text-[#f7f8f8]">
                    S/ {p.monto.toFixed(2)}
                  </td>
                  <td className="p-2 text-center">
                    <button
                      type="button"
                      onClick={() => handleRemovePayment(idx)}
                      className="px-2 py-0.5 text-[10px] text-[#ef4444] hover:bg-[#ef4444]/20 rounded"
                    >
                      X
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Agregar Pago Mixto */}
        {saldoPendiente > 0 && (
          <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded flex flex-col gap-2">
            <div className="font-mono text-[#8a8f98] text-[11px]">
              AGREGAR PAGO (SALDO: S/ {saldoPendiente.toFixed(2)})
            </div>
            <div className="grid grid-cols-4 gap-2">
              <select
                value={metodoActual}
                onChange={(e) => setMetodoActual(e.target.value as MetodoPago)}
                className="p-1.5 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-mono"
              >
                <option value="EFECTIVO">EFECTIVO</option>
                <option value="TARJETA">TARJETA</option>
                <option value="YAPE">YAPE</option>
                <option value="PLIN">PLIN</option>
                <option value="TRANSFERENCIA">TRANSF.</option>
              </select>

              <input
                ref={montoInputRef}
                type="number"
                step="any"
                placeholder="Monto"
                value={montoActual}
                onChange={(e) => setMontoActual(e.target.value)}
                className="p-1.5 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-mono tabular-nums"
              />

              <input
                type="text"
                placeholder={metodoActual === "EFECTIVO" ? "N/A" : "Código Op."}
                disabled={metodoActual === "EFECTIVO"}
                value={codigoOp}
                onChange={(e) => setCodigoOp(e.target.value)}
                className="p-1.5 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-mono disabled:opacity-40"
              />

              <button
                type="button"
                onClick={handleAddPayment}
                className="p-1.5 font-mono font-bold bg-[#23252a] text-[#e4f222] hover:bg-[#334155] rounded"
              >
                + Añadir
              </button>
            </div>
          </div>
        )}

        {/* Efectivo Recibido */}
        {totalEfectivo > 0 && (
          <div className="flex items-center gap-3 p-2 bg-[#0f1011] border border-[#23252a] rounded font-mono">
            <span className="text-[#8a8f98]">EFECTIVO RECIBIDO:</span>
            <input
              type="number"
              step="any"
              value={efectivoRecibido}
              onChange={(e) => setEfectivoRecibido(e.target.value)}
              className="w-32 p-1 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-bold tabular-nums text-center"
            />
            <span className="text-[#8a8f98]">Vuelto: S/ {vuelto.toFixed(2)}</span>
          </div>
        )}

        {error && (
          <div className="p-2 bg-[#ef4444]/15 border border-[#ef4444] rounded text-[#ef4444] font-medium flex items-center gap-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {/* Botones Finales */}
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
            disabled={isSubmitting || totalPagado < totalVenta}
            onClick={handleConfirm}
            className="px-4 py-2 font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] disabled:opacity-30 disabled:cursor-not-allowed flex items-center gap-2"
          >
            <CheckCircle2 className="w-4 h-4" />
            <span>{isSubmitting ? "Emitiendo..." : "Confirmar y Cobrar (Enter)"}</span>
          </button>
        </div>
      </div>
    </FocusTrapModal>
  );
};
