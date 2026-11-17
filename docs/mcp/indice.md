---
doc: "docs/mcp/indice.md"
tipo: "navegacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Índice de Herramientas del Servidor MCP — SIGIC Ferretería v2.2

Este directorio describe la arquitectura, herramientas y contratos del servidor MCP unificado del proyecto.

## 1. Identificación y Protocolo

- **Identificador Configurado:** `sigic-sdd-guardian` (en archivos de configuración `.agents/mcp_config.json` y `.codex/config.toml`).
- **Identidad de Proceso:** `sigic-sdd-mcp-server`.
- **Mecanismo de Transporte:** STDIO (Standard Input / Output con mensajes en formato JSON-RPC).

## 2. Herramientas Registradas

El servidor expone 7 herramientas divididas en dos grupos:
1. `auditar_especificacion_stock_sdd` (Dominio)
2. `validar_reserva_web_sdd` (Dominio)
3. `validar_ergonomia_pos_sdd` (Dominio)
4. `sdd_chain_validator` (Gobernanza)
5. `architecture_drift_auditor` (Gobernanza)
6. `quality_gate_enforcer` (Gobernanza)
7. `obsidian_vault_sync` (Gobernanza)

## 3. Ramas de Consulta Técnica

La documentación del servidor se organiza en cuatro secciones:
- [runtime.md](runtime.md): Proceso de ejecución, registro de herramientas y compatibilidad.
- [dominio.md](dominio.md): Especificación de herramientas de inventario, reservas y POS.
- [gobernanza.md](gobernanza.md): Especificación de herramientas de cadena SDD, arquitectura y calidad.
- [soporte.md](soporte.md): Utilidades internas, sandbox de rutas y parser frontmatter.
