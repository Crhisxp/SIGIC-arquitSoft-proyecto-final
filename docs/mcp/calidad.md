---
doc: "docs/mcp/calidad.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Herramienta MCP: Quality Gate Enforcer — SIGIC Ferretería v2.2

Este documento detalla el contrato y comportamiento de la herramienta `quality_gate_enforcer`.

## 1. Definición y Parámetros

Implementada en [quality-gate-enforcer.ts](../../mcp-server/src/tools/governance/quality-gate-enforcer.ts):
- `projectPath` (string, opcional): Ruta raíz del proyecto.
- `checkType` ("full" | "coverage" | "file-quality"): Tipo de verificación solicitada.
- `coverageThreshold` (number, default: 95): Umbral porcentual de cobertura exigido.
- `blockOnFailure` (boolean, default: true): Determina si un incumplimiento emite veredicto bloqueante NO-GO.

## 2. Formatos de Cobertura y Límites de Archivo

- **Cobertura Aceptada:** La herramienta procesa exclusivamente resúmenes en formato JSON (`coverage-summary.json` generado por herramientas tipo Istanbul/nyc/Jest). No admite de forma nativa reportes Cobertura XML ni formatos binarios.
- **Contención de Tamaño:** Evalúa extensiones `.ts`, `.tsx`, `.js`, `.jsx`, `.cs` y `.java` frente a un límite fijo de 300 líneas por archivo, definido en `DEFAULT_THRESHOLDS.maxFileLines`.
- **Regla de Skills:** La restricción de contención a un máximo de 160 líneas físicas para los archivos `SKILL.md` es una directiva específica de gobernanza de agentes que se verifica mediante auditorías independientes, no a través de esta herramienta.
