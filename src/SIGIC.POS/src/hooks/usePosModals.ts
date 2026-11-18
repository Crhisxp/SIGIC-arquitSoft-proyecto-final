import { useState, useCallback } from "react";

export function usePosModals() {
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [isCustomerOpen, setIsCustomerOpen] = useState(false);
  const [isQuantityOpen, setIsQuantityOpen] = useState(false);
  const [isDiscountOpen, setIsDiscountOpen] = useState(false);
  const [isPaymentOpen, setIsPaymentOpen] = useState(false);
  const [isHeldOpen, setIsHeldOpen] = useState(false);
  const [isBlindCloseOpen, setIsBlindCloseOpen] = useState(false);
  const [isDispatchOpen, setIsDispatchOpen] = useState(false);
  const [isStockOpen, setIsStockOpen] = useState(false);
  const [isSubstitutesOpen, setIsSubstitutesOpen] = useState(false);

  const anyModalOpen =
    isSearchOpen ||
    isCustomerOpen ||
    isQuantityOpen ||
    isDiscountOpen ||
    isPaymentOpen ||
    isHeldOpen ||
    isBlindCloseOpen ||
    isDispatchOpen ||
    isStockOpen ||
    isSubstitutesOpen;

  const closeAllModals = useCallback(() => {
    setIsSearchOpen(false);
    setIsCustomerOpen(false);
    setIsQuantityOpen(false);
    setIsDiscountOpen(false);
    setIsPaymentOpen(false);
    setIsHeldOpen(false);
    setIsBlindCloseOpen(false);
    setIsDispatchOpen(false);
    setIsStockOpen(false);
    setIsSubstitutesOpen(false);
  }, []);

  return {
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
    anyModalOpen,
    closeAllModals,
  };
}
