# Contexto Maestro para Agentes de IA — SIGIC Ferretería v2.2

Este documento constituye la referencia de contexto de alta densidad para **Codex (GPT-6.1-Sol - Orquestador / Planificador)** y **Gemini Flash (gemini-3.8-flash-high vía CLI agy - Ejecutor)**. Todo agente debe acatar estrictamente estas directivas.

---

## 1. Identidad del Proyecto y Arquitectura

* **Sistema:** SIGIC Ferretería v2.2 (Sistema Integrado de Gestión de Inventario y Comercialización - Ayacucho, Perú).
* **Documentación Base:** Consultar [README.md](README.md) para la visión del repositorio y [GEMINI.md](GEMINI.md) para directivas del entorno ejecutor.
* **Metodología:** Desarrollo Guiado por Especificaciones (SDD – *Spec-Driven Development*).
* **Catálogo de Agentes:** El directorio de habilidades se estructura en [docs/agentes/indice.md](docs/agentes/indice.md).
* **Backend:** Monolito Modular en **ASP.NET Core 8 LTS**, Clean Architecture y Domain-Driven Design (DDD).
* **Persistencia y Conexiones:** **PostgreSQL 16** intermediado por **PgBouncer 1.22** en modo *Transaction Pooling*.
* **Canales de Interacción:**
  1. **Mostrador POS (`SPA React 18 / Vite`):** Operación de misión crítica en red local (LAN), 100% por teclado (`F1`-`F12`) y escáner láser sin ratón (`RF-POS-003`), viewport `1024x768` (`RNF-USA-002`).
  2. **Portal E-commerce (`Next.js 14 SSR`):** Canal digital WAN con catálogo público asíncrono y checkout transaccional (`RF-WEB-001`, `RF-WEB-005`).

---

## 2. Invariantes de Dominio Inviolables

1. **Gobernanza de Stock (`RNF-CON-003`):**
   - Prohibido autorizar ventas o consultar disponibilidad únicamente con `stock_fisico`.
   - Fórmula matemática obligatoria en dominio, consultas Dapper y SQL:
     $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$
2. **Orden Determinista Anti-Deadlock (`RF-WEB-005`):**
   - Todo checkout multilínea (POS o Web) DEBE ordenar en memoria los ítems ascendentemente por identificador (`ORDER BY producto_id ASC` / `.OrderBy(i => i.ProductoId)`) ANTES de adquirir bloqueos en PostgreSQL 16 (Algoritmo de Havender).
   - Transacción pesimista corta con `SELECT ... FOR UPDATE` y condición atómica `WHERE (stock_fisico - stock_comprometido) >= @CantidadSolicitada`.
3. **Arqueo Ciego e Inmutable Append-Only (`RF-CAJ-002` / `AC03`):**
   - Cero visibilidad de saldos teóricos al momento del cierre de caja.
   - Tabla `caja_arqueo` con permisos revocados en PostgreSQL (`REVOKE UPDATE, DELETE`). Cualquier intento de modificación retroactiva debe fallar con `PostgresException 42501 permission denied`.
4. **Segregación Estricta de Pools en PgBouncer (`RNF-CON-001` / `DA-02`):**
   - `sigic_pos_pool` (50 conexiones dedicadas fijas en LAN, prioridad alta, latencia adquisición $< 1\text{ ms}$, $P95 < 250\text{ ms}$).
   - `sigic_web_pool` (30 conexiones compartidas en WAN, control de *backpressure* y *queue timeout* de 3 s).

---

## 3. Convenciones de Documentación (Obsidian + GitHub)

* **Enlaces Markdown Relativos:** Utilizar SIEMPRE la sintaxis estándar de Markdown `[Texto](ruta/archivo.md)` (como texto literal de enlace relativo). Queda **TERMINANTEMENTE PROHIBIDO el uso de wikilinks (sintaxis de doble corchete)** para garantizar compatibilidad nativa en GitHub y visores web.
* **Sintaxis KaTeX Limpia:**
  - En expresiones matemáticas (`$...$` o `$$...$$`), **NUNCA incluir guiones bajos `_` dentro de `\text{}`** (ej. prohibido `\text{stock_fisico}`, usar `\text{Stock Físico}`).
  - Los guiones bajos dentro de `\text{}` disparan el error crítico de renderizado de KaTeX `'_' allowed only in math mode` (cajas rojas en GitHub).
  - Unidades y latencias en formato limpio: `sub-15 ms`, `$P95 < 250\text{ ms}$`, `$\le 400\text{ ms}$`.

---

## 4. Flujo de Trabajo Híbrido Planner-Executor y MCP

* **División de Roles:**
  - **Codex (`GPT-6.1-Sol`):** Orquestador y planificador. Diseña soluciones, descompone tareas en pasos atómicos, audita cumplimiento arquitectónico y genera prompts quirúrgicos.
  - **Gemini Flash (`gemini-3.8-flash-high` vía CLI `agy`):** Ejecutor rápido. Ejecuta modificaciones de archivos, búsquedas en el árbol de directorios, refactorizaciones y suites de pruebas (`dotnet test`).
* **Servidor MCP Local (`mcp-server/`):**
  - Servidor unificado `sigic-sdd-mcp-server` con **7 herramientas registradas**:
    - **Dominio Ferretero (3 tools):**
      - `auditar_especificacion_stock_sdd`: Audita stock disponible (Físico - Comprometido), exclusión web (404) y bloqueo pesimista `SELECT ... FOR UPDATE` ordenado.
      - `validar_reserva_web_sdd`: Audita TTL de reservas y comprobantes (20 min pasarela / 240 min manual con 60 min voucher).
      - `validar_ergonomia_pos_sdd`: Audita React 18 en POS (100% hotkeys F1-F12 y resolución 1024x768).
    - **Gobernanza Arquitectónica y SDD (4 tools):**
      - `sdd_chain_validator`: Valida dinámicamente el encadenamiento de dependencias y estados en documentos SDD.
      - `architecture_drift_auditor`: Detecta violaciones de límites de capas e importaciones indebidas en Clean Architecture.
      - `quality_gate_enforcer`: Evalúa cobertura de pruebas unitarias (umbral >= 95%) y límites de tamaño de archivo (veredicto GO/NO-GO).
      - `obsidian_vault_sync`: Sincroniza documentos y grafos a notas Markdown con metadatos y enlaces relativos estándar `[Texto](ruta.md)` (sin wikilinks).
  - Configuración: `.agents/mcp_config.json` y `.codex/config.toml` apuntando a `./mcp-server/build/server.js`.
  - Skill guardián raíz: `sigic-sdd-guardian` (`.agents/skills/sigic-sdd-guardian/SKILL.md`).
* **Catálogo de Skills Especializadas (Sincronización Dual `.agents` y `.gemini`):**

| Nombre de la Skill | Disparador Principal de Uso | Ruta Primaria (`.agents`) | Ruta Espejo (`.gemini`) |
| :--- | :--- | :--- | :--- |
| `sigic-pos-industrial-ui` | Construcción y revisión de UI Mostrador POS React 18/Vite, atajos `F1`-`F12`, escáner EAN-13, contención estricta en `1024x768` y despacho web `F6` | `.agents/skills/sigic-pos-industrial-ui/SKILL.md` | `.gemini/skills/sigic-pos-industrial-ui/SKILL.md` |
| `sigic-ecommerce-b2b` | Desarrollo del Portal E-commerce Next.js 14, multitarifa (Público/Maestro/Contratista), exclusión web 404, checkout atómico, TTL reservas y vouchers | `.agents/skills/sigic-ecommerce-b2b/SKILL.md` | `.gemini/skills/sigic-ecommerce-b2b/SKILL.md` |
| `sigic-frontend-polish` | Pulido visual y de interacción, auditoría de tokens de `DESIGN.md`, contraste, densidad, foco visible `:focus-visible` y los 4 estados canónicos | `.agents/skills/sigic-frontend-polish/SKILL.md` | `.gemini/skills/sigic-frontend-polish/SKILL.md` |
| `sigic-clean-arch-auditor` | Auditoría de Clean Architecture en .NET 8, transacciones pesimistas anti-deadlock, arqueo ciego inmutable, pools PgBouncer y cobertura $\ge 95\%$ | `.agents/skills/sigic-clean-arch-auditor/SKILL.md` | `.gemini/skills/sigic-clean-arch-auditor/SKILL.md` |

* **Bitácora de Estado:** Toda iteración orquestada se registra en [ESTADO-ORQUESTACION.md](docs/bitacora/ESTADO-ORQUESTACION.md) e `docs/bitacora/HISTORIAL-CAMBIOS-OBSERVACIONES.md`.
