---
name: sigic-frontend-polish
description: >-
  Revisión y refinamiento visual y de interacción UI en interfaces de SIGIC:
  tokens, contraste, densidad, jerarquía, foco accesible y estados de carga/error.
---

# Skill: Refinamiento y Pulido Frontend — SIGIC Ferretería v2.2

Gobierna la auditoría estética, ergonomía visual y accesibilidad en las interfaces de usuario (POS React 18 y Portal Web Next.js 14).
Alineado con [DESIGN.md](../../../DESIGN.md), bajo la autoridad metodológica de `AGENTS.md`.

## 1. Disparadores de Uso
- Auditoría o refactorización de estilos, tokens CSS, componentes UI compartidos o tipografía.
- Corrección de problemas de contraste (WCAG AA), foco accesible, legibilidad en pantallas industriales o densidad visual.
- Implementación o estandarización de los cuatro estados canónicos (vacío, carga, error y éxito) en formularios o tablas.

## 2. Invariantes de Diseño y Tokens (`DESIGN.md`)
- **Tokens de Superficie:** Base `#08090a`, paneles secundarios `#121316`, bordes sutiles `rgba(255,255,255,0.08)`.
  - *Excepción Autorizada:* El Mostrador POS utiliza como fondo `#0f172a` con acento `#e4f222` por requisito de usuario. Esta excepción es exclusiva del POS y no debe trasladarse a la web.
- **Tokens de Acento y Semántica:** Acento industrial `#e4f222` (anillos de foco, acciones principales POS), éxito `#22c55e`, advertencia `#f59e0b`, peligro/error `#ef4444`.
- **Foco Accesible Determinista:** Todo elemento interactivo accesible por teclado debe exhibir un indicador visible nítido (`:focus-visible` con anillo `2px solid #e4f222` o token de acento). Prohibido eliminar el outline sin proporcionar reemplazo visual equivalente.
- **Tipografía y Densidad de Información:**
  - Valores monetarios, stocks, SKU y códigos de barras obligatoriamente con `font-variant-numeric: tabular-nums` (ej. JetBrains Mono o Inter Tabular).
  - Badges de dimensión ferretera (`.sigic-dimension-badge`) con `flex-shrink: 0` para evitar el truncamiento de medidas (`1/2"`, `2-1/2"`).
- **Los Cuatro Estados Canónicos:** Toda vista de datos o formulario debe implementar explícitamente:
  1. *Estado Vacío:* Mensaje claro y accionable sin layout colapsado.
  2. *Estado de Carga:* Skeleton loaders con dimensiones reservadas para evitar layout shifts (CLS).
  3. *Estado de Error:* Alerta descriptiva con acción de reintento o código de error rastreable.
  4. *Estado de Éxito:* Retroalimentación inmediata no intrusiva con persistencia de datos.

## 3. Herramientas MCP del Servidor `sigic-sdd-guardian`
Las especificaciones formales de las 7 herramientas se consultan en el [indice.md](../../../docs/mcp/indice.md) de contratos MCP:
1. `validar_ergonomia_pos_sdd`: Entradas: `codigoComponenteReact`, `esPantallaDespachoWeb`. Audita viewport 1024x768, badges de dimensión y hotkeys en componentes POS.
2. `auditar_especificacion_stock_sdd`: Entradas: `codigoFuente`, `canal`, `operacion`. Audita cálculos de stock y presentación.
3. `validar_reserva_web_sdd`: Entradas: `modalidadPago`, `ttlReservaMinutos`, `ttlSubidaComprobanteMinutos`. Valida tiempos de reserva en checkout.
4. `sdd_chain_validator`: Entradas: `projectPath`, `documentId`, `strictMode`. Valida consistencia de especificaciones.
5. `architecture_drift_auditor`: Entradas: `projectPath`, `targetPath`, `includeTests`. Audita límites de dependencias en UI.
6. `quality_gate_enforcer`: Entradas: `projectPath`, `checkType`, `coverageThreshold` (95), `blockOnFailure`. Certifica umbral de cobertura y contención de archivos.
7. `obsidian_vault_sync`: Entradas: `projectPath`, `vaultPath`, `syncMode`, `maxGraphNodes`. Esta tarea documental no requiere ejecutar sincronización sobre el vault canónico.

*Rigor Metodológico:* Prohibido inventar certificaciones estéticas o accesibilidad WCAG sin inspección de contrastes y DOM.
