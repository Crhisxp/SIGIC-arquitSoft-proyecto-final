# Matriz Exhaustiva de Requerimientos Funcionales (SIGIC Ferretería)

> **Documento:** `analisis-de-sistema/03-requisitos-funcionales.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Estructura:** Matriz Formal de Ingeniería de Software (Código | Descripción Funcional | Regla de Negocio | Métrica y Criterio de Aceptación)  

---

## 1. Convención de Codificación

La convención para la identificación de requerimientos sigue el estándar:
$$\text{TIPO}-\text{ÁREA}-\text{NNN}$$
- **TIPO:** `RF` (Requerimiento Funcional).
- **ÁREA:** 
  - `INV`: Gestión de Catálogo e Inventario Multialmacén.
  - `POS`: Captura de Operaciones y Ventas en Mostrador.
  - `PRE`: Configuración de Tarifas, Precios y Descuentos.
  - `CAJ`: Administración y Control de Caja y Turnos.
  - `REP`: Informes y Reportería Gerencial / Analítica.
  - `WEB`: Portal Web de Compras y Pedidos en Línea (E-commerce B2B/B2C).

---

## 2. Gestión de Catálogo e Inventario

| Código | Descripción funcional | Regla de negocio | Métrica y criterio de aceptación |
| :--- | :--- | :--- | :--- |
| **RF-INV-001** | Administración de artículos organizados por familia, subfamilia, marca y línea de producto, con navegación jerárquica tipo árbol, e indicador de canal de venta por artículo. | Todo artículo pertenece a exactamente una familia y una subfamilia; no se elimina un artículo con movimientos de kardex, solo se inactiva. Todo artículo incorpora el atributo de configuración `disponible_en_web` (booleano, por defecto `false`), que el Administrador habilita artículo por artículo. Con `disponible_en_web = false` el artículo es de venta exclusiva en mostrador (p. ej. pernería a granel, cortes a medida, artículos sin empaque estándar) y el catálogo y la API pública del portal lo excluyen de forma absoluta: no aparece en listados, búsquedas, fichas ni en la API de disponibilidad, y un intento directo de pedirlo por API se rechaza (`HTTP 404`). Con `disponible_en_web = true`, el artículo participa en el modelo de Stock Disponible compartido entre mostrador y portal (`RF-WEB-002`, `RNF-CON-003`). | Navegar del nivel raíz a un artículo en $\le 4$ clics. Intento de eliminación de artículo con movimientos rechazado en el 100 % de los casos. 0 artículos con `disponible_en_web = false` visibles o accesibles por el portal o su API, verificado en el 100 % de los casos de prueba. |
| **RF-INV-002** | Creación y gestión de "N" almacenes y áreas de despacho (Almacén Central, Mostrador 1, Mostrador 2, etc.) con su árbol de dependencias físicas. | Cada almacén tiene un responsable asignado y un tipo (central, mostrador); el stock se lleva de forma independiente por almacén. | Alta de un almacén nuevo en $\le 2$ min. El saldo consolidado equivale a la suma de saldos por almacén con diferencia 0. |
| **RF-INV-003** | Emisión de guías internas de traslado con estados: `Borrador`, `En Tránsito`, `Recibido` y `Rechazado`. | Solo transiciones válidas: `Borrador` $\rightarrow$ `En Tránsito` $\rightarrow$ `Recibido` / `Rechazado`. Al pasar a `En Tránsito` se descuenta el origen; el almacén receptor incrementa existencias únicamente tras confirmar físicamente la guía. Si se rechaza, el stock retorna al origen. | Suma de existencias (origen + tránsito + destino) constante durante todo el ciclo de la guía (diferencia = 0). Ninguna guía en estado `Recibido` sin usuario y marca de tiempo de confirmación. |
| **RF-INV-004** | Codificación interna correlativa y compatibilidad con EAN-13 y SKU. | El código interno es único e irrepetible; el EAN-13 se valida por dígito verificador. | 100 % de EAN-13 con dígito verificador inválido rechazados. Cero códigos internos duplicados. |
| **RF-INV-005** | Manejo de múltiples unidades de medida por artículo con factores de conversión (unidad, docena, bolsa, millar, metro). | El stock se almacena en la unidad base; toda venta o traslado se convierte a ella. Los factores admiten decimales. | Conversiones con precisión de 4 decimales; error de redondeo acumulado en kardex = 0 en una prueba de 10 000 movimientos. |
| **RF-INV-006** | Registro de órdenes de compra y carga de artículos desde comprobantes de compra en Almacén Central. | El stock del Almacén Central se incrementa solo al confirmar la recepción de la mercadería contra la orden de compra. | Recepción parcial soportada; el saldo pendiente de la orden coincide con la diferencia entre lo ordenado y lo recibido. |

---

## 3. Captura de Operaciones y Ventas (POS)

| Código | Descripción funcional | Regla de negocio | Métrica y criterio de aceptación |
| :--- | :--- | :--- | :--- |
| **RF-POS-001** | Visualización del stock disponible por almacén en el momento en que el cajero o vendedor registra la venta. | El stock disponible es el saldo físico menos el stock comprometido (reservas de pedidos web vigentes y guías de salida en tránsito) en ese almacén. | Consulta de stock $\le 1$ s (p95). Discrepancia con el saldo real de la base de datos = 0. |
| **RF-POS-002** | Captura y actualización de datos del cliente (DNI, RUC, razón social) o venta a "cliente general". | DNI de 8 dígitos; RUC de 11 dígitos validado con dígito verificador (módulo 11). La venta a cliente general se permite solo con documento interno o boleta bajo el umbral normativo vigente. | Formato inválido rechazado en el 100 % de los casos. Registro de cliente en $\le 15$ s. |
| **RF-POS-003** | Controles ágiles para navegar entre ítems, editar cantidades y cambiar precio según la lista asignada. | Cambiar el precio fuera de la lista asignada exige permiso especial y queda auditado. | Operación completa por teclado y escáner, sin mouse. Toda modificación de precio genera registro de auditoría. |
| **RF-POS-004** | Validación previa al cierre de la venta: cobertura total del pago y existencias suficientes. | No se cierra una venta si $\sum \text{pagos} \ne \text{total}$, o si algún ítem excede el stock del almacén. | 0 ventas cerradas con descuadre. 0 ventas con stock resultante negativo. |
| **RF-POS-005** | Ventas en espera para atender a otro cliente sin perder lo digitado. | Una venta en espera retiene su contenido hasta por 2 horas y no descuenta kardex ni reserva stock definitivo; al vencer, se descarta con registro. | Recuperación de una venta en espera en $\le 2$ s. Cero movimientos de kardex generados por ventas en espera. |
| **RF-POS-006** | Registro de pagos mixtos: efectivo, billeteras digitales (Yape/Plin), transferencia y tarjeta. | Todo pago digital exige captura manual del código de operación (único por medio y fecha). No se asume integración bancaria automática. | 100 % de pagos digitales con código de operación. Códigos duplicados rechazados. Suma de pagos = total al céntimo. |
| **RF-POS-007** | Distinción entre ticket/nota de venta interna y comprobante electrónico (boleta/factura). | El ticket interno no tiene validez tributaria; el comprobante oficial requiere DNI/RUC y su emisión depende del tercero (PSE/OSE). | Cada venta queda con un único tipo de documento. Ninguna nota interna se numera en la serie de comprobantes. |
| **RF-POS-008** | Cálculo del IGV y generación del payload estándar (JSON/XML) del comprobante. | IGV 18 % con redondeo a 2 decimales; base imponible + IGV = total. | Diferencia total vs. suma de componentes = S/ 0.00 en 1 000 casos de prueba. Payload validado contra el esquema del proveedor. |
| **RF-POS-009** | Reporte en tiempo real de transacciones por punto de venta, ticket promedio y volumen facturado. | Las cifras provienen de ventas confirmadas del turno vigente. | Actualización $\le 5$ s tras cada venta. Coincidencia con el detalle de ventas = 100 %. |
| **RF-POS-010** | Reporte de clientes con RUC nuevos o actualizados en el turno. | Se lista todo cliente creado o modificado con RUC durante el turno. | Reporte generado en $\le 3$ s; 0 omisiones frente al registro de auditoría. |
| **RF-POS-011** | Entrega en mostrador de los pedidos web con modalidad Retiro en Tienda, mediante una pantalla ligera de despacho que no realiza validación de pagos ni procesa comprobantes. | El Despachador/Cajero en mostrador solo recibe pedidos que ya llegaron al estado `Listo para Retiro`, es decir, con el pago validado previamente por Backoffice (`RF-WEB-003`). Su interacción se limita a: escanear el código QR o ingresar el código alfanumérico del pedido, verificar el DNI del titular o de la persona autorizada, y confirmar la entrega. No visualiza ni descarga el comprobante de pago (voucher) de 5 MB, no valida códigos de operación y no tiene acceso a la pantalla de Backoffice de tesorería, lo que preserva la velocidad de atención por teclado/escáner de `RF-POS-003` incluso en hora pico. Si el código no está en estado `Listo para Retiro`, el sistema rechaza la entrega y remite el caso a Backoffice. | Validación del código de pedido en $\le 1$ s (p95), sin llamadas a servicios de validación de pago desde esta pantalla. 0 entregas en estado distinto de `Listo para Retiro`. 100 % de entregas con DNI registrado. 0 accesos de este rol a la cola de vouchers de Backoffice. |

---

## 4. Configuración de Tarifas y Precios

| Código | Descripción funcional | Regla de negocio | Métrica y criterio de aceptación |
| :--- | :--- | :--- | :--- |
| **RF-PRE-001** | Diseño y actualización de listas de precios con descuentos por volumen, promociones y venta mayorista. | Un precio vigente por artículo, lista y unidad de medida en cada instante; las promociones tienen fecha de inicio y fin. | Sin solapamientos de vigencia. Actualización masiva de 1 000 precios en $\le 60$ s. |
| **RF-PRE-002** | Segmentación de listas: precio público, maestro de obra, contratista y distribuidor, compartidas por los canales POS y web. | Cada cliente tiene una lista asignada; el precio no puede ser menor al costo salvo autorización del administrador. El perfil de precios de un cliente web lo asigna el administrador, nunca el propio cliente. | 100 % de ventas por debajo del costo requieren autorización registrada. |

---

## 5. Administración y Control de Caja

| Código | Descripción funcional | Regla de negocio | Métrica y criterio de aceptación |
| :--- | :--- | :--- | :--- |
| **RF-CAJ-001** | Apertura y cierre de turno de caja por cajero. | Un cajero mantiene un único turno abierto; no se registran cobros sin turno abierto. | 0 cobros sin turno. Apertura en $\le 30$ s. |
| **RF-CAJ-002** | Arqueo ciego: el cajero declara efectivo y montos digitales sin ver los montos esperados. | El sistema muestra los esperados solo después de confirmar la declaración. | Cuadre al céntimo; 100 % de diferencias distintas de S/ 0.00 exigen justificación. |
| **RF-CAJ-003** | Conciliación de pagos digitales y transferencias por código de operación. | Cada código declarado se cruza con el registrado en el POS; los no conciliados quedan pendientes para revisión. | Reporte de pendientes generado en $\le 5$ s. 0 códigos sin estado de conciliación al cierre. |

---

## 6. Informes y Reportería

| Código | Descripción funcional | Regla de negocio | Métrica y criterio de aceptación |
| :--- | :--- | :--- | :--- |
| **RF-REP-001** | Modelo de información flexible basado en las variables de kardex y de ventas. | Los reportes se construyen desde el Datamart, nunca directamente sobre las tablas OLTP. | 100 % de reportes gerenciales leen del esquema analítico. |
| **RF-REP-002** | Acceso a reportes de balance de caja, rotación de artículos y utilidades. | Márgenes y costos unitarios visibles solo con permiso gerencial. | Acceso denegado (`HTTP 403`) al 100 % de roles no autorizados. |
| **RF-REP-003** | Generación por lotes de kardex valorizado mensual y resumen tributario diario, distribuidos por sucursal/almacén. | Kardex valorizado con método de costeo definido (costo promedio ponderado). | Kardex mensual de 50 000 movimientos en $\le 5$ min. Valorización conciliada con inventario, diferencia = S/ 0.00. |
| **RF-REP-004** | Navegación de reportes en jerarquías (Categoría $\rightarrow$ Marca $\rightarrow$ Producto). | El desglose (*drill-down*) conserva filtros de sucursal y periodo. | Cambio de nivel en $\le 3$ s. |
| **RF-REP-005** | Vista preliminar antes de imprimir comprobantes y cierres de caja. | La impresión final se habilita solo tras la vista previa. | Vista previa en $\le 2$ s. |
| **RF-REP-006** | Reporte Excel de artículos bajo el stock mínimo sin orden de compra generada. | Un artículo aparece si stock $\le$ mínimo y no existe orden de compra abierta. | Archivo `.xlsx` generado en $\le 10$ s; 0 falsos positivos en la muestra de prueba. |
| **RF-REP-007** | Comparativo de ventas, ticket promedio y margen por canal: Mostrador Físico vs. Portal Web. | Toda venta y todo pedido web pagado se etiquetan con su canal de origen; el reporte lee solo del Datamart. | Reporte generado en $\le 5$ s (p95). Suma de los dos canales = total de ventas, diferencia S/ 0.00. |

---

## 7. Portal Web E-commerce (B2B/B2C)

El portal web es el segundo canal de captura comercial del sistema. Consume el mismo núcleo transaccional mediante APIs REST, por lo que no duplica reglas de negocio, listas de precios ni inventario.

| Código | Descripción funcional | Regla de negocio | Métrica y criterio de aceptación |
| :--- | :--- | :--- | :--- |
| **RF-WEB-001** | Catálogo digital y precios diferenciados. Navegación web responsiva del catálogo de materiales (búsqueda, filtros por familia, subfamilia y marca, fichas técnicas) con precios dinámicos según el perfil autenticado: Público General, Maestro de Obra o Contratista. | Los precios provienen de las mismas listas del POS (`RF-PRE-001` y `RF-PRE-002`). Un visitante no autenticado solo ve el precio público; el perfil de precios lo asigna el administrador tras validar al cliente. | Carga del listado del catálogo $\le 1.5$ s (p95) en conexión móvil 4G de referencia. Coincidencia entre precio web y precio POS para el mismo perfil = 100 %. |
| **RF-WEB-002** | Carrito de compras y pedido digital. Registro de órdenes de compra con selección de modalidad: Retiro en Tienda / Mostrador, Retiro en Almacén Central o Envío a Obra en Huamanga. | El portal reserva y despacha prioritariamente desde el Almacén Central (Depósito Principal), el almacén de mayor profundidad de stock y el más adecuado para pedidos B2B de contratistas y envíos a obra. Si el cliente elige Retiro en Tienda / Mostrador, el sistema genera automáticamente, al confirmar el pago, una orden de preparación y guía interna de traslado (`RF-INV-003`) desde el Almacén Central hacia la sede de retiro elegida; el pedido solo llega a `Listo para Retiro` cuando esa guía se confirma como `Recibida` en la sede. La ventana de reserva de Stock Comprometido es dinámica según el medio de pago, no un valor único de 24 horas:<br>• **Pasarela web (tarjeta):** reserva de **20 minutos**, mientras el cliente completa el checkout en línea; vencida sin confirmación de la pasarela, se libera de inmediato.<br>• **Pagos manuales (Yape, Plin, transferencia):** reserva de **hasta 4 horas** dentro del horario operativo de la tienda; el cliente dispone de **1 hora** para adjuntar la constancia de pago, y de no hacerlo, o de no validarse dentro de las 4 horas, la reserva se libera automáticamente mediante un proceso en segundo plano (*background service*) idempotente. En ningún caso se altera el kardex físico definitivo hasta la confirmación del pago. | Reserva creada de forma atómica en $\le 2$ s (p95). 0 pedidos con cantidad reservada mayor al stock disponible del Almacén Central. Liberación de reservas de pasarela vencidas en $\le 1$ min tras los 20 min. Liberación de reservas de pago manual en $\le 5$ min tras cumplirse el plazo de 1 h (comprobante) o 4 h (validación). 100 % de los pedidos con Retiro en Tienda con guía interna generada automáticamente al confirmarse el pago. |
| **RF-WEB-003** | Pasarela y registro de pago web. Subida de constancia de pago con código de operación (Yape, Plin, transferencia BCP/BBVA) o pago con tarjeta mediante la pasarela en línea de un proveedor certificado. | Tesorería valida el código de operación antes de autorizar el despacho físico; el código es único por medio y fecha. SIGIC no almacena datos de tarjeta; la confirmación de la pasarela se recibe por webhook firmado e idempotente. | 100 % de pagos manuales con código y constancia (JPG, PNG o PDF de hasta 5 MB). Validación de tesorería registrada con usuario y fecha; 0 despachos sin pago validado. Confirmación de pasarela procesada en $\le 10$ s. |
| **RF-WEB-004** | Trazabilidad y estado del pedido. El cliente consulta el estado en tiempo real: `Pendiente de Validación` $\rightarrow$ `En Preparación en Depósito` $\rightarrow$ `Listo para Retiro` / `En Ruta` $\rightarrow$ `Entregado`. | Las transiciones siguen ese orden y las ejecuta solo personal autorizado (tesorería valida el pago, el Almacenero prepara, el Despachador marca Listo, En Ruta o Entregado). Estados de excepción: `Vencido` y `Cancelado`. Al validarse el pago, el Stock Comprometido se convierte en salida definitiva del kardex. | Cambio de estado visible al cliente en $\le 5$ s. 100 % de transiciones con usuario y marca de tiempo. 0 pedidos en `Entregado` sin pago validado. |
| **RF-WEB-005** | Sincronización asíncrona de inventario y revalidación atómica en checkout. El catálogo web consulta la disponibilidad física consolidada sin bloquear las transacciones del punto de venta en mostrador; la confirmación del pedido nunca confía en esa proyección. | El disponible mostrado en el catálogo (físico − comprometido) proviene de un modelo de lectura actualizado de forma asíncrona, con un desfase de hasta 30 s, y es solo informativo. Al pulsar "Confirmar Pedido / Reservar", el núcleo transaccional ejecuta una revalidación atómica dentro de una única transacción ACID sobre PostgreSQL 16: (a) relee el precio vigente de la lista asignada al cliente (`RF-PRE-001`), descartando el precio que el navegador tenía en caché; y (b) revalida el stock disponible real con bloqueo de fila (`SELECT … FOR UPDATE` seguido de `UPDATE` condicionado `WHERE fisico − comprometido >= :n`, equivalente al patrón de `RNF-CON-003`). Si el precio cambió, el checkout se detiene y se le muestra al cliente el precio actualizado para su confirmación; si el stock ya no alcanza, el pedido se ajusta a la cantidad disponible o se rechaza, nunca se confirma por encima del disponible real. Ninguna consulta de catálogo bloquea filas del POS; la revalidación de checkout sí usa bloqueo de fila, pero es una transacción corta. | Desfase máximo de la disponibilidad mostrada en catálogo $\le 30$ s. 100 % de los checkouts revalidan precio y stock contra el núcleo transaccional antes de confirmar, con 0 confirmaciones basadas solo en el valor cacheado. Revalidación atómica de checkout $\le 300$ ms (p95). Degradación del cobro en POS $\le 10$ % con 200 sesiones web concurrentes. 0 bloqueos de fila atribuibles a consultas de catálogo (no de checkout). |
