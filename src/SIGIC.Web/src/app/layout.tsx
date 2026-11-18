import "./globals.css";
import React from "react";
import { AuthProvider } from "../context/AuthContext";
import { CartProvider } from "../context/CartContext";
import { Navbar } from "../components/Navbar";

export const metadata = {
  title: "SIGIC Ferretería v2.2 — Portal E-Commerce B2B/B2C",
  description: "Catálogo técnico ferretero, tarifas por rol, PDP de mediciones y checkout transaccional.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="es" className="dark">
      <body className="bg-[#08090a] text-[#f7f8f8] min-h-screen flex flex-col antialiased">
        <AuthProvider>
          <CartProvider>
            <Navbar />
            <main className="flex-1 max-w-7xl w-full mx-auto p-4">{children}</main>
            <footer className="border-t border-[#23252a] bg-[#0f1011] py-4 text-center text-xs text-[#8a8f98] font-mono">
              SIGIC Ferretería v2.2 • Ayacucho, Perú • Monolito Modular .NET 8 + PgBouncer + Next.js 14
            </footer>
          </CartProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
