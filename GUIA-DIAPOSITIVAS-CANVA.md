# Guía Maestra de Diapositivas para Canva — SIGIC Ferretería v2.2

> **Asignatura:** IS-488 Arquitectura de Software (UNSCH)  
> **Proyecto:** SIGIC Ferretería (Sistema Integrado de Gestión de Inventario y Comercialización)  
> **Autor:** Crhistian Emanuel Flores Marquina  
> **Fuente de Verdad:** 19 Documentos Validados en `01-analisis-de-sistema/`, `02-arquitectura-software/`, `03-diseno-de-software/` y `04-modelo-c4/`  
> **Carpeta de Recursos Gráficos:** `/home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/`  
> **Formato de Recursos:** PNG 1080p (1920×1080 px, 16:9) listos para arrastrar y soltar (*drag & drop*) en Canva.

---

## Índice Maestro de Láminas y Recursos Gráficos

| N.° Lámina | Fase IS-488 UNSCH | Título Técnico de la Diapositiva | Archivo PNG para Arrastrar en Canva |
| :---: | :--- | :--- | :--- |
| **01** | **Contexto** | Contexto de Negocio y Problemática Operativa en Retail Ferretero | `recursos-exposicion/06-Contexto-Problematica-Negocio.png` |
| **02** | **Actores** | Mapa de Actores, Segregación de Identidades y Sistemas Externos | `recursos-exposicion/07-Mapa-Actores-Fronteras.png` |
| **03** | **Atributos de Calidad** | Atributos de Calidad Rectores y Métricas Cuantificables (ISO/IEC 25010) | `recursos-exposicion/08-Atributos-Calidad-Metricas.png` |
| **04** | **Monolito Modular** | Selección Arquitectónica: Monolito Modular vs. Microservicios | `recursos-exposicion/09-Decision-Monolito-Modular.png` |
| **05** | **PgBouncer** | Segregación de Conexiones y Resiliencia con PgBouncer | `recursos-exposicion/10-PgBouncer-Segregacion-Pools.png` |
| **06** | **Checkout Anti-Deadlock**| Protocolo de Checkout Atómico y Prevención Determinista de Deadlocks | `recursos-exposicion/05-Protocolo-Checkout-AntiDeadlock.png` |
| **07** | **C1: Contexto** | Modelo C4 — Nivel 1: Diagrama de Contexto del Sistema | `recursos-exposicion/01-C1-Contexto-Sistema.png` |
| **08** | **C2: Contenedores** | Modelo C4 — Nivel 2: Diagrama de Contenedores y Persistencia | `recursos-exposicion/02-C2-Contenedores-Segregacion.png` |
| **09** | **C3: Componentes** | Modelo C4 — Nivel 3: Diagrama de Componentes de Checkout Concurrente | `recursos-exposicion/03-C3-Componentes-Checkout-Concurrente.png` |
| **10** | **C4: Clases** | Modelo C4 — Nivel 4: Diagrama de Código y Clases del Dominio | `recursos-exposicion/04-C4-Clases-Dominio.png` |
| **11** | **Seguridad Arqueo** | Seguridad Transaccional: Arqueo Ciego y Custodia de Vouchers | `recursos-exposicion/11-Seguridad-Arqueo-Ciego.png` |
| **12** | **Conclusiones** | Conclusiones Técnicas, Certificación SDD y Valor de Negocio | `recursos-exposicion/12-Conclusiones-Certificacion-SDD.png` |

---

## Especificación Detallada de las 12 Láminas

---

### Diapositiva 01: Contexto de Negocio y Problemática Operativa en Retail Ferretero

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Contexto de Negocio y Problemática Operativa en Retail Ferretero`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Volumetría y Complejidad Dimensional:**
    - Catálogo activo superior a **12,000 SKUs** (fierro corrugado, cemento, tuberías PVC, pernería).
    - Variantes técnicas fraccionadas: pulgadas, milímetros, calibres, cédulas y tipos de rosca.
    - Unidades de medida multiescala: unidad, metro, ciento, millar, bolsa y caja master.
  - **Fricción Concurrente Omnicanal:**
    - Clientes en mostrador físico POS y compradores en e-commerce compitiendo simultáneamente por el mismo inventario.
    - Riesgo de **sobreventa crítica (*overselling*)** de materiales pesados por desfases en catálogo web ($> 30\text{ s}$).
  - **Puntos Críticos de Falla Operativa:**
    - Inanición de conexiones (*Connection Starvation*): ráfagas web saturan PostgreSQL y congelan terminales de caja.
    - Fugas de capital en cierres de turno: manipulación de arqueos cuando el cajero conoce previamente el saldo del sistema.
  - **Invariante Cardinal del Sistema (`RNF-CON-003`):**  
    $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$  
    *(Tolerancia cero: 0 sobreventas y 0 saldos negativos bajo cualquier condición de tráfico).*

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/06-Contexto-Problematica-Negocio.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Profesora, buenos días. SIGIC Ferretería v2.2 resuelve la problemática operativa de un retail ferretero de alta rotación en Ayacucho con más de doce mil artículos activos. La fricción crítica del negocio radica en el choque concurrente entre las cuadrillas de construcción que compran cemento y fierro en el mostrador físico y los clientes que compran por el portal web. Sin un control riguroso de inventario comprometido y sin aislar las conexiones de base de datos, las compras en línea agotan los recursos del servidor y generan sobreventas que paralizan los despachos en obra.*

---

### Diapositiva 02: Mapa de Actores, Segregación de Identidades y Sistemas Externos

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Mapa de Actores, Segregación de Identidades y Sistemas Externos`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Dominio Interno (Colaboradores — RBAC / JWT RS256 / Argon2id):**
    - **Superadministrador (`ROLE_SUPERADMIN`):** Gobernanza de catálogo maestro, reglas de tarifas multiescala y asignación de almacenes.
    - **Cajero Mostrador (`ROLE_CAJERO`):** Facturación rápida 100% por teclado (`F1`–`F12`) y escáner láser sin ratón en viewport $1024 \times 768$. Cobros mixtos y arqueo ciego.
    - **Almacenero (`ROLE_ALMACENERO`):** Recepción de compras, costeo promedio ponderado (CPP), guías de traslado inter-almacén (`#GI-...`) y preparación de pedidos.
    - **Despachador Mostrador (`ROLE_DESPACHADOR`):** Pantalla ligera (`RF-POS-011`) para entrega web cotejando DNI/Código. Aislamiento total: no toca dinero ni inventario.
    - **Personal de Tesorería (`ROLE_TESORERIA`):** Validación en Backoffice de vouchers bancarios Yape/Plin/Transferencias ($\le 5\text{ MB}$) en storage privado.
  - **Dominio Externo (Portal Web Next.js 14 SSR):**
    - **Cliente Web (`Audience: SIGIC_WebClient`):** Público general, Maestro de Obra y Contratista B2B. Tokens desacoplados sin acceso a APIs internas (`HTTP 403`).
  - **Sistemas Externos Homologados de Frontera:**
    - **Proveedor PSE / OSE SUNAT:** Facturación electrónica asíncrona oficial (XML con IGV al 18% y constancias CDR).
    - **Pasarela de Pagos Digitales:** Cobros con tarjetas bajo estándar PCI-DSS y confirmación asíncrona por webhooks firmados con HMAC SHA-256.
    - **Entidades Bancarias / Billeteras:** Cuentas BCP/BBVA y transferencias directas Yape/Plin verificadas contra extractos.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/07-Mapa-Actores-Fronteras.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *En este mapa delimitamos formalmente la frontera de identidades del sistema dividida en dos dominios estancos. En el dominio interno, colaboradores como cajeros, almaceneros y personal de tesorería operan bajo control de acceso basado en roles autenticado con tokens JWT firmados asimétricamente mediante RS256. En el dominio externo, los clientes web acceden con tokens restringidos que impiden cualquier invocación a las APIs internas de mostrador. Observen cómo el despachador de tienda tiene una frontera estricta: entrega pedidos web cotejando el DNI sin manipular dinero ni vouchers bancarios, protegiendo la seguridad financiera de la empresa.*

---

### Diapositiva 03: Atributos de Calidad Rectores y Métricas Cuantificables (ISO/IEC 25010)

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Atributos de Calidad Rectores y Métricas Cuantificables (ISO/IEC 25010)`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Matriz de Atributos de Calidad Rectores (IS-488 UNSCH):**

| ID Rector | Atributo ISO/IEC 25010 | Métrica Cuantificable y Criterio de Aceptación | Mecanismo Arquitectónico Implementado |
| :--- | :--- | :--- | :--- |
| **AC01** | **Rendimiento Transaccional** | Latencia en red local **$P95 < 250\text{ ms}$** en API Core.<br>Captura de ítem $\le 1.0\text{ s}$; cierre total de venta $\le 2.0\text{ s}$. | Conexiones dedicadas en PgBouncer (`pool_pos`, 50 conns). Consultas Dapper sub-15ms y llamadas intra-memoria $< 1\text{ ms}$. |
| **AC02** | **Consistencia Transaccional** | **Cero ($0$) sobreventas** y cero ($0$) saldos negativos.<br>100 hilos concurrentes compitiendo por el mismo SKU. | Bloqueo pesimista `SELECT ... FOR UPDATE` ordenado por `producto_id ASC` y fórmula $\text{Disponible} = \text{Físico} - \text{Comprometido}$. |
| **AC03** | **Trazabilidad y Auditoría** | **100% de operaciones sensibles auditadas**.<br>Cero posibilidad de alteración retroactiva contable. | Estructura **Append-Only** en `caja_arqueo` y `Audit_Log`. Restricción DDL: `REVOKE UPDATE, DELETE ON caja_arqueo`. |

  - **Atributos Secundarios Garantizados:**
    - **RNF-USA-002 (Ergonomía POS):** Viewport estricto $1024 \times 768\text{ px}$ sin desplazamiento horizontal; operación 100% por teclado y escáner láser.
    - **RNF-DIS-001 (Alta Disponibilidad):** Uptime $\ge 99.5\%$ en horario comercial; RTO $\le 60\text{ min}$ y RPO $\le 15\text{ min}$ con archivado continuo de WAL en PostgreSQL 16.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/08-Atributos-Calidad-Metricas.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Nuestra arquitectura se fundamenta en tres atributos de calidad rectores medibles bajo el estándar ISO 25010. El atributo AC01 garantiza que una venta en caja física cierre en menos de dos segundos con una latencia de API P95 inferior a 250 milisegundos gracias al pool dedicado de PgBouncer. El atributo AC02 erradica totalmente las sobreventas forzando la ecuación de stock disponible y aplicando bloqueos pesimistas ordenados en PostgreSQL. Finalmente, el atributo AC03 asegura la inmutabilidad contable mediante tablas append-only donde revocamos formalmente los comandos de actualización y eliminación.*

---

### Diapositiva 04: Selección Arquitectónica: Monolito Modular vs. Microservicios

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Selección Arquitectónica: Monolito Modular vs. Microservicios`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Tabla de Trade-Offs Técnicos (IS-488 UNSCH / Guía 03):**

| Criterio Técnico | Microservicios Distribuidos | Monolito Modular Clean Architecture (SIGIC v2.2) | Justificación de Ingeniería |
| :--- | :--- | :--- | :--- |
| **Consistencia de Inventario (AC02)** | Consistencia Eventual (Sagas / 2PC). Ventana de sobreventa crítica en picos. | **Transacciones ACID Inmediatas** sobre instancia unificada PostgreSQL 16. | En venta de cemento y fierro, la consistencia eventual provoca quiebres físicos irreparables en obra. |
| **Latencia Inter-Módulo (AC01)** | 20 a 50 ms por salto de red HTTP/gRPC entre microservicios. | **$< 1\text{ ms}$** mediante llamadas tipadas en memoria del proceso Kestrel. | Garantiza tiempo de respuesta en mostrador LAN $P95 < 250\text{ ms}$ sin acumulación de latencia de red. |
| **Sobrecarga Operacional (DevOps)** | Clúster Kubernetes, service mesh, múltiples repositorios y alto costo cloud. | **1 solo pipeline CI/CD**, 1 artefacto compilado .NET 8 desplegado en Docker/systemd. | Máxima mantenibilidad para el equipo de TI y presupuesto de la empresa comercial. |
| **Auditoría Contable (AC03)** | Bitácoras distribuidas asíncronas con riesgo de pérdida o desincronización. | **Transactional Outbox y Bitácora Relacional Unificada** (`Audit_Log`, `caja_arqueo`). | Cero desfase entre el registro contable y la mutación física de stock en la misma transacción relacional. |

  - **Regla Estructural Cardinal:** Comunicación entre módulos exclusivamente mediante interfaces de aplicación (`IInventarioReservaService`) y eventos de dominio en memoria con MediatR. Prohibido el acceso cruzado directo a tablas ajenas.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/09-Decision-Monolito-Modular.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Profesora, evaluamos rigurosamente el trade-off entre microservicios y un monolito modular. Desestimamos los microservicios porque su consistencia eventual basada en Sagas introduce ventanas de vulnerabilidad donde un producto se sobrevende antes de propagar el evento, sumado a una latencia de red de 20 a 50 milisegundos que degrada la atención en caja. El Monolito Modular en ASP.NET Core 8 nos permite ejecutar transacciones ACID inmediatas en la misma base de datos con llamadas en memoria inferiores a un milisegundo, simplificando drásticamente el despliegue a un solo pipeline de integración continua.*

---

### Diapositiva 05: Segregación de Conexiones y Resiliencia con PgBouncer

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Segregación de Conexiones y Resiliencia con PgBouncer`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Mecanismo Anti-Inanición (*Connection Starvation*):**
    - PgBouncer 1.21 configurado en modo asignación por transacción (`pool_mode = transaction`).
    - Desacoplamiento total entre las conexiones cliente del frontend y las conexiones físicas del motor PostgreSQL 16.
  - **Segregación Estricta de Pools de Conexión:**
    - **`pool_pos` (Alta Prioridad / Red LAN):**
      - **50 conexiones reservadas exclusivas** para terminales de mostrador y almacén.
      - Latencia de adquisición prácticamente nula ($< 1\text{ ms}$); cero colas de espera.
      - Garantiza el cumplimiento de cobro en mostrador $\le 2.0\text{ s}$ incluso bajo ataque DoS externo.
    - **`pool_web` (Pool Limitado / Control de Backpressure):**
      - **30 conexiones máximas fijas** para el tráfico público del portal e-commerce.
      - Política de contención estricta: timeout de espera en cola de 5 segundos.
      - Si 5,000 usuarios web saturan el checkout en línea, el portal encola o rechaza solicitudes web, pero las cajas físicas operan sin interrupción.
  - **Aislamiento de Carga Analítica:**
    - Datamart dimensional en esquema estrella (`sigic_olap`) para cálculo de Kardex mensual y utilidades por familia.
    - Cero contención de CPU y bloqueos de lectura sobre la base de datos transaccional (`sigic_oltp`).

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/10-PgBouncer-Segregacion-Pools.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Para blindar la atención en tienda física contra el tráfico de Internet, interponemos PgBouncer operando en modo transacción con dos pools estrictamente aislados. El pool_pos reserva cincuenta conexiones exclusivas para las cajas de mostrador, garantizando que el cajero tenga acceso instantáneo al motor de base de datos con latencia sub-milisegundo. En contraste, el pool_web restringe el comercio electrónico a treinta conexiones máximas con control de backpressure; de este modo, aunque el sitio web experimente un pico masivo de visitas o un ataque de denegación de servicio, las cajas físicas continúan facturando con absoluta fluidez.*

---

### Diapositiva 06: Protocolo de Checkout Atómico y Prevención Determinista de Deadlocks

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Protocolo de Checkout Atómico y Prevención Determinista de Deadlocks`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Algoritmo Transaccional Paso a Paso (Invariantes SDD):**
    - **Paso 1: Solicitud de Compra:** Cliente web envía carrito con múltiples SKUs hacia `CheckoutWebController`.
    - **Paso 2: Ordenamiento Determinista Anti-Deadlock (`RF-WEB-005`):**  
      `items.OrderBy(x => x.ProductoId).ToList()` en memoria antes de abrir transacción. Erradica bloqueos cruzados en PostgreSQL.
    - **Paso 3: Apertura Transaccional:** Conexión asignada desde `pool_web` en PgBouncer.
    - **Paso 4: Bloqueo Pesimista de Filas:** `SELECT ... FOR UPDATE` ordenado por identificador de producto.
    - **Paso 5: Revalidación Atómica de Precios:** Valida que la tarifa no haya caducado respecto a la lista asignada.
    - **Paso 6: Verificación de Disponibilidad (`RNF-CON-003`):**  
      Evalúa predicado atómico: `(stock_fisico - stock_comprometido) >= @CantidadSolicitada`. Si no alcanza, emite `HTTP 409 Conflict`.
    - **Paso 7: Incremento de Stock Comprometido con TTL Dinámico (`RF-WEB-002`):**
      - **Pasarela de Tarjetas:** TTL exacto de **20 minutos**.
      - **Pagos Manuales (Yape / Plin / Transferencia):** TTL total de **240 minutos (4 horas)** con ventana máxima de **60 minutos** para adjuntar voucher.
    - **Paso 8: Confirmación Atómica (Commit):** Emisión de Orden Web en estado `Pendiente_Pago` o `Pendiente_Validacion`.
    - **Paso 9: Liberación Asíncrona Idempotente:** Background Worker ejecuta cada **60 segundos** sentencia `SELECT ... FOR UPDATE SKIP LOCKED` para liberar reservas caducadas sin bloquear transacciones activas.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/05-Protocolo-Checkout-AntiDeadlock.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Este diagrama representa el núcleo de concurrencia de nuestro sistema: el protocolo de checkout atómico. Cuando un cliente compra varillas y cemento al mismo tiempo que el cajero factura en ventanilla, el riesgo clásico en bases de datos relacionales es el interbloqueo o deadlock. En SIGIC lo eliminamos ordenando deterministamente los identificadores de producto en memoria antes de solicitar la transacción relacional en PostgreSQL. Luego ejecutamos un SELECT FOR UPDATE, revalidamos atómicamente el precio y el stock disponible, e incrementamos el stock comprometido con su respectivo tiempo de vida útil, liberando automáticamente el inventario mediante un worker en segundo plano si el pago no se concreta.*

---

### Diapositiva 07: Modelo C4 — Nivel 1: Diagrama de Contexto del Sistema

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Modelo C4 — Nivel 1: Diagrama de Contexto del Sistema`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Frontera Operativa y Enfoque de Caja Negra (Simon Brown):**
    - Delimitación del sistema **SIGIC Ferretería** frente a su entorno comercial y fiscal.
  - **Interacción de los 6 Actores Humanos:**
    - **Superadministrador:** Consola central de tarifas, almacenes y permisos RBAC.
    - **Cajero Mostrador:** Facturación presencial de alta velocidad en red LAN.
    - **Almacenero:** Recepción de materiales y control de Kardex CPP.
    - **Despachador Mostrador:** Entrega ágil de pedidos web presenciales (`RF-POS-011`).
    - **Personal de Tesorería:** Validación de extractos bancarios y comprobantes Yape/Plin.
    - **Cliente Web:** Compras en línea, catálogo asíncrono y carga de constancias de abono.
  - **Sistemas Externos Integrados:**
    - **Proveedor PSE/OSE:** Intercambio seguro HTTPS/REST para firma de comprobantes XML y validación CDR ante SUNAT con IGV al 18%.
    - **Pasarela de Pagos:** Sesión de pago digital y confirmación asíncrona mediante webhooks.
    - **Entidades Bancarias:** Conciliación bancaria manual entre capturas y extractos de cuenta corporativa.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/01-C1-Contexto-Sistema.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Descendiendo a la formalización arquitectónica bajo el modelo C4 de Simon Brown, en el Nivel 1 delimitamos el sistema como una caja negra dentro de su entorno comercial. Observen cómo SIGIC no solo interactúa con los seis actores humanos de la ferretería, sino que mantiene integraciones directas con sistemas externos como el proveedor PSE/OSE homologado por SUNAT para facturación electrónica y las pasarelas de pago con tarjeta. Toda la interacción fiscal con SUNAT se realiza de forma asíncrona para que una eventual lentitud en los servidores tributarios nunca detenga la entrega física de mercadería en tienda.*

---

### Diapositiva 08: Modelo C4 — Nivel 2: Diagrama de Contenedores y Persistencia

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Modelo C4 — Nivel 2: Diagrama de Contenedores y Persistencia`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Topología de Cómputo y Procesos de Despliegue:**
    - **Reverse Proxy / API Gateway (Nginx 1.25):** Terminación TLS 1.3, rate limiting y compresión gzip.
    - **SPA POS Mostrador (React 18 / Vite / Tailwind):** Frontend ligero en red LAN, navegación por atajos de teclado sin mouse ($1024 \times 768\text{ px}$).
    - **Portal Web E-Commerce (Next.js 14 SSR):** Frontend público optimizado para SEO y móvil, desacoplado de la base de datos.
    - **Backend API Core (ASP.NET Core 8 Web API):** Núcleo monolítico modular bajo Clean Architecture y runtime Kestrel.
    - **Background Workers (.NET 8 BackgroundService):** Servicio de expiración periódica de reservas cada 60 s y consolidación ETL nocturna.
  - **Infraestructura de Datos y Aislamiento:**
    - **PgBouncer 1.21:** Pooler transaccional con segregación física (`pool_pos`: 50 conns LAN vs. `pool_web`: 30 conns WAN).
    - **PostgreSQL 16 (`sigic_oltp`):** Base de datos transaccional ACID, tablas append-only y bloqueos pesimistas.
    - **Datamart Analítico (`sigic_olap`):** Réplica dimensional en esquema estrella para reportería pesada.
    - **Storage Privado Cifrado (MinIO / FS Local):** Repositorio seguro para vouchers bancarios ($\le 5\text{ MB}$) y constancias XML/CDR SUNAT.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/02-C2-Contenedores-Segregacion.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *En el Nivel 2 de contenedores mostramos la descomposición física y los procesos de despliegue de nuestra solución. Contamos con dos aplicaciones cliente completamente desacopladas: una SPA en React 18 optimizada para red LAN sin uso de ratón y un portal web en Next.js 14 con renderizado en servidor. Ambos frontends se comunican con el backend central en ASP.NET Core 8, el cual intermedia sus accesos a PostgreSQL mediante PgBouncer. Adicionalmente, incorporamos un storage privado cifrado para custodiar los vouchers bancarios de hasta 5 megabytes y un Datamart en esquema estrella que aísla totalmente los reportes gerenciales de la base transaccional.*

---

### Diapositiva 09: Modelo C4 — Nivel 3: Diagrama de Componentes de Checkout Concurrente

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Modelo C4 — Nivel 3: Diagrama de Componentes de Checkout Concurrente`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Clean Architecture Concéntrica en ASP.NET Core 8:**
    - **Capa 1: Presentación (Controllers):**
      - `VentasPOSController`: Cobro en mostrador LAN y atajos de función (`RF-POS-003`).
      - `CheckoutWebController`: Checkout web con filtro 404 estricto para artículos no web (`RF-INV-001`).
      - `DespachoMostradorController`: Pantalla ligera de retiro en tienda (`RF-POS-011`).
    - **Capa 2: Aplicación (CQRS / MediatR):**
      - `IniciarCheckoutCommandHandler`: Orquesta la compra, ordenamiento anti-deadlock y reserva atómica.
      - `CrearVentaPOSCommandHandler`: Ejecuta la facturación presencial y rebaja directa de inventario.
      - `LiberarReservaVencidaCommandHandler`: Decrementa stock comprometido de órdenes caducadas.
    - **Capa 3: Dominio Puro (Domain Services):**
      - `InventarioDomainService`: Aplica fórmula $\text{Disponible} = \text{Físico} - \text{Comprometido}$ y ordenamiento en memoria.
      - `ReservaStockService`: Computa el TTL dinámico (20 min tarjetas / 4 h manuales).
      - `MotorTarifasDomainService`: Resuelve listas de precios (Público, Maestro, Contratista).
    - **Capa 4: Infraestructura (Adapters & Repositories):**
      - `InventarioRepository` (`SIGICDbContext`): Mutaciones ACID pesimistas con EF Core 8.
      - `CatalogoDapperRepository`: Micro-ORM con consultas SQL crudas indexadas ($< 15\text{ ms}$).
      - `StoragePrivadoAdapter`: Cifrado y custodia de comprobantes bancarios y XMLs.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/03-C3-Componentes-Checkout-Concurrente.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Al realizar un zoom en el Nivel 3 al interior del API Core, observamos la implementación estricta de Clean Architecture organizada en cuatro capas concéntricas. La capa de presentación expone controladores especializados para POS y Web, los cuales despachan comandos a través del mediador MediatR sin acoplarse directamente a la lógica de negocio. En el dominio puro, InventarioDomainService gobierna las reglas de inventario ordenando los identificadores antes del bloqueo pesimista. Finalmente, en la capa de persistencia combinamos EF Core 8 para escrituras transaccionales complejas y Dapper para lecturas de catálogo a velocidades sub-15 milisegundos.*

---

### Diapositiva 10: Modelo C4 — Nivel 4: Diagrama de Código y Clases del Dominio

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Modelo C4 — Nivel 4: Diagrama de Código y Clases del Dominio`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Diseño de Dominio Rico (Domain-Driven Design — Sin Modelos Anémicos):**
    - **`Producto` (Aggregate Root):**
      - Encapsula atributos dimensionales, clasificación jerárquica y el flag `DisponibleEnWeb`.
      - Método `ActualizarCostoPonderado(cant, costo)`: recalcula el CPP contable en cada compra.
      - Método `ValidarMargenSeguridad(precio)`: previene ventas por debajo del costo unitario.
    - **`InventarioStock`:**
      - Atributos inmutables: `StockFisico` y `StockComprometido`.
      - Regla cardinal encapsulada: `StockDisponible() => StockFisico - StockComprometido`.
      - Métodos de mutación controlada: `BloquearParaReserva(cant)` y `LiberarReserva(cant)`.
    - **`Venta` y `DetalleVenta`:**
      - Composición estricta de comprobantes oficiales con cálculo de base imponible e IGV al 18%.
      - Soporte para cobro mixto: efectivo, tarjeta y billeteras digitales.
    - **`ArqueoCaja` (Entidad Append-Only):**
      - Encapsula el procedimiento de arqueo ciego sin revelar saldos teóricos al cajero.
      - Desglose por denominaciones monetarias: `Billetes200`, `Billetes100`, `Monedas5`, etc.
      - Cálculo de discrepancias: `MontoDiferencia => MontoDeclarado - MontoCalculadoSistema`.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/04-C4-Clases-Dominio.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Llegando al Nivel 4 de código, presentamos las cuatro entidades de dominio medulares exigidas por la especificación: Producto, Venta, DetalleVenta y ArqueoCaja. En SIGIC repudiamos el modelo anémico: las clases encapsulan su propio comportamiento bajo el principio 'Tell, Don't Ask'. Por ejemplo, la entidad InventarioStock protege la disponibilidad impidiendo que el stock comprometido supere al stock físico. Asimismo, la clase ArqueoCaja materializa nuestra política de arqueo ciego calculando discrepancias contables de forma automática, garantizando que el cajero no pueda alterar la información histórica de caja.*

---

### Diapositiva 11: Seguridad Transaccional: Arqueo Ciego y Custodia de Vouchers

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Seguridad Transaccional: Arqueo Ciego y Custodia de Vouchers`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Procedimiento de Arqueo Ciego (`RF-CAJ-002` / `AC03`):**
    - Declaración obligatoria de efectivo físico por denominaciones de billetes y monedas.
    - **Cierre a ciegas:** el cajero desconoce formalmente el saldo teórico del sistema antes de contar.
    - **Triple Conciliación:** 1. Efectivo físico contado vs. 2. Saldo de ventas del sistema vs. 3. Pasarelas electrónicas.
  - **Inmutabilidad Append-Only en Base de Datos:**
    - Tablas de inserción exclusiva: `caja_arqueo`, `caja_arqueo_detalle` y `Audit_Log`.
    - **Sentencia DDL en PostgreSQL 16:**  
      `REVOKE UPDATE, DELETE ON caja_arqueo, caja_arqueo_detalle FROM rol_cajero;`  
      *(Garantiza no repudio y valor probatorio en auditorías contables).*
  - **Custodia de Comprobantes Bancarios (`RF-WEB-003` / Ley N.° 29733):**
    - Vouchers de hasta **5 MB** (JPG, PNG, PDF) almacenados en Storage Privado cifrado (MinIO/FS).
    - **Cero exposición pública:** sin URLs estáticas; acceso exclusivo para Tesorería mediante URLs pre-firmadas temporales de 15 minutos.
  - **Autenticación Asimétrica:** JWT firmados con RS256 (2048 bits) y contraseñas derivadas con Argon2id ($m=65536, t=3, p=4$).

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/11-Seguridad-Arqueo-Ciego.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *En esta lámina exponemos las defensas de seguridad transaccional que protegen los activos de la empresa. Implementamos el procedimiento de arqueo ciego donde el cajero debe declarar el efectivo físico desglosando billetes y monedas sin conocer el saldo calculado por el sistema, lo que elimina cualquier posibilidad de encubrir faltantes. Para garantizar el no repudio legal, revocamos formalmente los permisos de modificación y borrado en PostgreSQL mediante sentencias DDL. Además, custodiamos los comprobantes bancarios de los clientes en un storage privado cifrado bajo la Ley 29733, permitiendo su visualización únicamente al personal de tesorería a través de URLs temporales pre-firmadas.*

---

### Diapositiva 12: Conclusiones Técnicas, Certificación SDD y Valor de Negocio

- **TÍTULO DE LA DIAPOSITIVA:**  
  `Conclusiones Técnicas, Certificación SDD y Valor de Negocio`

- **CONTENIDO EXACTO PARA COPIAR Y PEGAR EN CANVA:**
  - **Metodología Spec-Driven Development (SDD):**
    - **19 documentos técnicos validados** que actúan como la Única Fuente de Verdad del sistema.
    - Servidor MCP Guardian (`sigic-sdd-guardian`) que audita invariantes de stock antes de generar código.
  - **Logros de Ingeniería Demostrados:**
    - **Cero Sobreventas (0%):** Invariante $\text{Disponible} = \text{Físico} - \text{Comprometido}$ validada bajo estrés con 100 hilos concurrentes.
    - **Erradicación de Deadlocks:** Ordenamiento determinista en memoria `producto_id ASC` con bloqueos `SELECT ... FOR UPDATE`.
    - **Resiliencia Operativa:** PgBouncer con `pool_pos` asegura que el mostrador físico opere ininterrumpidamente ($P95 < 250\text{ ms}$).
    - **Modelo Rico DDD:** Clases con lógica encapsulada y arquitectura limpia concéntrica sin acoplamientos caóticos.
  - **Cumplimiento Tributario y Legal:**
    - Emisión oficial de comprobantes electrónicos homologados ante SUNAT (PSE/OSE, IGV al 18%, XML y CDR).
    - Custodia de datos personales y financieros alineada a la Ley N.° 29733.
  - **Veredicto Académico:** Arquitectura formalizada y aprobada conforme a los estándares de IS-488 UNSCH.

- **IMAGEN ASOCIADA:**  
  `recursos-exposicion/12-Conclusiones-Certificacion-SDD.png`

- **NOTAS DEL EXPOSITOR (GUION VERBAL):**  
  > *Para concluir, SIGIC Ferretería v2.2 demuestra cómo una metodología guiada estrictamente por especificaciones resuelve de raíz los problemas críticos de un negocio comercial complejo. Hemos garantizado cero sobreventas y cero interbloqueos en transacciones concurrentes, blindando el mostrador físico mediante la segregación de conexiones en PgBouncer. Toda la solución cumple rigurosamente con la normativa fiscal de SUNAT y con las directivas de seguridad informática, respaldada por un modelo C4 completo que formaliza la arquitectura requerida en la asignatura IS-488. Muchas gracias por su atención.*

---

## 3. Instrucciones de Montaje Rápido en Canva

1. **Creación del Proyecto:** Crear un diseño de presentación estándar **16:9** (resolución $1920 \times 1080\text{ px}$).
2. **Importación de Recursos:** Abrir la carpeta local `/home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/` y arrastrar los **12 archivos PNG** directamente al panel de subidas de Canva.
3. **Distribución Recomendada en Lienzo:**
   - **Opción A (Lámina Gráfica Completa):** Arrastrar la imagen PNG de la lámina para que ocupe el 100% del lienzo ($1920 \times 1080$), utilizando las notas del expositor en el panel inferior de Canva.
   - **Opción B (Lámina Split / Doble Panel):** Colocar el diagrama PNG en el panel derecho (60% del ancho) y copiar los textos y tablas resumidas de este documento en el panel izquierdo (40% del ancho).
4. **Ensayos Verbales:** Utilizar el guion verbal de 3 líneas provisto en cada lámina para mantener un ritmo de exposición riguroso y conciso durante la defensa final ante el jurado docente de IS-488 UNSCH.
