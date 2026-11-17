---
doc: "docs/mcp/obsidian.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Herramienta MCP: Sincronizador de Vault Obsidian — SIGIC Ferretería v2.2

Este documento detalla el contrato y comportamiento de la herramienta `obsidian_vault_sync`.

## 1. Definición y Parámetros

Implementada en [obsidian-vault-sync.ts](../../mcp-server/src/tools/governance/obsidian-vault-sync.ts):
- `projectPath` (string, opcional): Raíz del proyecto.
- `vaultPath` (string, opcional): Ruta de destino para las notas generadas (default: `./obsidian-sync`).
- `syncMode` ("sdd-only" | "graph-only" | "full"): Modalidad de sincronización.
- `maxGraphNodes` (number, opcional): Límite de nodos a procesar en el grafo exportado.

## 2. Restricción Estricta y Decisión Aprobada de la Fase 2C

> **Directiva Obligatoria de Fase 2C:**  
> No ejecutar `obsidian_vault_sync` sobre este DAG canónico: su generación de enlaces a dependencias y documentos habilitados reintroduciría ciclos bidireccionales y una representación duplicada.

Esta restricción es un mandato aprobado para garantizar que la topología canónica de 79 nodos, 96 aristas dirigidas y 0 ciclos se mantenga inalterada y libre de enlaces redundantes o circulares.
