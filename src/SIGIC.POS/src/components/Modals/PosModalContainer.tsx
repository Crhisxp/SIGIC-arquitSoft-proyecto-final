import React from "react";
import { SearchProductModal } from "./SearchProductModal";
import { CustomerModal } from "./CustomerModal";
import { EditQuantityModal } from "./EditQuantityModal";
import { DiscountModal } from "./DiscountModal";
import { PaymentModal } from "./PaymentModal";
import { HeldTicketsModal } from "./HeldTicketsModal";
import { BlindCloseModal } from "./BlindCloseModal";
import { WebDispatchModal } from "./WebDispatchModal";
import { StockModal } from "./StockModal";
import { SubstitutesModal } from "./SubstitutesModal";
import {
  Product,
  CartItem,
  Customer,
  TipoComprobante,
  PaymentEntry,
  DespachoWebPedido,
  HeldTicket,
  CashDenomination,
  ArqueoResponse,
  RestoreTicketResult,
} from "../../types/pos";

export interface PosModalContainerProps {
  isSearchOpen: boolean;
  setIsSearchOpen: (open: boolean) => void;
  isCustomerOpen: boolean;
  setIsCustomerOpen: (open: boolean) => void;
  isQuantityOpen: boolean;
  setIsQuantityOpen: (open: boolean) => void;
  isDiscountOpen: boolean;
  setIsDiscountOpen: (open: boolean) => void;
  isPaymentOpen: boolean;
  setIsPaymentOpen: (open: boolean) => void;
  isHeldOpen: boolean;
  setIsHeldOpen: (open: boolean) => void;
  isBlindCloseOpen: boolean;
  setIsBlindCloseOpen: (open: boolean) => void;
  isDispatchOpen: boolean;
  setIsDispatchOpen: (open: boolean) => void;
  isStockOpen: boolean;
  setIsStockOpen: (open: boolean) => void;
  isSubstitutesOpen: boolean;
  setIsSubstitutesOpen: (open: boolean) => void;
  products: Product[];
  addItem: (p: Product, qty?: number) => boolean;
  customer: Customer;
  setCustomer: (c: Customer) => void;
  selectedItem: CartItem | null;
  selectedIndex: number;
  updateQuantity: (index: number, qty: number) => boolean;
  updateDiscount: (index: number, pct: number) => void;
  removeItem: (index: number) => void;
  total: number;
  tipoComprobante: TipoComprobante;
  handleConfirmSale: (pagos: PaymentEntry[], tipo: TipoComprobante) => Promise<void>;
  heldTickets: HeldTicket[];
  itemsCount: number;
  holdTicket: () => boolean;
  restoreTicket: (id: string, stockOverride?: any[]) => Promise<RestoreTicketResult>;
  purgeExpiredTickets?: (onAlert?: (msg: string) => void) => number;
  turnoCajaId: string;
  handleConfirmClose: (params: {
    turnoCajaId: string;
    desgloseFisico: CashDenomination[];
    totalDigitalDeclarado: number;
    justificacion?: string;
  }) => Promise<ArqueoResponse>;
  pedidosWeb: DespachoWebPedido[];
  setPedidosWeb: React.Dispatch<React.SetStateAction<DespachoWebPedido[]>>;
  refocusScanner: () => void;
  showToast: (msg: string, err?: boolean) => void;
}

export const PosModalContainer: React.FC<PosModalContainerProps> = ({
  isSearchOpen,
  setIsSearchOpen,
  isCustomerOpen,
  setIsCustomerOpen,
  isQuantityOpen,
  setIsQuantityOpen,
  isDiscountOpen,
  setIsDiscountOpen,
  isPaymentOpen,
  setIsPaymentOpen,
  isHeldOpen,
  setIsHeldOpen,
  isBlindCloseOpen,
  setIsBlindCloseOpen,
  isDispatchOpen,
  setIsDispatchOpen,
  isStockOpen,
  setIsStockOpen,
  isSubstitutesOpen,
  setIsSubstitutesOpen,
  products,
  addItem,
  customer,
  setCustomer,
  selectedItem,
  selectedIndex,
  updateQuantity,
  updateDiscount,
  removeItem,
  total,
  tipoComprobante,
  handleConfirmSale,
  heldTickets,
  itemsCount,
  holdTicket,
  restoreTicket,
  purgeExpiredTickets,
  turnoCajaId,
  handleConfirmClose,
  pedidosWeb,
  setPedidosWeb,
  refocusScanner,
  showToast,
}) => {
  return (
    <>
      <SearchProductModal
        isOpen={isSearchOpen}
        onClose={() => {
          setIsSearchOpen(false);
          refocusScanner();
        }}
        products={products}
        onSelectProduct={(p) => {
          addItem(p, 1);
          refocusScanner();
        }}
      />

      <CustomerModal
        isOpen={isCustomerOpen}
        onClose={() => {
          setIsCustomerOpen(false);
          refocusScanner();
        }}
        activeCustomer={customer}
        onSelectCustomer={(c) => {
          setCustomer(c);
          refocusScanner();
        }}
      />

      <EditQuantityModal
        isOpen={isQuantityOpen}
        onClose={() => {
          setIsQuantityOpen(false);
          refocusScanner();
        }}
        item={selectedItem}
        onConfirm={(qty) => {
          updateQuantity(selectedIndex, qty);
          refocusScanner();
        }}
      />

      <DiscountModal
        isOpen={isDiscountOpen}
        onClose={() => {
          setIsDiscountOpen(false);
          refocusScanner();
        }}
        item={selectedItem}
        onConfirm={(pct) => {
          updateDiscount(selectedIndex, pct);
          refocusScanner();
        }}
      />

      <PaymentModal
        isOpen={isPaymentOpen}
        onClose={() => {
          setIsPaymentOpen(false);
          refocusScanner();
        }}
        totalVenta={total}
        tipoComprobanteInicial={tipoComprobante}
        onConfirmSale={handleConfirmSale}
      />

      <HeldTicketsModal
        isOpen={isHeldOpen}
        onClose={() => {
          setIsHeldOpen(false);
          refocusScanner();
        }}
        heldTickets={heldTickets}
        canHoldCurrentCart={itemsCount > 0}
        onHoldCurrentCart={() => {
          holdTicket();
          showToast("Venta encolada exitosamente.");
          refocusScanner();
        }}
        onRestoreTicket={async (id) => {
          const res = await restoreTicket(id);
          if (!res.success) {
            showToast(res.error || "No se pudo recuperar el ticket.", true);
          } else {
            if (res.warnings && res.warnings.length > 0) {
              showToast(`Ticket ${id} recuperado con alertas de stock: ${res.warnings.join(" | ")}`, true);
            } else {
              showToast(`Ticket ${id} recuperado.`);
            }
          }
          refocusScanner();
        }}
        onPurgeExpired={() => {
          if (purgeExpiredTickets) {
            purgeExpiredTickets((msg) => showToast(msg, true));
          }
        }}
      />

      <BlindCloseModal
        isOpen={isBlindCloseOpen}
        onClose={() => {
          setIsBlindCloseOpen(false);
          refocusScanner();
        }}
        turnoCajaId={turnoCajaId}
        onConfirmClose={handleConfirmClose}
      />

      <WebDispatchModal
        isOpen={isDispatchOpen}
        onClose={() => {
          setIsDispatchOpen(false);
          refocusScanner();
        }}
        pedidosDisponibles={pedidosWeb}
        onConfirmarEntrega={(pedId) => {
          setPedidosWeb((prev) =>
            prev.map((p) => (p.pedidoId === pedId ? { ...p, estado: "ENTREGADO" } : p))
          );
          refocusScanner();
        }}
      />

      <StockModal
        isOpen={isStockOpen}
        onClose={() => {
          setIsStockOpen(false);
          refocusScanner();
        }}
        product={selectedItem ? selectedItem.producto : null}
      />

      <SubstitutesModal
        isOpen={isSubstitutesOpen}
        onClose={() => {
          setIsSubstitutesOpen(false);
          refocusScanner();
        }}
        product={selectedItem ? selectedItem.producto : null}
        allProducts={products}
        onSelectSubstitute={(sub) => {
          if (selectedItem) {
            removeItem(selectedIndex);
            addItem(sub, 1);
          }
          refocusScanner();
        }}
      />
    </>
  );
};
