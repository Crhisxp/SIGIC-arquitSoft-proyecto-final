"use client";

import React, { useState, useEffect, useCallback } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Clock, Upload, CheckCircle, AlertTriangle, ArrowLeft, FileText, Truck, ShieldAlert } from "lucide-react";
import { PedidoWebDetalle, TTL_CONFIG } from "../../../types/web";
import { webApi, ApiError } from "../../../services/api";
import { useAuth } from "../../../context/AuthContext";

const estados: Record<string, string> = {
  PendientePago: "Pendiente de pago",
  PendienteValidacion: "En revisión",
  PagadoConfirmado: "Pago confirmado",
  EnPreparacion: "En preparación",
  ListoParaRetiro: "Listo para retirar",
  Entregado: "Entregado",
  Cancelado: "Cancelado",
  CanceladoPorExpiracion: "Reserva vencida",
  Expirado: "Reserva vencida",
  Rechazado: "Pago rechazado",
};

export default function PedidoTrackingPage() {
  const params = useParams();
  const pedidoId = params?.id as string;
  const { user, isAuthenticated } = useAuth();

  const [pedido, setPedido] = useState<PedidoWebDetalle | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const [timeRemainingSec, setTimeRemainingSec] = useState<number>(0);
  const [voucherTimeSec, setVoucherTimeSec] = useState<number>(0);

  const cargarPedido = useCallback(async () => {
    if (!pedidoId || !user?.token) return;
    setIsLoading(true);
    try {
      const data = await webApi.obtenerEstadoPedido(pedidoId, user.token);
      setPedido(data);
      if (data.fechaExpiracionUtc) {
        setTimeRemainingSec(Math.max(0, Math.floor((new Date(data.fechaExpiracionUtc).getTime() - Date.now()) / 1000)));
      }
      if (data.fechaLimiteVoucherUtc) {
        setVoucherTimeSec(Math.max(0, Math.floor((new Date(data.fechaLimiteVoucherUtc).getTime() - Date.now()) / 1000)));
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Error al obtener estado del pedido");
    } finally {
      setIsLoading(false);
    }
  }, [pedidoId, user?.token]);

  useEffect(() => {
    cargarPedido();
  }, [cargarPedido]);

  // Temporizadores de reserva y comprobante.
  useEffect(() => {
    if (!pedido) return;

    const interval = setInterval(() => {
      const nowMs = Date.now();
      const expMs = new Date(pedido.fechaExpiracionUtc).getTime();
      const diffSec = Math.max(0, Math.floor((expMs - nowMs) / 1000));
      setTimeRemainingSec(diffSec);

      if (pedido.fechaLimiteVoucherUtc) {
        const voucherLimitMs = new Date(pedido.fechaLimiteVoucherUtc).getTime();
        const diffVoucher = Math.max(0, Math.floor((voucherLimitMs - nowMs) / 1000));
        setVoucherTimeSec(diffVoucher);
      }
    }, 1000);

    return () => clearInterval(interval);
  }, [pedido]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (!selected) return;

    if (selected.size > TTL_CONFIG.MAX_VOUCHER_BYTES) {
      setError("El comprobante no debe superar los 5 MB.");
      setFile(null);
      return;
    }

    const ext = "." + selected.name.split(".").pop()?.toLowerCase();
    if (!TTL_CONFIG.FORMATOS_VOUCHER.includes(ext)) {
      setError(`Formato '${ext}' no permitido. Solo se aceptan archivos JPG, PNG y PDF.`);
      setFile(null);
      return;
    }

    setError(null);
    setFile(selected);
  };

  const handleUploadVoucher = async () => {
    if (!file || !pedidoId || !user?.token) return;
    if (voucherTimeSec <= 0 && pedido?.metodoPago !== "PasarelaTarjeta") {
      setError("La ventana máxima de 60 minutos para adjuntar el comprobante ha expirado.");
      return;
    }

    setIsUploading(true);
    setError(null);
    setSuccessMsg(null);

    try {
      const res = await webApi.subirVoucher(pedidoId, file, user.token);
      setSuccessMsg(res.mensaje || "Comprobante subido exitosamente.");
      setFile(null);
      await cargarPedido();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Fallo al subir comprobante");
    } finally {
      setIsUploading(false);
    }
  };

  const formatTimer = (sec: number) => {
    const h = Math.floor(sec / 3600);
    const m = Math.floor((sec % 3600) / 60);
    const s = sec % 60;
    if (h > 0) return `${h}h ${m.toString().padStart(2, "0")}m ${s.toString().padStart(2, "0")}s`;
    return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  };

  if (!isAuthenticated) {
    return (
      <div className="p-8 text-center text-xs font-mono bg-[#0f1011] border border-[#23252a] rounded">
        Inicie sesión como Cliente Web para acceder a esta orden.
      </div>
    );
  }

  if (isLoading) {
    return <div className="p-12 text-center text-xs font-mono text-[#8a8f98]">Consultando estado de pedido y guía...</div>;
  }

  if (!pedido) {
    return (
      <div className="p-8 bg-[#0f1011] border border-[#ef4444] rounded text-xs font-mono text-center">
        {error || "Pedido no encontrado o sin autorización."}
      </div>
    );
  }

  const isManual = pedido.metodoPago !== "PasarelaTarjeta";
  const voucherExpired = isManual && voucherTimeSec <= 0 && !pedido.fechaSubidaVoucherUtc;
  const itemsSum = (pedido.items || []).reduce((s, it) => s + it.cantidad * it.precioUnitario, 0);
  const totalDisplay = (pedido.total ?? itemsSum).toFixed(2);
  const codigoDisplay = pedido.codigo || `PED-${pedido.id.substring(0, 8).toUpperCase()}`;

  return (
    <div className="flex flex-col gap-6 text-xs max-w-4xl mx-auto">
      <div className="flex justify-between items-center">
        <Link href="/" className="inline-flex items-center gap-1.5 text-[#8a8f98] hover:text-[#f7f8f8] font-mono transition-colors">
          <ArrowLeft className="w-4 h-4" /> Ir al Catálogo
        </Link>
        <span className="font-mono text-[#8a8f98]">Seguimiento del pedido</span>
      </div>

      <div className="p-6 bg-[#0f1011] border border-[#23252a] rounded-lg space-y-6">
        <div className="flex justify-between items-start border-b border-[#23252a] pb-4">
          <div>
            <div className="flex items-center gap-2">
              <span className="font-mono font-bold text-sm text-[#e4f222]">{codigoDisplay}</span>
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-[#161718] border border-[#23252a] text-[#f7f8f8]">
                {estados[pedido.estado] ?? "Estado en actualización"}
              </span>
            </div>
            <div className="text-[11px] text-[#8a8f98] font-mono mt-1">
              Pago: {pedido.metodoPago === "PasarelaTarjeta" ? "Tarjeta" : pedido.metodoPago === "TransferenciaBancaria" ? "Transferencia bancaria" : pedido.metodoPago}
              {" · Entrega: "}{pedido.modalidadEntrega === "DespachoDomicilio" ? "Despacho a domicilio" : "Retiro en tienda"}
            </div>
          </div>
          <div className="text-right font-mono sigic-tabular">
            <span className="text-[10px] text-[#8a8f98] block">TOTAL ORDEN</span>
            <span className="text-base font-bold text-[#f7f8f8]">S/ {totalDisplay}</span>
          </div>
        </div>

        {/* Temporizadores de Reserva en Vivo */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="p-4 bg-[#161718] border border-[#23252a] rounded space-y-1">
            <span className="text-[#8a8f98] font-mono text-[11px] flex items-center gap-1.5">
              <Clock className="w-3.5 h-3.5 text-[#e4f222]" /> TIEMPO RESTANTE DE RESERVA DE STOCK
            </span>
            <div className="text-lg font-bold font-mono sigic-tabular text-[#f7f8f8]">
              {timeRemainingSec > 0 ? formatTimer(timeRemainingSec) : <span className="text-[#ef4444]">RESERVA EXPIRADA</span>}
            </div>
          </div>

          {isManual && (
            <div className="p-4 bg-[#161718] border border-[#23252a] rounded space-y-1">
              <span className="text-[#8a8f98] font-mono text-[11px] flex items-center gap-1.5">
                <FileText className="w-3.5 h-3.5 text-[#38bdf8]" /> VENTANA PARA SUBIR COMPROBANTE
              </span>
              <div className="text-lg font-bold font-mono sigic-tabular text-[#f7f8f8]">
                {pedido.fechaSubidaVoucherUtc ? (
                  <span className="text-[#22c55e] flex items-center gap-1 text-sm"><CheckCircle className="w-4 h-4" /> Comprobante Recibido</span>
                ) : voucherTimeSec > 0 ? (
                  formatTimer(voucherTimeSec)
                ) : (
                  <span className="text-[#ef4444] text-sm">VENTANA 60 MIN EXPIRADA</span>
                )}
              </div>
              <span className="text-[10px] text-[#8a8f98] block font-mono">Límite: 60 minutos desde creación</span>
            </div>
          )}
        </div>

        {/* Sección de Guía de Traslado Interno si Retiro en Tienda */}
        {pedido.numeroGuia && (
          <div className="p-4 bg-[#161718] border border-[#38bdf8]/40 rounded space-y-1 font-mono">
            <div className="text-[#38bdf8] font-bold flex items-center gap-1.5">
              <Truck className="w-4 h-4" /> Guía de traslado: {pedido.numeroGuia}
            </div>
            <p className="text-[11px] text-[#8a8f98]">
              Presente su DNI al recoger su pedido en la tienda.
            </p>
          </div>
        )}

        {/* Formulario de Carga de Comprobante (Manual) */}
        {isManual && !pedido.fechaSubidaVoucherUtc && (
          <div className="p-4 bg-[#161718] border border-[#23252a] rounded space-y-3">
            <div className="font-mono font-bold text-sm text-[#f7f8f8] flex items-center gap-1.5">
              <Upload className="w-4 h-4 text-[#e4f222]" />
              <span>Adjuntar Constancia de Pago (Yape / Plin / Transferencia)</span>
            </div>
            <p className="text-[11px] text-[#8a8f98]">
              Cargue captura o documento oficial de pago (máx. 5 MB en formato JPG, PNG o PDF) dentro de la ventana de 60 minutos.
            </p>

            <div className="flex flex-col sm:flex-row items-center gap-3">
              <input
                type="file"
                accept=".jpg,.jpeg,.png,.pdf"
                disabled={voucherExpired || isUploading}
                onChange={handleFileChange}
                className="w-full text-xs font-mono text-[#8a8f98] file:mr-3 file:py-2 file:px-4 file:rounded file:border-0 file:text-xs file:font-mono file:font-bold file:bg-[#23252a] file:text-[#f7f8f8] hover:file:bg-[#334155] file:cursor-pointer"
              />
              <button
                type="button"
                disabled={!file || voucherExpired || isUploading}
                onClick={handleUploadVoucher}
                className="w-full sm:w-auto px-5 py-2 bg-[#e4f222] text-[#08090a] font-mono font-bold rounded hover:bg-[#d3e015] disabled:opacity-40 transition-colors shrink-0"
              >
                {isUploading ? "Subiendo…" : "Subir comprobante"}
              </button>
            </div>
          </div>
        )}

        {error && (
          <div className="p-3 bg-[#ef4444]/15 border border-[#ef4444] rounded text-[#ef4444] font-mono flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {successMsg && (
          <div className="p-3 bg-[#22c55e]/15 border border-[#22c55e] rounded text-[#22c55e] font-mono flex items-center gap-2">
            <CheckCircle className="w-4 h-4 shrink-0" />
            <span>{successMsg}</span>
          </div>
        )}
      </div>
    </div>
  );
}
