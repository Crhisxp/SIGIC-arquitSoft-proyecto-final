"use client";

import React, { FormEvent, useEffect, useRef, useState } from "react";
import { ArrowUpRight, BarChart3 } from "lucide-react";
import { adminAnaliticaService as api, AnaliticaHttpError } from "@/services/adminAnaliticaService";
import type { ConsultaAnaliticaResponse, StaffUser } from "@/types/analitica";
import styles from "./panel.module.css";

const sugerencias = [
  ["Ventas", "¿Cuáles fueron las ventas por canal?"],
  ["Ticket promedio", "¿Cuál fue el ticket promedio?"],
  ["Rotación de inventario", "¿Cuál fue la rotación de inventario?"],
  ["Quiebres de stock", "¿Cuántas posiciones están sin stock?"],
  ["Margen operativo", "¿Cuál fue el margen operativo del periodo?"],
];
const fechaLima = new Intl.DateTimeFormat("en-CA", { timeZone: "America/Lima", year: "numeric", month: "2-digit", day: "2-digit" });
const formatos = { timeZone: "America/Lima", dateStyle: "medium", timeStyle: "short" } as const;

export default function AnaliticaPage() {
  const [staff, setStaff] = useState<StaffUser | null>(null);
  const [verificando, verificar] = useState(true);
  const [ocupado, ocupar] = useState(false);
  const [error, avisar] = useState("");
  const [pregunta, preguntar] = useState("");
  const [desde, cambiarDesde] = useState(() => fechaLima.format(Date.now() - 6 * 86400000));
  const [hasta, cambiarHasta] = useState(() => fechaLima.format(Date.now() + 86400000));
  const [resultado, mostrar] = useState<ConsultaAnaliticaResponse | null>(null);
  const entrada = useRef<HTMLTextAreaElement>(null);
  const peticion = useRef<AbortController | null>(null);

  useEffect(() => {
    let activo = true;
    api.sesion().then(user => { if (activo) setStaff(user); }).catch(e => {
      if (activo && e.status !== 401) avisar("No se pudo verificar la sesión interna.");
    }).finally(() => { if (activo) verificar(false); });
    return () => { activo = false; peticion.current?.abort(); };
  }, []);

  async function ingresar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    avisar(""); ocupar(true);
    try { setStaff(await api.login(String(form.get("usuario")), String(form.get("password")))); }
    catch (e) { avisar(e instanceof Error ? e.message : "No se pudo iniciar sesión."); }
    finally { ocupar(false); }
  }

  async function consultar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const dias = (Date.parse(hasta) - Date.parse(desde)) / 86400000;
    mostrar(null); avisar("");
    if (!(dias > 0 && dias <= 366)) { avisar("El período debe contener entre 1 y 366 días."); return; }
    const ctrl = new AbortController();
    peticion.current = ctrl;
    ocupar(true);
    try {
      const datos = await api.consultar({ pregunta, desde, hastaExclusivo: hasta }, ctrl.signal);
      if (!ctrl.signal.aborted) mostrar(datos);
    } catch (e) {
      if (ctrl.signal.aborted) return;
      if (e instanceof AnaliticaHttpError && e.status === 401) setStaff(null);
      avisar(e instanceof Error ? e.message : "No se pudo completar la consulta.");
    } finally { if (peticion.current === ctrl) { peticion.current = null; ocupar(false); } }
  }

  async function salir() {
    avisar(""); ocupar(true);
    try { await api.logout(); setStaff(null); mostrar(null); preguntar(""); }
    catch { avisar("No se pudo cerrar la sesión. Vuelva a intentarlo."); }
    finally { ocupar(false); }
  }

  return <main className={styles.panel}>
    <header className={styles.cabecera}>
      <div><BarChart3 size={24} aria-hidden="true" /><h1>Copiloto analítico</h1></div>
      {staff && <button disabled={ocupado} onClick={salir}>Cerrar sesión · {staff.username}</button>}
    </header>
    {error && <p role="alert" className={styles.error}>{error}</p>}
    {verificando ? <p role="status">Verificando sesión…</p> : !staff ? (
      <form className={styles.ingreso} onSubmit={ingresar}>
        <h2>Acceso del personal</h2>
        <label>Usuario<input name="usuario" autoComplete="username" required /></label>
        <label>Contraseña<input name="password" type="password" autoComplete="current-password" required /></label>
        <button className={styles.primario} disabled={ocupado}>{ocupado ? "Ingresando…" : "Ingresar"}</button>
      </form>
    ) : <div className={styles.columnas}>
      <aside><h2>Consultas rápidas</h2>{sugerencias.map(([titulo, texto]) =>
        <button key={titulo} disabled={ocupado} onClick={() => { preguntar(texto); entrada.current?.focus(); }}>
          {titulo} <ArrowUpRight size={15} aria-hidden="true" /></button>)}</aside>
      <section><form onSubmit={consultar}>
        <label>Pregunta<textarea ref={entrada} value={pregunta} required maxLength={500}
          onChange={e => preguntar(e.target.value)} disabled={ocupado} /></label>
        <div className={styles.fechas}>
          <label>Desde<input type="date" required value={desde} disabled={ocupado} onChange={e => cambiarDesde(e.target.value)} /></label>
          <label>Hasta (exclusivo)<input type="date" required value={hasta} disabled={ocupado} onChange={e => cambiarHasta(e.target.value)} /></label>
        </div>
        <div className={styles.acciones}><button className={styles.primario} disabled={ocupado}>{ocupado ? "Consultando…" : "Consultar"}</button>
          {peticion.current && <button type="button" onClick={() => peticion.current?.abort()}>Cancelar</button>}</div>
      </form>
      {resultado && <article aria-live="polite">
        <h2>Resumen ejecutivo</h2><p>{resultado.resumen}</p>
        {resultado.hechos.length > 0 && <table><caption>Indicadores</caption>
          <thead><tr><th scope="col">Indicador</th><th scope="col">Valor</th><th scope="col">Unidad</th></tr></thead>
          <tbody>{resultado.hechos.map(h => <tr key={h.etiqueta}><th scope="row">{h.etiqueta}</th>
            <td className="sigic-tabular">{h.valor}</td><td>{h.unidad}</td></tr>)}</tbody></table>}
        <small>Corte en Lima · {new Intl.DateTimeFormat("es-PE", formatos).format(new Date(resultado.corteUtc))}</small>
      </article>}</section>
    </div>}
  </main>;
}
