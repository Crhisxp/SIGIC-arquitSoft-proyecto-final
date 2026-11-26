"use client";

import React, { createContext, useContext, useState, useEffect, useCallback, useMemo } from "react";
import { CartItemWeb } from "../types/web";

interface CartContextType {
  items: CartItemWeb[];
  addItem: (item: CartItemWeb) => boolean;
  removeItem: (productoId: string, unidadMedida?: string) => void;
  updateQuantity: (productoId: string, cantidad: number, unidadMedida?: string) => boolean;
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
      const enCarrito = prev.filter((i) => i.productoId === newItem.productoId)
        .reduce((total, i) => total + i.cantidadBase, 0);
      if (enCarrito + newItem.cantidadBase > newItem.stockDisponible) return prev;

      const idx = prev.findIndex((i) => i.productoId === newItem.productoId && i.unidadMedida === newItem.unidadMedida);
      let updated: CartItemWeb[];
      if (idx >= 0) {
        const existing = prev[idx];
        const newQty = existing.cantidad + newItem.cantidad;
        const newQtyBase = existing.cantidadBase + newItem.cantidadBase;
        const subtotal = Math.round(newQty * newItem.precioUnitario * 100) / 100;
        updated = [...prev];
        updated[idx] = { ...newItem, cantidad: newQty, cantidadBase: newQtyBase, subtotal };
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

  const removeItem = useCallback((productoId: string, unidadMedida?: string) => {
    setItems((prev) => {
      const updated = prev.filter((i) =>
        i.productoId !== productoId || (unidadMedida !== undefined && i.unidadMedida !== unidadMedida));
      try {
        localStorage.setItem("sigic_web_cart", JSON.stringify(updated));
      } catch {
        // ignore
      }
      return updated;
    });
  }, []);

  const updateQuantity = useCallback((productoId: string, cantidad: number, unidadMedida?: string): boolean => {
    if (cantidad <= 0) return false;
    let ok = true;
    setItems((prev) => {
      const idx = prev.findIndex((i) => i.productoId === productoId &&
        (unidadMedida === undefined || i.unidadMedida === unidadMedida));
      if (idx < 0) return prev;
      const item = prev[idx];
      const factor = item.cantidadBase / item.cantidad;
      const newQtyBase = Math.round(cantidad * factor * 10000) / 10000;
      const otrasPresentaciones = prev.reduce((total, otra, indice) =>
        otra.productoId === productoId && indice !== idx ? total + otra.cantidadBase : total, 0);
      if (newQtyBase + otrasPresentaciones > item.stockDisponible) {
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
