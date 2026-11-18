import React from "react";

interface HotkeyBarProps {
  onF1: () => void;
  onF2: () => void;
  onF3: () => void;
  onF4: () => void;
  onF5: () => void;
  onF6: () => void;
  onF7: () => void;
  onF8: () => void;
  onF9: () => void;
  onF10: () => void;
  onF11: () => void;
  onF12: () => void;
  disabledF4?: boolean;
  disabledF6?: boolean;
  disabledF8?: boolean;
  disabledF9?: boolean;
}

interface HotkeyAction {
  key: string;
  label: string;
  action: () => void;
  disabled?: boolean;
  highlight?: boolean;
}

export const HotkeyBar: React.FC<HotkeyBarProps> = ({
  onF1,
  onF2,
  onF3,
  onF4,
  onF5,
  onF6,
  onF7,
  onF8,
  onF9,
  onF10,
  onF11,
  onF12,
  disabledF4 = false,
  disabledF6 = false,
  disabledF8 = false,
  disabledF9 = false,
}) => {
  const actions: HotkeyAction[] = [
    { key: "F1", label: "Escáner", action: onF1 },
    { key: "F2", label: "Buscar", action: onF2 },
    { key: "F3", label: "Cliente", action: onF3 },
    { key: "F4", label: "Cantidad", action: onF4, disabled: disabledF4 },
    { key: "F5", label: "Stock", action: onF5 },
    { key: "F6", label: "Despacho/Dscto", action: onF6, disabled: disabledF6 },
    { key: "F7", label: "Sustitutos", action: onF7 },
    { key: "F8", label: "Eliminar", action: onF8, disabled: disabledF8 },
    { key: "F9", label: "Cobrar", action: onF9, disabled: disabledF9, highlight: true },
    { key: "F10", label: "Boleta", action: onF10 },
    { key: "F11", label: "Espera", action: onF11 },
    { key: "F12", label: "Arqueo Ciego", action: onF12 },
  ];

  return (
    <div className="h-[52px] bg-[#0f1011] border-t border-[#23252a] px-2 py-1.5 flex items-center justify-between gap-1 shrink-0 overflow-x-hidden">
      {actions.map((act) => (
        <button
          key={act.key}
          type="button"
          disabled={act.disabled}
          onClick={act.action}
          className={`flex-1 min-h-[40px] px-1 py-1 rounded border flex flex-col items-center justify-center transition-colors text-center ${
            act.highlight
              ? "bg-[#e4f222] text-[#08090a] border-[#e4f222] font-bold hover:bg-[#d3e015]"
              : act.disabled
              ? "bg-[#161718] text-[#5e636e] border-[#18191c] opacity-50 cursor-not-allowed"
              : "bg-[#161718] text-[#8a8f98] border-[#23252a] hover:bg-[#1c1e22] hover:text-[#f7f8f8]"
          }`}
          title={`${act.key} - ${act.label}`}
        >
          <span
            className={`font-mono text-[10px] font-bold ${
              act.highlight ? "text-[#08090a]" : "text-[#e4f222]"
            }`}
          >
            {act.key}
          </span>
          <span className="text-[10px] truncate max-w-[70px] leading-tight">
            {act.label}
          </span>
        </button>
      ))}
    </div>
  );
};
