---
doc: "docs/mcp/compatibilidad.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Capa de Compatibilidad de Herramientas MCP — SIGIC Ferretería v2.2

Este documento describe los módulos de reexportación que preservan la compatibilidad histórica de importaciones en el servidor MCP.

## 1. Reexportaciones de Herramientas de Dominio

Los siguientes archivos funcionan como envoltorios de compatibilidad:
- [inventoryTools.ts](../../mcp-server/src/tools/inventoryTools.ts): reexporta las implementaciones de `inventoryTools.js` ubicadas bajo `src/tools/domain/`.
- [posTools.ts](../../mcp-server/src/tools/posTools.ts): reexporta las implementaciones de `posTools.js` ubicadas bajo `src/tools/domain/`.

Estos archivos no constituyen herramientas adicionales ni agregan funcionalidad nueva; su único propósito es mantener la validez de referencias heredadas dentro de la base de código.
