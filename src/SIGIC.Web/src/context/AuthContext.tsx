"use client";

import React, { createContext, useContext, useState, useEffect, useRef, useCallback } from "react";
import { AuthWebUser, RolClienteWeb } from "../types/web";
import { webApi } from "../services/api";

interface AuthContextType {
  user: AuthWebUser | null;
  rolComercial: RolClienteWeb;
  login: (u: string, p: string) => Promise<void>;
  register: (u: string, p: string, email?: string, nombres?: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);
const sessionKey = "sigic_web_user";

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<AuthWebUser | null>(null);
  const restoredSession = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    restoredSession.current = controller;
    try {
      const saved = localStorage.getItem(sessionKey);
      const token: unknown = saved ? JSON.parse(saved)?.token : null;
      if (typeof token === "string" && token) {
        webApi.sesion(token, controller.signal).then((profile) => {
          if (!controller.signal.aborted) setUser(profile);
        }).catch(() => {
          // Una sesión sin confirmar conserva el perfil público.
        });
      }
    } catch {
      // El almacenamiento puede estar deshabilitado en el navegador.
    }
    return () => controller.abort();
  }, []);

  const updateSession = useCallback((profile: AuthWebUser | null) => {
    restoredSession.current?.abort();
    setUser(profile);
    try {
      if (profile) localStorage.setItem(sessionKey, JSON.stringify({ token: profile.token }));
      else localStorage.removeItem(sessionKey);
    } catch {
      // La sesión actual también funciona sin almacenamiento persistente.
    }
  }, []);

  const login = useCallback(async (u: string, p: string) => {
    updateSession(await webApi.login(u, p));
  }, [updateSession]);

  const register = useCallback(async (u: string, p: string, email?: string, nombres?: string) => {
    updateSession(await webApi.register(u, p, email, nombres));
  }, [updateSession]);

  const logout = useCallback(() => updateSession(null), [updateSession]);

  return (
    <AuthContext.Provider value={{
      user,
      rolComercial: user?.token ? user.rolComercial : "PublicoGeneral",
      login,
      register,
      logout,
      isAuthenticated: !!user?.token,
    }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within an AuthProvider");
  return context;
};
