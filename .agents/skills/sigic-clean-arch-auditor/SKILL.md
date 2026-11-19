---
name: sigic-clean-arch-auditor
description: >-
  Auditoría técnica de Clean Architecture, concurrencia ACID y límites de capas
  en ASP.NET Core 8, PostgreSQL 16 y PgBouncer para el monolito modular de SIGIC.
---

# Skill: Auditor de Clean Architecture y Persistencia — SIGIC Ferretería v2.2

Gobierna la auditoría técnica del backend ASP.NET Core 8 LTS, base de datos PostgreSQL 16 y connection pooler PgBouncer 1.22.
Alineado con [estilo-arquitectonico.md](../../../docs/arquitectura/estilo-arquitectonico.md), bajo la autoridad metodológica de `AGENTS.md`.

## 1. Disparadores de Uso
- Auditoría de código en `src/SIGIC.Domain`, `src/SIGIC.Application`, `src/SIGIC.Infrastructure` o `src/SIGIC.WebApi`.
- Implementación o refactorización de handlers de checkout, reserva de inventario o arqueo de caja.
- Verificación de límites de capas, referencias entre proyectos `.csproj` y pruebas de concurrencia.

## 2. Invariantes Arquitectónicas y de Persistencia
- **Regla de Dependencia de Clean Architecture:**
  - *Dominio (`SIGIC.Domain`):* Núcleo puro sin dependencias de Entity Framework Core, Dapper, SQL, controladores o paquetes de infraestructura.
  - *Aplicación (`SIGIC.Application`):* Orquesta casos de uso (MediatR/CQRS) definiendo contratos e interfaces; no referencia infraestructura concreta.
  - *Infraestructura (`SIGIC.Infrastructure`):* Implementa adaptadores, repositorios y persistencia PostgreSQL 16.
  - *Punto de Entrada (`SIGIC.WebApi`):* Composición de dependencias en `Program.cs`. Verificar referencias reales en archivos `.csproj` además de sentencias `using`.
- **Gobernanza de Stock (`RNF-CON-003`):** Prohibido consultar disponibilidad únicamente con `stock_fisico`. La fórmula matemática obligatoria en dominio, consultas Dapper y SQL es:
  $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$
- **Orden Determinista Anti-Deadlock (`RF-WEB-005`):** Todo checkout multilínea (POS o Web) DEBE ordenar en memoria los ítems por identificador ascendente (`.OrderBy(i => i.ProductoId)`) ANTES de adquirir bloqueos en base de datos. En SQL, ejecutar `SELECT ... FOR UPDATE` ordenado por `producto_id ASC` y `UPDATE` condicionado con `WHERE (stock_fisico - stock_comprometido) >= @CantidadSolicitada`. Verificar filas afectadas y rollback inmediato ante discrepancia. Prohibido retener cerrojos durante llamadas a pasarelas externas o esperas de I/O.
- **Arqueo Ciego e Inmutabilidad (`RF-CAJ-002`):** La API jamás debe devolver saldos teóricos al cajero antes del cierre. La tabla `caja_arqueo` opera en modo append-only con permisos `REVOKE UPDATE, DELETE` para el rol de aplicación; los intentos de alteración deben fallar con error PostgreSQL `42501` (`insufficient_privilege`).
- **Segregación de Pools en PgBouncer (`RNF-CON-001`):**
  - `sigic_pos_pool`: 50 conexiones fijas dedicadas para LAN POS (adquisición $< 1\text{ ms}$, $P95 < 250\text{ ms}$ como metas a medir bajo carga).
  - `sigic_web_pool`: 30 conexiones compartidas para WAN Web con backpressure y timeout de cola de 3 segundos.

## 3. Criterios de Rigor y Evidencia
- **Evidencia en Ejecución:** No afirmar que análisis estático o expresiones regulares prueban propiedades en tiempo de ejecución. Exigir suites de concurrencia y pruebas de stock insuficiente.
- **Umbral de Calidad:** Cobertura de pruebas unitarias en `SIGIC.Domain` $\ge 95\%$ respaldada por reporte formal de cobertura vigente.

## 4. Herramientas MCP del Servidor `sigic-sdd-guardian`
Las especificaciones formales de las 7 herramientas se consultan en el [indice.md](../../../docs/mcp/indice.md) de contratos MCP:
1. `architecture_drift_auditor`: Entradas: `projectPath`, `targetPath`, `includeTests`. Detecta violaciones de capas e importaciones cruzadas prohibidas.
2. `auditar_especificacion_stock_sdd`: Entradas: `codigoFuente`, `canal`, `operacion`. Audita stock físico/comprometido y bloqueos ordenados en C# o SQL.
3. `validar_reserva_web_sdd`: Entradas: `modalidadPago`, `ttlReservaMinutos`, `ttlSubidaComprobanteMinutos`. Valida TTL en servicios de aplicación.
4. `quality_gate_enforcer`: Entradas: `projectPath`, `checkType`, `coverageThreshold` (95), `blockOnFailure`. Certifica umbral de cobertura y tamaño de archivos.
5. `sdd_chain_validator`: Entradas: `projectPath`, `documentId`, `strictMode`. Valida consistencia documental.
6. `validar_ergonomia_pos_sdd`: NO APLICA en backend (exclusivo para React 18). Declarar "NO APLICA" con el motivo técnico.
7. `obsidian_vault_sync`: Entradas: `projectPath`, `vaultPath`, `syncMode`, `maxGraphNodes`. Esta tarea documental no requiere ejecutar sincronización sobre el vault canónico.

*Rigor Metodológico:* Prohibido inventar certificaciones o asumir resueltos problemas de concurrencia sin verificación empírica.
