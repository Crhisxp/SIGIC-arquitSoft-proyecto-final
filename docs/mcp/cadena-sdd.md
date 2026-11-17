---
doc: "docs/mcp/cadena-sdd.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Herramienta MCP: Validador de Cadena SDD — SIGIC Ferretería v2.2

Este documento detalla el contrato y comportamiento de la herramienta `sdd_chain_validator`.

## 1. Definición y Registro

La herramienta se implementa en [sdd-chain-validator.ts](../../mcp-server/src/tools/governance/sdd-chain-validator.ts) y se expone a través del registro de gobernanza en [index.ts](../../mcp-server/src/tools/governance/index.ts).

## 2. Parámetros de Entrada

- `projectPath` (string, opcional): Ruta raíz del proyecto.
- `documentId` (string, opcional): Identificador específico de un documento a validar individualmente.
- `strictMode` (boolean, opcional): Exige cumplimiento estricto de aprobación y formato.

## 3. Metadatos YAML y Ciclo de Vida

La herramienta inspecciona los siguientes campos frontmatter:
- `doc`: ruta canónica del archivo.
- `estado`: reconoce estados como `BORRADOR`, `EN_REVISION`, `APROBADO`, `VALIDADO_PARA_EXPOSICION`, `DEPRECADO`, `RECHAZADO`.
- `depende-de` y `habilita`: listas de identificadores causales.
- `aprobado`: valor booleano de certificación.

## 4. Alcance y Límites Técnicos

- **Exploración:** Excluye carpetas ocultas (`.git`, `.gemini`, `.agents`, `node_modules`, `bin`, `obj`) con profundidad controlada.
- **Detección de Ciclos:** El algoritmo interno de la herramienta puede presentar limitaciones para emitir diagnósticos concluyentes sobre aciclicidad en ciertas configuraciones complejas. Por este motivo, la verificación de este DAG canónico se apoya en un comprobador programático independiente basado en el algoritmo de Kahn.
