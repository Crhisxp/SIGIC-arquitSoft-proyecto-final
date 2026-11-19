# SIGIC Ferretería v2.2

Sistema de inventario, comercialización y caja para ferreterías de Ayacucho, Perú.
El backend es un **Monolito Modular en ASP.NET Core 8**, organizado con Clean
Architecture y DDD. Comparte inventario transaccional entre Mostrador y E-commerce.

| Componente | Tecnología | Responsabilidad |
|---|---|---|
| Backend `src/SIGIC.Domain`, `Application`, `Infrastructure`, `WebApi` | .NET 8, EF Core y Dapper | Casos de uso, API REST, stock y arqueo ciego |
| Persistencia | PostgreSQL 16 + PgBouncer 1.22.1 | OLTP ACID; alias POS de 50 y Web de 30 conexiones |
| Mostrador `src/SIGIC.POS` | React 18 + Vite | Atención LAN, teclado y escáner, viewport 1024×768 |
| Portal `src/SIGIC.Web` | Next.js 14 App Router + React 18 | Canal B2B/B2C; el catálogo actual obtiene datos en cliente |

El stock disponible se calcula siempre como físico menos comprometido. Los
checkouts bloquean productos en orden ascendente y revalidan precio y disponibilidad.
La analítica segregada y el copiloto IA tienen contrato documental; su datamart,
ETL, adaptador y pantalla administrativa siguen pendientes de implementación.

- [Ejecución local, servicios, compilación y pruebas](docs/GUIA-EJECUCION.md).
- [Actores e historias de usuario](docs/especificaciones/01-actores.md).
- [Restricciones y atributos de calidad](docs/especificaciones/05-restricciones.md).

```text
.
├── README.md, AGENTS.md, GEMINI.md, DESIGN.md, SIGIC.sln
├── docs/
│   ├── GUIA-EJECUCION.md
│   ├── especificaciones/       # Requisitos y RF-ANA-001
│   ├── arquitectura/          # Decisiones, diseño, C4 e imágenes
│   ├── entregables/           # Sprint 2, Sprint 3 y exposición
│   ├── bitacora/              # Estado, historial y plan histórico
│   └── agentes/, mcp/, implementacion/
├── src/                       # Backend y ambos frontends
├── tests/                     # xUnit, E2E, k6 y contratos Postman
├── infrastructure/            # Configuración de PgBouncer
├── scripts/                   # CI, orquestación y generadores de exposición
└── mcp-server/                # Gobierno SDD
```

La navegación se inicia en `AGENTS.md`. El gate documental verifica conjuntos
canónicos de 85 nodos y 107 aristas, sin ciclos y con un máximo de cuatro destinos
por nodo. Las actas del runbook distinguen evidencia previa, ejecución y pendientes.
