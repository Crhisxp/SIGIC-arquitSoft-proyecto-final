---
id: "02-historias-del-usuario"
doc: "02-historias-del-usuario"
version: "2.2.0"
estado: "VALIDADO_PARA_EXPOSICION"
tipo: "historias-usuario-bdd"
proyecto: "SIGIC-Ferreteria-v2.2"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 UNSCH"
fecha: "2026-10-30"
---

# Historias de Usuario Críticas (SIGIC Ferretería v2.2)

> **Documento:** `01-analisis-de-sistema/02-historias-del-usuario.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Metodología:** User Story Mapping y Criterios Cuantitativos BDD (Given-When-Then)  
> **Alineación de Calidad:** AC01 (Rendimiento P95 < 250ms), AC02 (Consistencia ACID Cero Sobreventas), AC03 (Trazabilidad Append-Only)  
> **Estado:** VALIDADO_PARA_EXPOSICION  

---

## 1. Introducción y Matriz de Trazabilidad

Las historias de usuario aquí documentadas formalizan los flujos transaccionales y de interacción críticos del sistema **SIGIC Ferretería v2.2**, resolviendo la concurrencia operativa entre el mostrador físico (POS) y el canal digital e-commerce sobre una persistencia relacional unificada en PostgreSQL 16 con PgBouncer.

| Historia de Usuario | Actor Primario | Código RF/RNF Relacionado | Atributo de Calidad Rector |
| :--- | :--- | :--- | :--- |
| **HU-01: Venta Rápida POS sin Ratón** | Cajero Mostrador | `RF-POS-003`, `RF-POS-001`, `RF-POS-004` | **AC01** (Latencia P95 < 250ms API / $\le 2$s cobro) |
| **HU-02: Consulta Catálogo con Filtro Web** | Cliente Web (B2C/B2B) | `RF-INV-001`, `RF-WEB-001`, `RNF-PERF-004` | **AC01** (Respuesta catálogo $\le 400$ms) |
| **HU-03: Checkout Web con Revalidación Atómica** | Cliente Web (Contratista/B2C) | `RF-WEB-005`, `RF-WEB-002`, `RNF-CON-001` | **AC02** (Consistencia ACID, cero sobreventas) |
| **HU-04: Reserva Dinámica de Stock por Medio Pago** | Cliente Web | `RF-WEB-002`, `RNF-CON-004` | **AC02** (Gestión de Stock Comprometido TTL) |
| **HU-05: Validación Pagos en Backoffice Desacoplado** | Superadmin / Tesorería | `RF-WEB-003`, `RNF-SEG-004` | **AC03** (Trazabilidad inmutable de validación) |
| **HU-06: Generación Automática de Guía de Traslado** | Almacenero | `RF-WEB-002`, `RF-INV-003`, `RF-POS-011` | **AC02** (Conservación de existencias en tránsito) |
| **HU-07: Entrega Ágil Mostrador Pedidos Web (F6)** | Despachador / Cajero | `RF-POS-011`, `RNF-PERF-001` | **AC01** (Despacho ligero en $\le 1$s) |
| **HU-08: Arqueo Ciego de Caja Append-Only** | Cajero Mostrador | `RF-CAJ-002`, `RNF-SEG-004` | **AC03** (Trazabilidad contable inmutable) |

---

## 2. Historias de Usuario Detalladas (Formato BDD)

### HU-01: Venta Rápida en Mostrador por Teclado y Escáner sin Ratón
- **Código Relacionado:** `RF-POS-003`, `RF-POS-001`, `RF-POS-004`, `RNF-PERF-001`, `AC01`.
- **Actor:** Cajero Mostrador.
- **Narrativa:**
  > **Como** Cajero de tienda en mostrador,  
  > **quiero** registrar los productos de una compra mediante lectura de código de barras o atajos de teclado y procesar el cobro sin tocar el ratón,  
  > **para** atender a los clientes y maestros de obra con la máxima velocidad y reducir los tiempos de espera en las horas pico.
- **Reglas de Negocio Asociadas:**
  - El sistema mantiene el foco activo en el campo de captura del artículo en todo momento (`F1`).
  - La lectura de un código de barras válido (EAN-13 o SKU) agrega el artículo al detalle con cantidad unitaria por defecto. Si el código ya existe en la grilla, incrementa la cantidad en 1.
  - Teclas de función mapeadas de manera fija: `F1` (captura SKU/EAN-13), `F2` (búsqueda paramétrica), `F3` (cliente DNI/RUC), `F4` (editar cantidad), `F5` (ver stock multialmacén), `F6` (entrega web), `F7` (sustitutos), `F8` (eliminar ítem), `F9` (totalizar y panel mixto), `F10` (boleta), `F12` (factura).
  - Tiempo de respuesta de búsqueda y adición $\le 1$ segundo; confirmación de cobro $\le 2$ segundos (P95).
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Captura consecutiva por escáner de código de barras.**
    - **Dado** que el cajero tiene un turno de caja abierto y la pantalla de venta en mostrador activa,
    - **Cuando** pasa 5 productos distintos por el lector óptico de código de barras sin tocar el teclado ni el ratón,
    - **Entonces** los 5 productos se visualizan en la tabla de venta con sus descripciones, precios unitarios y subtotales en menos de 1 segundo por ítem, manteniendo el cursor listo para el siguiente producto.
  - **Escenario 2: Cobro completo exclusivamente mediante teclas de función.**
    - **Dado** que se han registrado 3 artículos en la orden de venta actual,
    - **Cuando** el cajero presiona `F9`, ingresa el monto recibido en efectivo, presiona `Enter` y luego presiona `F10` (Boleta),
    - **Entonces** el sistema procesa la transacción en PostgreSQL en menos de 250 ms (P95 en API local), descuenta el inventario físico de tienda, registra el asiento inmutable de caja y emite la orden de impresión térmica ESC/POS.

---

### HU-02: Consulta de Catálogo Web con Exclusión Estricta de Artículos sin Flag Web
- **Código Relacionado:** `RF-INV-001`, `RF-WEB-001`, `RNF-PERF-004`, `AC01`.
- **Actor:** Cliente Web (Público General / Maestro de Obra / Contratista B2B).
- **Narrativa:**
  > **Como** cliente del portal web e-commerce,  
  > **quiero** consultar y buscar materiales de construcción en el catálogo digital filtrados por categoría y marca,  
  > **para** encontrar únicamente artículos que cuentan con empaque estandarizado y despacho garantizado para compra en línea, sin toparme con productos de venta exclusiva en mostrador.
- **Reglas de Negocio Asociadas:**
  - El atributo booleano `disponible_en_web` determina la visibilidad del producto.
  - Si un artículo tiene `disponible_en_web = false`, es excluido estrictamente de todos los índices de búsqueda, filtros de categoría, páginas de detalle y llamadas de la API pública.
  - Cualquier petición directa vía HTTP GET a `/api/v1/public/products/{id}` o `/api/v1/public/products/slug/{slug}` para un artículo con `disponible_en_web = false` responde con código `HTTP 404 Not Found` absoluto.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Búsqueda pública en catálogo excluye artículos restringidos.**
    - **Dado** que en la base de datos existen 500 artículos registrados, de los cuales 120 tienen `disponible_en_web = false`,
    - **Cuando** un usuario no autenticado navega por el catálogo web o realiza una búsqueda general de materiales,
    - **Entonces** la interfaz y la API pública listan únicamente entre los 380 artículos autorizados, con 0 % de presencia de los 120 artículos restringidos y latencia $\le 400$ ms (P95).
  - **Escenario 2: Intento de acceso directo por URL a artículo restringido.**
    - **Dado** que el producto "Tornillo Drywall a Granel x Kilo" tiene ID `409` y `disponible_en_web = false`,
    - **Cuando** un usuario intenta acceder directamente a `https://ferreteria.pe/catalogo/producto/409` o consume `/api/v1/public/products/409`,
    - **Entonces** el servidor retorna `HTTP 404 Not Found` y la interfaz web muestra la vista estándar de "Producto no encontrado".

---

### HU-03: Checkout Web con Revalidación Atómica de Precio y Stock Disponible
- **Código Relacionado:** `RF-WEB-005`, `RF-WEB-002`, `RNF-CON-001`, `RNF-CON-003`, `AC02`.
- **Actor:** Cliente Web (Contratista / Público General).
- **Narrativa:**
  > **Como** cliente que finaliza una compra en el carrito del portal web,  
  > **quiero** que el sistema verifique atómicamente el stock real disponible y los precios actualizados antes de autorizar mi pago,  
  > **para** garantizar que no pagaré por mercadería inexistente ni se me cobrarán tarifas desactualizadas debido al desfase de la caché web.
- **Reglas de Negocio Asociadas:**
  - El catálogo web puede operar sobre una capa de lectura en caché con hasta 30 segundos de desfase.
  - Al presionar "Pagar / Confirmar Pedido", la API pública invoca obligatoriamente una transacción ACID en PostgreSQL con bloqueo pesimista de fila (`SELECT ... FOR UPDATE`) sobre las tablas maestras, ordenando deterministamente por `producto_id ASC` para erradicar deadlocks.
  - Se evalúa: $\text{Stock Disponible} = \text{stock\_fisico} - \text{stock\_comprometido}$. Si la cantidad solicitada excede el disponible, la transacción se aborta con `HTTP 409 Conflict`.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Confirmación de pedido con stock suficiente en Almacén Central.**
    - **Dado** que el Almacén Central cuenta con 50 bolsas de Cemento Portland disponibles y el precio es S/ 28.50,
    - **Cuando** el cliente envía la solicitud de checkout para 20 bolsas al precio de S/ 28.50,
    - **Entonces** el sistema bloquea la fila transaccional, valida existencias, incrementa el `Stock Comprometido` en 20 bolsas, genera la orden en estado `Pendiente de Pago` y responde con éxito en menos de 1 segundo.
  - **Escenario 2: Intento de checkout con stock insuficiente por venta concurrente en mostrador.**
    - **Dado** que en el catálogo web se visualizaban 5 unidades de un taladro percutor por efecto de la caché, pero el mostrador acaba de vender las últimas 5 unidades hace 10 segundos,
    - **Cuando** el cliente web pulsa "Confirmar Pedido",
    - **Entonces** la revalidación atómica detecta que `Stock Disponible = 0`, rechaza la orden sin generar cargo económico, retorna código `HTTP 409 Conflict` e instruye a la interfaz a actualizar el carrito con el aviso "El artículo se ha agotado en almacén".

---

### HU-04: Reserva Dinámica de Stock Temporal según Medio de Pago
- **Código Relacionado:** `RF-WEB-002`, `RNF-CON-004`, `AC02`.
- **Actor:** Cliente Web (Público General / Contratista).
- **Narrativa:**
  > **Como** cliente del portal web,  
  > **quiero** contar con un tiempo prudencial garantizado para completar el pago de mi pedido sin perder los materiales reservados,  
  > **para** poder realizar la transferencia bancaria o ingresar los datos de mi tarjeta con total tranquilidad.
- **Reglas de Negocio Asociadas:**
  - Al completar la revalidación atómica (HU-03), el sistema asigna un tiempo de vida (TTL) a la reserva según el medio de pago:
    - **Pasarela de Tarjetas (Crédito/Débito):** Reserva fija de **20 minutos**.
    - **Pago Manual (Transferencia Bancaria BCP/BBVA o Billeteras Yape/Plin):** Reserva de **hasta 4 horas**, sujeta a que el cliente adjunte el comprobante dentro de los primeros **60 minutos**.
  - Si transcurren los plazos sin confirmación, el worker en segundo plano (`ReservaExpirationWorker`) cancela el pedido de forma idempotente y decrementa `stock_comprometido`, liberando el inventario.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Expiración automática de reserva por tarjeta de crédito tras 20 minutos.**
    - **Dado** que un cliente inició checkout con tarjeta reservando 10 planchas de fibrocemento a las 10:00:00 h,
    - **Cuando** son las 10:20:01 h y la pasarela no ha emitido webhook de confirmación,
    - **Entonces** el proceso en segundo plano cancela la orden por expiración, libera las 10 planchas al Stock Disponible y marca el pedido como `Cancelado por Expiración`.
  - **Escenario 2: Plazo de carga de comprobante para transferencias bancarias.**
    - **Dado** que un contratista genera un pedido a las 08:00 h seleccionando "Transferencia BCP", obteniendo un TTL máximo de reserva de 4 horas,
    - **Cuando** transcurren 61 minutos (09:01 h) y el contratista no ha subido el archivo del voucher al portal,
    - **Entonces** el sistema anula la reserva por incumplimiento del plazo de carga de 1 hora, liberando el material comprometido en Almacén Central.

---

### HU-05: Validación de Pagos Manuales en Consola de Backoffice Desacoplada
- **Código Relacionado:** `RF-WEB-003`, `RNF-SEG-004`, `AC03`.
- **Actor:** Superadmin / Asistente de Tesorería.
- **Narrativa:**
  > **Como** responsable de Tesorería en Backoffice,  
  > **quiero** contar con una cola de auditoría donde pueda visualizar las constancias de transferencia y capturas de Yape subidas por los clientes web y contrastarlas con las cuentas bancarias corporativas,  
  > **para** aprobar legítimamente los pedidos sin interrumpir ni recargar las labores del personal de caja en el mostrador.
- **Reglas de Negocio Asociadas:**
  - Archivos adjuntos soportados: JPG, PNG o PDF con un tamaño máximo de 5 MB.
  - La consola muestra la cola priorizada por orden de llegada y tiempo de reserva restante.
  - Al aprobar, el sistema cambia el pedido a `Pago Confirmado`, convierte el stock comprometido en salida definitiva de Kardex del Almacén Central y encola la orden para preparación.
  - Desacoplamiento total: los cajeros de mostrador no tienen acceso ni responsabilidades sobre esta cola.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Aprobación exitosa de pago con comprobante bancario.**
    - **Dado** que un pedido web de S/ 1,450.00 tiene una constancia de transferencia BCP en estado `Pendiente de Validación` en la cola de Backoffice,
    - **Cuando** el Superadmin / Tesorero verifica los fondos en el extracto bancario y hace clic en "Aprobar Pago",
    - **Entonces** el pedido cambia a estado `Pago Confirmado`, el stock físico se debita formalmente en el Kardex, el pedido pasa a la cola de preparación del Almacenero y se registra la pista de auditoría inmutable en `Audit_Log`.
  - **Escenario 2: Rechazo de comprobante duplicado o no verificado.**
    - **Dado** que un comprobante adjunto corresponde a una captura reutilizada o no figura en la cuenta bancaria,
    - **Cuando** el personal de Backoffice selecciona "Rechazar" e ingresa la justificación,
    - **Entonces** el pedido pasa a estado `Rechazado`, la reserva de stock comprometido se decrementa liberando los productos al catálogo y se notifica al cliente por correo electrónico.

---

### HU-06: Generación Automática de Guía Interna de Traslado para Retiro en Tienda
- **Código Relacionado:** `RF-WEB-002`, `RF-INV-003`, `RF-POS-011`, `AC02`.
- **Actor:** Almacenero / Sistema Automatizado.
- **Narrativa:**
  > **Como** Almacenero encargado del depósito central,  
  > **quiero** que el sistema genere automáticamente una guía interna de traslado en estado borrador cuando un pedido web pagado tenga modalidad "Retiro en Tienda",  
  > **para** preparar el embalaje de los materiales y remitirlos ordenadamente hacia el mostrador de atención.
- **Reglas de Negocio Asociadas:**
  - Todos los pedidos web se reservan y preparan inicialmente en el Almacén Central.
  - Al confirmarse el pago de un pedido con modalidad "Retiro en Tienda Mostrador", el sistema genera automáticamente una **Guía Interna de Traslado** con origen `Almacén Central` y destino `Tienda Mostrador`, en estado `Borrador`.
  - El Almacenero recolecta los materiales físicamente y cambia la guía a `En Tránsito`, debitando el stock del Almacén Central.
  - Al recibirse el lote en tienda física, el encargado confirma el bulto pasando la guía a `Recibido`; en ese instante el pedido web cambia automáticamente a `Listo para Retiro`.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Creación automática de guía al confirmarse pago web.**
    - **Dado** que un cliente completó la compra de 4 galones de esmalte sintético con modalidad "Retiro en Mostrador 1",
    - **Cuando** Tesorería aprueba el comprobante de pago en Backoffice,
    - **Entonces** el sistema crea una Guía Interna de Traslado vinculada al pedido con estado `Borrador`, detallando los 4 galones, el almacén emisor (Central) y el almacén receptor (Mostrador 1).
  - **Escenario 2: Notificación al cliente tras recepción en mostrador.**
    - **Dado** que la guía interna asociada al pedido `WEB-8821` se encuentra en estado `En Tránsito`,
    - **Cuando** el encargado de tienda en Mostrador 1 revisa físicamente los artículos y marca la guía como `Recibido`,
    - **Entonces** el pedido `WEB-8821` pasa automáticamente al estado `Listo para Retiro` y se emite un correo electrónico al cliente con el código de recojo.

---

### HU-07: Entrega Ágil en Mostrador de Pedidos Web mediante Escaneo y DNI (F6)
- **Código Relacionado:** `RF-POS-011`, `RNF-PERF-001`, `AC01`.
- **Actor:** Despachador Mostrador (o Cajero en función de entrega).
- **Narrativa:**
  > **Como** Despachador en el mostrador físico de la ferretería,  
  > **quiero** entregar los pedidos web a los clientes en menos de 1 minuto validando únicamente el código del pedido y su documento de identidad,  
  > **para** evitar aglomeraciones en la tienda sin tener que verificar estados de cuenta bancaria ni revisar comprobantes de pago.
- **Reglas de Negocio Asociadas:**
  - La pantalla ligera de entrega (`F6`) solo procesa pedidos en estado `Listo para Retiro`.
  - El operador escanea el código QR/código alfanumérico, coteja el DNI físico y confirma la entrega.
  - Prohibición de acceso: el operador no visualiza vouchers bancarios ni tiene acceso a la consola de tesorería.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Entrega satisfactoria de pedido listo para retiro.**
    - **Dado** que el pedido `PED-4020` está en estado `Listo para Retiro` en el mostrador de Huamanga,
    - **Cuando** el Despachador escanea el código con la pistola óptica, verifica que el DNI coincide con el titular registrado y pulsa `Confirmar Entrega`,
    - **Entonces** el sistema actualiza el estado a `Entregado` en menos de 1 segundo, imprime la constancia de entrega física y archiva la orden.
  - **Escenario 2: Intento de retiro de pedido no validado por tesorería.**
    - **Dado** que un cliente se presenta en mostrador exigiendo mercadería de un pedido en estado `Pendiente de Validación de Pago`,
    - **Cuando** el operador escanea el código,
    - **Entonces** la terminal muestra una alerta "PEDIDO NO DISPONIBLE: Pago en proceso de verificación por Tesorería", inhabilitando cualquier botón de despacho.

---

### HU-08: Arqueo Ciego de Caja Append-Only al Cierre de Turno por Cajero
- **Código Relacionado:** `RF-CAJ-002`, `RNF-SEG-004`, `AC03`.
- **Actor:** Cajero Mostrador.
- **Narrativa:**
  > **Como** Cajero al finalizar mi jornada laboral,  
  > **quiero** declarar el monto físico total de dinero en efectivo y vouchers que tengo en mi gaveta sin que la pantalla me muestre cuánto dinero calcula el sistema,  
  > **para** garantizar la transparencia del cuadre de caja y evitar manipulaciones en el proceso de rendición de cuentas.
- **Reglas de Negocio Asociadas:**
  - Al ejecutar el "Cierre de Turno", el sistema presenta el formulario de **Arqueo Ciego**, ocultando por completo las ventas acumuladas del día y el saldo esperado.
  - El cajero desglosa obligatoriamente la cantidad física por denominación (billetes de S/ 200, 100, 50, 20, 10; monedas de S/ 5, 2, 1, 0.50, 0.20, 0.10) y la suma de vouchers físicos de tarjetas.
  - El modelo de datos es estrictamente **append-only**: queda prohibido cualquier `UPDATE` o `DELETE` sobre las tablas de arqueo.
  - El servidor congela el turno, calcula $\text{Diferencia} = \text{Monto Declarado} - \text{Monto Teórico}$ y emite una alerta clasificada si existe faltante o sobrante.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Declaración de arqueo ciego sin información previa.**
    - **Dado** que el cajero decide cerrar su turno a las 20:00 h,
    - **Cuando** abre la opción de cierre de caja,
    - **Entonces** la pantalla no muestra ningún monto de ventas acumuladas, solicitando exclusivamente el ingreso de la cantidad de unidades por denominación monetaria.
  - **Escenario 2: Consolidación y detección de diferencias tras el cierre.**
    - **Dado** que el sistema registró ventas en efectivo por S/ 3,250.00 con un fondo inicial de S/ 200.00 (Total esperado: S/ 3,450.00),
    - **Cuando** el cajero ingresa un conteo físico que totaliza S/ 3,430.00 y confirma el cierre,
    - **Entonces** el sistema cierra el turno de forma irrevocable en PostgreSQL, registra una diferencia de `- S/ 20.00` (Faltante) en un registro inmutable append-only, emite el ticket de cierre y remite el reporte al Superadmin.
