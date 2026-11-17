---
doc: "docs/agentes/skills-sdd.md"
tipo: "navegacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Skills de Gobernanza SDD y Arquitectura — SIGIC Ferretería v2.2

Este documento organiza las habilidades de agentes dedicadas a la gobernanza metodológica y la auditoría técnica de Clean Architecture.

## 1. Criterios de Selección de Skills de Gobernanza

- **Guardián SDD:** Utilizar [sigic-sdd-guardian](../../.agents/skills/sigic-sdd-guardian/SKILL.md) como habilidad rectora ante cualquier tarea de modificación o revisión de código de inventario, reservas, checkout o componentes POS, exigiendo el cumplimiento del protocolo de tres fases y la verificación de invariantes.
- **Auditor de Clean Architecture:** Utilizar [sigic-clean-arch-auditor](../../.agents/skills/sigic-clean-arch-auditor/SKILL.md) para auditar fronteras de capas en .NET 8, orden determinista anti-deadlock en PostgreSQL 16, inmutabilidad de arqueo de caja y segregación de pools de PgBouncer.

## 2. Remisión a Herramientas MCP

Ambas skills coordinan sus evaluaciones con las herramientas automatizadas documentadas en el [indice.md](../mcp/indice.md). Ninguna de estas skills autoriza declarar certificaciones funcionales sin comprobación empírica en el entorno respectivo.
