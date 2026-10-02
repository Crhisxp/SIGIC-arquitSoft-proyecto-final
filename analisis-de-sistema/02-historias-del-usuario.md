# Historias de Usuario Críticas (SIGIC Ferretería)

> **Documento:** `analisis-de-sistema/02-historias-del-usuario.md`  
> **Versión de Especificación:** 2.2 (Oficial)  
> **Metodología:** User Story Mapping y Criterios de Aceptación Cuantitativos (BDD: Given-When-Then / Dado-Cuando-Entonces)  

---

## Introducción

Las historias de usuario aquí documentadas formalizan los flujos transaccionales y de interacción críticos del sistema **SIGIC Ferretería (Versión 2.2)**, resolviendo las inconsistencias operativas detectadas en la interacción entre el mostrador físico (POS) y el canal digital e-commerce. Cada historia vincula directamente los requerimientos funcionales (`RF-xxx`), reglas de negocio y criterios de aceptación verificables mediante pruebas automatizadas.

---

### HU-01: Venta Rápida en Mostrador por Teclado y Escáner sin Ratón
- **Código Relacionado:** `RF-POS-003`, `RF-POS-001`, `RF-POS-004`, `RNF-PERF-001`.
- **Actor:** Cajero.
- **Narrativa:**
  > **Como** Cajero de tienda en mostrador,  
  > **quiero** registrar los productos de una compra mediante lectura de código de barras o atajos de teclado y procesar el cobro sin tocar el ratón,  
  > **para** atender a los clientes y maestros de obra con la máxima velocidad y reducir los tiempos de espera en las horas pico.
- **Reglas de Negocio Asociadas:**
  - El sistema debe mantener el foco activo en el campo de captura del artículo en todo momento.
  - La lectura de un código de barras válido (EAN-13 o SKU) agrega el artículo al detalle con cantidad unitaria por defecto. Si el código ya existe en la grilla, se incrementa la cantidad en 1.
  - Teclas de función mapeadas de manera fija: `F2` (buscar por texto/nombre), `F4` (modificar cantidad), `F7` (aplicar descuento permitido según umbral del cajero), `F8` (seleccionar cliente por DNI/RUC), `F9` (totalizar y pasar a panel de pago mixto), `F12` (confirmar e imprimir comprobante).
  - El tiempo de respuesta de búsqueda y adición no debe superar 1 segundo.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Captura consecutiva por escáner de código de barras.**
    - **Dado** que el cajero tiene un turno de caja abierto y la pantalla de venta en mostrador activa,
    - **Cuando** pasa 5 productos distintos por el lector óptico de código de barras sin tocar el teclado ni el ratón,
    - **Entonces** los 5 productos se visualizan en la tabla de venta con sus descripciones, precios unitarios y subtotales en menos de 1 segundo por ítem, manteniendo el cursor listo para el siguiente producto.
  - **Escenario 2: Cobro completo exclusivamente mediante teclas de función.**
    - **Dado** que se han registrado 3 artículos en la orden de venta actual,
    - **Cuando** el cajero presiona `F9`, ingresa el monto recibido en efectivo, presiona `Enter` y luego presiona `F12`,
    - **Entonces** el sistema valida la transacción en base de datos en menos de 2 segundos (p95), descuenta el inventario del almacén de mostrador, registra el asiento de caja y envía la orden de impresión del ticket al puerto térmico.

---

### HU-02: Consulta de Catálogo Web con Exclusión Estricta de Artículos sin Flag Web
- **Código Relacionado:** `RF-INV-001`, `RF-WEB-001`, `RNF-PERF-004`.
- **Actor:** Público General / Maestro de Obra / Contratista (Cliente Web).
- **Narrativa:**
  > **Como** cliente del portal web e-commerce,  
  > **quiero** consultar y buscar materiales de construcción en el catálogo digital filtrados por categoría y marca,  
  > **para** encontrar únicamente artículos que cuentan con empaque estandarizado y despacho garantizado para compra en línea, sin toparme con productos de venta exclusiva en mostrador.
- **Reglas de Negocio Asociadas:**
  - El atributo booleano `disponible_en_web` determina la visibilidad del producto.
  - Si un artículo tiene `disponible_en_web = false`, debe ser excluido estrictamente de todos los índices de búsqueda, filtros de categoría, páginas de detalle y llamadas de la API pública.
  - Cualquier petición directa vía HTTP GET a `/api/v1/public/products/{id}` o `/api/v1/public/products/slug/{slug}` para un artículo con `disponible_en_web = false` debe responder con código `HTTP 404 Not Found`.
  - Artículos como pernería al peso, varillas fraccionadas o cemento en bolsas rotas se mantienen en `false` por defecto.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Búsqueda pública en catálogo excluye artículos restringidos.**
    - **Dado** que en la base de datos existen 500 artículos registrados, de los cuales 120 tienen `disponible_en_web = false`,
    - **Cuando** un usuario no autenticado navega por el catálogo web o realiza una búsqueda general de materiales,
    - **Entonces** la interfaz y la API pública listan únicamente entre los 380 artículos autorizados, con 0 % de presencia de los 120 artículos restringidos.
  - **Escenario 2: Intento de acceso directo por URL a artículo restringido.**
    - **Dado** que el producto "Tornillo Drywall a Granel x Kilo" tiene ID `409` y `disponible_en_web = false`,
    - **Cuando** un usuario intenta acceder directamente a `https://ferreteria.pe/catalogo/producto/409` o consume `/api/v1/public/products/409`,
    - **Entonces** el servidor retorna un error `HTTP 404` y la interfaz web muestra la vista estándar de "Producto no encontrado o no disponible para venta en línea".

---

### HU-03: Checkout Web con Revalidación Atómica de Precio y Stock Disponible
- **Código Relacionado:** `RF-WEB-005`, `RF-WEB-002`, `RNF-CON-001`, `RNF-CON-003`.
- **Actor:** Contratista / Público General (Cliente Web).
- **Narrativa:**
  > **Como** cliente que finaliza una compra en el carrito del portal web,  
  > **quiero** que el sistema verifique atómicamente el stock real disponible y los precios actualizados antes de autorizar mi pago,  
  > **para** garantizar que no pagaré por mercadería inexistente ni se me cobrarán tarifas desactualizadas debido al desfase de la caché web.
- **Reglas de Negocio Asociadas:**
  - El catálogo web puede operar sobre una capa de lectura en caché con hasta 30 segundos de desfase para optimizar navegación.
  - Al presionar "Pagar / Confirmar Pedido", la API pública invoca obligatoriamente una transacción ACID en PostgreSQL con bloqueo pesimista de fila (`SELECT ... FOR UPDATE`) sobre las tablas maestras de stock y listas de precios vigentes.
  - Se evalúa: `Stock Disponible = Stock Físico - Stock Comprometido`. Si la cantidad solicitada excede el Stock Disponible, la transacción se aborta con mensaje específico y se actualiza el carrito del usuario.
  - Si el precio oficial cambió respecto al mostrado en el catálogo, se notifica el cambio al cliente y se le solicita reconfirmar el pedido.
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
- **Código Relacionado:** `RF-WEB-002`, `RNF-CON-004`.
- **Actor:** Público General / Contratista (Cliente Web).
- **Narrativa:**
  > **Como** cliente del portal web,  
  > **quiero** contar con un tiempo prudencial garantizado para completar el pago de mi pedido sin perder los materiales reservados,  
  > **para** poder realizar la transferencia bancaria o ingresar los datos de mi tarjeta con total tranquilidad.
- **Reglas de Negocio Asociadas:**
  - Al completar la revalidación atómica (HU-03), el sistema asigna un tiempo de vida (TTL) a la reserva en función del medio de pago seleccionado:
    - **Pasarela de Tarjetas (Crédito/Débito):** Reserva fija de **20 minutos**.
    - **Pago Manual (Transferencia Bancaria BCP/BBVA o Billeteras Yape/Plin):** Reserva de **hasta 4 horas**, sujeta a una regla estricta: el cliente tiene un plazo máximo de **1 hora** para adjuntar la constancia o comprobante de pago en el portal.
  - Si transcurre 1 hora sin carga de comprobante en pagos manuales, o si vencen los 20 minutos de pasarela sin confirmación, el servicio en segundo plano (`Worker de Reservas`) cancela el pedido de forma idempotente y decrementa el `Stock Comprometido`, liberando el inventario de inmediato.
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
- **Código Relacionado:** `RF-WEB-003`, `RNF-SEG-004`.
- **Actor:** Administrador / Asistente de Tesorería.
- **Narrativa:**
  > **Como** responsable de Tesorería en Backoffice,  
  > **quiero** contar con una cola de auditoría donde pueda visualizar las constancias de transferencia y capturas de Yape subidas por los clientes web y contrastarlas con las cuentas bancarias corporativas,  
  > **para** aprobar legítimamente los pedidos sin interrumpir ni recargar las labores del personal de caja en el mostrador.
- **Reglas de Negocio Asociadas:**
  - El cliente web puede adjuntar comprobantes en formato JPG, PNG o PDF con un tamaño máximo de 5 MB.
  - La consola de Backoffice muestra la cola priorizada por orden de llegada y tiempo de reserva restante.
  - El personal de Tesorería verifica el número de operación bancaria y monto contra el portal bancario. Al aprobar, ingresa opcionalmente observaciones y confirma la acreditación.
  - El sistema cambia el pedido a estado `Pago Confirmado`, consolida la reserva descontando el `Stock Físico` del Almacén Central y encola la orden para preparación en almacén.
  - Si el voucher es ilegible o falso, Tesorería presiona "Rechazar Comprobante", ingresando el motivo; el sistema libera el stock y notifica al cliente por correo electrónico.
  - **Desacoplamiento total:** Los cajeros de mostrador no tienen acceso ni responsabilidades sobre esta cola.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Aprobación exitosa de pago con comprobante bancario.**
    - **Dado** que un pedido web de S/ 1,450.00 tiene una constancia de transferencia BCP en estado `Pendiente de Validación` en la cola de Backoffice,
    - **Cuando** el Asistente de Tesorería verifica los fondos en el extracto bancario y hace clic en "Aprobar Pago",
    - **Entonces** el pedido cambia a estado `Pago Confirmado`, el stock físico se debita formalmente en el Kardex, el pedido pasa a la cola de preparación del Almacenero y se registra la pista de auditoría con el usuario de tesorería y marca de tiempo.
  - **Escenario 2: Rechazo de comprobante duplicado o no verificado.**
    - **Dado** que un comprobante adjunto corresponde a una captura reutilizada o no figura en la cuenta bancaria,
    - **Cuando** el personal de Backoffice selecciona "Rechazar" e ingresa la justificación "Operación no figura en extracto de cuenta",
    - **Entonces** el pedido pasa a estado `Rechazado`, la reserva de stock comprometido se decrementa liberando los productos al catálogo y se envía un correo automático al cliente con la notificación.

---

### HU-06: Generación Automática de Guía Interna de Traslado para Retiro en Tienda
- **Código Relacionado:** `RF-WEB-002`, `RF-INV-003`, `RF-POS-011`.
- **Actor:** Almacenero / Sistema (Proceso Automatizado).
- **Narrativa:**
  > **Como** Almacenero encargado del depósito central,  
  > **quiero** que el sistema genere automáticamente una guía interna de traslado en estado borrador cuando un pedido web pagado tenga modalidad "Retiro en Tienda",  
  > **para** preparar el embalaje de los materiales y remitirlos ordenadamente hacia el mostrador de atención.
- **Reglas de Negocio Asociadas:**
  - Todos los pedidos web se reservan y preparan inicialmente en el Almacén Central.
  - Si el cliente seleccionó modalidad "Retiro en Tienda Mostrador", inmediatamente después de la confirmación del pago (webhook de pasarela o aprobación en Backoffice), el sistema genera de forma automática una **Guía Interna de Traslado** con origen `Almacén Central` y destino `Tienda Mostrador`, en estado `Borrador`.
  - El Almacenero visualiza la guía en su panel, efectúa la recolección física de los materiales, y cambia la guía a estado `En Tránsito`, descontando el stock del Almacén Central.
  - Al llegar el lote a la tienda física, el personal de recepción confirma el bulto y la guía pasa a `Recibido`; en ese instante el pedido web cambia automáticamente a estado `Listo para Retiro` y se notifica al cliente que puede acercarse a recoger.
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

### HU-07: Entrega Ágil en Mostrador de Pedidos Web mediante Escaneo y DNI
- **Código Relacionado:** `RF-POS-011`, `RNF-PERF-001`.
- **Actor:** Despachador (o Cajero en función de entrega).
- **Narrativa:**
  > **Como** Despachador en el mostrador físico de la ferretería,  
  > **quiero** entregar los pedidos web a los clientes en menos de 1 minuto validando únicamente el código del pedido y su documento de identidad,  
  > **para** evitar aglomeraciones en la tienda sin tener que verificar estados de cuenta bancaria ni revisar comprobantes de pago.
- **Reglas de Negocio Asociadas:**
  - El Despachador solo puede buscar y entregar pedidos que se encuentren en estado `Listo para Retiro`.
  - Si un cliente se acerca con un pedido en estado `Pendiente de Pago` o `En Preparación`, el sistema muestra una advertencia en rojo y bloquea la entrega.
  - La entrega se realiza escaneando el código de barras/QR del correo de confirmación presentado en el teléfono o papel del cliente, o digitando el código alfanumérico del pedido.
  - El operador solicita el DNI físico del cliente (o de la persona expresamente autorizada en la orden web), digita el número de documento y pulsa "Confirmar Entrega".
  - El sistema marca el pedido como `Entregado`, registra la fecha/hora y el DNI de quien recogió.
  - **Regla de oro de seguridad:** El operador en mostrador no tiene que comprobar ningún pago; la existencia del estado `Listo para Retiro` certifica que Tesorería ya dio la conformidad financiera previa.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Entrega satisfactoria de pedido listo para retiro.**
    - **Dado** que el pedido `PED-4020` está en estado `Listo para Retiro` en el mostrador de Huamanga,
    - **Cuando** el Despachador escanea el código con la pistola óptica, verifica que el DNI del cliente coincide con el titular registrado y pulsa `Confirmar Entrega`,
    - **Entonces** el sistema actualiza el estado a `Entregado` en menos de 1 segundo, imprime la constancia de entrega física y archiva la orden.
  - **Escenario 2: Intento de retiro de pedido no validado por tesorería.**
    - **Dado** que un cliente se presenta en mostrador exigiendo mercadería de un pedido que aún está en estado `Pendiente de Validación de Pago`,
    - **Cuando** el despachador escanea el código,
    - **Entonces** la terminal muestra una alerta modal "PEDIDO NO DISPONIBLE PARA RETIRO: Pago en proceso de verificación por Tesorería", inhabilitando cualquier botón de despacho.

---

### HU-08: Arqueo Ciego de Caja al Cierre de Turno por Cajero
- **Código Relacionado:** `RF-CAJ-002`, `RNF-SEG-004`.
- **Actor:** Cajero.
- **Narrativa:**
  > **Como** Cajero al finalizar mi jornada laboral,  
  > **quiero** declarar el monto físico total de dinero en efectivo y vouchers que tengo en mi gaveta sin que la pantalla me muestre cuánto dinero calcula el sistema,  
  > **para** garantizar la transparencia del cuadre de caja y evitar manipulaciones en el proceso de rendición de cuentas.
- **Reglas de Negocio Asociadas:**
  - Al ejecutar el proceso de "Cierre de Turno", el sistema presenta el formulario de **Arqueo Ciego**.
  - La interfaz oculta completamente el total de ventas del día, el saldo inicial acumulado y el saldo esperado según las transacciones del sistema.
  - El cajero debe desglosar obligatoriamente la cantidad física de billetes y monedas por denominación (S/ 200, S/ 100, S/ 50, S/ 20, S/ 10, monedas de S/ 5, S/ 2, S/ 1, etc.) y registrar la suma de vouchers de tarjetas físicas.
  - Al pulsar "Enviar Arqueo", el sistema bloquea el turno (no admite nuevas ventas) y almacena la declaración del cajero.
  - Inmediatamente después, el sistema genera de forma interna el cálculo de diferencias:  
    $$\text{Diferencia} = \text{Monto Declarado (Físico)} - \text{Monto Teórico (Sistema)}$$
  - Si existe un faltante o sobrante, se emite una alerta clasificada que se envía a la bandeja del Administrador y Tesorería para su liquidación.
- **Criterios de Aceptación (Given-When-Then):**
  - **Escenario 1: Declaración de arqueo ciego sin información previa.**
    - **Dado** que el cajero decide cerrar su turno a las 20:00 h,
    - **Cuando** abre la opción de cierre de caja,
    - **Entonces** la pantalla no muestra ningún monto de ventas acumuladas, solicitando exclusivamente el ingreso de la cantidad de unidades por denominación monetaria.
  - **Escenario 2: Consolidación y detección de diferencias tras el cierre.**
    - **Dado** que el sistema registró ventas en efectivo por S/ 3,250.00 con un fondo inicial de S/ 200.00 (Total esperado: S/ 3,450.00),
    - **Cuando** el cajero ingresa un conteo físico que totaliza S/ 3,430.00 y confirma el cierre,
    - **Entonces** el sistema cierra el turno de forma irrevocable, registra una diferencia de `- S/ 20.00` (Faltante), emite el ticket de cierre para auditoría y remite el reporte al Administrador sin permitir modificaciones posteriores.
