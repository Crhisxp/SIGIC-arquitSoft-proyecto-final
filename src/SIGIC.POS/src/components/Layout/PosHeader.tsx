import React, { useEffect, useState, forwardRef } from "react";
import { Barcode, Wifi, Clock, Store, ShieldCheck } from "lucide-react";

interface PosHeaderProps {
  cajeroNombre: string;
  cajaNumero: string;
  turnoId: string;
  almacenNombre: string;
  barcodeInput: string;
  onBarcodeChange: (val: string) => void;
  onBarcodeSubmit: (e: React.FormEvent) => void;
  onOpenSearch: () => void;
  onOpenDispatch: () => void;
}

export const PosHeader = forwardRef<HTMLInputElement, PosHeaderProps>(
  (
    {
      cajeroNombre,
      cajaNumero,
      turnoId,
      almacenNombre,
      barcodeInput,
      onBarcodeChange,
      onBarcodeSubmit,
      onOpenSearch,
      onOpenDispatch,
    },
    ref
  ) => {
    const [currentTime, setCurrentTime] = useState("");

    useEffect(() => {
      const update = () => {
        const now = new Date();
        setCurrentTime(now.toLocaleTimeString("es-PE", { hour12: false }));
      };
      update();
      const interval = setInterval(update, 1000);
      return () => clearInterval(interval);
    }, []);

    return (
      <div className="flex flex-col shrink-0 border-b border-[#23252a]">
        {/* Franja A: Telemetría y Estado de Conexión (36px) */}
        <div className="h-[36px] bg-[#0f1011] px-4 flex items-center justify-between text-xs text-[#8a8f98] font-mono border-b border-[#18191c]">
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-1.5 text-[#f7f8f8] font-bold">
              <Store className="w-4 h-4 text-[#e4f222]" />
              <span>SIGIC POS v2.2</span>
              <span className="text-[#5e636e] font-normal">• {almacenNombre}</span>
            </div>
            <div className="flex items-center gap-1.5 text-[#8a8f98]">
              <ShieldCheck className="w-3.5 h-3.5 text-[#38bdf8]" />
              <span>{cajeroNombre}</span>
              <span className="text-[#5e636e]">({cajaNumero})</span>
            </div>
            <div className="text-[11px] text-[#8a8f98]">
              Turno: <span className="text-[#e4f222]">{turnoId}</span>
            </div>
          </div>

          <div className="flex items-center gap-4 text-[11px]">
            <div className="flex items-center gap-1.5 text-[#22c55e]">
              <span className="w-2 h-2 rounded-full bg-[#22c55e] animate-pulse" />
              <Wifi className="w-3.5 h-3.5" />
              <span>LAN POS (pool_pos)</span>
            </div>
            <div className="flex items-center gap-1 text-[#f7f8f8] tabular-nums font-semibold">
              <Clock className="w-3.5 h-3.5 text-[#8a8f98]" />
              <span>{currentTime}</span>
            </div>
          </div>
        </div>

        {/* Franja B: Escáner y Entrada EAN-13 (52px) */}
        <div className="h-[52px] bg-[#161718] px-4 py-2 flex items-center gap-3">
          <form onSubmit={onBarcodeSubmit} className="flex-1 flex items-center gap-2">
            <div className="relative flex-1">
              <Barcode className="absolute left-3 top-2.5 w-5 h-5 text-[#e4f222]" />
              <input
                ref={ref}
                type="text"
                value={barcodeInput}
                onChange={(e) => onBarcodeChange(e.target.value)}
                placeholder="[F1] Escanear EAN-13 o digitar código SKU..."
                className="w-full pl-11 pr-4 py-1.5 bg-[#0f1011] border border-[#23252a] rounded font-mono text-sm text-[#f7f8f8] placeholder-[#5e636e] focus:border-[#e4f222] focus:bg-[#0f1011]"
                autoComplete="off"
              />
            </div>
            <button
              type="submit"
              className="px-4 py-2 font-mono font-bold text-xs bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] transition-colors shrink-0"
            >
              Enter / Agregar
            </button>
          </form>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onOpenSearch}
              className="px-3 py-2 font-mono text-xs bg-[#0f1011] text-[#8a8f98] hover:text-[#f7f8f8] hover:bg-[#1c1e22] rounded border border-[#23252a] transition-colors shrink-0 flex items-center gap-1.5"
            >
              <span className="text-[#e4f222] font-bold">[F2]</span>
              <span>Buscar</span>
            </button>
            <button
              type="button"
              onClick={onOpenDispatch}
              className="px-3 py-2 font-mono text-xs bg-[#0f1011] text-[#38bdf8] hover:bg-[#1c1e22] rounded border border-[#23252a] transition-colors shrink-0 flex items-center gap-1.5"
            >
              <span className="font-bold">[F6]</span>
              <span>Despacho Web</span>
            </button>
          </div>
        </div>
      </div>
    );
  }
);

PosHeader.displayName = "PosHeader";
