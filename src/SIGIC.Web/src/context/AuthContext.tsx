"use client";

import React, { createContext, useContext, useState, useEffect, useCallback } from "react";
import { AuthWebUser, RolClienteWeb } from "../types/web";
import { webApi } from "../services/api";

interface AuthContextType {
  user: AuthWebUser | null;
  rolComercial: RolClienteWeb;
  setRolComercial: (rol: RolClienteWeb) => void;
  login: (u: string, p: string) => Promise<void>;
  register: (u: string, p: string, email?: string, nombres?: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<AuthWebUser | null>(null);
  const [rolComercial, setRolComercialState] = useState<RolClienteWeb>("PublicoGeneral");

  useEffect(() => {
    try {
      const savedUser = localStorage.getItem("sigic_web_user");
      const savedRol = localStorage.getItem("sigic_web_rol") as RolClienteWeb;
      if (savedUser) setUser(JSON.parse(savedUser));
      if (savedRol) setRolComercialState(savedRol);
    } catch {
      // ignore
    }
  }, []);

  const setRolComercial = useCallback((rol: RolClienteWeb) => {
    setRolComercialState(rol);
    try {
      localStorage.setItem("sigic_web_rol", rol);
    } catch {
      // ignore
    }
  }, []);

  const login = useCallback(async (u: string, p: string) => {
    const res = await webApi.login(u, p);
    setUser(res);
    try {
      localStorage.setItem("sigic_web_user", JSON.stringify(res));
    } catch {
      // ignore
    }
  }, []);

  const register = useCallback(async (u: string, p: string, email?: string, nombres?: string) => {
    const res = await webApi.register(u, p, email, nombres);
    setUser(res);
    try {
      localStorage.setItem("sigic_web_user", JSON.stringify(res));
    } catch {
      // ignore
    }
  }, []);

  const logout = useCallback(() => {
    setUser(null);
    try {
      localStorage.removeItem("sigic_web_user");
    } catch {
      // ignore
    }
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        rolComercial,
        setRolComercial,
        login,
        register,
        logout,
        isAuthenticated: !!user?.token,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within an AuthProvider");
  return context;
};
