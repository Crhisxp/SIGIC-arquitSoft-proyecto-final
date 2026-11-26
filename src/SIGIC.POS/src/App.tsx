import React, { useState, useRef, useCallback, useEffect } from "react";
import { useCart } from "./hooks/useCart";
import { useHotkeys } from "./hooks/useHotkeys";
import { usePosModals } from "./hooks/usePosModals";
import { useTurnoActual } from "./hooks/useTurnoActual";
import { PosHeader } from "./components/Layout/PosHeader";
import { CartTable } from "./components/Layout/CartTable";
import { TotalsPanel } from "./components/Layout/TotalsPanel";
import { HotkeyBar } from "./components/Layout/HotkeyBar";
import { PosModalContainer } from "./components/Modals/PosModalContainer";
import { INITIAL_PRODUCTS } from "./data/mockProducts";
import {
  Product,
  PaymentEntry,
  TipoComprobante,
  DespachoWebPedido,
  CashDenomination,
} from "./types/pos";
import { api } from "./services/api";

export const App: React.FC = () => {
  const [products, setProducts] = useState<Product[]>(INITIAL_PRODUCTS);
  const [barcodeInput, setBarcodeInput] = useState("");
  const [pedidosWeb, setPedidosWeb] = useState<DespachoWebPedido[]>([]);
  const [toast, setToast] = useState<{ msg: string; err?: boolean } | null>(null);

  const scannerRef = useRef<HTMLInputElement>(null);
  const modals = usePosModals();
  const { turno: turnoActivo, estado: estadoTurno, marcarCerrado } = useTurnoActual();
  const almacenActivoId = turnoActivo?.almacenId;

  useEffect(() => {
    if (!almacenActivoId) return;
    let active = true;
    api.obtenerStockAlmacen(almacenActivoId).then((rows) => {
      if (!active || !Array.isArray(rows)) return;
      setProducts((prev) =>
        prev.map((p) => {
          const row = rows.find(
            (r: any) =>
              (r.productoId && r.productoId.toLowerCase() === p.id.toLowerCase()) ||
              (r.id && r.id.toLowerCase() === p.id.toLowerCase()) ||
              (r.codigoSku && r.codigoSku.toLowerCase() === p.codigoSku.toLowerCase())
          );
          if (!row) return p;
          const f = Number(row.stockFisico);
          const c = Number(row.stockComprometido);
          return { ...p, stockFisico: f, stockComprometido: c, stockDisponible: f - c };
        })
      );
    }).catch(() => {});
    return () => { active = false; };
  }, [almacenActivoId]);

  const {
    items,
    selectedIndex,
    setSelectedIndex,
    customer,
    setCustomer,
    tipoComprobante,
    setTipoComprobante,
    heldTickets,
    addItem,
    updateQuantity,
    updateDiscount,
    removeItem,
    clearCart,
    selectPrevious,
    selectNext,
    holdTicket,
    restoreTicket,
    purgeExpiredTickets,
    total,
    subtotalNeto,
    igv,
    totalItems,
  } = useCart();

  const selectedItem = items[selectedIndex] || null;

  const showToast = useCallback((msg: string, err = false) => {
    setToast({ msg, err });
    setTimeout(() => setToast(null), 3000);
  }, []);

  const refocusScanner = useCallback(() => {
    setTimeout(() => scannerRef.current?.focus(), 40);
  }, []);

  const handleBarcodeSubmit = (e?: React.FormEvent) => {
    e?.preventDefault();
    const code = barcodeInput.trim();
    if (!code) return;

    const found = products.find(
      (p) =>
        p.codigoBarras === code ||
        p.codigoSku.toLowerCase() === code.toLowerCase()
    );

    if (found) {
      const added = addItem(found, 1);
      if (added) {
        showToast(`Agregado: ${found.nombreComercial}`);
      } else {
        showToast(`Stock insuficiente para ${found.codigoSku}`, true);
      }
      setBarcodeInput("");
    } else {
      showToast(`Código no encontrado: ${code}. Abriendo búsqueda...`, true);
      modals.setIsSearchOpen(true);
    }
  };

  const handleConfirmSale = async (pagos: PaymentEntry[], tipoComp: TipoComprobante) => {
    if (!turnoActivo) throw new Error("Se necesita un turno abierto para cobrar.");
    try {
      const res = await api.registrarVenta({
        turnoCajaId: turnoActivo.turnoId,
        clienteId: customer.id,
        items,
        pagos,
        tipoComprobante: tipoComp,
      });
      showToast(res.numeroComprobante ? `Venta registrada. Comprobante: ${res.numeroComprobante}` : "Venta registrada.");
      clearCart();
    } finally {
      refocusScanner();
    }
  };

  const handleConfirmClose = async (params: {
    turnoCajaId: string;
    desgloseFisico: CashDenomination[];
    totalDigitalDeclarado: number;
    justificacion?: string;
  }) => {
    if (!turnoActivo || params.turnoCajaId !== turnoActivo.turnoId)
      throw new Error("Se necesita un turno abierto para cerrar la caja.");

    const cierre = await api.cerrarTurnoArqueoCiego(params);
    marcarCerrado();
    return cierre;
  };

  const acciones = {
    onF1: () => scannerRef.current?.focus(),
    onF2: () => modals.setIsSearchOpen(true),
    onF3: () => modals.setIsCustomerOpen(true),
    onF4: () => items.length > 0 && modals.setIsQuantityOpen(true),
    onF5: () => selectedItem && modals.setIsStockOpen(true),
    onF6: () => (selectedItem ? modals.setIsDiscountOpen(true) : modals.setIsDispatchOpen(true)),
    onF7: () => selectedItem && modals.setIsSubstitutesOpen(true),
    onF8: () => items.length > 0 && removeItem(selectedIndex),
    onF9: () => {
      if (total <= 0) return;
      if (!turnoActivo) return showToast("Se necesita un turno abierto para cobrar.", true);
      modals.setIsPaymentOpen(true);
    },
    onF10: () => setTipoComprobante((prev) => (prev === "BOLETA" ? "FACTURA" : "BOLETA")),
    onF11: () => modals.setIsHeldOpen(true),
    onF12: () => {
      if (!turnoActivo) return showToast("Sin turno abierto para cerrar la caja.", true);
      modals.setIsBlindCloseOpen(true);
    },
  };

  useHotkeys(
    {
      ...acciones,
      onEscape: () => {
        modals.closeAllModals();
        refocusScanner();
      },
      onArrowUp: selectPrevious,
      onArrowDown: selectNext,
    },
    modals.anyModalOpen
  );

  return (
    <div className="w-full h-screen flex flex-col overflow-hidden bg-[#0f172a] text-[#f7f8f8] font-sans antialiased select-none">
      <PosHeader
        ref={scannerRef}
        turno={turnoActivo}
        estadoTurno={estadoTurno}
        barcodeInput={barcodeInput}
        onBarcodeChange={setBarcodeInput}
        onBarcodeSubmit={handleBarcodeSubmit}
        onOpenSearch={acciones.onF2}
        onOpenDispatch={() => modals.setIsDispatchOpen(true)}
      />

      <main className="grid flex-1 min-h-0 min-w-0 grid-cols-1 grid-rows-[minmax(0,1fr)_minmax(0,45%)] gap-3 overflow-hidden px-3 py-2 md:grid-cols-[minmax(0,1fr)_clamp(15rem,28vw,26rem)] md:grid-rows-1">
        <CartTable
          items={items}
          selectedIndex={selectedIndex}
          onSelectItem={setSelectedIndex}
          onRemoveItem={removeItem}
        />
        <TotalsPanel
          selectedItem={selectedItem}
          customer={customer}
          tipoComprobante={tipoComprobante}
          subtotalNeto={subtotalNeto}
          igv={igv}
          total={total}
          totalItems={totalItems}
          onOpenCustomer={() => modals.setIsCustomerOpen(true)}
          onOpenQuantity={() => modals.setIsQuantityOpen(true)}
          onOpenDiscount={() => modals.setIsDiscountOpen(true)}
          onRemoveItem={() => removeItem(selectedIndex)}
          onOpenPayment={acciones.onF9}
        />
      </main>

      <HotkeyBar
        {...acciones}
        disabledF4={items.length === 0}
        disabledF8={items.length === 0}
        disabledF9={total <= 0}
      />

      {toast && (
        <div
          role={toast.err ? "alert" : "status"}
          className={`fixed bottom-16 right-4 z-50 px-4 py-2 rounded shadow-2xl font-mono text-xs tabular-nums shrink-0 line-clamp-2 border ${
            toast.err ? "bg-[#ef4444] text-white border-red-400" : "bg-[#e4f222] text-[#08090a] border-yellow-200"
          }`}
        >
          {toast.msg}
        </div>
      )}

      <PosModalContainer
        {...modals}
        products={products}
        addItem={addItem}
        customer={customer}
        setCustomer={setCustomer}
        selectedItem={selectedItem}
        selectedIndex={selectedIndex}
        updateQuantity={updateQuantity}
        updateDiscount={updateDiscount}
        removeItem={removeItem}
        total={total}
        tipoComprobante={tipoComprobante}
        handleConfirmSale={handleConfirmSale}
        heldTickets={heldTickets}
        itemsCount={items.length}
        holdTicket={holdTicket}
        restoreTicket={restoreTicket}
        purgeExpiredTickets={purgeExpiredTickets}
        turnoCajaId={turnoActivo?.turnoId ?? ""}
        handleConfirmClose={handleConfirmClose}
        pedidosWeb={pedidosWeb}
        setPedidosWeb={setPedidosWeb}
        refocusScanner={refocusScanner}
        showToast={showToast}
      />
    </div>
  );
};
