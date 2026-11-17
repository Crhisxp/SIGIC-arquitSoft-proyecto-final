---
doc: "docs/agentes/skills-ui.md"
tipo: "navegacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Skills de Interfaz de Usuario — SIGIC Ferretería v2.2

Este documento clasifica las habilidades de los agentes destinadas al desarrollo y revisión de las interfaces de usuario de SIGIC Ferretería v2.2.

## 1. Criterios de Selección de Skills de UI

- **Mostrador POS Industrial:** Utilizar [sigic-pos-industrial-ui](../../.agents/skills/sigic-pos-industrial-ui/SKILL.md) para componentes React 18/Vite del mostrador de ventas presencial, operación 100% por teclado (`F1`-`F12`), integración de escáner EAN-13, presupuesto estricto de viewport `1024x768` y despacho web en mostrador (`F6`).
- **Portal E-Commerce B2B/B2C:** Utilizar [sigic-ecommerce-b2b](../../.agents/skills/sigic-ecommerce-b2b/SKILL.md) para el frontend WAN Next.js 14 App Router, reglas de multitarifa por rol, exclusión estricta de productos web (`404`), checkout atómico y control de TTL de reservas y vouchers.
- **Pulido Visual y Accesibilidad:** Utilizar [sigic-frontend-polish](../../.agents/skills/sigic-frontend-polish/SKILL.md) para auditorías visuales, contraste WCAG AA, densidad de información ferretera, foco visible `:focus-visible` y manejo de los cuatro estados canónicos de interacción.

## 2. Contratos y Validación

Las interfaces de usuario deben certificarse contra los contratos del servidor MCP accesibles mediante el [indice.md](../mcp/indice.md).
