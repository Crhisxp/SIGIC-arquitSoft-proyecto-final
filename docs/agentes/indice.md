---
doc: "docs/agentes/indice.md"
tipo: "navegacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Índice del Catálogo de Skills para Agentes — SIGIC Ferretería v2.2

Este índice organiza el catálogo de habilidades especializadas disponibles para los agentes que colaboran en el desarrollo guiado por especificaciones de SIGIC Ferretería v2.2.

## 1. Autoridad y Estructura del Catálogo

- La autoridad rectora y contexto maestro del proyecto reside exclusivamente en el documento `AGENTS.md`.
- El directorio `.agents/skills/` constituye la fuente canónica de todas las skills especializadas.
- Los directorios espejos bajo `.gemini/skills/` son copias operativas para la integración con el CLI agy y se mantienen con paridad binaria exacta frente a sus fuentes canónicas, sin enlazarse directamente en este grafo.
- La selección o activación de una skill no equivale a la aprobación formal de código; toda solución debe ser validada rigurosamente contra las especificaciones vigentes.

## 2. Especialidades Disponibles

El catálogo se estructura en dos ramas principales:
- [skills-ui.md](skills-ui.md): Habilidades orientadas a las interfaces de usuario (Mostrador POS, E-Commerce B2B/B2C y Pulido UI).
- [skills-sdd.md](skills-sdd.md): Habilidades de gobernanza SDD, auditoría de Clean Architecture y verificación de calidad.
