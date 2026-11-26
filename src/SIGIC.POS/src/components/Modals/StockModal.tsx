import { useEffect, useState } from "react";
import { FocusTrapModal } from "./FocusTrapModal";
import { api } from "../../services/api";
import type { Product, StockAlmacenDetalle } from "../../types/pos";

interface StockModalProps {
  isOpen: boolean;
  onClose: () => void;
  product: Product | null;
}

type Consulta =
  | { estado: "cargando" }
  | { estado: "error" }
  | { estado: "lista"; almacenes: StockAlmacenDetalle[] };

export function StockModal({ isOpen, onClose, product }: StockModalProps) {
  const [consulta, setConsulta] = useState<Consulta>({ estado: "cargando" });
  const [intento, setIntento] = useState(0);
  const productoId = product?.id;

  useEffect(() => {
    setConsulta({ estado: "cargando" });
    if (!isOpen || !productoId) return;

    const controller = new AbortController();
    api.obtenerStock(undefined, productoId, controller.signal).then((almacenes) => {
      if (!controller.signal.aborted) setConsulta({ estado: "lista", almacenes });
    }).catch(() => {
      if (!controller.signal.aborted) setConsulta({ estado: "error" });
    });

    return () => controller.abort();
  }, [isOpen, productoId, intento]);

  if (!product) return null;

  return (
    <FocusTrapModal isOpen={isOpen} onClose={onClose} title="Disponibilidad por almacén" badge="F5" maxWidth="max-w-lg">
      <div className="flex flex-col gap-3 text-xs">
        <div className="p-3 bg-[#0f1011] border border-[#23252a] rounded">
          <div className="font-mono text-[#8a8f98] mb-0.5">{product.codigoSku}</div>
          <div className="font-medium text-[#f7f8f8] sigic-hardware-desc line-clamp-2">{product.nombreComercial}</div>
          {product.dimensiones && (
            <span className="inline-block mt-1 px-1.5 py-0.5 bg-[#23252a] text-[#8a8f98] rounded text-[10px] font-mono">
              {product.dimensiones}
            </span>
          )}
        </div>

        {consulta.estado === "cargando" && <p role="status">Consultando disponibilidad…</p>}
        {consulta.estado === "error" && (
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p role="alert" className="text-[#fca5a5]">No se pudo consultar la disponibilidad.</p>
            <button type="button" onClick={() => setIntento((actual) => actual + 1)} className="min-h-[44px] px-3 rounded border border-[#23252a] text-[#e4f222]">
              Reintentar
            </button>
          </div>
        )}
        {consulta.estado === "lista" && (consulta.almacenes.length === 0 ? (
          <p role="status">No hay existencias registradas para este producto.</p>
        ) : (
          <div className="overflow-auto rounded border border-[#23252a] sigic-scrollbar">
            <table aria-label="Existencias por almacén" className="w-full text-left border-collapse">
              <thead className="bg-[#0f1011] text-[#8a8f98] font-mono border-b border-[#23252a]">
                <tr>
                  <th scope="col" className="p-2">Almacén</th>
                  <th scope="col" className="p-2 text-right">En almacén</th>
                  <th scope="col" className="p-2 text-right">Reservado</th>
                  <th scope="col" className="p-2 text-right">Disponible</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#18191c]">
                {consulta.almacenes.map((almacen) => (
                  <tr key={almacen.almacenId}>
                    <th scope="row" className="p-2 text-left text-[#f7f8f8]">{almacen.nombreAlmacen}</th>
                    <td className="p-2 text-right font-mono tabular-nums">{almacen.stockFisico} {product.unidadBase}</td>
                    <td className="p-2 text-right font-mono tabular-nums">{almacen.stockComprometido} {product.unidadBase}</td>
                    <td className="p-2 text-right font-mono tabular-nums font-bold text-[#e4f222]">
                      {almacen.stockFisico - almacen.stockComprometido} {product.unidadBase}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}

        <div className="flex justify-end pt-2 border-t border-[#23252a]">
          <button type="button" onClick={onClose} className="min-h-[44px] px-4 bg-[#0f1011] text-[#8a8f98] hover:text-[#f7f8f8] border border-[#23252a] rounded">
            Cerrar (Escape)
          </button>
        </div>
      </div>
    </FocusTrapModal>
  );
}
