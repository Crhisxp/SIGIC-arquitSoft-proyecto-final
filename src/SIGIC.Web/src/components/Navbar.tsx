"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { ShoppingCart, User, Wrench, ShieldCheck, LogOut, Package, BarChart3 } from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useCart } from "../context/CartContext";
import { adminAnaliticaService, staffSessionChanged } from "../services/adminAnaliticaService";

export const Navbar: React.FC = () => {
  const { user, isAuthenticated, logout } = useAuth();
  const { totalItemsCount, total } = useCart();
  const [hasStaffSession, setHasStaffSession] = useState(false);

  useEffect(() => {
    let request: AbortController | undefined;
    let refresh: number | undefined;

    const checkSession = () => {
      request?.abort();
      window.clearTimeout(refresh);
      const controller = new AbortController();
      request = controller;
      adminAnaliticaService.sesion(controller.signal).then(() => {
        if (controller.signal.aborted) return;
        setHasStaffSession(true);
        refresh = window.setTimeout(checkSession, 30_000);
      }).catch(() => {
        if (!controller.signal.aborted) setHasStaffSession(false);
      });
    };

    checkSession();
    window.addEventListener(staffSessionChanged, checkSession);
    window.addEventListener("focus", checkSession);
    return () => {
      request?.abort();
      window.clearTimeout(refresh);
      window.removeEventListener(staffSessionChanged, checkSession);
      window.removeEventListener("focus", checkSession);
    };
  }, []);

  return (
    <header className="border-b border-[#23252a] bg-[#0f1011] sticky top-0 z-40 text-xs">
      <div className="max-w-7xl mx-auto px-4 h-14 flex items-center justify-between gap-4">
        {/* Brand */}
        <Link href="/" className="flex items-center gap-2 text-[#f7f8f8] hover:text-[#e4f222] transition-colors">
          <Wrench className="w-5 h-5 text-[#e4f222]" />
          <span className="font-bold tracking-tight text-sm">Ferretería SIGIC Ayacucho</span>
        </Link>

        {/* Right actions */}
        <div className="flex items-center gap-4">
          <Link
            href="/"
            className="text-[#8a8f98] hover:text-[#f7f8f8] transition-colors flex items-center gap-1.5"
          >
            <Package className="w-4 h-4" />
            <span>Catálogo</span>
          </Link>

          {hasStaffSession && <Link
            href="/admin/analitica"
            className="text-[#8a8f98] hover:text-[#e4f222] transition-colors flex items-center gap-1.5 px-2.5 py-1 rounded bg-[#161718] border border-[#23252a] hover:border-[#e4f222]/40 font-mono text-[11px]"
          >
            <BarChart3 className="w-3.5 h-3.5 text-[#e4f222]" />
            <span className="font-semibold">Analítica</span>
          </Link>}

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
