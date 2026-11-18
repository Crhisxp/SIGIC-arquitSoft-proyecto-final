import React from "react";
import { ShoppingCart } from "lucide-react";
import { CartItem } from "../../types/pos";

interface CartTableProps {
  items: CartItem[];
  selectedIndex: number;
  onSelectItem: (index: number) => void;
  onRemoveItem: (index: number) => void;
}

export const CartTable: React.FC<CartTableProps> = ({
  items,
  selectedIndex,
  onSelectItem,
  onRemoveItem,
}) => {
  return (
    <div className="flex-1 bg-[#0f1011] border border-[#23252a] rounded overflow-hidden flex flex-col h-full">
      {/* Cabecera de Tabla */}
      <div className="bg-[#161718] border-b border-[#23252a] px-3 py-2 flex items-center justify-between text-xs font-mono text-[#8a8f98] shrink-0">
        <div className="flex items-center gap-2">
          <ShoppingCart className="w-4 h-4 text-[#e4f222]" />
          <span className="font-bold text-[#f7f8f8]">DETALLE DE FACTURACIÓN</span>
          <span className="text-[#8a8f98]">({items.length} ítems)</span>
        </div>
        <div className="text-[11px] text-[#5e636e]">
          Navegación: flechas ↑ ↓ • F4 Cantidad • F8 Eliminar
        </div>
      </div>

      {/* Contenedor con Scroll Vertical Interno y sin Scroll Horizontal */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden sigic-scrollbar">
        <table className="w-full text-left border-collapse text-xs">
          <thead className="bg-[#161718]/80 text-[#8a8f98] font-mono border-b border-[#23252a] sticky top-0 z-10">
            <tr>
              <th className="py-2 px-2 text-center w-8">#</th>
              <th className="py-2 px-2 w-28">SKU</th>
              <th className="py-2 px-3">DESCRIPCIÓN FERRETERA</th>
              <th className="py-2 px-2 text-right w-20">CANT.</th>
              <th className="py-2 px-2 text-right w-20">P. UNIT</th>
              <th className="py-2 px-2 text-right w-16">DSCTO</th>
              <th className="py-2 px-3 text-right w-24">SUBTOTAL</th>
              <th className="py-2 px-2 text-center w-12">ACC.</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#18191c]">
            {items.length === 0 ? (
              <tr>
                <td colSpan={8} className="py-20 text-center text-[#5e636e]">
                  <div className="flex flex-col items-center gap-2">
                    <ShoppingCart className="w-10 h-10 opacity-20 text-[#8a8f98]" />
                    <p className="font-medium text-sm">El carrito está vacío</p>
                    <p className="text-xs">
                      Escanee un código de barras EAN-13 (F1) o presione F2 para buscar productos
                    </p>
                  </div>
                </td>
              </tr>
            ) : (
              items.map((item, idx) => {
                const isSelected = idx === selectedIndex;
                const stockDisp = item.producto.stockFisico - item.producto.stockComprometido;

                return (
                  <tr
                    key={item.id}
                    tabIndex={0}
                    onClick={() => onSelectItem(idx)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        onSelectItem(idx);
                      }
                    }}
                    className={`transition-colors cursor-pointer outline-none ${
                      isSelected
                        ? "bg-[#e4f222]/15 text-[#f7f8f8] border-l-2 border-l-[#e4f222]"
                        : "hover:bg-[#1c1e22] text-[#8a8f98]"
                    }`}
                  >
                    <td className="py-2.5 px-2 text-center font-mono tabular-nums text-[11px] text-[#5e636e]">
                      {idx + 1}
                    </td>

                    <td className="py-2.5 px-2 font-mono tabular-nums text-[#8a8f98]">
                      <div>{item.producto.codigoSku}</div>
                      {item.producto.codigoBarras && (
                        <div className="text-[10px] text-[#5e636e]">{item.producto.codigoBarras}</div>
                      )}
                    </td>

                    <td className="py-2.5 px-3">
                      <div className="font-medium text-[#f7f8f8] sigic-hardware-desc line-clamp-2">
                        {item.producto.nombreComercial}
                      </div>
                      <div className="flex items-center gap-1.5 mt-0.5">
                        {item.producto.dimensiones && (
                          <span className="px-1.5 py-0.2 bg-[#23252a] text-[#8a8f98] rounded text-[10px] font-mono shrink-0 sigic-dimension-badge">
                            {item.producto.dimensiones}
                          </span>
                        )}
                        <span className="text-[10px] font-mono tabular-nums text-[#5e636e]">
                          Disp: {stockDisp} {item.unidadMedida}
                        </span>
                      </div>
                    </td>

                    <td className="py-2.5 px-2 text-right font-mono tabular-nums font-semibold text-[#f7f8f8]">
                      {item.cantidad} {item.unidadMedida}
                    </td>

                    <td className="py-2.5 px-2 text-right font-mono tabular-nums text-[#8a8f98]">
                      S/ {item.precioUnitario.toFixed(2)}
                    </td>

                    <td className="py-2.5 px-2 text-right font-mono tabular-nums">
                      {item.descuentoPorcentaje > 0 ? (
                        <span className="text-[#e4f222] font-semibold">
                          -{item.descuentoPorcentaje}%
                        </span>
                      ) : (
                        <span className="text-[#5e636e]">0%</span>
                      )}
                    </td>

                    <td className="py-2.5 px-3 text-right font-mono tabular-nums font-bold text-[#f7f8f8] text-sm">
                      S/ {item.subtotal.toFixed(2)}
                    </td>

                    <td className="py-2.5 px-2 text-center">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          onRemoveItem(idx);
                        }}
                        className="px-2 py-0.5 text-xs text-[#ef4444] hover:bg-[#ef4444]/20 rounded transition-colors"
                        title="Eliminar ítem (F8)"
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
