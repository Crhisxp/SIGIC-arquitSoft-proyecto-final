"use client";

import React, { useState, useEffect, useCallback } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Wrench, CheckCircle, XCircle, Calculator, ShoppingCart, ArrowLeft, AlertTriangle } from "lucide-react";
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
  const [calculo, setCalculo] = useState<CalculoPDPResultado | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const { user, rolComercial } = useAuth();
  const { addItem } = useCart();

  useEffect(() => {
    if (!productoId) return;
    setIsLoading(true);
    webApi.obtenerProducto(productoId)
      .then((data) => {
        if (!data.disponibleEnWeb) {
          setError("Este producto es exclusivo de mostrador presencial (RF-INV-001).");
          setProducto(null);
        } else {
          setProducto(data);
          setUnidadSeleccionada(data.unidadBase || "UND");
        }
      })
      .catch((err) => setError(err instanceof Error ? err.message : "Producto no encontrado (404)"))
      .finally(() => setIsLoading(false));
  }, [productoId]);

  const recalcularPrecio = useCallback(async () => {
    if (!productoId || cantidad <= 0) return;
    try {
      const res = await webApi.calcularPrecioPDP({
        productoId, cantidad, unidadMedida: unidadSeleccionada, rolCliente: rolComercial, clienteId: user?.userId,
      });
      setCalculo(res);
      setError(null);
    } catch (err: unknown) {
      setCalculo(null);
      setError(err instanceof Error ? err.message : "Error al calcular precio vigente en el servidor.");
    }
  }, [productoId, cantidad, unidadSeleccionada, rolComercial, user?.userId]);

  useEffect(() => { if (producto) recalcularPrecio(); }, [producto, recalcularPrecio]);

  const handleAddToCart = () => {
    if (!producto || !calculo) {
      setError("No hay cálculo de precio vigente del servidor para agregar al carrito.");
      return;
    }
    if (calculo.cantidadBaseConvertida > calculo.stockDisponible) {
      setError(`Stock insuficiente: solicita ${calculo.cantidadBaseConvertida} ${producto.unidadBase}, disponible: ${calculo.stockDisponible}.`);
      return;
    }
    const added = addItem({
      productoId: producto.id, codigoSku: producto.codigoSku, nombreComercial: producto.nombreComercial,
      unidadMedida: unidadSeleccionada, cantidad, cantidadBase: calculo.cantidadBaseConvertida,
      precioUnitario: calculo.precioUnitarioComercial || calculo.precioFinalUnitario,
      subtotal: calculo.subtotal, stockDisponible: calculo.stockDisponible,
    });
    if (added) {
      setToast("¡Ítem agregado al carrito con éxito!");
      setTimeout(() => setToast(null), 3000);
    } else {
      setError("No se pudo agregar al carrito: supera el stock disponible.");
    }
  };

  if (isLoading) return <div className="p-12 text-center text-xs font-mono text-[#8a8f98]">Cargando Ficha Técnica PDP...</div>;

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

  const stockDisp = producto.stockDisponible;
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
        <div className="flex flex-col justify-between border-b md:border-b-0 md:border-r border-[#23252a] pb-6 md:pb-0 md:pr-6">
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
              Especificación técnica oficial. Unidad base: <strong className="text-[#f7f8f8] font-mono">{producto.unidadBase}</strong>.
            </p>
          </div>
          <div className="mt-6 p-4 bg-[#161718] border border-[#23252a] rounded space-y-2">
            <div className="font-mono text-[#8a8f98] font-semibold flex items-center gap-1.5">
              <Wrench className="w-4 h-4 text-[#e4f222]" /> GOBERNANZA DE PRECIOS Y STOCK (MÓDULO 5)
            </div>
            <div className="grid grid-cols-2 gap-2 text-[11px] font-mono">
              <div><span className="text-[#8a8f98] block">Tarifa:</span><span className="text-[#f7f8f8] font-bold">{rolComercial}</span></div>
              <div><span className="text-[#8a8f98] block">Almacén:</span><span className="text-[#f7f8f8]">Central Ayacucho</span></div>
            </div>
          </div>
        </div>

        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-2 font-mono font-bold text-sm text-[#f7f8f8]">
            <Calculator className="w-4 h-4 text-[#e4f222]" /> <span>Calculadora de Conversiones (RF-INV-005)</span>
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
                <span className="text-[#f7f8f8] sigic-tabular">S/ {(calculo.precioUnitarioComercial || calculo.precioFinalUnitario).toFixed(2)}</span>
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
