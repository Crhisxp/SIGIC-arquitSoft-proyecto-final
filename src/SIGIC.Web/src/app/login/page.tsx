"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { User, Lock, Mail, AlertTriangle, ArrowLeft, ShieldCheck } from "lucide-react";
import { useAuth } from "../../context/AuthContext";

export default function LoginPage() {
  const router = useRouter();
  const { login, register } = useAuth();

  const [tab, setTab] = useState<"login" | "register">("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [email, setEmail] = useState("");
  const [nombres, setNombres] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    try {
      if (tab === "login") {
        await login(username.trim(), password);
      } else {
        if (password.length < 8) {
          throw new Error("La contraseña debe contener al menos 8 caracteres.");
        }
        await register(username.trim(), password, email.trim() || undefined, nombres.trim() || undefined);
      }
      router.push("/");
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Error durante la autenticación");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="max-w-md mx-auto my-8 p-6 bg-[#0f1011] border border-[#23252a] rounded-lg text-xs space-y-6">
      <div className="flex justify-between items-center">
        <Link href="/" className="inline-flex items-center gap-1.5 text-[#8a8f98] hover:text-[#f7f8f8] font-mono transition-colors">
          <ArrowLeft className="w-4 h-4" /> Catálogo
        </Link>
        <span className="font-mono text-[#8a8f98] flex items-center gap-1">
          <ShieldCheck className="w-4 h-4 text-[#e4f222]" /> Mi cuenta
        </span>
      </div>

      <div className="flex border-b border-[#23252a] font-mono">
        <button
          type="button"
          onClick={() => { setTab("login"); setError(null); }}
          className={`flex-1 py-2 font-bold transition-colors ${
            tab === "login" ? "border-b-2 border-[#e4f222] text-[#f7f8f8]" : "text-[#8a8f98] hover:text-[#f7f8f8]"
          }`}
        >
          Iniciar Sesión
        </button>
        <button
          type="button"
          onClick={() => { setTab("register"); setError(null); }}
          className={`flex-1 py-2 font-bold transition-colors ${
            tab === "register" ? "border-b-2 border-[#e4f222] text-[#f7f8f8]" : "text-[#8a8f98] hover:text-[#f7f8f8]"
          }`}
        >
          Registrarse
        </button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        {tab === "register" && (
          <>
            <div>
              <label className="text-[#8a8f98] font-mono block mb-1">NOMBRES Y APELLIDOS O RAZÓN SOCIAL:</label>
              <div className="relative">
                <User className="w-4 h-4 text-[#8a8f98] absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  value={nombres}
                  onChange={(e) => setNombres(e.target.value)}
                  placeholder="Juan Pérez o Constructora SAC"
                  className="w-full pl-9 pr-3 py-2 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-mono focus:border-[#e4f222] focus:outline-none"
                />
              </div>
            </div>

            <div>
              <label className="text-[#8a8f98] font-mono block mb-1">CORREO ELECTRÓNICO:</label>
              <div className="relative">
                <Mail className="w-4 h-4 text-[#8a8f98] absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="cliente@ferreteria.pe"
                  className="w-full pl-9 pr-3 py-2 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-mono focus:border-[#e4f222] focus:outline-none"
                />
              </div>
            </div>
          </>
        )}

        <div>
          <label className="text-[#8a8f98] font-mono block mb-1">NOMBRE DE USUARIO O DNI/RUC:</label>
          <div className="relative">
            <User className="w-4 h-4 text-[#8a8f98] absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              required
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="cliente_ayacucho"
              className="w-full pl-9 pr-3 py-2 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-mono focus:border-[#e4f222] focus:outline-none"
            />
          </div>
        </div>

        <div>
          <label className="text-[#8a8f98] font-mono block mb-1">CONTRASEÑA:</label>
          <div className="relative">
            <Lock className="w-4 h-4 text-[#8a8f98] absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full pl-9 pr-3 py-2 bg-[#161718] border border-[#23252a] rounded text-[#f7f8f8] font-mono focus:border-[#e4f222] focus:outline-none"
            />
          </div>
        </div>

        {error && (
          <div className="p-3 bg-[#ef4444]/15 border border-[#ef4444] rounded text-[#ef4444] font-mono flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <button
          type="submit"
          disabled={isLoading}
          className="w-full py-2.5 bg-[#e4f222] text-[#08090a] font-mono font-bold rounded hover:bg-[#d3e015] disabled:opacity-40 transition-colors"
        >
          {isLoading ? "Procesando..." : tab === "login" ? "Ingresar al Portal" : "Crear Cuenta de Cliente Web"}
        </button>
      </form>
    </div>
  );
}
