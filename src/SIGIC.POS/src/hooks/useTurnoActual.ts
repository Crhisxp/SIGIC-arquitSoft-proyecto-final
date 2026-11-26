import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "../services/api";
import { TurnoCaja } from "../types/pos";

export function useTurnoActual() {
  const [turno, setTurno] = useState<TurnoCaja | null>(null);
  const [estado, setEstado] = useState<"consultando" | "listo" | "error">(() =>
    api.getToken() ? "consultando" : "listo");
  const consulta = useRef<AbortController | null>(null);

  useEffect(() => {
    function actualizar() {
      consulta.current?.abort();
      if (!api.getToken()) {
        setTurno(null);
        setEstado("listo");
        return;
      }
      const controller = new AbortController();
      consulta.current = controller;
      api.obtenerTurnoActual(undefined, controller.signal).then((actual) => {
        if (controller.signal.aborted) return;
        setTurno(actual?.estado === "Aperturado" ? actual : null);
        setEstado("listo");
      }).catch(() => {
        if (controller.signal.aborted) return;
        setTurno(null);
        setEstado("error");
      });
    }

    actualizar();
    window.addEventListener("focus", actualizar);
    return () => {
      window.removeEventListener("focus", actualizar);
      consulta.current?.abort();
    };
  }, []);

  const marcarCerrado = useCallback(() => {
    consulta.current?.abort();
    setTurno(null);
    setEstado("listo");
  }, []);

  return { turno, estado, marcarCerrado };
}
