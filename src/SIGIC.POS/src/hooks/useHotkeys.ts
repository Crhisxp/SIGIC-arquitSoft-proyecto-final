import { useEffect } from "react";

export interface HotkeyHandlers {
  onF1?: () => void; // Escáner / Entrada Código de Barras
  onF2?: () => void; // Buscar Producto
  onF3?: () => void; // Cliente DNI/RUC
  onF4?: () => void; // Editar Cantidad
  onF5?: () => void; // Stock Multialmacén
  onF6?: () => void; // Aplicar Descuento / Tarifa / Despacho Web
  onF7?: () => void; // Sustitutos
  onF8?: () => void; // Eliminar Ítem
  onF9?: () => void; // Cobrar / Pago Mixto
  onF10?: () => void; // Boleta Electrónica
  onF11?: () => void; // Pausar / Recuperar Ticket
  onF12?: () => void; // Arqueo Ciego
  onEscape?: () => void; // Cancelar / Refocus F1
  onEnter?: () => void; // Confirmar
  onArrowUp?: () => void; // Fila anterior
  onArrowDown?: () => void; // Fila siguiente
}

export function useHotkeys(handlers: HotkeyHandlers, isModalOpen = false) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Prevenir atajos nativos del navegador para F1-F12
      if (/^F[1-9]$|^F1[0-2]$/.test(e.key)) {
        e.preventDefault();
      }

      // Si un modal está abierto, solo permitir Escape y Enter globales
      // salvo que el modal maneje sus propios eventos
      if (isModalOpen) {
        if (e.key === "Escape") {
          e.preventDefault();
          handlers.onEscape?.();
        } else if (e.key === "Enter") {
          handlers.onEnter?.();
        }
        return;
      }

      switch (e.key) {
        case "F1":
          handlers.onF1?.();
          break;
        case "F2":
          handlers.onF2?.();
          break;
        case "F3":
          handlers.onF3?.();
          break;
        case "F4":
          handlers.onF4?.();
          break;
        case "F5":
          handlers.onF5?.();
          break;
        case "F6":
          handlers.onF6?.();
          break;
        case "F7":
          handlers.onF7?.();
          break;
        case "F8":
          handlers.onF8?.();
          break;
        case "F9":
          handlers.onF9?.();
          break;
        case "F10":
          handlers.onF10?.();
          break;
        case "F11":
          handlers.onF11?.();
          break;
        case "F12":
          handlers.onF12?.();
          break;
        case "Escape":
          e.preventDefault();
          handlers.onEscape?.();
          break;
        case "Enter":
          handlers.onEnter?.();
          break;
        case "ArrowUp":
          e.preventDefault();
          handlers.onArrowUp?.();
          break;
        case "ArrowDown":
          e.preventDefault();
          handlers.onArrowDown?.();
          break;
        default:
          break;
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [handlers, isModalOpen]);
}
