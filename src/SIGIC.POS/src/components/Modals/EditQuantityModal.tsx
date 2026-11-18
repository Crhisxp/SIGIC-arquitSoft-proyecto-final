import React, { useState, useEffect, useRef } from "react";
import { FocusTrapModal } from "./FocusTrapModal";
import { CartItem } from "../../types/pos";

interface EditQuantityModalProps {
  isOpen: boolean;
  onClose: () => void;
  item: CartItem | null;
  onConfirm: (quantity: number) => void;
}

export const EditQuantityModal: React.FC<EditQuantityModalProps> = ({
  isOpen,
  onClose,
  item,
  onConfirm,
}) => {
  const [quantity, setQuantity] = useState<string>("1");
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen && item) {
      setQuantity(item.cantidad.toString());
      setError(null);
      setTimeout(() => {
        inputRef.current?.focus();
        inputRef.current?.select();
      }, 30);
    }
  }, [isOpen, item]);

  if (!item) return null;

  const stockDisp = item.producto.stockFisico - item.producto.stockComprometido;

  const handleConfirm = () => {
    const val = parseFloat(quantity);
    if (isNaN(val) || val <= 0) {
      setError("Ingrese una cantidad positiva válida.");
      return;
    }
    if (val > stockDisp) {
      setError(`Stock insuficiente. Disponible: ${stockDisp} ${item.unidadMedida}.`);
      return;
    }
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
      title="Modificar Cantidad de Ítem"
      badge="F4"
      maxWidth="max-w-md"
    >
      <div className="flex flex-col gap-4 text-xs">
        {/* Info del Producto */}
        <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded">
          <div className="font-mono text-[#8a8f98] mb-1">{item.producto.codigoSku}</div>
          <div className="font-medium text-[#f7f8f8] sigic-hardware-desc line-clamp-2">
            {item.producto.nombreComercial}
          </div>
          <div className="mt-2 flex items-center justify-between font-mono tabular-nums text-[11px]">
            <span className="text-[#8a8f98]">Unidad: {item.unidadMedida}</span>
            <span className="text-[#22c55e]">Disponible: {stockDisp}</span>
          </div>
        </div>

        {/* Input de Cantidad */}
        <div>
          <label htmlFor="nueva-cantidad" className="text-[#8a8f98] font-mono block mb-1">NUEVA CANTIDAD</label>
          <input
            ref={inputRef}
            id="nueva-cantidad"
            data-testid="input-nueva-cantidad"
            type="number"
            step="any"
            min="0.01"
            max={stockDisp}
            value={quantity}
            onChange={(e) => {
              setQuantity(e.target.value);
              setError(null);
            }}
            onKeyDown={handleKeyDown}
            className="w-full p-2.5 bg-[#0f1011] border border-[#23252a] rounded text-lg font-mono tabular-nums text-[#f7f8f8] text-center focus:border-[#e4f222]"
          />
        </div>

        {error && (
          <div className="p-2 bg-[#ef4444]/15 border border-[#ef4444] rounded text-[#ef4444] font-medium text-center">
            {error}
          </div>
        )}

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
            Actualizar (Enter)
          </button>
        </div>
      </div>
    </FocusTrapModal>
  );
};
