import "./globals.css";
import React from "react";
import { AuthProvider } from "../context/AuthContext";
import { CartProvider } from "../context/CartContext";
import { Navbar } from "../components/Navbar";

export const metadata = {
  title: "Ferretería SIGIC Ayacucho",
  description: "Materiales, herramientas y suministros ferreteros con retiro en tienda o despacho a domicilio en Ayacucho.",
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
              Ferretería SIGIC Ayacucho · Ayacucho, Perú
            </footer>
          </CartProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
