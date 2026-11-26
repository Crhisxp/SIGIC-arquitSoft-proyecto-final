"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { Search, AlertTriangle, CheckCircle, XCircle, ArrowRight } from "lucide-react";
import { ProductImage } from "../components/ProductImage";
import { ProductoCatalogo } from "../types/web";
import { webApi } from "../services/api";
import { useAuth } from "../context/AuthContext";

export default function CatalogoPage() {
  const [catalogo, setCatalogo] = useState<{ token: string | undefined; productos: ProductoCatalogo[] }>();
  const [searchTerm, setSearchTerm] = useState("");
  const [busqueda, setBusqueda] = useState({ term: "" });
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { user } = useAuth();
  const token = user?.token;
  const productos = catalogo?.token === token ? catalogo?.productos ?? [] : [];

  useEffect(() => {
    const controller = new AbortController();
    setIsLoading(true);
    setError(null);
    webApi.listarProductos(busqueda.term, undefined, token, controller.signal).then((data) => {
      if (!controller.signal.aborted)
        setCatalogo({ token, productos: data.filter((producto) => producto.disponibleEnWeb) });
    }).catch(() => {
      if (!controller.signal.aborted) setError("No pudimos cargar el catálogo. Vuelva a intentar la búsqueda.");
    }).finally(() => {
      if (!controller.signal.aborted) setIsLoading(false);
    });
    return () => controller.abort();
  }, [busqueda, token]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setBusqueda({ term: searchTerm.trim() });
  };

  return (
    <div className="flex flex-col gap-6">
      <div className="p-6 bg-[#0f1011] border border-[#23252a] rounded-lg">
        <h1 className="text-2xl font-bold text-[#f7f8f8]">Materiales y herramientas en Ayacucho</h1>
        <p className="text-sm text-[#8a8f98] mt-1">Compre en línea y elija retiro en tienda o despacho a domicilio.</p>
      </div>

      {/* Buscador */}
      <form onSubmit={handleSearchSubmit} className="flex gap-2">
        <div className="relative flex-1">
          <Search className="w-4 h-4 text-[#8a8f98] absolute left-3 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            aria-label="Buscar productos"
            placeholder="Buscar por nombre o código de producto"
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
          Cargando productos…
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

                return (
                  <div
                    key={prod.id}
                    className="p-4 bg-[#0f1011] border border-[#23252a] hover:border-[#334155] rounded flex flex-col justify-between transition-colors text-xs"
                  >
                    <div>
                      <Link href={`/pdp/${prod.id}`} className="mb-3 block">
                        <ProductImage nombre={prod.nombreComercial} imagenUrl={prod.imagenUrl} />
                      </Link>
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
                        Presentación: <span className="text-[#f7f8f8]">{prod.unidadBase}</span>
                      </div>
                    </div>

                    {/* Precios y Acción */}
                    <div className="mt-4 pt-3 border-t border-[#18191c] flex items-end justify-between">
                      <div>
                        <span className="text-[10px] text-[#8a8f98] font-mono block">
                          Precio unitario
                        </span>
                        <div className="font-mono sigic-tabular text-base font-bold text-[#f7f8f8]">
                          S/ {prod.precioVenta.toFixed(2)}
                        </div>
                      </div>

                      <Link
                        href={`/pdp/${prod.id}`}
                        className="px-3 py-1.5 font-mono text-xs font-bold rounded flex items-center gap-1 transition-colors bg-[#23252a] text-[#e4f222] hover:bg-[#334155]"
                      >
                        <span>Ver producto</span>
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
