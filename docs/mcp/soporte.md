---
doc: "docs/mcp/soporte.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Utilidades de Soporte del Servidor MCP — SIGIC Ferretería v2.2

Este documento detalla los componentes de soporte interno que asisten a las herramientas del servidor MCP.

## 1. Componentes de Soporte

- **Aislamiento de Rutas:** [sandbox.ts](../../mcp-server/src/core/sandbox.ts) valida que cualquier ruta solicitada resuelva estrictamente dentro de la raíz del proyecto SIGIC, impidiendo accesos no autorizados mediante `assertReadable` y `assertWritable`.
- **Parser de Metadatos:** [yaml-frontmatter.ts](../../mcp-server/src/core/yaml-frontmatter.ts) implementa un analizador ligero de metadatos YAML mediante expresiones regulares sin dependencias de paquetes externos; extrae campos como `doc`, `tipo`, `estado`, `depende-de` y `habilita`.
- **Tipos y Contratos:** [types.ts](../../mcp-server/src/tools/types.ts) define las interfaces compartidas `SddToolHandler` y `SddAuditCheck`, estandarizando los niveles de severidad `BLOCKER`, `CRITICAL` e `INFO`.

## 2. Límites Funcionales

El analizador de frontmatter está diseñado para extraer tipos escalares y listas simples de cadenas; no constituye un parser YAML de especificación completa. Asimismo, el módulo sandbox restringe el espacio de rutas sin proporcionar aislamiento de procesos ni sandboxing a nivel de sistema operativo.
