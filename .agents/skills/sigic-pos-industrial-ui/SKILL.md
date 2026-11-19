---
name: sigic-pos-industrial-ui
description: >-
  Guía técnica y ergonómica para UI de Mostrador POS en React 18/Vite: operación 100%
  teclado y escáner, viewport 1024x768 sin scroll, tokens, hotkeys y despacho F6.
---

# Skill: Interfaz Industrial POS de Mostrador — SIGIC Ferretería v2.2

Gobierna el diseño, construcción y revisión de componentes React 18/Vite para el Mostrador POS presencial.
Alineado con [02-historias-del-usuario.md](../../../docs/especificaciones/02-historias-del-usuario.md) y [DESIGN.md](../../../DESIGN.md), bajo la autoridad metodológica de `AGENTS.md`.

## 1. Disparadores de Uso
- Creación o refactorización de pantallas, modales o hooks del Mostrador POS (`RF-POS-003`).
- Implementación del módulo ligero de entrega en tienda de pedidos web (`RF-POS-011`).
- Integración de listeners globales de teclado, escáner láser EAN-13 y validación ergonómica.

## 2. Invariantes Técnicas y Presupuesto de Viewport (`1024x768`)
- **Contención Estricta (`RNF-USA-002`):** `1024px` de ancho por `768px` de alto fijos. Prohibido el scroll horizontal y el scroll global de ventana (`overflow: hidden`). Solo la tabla central de ítems admite scroll vertical interno.
- **Presupuesto Físico Vertical (768px):** Franja A Telemetría (`36px`) + Franja B Escáner/Cliente (`52px`) + Franja C Área Central (`560px`) + Franja D Zócalo Liquidación/Atajos (`120px` = `64px` tributario + `56px` grilla hotkeys).
- **Presupuesto Horizontal (1024px):** Tabla izquierda de facturación (`714px`) + Inspector técnico derecho (`310px`). Computar bordes de `1px` y paddings para evitar desbordamientos reales.
- **Excepción Explícita de Color POS:** Por directiva de usuario, el fondo POS es `#0f172a` con acento industrial `#e4f222`. Esta excepción modifica la base `#08090a` de [DESIGN.md](../../../DESIGN.md) únicamente en el POS y NO debe propagarse al portal web.
- **Tipografía y Legibilidad:** Datos numéricos, SKU y precios obligatoriamente monoespaciados con `font-variant-numeric: tabular-nums`. Descripciones ferreteras en 2 líneas (`line-clamp-2`) acompañadas de badge de dimensión con `flex-shrink: 0` (`.sigic-dimension-badge`) para no ocultar medidas críticas (`#12 x 2-1/2"`).
- **Fórmula de Stock:** Toda disponibilidad mostrada debe calcularse como $\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$.

## 3. Navegación y Operación Híbrida 100% Teclado y 100% Ratón
- **Operación Híbrida Completa:** UI operable 100% por teclado y 100% por ratón. Botones de acción de al menos $44 \times 44\text{ px}$ con badge visible del atajo (`F1` a `F12`).
- **Auto-Refocus y Secuencia de Foco:** En grilla y acciones directas, el foco retorna al input EAN-13 (`F1`) en $\le 50\text{ ms}$. Al abrir modales (`F2`, `F3`, `F7`, `F9`, `F12`), el foco se transfiere de inmediato al primer input editable (focus trap) para digitación inmediata; al cerrar el modal (`Escape` o confirmar), se restituye incondicionalmente al escáner (`F1`).
- **Foco Determinista:** Indicador visual activo con anillo `#e4f222` (`outline: 2px solid #e4f222`). Todo modal cerrado (`Escape` / confirmación) restituye el foco visible al input de escaneo (`F1`).
- **Escáner EAN-13 (`RF-INV-004`):** Capturar código de barras preservando ceros iniciales como texto; agregar cantidad 1 o incrementar fila existente; ante error de lectura mantener foco sin bloquear la digitación; distinguir lecturas repetidas válidas.
- **Atajos Oficiales Conciliados:**
  - `F1` (Escáner/SKU), `F2` (Búsqueda paramétrica), `F3` (Cliente RUC/DNI), `F4` (Editar cantidad), `F5` (Stock multialmacén), `F6` (Despacho web `RF-POS-011`), `F7` (Sustitutos), `F8` (Eliminar ítem).
  - `F9` (**Totalizar / Pago Mixto**): Abre panel de liquidación y cobro mixto (efectivo, tarjeta, Yape/Plin).
  - `F10` (Boleta Electrónica), `F12` (Factura Electrónica), `↑`/`↓` (Navegar tabla), `Enter` (Confirmar), `Escape` (Cancelar/F1).
  - `F11` (**Encolar / Recuperar Ticket en Espera**): Retiene ticket hasta por 2 horas sin movimientos de Kardex ni reserva definitiva (`RF-POS-005`), revalidando stock al recuperar.
  *(Resolución Fase 3A: superadas advertencias históricas de conflicto F9 o ausencia F11).*
- **Despacho Web F6 (`RF-POS-011`):** Muestra únicamente pedidos en estado "Listo para Retiro" con Guía Interna generada (`RF-INV-003`). El cajero no valida vouchers ni descarga comprobantes financieros.

## 4. Herramientas MCP del Servidor `sigic-sdd-guardian`
Las especificaciones formales de las 7 herramientas se consultan en el [indice.md](../../../docs/mcp/indice.md) de contratos MCP:
1. `validar_ergonomia_pos_sdd`: Entradas: `codigoComponenteReact`, `esPantallaDespachoWeb`. Aplica obligatoriamente a todo componente o hook React del POS.
2. `auditar_especificacion_stock_sdd`: Entradas: `codigoFuente`, `canal` ("POS"), `operacion` ("LECTURA_CATALOGO"|"CHECKOUT_RESERVA"). Aplica a cálculo de stock y transacciones.
3. `validar_reserva_web_sdd`: NO APLICA en POS presencial (exclusiva para reservas WAN). Declarar "NO APLICA" con el motivo técnico.
4. `sdd_chain_validator`: Entradas: `projectPath`, `documentId`, `strictMode`. Valida consistencia de especificaciones.
5. `architecture_drift_auditor`: Entradas: `projectPath`, `targetPath`, `includeTests`. Audita límites de capas e importaciones prohibidas.
6. `quality_gate_enforcer`: Entradas: `projectPath`, `checkType`, `coverageThreshold` (95), `blockOnFailure`. Valida cobertura y límites de tamaño de archivo.
7. `obsidian_vault_sync`: Entradas: `projectPath`, `vaultPath`, `syncMode`, `maxGraphNodes`. Esta tarea documental no requiere ejecutar sincronización sobre el vault canónico.

*Rigor Metodológico:* Prohibido inventar certificaciones funcionales o dar por válidos componentes sin verificación técnica.
