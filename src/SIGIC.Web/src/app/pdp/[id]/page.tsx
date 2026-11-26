"use client";

import React, { useState, useEffect } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { CheckCircle, XCircle, Calculator, ShoppingCart, ArrowLeft, AlertTriangle } from "lucide-react";
import { ProductImage } from "../../../components/ProductImage";
import { ProductoCatalogo, CalculoPDPResultado } from "../../../types/web";
import { webApi } from "../../../services/api";
import { useAuth } from "../../../context/AuthContext";
import { useCart } from "../../../context/CartContext";

export default function ProductDetailPage() {
  const params = useParams();
  const productoId = params?.id as string;

  const [producto, setProducto] = useState<ProductoCatalogo | null>(null);
  const [cantidad, setCantidad] = useState<number>(1);
  const [unidadSeleccionada, setUnidadSeleccionada] = useState<string>("UND");
  const [cotizacion, setCotizacion] = useState<{ clave: string; datos: CalculoPDPResultado }>();
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const { user } = useAuth();
  const { addItem } = useCart();
  const token = user?.token;
  const clave = JSON.stringify([productoId, cantidad, unidadSeleccionada, token]);
  const calculo = cotizacion?.clave === clave ? cotizacion.datos : null;

  useEffect(() => {
    if (!productoId) return;
    let activo = true;
    setIsLoading(true);
    setProducto(null);
    setError(null);
    webApi.obtenerProducto(productoId)
      .then((data) => {
        if (!activo) return;
        if (!data.disponibleEnWeb) {
          setError("Producto no disponible.");
        } else {
          setProducto(data);
          setUnidadSeleccionada(data.unidadBase || "UND");
        }
      })
      .catch(() => { if (activo) setError("Producto no disponible."); })
      .finally(() => { if (activo) setIsLoading(false); });
    return () => { activo = false; };
  }, [productoId]);

  useEffect(() => {
    if (!producto || producto.id !== productoId) return;
    const controller = new AbortController();
    setCotizacion(undefined);
    setError(null);
    webApi.calcularPrecioPDP({ productoId, cantidad, unidadMedida: unidadSeleccionada }, token, controller.signal)
      .then((datos) => {
        if (!controller.signal.aborted) setCotizacion({ clave, datos });
      })
      .catch(() => {
        if (!controller.signal.aborted) setError("No pudimos calcular el precio. Revise la presentación o vuelva a intentar.");
      });
    return () => controller.abort();
  }, [producto, productoId, cantidad, unidadSeleccionada, token, clave]);

  const handleAddToCart = () => {
    if (!producto || !calculo) {
      setError("El precio aún no está disponible.");
      return;
    }
    if (calculo.cantidadBaseConvertida > calculo.stockDisponible) {
      setError(`Stock insuficiente: solicita ${calculo.cantidadBaseConvertida} ${producto.unidadBase}, disponible: ${calculo.stockDisponible}.`);
      return;
    }
    const added = addItem({
      productoId: producto.id, codigoSku: producto.codigoSku, nombreComercial: producto.nombreComercial,
      unidadMedida: unidadSeleccionada, cantidad, cantidadBase: calculo.cantidadBaseConvertida,
      precioUnitario: calculo.precioFinalUnitario,
      subtotal: calculo.subtotal, stockDisponible: calculo.stockDisponible,
    });
    if (added) {
      setToast("¡Ítem agregado al carrito con éxito!");
      setTimeout(() => setToast(null), 3000);
    } else {
      setError("No se pudo agregar al carrito: supera el stock disponible.");
    }
  };

  if (isLoading) return <div className="p-12 text-center text-xs font-mono text-[#8a8f98]">Cargando producto…</div>;

  if (error && !producto) {
    return (
      <div className="p-8 bg-[#0f1011] border border-[#ef4444] rounded text-xs font-mono text-center flex flex-col items-center gap-4">
        <AlertTriangle className="w-8 h-8 text-[#ef4444]" />
        <div className="text-sm font-bold text-[#ef4444]">{error}</div>
        <Link href="/" className="px-4 py-2 bg-[#23252a] text-[#f7f8f8] hover:bg-[#334155] rounded flex items-center gap-2">
          <ArrowLeft className="w-4 h-4" /> Volver al Catálogo Web
        </Link>
      </div>
    );
  }

  if (!producto) return null;

  const stockDisp = calculo?.stockDisponible ?? producto.stockDisponible;
  const tieneStock = stockDisp > 0;
  const factorActual = calculo ? calculo.factorConversion : null;
  const cantBaseConv = calculo ? calculo.cantidadBaseConvertida : null;
  const stockInsuficiente = cantBaseConv !== null ? cantBaseConv > stockDisp : false;

  return (
    <div className="flex flex-col gap-6 text-xs max-w-4xl mx-auto">
      <Link href="/" className="inline-flex items-center gap-1.5 text-[#8a8f98] hover:text-[#f7f8f8] font-mono transition-colors">
        <ArrowLeft className="w-4 h-4" /> Volver al Catálogo
      </Link>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 bg-[#0f1011] border border-[#23252a] rounded-lg p-6">
        <div className="flex flex-col gap-4 border-b md:border-b-0 md:border-r border-[#23252a] pb-6 md:pb-0 md:pr-6">
          <ProductImage nombre={producto.nombreComercial} imagenUrl={producto.imagenUrl ?? calculo?.imagenUrl} priority />
          <div>
            <div className="flex items-center gap-2 mb-2">
              <span className="font-mono font-bold text-[#e4f222] text-sm">{producto.codigoSku}</span>
              {tieneStock ? (
                <span className="px-2 py-0.5 rounded text-[11px] font-mono font-bold bg-[#22c55e]/20 text-[#22c55e] flex items-center gap-1">
                  <CheckCircle className="w-3.5 h-3.5" /> <span>Disponible: {stockDisp} {producto.unidadBase}</span>
                </span>
              ) : (
                <span className="px-2 py-0.5 rounded text-[11px] font-mono font-bold bg-[#ef4444]/20 text-[#ef4444] flex items-center gap-1">
                  <XCircle className="w-3.5 h-3.5" /> Agotado en Web
                </span>
              )}
            </div>
            <h1 className="text-lg font-bold text-[#f7f8f8] mt-1">{producto.nombreComercial}</h1>
            <p className="text-[#8a8f98] text-[11px] mt-2">
              Presentación: <strong className="text-[#f7f8f8] font-mono">{producto.unidadBase}</strong>
            </p>
          </div>
        </div>

        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-2 font-mono font-bold text-sm text-[#f7f8f8]">
            <Calculator className="w-4 h-4 text-[#e4f222]" /> <span>Calcula tu compra</span>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-[#8a8f98] font-mono block mb-1">UNIDAD COMERCIAL:</label>
              <select
                value={unidadSeleccionada}
                onChange={(e) => setUnidadSeleccionada(e.target.value)}
                className="w-full p-2 bg-[#161718] border border-[#23252a] rounded font-mono text-[#f7f8f8] focus:border-[#e4f222]"
              >
                <option value={producto.unidadBase}>{producto.unidadBase} (Base)</option>
                <option value="Caja">Caja (50 {producto.unidadBase})</option>
                <option value="Ciento">Ciento (100 {producto.unidadBase})</option>
                <option value="Rollo">Rollo (100 {producto.unidadBase})</option>
                <option value="Saco">Saco (42.5 {producto.unidadBase})</option>
              </select>
            </div>
            <div>
              <label className="text-[#8a8f98] font-mono block mb-1">CANTIDAD:</label>
              <input
                type="number" min="1" step="1" value={cantidad}
                onChange={(e) => setCantidad(Math.max(1, parseInt(e.target.value) || 1))}
                className="w-full p-2 bg-[#161718] border border-[#23252a] rounded font-mono sigic-tabular text-[#f7f8f8] focus:border-[#e4f222]"
              />
            </div>
          </div>

          <div className="p-3 bg-[#161718] border border-[#23252a] rounded flex justify-between items-center font-mono">
            <div>
              <span className="text-[#8a8f98] block text-[10px]">CONVERSIÓN BASE (FACTOR {factorActual !== null ? factorActual.toFixed(4) : "—"}):</span>
              <span className="text-sm font-bold text-[#e4f222] sigic-tabular">{cantBaseConv !== null ? `${cantBaseConv} ${producto.unidadBase}` : "—"}</span>
            </div>
            <div className="text-right">
              <span className="text-[#8a8f98] block text-[10px]">STOCK DISPONIBLE:</span>
              <span className={`text-sm font-bold sigic-tabular ${stockInsuficiente ? "text-[#ef4444]" : "text-[#22c55e]"}`}>
                {stockDisp} {producto.unidadBase}
              </span>
            </div>
          </div>

          {calculo && (
            <div className="p-4 bg-[#161718] border border-[#23252a] rounded space-y-2">
              <div className="flex justify-between items-center font-mono text-[11px]">
                <span className="text-[#8a8f98]">Precio Unitario ({unidadSeleccionada}):</span>
                <span className="text-[#f7f8f8] sigic-tabular">S/ {calculo.precioFinalUnitario.toFixed(2)}</span>
              </div>
              {calculo.descuentoUnitario > 0 && (
                <div className="flex justify-between items-center font-mono text-[11px] text-[#22c55e]">
                  <span>Ahorro / Tarifa Preferencial:</span>
                  <span className="sigic-tabular">-{calculo.porcentajeDescuentoTotal}%</span>
                </div>
              )}
              <div className="pt-2 border-t border-[#23252a] flex justify-between items-baseline font-mono">
                <span className="text-[#8a8f98] text-xs">SUBTOTAL CON IGV (18%):</span>
                <span className="text-lg font-bold text-[#f7f8f8] sigic-tabular">S/ {calculo.subtotal.toFixed(2)}</span>
              </div>
            </div>
          )}

          {error && (
            <div className="p-2 bg-[#ef4444]/15 border border-[#ef4444] rounded text-xs text-[#ef4444] font-mono flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 shrink-0" /> <span>{error}</span>
            </div>
          )}
          {toast && (
            <div className="p-2 bg-[#22c55e]/15 border border-[#22c55e] rounded text-xs text-[#22c55e] font-mono flex items-center gap-2">
              <CheckCircle className="w-4 h-4 shrink-0" /> <span>{toast}</span>
            </div>
          )}

          <button
            type="button"
            disabled={!tieneStock || stockInsuficiente || !calculo}
            onClick={handleAddToCart}
            className="w-full py-3 bg-[#e4f222] text-[#08090a] font-mono font-bold text-sm rounded hover:bg-[#d3e015] disabled:opacity-30 disabled:pointer-events-none transition-colors flex items-center justify-center gap-2"
          >
            <ShoppingCart className="w-4 h-4" />
            <span>{!calculo ? "Precio No Disponible" : stockInsuficiente ? "Stock Insuficiente" : "Agregar al Carrito"}</span>
          </button>
        </div>
      </div>
    </div>
  );
}
