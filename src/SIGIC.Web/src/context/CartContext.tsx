"use client";

import React, { createContext, useContext, useState, useEffect, useCallback, useMemo } from "react";
import { CartItemWeb } from "../types/web";

interface CartContextType {
  items: CartItemWeb[];
  addItem: (item: CartItemWeb) => boolean;
  removeItem: (productoId: string) => void;
  updateQuantity: (productoId: string, cantidad: number) => boolean;
  clearCart: () => void;
  total: number;
  totalItemsCount: number;
}

const CartContext = createContext<CartContextType | undefined>(undefined);

export const CartProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [items, setItems] = useState<CartItemWeb[]>([]);

  useEffect(() => {
    try {
      const saved = localStorage.getItem("sigic_web_cart");
      if (saved) setItems(JSON.parse(saved));
    } catch {
      // ignore
    }
  }, []);

  const saveCart = (newItems: CartItemWeb[]) => {
    setItems(newItems);
    try {
      localStorage.setItem("sigic_web_cart", JSON.stringify(newItems));
    } catch {
      // ignore
    }
  };

  const addItem = useCallback((newItem: CartItemWeb): boolean => {
    // Verificar si la cantidad solicitada supera el stock disponible
    if (newItem.cantidadBase > newItem.stockDisponible) {
      return false;
    }

    setItems((prev) => {
      const idx = prev.findIndex((i) => i.productoId === newItem.productoId);
      let updated: CartItemWeb[];
      if (idx >= 0) {
        const existing = prev[idx];
        const newQty = existing.cantidad + newItem.cantidad;
        const newQtyBase = existing.cantidadBase + newItem.cantidadBase;
        if (newQtyBase > newItem.stockDisponible) {
          return prev;
        }
        const subtotal = Math.round(newQty * existing.precioUnitario * 100) / 100;
        updated = [...prev];
        updated[idx] = { ...existing, cantidad: newQty, cantidadBase: newQtyBase, subtotal };
      } else {
        updated = [...prev, newItem];
      }
      try {
        localStorage.setItem("sigic_web_cart", JSON.stringify(updated));
      } catch {
        // ignore
      }
      return updated;
    });
    return true;
  }, []);

  const removeItem = useCallback((productoId: string) => {
    setItems((prev) => {
      const updated = prev.filter((i) => i.productoId !== productoId);
      try {
        localStorage.setItem("sigic_web_cart", JSON.stringify(updated));
      } catch {
        // ignore
      }
      return updated;
    });
  }, []);

  const updateQuantity = useCallback((productoId: string, cantidad: number): boolean => {
    if (cantidad <= 0) return false;
    let ok = true;
    setItems((prev) => {
      const idx = prev.findIndex((i) => i.productoId === productoId);
      if (idx < 0) return prev;
      const item = prev[idx];
      const factor = item.cantidadBase / item.cantidad;
      const newQtyBase = Math.round(cantidad * factor * 10000) / 10000;
      if (newQtyBase > item.stockDisponible) {
        ok = false;
        return prev;
      }
      const subtotal = Math.round(cantidad * item.precioUnitario * 100) / 100;
      const updated = [...prev];
      updated[idx] = { ...item, cantidad, cantidadBase: newQtyBase, subtotal };
      try {
        localStorage.setItem("sigic_web_cart", JSON.stringify(updated));
      } catch {
        // ignore
      }
      return updated;
    });
    return ok;
  }, []);

  const clearCart = useCallback(() => {
    saveCart([]);
  }, []);

  const total = useMemo(() => Math.round(items.reduce((acc, i) => acc + i.subtotal, 0) * 100) / 100, [items]);
  const totalItemsCount = useMemo(() => items.reduce((acc, i) => acc + i.cantidad, 0), [items]);

  return (
    <CartContext.Provider
      value={{
        items,
        addItem,
        removeItem,
        updateQuantity,
        clearCart,
        total,
        totalItemsCount,
      }}
    >
      {children}
    </CartContext.Provider>
  );
};

export const useCart = () => {
  const context = useContext(CartContext);
  if (!context) throw new Error("useCart must be used within a CartProvider");
  return context;
};
