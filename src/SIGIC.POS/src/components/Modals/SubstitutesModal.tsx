import React from "react";
import { Replace } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { Product } from "../../types/pos";

interface SubstitutesModalProps {
  isOpen: boolean;
  onClose: () => void;
  product: Product | null;
  allProducts: Product[];
  onSelectSubstitute: (sub: Product) => void;
}

export const SubstitutesModal: React.FC<SubstitutesModalProps> = ({
  isOpen,
  onClose,
  product,
  allProducts,
  onSelectSubstitute,
}) => {
  if (!product) return null;

  // Filtra productos de la misma familia excluyendo el actual
  const substitutes = allProducts.filter(
    (p) => p.familia === product.familia && p.id !== product.id
  );

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Sustitutos y Equivalentes Técnicos (F7)"
      badge="F7"
      maxWidth="max-w-xl"
    >
      <div className="flex flex-col gap-3 text-xs">
        {/* Producto Base */}
        <div className="p-2.5 bg-[#0f1011] border border-[#23252a] rounded">
          <span className="text-[#8a8f98] font-mono block text-[10px]">ARTÍCULO DE REFERENCIA:</span>
          <div className="font-semibold text-[#f7f8f8]">{product.nombreComercial}</div>
          <div className="text-[#8a8f98] font-mono text-[11px] mt-0.5">
            Familia: {product.familia} • SKU: {product.codigoSku}
          </div>
        </div>

        {/* Lista de Equivalentes */}
        <div className="border border-[#23252a] rounded overflow-hidden max-h-[300px] overflow-y-auto sigic-scrollbar">
          {substitutes.length === 0 ? (
            <div className="p-6 text-center text-[#8a8f98]">
              No se registraron productos sustitutos directos en esta familia técnica.
            </div>
          ) : (
            <table className="w-full text-left border-collapse text-xs">
              <thead className="bg-[#0f1011] text-[#8a8f98] font-mono border-b border-[#23252a]">
                <tr>
                  <th className="p-2">SKU</th>
                  <th className="p-2">DESCRIPCIÓN SUSTITUTA</th>
                  <th className="p-2 text-right">DISPONIBLE</th>
                  <th className="p-2 text-right">PRECIO</th>
                  <th className="p-2 text-center">ACCIÓN</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#18191c]">
                {substitutes.map((sub) => {
                  const disp = sub.stockFisico - sub.stockComprometido;
                  return (
                    <tr key={sub.id} className="hover:bg-[#1c1e22]">
                      <td className="p-2 font-mono text-[#8a8f98]">{sub.codigoSku}</td>
                      <td className="p-2">
                        <div className="font-medium text-[#f7f8f8] line-clamp-2">
                          {sub.nombreComercial}
                        </div>
                        {sub.dimensiones && (
                          <span className="inline-block px-1 bg-[#23252a] text-[#8a8f98] rounded text-[10px] font-mono shrink-0">
                            {sub.dimensiones}
                          </span>
                        )}
                      </td>
                      <td className="p-2 text-right font-mono tabular-nums text-[#22c55e]">
                        {disp} {sub.unidadBase}
                      </td>
                      <td className="p-2 text-right font-mono tabular-nums font-bold text-[#f7f8f8]">
                        S/ {sub.precioVenta.toFixed(2)}
                      </td>
                      <td className="p-2 text-center">
                        <button
                          type="button"
                          onClick={() => {
                            onSelectSubstitute(sub);
                            onClose();
                          }}
                          className="px-2 py-1 font-mono font-bold bg-[#e4f222] text-[#08090a] hover:bg-[#d3e015] rounded text-xs flex items-center gap-1 mx-auto"
                        >
                          <Replace className="w-3 h-3" />
                          <span>Reemplazar</span>
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
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
