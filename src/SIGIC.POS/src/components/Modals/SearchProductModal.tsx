import React, { useState, useMemo, useEffect, useRef } from "react";
import { Search } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { Product } from "../../types/pos";

interface SearchProductModalProps {
  isOpen: boolean;
  onClose: () => void;
  products: Product[];
  onSelectProduct: (product: Product) => void;
}

export const SearchProductModal: React.FC<SearchProductModalProps> = ({
  isOpen,
  onClose,
  products,
  onSelectProduct,
}) => {
  const [query, setQuery] = useState("");
  const [selectedIndex, setSelectedIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setQuery("");
      setSelectedIndex(0);
      setTimeout(() => inputRef.current?.focus(), 30);
    }
  }, [isOpen]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return products;
    return products.filter(
      (p) =>
        p.codigoSku.toLowerCase().includes(q) ||
        (p.codigoBarras && p.codigoBarras.includes(q)) ||
        p.nombreComercial.toLowerCase().includes(q) ||
        (p.dimensiones && p.dimensiones.toLowerCase().includes(q))
    );
  }, [products, query]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setSelectedIndex((prev) => (prev < filtered.length - 1 ? prev + 1 : prev));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setSelectedIndex((prev) => (prev > 0 ? prev - 1 : prev));
    } else if (e.key === "Enter" && filtered.length > 0) {
      e.preventDefault();
      const p = filtered[selectedIndex];
      if (p) {
        onSelectProduct(p);
        onClose();
      }
    }
  };

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Búsqueda Paramétrica de Productos"
      badge="F2"
      maxWidth="max-w-2xl"
    >
      <div className="flex flex-col gap-3">
        {/* Input de Búsqueda */}
        <div className="relative">
          <Search className="absolute left-3 top-2.5 w-4 h-4 text-[#8a8f98]" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setSelectedIndex(0);
            }}
            onKeyDown={handleKeyDown}
            placeholder="Buscar por SKU, código de barras EAN-13 o descripción ferretera..."
            className="w-full pl-9 pr-3 py-2 bg-[#0f1011] border border-[#23252a] rounded text-sm text-[#f7f8f8] placeholder-[#5e636e] focus:outline-none focus:border-[#e4f222]"
          />
        </div>

        {/* Resultados */}
        <div className="border border-[#23252a] rounded overflow-hidden max-h-[380px] overflow-y-auto sigic-scrollbar">
          <table className="w-full text-left border-collapse text-xs">
            <thead className="bg-[#0f1011] text-[#8a8f98] font-mono border-b border-[#23252a] sticky top-0">
              <tr>
                <th className="py-2 px-3">SKU / CÓDIGO</th>
                <th className="py-2 px-3">DESCRIPCIÓN FERRETERA</th>
                <th className="py-2 px-3 text-right">DISPONIBLE</th>
                <th className="py-2 px-3 text-right">PRECIO</th>
                <th className="py-2 px-2 text-center">ACCIÓN</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#18191c]">
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-6 text-center text-[#8a8f98]">
                    No se encontraron artículos para la búsqueda ingresada.
                  </td>
                </tr>
              ) : (
                filtered.map((item, idx) => {
                  const isSelected = idx === selectedIndex;
                  const stockDisp = item.stockFisico - item.stockComprometido;
                  const hasStock = stockDisp > 0;

                  return (
                    <tr
                      key={item.id}
                      className={`transition-colors ${
                        isSelected ? "bg-[#e4f222]/15 text-[#f7f8f8]" : "hover:bg-[#1c1e22]"
                      }`}
                    >
                      <td className="py-2 px-3 font-mono tabular-nums text-[#8a8f98]">
                        <div>{item.codigoSku}</div>
                        {item.codigoBarras && (
                          <div className="text-[10px] text-[#5e636e]">{item.codigoBarras}</div>
                        )}
                      </td>
                      <td className="py-2 px-3">
                        <div className="font-medium sigic-hardware-desc line-clamp-2">
                          {item.nombreComercial}
                        </div>
                        {item.dimensiones && (
                          <span className="inline-block mt-0.5 px-1.5 py-0.2 bg-[#23252a] text-[#8a8f98] rounded text-[10px] font-mono shrink-0">
                            {item.dimensiones}
                          </span>
                        )}
                      </td>
                      <td className="py-2 px-3 text-right font-mono tabular-nums">
                        <span className={hasStock ? "text-[#22c55e]" : "text-[#ef4444]"}>
                          {stockDisp} {item.unidadBase}
                        </span>
                      </td>
                      <td className="py-2 px-3 text-right font-mono tabular-nums font-semibold text-[#f7f8f8]">
                        S/ {item.precioVenta.toFixed(2)}
                      </td>
                      <td className="py-2 px-2 text-center">
                        <button
                          type="button"
                          disabled={!hasStock}
                          onClick={() => {
                            onSelectProduct(item);
                            onClose();
                          }}
                          className="px-2 py-1 text-xs font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                        >
                          Elegir
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Ayuda de Teclado */}
        <div className="flex items-center justify-between text-xs text-[#8a8f98] px-1 pt-1 border-t border-[#23252a]">
          <span>Use flechas ↑ ↓ para navegar, Enter para seleccionar</span>
          <span>Escape para cancelar</span>
        </div>
      </div>
    </FocusTrapModal>
  );
};
