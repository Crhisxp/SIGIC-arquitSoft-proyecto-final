import React, { useEffect, useRef } from "react";
import { X } from "lucide-react";

interface FocusTrapModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  badge?: string;
  children: React.ReactNode;
  maxWidth?: string;
}

export const FocusTrapModal: React.FC<FocusTrapModalProps> = ({
  isOpen,
  onClose,
  title,
  badge,
  children,
  maxWidth = "max-w-xl",
}) => {
  const modalRef = useRef<HTMLDivElement>(null);
  const previouslyFocusedElement = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!isOpen) return;

    previouslyFocusedElement.current = document.activeElement as HTMLElement;

    const modal = modalRef.current;
    if (!modal) return;

    // Auto-focus first input/button
    const focusableElements = modal.querySelectorAll<HTMLElement>(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );
    if (focusableElements.length > 0) {
      focusableElements[0].focus();
    }

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }

      if (e.key === "Tab") {
        const focusable = modal.querySelectorAll<HTMLElement>(
          'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );
        if (focusable.length === 0) return;

        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (e.shiftKey) {
          if (document.activeElement === first) {
            e.preventDefault();
            last.focus();
          }
        } else {
          if (document.activeElement === last) {
            e.preventDefault();
            first.focus();
          }
        }
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      previouslyFocusedElement.current?.focus();
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm overflow-hidden"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-title"
    >
      <div
        ref={modalRef}
        className={`relative w-full ${maxWidth} bg-[#161718] border border-[#23252a] rounded-lg shadow-2xl overflow-hidden flex flex-col`}
      >
        {/* Cabecera */}
        <div className="flex items-center justify-between px-4 py-3 bg-[#0f1011] border-b border-[#23252a] shrink-0">
          <div className="flex items-center gap-2">
            {badge && (
              <span className="px-2 py-0.5 text-xs font-mono font-bold bg-[#e4f222] text-[#08090a] rounded shrink-0">
                {badge}
              </span>
            )}
            <h2 id="modal-title" className="text-sm font-semibold tracking-wide text-[#f7f8f8] uppercase">
              {title}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1 rounded text-[#8a8f98] hover:text-[#f7f8f8] hover:bg-[#1c1e22] transition-colors focus:outline-none focus-visible:ring-1 focus-visible:ring-[#e4f222]"
            aria-label="Cerrar modal (Escape)"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Contenido */}
        <div className="p-4 overflow-y-auto max-h-[560px] sigic-scrollbar">
          {children}
        </div>
      </div>
    </div>
  );
};
