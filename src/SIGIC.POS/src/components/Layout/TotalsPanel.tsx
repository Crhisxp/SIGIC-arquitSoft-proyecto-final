import React from "react";
import { User, FileText, CreditCard, Edit3, Percent, Trash2 } from "lucide-react";
import { CartItem, Customer, TipoComprobante } from "../../types/pos";

interface TotalsPanelProps {
  selectedItem: CartItem | null;
  customer: Customer;
  tipoComprobante: TipoComprobante;
  subtotalNeto: number;
  igv: number;
  total: number;
  totalItems: number;
  onOpenCustomer: () => void;
  onOpenQuantity: () => void;
  onOpenDiscount: () => void;
  onRemoveItem: () => void;
  onOpenPayment: () => void;
}

export const TotalsPanel: React.FC<TotalsPanelProps> = ({
  selectedItem,
  customer,
  tipoComprobante,
  subtotalNeto,
  igv,
  total,
  totalItems,
  onOpenCustomer,
  onOpenQuantity,
  onOpenDiscount,
  onRemoveItem,
  onOpenPayment,
}) => {
  return (
    <div className="w-[310px] flex flex-col gap-2 h-full shrink-0">
      {/* Inspector Técnico de Ítem Seleccionado */}
      <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded flex flex-col gap-1.5 shrink-0">
        <div className="flex items-center justify-between text-[11px] font-mono text-[#8a8f98]">
          <span>INSPECTOR DE ÍTEM</span>
          {selectedItem && (
            <span className="text-[#e4f222] font-semibold">{selectedItem.producto.codigoSku}</span>
          )}
        </div>

        {selectedItem ? (
          <div>
            <div className="font-medium text-xs text-[#f7f8f8] sigic-hardware-desc line-clamp-2">
              {selectedItem.producto.nombreComercial}
            </div>
            <div className="flex items-center gap-2 mt-1">
              {selectedItem.producto.dimensiones && (
                <span className="px-1.5 py-0.5 bg-[#23252a] text-[#8a8f98] rounded text-[10px] font-mono shrink-0 sigic-dimension-badge">
                  {selectedItem.producto.dimensiones}
                </span>
              )}
              <span className="text-[10px] font-mono tabular-nums text-[#22c55e]">
                Stock Disp: {selectedItem.producto.stockFisico - selectedItem.producto.stockComprometido} {selectedItem.unidadMedida}
              </span>
            </div>

            {/* Acciones Rápidas sobre el Ítem */}
            <div className="grid grid-cols-3 gap-1 mt-2 pt-2 border-t border-[#23252a]">
              <button
                type="button"
                onClick={onOpenQuantity}
                className="p-1.5 bg-[#161718] hover:bg-[#1c1e22] text-[#8a8f98] hover:text-[#f7f8f8] rounded border border-[#23252a] flex flex-col items-center gap-0.5"
                title="Editar Cantidad (F4)"
              >
                <Edit3 className="w-3.5 h-3.5 text-[#e4f222]" />
                <span className="text-[10px] font-mono">F4 Cant</span>
              </button>
              <button
                type="button"
                onClick={onOpenDiscount}
                className="p-1.5 bg-[#161718] hover:bg-[#1c1e22] text-[#8a8f98] hover:text-[#f7f8f8] rounded border border-[#23252a] flex flex-col items-center gap-0.5"
                title="Descuento (F6)"
              >
                <Percent className="w-3.5 h-3.5 text-[#e4f222]" />
                <span className="text-[10px] font-mono">F6 Dscto</span>
              </button>
              <button
                type="button"
                onClick={onRemoveItem}
                className="p-1.5 bg-[#161718] hover:bg-[#ef4444]/20 text-[#ef4444] rounded border border-[#23252a] flex flex-col items-center gap-0.5"
                title="Eliminar Ítem (F8)"
              >
                <Trash2 className="w-3.5 h-3.5" />
                <span className="text-[10px] font-mono">F8 Borrar</span>
              </button>
            </div>
          </div>
        ) : (
          <div className="text-[11px] text-[#5e636e] py-4 text-center">
            Seleccione una línea para ver especificaciones y atajos de edición rápida
          </div>
        )}
      </div>

      {/* Cliente y Tipo de Comprobante */}
      <div className="p-2.5 bg-[#0f1011] border border-[#23252a] rounded flex flex-col gap-2 shrink-0">
        <button
          type="button"
          onClick={onOpenCustomer}
          className="flex items-center justify-between text-left p-1.5 bg-[#161718] hover:bg-[#1c1e22] border border-[#23252a] rounded transition-colors group"
        >
          <div className="flex items-center gap-2 overflow-hidden">
            <User className="w-4 h-4 text-[#e4f222] shrink-0" />
            <div className="overflow-hidden">
              <div className="text-[10px] font-mono text-[#8a8f98]">CLIENTE (F3)</div>
              <div className="font-semibold text-xs text-[#f7f8f8] truncate">
                {customer.razonSocial}
              </div>
            </div>
          </div>
          <span className="px-1.5 py-0.5 text-[9px] font-mono font-bold bg-[#23252a] text-[#e4f222] rounded shrink-0">
            {customer.tarifa}
          </span>
        </button>

        <div className="flex items-center justify-between px-1 text-xs">
          <span className="text-[#8a8f98] font-mono flex items-center gap-1">
            <FileText className="w-3.5 h-3.5 text-[#8a8f98]" />
            COMPROBANTE:
          </span>
          <span className="font-mono font-bold text-[#f7f8f8] bg-[#161718] px-2 py-0.5 rounded border border-[#23252a]">
            {tipoComprobante}
          </span>
        </div>
      </div>

      {/* Liquidación Monetaria / Totales */}
      <div className="flex-1 p-3 bg-[#0f1011] border border-[#23252a] rounded flex flex-col justify-between">
        <div className="flex flex-col gap-2 font-mono tabular-nums text-xs">
          <div className="flex justify-between text-[#8a8f98] pb-1 border-b border-[#18191c]">
            <span>TOTAL UNIDADES:</span>
            <span className="font-semibold text-[#f7f8f8]">{totalItems}</span>
          </div>
          <div className="flex justify-between text-[#8a8f98]">
            <span>SUBTOTAL NETO (Base):</span>
            <span className="font-semibold text-[#f7f8f8]">S/ {subtotalNeto.toFixed(2)}</span>
          </div>
          <div className="flex justify-between text-[#8a8f98] pb-2 border-b border-[#23252a]">
            <span>IGV (18%):</span>
            <span className="font-semibold text-[#f7f8f8]">S/ {igv.toFixed(2)}</span>
          </div>

          <div className="pt-1">
            <span className="text-xs text-[#8a8f98] block">TOTAL A COBRAR</span>
            <div className="text-3xl font-extrabold text-[#e4f222] tracking-tight">
              S/ {total.toFixed(2)}
            </div>
          </div>
        </div>

        {/* Botón Primario de Cobro (F9) >= 44x44px */}
        <button
          type="button"
          disabled={total <= 0}
          onClick={onOpenPayment}
          className="w-full min-h-[48px] py-3 px-4 font-mono font-bold text-sm bg-[#e4f222] text-[#08090a] hover:bg-[#d3e015] rounded border border-[#e4f222] transition-colors disabled:opacity-30 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg"
        >
          <CreditCard className="w-5 h-5" />
          <span>COBRAR / PAGO MIXTO (F9)</span>
        </button>
      </div>
    </div>
  );
};
