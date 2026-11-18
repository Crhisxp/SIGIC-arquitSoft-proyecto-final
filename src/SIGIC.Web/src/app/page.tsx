"use client";

import React, { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { Search, Package, AlertTriangle, CheckCircle, XCircle, ArrowRight } from "lucide-react";
import { ProductoCatalogo } from "../types/web";
import { webApi } from "../services/api";
import { useAuth } from "../context/AuthContext";

export default function CatalogoPage() {
  const [productos, setProductos] = useState<ProductoCatalogo[]>([]);
  const [searchTerm, setSearchTerm] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { rolComercial } = useAuth();

  const cargarCatalogo = useCallback(async (busqueda?: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await webApi.listarProductos(busqueda);
      // Invariante 2 (RF-INV-001): Solo productos con disponibleEnWeb == true
      const webOnly = data.filter((p) => p.disponibleEnWeb);
      setProductos(webOnly);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Error al conectar con la API de Catálogo";
      setError(msg);
      setProductos([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    cargarCatalogo();
  }, [cargarCatalogo]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    cargarCatalogo(searchTerm.trim());
  };

  const getPrecioTarifa = (precioBase: number): number => {
    if (rolComercial === "ContratistaB2B") return Math.round(precioBase * 0.88 * 100) / 100;
    if (rolComercial === "MaestroObra") return Math.round(precioBase * 0.94 * 100) / 100;
    return precioBase;
  };

  return (
    <div className="flex flex-col gap-6">
      {/* Banner de Presentación */}
      <div className="p-6 bg-[#0f1011] border border-[#23252a] rounded-lg flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-bold font-mono text-[#f7f8f8]">
            Catálogo E-Commerce Técnico Ferretero
          </h1>
          <p className="text-xs text-[#8a8f98] mt-1">
            Precios vigentes para canal web según tarifa:{" "}
            <span className="font-bold text-[#e4f222] font-mono">{rolComercial}</span>.
            Disponibilidad asíncrona real sobre Almacén Central (RF-INV-003, RNF-CON-003).
          </p>
        </div>
        <div className="text-xs font-mono bg-[#161718] border border-[#23252a] rounded px-3 py-2 shrink-0">
          <span className="text-[#8a8f98] block">Fórmula de Inventario:</span>
          <span className="text-[#f7f8f8]">Disp = Físico - Comprometido</span>
        </div>
      </div>

      {/* Buscador */}
      <form onSubmit={handleSearchSubmit} className="flex gap-2">
        <div className="relative flex-1">
          <Search className="w-4 h-4 text-[#8a8f98] absolute left-3 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Buscar por código SKU o nombre comercial ferretero..."
            className="w-full pl-9 pr-3 py-2 bg-[#0f1011] border border-[#23252a] rounded text-xs text-[#f7f8f8] font-mono focus:border-[#e4f222] focus:outline-none"
          />
        </div>
        <button
          type="submit"
          disabled={isLoading}
          className="px-4 py-2 bg-[#e4f222] text-[#08090a] font-mono font-bold text-xs rounded hover:bg-[#d3e015] disabled:opacity-40 transition-colors"
        >
          {isLoading ? "Buscando..." : "Buscar"}
        </button>
      </form>

      {/* Estados de Carga y Error */}
      {error && (
        <div className="p-4 bg-[#ef4444]/15 border border-[#ef4444] rounded text-xs text-[#ef4444] flex items-center gap-2 font-mono">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {isLoading && (
        <div className="p-12 text-center text-xs text-[#8a8f98] font-mono">
          Consultando catálogo y disponibilidad asíncrona en servidor...
        </div>
      )}

      {/* Grid de Productos */}
      {!isLoading && !error && (
        <>
          {productos.length === 0 ? (
            <div className="p-12 text-center text-xs text-[#8a8f98] font-mono border border-dashed border-[#23252a] rounded">
              No se encontraron artículos disponibles en el catálogo web para este criterio.
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
              {productos.map((prod) => {
                const stockDisp = prod.stockDisponible;
                const tieneStock = stockDisp > 0;
                const precio = getPrecioTarifa(prod.precioVenta);

                return (
                  <div
                    key={prod.id}
                    className="p-4 bg-[#0f1011] border border-[#23252a] hover:border-[#334155] rounded flex flex-col justify-between transition-colors text-xs"
                  >
                    <div>
                      {/* Cabecera Tarjeta */}
                      <div className="flex justify-between items-start gap-2 mb-2">
                        <span className="font-mono font-bold text-[#e4f222] text-[11px]">
                          {prod.codigoSku}
                        </span>
                        {tieneStock ? (
                          <span className="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-[#22c55e]/20 text-[#22c55e] flex items-center gap-1">
                            <CheckCircle className="w-3 h-3" />
                            <span>Stock: {stockDisp} {prod.unidadBase}</span>
                          </span>
                        ) : (
                          <span className="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-[#ef4444]/20 text-[#ef4444] flex items-center gap-1">
                            <XCircle className="w-3 h-3" />
                            <span>Agotado</span>
                          </span>
                        )}
                      </div>

                      {/* Nombre y Unidad */}
                      <h2 className="font-semibold text-[#f7f8f8] line-clamp-2 min-h-[34px]">
                        {prod.nombreComercial}
                      </h2>
                      <div className="text-[11px] text-[#8a8f98] font-mono mt-1">
                        Unidad Base: <span className="text-[#f7f8f8]">{prod.unidadBase}</span>
                      </div>
                    </div>

                    {/* Precios y Acción */}
                    <div className="mt-4 pt-3 border-t border-[#18191c] flex items-end justify-between">
                      <div>
                        <span className="text-[10px] text-[#8a8f98] font-mono block">
                          PRECIO {rolComercial !== "PublicoGeneral" ? `(${rolComercial})` : "LISTA"}
                        </span>
                        <div className="font-mono sigic-tabular text-base font-bold text-[#f7f8f8]">
                          S/ {precio.toFixed(2)}
                        </div>
                      </div>

                      <Link
                        href={`/pdp/${prod.id}`}
                        className={`px-3 py-1.5 font-mono text-xs font-bold rounded flex items-center gap-1 transition-colors ${
                          tieneStock
                            ? "bg-[#23252a] text-[#e4f222] hover:bg-[#334155]"
                            : "bg-[#18191c] text-[#8a8f98] pointer-events-none opacity-50"
                        }`}
                      >
                        <span>{tieneStock ? "Ver PDP" : "Sin Stock"}</span>
                        <ArrowRight className="w-3.5 h-3.5" />
                      </Link>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </>
      )}
    </div>
  );
}
