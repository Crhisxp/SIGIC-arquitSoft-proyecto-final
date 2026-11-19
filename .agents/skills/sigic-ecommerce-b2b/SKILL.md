---
name: sigic-ecommerce-b2b
description: >-
  Guía para el Portal E-Commerce B2B/B2C en Next.js 14 App Router: tarifas por rol,
  exclusión web HTTP 404, checkout atómico, TTL reservas y carga de vouchers.
---

# Skill: Portal E-Commerce B2B/B2C — SIGIC Ferretería v2.2

Gobierna el diseño, construcción y revisión de la aplicación web pública en Next.js 14 App Router.
Alineado con [03-requisitos-funcionales.md](../../../docs/especificaciones/03-requisitos-funcionales.md) y [DESIGN.md](../../../DESIGN.md), bajo la autoridad metodológica de `AGENTS.md`.

## 1. Disparadores de Uso
- Construcción o refactorización de Server Components, Client Components o Server Actions en Next.js 14 (`RF-WEB-001`, `RF-WEB-005`).
- Integración de Ficha Técnica PDP, calculadora de conversiones y tarifas del Módulo 5 (`ProductDetailsPricing`).
- Implementación de lógica de precios segmentada por rol comercial (Público General, Maestro de Obra, Contratista).
- Integración de formularios de checkout, pasarelas de pago o módulo de carga de comprobantes manuales (`RF-WEB-002`).

## 2. Invariantes Comerciales y de Persistencia WAN
- **Segregación Estricta de Catálogo Web (`RF-INV-001`):** Los artículos con `disponible_en_web == false` son exclusivos de venta presencial en mostrador. En endpoints públicos y SSR, retornar **HTTP `404 Not Found` absoluto** por ID/SKU y filtrarlos de listados. Prohibido revelar stock o existencia de ítems exclusivos de mostrador.
- **Módulo 5: PDP, Calculadora de Mediciones y Tarifas por Rol (`ProductDetailsPricing`):**
  - *Ficha Técnica PDP:* Renderizado estructurado de atributos técnicos ferreteros y compatibilidad en Next.js.
  - *Calculadora de Mediciones (`RF-INV-005`):* Factores de conversión precisos (4 decimales) hacia la unidad base.
  - *Público General:* Precio de lista base con IGV (18%).
  - *Maestro de Obra:* Descuento por volumen y tarifa preferencial (Lista 2) en categorías de obra gruesa.
  - *Contratista:* Tarifa mayorista (Lista 3) condicionada a RUC activo y línea comercial.
  - *Protección de Margen:* En toda escala rige `precioVenta >= costoUnitario` (`MargenInvalidoException`). Prohibida venta bajo el costo.
- **Gobernanza de Stock (`RNF-CON-003`):** Toda disponibilidad en catálogo web debe calcularse como $\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$. Desfase máximo tolerado en lecturas asíncronas $\le 30\text{ segundos}$.
- **Checkout Atómico y Orden Determinista (`RF-WEB-005`):**
  - Ordenar ítems en memoria ascendentemente por identificador (`.OrderBy(i => i.ProductoId)`) ANTES de solicitar bloqueos en base de datos para prevenir deadlocks (Havender).
  - Revalidar atómicamente en PostgreSQL 16 con `SELECT ... FOR UPDATE` condicionado a `(stock_fisico - stock_comprometido) >= @Cantidad`.
  - Revalidar precios vigentes al momento del checkout; si un precio varió durante la navegación, rechazar la orden e informar al cliente.
- **TTL de Reservas y Vouchers (`RF-WEB-002`):**
  - *Pasarela de Tarjeta (Niubiz/MercadoPago):* Incrementa `stock_comprometido` con TTL exacto de **20 minutos**. Si no se confirma el pago, liberar reserva.
  - *Pago Manual (Yape / Plin / Transferencia):* TTL total de **hasta 240 minutos (4 horas)** para validación administrativa; el cliente DEBE cargar el voucher dentro de una ventana máxima de **60 minutos** desde la creación del pedido.
- **Despacho Web Centralizado (`RF-INV-003`):** Las reservas web se realizan sobre el Almacén Central. En la modalidad "Retiro en Tienda", la confirmación genera automáticamente una Guía Interna de Traslado hacia la tienda destino para entrega vía `RF-POS-011`.

## 3. Herramientas MCP del Servidor `sigic-sdd-guardian`
Las especificaciones formales de las 7 herramientas se consultan en el [indice.md](../../../docs/mcp/indice.md) de contratos MCP:
1. `validar_reserva_web_sdd`: Entradas: `modalidadPago` ("PASARELA"|"MANUAL"), `ttlReservaMinutos`, `ttlSubidaComprobanteMinutos`. Obligatoria para toda lógica de expiración de carritos y reservas web.
2. `auditar_especificacion_stock_sdd`: Entradas: `codigoFuente`, `canal` ("WEB"), `operacion` ("LECTURA_CATALOGO"|"CHECKOUT_RESERVA"). Obligatoria para verificar exclusión 404, stock disponible y bloqueos anti-deadlock.
3. `validar_ergonomia_pos_sdd`: NO APLICA en portal web (exclusiva para SPA React 18 Mostrador POS). Declarar "NO APLICA" con el motivo técnico.
4. `sdd_chain_validator`: Entradas: `projectPath`, `documentId`, `strictMode`. Valida consistencia de especificaciones.
5. `architecture_drift_auditor`: Entradas: `projectPath`, `targetPath`, `includeTests`. Audita límites de capas e importaciones.
6. `quality_gate_enforcer`: Entradas: `projectPath`, `checkType`, `coverageThreshold` (95), `blockOnFailure`. Valida umbrales de cobertura y contención de archivos.
7. `obsidian_vault_sync`: Entradas: `projectPath`, `vaultPath`, `syncMode`, `maxGraphNodes`. Esta tarea documental no requiere ejecutar sincronización sobre el vault canónico.

*Rigor Metodológico:* Prohibido inventar certificaciones o respuestas simuladas sin validación real.
