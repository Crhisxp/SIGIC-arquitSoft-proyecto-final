# Guía de Montaje e Índice para Canva — SIGIC Ferretería v2.2

> **Proyecto:** SIGIC Ferretería (Sistema Integrado de Gestión de Inventario y Comercialización)  
> **Asignatura:** IS-488 Arquitectura de Software (UNSCH)  
> **Carpeta de Recursos:** `recursos-exposicion/`  
> **Formato de Salida:** Imágenes PNG 1080p (1920×1080 px, 16:9) y SVGs vectoriales listos para arrastrar y soltar (*drag & drop*) en Canva.

---

## 1. Estructura General de la Exposición en Canva

Para lograr una exposición académica impecable con alto impacto visual y rigor técnico, se estructura la presentación en la siguiente secuencia de diapositivas recomendada:

| N.° Slide Canva | Sección de la Presentación | Archivo de Imagen Asociado | Tipo de Recurso | Propósito en la Exposición |
| :---: | :--- | :--- | :---: | :--- |
| **Slide 01** | Portada y Título | *(Diseño editorial en Canva)* | Texto / Identidad | Presentación del proyecto, autor, docente y contexto comercial. |
| **Slide 02** | Problemática y Drivers | *(Tabla de Atributos AC01-AC03)* | Texto / Métricas | Fricciones de concurrencia ferretera y los 3 atributos de calidad rectores. |
| **Slide 03** | Estilo: Monolito Modular vs Microservicios | *(Cuadro Comparativo)* | Tabla / Trade-off | Justificación de transacciones ACID y eliminación de latencia LAN. |
| **Slide 04** | **C4 Nivel 1: Contexto de Sistema** | `01-C1-Contexto-Sistema.png` | **Diagrama 1080p** | Frontera del sistema, los 6 actores y sistemas externos (SUNAT, Pasarela). |
| **Slide 05** | **C4 Nivel 2: Contenedores y Segregación** | `02-C2-Contenedores-Segregacion.png` | **Diagrama 1080p** | Frontends desacoplados, PgBouncer (`pool_pos`/`pool_web`) y Storage Privado. |
| **Slide 06** | **C4 Nivel 3: Componentes Internos** | `03-C3-Componentes-Checkout-Concurrente.png` | **Diagrama 1080p** | Clean Architecture en ASP.NET Core 8, MediatR CQRS y Persistencia dual. |
| **Slide 07** | **C4 Nivel 4: Modelo de Dominio** | `04-C4-Clases-Dominio.png` | **Diagrama 1080p** | Clases `Producto`, `Venta`, `DetalleVenta`, `ArqueoCaja` e invariantes DDD. |
| **Slide 08** | **Protocolo Checkout Anti-Deadlock** | `05-Protocolo-Checkout-AntiDeadlock.png` | **Diagrama 1080p** | Secuencia técnica de bloqueo ordenado, revalidación y worker asíncrono. |
| **Slide 09** | Conclusiones y Demostración SDD | *(Resumen de Certificación)* | Texto / Cierre | Gobernanza con MCP Guardian y cumplimiento de la especificación v2.2. |

---

## 2. Detalle de Diapositivas Técnicas y Guión del Expositor

---

### Slide 04 — Diagrama de Contexto del Sistema (C4 Nivel 1)
- **Archivo para Arrastrar:** [`01-C1-Contexto-Sistema.png`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/01-C1-Contexto-Sistema.png)  
- **Archivo Vectorial:** [`01-C1-Contexto-Sistema.svg`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/01-C1-Contexto-Sistema.svg)  
- **Vista Web Interactiva:** [`01-C1-Contexto-Sistema.html`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/01-C1-Contexto-Sistema.html)

#### Texto de Apoyo en Canva:
> **Título:** Frontera Operativa Omnicanal y Sistemas Externos Integrados  
> **Subtítulo:** Interacción simultánea de colaboradores de tienda, clientes web y entidades fiscales.  
> **Puntos en Diapositiva:**
> - **Actores Humanos Segregados:** Superadmin (RBAC y tarifas), Cajero POS (mostrador rápido LAN), Cliente Web (catálogo y compras B2B/B2C), Almacenero (Kardex CPP) y Despachador (`RF-POS-011`).
> - **Integraciones Oficiales:** Proveedor PSE/OSE homologado SUNAT (XML con 18% IGV), Pasarela de tarjetas (TTL 20 min) y Entidades Bancarias para abonos manuales Yape/Plin.
> - **Aislamiento Operativo:** La verificación de comprobantes recae exclusivamente en Tesorería, protegiendo al mostrador físico de cuellos de botella.

#### Guión Verbal del Expositor (Speech):
> *"Buenos días profesora y compañeros. En este primer nivel de Simon Brown delimitamos la frontera de SIGIC Ferretería tratando al software como una caja negra. Resolvemos una fricción comercial real: el choque entre la venta presencial de constructores que compran cemento y fierro en mostrador físico y los clientes que adquieren materiales en línea. Nuestro sistema no solo atiende al cajero en red local con atajos de teclado sin mouse, sino que se integra asíncronamente con SUNAT para la facturación electrónica oficial y con pasarelas de pago digitales. Observen cómo el despachador de tienda tiene una frontera estricta: solo valida entregas cotejando DNI sin manipular dinero ni vouchers bancarios, protegiendo la seguridad financiera de la empresa."*

---

### Slide 05 — Diagrama de Contenedores y Segregación de Persistencia (C4 Nivel 2)
- **Archivo para Arrastrar:** [`02-C2-Contenedores-Segregacion.png`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/02-C2-Contenedores-Segregacion.png)  
- **Archivo Vectorial:** [`02-C2-Contenedores-Segregacion.svg`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/02-C2-Contenedores-Segregacion.svg)  
- **Vista Web Interactiva:** [`02-C2-Contenedores-Segregacion.html`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/02-C2-Contenedores-Segregacion.html)

#### Texto de Apoyo en Canva:
> **Título:** Topología de Contenedores y Aislamiento de Conexiones en PgBouncer  
> **Subtítulo:** Desacoplamiento de frontends especializados y blindaje de la base de datos relacional.  
> **Puntos en Diapositiva:**
> - **Frontends Especializados:** SPA POS React 18 optimizado para 1024×768 y atajos F1-F12 versus Portal Web Next.js 14 SSR para SEO y móvil.
> - **Pooler PgBouncer:** `pool_pos` con 50 conexiones reservadas LAN de alta prioridad frente a `pool_web` con 30 conexiones limitadas para contención de sobrecarga (*backpressure*).
> - **Storage Privado Cifrado:** Almacén seguro para vouchers de pago ($\le 5\text{ MB}$) y constancias tributarias XML/CDR sin exposición pública directa.
> - **Desacoplamiento OLTP vs. OLAP:** Extracción nocturna diaria hacia un Datamart en esquema estrella, eliminando contención de CPU en horas de venta.

#### Guión Verbal del Expositor (Speech):
> *"Al descender al Nivel 2, mostramos cómo está estructurado el despliegue del sistema. SIGIC opera como un Monolito Modular en ASP.NET Core 8 Web API, pero cuenta con dos frontends radicalmente distintos: una SPA en React 18 que corre en la red local de tienda para garantizar una latencia inferior a un segundo por cobro, y un portal Next.js 14 con Server-Side Rendering expuesto a Internet. Para evitar que una ráfaga de compras web o un ataque a la tienda en línea deje sin conexiones al cajero físico, interponemos PgBouncer configurando dos pools estrictamente aislados: pool_pos con 50 conexiones garantizadas para el mostrador y pool_web con 30 conexiones limitadas. Adicionalmente, separamos la base de datos transaccional del Datamart analítico y habilitamos un storage privado para custodiar los vouchers bancarios de hasta 5 MB sin exponerlos a la web pública."*

---

### Slide 06 — Diagrama de Componentes Internos: Checkout Concurrente (C4 Nivel 3)
- **Archivo para Arrastrar:** [`03-C3-Componentes-Checkout-Concurrente.png`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/03-C3-Componentes-Checkout-Concurrente.png)  
- **Archivo Vectorial:** [`03-C3-Componentes-Checkout-Concurrente.svg`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/03-C3-Componentes-Checkout-Concurrente.svg)  
- **Vista Web Interactiva:** [`03-C3-Componentes-Checkout-Concurrente.html`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/03-C3-Componentes-Checkout-Concurrente.html)

#### Texto de Apoyo en Canva:
> **Título:** Arquitectura Limpia Interna en ASP.NET Core 8 Web API  
> **Subtítulo:** Desacoplamiento estricto de casos de uso mediante MediatR CQRS e Inversión de Dependencias.  
> **Puntos en Diapositiva:**
> - **Controladores Segregados:** `VentasPOSController` (LAN) y `CheckoutWebController` (Web con filtro 404 estricto para artículos presenciales).
> - **Orquestación Transaccional:** `IniciarCheckoutCommandHandler` ordena los identificadores en memoria (`ORDER BY producto_id ASC`) antes de abrir la transacción.
> - **Servicios de Dominio Puros:** `ReservaStockService` gobierna el TTL dinámico (20 min tarjetas / 4 horas manuales); `InventarioDomainService` aplica $\text{Disponible} = \text{Físico} - \text{Comprometido}$.
> - **Acceso a Datos Híbrido:** EF Core 8 para mutaciones ACID con `SELECT ... FOR UPDATE` y Dapper para lecturas de catálogo a velocidades sub-15ms.

#### Guión Verbal del Expositor (Speech):
> *"En el Nivel 3 hacemos un zoom al interior de nuestra API central. La aplicación implementa Clean Architecture respetando la regla concéntrica de dependencias: la capa de dominio no conoce ni a la base de datos ni al framework web. Empleamos MediatR para desacoplar comandos y consultas: IniciarCheckoutCommandHandler coordina la compra asegurando que antes de emitir cualquier bloqueo en PostgreSQL, los productos sean ordenados ascendentemente por ID para eliminar los interbloqueos. Noten la combinación de tecnologías en persistencia: utilizamos EF Core 8 para la confirmación atómica de escrituras complejas y Dapper como micro-ORM para que las búsquedas de catálogo respondan en menos de 15 milisegundos, cumpliendo con nuestro atributo de calidad AC01."*

---

### Slide 07 — Diagrama de Código: Clases del Modelo de Dominio (C4 Nivel 4)
- **Archivo para Arrastrar:** [`04-C4-Clases-Dominio.png`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/04-C4-Clases-Dominio.png)  
- **Archivo Vectorial:** [`04-C4-Clases-Dominio.svg`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/04-C4-Clases-Dominio.svg)  
- **Vista Web Interactiva:** [`04-C4-Clases-Dominio.html`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/04-C4-Clases-Dominio.html)

#### Texto de Apoyo en Canva:
> **Título:** Modelo de Dominio Rico: Entidades, Invariantes y Métodos de Negocio  
> **Subtítulo:** Eliminación del modelo anémico mediante encapsulamiento y comportamiento puro en C#.  
> **Puntos en Diapositiva:**
> - **`Producto` (Aggregate Root):** Encapsula el Costo Promedio Ponderado (`ActualizarCostoPonderado`) y el flag `DisponibleEnWeb`.
> - **`Venta` y `DetalleVenta`:** Composición estricta de comprobantes oficiales con desglose de IGV al 18% y soporte para cobro mixto.
> - **`ArqueoCaja` (Append-Only):** Cierre ciego con declaración por denominaciones de billetes y monedas; prohibición de `UPDATE` y `DELETE` en PostgreSQL.
> - **`InventarioStock`:** Gobierna la fórmula inviolable: $\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$.

#### Guión Verbal del Expositor (Speech):
> *"Llegando al Nivel 4 de código, presentamos las cuatro clases de dominio medulares solicitadas por la especificación: Producto, Venta, DetalleVenta y ArqueoCaja. En SIGIC repudiamos el modelo anémico: los objetos de dominio encapsulan su propio comportamiento bajo el principio 'Tell, Don't Ask'. Por ejemplo, la entidad InventarioStock protege la disponibilidad impidiendo que el stock comprometido supere al stock físico. Asimismo, la clase ArqueoCaja materializa nuestra política de arqueo ciego: el cajero no puede consultar el saldo del sistema antes de cerrar su turno, ingresando únicamente el conteo físico de billetes y monedas. Para garantizar no repudio, revocamos formalmente los permisos de modificación y eliminación a nivel de base de datos."*

---

### Slide 08 — Protocolo Transaccional de Checkout y Control Anti-Deadlock
- **Archivo para Arrastrar:** [`05-Protocolo-Checkout-AntiDeadlock.png`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/05-Protocolo-Checkout-AntiDeadlock.png)  
- **Archivo Vectorial:** [`05-Protocolo-Checkout-AntiDeadlock.svg`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/05-Protocolo-Checkout-AntiDeadlock.svg)  
- **Vista Web Interactiva:** [`05-Protocolo-Checkout-AntiDeadlock.html`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/05-Protocolo-Checkout-AntiDeadlock.html)

#### Texto de Apoyo en Canva:
> **Título:** Protocolo de Checkout Atómico y Prevención Determinista de Deadlocks  
> **Subtítulo:** Secuencia paso a paso para compras concurrentes bajo la metodología SDD.  
> **Puntos en Diapositiva:**
> - **Paso 2 (Anti-Deadlock):** Ordenamiento determinista en memoria: `items.OrderBy(x => x.ProductoId).ToList()` antes de abrir transacción.
> - **Paso 4 (Bloqueo Pesimista):** `SELECT ... FOR UPDATE` ordenado por ID en PostgreSQL 16 sobre la conexión de `pool_web`.
> - **Paso 6 (Invariante AC02):** Revalidación atómica: comprueba que el precio siga vigente y que `stock_fisico - stock_comprometido >= solicitado`.
> - **Paso 7 (TTL Dinámico):** 20 minutos para pasarela de tarjetas; 4 horas para transferencias manuales con 1 hora para adjuntar el voucher.
> - **Paso 10 (Worker Asíncrono):** Liberación periódica cada 60s con `FOR UPDATE SKIP LOCKED`, garantizando idempotencia absoluta.

#### Guión Verbal del Expositor (Speech):
> *"Finalmente, esta diapositiva detalla el núcleo de concurrencia de nuestro proyecto: el flujo transaccional paso a paso. Cuando múltiples usuarios compran varillas de fierro y bolsas de cemento al mismo tiempo que un cajero factura en tienda, el riesgo clásico en bases de datos relacionales es el interbloqueo o Deadlock. En SIGIC lo resolvemos ordenando deterministamente los identificadores de productos en memoria antes de solicitar la transacción. Luego ejecutamos un SELECT FOR UPDATE sobre PostgreSQL 16, revalidamos atómicamente el precio y la existencia disponible, e incrementamos el stock comprometido asignando el TTL respectivo. Si el cliente no completa el pago en 20 minutos o no sube su voucher en 1 hora, un servicio en segundo plano despierta cada 60 segundos y libera el inventario automáticamente mediante sentencias SKIP LOCKED, logrando cero sobreventas y máxima disponibilidad."*

---

## 3. Preguntas Frecuentes de Evaluación (Defensa ante el Jurado)

| Pregunta Típica del Evaluador | Fundamento Técnico de Respuesta (SIGIC v2.2) |
| :--- | :--- |
| **¿Por qué eligieron un Monolito Modular en lugar de Microservicios?** | Para evitar la *consistencia eventual* de microservicios con Sagas/2PC en un negocio de materiales pesados donde la sobreventa es crítica. El monolito garantiza transacciones ACID unificadas inmediatas y latencias en memoria sub-milisegundo ($< 1\text{ ms}$) en red LAN mostrador. |
| **¿Cómo garantizan que un ataque DoS al e-commerce no detenga las cajas físicas?** | Mediante **PgBouncer** en modo transacción: `pool_pos` reserva 50 conexiones exclusivas y de alta prioridad para la LAN de tiendas, mientras que `pool_web` tiene una cuota fija de 30 conexiones con *backpressure* y timeout de 5 segundos. |
| **¿Qué ocurre si dos clientes intentan comprar la última bolsa de cemento simultáneamente?** | El ordenamiento determinista y el `SELECT ... FOR UPDATE` en PostgreSQL obligan a que una transacción adquiera el cerrojo de fila primero. La segunda transacción espera; al evaluarse el invariante $\text{stock\_fisico} - \text{stock\_comprometido} \ge \text{cant}$, detectará saldo cero y retornará `HTTP 409 Conflict`. |
| **¿Por qué el Arqueo de Caja se define como Append-Only?** | Para cumplir con `AC03` y `RNF-SEG-004`. Al prohibir sentencias `UPDATE` y `DELETE` en la tabla `caja_arqueo`, garantizamos que ningún empleado o administrador pueda alterar el historial de discrepancias de turnos, asegurando no repudio y trazabilidad contable auditada. |
| **¿Dónde y cómo se protegen los comprobantes de pago de los clientes?** | En un **Storage Privado** cifrado (MinIO o sistema de archivos local en RAID 1). Los archivos no se exponen bajo ninguna URL pública; el acceso a los vouchers de hasta 5 MB está restringido al rol de Tesorería a través de endpoints seguros de la API Core. |

---

## 4. Instrucciones para Canva

1. Abre tu presentación en **Canva** (formato 16:9 presentación estándar 1920×1080).
2. Selecciona y arrastra los archivos PNG desde la carpeta local:  
   `/home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/`
3. Cada imagen encaja con exactitud en las dimensiones de diapositiva (`1920 x 1080 px`).
4. Utiliza los textos de apoyo y el guión verbal de este documento para complementar las notas de la presentación y la exposición oral frente a la docente.
