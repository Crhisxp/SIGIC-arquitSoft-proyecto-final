# Diseño Interno de Módulos (Bounded Contexts) — SIGIC Ferretería

> **Documento:** `03-diseno-de-software/diseno-interno-modulos/diseno-interno-de-modulos.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Disciplina:** Diseño Detallado de Software y Domain-Driven Design (DDD)  
> **Patrón Arquitectónico:** Monolito Modular con Bounded Contexts Cohesivos

---

## 1. Mapa de Contextos Delimitados (Context Map)

El sistema **SIGIC Ferretería v2.2** se estructura internamente en cinco **Contextos Delimitados (Bounded Contexts)** independientes, desplegados dentro del mismo proceso en ASP.NET Core 8 Web API pero con encapsulamiento estricto de modelo de dominio y esquemas de persistencia:

```mermaid
flowchart TD
    subgraph CoreDomain ["Núcleo Transaccional Central"]
        CTX_INV["1. Bounded Context: Inventario y Almacenes\n(Kardex CPP, Stock Físico/Comprometido, Guías)"]
        CTX_POS["2. Bounded Context: Ventas Mostrador POS\n(Facturación Rápida, Pagos Mixtos, Despacho RF-POS-011)"]
        CTX_WEB["3. Bounded Context: E-commerce y Pedidos\n(Catálogo, Carrito, TTL Dinámico, Checkout Atómico)"]
    end

    subgraph SupportingDomain ["Dominios de Soporte"]
        CTX_CAJ["4. Bounded Context: Caja y Tesorería\n(Turnos, Arqueo Ciego, Consola Vouchers Backoffice)"]
        CTX_PRE_SEC["5. Bounded Context: Tarifas y Seguridad\n(Listas Mayoristas, Autenticación RS256, RBAC)"]
    end

    CTX_WEB -->|Consulta Stock & Reserva TTL| CTX_INV
    CTX_POS -->|Descuenta Stock Inmediato| CTX_INV
    CTX_POS -->|Registra Movimientos de Cobro| CTX_CAJ
    CTX_WEB -->|Valida Comprobantes Yape/Plin| CTX_CAJ
    CTX_WEB -->|Aplica Listas según Perfil| CTX_PRE_SEC
    CTX_POS -->|Aplica Listas Mostrador| CTX_PRE_SEC
    CTX_POS -->|Entrega Pedidos Retiro en Tienda| CTX_WEB
```

---

## 2. Especificación Detallada por Módulo

### 2.1. Módulo 1: Inventario y Almacenes (`SIGIC.Modules.Inventory`)
- **Propósito:** Control centralizado de existencias físicas y comprometidas, movimientos de almacén, valorización por Costo Promedio Ponderado y segregación multicanal.
- **Entidades y Agregados:**
  - `Articulo` (Root Entity): Código SKU, EAN-13, nombre técnico, unidad base de medida, familia, subfamilia, factor de conversión y bandera `disponible_en_web` (`bool`).
  - `InventarioStock`: Saldo por almacén discriminado en `stock_fisico` y `stock_comprometido`.
  - `KardexMovimiento`: Historial inmutable de entradas y salidas con saldo resultante, costo unitario de entrada y nuevo CPP.
  - `GuiaInternaTraslado`: Traslados inter-almacén con máquina de estados finita: `Borrador` $\rightarrow$ `En Tránsito` $\rightarrow$ `Recibido` / `Rechazado` (`RF-INV-003`).
- **Reglas de Negocio Clave:**
  - **Segregación de Catálogo:** Si `Articulo.DisponibleEnWeb == false`, el catálogo público y su API lo ocultan al 100 % (responde `HTTP 404`).
  - **Fórmula de Stock Disponible:**
    $$\text{Stock Disponible} = \text{stock\_fisico} - \text{stock\_comprometido}$$
  - **Costo Promedio Ponderado (CPP):** Calculado de forma inmutable tras cada recepción de mercadería contra orden de compra (`RF-INV-006`).

---

### 2.2. Módulo 2: Ventas y Mostrador POS (`SIGIC.Modules.SalesPOS`)
- **Propósito:** Facturación de alta velocidad en mostrador presencial, orientada a constructores y maestros de obra con operación exclusiva por teclado y escáner láser (`RF-POS-003`).
- **Entidades y Agregados:**
  - `VentaPOS` (Root Entity): Correlativo interno, fecha UTC, cliente (DNI/RUC o cliente general), almacén emisor, tipo de comprobante (ticket interno, boleta o factura electrónica).
  - `VentaItem`: Artículo, unidad de medida, cantidad, precio unitario aplicado, subtotal e IGV al 18 %.
  - `PagoPOS`: Medio de pago (efectivo, tarjeta, transferencia, Yape/Plin), monto y código de operación bancaria manual único por día.
  - `EntregaDespachoPOS`: Registro de entrega física en mostrador para pedidos web `Listo para Retiro` (`RF-POS-011`).
- **Reglas de Negocio Clave:**
  - **Operación sin Ratón:** Foco en grilla, navegación con flechas y teclas rápidas fijas (`F2` Búsqueda, `F4` Cantidad, `F8` Cliente, `F9` Pago, `F12` Confirmar).
  - **Frontera de Despacho Web (`RF-POS-011`):** La pantalla de entrega solo valida pedidos en estado `Listo para Retiro` cotejando código y DNI. No tiene acceso a comprobantes ni a saldos bancarios.

---

### 2.3. Módulo 3: E-commerce y Pedidos Web (`SIGIC.Modules.OrdersWeb`)
- **Propósito:** Gestión de compras y reservas en línea (B2B/B2C), checkout atómico y trazabilidad de pedidos para retiro en tienda o despacho a obra.
- **Entidades y Agregados:**
  - `PedidoWeb` (Root Entity): Código alfanumérico único, cliente web ID, modalidad de despacho (`RetiroTienda`, `RetiroCentral`, `EnvioObra`), estado del pedido, total e importe IGV.
  - `PedidoDetalle`: Artículos solicitados, precio unitario congelado y cantidad.
  - `ReservaStockPedido`: Registro de reserva de stock con `FechaCreacionUtc`, `FechaExpiracionUtc`, `EstadoReserva` (`Activa`, `Consolidada`, `Expirada`, `Cancelada`).
  - `ComprobantePagoAdjunto`: Registro del archivo de constancia subido por el cliente (máximo 5 MB, JPG/PNG/PDF).
- **Reglas de Negocio Clave:**
  - **TTL Dinámico de Reserva (`RF-WEB-002`):**
    - Pasarela con tarjeta: $TTL = 20\text{ minutos}$.
    - Pagos manuales: $TTL = 240\text{ minutos}$ (4 horas), con plazo de $60\text{ minutos}$ (1 hora) para subir la constancia de pago.
  - **Revalidación Atómica en Checkout (`RF-WEB-005`):** Adquisición pesimista de fila (`SELECT ... FOR UPDATE` ordenado por `producto_id ASC`) y revalidación de precio y existencias en el núcleo antes de generar la orden.
  - **Guía Automática:** Si la modalidad es "Retiro en Tienda", la confirmación del pago dispara automáticamente la creación de una guía interna de traslado desde el Almacén Central hacia la tienda física (`RF-WEB-002`).

---

### 2.4. Módulo 4: Caja y Tesorería (`SIGIC.Modules.CashManagement`)
- **Propósito:** Control de flujo monetario en tiendas físicas y validación administrativa asíncrona de pagos en línea.
- **Entidades y Agregados:**
  - `TurnoCaja`: Cajero ID, fecha/hora apertura, saldo inicial (fondo de sencillo), fecha/hora cierre, estado (`Abierto`, `Cerrado`).
  - `ArqueoCiegoDeclaracion`: Conteo físico declarado por denominación de billetes y monedas sin visibilidad del saldo teórico (`RF-CAJ-002`).
  - `LiquidacionTurno`: Cálculo de diferencias automáticas entre lo declarado y lo registrado en el sistema.
  - `ColaValidacionTesoreria`: Bandeja de auditoría de Backoffice donde Tesorería coteja las transferencias bancarias de pedidos web contra los extractos de cuenta (`RF-WEB-003`).
- **Reglas de Negocio Clave:**
  - Prohibición de registro de ventas sin turno activo abierto.
  - Desacoplamiento total: la verificación de comprobantes web recae exclusivamente en Tesorería, protegiendo al mostrador físico.

---

### 2.5. Módulo 5: Tarifas y Seguridad (`SIGIC.Modules.PricingSecurity`)
- **Propósito:** Administración de listas de precios mayoristas, autenticación criptográfica y control de acceso basado en roles (RBAC).
- **Entidades y Agregados:**
  - `ListaPrecio`: Definición de listas (1: Público General, 2: Maestro de Obra, 3: Contratista/Mayorista).
  - `PrecioArticulo`: Precio fijado por artículo, lista y unidad de medida, con vigencia temporal.
  - `UsuarioColaborador`: Credenciales internas en Argon2id, asignación de roles RBAC (`Cajero`, `Almacenero`, `Despachador`, `Administrador`).
  - `ClienteWeb`: Identidad de clientes externos, perfil de compras y consentimiento de datos (Ley N.° 29733).
- **Reglas de Negocio Clave:**
  - Emisión de tokens **JWT asimétricos firmados con RS256** (vida útil 15 minutos) con claims segregados (`SIGIC_InternalStaff` vs `SIGIC_WebClient`).
  - Los clientes no autenticados en el portal web visualizan únicamente la Lista 1 (Público General).
