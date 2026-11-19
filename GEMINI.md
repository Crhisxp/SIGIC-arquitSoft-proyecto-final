# Gobernanza de Desarrollo Guiado por Especificaciones (SDD) — SIGIC v2.2

Este documento rige la ejecución técnica operativa para el entorno de Gemini Flash vía CLI `agy`, reconociendo en todo momento la autoridad única del contexto maestro definido en `AGENTS.md`. No constituye una fuente paralela de directivas ni un catálogo independiente de habilidades.

Este repositorio implementa **SIGIC (Sistema Integrado de Gestión de Inventario y Comercialización)** bajo la metodología estricta **Spec-Driven Development (SDD)**. La documentación técnica en `docs/especificaciones/`, `docs/arquitectura/` y la especificación técnica oficial constituyen la **Única Fuente de Verdad (Single Source of Truth)**. Ningún agente de IA ni desarrollador está autorizado a escribir código de negocio que contradiga o ignore las especificaciones vigentes.

---

## 1. Arquitectura de Referencia e Infraestructura Crítica

- **Estilo Arquitectónico (`docs/arquitectura/estilo-arquitectonico.md`):** Monolito Modular en Capas (Cliente-Servidor) implementado con **Clean Architecture** sobre **ASP.NET Core 8 Web API**.
- **Persistencia y Segregación de Conexiones:** **PostgreSQL 16** antecedido por **PgBouncer** configurado en dos *Connection Pools* estrictamente aislados:
  1. `sigic_pos_pool` (Prioridad Alta / Baja Latencia): Garantiza disponibilidad inmediata de conexiones para terminales de mostrador POS evitando inanición por picos de tráfico web.
  2. `sigic_web_pool` (Pool Limitado / Control de Backpressure): Gestiona las lecturas asíncronas y transacciones acotadas del Portal E-Commerce.
- **Canales de Interacción:**
  1. **Canal Mostrador POS (`SPA React 18`):** Operación de misión crítica 100% por teclado y escáner láser sin ratón (`RF-POS-003`), optimizada para monitores `1024x768` sin scroll horizontal (`RNF-USA-002`) y con módulo ligero de entrega web (`RF-POS-011`).
  2. **Canal Portal Web (`Next.js`):** Catálogo asíncrono desacoplado con desfase máximo permitido `<= 30 segundos` y checkout transaccional con revalidación atómica (`RF-WEB-005`).

---

## 2. Protocolo SDD Obligatorio Antes de Generar Código

Todo agente (AGY / Gemini) **DEBE** ejecutar secuencialmente estas 3 etapas antes de emitir cualquier bloque de código C#, SQL o TypeScript/React:

1. **Fase 1 — Inspección Documental:**
   - Localizar y leer el Requerimiento Funcional (`RF-*`), Atributo de Calidad (`RNF-*`), Historia de Usuario y Decisión Arquitectónica (`ADR`) en `docs/especificaciones/` y `docs/arquitectura/`.
   - Activar la skill `.agents/skills/sigic-sdd-guardian/SKILL.md`.
2. **Fase 2 — Auditoría Previa mediante el Servidor MCP Interno (`mcp-server`):**
   - Invocar obligatoriamente `auditar_especificacion_stock_sdd` para todo código de inventario, consultas de catálogo o checkout.
   - Invocar `validar_reserva_web_sdd` cuando se configuren o evalúen tiempos de expiración (TTL) de reservas en el canal Web.
   - Invocar `validar_ergonomia_pos_sdd` al construir o modificar componentes React destinados al Mostrador POS.
3. **Fase 3 — Emisión de Código Certificado:**
   - Solo se entrega código al usuario cuando las herramientas del servidor MCP devuelven estado `CUMPLIMIENTO_VERIFICADO`.

---

## 3. Reglas de Oro Inquebrantables (Invariantes de Dominio y Concurrencia)

### Invariante 1: Gobernanza de Stock (`RNF-CON-003`)
- **PROHIBIDO** autorizar ventas, reservas o mostrar disponibilidad consultando únicamente `stock_fisico`.
- En toda entidad de dominio C#, consulta Dapper/EF Core o sentencia SQL debe aplicarse invariablemente la fórmula:
  $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$

### Invariante 2: Segregación Estricta de Catálogo Web (`RF-INV-001`)
- Los artículos cuyo atributo `disponible_en_web == false` son exclusivos de venta presencial en mostrador.
- En los endpoints y consultas de la API Pública Web, si un producto tiene `disponible_en_web == false`, el sistema **DEBE** filtrarlo en listados y retornar **HTTP `404 Not Found` absoluto** en consultas por ID/SKU o intentos de inclusión en el checkout web (jamás revelar su existencia ni su stock interno).

### Invariante 3: Orden Determinista Antideadlock y Revalidación Atómica (`RF-WEB-005`)
- Todo proceso de Checkout (POS o Web), reserva de inventario o descuento de stock que afecte múltiples ítems **DEBE**:
  1. Ordenar en memoria la lista de ítems ascendentemente por identificador de producto (`ORDER BY producto_id ASC` / `.OrderBy(i => i.ProductoId)`) **ANTES** de abrir o ejecutar sentencias de bloqueo en PostgreSQL 16, eliminando de raíz los *deadlocks* por inversión de orden de bloqueo.
  2. Ejecutar una transacción corta con bloqueo pesimista de fila (`SELECT ... FOR UPDATE`) seguido de un `UPDATE` condicionado que verifique en el predicado `WHERE (stock_fisico - stock_comprometido) >= @CantidadSolicitada`.
  3. Revalidar atómicamente tanto el **Stock Disponible** como el **Precio Vigente** en el instante exacto del checkout.

### Invariante 4: Reservas Web Dinámicas y Comprobantes (`RF-WEB-002`)
- **Pagos por Pasarela de Tarjeta:** Incrementan `stock_comprometido` con un TTL exacto e improrrogable de **20 minutos**.
- **Pagos Manuales (Yape / Plin / Transferencia Bancaria):** Incrementan `stock_comprometido` con un TTL total de **hasta 240 minutos (4 horas)** para validación administrativa, exigiendo que el cliente suba la constancia/comprobante de pago dentro de una ventana máxima de **60 minutos (1 hora)** desde la creación de la orden.

### Invariante 5: Despacho Web Centralizado y Guías Automáticas (`RF-INV-003` / `RF-WEB-002`)
- Toda reserva y preparación de pedidos originados en el canal Web se ejecuta **prioritaria y centralizadamente sobre el Almacén Central**, protegiendo el stock operativo de exhibición de las tiendas físicas.
- Cuando el cliente selecciona la modalidad **Retiro en Tienda**, la confirmación del pedido dispara la **generación automática de una Guía Interna de Traslado** desde el Almacén Central hacia la tienda de destino, la cual se recepciona y entrega mediante la pantalla ligera de despacho (`RF-POS-011`).

---

## 4. Comandos de Verificación del Proyecto en Arch Linux (`fish`)

- **Compilar y Auditar Servidor MCP:**
  `npm --prefix mcp-server run build`
- **Verificar Backend .NET 8 (Clean Architecture):**
  `dotnet build --configuration Release -warnaserror`
- **Ejecutar Suite de Pruebas de Dominio y Concurrencia:**
  `dotnet test --filter "Category=Concurrency|Category=SDD"`
- **Verificar Frontend POS (React 18) y Web (Next.js):**
  `pnpm --recursive run typecheck && pnpm --recursive run lint`
