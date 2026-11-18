import React, { useState, useEffect, useRef } from "react";
import { Percent } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { CartItem } from "../../types/pos";

interface DiscountModalProps {
  isOpen: boolean;
  onClose: () => void;
  item: CartItem | null;
  onConfirm: (discountPct: number) => void;
}

export const DiscountModal: React.FC<DiscountModalProps> = ({
  isOpen,
  onClose,
  item,
  onConfirm,
}) => {
  const [discountPct, setDiscountPct] = useState<string>("0");
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen && item) {
      setDiscountPct(item.descuentoPorcentaje.toString());
      setTimeout(() => {
        inputRef.current?.focus();
        inputRef.current?.select();
      }, 30);
    }
  }, [isOpen, item]);

  if (!item) return null;

  const currentVal = parseFloat(discountPct) || 0;
  const discountedUnit = item.precioUnitario * (1 - currentVal / 100);
  const newSubtotal = item.cantidad * discountedUnit;

  const handleConfirm = () => {
    const val = Math.max(0, Math.min(100, parseFloat(discountPct) || 0));
    onConfirm(val);
    onClose();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      e.preventDefault();
      handleConfirm();
    }
  };

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Aplicar Descuento / Tarifa Especial (F6)"
      badge="F6"
      maxWidth="max-w-md"
    >
      <div className="flex flex-col gap-4 text-xs">
        {/* Info del Ítem */}
        <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded">
          <div className="font-mono text-[#8a8f98] mb-1">{item.producto.codigoSku}</div>
          <div className="font-medium text-[#f7f8f8] sigic-hardware-desc line-clamp-2">
            {item.producto.nombreComercial}
          </div>
          <div className="mt-2 flex items-center justify-between font-mono tabular-nums text-[11px] text-[#8a8f98]">
            <span>Precio Base: S/ {item.precioUnitario.toFixed(2)}</span>
            <span>Cantidad: {item.cantidad}</span>
          </div>
        </div>

        {/* Presets Rápidos */}
        <div>
          <label className="text-[#8a8f98] font-mono block mb-1">DESCUENTOS RÁPIDOS</label>
          <div className="grid grid-cols-4 gap-2">
            {[0, 5, 10, 15].map((pct) => (
              <button
                key={pct}
                type="button"
                onClick={() => setDiscountPct(pct.toString())}
                className={`py-1.5 font-mono tabular-nums font-semibold rounded border transition-colors ${
                  currentVal === pct
                    ? "bg-[#e4f222] text-[#08090a] border-[#e4f222]"
                    : "bg-[#0f1011] text-[#8a8f98] border-[#23252a] hover:bg-[#1c1e22]"
                }`}
              >
                {pct}%
              </button>
            ))}
          </div>
        </div>

        {/* Input Porcentaje */}
        <div>
          <label className="text-[#8a8f98] font-mono block mb-1">PORCENTAJE PERSONALIZADO (%)</label>
          <div className="relative">
            <Percent className="absolute right-3 top-2.5 w-4 h-4 text-[#8a8f98]" />
            <input
              ref={inputRef}
              type="number"
              min="0"
              max="100"
              step="1"
              value={discountPct}
              onChange={(e) => setDiscountPct(e.target.value)}
              onKeyDown={handleKeyDown}
              className="w-full p-2.5 bg-[#0f1011] border border-[#23252a] rounded text-lg font-mono tabular-nums text-[#f7f8f8] text-center focus:border-[#e4f222]"
            />
          </div>
        </div>

        {/* Vista Previa de Precios */}
        <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded flex justify-between font-mono tabular-nums text-xs">
          <div>
            <span className="text-[#8a8f98]">Precio Unitario Final:</span>
            <div className="text-sm font-bold text-[#e4f222]">
              S/ {discountedUnit.toFixed(2)}
            </div>
          </div>
          <div className="text-right">
            <span className="text-[#8a8f98]">Nuevo Subtotal:</span>
            <div className="text-sm font-bold text-[#f7f8f8]">
              S/ {newSubtotal.toFixed(2)}
            </div>
          </div>
        </div>

        {/* Botones */}
        <div className="flex justify-end gap-2 pt-2 border-t border-[#23252a]">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-[#0f1011] text-[#8a8f98] hover:text-[#f7f8f8] border border-[#23252a] rounded transition-colors"
          >
            Cancelar (Escape)
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            className="px-4 py-2 font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] transition-colors"
          >
            Aplicar Descuento (Enter)
          </button>
        </div>
      </div>
    </FocusTrapModal>
  );
};
