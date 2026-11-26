import React, { useState, useEffect, useRef } from "react";
import { UserCheck } from "lucide-react";
import { FocusTrapModal } from "./FocusTrapModal";
import { Customer, TarifaCliente, TipoDocumentoCliente } from "../../types/pos";
import { SAMPLE_CUSTOMERS } from "../../data/mockProducts";

interface CustomerModalProps {
  isOpen: boolean;
  onClose: () => void;
  activeCustomer: Customer;
  onSelectCustomer: (customer: Customer) => void;
}

export const CustomerModal: React.FC<CustomerModalProps> = ({
  isOpen,
  onClose,
  activeCustomer,
  onSelectCustomer,
}) => {
  const [docType, setDocType] = useState<TipoDocumentoCliente>(activeCustomer.tipoDocumento);
  const [docNumber, setDocNumber] = useState(activeCustomer.numeroDocumento);
  const [name, setName] = useState(activeCustomer.razonSocial);
  const [tariff, setTariff] = useState<TarifaCliente>(activeCustomer.tarifa);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setDocType(activeCustomer.tipoDocumento);
      setDocNumber(activeCustomer.numeroDocumento);
      setName(activeCustomer.razonSocial);
      setTariff(activeCustomer.tarifa);
      setTimeout(() => inputRef.current?.focus(), 30);
    }
  }, [isOpen, activeCustomer]);

  const handleConfirm = () => {
    const cust: Customer = {
      id: `cust-${Date.now()}`,
      tipoDocumento: docType,
      numeroDocumento: docNumber.trim() || "00000000",
      razonSocial: name.trim() || "CLIENTE MOSTRADOR",
      tarifa: tariff,
    };
    onSelectCustomer(cust);
    onClose();
  };

  const handleSelectPreset = (cust: Customer) => {
    onSelectCustomer(cust);
    onClose();
  };

  return (
    <FocusTrapModal
      isOpen={isOpen}
      onClose={onClose}
      title="Selección y captura de cliente"
      badge="F3"
      maxWidth="max-w-lg"
    >
      <div className="flex flex-col gap-4 text-xs">
        {/* Presets Rápidos */}
        <div>
          <label className="text-[#8a8f98] font-mono uppercase block mb-1">
            Clientes Preconfigurados / Perfiles Frecuentes
          </label>
          <div className="grid grid-cols-1 gap-2">
            {SAMPLE_CUSTOMERS.map((c) => (
              <button
                key={c.id}
                type="button"
                onClick={() => handleSelectPreset(c)}
                className={`flex items-center justify-between p-2 rounded border text-left transition-colors ${
                  c.numeroDocumento === activeCustomer.numeroDocumento
                    ? "bg-[#e4f222]/15 border-[#e4f222] text-[#f7f8f8]"
                    : "bg-[#0f1011] border-[#23252a] text-[#8a8f98] hover:bg-[#1c1e22]"
                }`}
              >
                <div>
                  <div className="font-semibold text-[#f7f8f8]">{c.razonSocial}</div>
                  <div className="font-mono tabular-nums text-[11px] text-[#8a8f98]">
                    {c.tipoDocumento}: {c.numeroDocumento}
                  </div>
                </div>
                <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-[#23252a] text-[#e4f222] shrink-0">
                  {c.tarifa}
                </span>
              </button>
            ))}
          </div>
        </div>

        {/* Captura Manual */}
        <div className="border-t border-[#23252a] pt-3 flex flex-col gap-3">
          <div className="font-semibold text-[#f7f8f8] flex items-center gap-1.5">
            <UserCheck className="w-4 h-4 text-[#e4f222]" />
            <span>Captura Manual de Documento</span>
          </div>

          <div className="grid grid-cols-3 gap-2">
            <div>
              <label className="text-[#8a8f98] font-mono block mb-1">TIPO DOC</label>
              <select
                value={docType}
                onChange={(e) => setDocType(e.target.value as TipoDocumentoCliente)}
                className="w-full p-2 bg-[#0f1011] border border-[#23252a] rounded text-[#f7f8f8] focus:border-[#e4f222]"
              >
                <option value="DNI">DNI (8 dígitos)</option>
                <option value="RUC">RUC (11 dígitos)</option>
                <option value="VARIOS">VARIOS / GENERAL</option>
              </select>
            </div>

            <div className="col-span-2">
              <label className="text-[#8a8f98] font-mono block mb-1">NÚMERO DOCUMENTO</label>
              <input
                ref={inputRef}
                type="text"
                value={docNumber}
                onChange={(e) => setDocNumber(e.target.value)}
                placeholder="Ingrese número de documento..."
                className="w-full p-2 bg-[#0f1011] border border-[#23252a] rounded text-[#f7f8f8] font-mono tabular-nums focus:border-[#e4f222]"
              />
            </div>
          </div>

          <div>
            <label className="text-[#8a8f98] font-mono block mb-1">RAZÓN SOCIAL / NOMBRE COMPLETO</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Nombre del cliente o empresa..."
              className="w-full p-2 bg-[#0f1011] border border-[#23252a] rounded text-[#f7f8f8] focus:border-[#e4f222]"
            />
          </div>

          <div>
            <label className="text-[#8a8f98] font-mono block mb-1">TARIFA / LISTA DE PRECIOS</label>
            <select
              value={tariff}
              onChange={(e) => setTariff(e.target.value as TarifaCliente)}
              className="w-full p-2 bg-[#0f1011] border border-[#23252a] rounded text-[#f7f8f8] focus:border-[#e4f222]"
            >
              <option value="PUBLICO">Lista 1: Público Retail (Sin descuento)</option>
              <option value="MAESTRO">Lista 2: Maestro de Obra (Precio Mayorista)</option>
              <option value="CONTRATISTA">Lista 3: Contratista B2B (Precio Especial)</option>
            </select>
          </div>
        </div>

        {/* Botones de Acción */}
        <div className="flex justify-end gap-2 pt-2 border-t border-[#23252a]">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-[#0f1011] text-[#8a8f98] hover:text-[#f7f8f8] border border-[#23252a] rounded transition-colors"
          >
            Cancelar (Escape)
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            className="px-4 py-2 font-mono font-bold bg-[#e4f222] text-[#08090a] rounded hover:bg-[#d3e015] transition-colors"
          >
            Confirmar Cliente (Enter)
          </button>
        </div>
      </div>
    </FocusTrapModal>
  );
};
