import React from "react";
import { Warehouse } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { Product } from "../../types/pos";

interface StockModalProps {
  isOpen: boolean;
  onClose: () => void;
  product: Product | null;
}

export const StockModal: React.FC<StockModalProps> = ({ isOpen, onClose, product }) => {
  if (!product) return null;

  const stockDisp = product.stockFisico - product.stockComprometido;

  // Distribución multialmacén de ejemplo (Almacén Central vs Mostrador)
  const almacenes = [
    {
      nombre: "Almacén Central (Mayorista / Despacho Web)",
      tipo: "CENTRAL",
      fisico: Math.round(product.stockFisico * 0.7),
      comprometido: product.stockComprometido,
      disponible: Math.round(product.stockFisico * 0.7) - product.stockComprometido,
    },
    {
      nombre: "Mostrador 01 (Venta Presencial LAN)",
      tipo: "TIENDA",
      fisico: Math.round(product.stockFisico * 0.3),
      comprometido: 0,
      disponible: Math.round(product.stockFisico * 0.3),
    },
  ];

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Consulta de Stock Multialmacén (RNF-CON-003)"
      badge="F5"
      maxWidth="max-w-lg"
    >
      <div className="flex flex-col gap-3 text-xs">
        {/* Info del Producto */}
        <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded">
          <div className="font-mono text-[#8a8f98] mb-0.5">{product.codigoSku}</div>
          <div className="font-medium text-[#f7f8f8] sigic-hardware-desc line-clamp-2">
            {product.nombreComercial}
          </div>
          {product.dimensiones && (
            <span className="inline-block mt-1 px-1.5 py-0.5 bg-[#23252a] text-[#8a8f98] rounded text-[10px] font-mono shrink-0">
              {product.dimensiones}
            </span>
          )}
        </div>

        {/* Tabla de Almacenes */}
        <div className="border border-[#23252a] rounded overflow-hidden">
          <table className="w-full text-left border-collapse text-xs">
            <thead className="bg-[#0f1011] text-[#8a8f98] font-mono border-b border-[#23252a]">
              <tr>
                <th className="p-2">ALMACÉN</th>
                <th className="p-2 text-right">FÍSICO</th>
                <th className="p-2 text-right">COMPR.</th>
                <th className="p-2 text-right">DISPONIBLE</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#18191c]">
              {almacenes.map((a, idx) => (
                <tr key={idx} className="hover:bg-[#1c1e22]">
                  <td className="p-2">
                    <div className="font-semibold text-[#f7f8f8] flex items-center gap-1.5">
                      <Warehouse className="w-3.5 h-3.5 text-[#38bdf8]" />
                      <span>{a.nombre}</span>
                    </div>
                  </td>
                  <td className="p-2 text-right font-mono tabular-nums text-[#8a8f98]">
                    {a.fisico} {product.unidadBase}
                  </td>
                  <td className="p-2 text-right font-mono tabular-nums text-[#f59e0b]">
                    {a.comprometido} {product.unidadBase}
                  </td>
                  <td className="p-2 text-right font-mono tabular-nums font-bold text-[#22c55e]">
                    {a.disponible} {product.unidadBase}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot className="bg-[#0f1011] border-t border-[#23252a] font-mono tabular-nums">
              <tr>
                <td className="p-2 font-bold text-[#f7f8f8]">TOTAL CONSOLIDADO</td>
                <td className="p-2 text-right font-semibold text-[#8a8f98]">
                  {product.stockFisico} {product.unidadBase}
                </td>
                <td className="p-2 text-right font-semibold text-[#f59e0b]">
                  {product.stockComprometido} {product.unidadBase}
                </td>
                <td className="p-2 text-right font-bold text-[#e4f222]">
                  {stockDisp} {product.unidadBase}
                </td>
              </tr>
            </tfoot>
          </table>
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
