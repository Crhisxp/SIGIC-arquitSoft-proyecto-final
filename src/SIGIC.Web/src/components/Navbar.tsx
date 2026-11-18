"use client";

import React from "react";
import Link from "next/link";
import { ShoppingCart, User, Wrench, ShieldCheck, LogOut, Package } from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useCart } from "../context/CartContext";
import { RolClienteWeb } from "../types/web";

export const Navbar: React.FC = () => {
  const { user, isAuthenticated, logout, rolComercial, setRolComercial } = useAuth();
  const { totalItemsCount, total } = useCart();

  const handleRoleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setRolComercial(e.target.value as RolClienteWeb);
  };

  return (
    <header className="border-b border-[#23252a] bg-[#0f1011] sticky top-0 z-40 text-xs">
      <div className="max-w-7xl mx-auto px-4 h-14 flex items-center justify-between gap-4">
        {/* Brand */}
        <Link href="/" className="flex items-center gap-2 text-[#f7f8f8] hover:text-[#e4f222] transition-colors">
          <Wrench className="w-5 h-5 text-[#e4f222]" />
          <span className="font-bold tracking-tight text-sm font-mono">SIGIC Ferretería v2.2</span>
          <span className="text-[10px] px-1.5 py-0.5 rounded bg-[#23252a] text-[#8a8f98] font-mono">B2B/B2C</span>
        </Link>

        {/* Central: Tarifa selector (RF-PRE-002) */}
        <div className="flex items-center gap-2 bg-[#161718] border border-[#23252a] rounded px-2.5 py-1">
          <span className="text-[#8a8f98] text-[11px] font-mono">Tarifa Vigente:</span>
          <select
            value={rolComercial}
            onChange={handleRoleChange}
            aria-label="Tarifa Comercial"
            className="bg-transparent font-mono text-[11px] font-bold text-[#e4f222] focus:outline-none cursor-pointer"
          >
            <option value="PublicoGeneral" className="bg-[#161718] text-[#f7f8f8]">Público General</option>
            <option value="MaestroObra" className="bg-[#161718] text-[#f7f8f8]">Maestro de Obra</option>
            <option value="ContratistaB2B" className="bg-[#161718] text-[#f7f8f8]">Contratista B2B</option>
          </select>
        </div>

        {/* Right actions */}
        <div className="flex items-center gap-4">
          <Link
            href="/"
            className="text-[#8a8f98] hover:text-[#f7f8f8] transition-colors flex items-center gap-1.5"
          >
            <Package className="w-4 h-4" />
            <span>Catálogo</span>
          </Link>

          {/* Cart */}
          <Link
            href="/checkout"
            className="flex items-center gap-2 px-3 py-1.5 bg-[#161718] hover:bg-[#23252a] border border-[#23252a] rounded text-[#f7f8f8] transition-colors"
          >
            <ShoppingCart className="w-4 h-4 text-[#e4f222]" />
            <span className="font-mono sigic-tabular font-bold">
              {totalItemsCount}
            </span>
            <span className="text-[#8a8f98] font-mono sigic-tabular text-[11px]">
              (S/ {total.toFixed(2)})
            </span>
          </Link>

          {/* Auth */}
          {isAuthenticated && user ? (
            <div className="flex items-center gap-2">
              <span className="font-mono text-[#8a8f98] flex items-center gap-1">
                <ShieldCheck className="w-3.5 h-3.5 text-[#22c55e]" />
                <span className="text-[#f7f8f8] max-w-[120px] truncate">{user.username}</span>
              </span>
              <button
                type="button"
                onClick={logout}
                className="p-1.5 text-[#8a8f98] hover:text-[#ef4444] transition-colors rounded"
                title="Cerrar sesión"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <Link
              href="/login"
              className="flex items-center gap-1.5 px-3 py-1.5 bg-[#e4f222] text-[#08090a] font-bold rounded font-mono hover:bg-[#d3e015] transition-colors"
            >
              <User className="w-3.5 h-3.5" />
              <span>Ingresar</span>
            </Link>
          )}
        </div>
      </div>
    </header>
  );
};
