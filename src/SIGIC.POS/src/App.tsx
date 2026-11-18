import React, { useState, useRef, useCallback, useEffect } from "react";
import { useCart } from "./hooks/useCart";
import { useHotkeys } from "./hooks/useHotkeys";
import { usePosModals } from "./hooks/usePosModals";
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
  TurnoCaja,
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

  const turnoActivo: TurnoCaja = {
    turnoId: "TRN-AYAC-8841",
    cajaId: "CAJA-01",
    cajeroId: "00000000-0000-0000-0000-000000000010",
    cajeroNombre: "Crhistian Flores",
    almacenId: "00000000-0000-0000-0000-000000000001",
    almacenNombre: "Sede Central Ayacucho",
    fondoInicial: 250.0,
    estado: "Aperturado",
    fechaAperturaUtc: new Date().toISOString(),
  };

  useEffect(() => {
    let active = true;
    api.obtenerStockAlmacen(turnoActivo.almacenId).then((rows) => {
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
  }, [turnoActivo.almacenId]);

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
    try {
      const res = await api.registrarVenta({
        turnoCajaId: turnoActivo.turnoId,
        clienteId: customer.id,
        items,
        pagos,
        tipoComprobante: tipoComp,
      });
      showToast(`¡Venta Registrada! Comprobante: ${res.numeroComprobante || "B001-000123"}`);
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
    return await api.cerrarTurnoArqueoCiego(params);
  };

  useHotkeys(
    {
      onF1: () => scannerRef.current?.focus(),
      onF2: () => modals.setIsSearchOpen(true),
      onF3: () => modals.setIsCustomerOpen(true),
      onF4: () => items.length > 0 && modals.setIsQuantityOpen(true),
      onF5: () => selectedItem && modals.setIsStockOpen(true),
      onF6: () => (selectedItem ? modals.setIsDiscountOpen(true) : modals.setIsDispatchOpen(true)),
      onF7: () => selectedItem && modals.setIsSubstitutesOpen(true),
      onF8: () => items.length > 0 && removeItem(selectedIndex),
      onF9: () => total > 0 && modals.setIsPaymentOpen(true),
      onF10: () => setTipoComprobante((prev) => (prev === "BOLETA" ? "FACTURA" : "BOLETA")),
      onF11: () => modals.setIsHeldOpen(true),
      onF12: () => modals.setIsBlindCloseOpen(true),
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
    <div className="w-[1024px] h-[768px] max-w-[1024px] max-h-[768px] overflow-hidden overflow-x-hidden flex flex-col bg-[#0f172a] text-[#f7f8f8] font-sans antialiased select-none">
      <PosHeader
        ref={scannerRef}
        cajeroNombre={turnoActivo.cajeroNombre}
        cajaNumero={turnoActivo.cajaId}
        turnoId={turnoActivo.turnoId}
        almacenNombre={turnoActivo.almacenNombre}
        barcodeInput={barcodeInput}
        onBarcodeChange={setBarcodeInput}
        onBarcodeSubmit={handleBarcodeSubmit}
        onOpenSearch={() => modals.setIsSearchOpen(true)}
        onOpenDispatch={() => modals.setIsDispatchOpen(true)}
      />

      <div className="flex-1 px-3 py-2 flex gap-3 overflow-hidden">
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
          onOpenPayment={() => modals.setIsPaymentOpen(true)}
        />
      </div>

      <HotkeyBar
        onF1={() => scannerRef.current?.focus()}
        onF2={() => modals.setIsSearchOpen(true)}
        onF3={() => modals.setIsCustomerOpen(true)}
        onF4={() => items.length > 0 && modals.setIsQuantityOpen(true)}
        onF5={() => selectedItem && modals.setIsStockOpen(true)}
        onF6={() => (selectedItem ? modals.setIsDiscountOpen(true) : modals.setIsDispatchOpen(true))}
        onF7={() => selectedItem && modals.setIsSubstitutesOpen(true)}
        onF8={() => items.length > 0 && removeItem(selectedIndex)}
        onF9={() => total > 0 && modals.setIsPaymentOpen(true)}
        onF10={() => setTipoComprobante((prev) => (prev === "BOLETA" ? "FACTURA" : "BOLETA"))}
        onF11={() => modals.setIsHeldOpen(true)}
        onF12={() => modals.setIsBlindCloseOpen(true)}
        disabledF4={items.length === 0}
        disabledF8={items.length === 0}
        disabledF9={total <= 0}
      />

      {toast && (
        <div
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
        turnoCajaId={turnoActivo.turnoId}
        handleConfirmClose={handleConfirmClose}
        pedidosWeb={pedidosWeb}
        setPedidosWeb={setPedidosWeb}
        refocusScanner={refocusScanner}
        showToast={showToast}
      />
    </div>
  );
};
