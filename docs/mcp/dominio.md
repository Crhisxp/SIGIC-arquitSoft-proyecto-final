---
doc: "docs/mcp/dominio.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Herramientas de Dominio Ferretero — SIGIC Ferretería v2.2

Este documento especifica las tres herramientas MCP orientadas a la validación de reglas de negocio ferretero, consolidadas en [index.ts](../../mcp-server/src/tools/domain/index.ts).

## 1. Especificación de Herramientas

### 1.1. `auditar_especificacion_stock_sdd`
Implementada en [inventoryTools.ts](../../mcp-server/src/tools/domain/inventoryTools.ts):
- **Parámetros:** `codigoFuente` (string), `canal` ("POS" | "WEB"), `operacion` ("LECTURA_CATALOGO" | "CHECKOUT_RESERVA").
- **Reglas Verificadas:**
  - `RNF-CON-003`: fórmula de disponibilidad (Stock Disponible = Stock Físico - Stock Comprometido).
  - `RF-INV-001`: en canal WEB, productos con `disponible_en_web == false` deben responder HTTP 404 estricto.
  - `RF-WEB-005`: transacciones cortas con `SELECT ... FOR UPDATE` ordenadas ascendentemente por ID para prevenir deadlocks.

### 1.2. `validar_reserva_web_sdd`
Implementada en [inventoryTools.ts](../../mcp-server/src/tools/domain/inventoryTools.ts):
- **Parámetros:** `modalidadPago` ("PASARELA" | "MANUAL"), `ttlReservaMinutos` (number), `ttlSubidaComprobanteMinutos` (number, opcional).
- **Reglas Verificadas:** `RF-WEB-002` (TTL de 20 minutos para pasarela; hasta 240 minutos para pagos manuales con ventana máxima de 60 minutos para comprobante).

### 1.3. `validar_ergonomia_pos_sdd`
Implementada en [posTools.ts](../../mcp-server/src/tools/domain/posTools.ts):
- **Parámetros:** `codigoComponenteReact` (string), `esPantallaDespachoWeb` (boolean, opcional).
- **Reglas Verificadas:** `RF-POS-003` (navegación 100% por teclado, hotkeys F1-F12, escáner EAN-13, ausencia de clics exclusivos de ratón) y `RNF-USA-002` (contención en 1024x768).

## 2. Límites del Análisis Estático

Estas herramientas evalúan patrones léxicos y sintácticos mediante expresiones regulares sobre el código fuente analizado. Sus veredictos no sustituyen la ejecución en entorno real ni garantizan por sí solos la ausencia de errores lógicos o problemas de concurrencia en producción.
