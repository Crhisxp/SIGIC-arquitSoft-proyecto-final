# Bitácora de Orquestación Híbrida — SIGIC Ferretería v2.2

Este documento es la **bitácora viva de coordinación y trazabilidad** entre **Codex (`GPT-6.1-Sol` - Orquestador / Planificador)** y **Gemini Flash (`gemini-3.8-flash-high` vía CLI `agy` - Ejecutor)**. Registra el progreso de las fases de desarrollo, control de cuotas y estado de los artefactos del proyecto.

---

## 1. Resumen de Fases del Flujo Híbrido

| Fase | Descripción del Hito | Estado | Responsables |
| :---: | :--- | :---: | :--- |
| **Fase 1** | **Infraestructura de Orquestación y Reglas de Agente:** Configuración de `.gitignore` para Obsidian, Contexto Maestro `AGENTS.md`, Script Wrapper con control de cuota de 5h ([scripts/agy_runner.fish](scripts/agy_runner.fish)) e plan maestro en [PLAN-MIGRACION-SIGIC.md](PLAN-MIGRACION-SIGIC.md) e inicialización de bitácora viva. | **COMPLETADA** | Codex + Gemini Flash |
| **Fase 2A** | **Skills Especializadas, Paridad Dual y Configuración Codex:** 4 skills compactas ($\le 160$ líneas), sincronización `.agents`/`.gemini`, `.codex/config.toml`, verificación STDIO del servidor unificado (7 tools) y catálogo en `AGENTS.md`. | **COMPLETADA** | Gemini Flash |
| **Fase 2B** | **Auditoría de Dominio en `src/`:** Inspección profunda de entidades, agregados y value objects en `src/SIGIC.Domain/` frente a especificaciones SDD en `01` a `04`. | **PENDIENTE** | Codex + Gemini Flash |
| **Fase 2C** | **Implementación del DAG Documental y Topología Canónica:** Formalización de 79 nodos, 96 aristas dirigidas, 0 ciclos, raíz única en `AGENTS.md`, 18 notas puente, paridad byte a byte en skills y filtros en `.obsidian/app.json`. | **COMPLETADA** | Codex + Gemini Flash |
| **Fase 3A** | **Conciliación Modular, Interacción POS Híbrida y SDD Gate:** 6 Bounded Contexts independientes (PDP-Mediciones-Descuentos y Seguridad independientes), operación POS híbrida (teclado 100% y ratón 100%, botones $\ge 44 \times 44\text{ px}$, auto-refoco $\le 50\text{ ms}$, ceros a la izquierda EAN-13), formalización F9/F11, margen RF-PRE-002, workflow sigic-sdd-gate.yml y paridad binaria de skills. | **COMPLETADA** | Gemini Flash |
| **Fase 3B** | **Ampliación de Capas de Aplicación e Infraestructura:** Implementación de controladores, servicios de aplicación MediatR y adaptadores de persistencia EF Core / Dapper. | **PENDIENTE** | Codex + Gemini Flash |
| **Fase 4** | **Certificación de Calidad y Pruebas Continuas:** Ejecución de suites xUnit ($\ge 95\%$ cobertura), pruebas E2E Playwright y k6. | **PENDIENTE** | Codex + Gemini Flash |

---

## 2. Detalle de Entregas — Fase 1 (Completada)

* **Fecha de Culminación:** 2026-11-16
* **Hitos Alcanzados:**
  1. **Protección de `.gitignore`:** Incorporación de reglas para herramientas locales y Obsidian (`.obsidian/`, `*.log`, `.agy_last_run.tmp`) preservando las exclusiones existentes de .NET y Node.js.
  2. **Contexto Maestro `AGENTS.md`:** Documento ejecutivo para Codex detallando la arquitectura del Monolito Modular en .NET 8, las 4 invariantes sagradas de dominio, prohibición absoluta de wikilinks (sintaxis de doble corchete) y reglas de fórmulas KaTeX limpias (sin guiones bajos dentro de `\text{}`).
  3. **Script de Control de Cuotas [scripts/agy_runner.fish](scripts/agy_runner.fish):** Wrapper en Fish shell que ejecuta `agy` con `gemini-3.8-flash-high`, intercepta códigos de error de cuota (`429`, `RESOURCE_EXHAUSTED`, `rate limit`), muestra cuenta regresiva en consola y reintenta automáticamente en bloques de 15 minutos (`sleep 900`) hasta la renovación de la cuota de 5 horas.
  4. **Suite Unitaria Certificada:** Verificación de 13/13 pruebas unitarias pasando al 100% en `SIGIC.Domain.Tests`.

---

## 3. Detalle de Entregas — Fase 2A (Completada)

* **Fecha de Culminación:** 2026-11-16
* **Fuentes y Repositorios Consultados en `/tmp/skills-raw/`:**
  - `anthropics-skills` (commit `e4bb5d960d87126ff3b3a8af1a104b7db67560ba`): `frontend-design`, `webapp-testing`.
  - `impeccable` (commit `d41d9860b0094dcb68030657da63461212835aaa`, Apache 2.0): `impeccable`.
  - `superpowers` (commit `22f0b463e7a5e579904dd6211a520321035526f7`, MIT): `writing-plans`, `systematic-debugging`, `test-driven-development`, `verification-before-completion`.
  - `ui-ux-pro-max` (commit `e59ff376a41f7ca47b7541ad5c116495cbade3ee`, MIT): `ui-ux-pro-max`, `ui-styling`, `design-system`.
* **Métricas de Longitud y Paridad de Skills (8 archivos generados):**
  - `sigic-pos-industrial-ui`: **50 líneas** (Límite: $\le 160$). Paridad `.agents` / `.gemini`: **100% idéntica (0 diff)**.
  - `sigic-ecommerce-b2b`: **54 líneas** (Límite: $\le 160$). Paridad `.agents` / `.gemini`: **100% idéntica (0 diff)**.
  - `sigic-frontend-polish`: **53 líneas** (Límite: $\le 160$). Paridad `.agents` / `.gemini`: **100% idéntica (0 diff)**.
  - `sigic-clean-arch-auditor`: **51 líneas** (Límite: $\le 160$). Paridad `.agents` / `.gemini`: **100% idéntica (0 diff)**.
* **Excepción de Fondo en POS:**
  - Mostrador POS configurado con fondo `#0f172a` y acento `#e4f222` por instrucción expresa del usuario. Modificación puntual documentada frente al `#08090a` de `DESIGN.md` sin alterar dicho archivo ni propagar la excepción al portal web.
* **Hallazgos y Discrepancias Documentales Registradas:**
  - **Conflicto F9:** `DESIGN.md` asigna a `F9` la función "Encolar / Recuperar Ticket", mientras que `01-analisis-de-sistema/02-historias-del-usuario.md` le asigna "Totalizar y Panel Mixto". Se exige conciliación formal antes de codificar la acción.
  - **Ausencia F11:** Sin asignación funcional en la especificación. Se prohíbe inventar atajos no definidos.
* **Configuración y Verificación del Servidor MCP (`sigic-sdd-guardian`):**
  - Archivo de configuración `.codex/config.toml` creado con comando `node ./mcp-server/build/server.js`, validado con `python tomllib`.
  - Verificación CLI Codex: `codex mcp list` detecta el servidor con estado `enabled`.
  - Verificación STDIO: Protocolo JSON-RPC probado (`initialize` y `tools/list`) devolviendo exitosamente las 7 herramientas unificadas (`auditar_especificacion_stock_sdd`, `validar_reserva_web_sdd`, `validar_ergonomia_pos_sdd`, `sdd_chain_validator`, `architecture_drift_auditor`, `quality_gate_enforcer`, `obsidian_vault_sync`).
  - *Distinción de Carga:* Conexión y catálogo 100% verificados vía SDK Stdio. La carga activa en sesiones interactivas de Codex requiere la concesión de confianza de proyecto en el entorno de ejecución de Codex.

---

---

## 4. Detalle de Entregas — Fase 2C: DAG Documental y Topología Canónica (Completada)

* **Fecha y Hora de Culminación:** 2026-11-16 13:22:00-05:00
* **Alcance de Modificación:**
  - Archivos maestros actualizados: `AGENTS.md`, `README.md`, `GEMINI.md`, `DESIGN.md`, `ESTADO-ORQUESTACION.md`, `PLAN-MIGRACION-SIGIC.md`, `HISTORIAL-CAMBIOS-OBSERVACIONES.md`.
  - 21 documentos SDD analíticos y arquitectónicos actualizados con secciones de continuidad trazable: `01-analisis-de-sistema/` (01 a 08), `02-arquitectura-software/` (6 documentos), `04-modelo-c4/` (4 documentos), `03-diseno-de-software/diseno-interno-modulos/` (3 documentos) y `05-pruebas-y-calidad/02-pruebas-e2e.md`.
  - Creación de 18 notas puente breves y sustantivas:
    1. `05-pruebas-y-calidad/01-pruebas-unitarias.md`
    2. `05-pruebas-y-calidad/03-pruebas-carga.md`
    3. `docs/implementacion/producto.md`
    4. `docs/implementacion/arqueo-caja.md`
    5. `docs/implementacion/soporte-dominio.md`
    6. `docs/agentes/indice.md`
    7. `docs/agentes/skills-ui.md`
    8. `docs/agentes/skills-sdd.md`
    9. `docs/mcp/indice.md`
    10. `docs/mcp/runtime.md`
    11. `docs/mcp/compatibilidad.md`
    12. `docs/mcp/dominio.md`
    13. `docs/mcp/gobernanza.md`
    14. `docs/mcp/cadena-sdd.md`
    15. `docs/mcp/arquitectura.md`
    16. `docs/mcp/calidad.md`
    17. `docs/mcp/obsidian.md`
    18. `docs/mcp/soporte.md`
* **Métricas y Propiedades del Grafo Verificadas Programáticamente:**
  - Nodos Canónicos: **79** (52 documentos Markdown y 27 archivos de evidencia textual).
  - Aristas Dirigidas Únicas: **96** (destinos_extraidos == set(DAG[documento]) al 100%).
  - Ciclos: **0** (verificado mediante algoritmo de Kahn en memoria).
  - Raíz Única: **`AGENTS.md`** (grado de entrada 0 único).
  - Alcanzabilidad: **79 / 79 nodos alcanzables** desde `AGENTS.md`.
  - Enlaces Rotos / Destinos Faltantes o Adicionales: **0**.
  - Contención Saliente: Máximo 4 destinos por documento canónico.
* **Adaptación de Skills y Paridad Binaria:**
  - `sigic-pos-industrial-ui`: 45 líneas (paridad byte a byte 100% entre `.agents` y `.gemini`).
  - `sigic-ecommerce-b2b`: 44 líneas (paridad byte a byte 100% entre `.agents` y `.gemini`).
  - `sigic-frontend-polish`: 42 líneas (paridad byte a byte 100% entre `.agents` y `.gemini`).
  - `sigic-clean-arch-auditor`: 46 líneas (paridad byte a byte 100% entre `.agents` y `.gemini`).
  - `sigic-sdd-guardian`: 71 líneas (sin espejo, restricción de sincronización documentada).
  - Las 7 herramientas MCP citadas en cada skill remitiendo a contratos formales en `docs/mcp/indice.md`.
* **Higiene Visual de Obsidian:**
  - `userIgnoreFilters` actualizado en `.obsidian/app.json` con la lista exacta de 12 exclusiones ordenadas.
* **Integridad de Archivos Protegidos:**
  - `docs/jwe/flattened/decrypt/functions/flattenedDecrypt.md` intacto (0 bytes).
  - Runner [scripts/agy_runner.fish](scripts/agy_runner.fish), proyectos `.csproj`, fuentes C#/TS y configuraciones MCP intactos.
* **Delimitación y Límites de la Fase:**
  - Durante la Fase 2C **no se ejecutaron**: PostgreSQL 16, pruebas unitarias (`dotnet test`), Playwright E2E, k6 ni la herramienta `obsidian_vault_sync`.
  - Esta verificación documental certifica la estructura acíclica de la especificación, pero **no constituye certificación del software funcional**.
* **Estado de Pendientes tras Fase 3A:**
  - Conflicto de `F9` entre `DESIGN.md` y `01-analisis-de-sistema/02-historias-del-usuario.md`: **RESUELTO** (`F9: Totalizar / Pago Mixto`).
  - Asignación funcional para `F11`: **RESUELTO** (`F11: Encolar / Recuperar Ticket en Espera`).
  - Operación POS híbrida (teclado 100% y ratón 100%): **FORMALIZADO** (botones $\ge 44 \times 44\text{ px}$, auto-foco EAN-13 $\le 50\text{ ms}$, ceros preservados como string).
  - Excepción autorizada de fondo POS `#0f172a` frente a superficies `#08090a` de `DESIGN.md`: Documentada y vigente.
  - Servidor MCP unificado: Operativo en STDIO con 7 herramientas.

---

## 5. Detalle de Entregas — Fase 3A: Conciliación Modular, Interacción POS Híbrida y SDD Gate (Completada)

* **Fecha de Culminación:** 2026-11-17
* **Hitos y Entregables:**
  1. **Seis Bounded Contexts Independientes y Delimitación de REP:** Desdoblamiento formal de Mostrador POS (`SIGIC.Modules.SalesPOS`) y Caja/Tesorería (`SIGIC.Modules.CashManagement`) con ciclo de vida y puertos desacoplados. Formalización del Módulo 5 como "Módulo de Detalles del Producto (PDP), Mediciones y Descuentos" (`SIGIC.Modules.ProductDetailsPricing`), absorbiendo ficha técnica enriquecida, conversión fraccionaria (`RF-INV-005`), descuentos por volumen y tarifas B2B/B2C (Público, Maestro, Contratista). Módulo 6 de Seguridad y Auditoría (`SIGIC.Modules.Security`) independiente con claims RS256 segregados. En `01-analisis-de-sistema/03-requisitos-funcionales.md` (§1) se delimita explícitamente que REP es un mecanismo transversal de consulta analítica sobre el Datamart segregado (`sigic_olap`) y no un Bounded Context independiente.
  2. **Operación POS Híbrida, Secuencia Inequívoca de Foco y Atajos F9/F11:** Conciliación canónica de `F9` (Totalizar / Pago Mixto) y `F11` (Encolar / Recuperar Ticket en Espera). Operación 100% teclado y 100% ratón sin penalización ergonómica: botones clickeables/táctiles $\ge 44 \times 44\text{ px}$ con badge visible, EAN-13 tratado como string preservando ceros a la izquierda. Secuencia de foco inequívoca especificada en `DESIGN.md` (§3.4): retorno automático al escáner en $\le 50\text{ ms}$ tras acciones en grilla, focus trap en apertura de modales (`F2`, `F3`, `F9`, `F12`) para digitación inmediata por teclado, y restitución incondicional a `F1` al cerrar. Fórmulas KaTeX de las skills depuradas eliminando tabuladores residuales antes de `ext{...}`.
  3. **Erradicación de Inconsistencias de Disponibilidad:** Eliminación de "Validar Stock Físico Directo" en `04-modelo-c4/Nivel3-Diagrama-de-Componentes.md` y `ValidarExistenciasFisicas` en `04-modelo-c4/Nivel4-DiagramadeCodigo.md`. Toda verificación valida invariablemente la fórmula: $\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido} \ge \text{CantidadSolicitada}$.
  4. **Gobernanza de Margen Comercial:** Diferenciación en `01-analisis-de-sistema/03-requisitos-funcionales.md` entre `RF-PRE-002` (venta bajo costo prohibida sin excepción ni bypass de Superadmin lanzando `MargenInvalidoException`) y `RF-INV-008` (importación masiva con validación y rollback atómico).
  5. **Reproducibilidad en Git y TDD del SDD Gate:** Saneamiento de `.gitignore` (exclusión acotada a `node_modules/` y `build/` de `mcp-server/`), incorporando al seguimiento selectivo los 22 nodos canónicos previamente ignorados. Validación ejecutada exitosamente desde un árbol Git temporal limpio (`/tmp/sigic-clean-tree/`) generado exclusivamente desde el índice de Git. TDD del Gate ejecutando el código Python real extraído del workflow (`/tmp/sigic-fase3a-evidence/test_gate_extracted.py`), demostrando 7/7 casos superados.
  6. **Ejecución Real de Pruebas Unitarias y Resolución de Quality Gate GO:** `dotnet test tests/SIGIC.Domain.Tests/` ejecutado con `--collect:"XPlat Code Coverage"` y logger TRX (`/tmp/sigic-fase3a-evidence/TestResults/domain.trx`): 13/13 pruebas exitosas (100% aprobación). Cobertura real en XML: 203/209 líneas (97.13%) y 59/62 ramas (95.16%) [corte 181/185 líneas (97.84%), 60/62 ramas (96.77%)]. Conversión automatizada a `coverage-summary.json` sin alterar conteos y veredicto definitivo emitido por `quality_gate_enforcer`: `✅ GO — Quality Gate Aprobado`.
  7. **Certificación de Dominio y Calidad:** 42/42 aserciones aprobadas en `acceptance_phase3a.py`. Paridad SHA-256 byte a byte 100% verificada en las 4 parejas de skills especializadas ($\le 160$ líneas). Auditoría MCP de las 7 herramientas ejecutada con salidas reales documentadas.

---

## 6. Próximo Paso — Fase 2B: Auditoría de Dominio en `src/`

* **Objetivo de la Fase 2B:**
  1. Auditar el grado de cobertura y paridad entre las especificaciones en `01-analisis-de-sistema/`, `02-arquitectura-software/`, `03-diseno-de-software/`, `04-modelo-c4/` y las entidades implementadas en `src/SIGIC.Domain/`.
  2. Ejecutar las herramientas de certificación MCP `architecture_drift_auditor` y `auditar_especificacion_stock_sdd` sobre el código C#.
  3. Identificar brechas de modelo en entidades ferreteras clave (`Producto`, `ArqueoCaja`, `ReservaWeb`, `GuiaTraslado`).

---

## 7. Registro Histórico de Ejecuciones

| Timestamp | Intento | Prompt / Tarea Ejecutada | Resultado | Notas de Cuota / Salida |
| :--- | :---: | :--- | :---: | :--- |
| `2026-11-16 11:08` | 1 | Inicialización de Fase 1 (Orquestación Híbrida y AGENTS.md) | **EXITOSO** | Entorno configurado, script validado con `fish -n`, pruebas en verde (13/13). |
| `2026-11-16 12:45` | 1 | Fase 2A: Skills especializadas, paridad dual, config Codex y verificación MCP | **EXITOSO** | 4 skills creadas ($\le 54$ líneas), paridad byte a byte 100%, config.toml válido, 7 tools verificadas en STDIO. |
| `2026-11-16 13:22` | 1 | Fase 2C: Implementación del DAG documental canónico (79 nodos, 96 aristas, 0 ciclos) | **EXITOSO** | Topología 100% verificada en memoria; paridad byte a byte; filtros en `.obsidian/app.json`. |
| `2026-11-17 02:45` | 1 | Fase 3A: Conciliación de seis módulos, POS híbrido, atajos F9/F11, workflow sigic-sdd-gate.yml | **EXITOSO** | 36/36 aserciones aprobadas, dotnet test 13/13 ok (97.83% cov), 0 wikilinks, DAG 79/96/0 validado. |
| `2026-11-17 03:15` | 2 | Fase 3A (Corrección): Reproducibilidad en Git limpio, Quality Gate GO, erradicación de discrepancias de stock y TDD workflow | **EXITOSO** | 42/42 aserciones aprobadas, 7/7 casos TDD workflow, cobertura 97.13% / 95.16% (GO), DAG 79/96/0 en workspace y clean tree. |

### ✅ Ejecución Exitosa - 2026-11-16 12:48:25 (Intento 1)
- **Estado:** Finalizado con código 0 sin errores de cuota.

### ⏸️ Pausa por Cuota Alcanzada - 2026-11-16 13:25:17 (Intento 1)
- **Motivo:** Patrón de cuota detectado (429 / RESOURCE_EXHAUSTED / Rate Limit).
- **Acción:** Espera automática de 15 minutos en bucle hasta renovación.

### ✅ Ejecución Exitosa - 2026-11-17 00:51:05 (Intento 1)
- **Estado:** Finalizado con código 0 sin errores de cuota.

### ✅ Ejecución Exitosa - 2026-11-17 02:03:18 (Intento 1)
- **Estado:** Finalizado con código 0 sin errores de cuota.

### ✅ Ejecución Exitosa - 2026-11-17 02:45:10 (Intento 1)
- **Estado:** Finalizado con código 0 sin errores de cuota.
- **Entregables:** 6 módulos independientes, operación POS híbrida, atajos F9/F11, RF-PRE-002, workflow `.github/workflows/sigic-sdd-gate.yml`, paridad binaria de skills y verificación en verde (36/36).

### ✅ Ejecución Exitosa - 2026-11-17 03:15:30 (Intento 2 - Corrección Integral)
- **Estado:** Finalizado con código 0 sin errores de cuota.
- **Entregables:** Reproducibilidad de workflow desde árbol Git limpio (`/tmp/sigic-clean-tree/`), resolución de Quality Gate GO (cobertura 97.13%, 203/209 líneas y 59/62 ramas), 7/7 casos TDD con código extraído, erradicación de inconsistencias de stock en C3/C4, erradicación de tabuladores en fórmulas de skills, mapa de 6 contextos con delimitación de REP y 42/42 aserciones en verde.

### ✅ Ejecución Exitosa - 2026-11-17 02:55:47 (Intento 1)
- **Estado:** Finalizado con código 0 sin errores de cuota.

### ✅ Ejecución Exitosa - 2026-11-17 03:16:16 (Intento 1)
- **Estado:** Finalizado con código 0 sin errores de cuota.

### ✅ Ejecución Exitosa - 2026-11-17 07:16:06 (Intento 1)
- **Estado:** Finalizado con código 0 sin errores de cuota.
