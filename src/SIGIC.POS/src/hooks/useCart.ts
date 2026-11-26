import { useState, useMemo, useCallback } from "react";
import {
  CartItem,
  Product,
  Customer,
  TipoComprobante,
  HeldTicket,
  RestoreTicketResult,
} from "../types/pos";
import { DEFAULT_CUSTOMER } from "../data/mockProducts";
import { api } from "../services/api";

export function useCart() {
  const [items, setItems] = useState<CartItem[]>([]);
  const [selectedIndex, setSelectedIndex] = useState<number>(0);
  const [customer, setCustomer] = useState<Customer>(DEFAULT_CUSTOMER);
  const [tipoComprobante, setTipoComprobante] = useState<TipoComprobante>("BOLETA");
  const [heldTickets, setHeldTickets] = useState<HeldTicket[]>([]);

  const getPriceForTariff = useCallback((p: Product, cust: Customer): number => {
    if (cust.tarifa === "CONTRATISTA" && p.precioEspecial) return p.precioEspecial;
    if (cust.tarifa === "MAESTRO" && p.precioMayorista) return p.precioMayorista;
    return p.precioVenta;
  }, []);

  const addItem = useCallback((product: Product, cantidad = 1): boolean => {
    const stockDisp = product.stockFisico - product.stockComprometido;
    if (stockDisp <= 0) return false;

    setItems((prev) => {
      const idx = prev.findIndex((i) => i.producto.id === product.id);
      const unitPrice = getPriceForTariff(product, customer);

      if (idx >= 0) {
        const existing = prev[idx];
        const newQty = existing.cantidad + cantidad;
        if (newQty > stockDisp) return prev;
        const subtotal = (newQty * unitPrice) * (1 - existing.descuentoPorcentaje / 100);
        const updated = [...prev];
        updated[idx] = { ...existing, cantidad: newQty, precioUnitario: unitPrice, subtotal: Math.round(subtotal * 100) / 100 };
        setSelectedIndex(idx);
        return updated;
      }

      if (cantidad > stockDisp) return prev;
      const subtotal = (cantidad * unitPrice);
      const newItem: CartItem = {
        id: `cart-${Date.now()}-${Math.random().toString(36).substring(2, 6)}`,
        producto: product,
        cantidad,
        unidadMedida: product.unidadBase,
        precioUnitario: unitPrice,
        descuentoPorcentaje: 0,
        subtotal: Math.round(subtotal * 100) / 100,
      };
      setSelectedIndex(prev.length);
      return [...prev, newItem];
    });
    return true;
  }, [customer, getPriceForTariff]);

  const updateQuantity = useCallback((index: number, newQty: number): boolean => {
    if (newQty <= 0) return false;
    let ok = true;
    setItems((prev) => {
      if (index < 0 || index >= prev.length) return prev;
      const item = prev[index];
      const stockDisp = item.producto.stockFisico - item.producto.stockComprometido;
      if (newQty > stockDisp) { ok = false; return prev; }
      const subtotal = (newQty * item.precioUnitario) * (1 - item.descuentoPorcentaje / 100);
      const updated = [...prev];
      updated[index] = { ...item, cantidad: newQty, subtotal: Math.round(subtotal * 100) / 100 };
      return updated;
    });
    return ok;
  }, []);

  const updateDiscount = useCallback((index: number, discountPct: number) => {
    const validPct = Math.max(0, Math.min(100, discountPct));
    setItems((prev) => {
      if (index < 0 || index >= prev.length) return prev;
      const item = prev[index];
      const subtotal = (item.cantidad * item.precioUnitario) * (1 - validPct / 100);
      const updated = [...prev];
      updated[index] = { ...item, descuentoPorcentaje: validPct, subtotal: Math.round(subtotal * 100) / 100 };
      return updated;
    });
  }, []);

  const removeItem = useCallback((index: number) => {
    setItems((prev) => {
      if (index < 0 || index >= prev.length) return prev;
      const updated = prev.filter((_, i) => i !== index);
      setSelectedIndex(Math.max(0, Math.min(index, updated.length - 1)));
      return updated;
    });
  }, []);

  const clearCart = useCallback(() => { setItems([]); setSelectedIndex(0); }, []);

  const changeCustomer = useCallback((newCust: Customer) => {
    setCustomer(newCust);
    setItems((prev) =>
      prev.map((item) => {
        const unitPrice = getPriceForTariff(item.producto, newCust);
        const subtotal = (item.cantidad * unitPrice) * (1 - item.descuentoPorcentaje / 100);
        return { ...item, precioUnitario: unitPrice, subtotal: Math.round(subtotal * 100) / 100 };
      })
    );
  }, [getPriceForTariff]);

  const selectPrevious = useCallback(() => setSelectedIndex((prev) => (prev > 0 ? prev - 1 : prev)), []);
  const selectNext = useCallback(() => setItems((curr) => { setSelectedIndex((prev) => (prev < curr.length - 1 ? prev + 1 : prev)); return curr; }), []);

  const total = useMemo(() => Math.round(items.reduce((acc, i) => acc + i.subtotal, 0) * 100) / 100, [items]);
  const subtotalNeto = useMemo(() => Math.round((total / 1.18) * 100) / 100, [total]);
  const igv = useMemo(() => Math.round((total - subtotalNeto) * 100) / 100, [total, subtotalNeto]);
  const totalItems = useMemo(() => items.reduce((acc, i) => acc + i.cantidad, 0), [items]);

  const holdTicket = useCallback((): boolean => {
    if (items.length === 0) return false;
    const now = Date.now();
    const ticket: HeldTicket = {
      id: `TKT-${now.toString().slice(-6)}`,
      cliente: customer,
      items: [...items],
      tipoComprobante,
      fechaGuardado: new Date(now).toLocaleTimeString(),
      createdAtMs: now,
      total,
    };
    setHeldTickets((prev) => [ticket, ...prev]);
    clearCart();
    setCustomer(DEFAULT_CUSTOMER);
    return true;
  }, [items, customer, tipoComprobante, total, clearCart]);

  const purgeExpiredTickets = useCallback((onAlert?: (msg: string) => void): number => {
    const now = Date.now();
    let count = 0;
    setHeldTickets((prev) =>
      prev.filter((t) => {
        const isExp = now - t.createdAtMs > 7_200_000;
        if (isExp) count++;
        return !isExp;
      })
    );
    if (count > 0 && onAlert) {
      onAlert(`Se retiraron ${count} tickets en espera que superaron las 2 horas.`);
    }
    return count;
  }, []);

  const restoreTicket = useCallback(
    async (ticketId: string, stockOverride?: any[]): Promise<RestoreTicketResult> => {
      const now = Date.now();
      const ticket = heldTickets.find((t) => t.id === ticketId);
      if (!ticket) return { success: false, error: "El ticket no existe o ya fue procesado." };

      if (now - ticket.createdAtMs > 7_200_000) {
        setHeldTickets((prev) => prev.filter((t) => t.id !== ticketId));
        return { success: false, error: `El ticket ${ticketId} expiró por superar las 2 horas de espera.` };
      }

      let stockVigente: any[];
      if (stockOverride) {
        stockVigente = stockOverride;
      } else {
        try {
          stockVigente = await api.obtenerStockAlmacen();
          if (!Array.isArray(stockVigente)) {
            return { success: false, error: "No se pudo verificar el stock vigente en el servidor." };
          }
        } catch {
          return { success: false, error: "No se pudo verificar el stock vigente en el servidor." };
        }
      }

      const warnings: string[] = [];
      const adjustedItems: CartItem[] = [];

      for (const item of ticket.items) {
        const stockRow = stockVigente.find(
          (s: any) =>
            (s.productoId && s.productoId.toLowerCase() === item.producto.id.toLowerCase()) ||
            (s.id && s.id.toLowerCase() === item.producto.id.toLowerCase()) ||
            (s.codigoSku && s.codigoSku.toLowerCase() === item.producto.codigoSku.toLowerCase())
        );

        // Sin fallback a item.producto.stockFisico / stockComprometido: si no existe en la respuesta, stock es 0
        const stockFisico = stockRow ? Number(stockRow.stockFisico) : 0;
        const stockComprometido = stockRow ? Number(stockRow.stockComprometido) : 0;
        const stockDisp = stockFisico - stockComprometido;

        const prodActualizado = stockRow
          ? { ...item.producto, stockFisico, stockComprometido, stockDisponible: stockDisp }
          : { ...item.producto, stockFisico: 0, stockComprometido: 0, stockDisponible: 0 };

        if (stockDisp <= 0) {
          warnings.push(`Sin stock disponible para '${prodActualizado.nombreComercial}' (Disponible: ${stockDisp}). Ítem omitido.`);
          continue;
        }

        if (item.cantidad > stockDisp) {
          warnings.push(`Stock insuficiente para '${prodActualizado.nombreComercial}'. Solicitado: ${item.cantidad}, disponible: ${stockDisp}. Se ajustó la cantidad.`);
          const subtotal = Math.round(stockDisp * item.precioUnitario * (1 - item.descuentoPorcentaje / 100) * 100) / 100;
          adjustedItems.push({ ...item, producto: prodActualizado, cantidad: stockDisp, subtotal });
        } else {
          adjustedItems.push({ ...item, producto: prodActualizado });
        }
      }

      if (adjustedItems.length === 0) {
        setHeldTickets((prev) => prev.filter((t) => t.id !== ticketId));
        return { success: false, error: "No hay stock disponible para ninguno de los ítems del ticket recuperado.", warnings };
      }

      setItems(adjustedItems);
      setCustomer(ticket.cliente);
      setTipoComprobante(ticket.tipoComprobante);
      setSelectedIndex(0);
      setHeldTickets((prev) => prev.filter((t) => t.id !== ticketId));

      return { success: true, warnings: warnings.length > 0 ? warnings : undefined };
    },
    [heldTickets]
  );

  return {
    items,
    selectedIndex,
    setSelectedIndex,
    customer,
    setCustomer: changeCustomer,
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
  };
}
