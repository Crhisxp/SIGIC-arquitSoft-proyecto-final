---
doc: "docs/mcp/gobernanza.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Herramientas de Gobernanza Arquitectónica y SDD — SIGIC Ferretería v2.2

Este documento describe las herramientas MCP encargadas de fiscalizar la arquitectura, la trazabilidad SDD y la calidad del código.

## 1. Clasificación y Uso de Herramientas

Las cuatro herramientas de gobernanza se distribuyen según su propósito:
- [cadena-sdd.md](cadena-sdd.md): Valida dinámicamente metadatos YAML y consistencia del grafo de especificaciones (`sdd_chain_validator`).
- [arquitectura.md](arquitectura.md): Detecta violaciones de límites de capas e importaciones indebidas en Clean Architecture (`architecture_drift_auditor`).
- [calidad.md](calidad.md): Verifica umbrales de cobertura unitaria y tamaños máximos de archivos fuente (`quality_gate_enforcer`).
- [obsidian.md](obsidian.md): Sincronización y generación de notas documentales (`obsidian_vault_sync`).

## 2. Operaciones de Lectura vs. Escritura

- Las herramientas de cadena SDD, arquitectura y calidad son exclusivamente de **lectura y análisis estático** sin efectos colaterales sobre el disco.
- La herramienta `obsidian_vault_sync` es una operación de **escritura** sobre archivos del vault.
- No se deben ejecutar herramientas en el transcurso de la documentación de sus contratos.
