---
doc: "docs/mcp/arquitectura.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Herramienta MCP: Auditor de Drift Arquitectónico — SIGIC Ferretería v2.2

Este documento detalla el contrato y comportamiento de la herramienta `architecture_drift_auditor`.

## 1. Definición y Parámetros

La herramienta está implementada en [architecture-drift-auditor.ts](../../mcp-server/src/tools/governance/architecture-drift-auditor.ts):
- `projectPath` (string, opcional): Directorio raíz del proyecto.
- `targetPath` (string, opcional): Subdirectorio o archivo específico a inspeccionar.
- `includeTests` (boolean, opcional): Determina si se incluyen los proyectos de prueba en la auditoría.

## 2. Reglas de Capas en Clean Architecture

La herramienta analiza sentencias `using` e `import` verificando:
- **Domain:** No puede depender de Application, Infrastructure ni WebApi.
- **Application:** Solo puede depender de Domain.
- **Infrastructure:** Depende de Domain y Application; no debe ser consumida indebidamente por Domain.
- **WebApi:** Punto de entrada que orquesta Application e Infrastructure.

## 3. Límites del Análisis

La herramienta realiza inspección estática de dependencias léxicas. No verifica aislamiento de transacciones de base de datos, políticas de hilos concurrentes ni la segregación física de pools en PgBouncer.
