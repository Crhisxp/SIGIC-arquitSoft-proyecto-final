# Decisiones Arquitectónicas Formales (ADRs) - SIGIC Ferretería

> **Documento:** `analisis-de-sistema/07-decision-arquitectonica.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Disciplina:** Registro de Decisiones de Arquitectura (ADR – *Architectural Decision Records*)  
> **Metodología:** Alineado a la Guía 03 (Estructura de Trazabilidad, Drivers Rectores y Plantilla Estándar Nygard/MADR)

---

## 1. Tabla Consolidada de Decisiones Arquitectónicas

La siguiente matriz presenta el compendio consolidado de decisiones tomadas para dar respuesta rigurosa a los **Drivers Arquitectónicos (DA-01 a DA-07)** identificados para **SIGIC Ferretería**, detallando su justificación técnica en el contexto del negocio ferretero y su impacto estructural.

| ID | Decisión Arquitectónica | Driver Relacionado | Justificación Técnica en SIGIC | Resultado en la Arquitectura |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-001** | **Adopción de Arquitectura Monolítica Modular en Núcleo Transaccional Unificado** | `DA-01`, `DA-05`, `DA-06` | Una arquitectura de microservicios distribuidos añadiría complejidad excesiva (transacciones distribuidas 2PC/Sagas de inventario, latencia de red en LAN) incompatible con el tamaño de equipo y la necesidad de ACID estricto entre mostrador y web. | Un solo despliegue de backend en ASP.NET Core 8 Web API estructurado internamente en módulos cohesivos (`Inventario`, `VentasPOS`, `PedidosWeb`, `Precios`, `Caja`, `Seguridad`). Cero sobrecosto de red inter-servicio. |
| **ADR-002** | **Implementación del Patrón Clean Architecture (Arquitectura Limpia con Puertos y Adaptadores)** | `DA-04`, `DA-06` | Las reglas complejas del negocio ferretero (kardex por costo promedio ponderado, factores de conversión multiescala, reservas dinámicas, listas de precios mayoristas) deben aislarse de la base de datos, frameworks web y servicios de terceros. | Descomposición en 4 capas concéntricas con regla de dependencia unidireccional: *Domain*, *Application*, *Infrastructure*, *Presentation*. Inversión de dependencias mediante interfaces (puertos) para repositorios y proveedores externos. |
| **ADR-003** | **Estrategia de Concurrencia Pesimista Fina, Fórmulas Segregadas y TTL Dinámico de Reservas** | `DA-01`, `DA-02` | La sobreventa de materiales pesados (fierro, cemento) es inasumible. El modelo $Disponible = Físico - Comprometido$ junto a bloqueos pesimistas de fila (`SELECT ... FOR UPDATE`) con orden determinista (`ORDER BY producto_id ASC`) previene sobreventas y deadlocks sin bloquear tablas completas. | Cero saldos negativos en inventario. Bloqueos atómicos de fila de corta duración en PostgreSQL 16. Worker asíncrono idempotente para liberación de reservas web con TTL (20 min pasarela; 1 h comprobante / 4 h validación en pagos manuales). |
| **ADR-004** | **Segregación de Conexiones en PgBouncer y Desacoplamiento de Lecturas Web (CQRS Ligero)** | `DA-02`, `DA-05` | El tráfico de navegación masivo del catálogo web no debe competir por conexiones ni degradar el cobro en mostrador físico ($p95 \le 1$ s). | PgBouncer configurado en modo transacción con dos pools segregados (`pool_pos` prioritario con conexiones garantizadas y `pool_web` con cuota limitada). Catálogo web consume vistas pre-renderizadas/cacheadas con desfase de hasta 30 s; checkout efectúa revalidación atómica. |
| **ADR-005** | **Dualidad de Dominios de Identidad, Autenticación Asimétrica RS256 y RBAC en Servidor** | `DA-03` | Evitar la superficie de ataque y el riesgo de escalada de privilegios entre clientes web externos no confiables y colaboradores internos de la ferretería. | Dos almacenes de identidad lógicos. Emisión de JWT firmados con clave RSA 2048 (RS256) con claims diferenciados. Middleware de autorización en ASP.NET Core que rechaza con `HTTP 403` a clientes web en endpoints internos de caja/inventario. Passwords en Argon2id. |
| **ADR-006** | **Desacoplamiento Operativo de Pagos: Consola Backoffice Asíncrona y Adaptadores de Pasarela** | `DA-04`, `DA-02` | En Ayacucho, los pagos web se efectúan mayormente por Yape/Plin y transferencias con subida de comprobantes (archivos de 5 MB). La validación humana de vouchers colapsaría la atención presencial del cajero/despachador si se hiciera en la misma cola. | Consola desacoplada de Backoffice para Tesorería. La pantalla de mostrador POS solo visualiza pedidos en estado `Listo para Retiro`. Integración de pasarelas web desacoplada mediante patrón Adapter y webhooks idempotentes. |
| **ADR-007** | **Segregación de Frontends: SPA React 18 (POS Mostrador) y Next.js 14 SSR (Portal Web)** | `DA-02`, `DA-05` | El cajero requiere una SPA ultra-ligera en LAN optimizada para atajos de teclado (`F1`-`F12`) y escáner óptico sin recarga de página. El cliente web requiere SSR para SEO, indexación de materiales y carga inicial inferior a 1.5 s en conexiones móviles 4G. | Dos clientes frontend especializados que consumen el mismo backend ASP.NET Core 8 vía API REST: SPA React 18 para mostrador y Next.js 14 para el portal e-commerce. |
| **ADR-008** | **Aislamiento de Base de Datos Transaccional (OLTP) y Datamart Dimensional (OLAP) con ETL Asíncrono** | `DA-07` | El cálculo de Kardex valorizado mensual y balances financieros sobre más de 50 000 movimientos satura memoria *work_mem* y CPU, arriesgando bloqueos en el POS. | Separación física/lógica: base de datos operacional `sigic_oltp` y Datamart analítico `sigic_olap` en esquema estrella (`Fact_Ventas`, dimensiones conformadas). Proceso ETL nocturno asíncrono para poblar el Datamart sin impacto en horario de atención. |

---

## 2. Detalle Formal de los Registros de Decisiones de Arquitectura (ADRs)

---

### ADR-001: Adopción de Arquitectura Monolítica Modular en Núcleo Transaccional Unificado

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-01` (Concurrencia e Integridad de Inventario), `DA-05` (Desacoplamiento Omnicanal), `DA-06` (Mantenibilidad del Dominio)

#### Contexto y Planteamiento del Problema
SIGIC Ferretería debe coordinar operaciones de dos canales comerciales concurrentes: ventas físicas presenciales por escáner en mostrador y compras con reserva en portal web e-commerce. Ambos canales compiten directamente por el mismo stock físico de materiales pesados (cemento, fierro, agregados). Se requiere decidir el estilo arquitectónico general del backend para garantizar integridad transaccional inmediata (ACID), simplicidad operativa y bajo coste de mantenimiento para el equipo de desarrollo.

#### Alternativas Consideradas y Análisis Comparativo
1. **Arquitectura de Microservicios Distribuidos:**
   - *Ventajas:* Despliegue independiente por servicio; escalamiento elástico del catálogo web sin tocar el servicio de ventas físicas.
   - *Desventajas:* Requiere implementar el patrón Saga o transacciones distribuidas (2PC) para garantizar consistencia entre el servicio de inventario y los servicios de pedidos POS y Web. Añade latencia de red HTTP/gRPC entre servicios, eventual consistencia con alto riesgo de sobreventa de cemento/fierro, y complejidad de observabilidad desproporcionada para el contexto del proyecto.
2. **Monolito Tradicional Monocapa (Spaghetti):**
   - *Ventajas:* Rápido desarrollo inicial.
   - *Desventajas:* Acoplamiento caótico entre módulos de inventario, facturación y caja; nula capacidad de probar reglas de negocio de forma aislada; alto riesgo de efectos secundarios al modificar tarifas.
3. **Monolito Modular (Seleccionada):**
   - *Ventajas:* Despliegue único y simplificado en un solo proceso ejecutable; comunicación en memoria con latencia cero entre módulos; transaccionalidad ACID local respaldada directamente por PostgreSQL 16; estricta segregación de límites de módulos (*bounded contexts*) con interfaces explícitas.

#### Decisión Tomada
Se decide implementar un **Monolito Modular** sobre ASP.NET Core 8 Web API. El núcleo del sistema se divide internamente en módulos altamente cohesivos (`Inventario`, `VentasPOS`, `PedidosWeb`, `Precios`, `Caja`, `Auditoría`), compartiendo una única base de datos transaccional con esquemas estructurados y fronteras lógicas bien delimitadas.

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - Garantía de transaccionalidad ACID inmediata sobre las tablas de inventario sin necesidad de coordinadores de transacciones distribuidas.
  - Latencia intra-servicio prácticamente nula ($< 1$ ms), vital para el cobro rápido en mostrador.
  - Costo de infraestructura y esfuerzo de despliegue/DevOps significativamente menor.
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* Todo el sistema se despliega como una sola unidad; una caída del proceso afecta a ambos canales.
  - *Mitigación:* Se implementa contención de errores mediante middlewares de resiliencia en ASP.NET Core, pruebas unitarias exhaustivas en la capa de dominio y pool segregado de conexiones de base de datos con PgBouncer para evitar que la carga web bloquee las operaciones internas.

---

### ADR-002: Implementación de Clean Architecture (Arquitectura Limpia con Puertos y Adaptadores)

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-04` (Desacoplamiento de Fronteras Externas), `DA-06` (Mantenibilidad y Evolución del Dominio Ferretero)

#### Contexto y Planteamiento del Problema
El dominio ferretero de SIGIC posee reglas de negocio rigurosas: kardex valorizado por Costo Promedio Ponderado, cálculo de stock disponible restando reservas dinámicas, factores de conversión multiescala (unidades, bolsas, varillas, toneladas, metros), políticas de listas de precios mayoristas y estados estrictos de guías de traslado interno. Si estas reglas se acoplan a Entity Framework Core, controladores HTTP o librerías externas de terceros (como facturación SUNAT o pasarelas de pago), cualquier cambio tecnológico o tributario rompería la lógica del negocio.

#### Alternativas Consideradas y Análisis Comparativo
1. **Arquitectura Tradicional en N-Capas (UI -> BLL -> DAL):**
   - *Desventajas:* La capa de lógica de negocio (BLL) depende directamente de la capa de acceso a datos (DAL) y de las entidades del ORM. Las reglas de negocio quedan atadas al esquema de base de datos relacional y a Entity Framework.
2. **Clean Architecture / Hexagonal Architecture (Seleccionada):**
   - *Ventajas:* Inversión total de dependencias. El núcleo de dominio es el centro absoluto y no tiene dependencias de ningún framework, base de datos ni UI. La infraestructura depende del dominio a través de abstracciones (interfaces/puertos).

#### Decisión Tomada
Adoptar **Clean Architecture** estructurado en cuatro proyectos/capas concéntricas:
1. **Domain:** Entidades (`Articulo`, `Almacen`, `InventarioStock`, `PedidoWeb`, `GuiaTraslado`), Value Objects, Enums de estado y reglas de negocio puras.
2. **Application:** Casos de uso (Commands/Queries), validaciones de negocio, interfaces de repositorios (`IInventarioRepository`) y contratos de servicios externos (`IPagoGatewayPort`, `IFacturacionElectronicaPort`).
3. **Infrastructure:** Adaptadores de base de datos (PostgreSQL 16, EF Core 8 / Npgsql), servicios de hash Argon2id, clientes de pasarela de pago y generadores de payload JSON/XML para PSE/OSE.
4. **Presentation (WebApi):** Controladores REST de ASP.NET Core 8 segregados en superficies interna y pública.

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - Las reglas de inventario y precios pueden probarse al 100 % mediante pruebas unitarias puras sin necesidad de levantar bases de datos ni mocks complejos.
  - Facilidad para sustituir o actualizar tecnologías de infraestructura (por ejemplo, cambiar de proveedor de pasarela o actualizar el proveedor PSE de facturación) sin alterar una sola línea de código del dominio.
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* Mayor cantidad de clases e interfaces (mapeos DTO, contratos, clases de comando).
  - *Mitigación:* Se adoptan convenciones uniformes de nomenclatura y patrones estándar (Mediator con MediatR o dispatchers directos) para evitar sobrecarga cognitiva en el equipo.

---

### ADR-003: Estrategia de Concurrencia Pesimista Fina, Fórmulas Segregadas y TTL Dinámico de Reservas

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-01` (Concurrencia e Integridad Transaccional de Inventario Multicanal), `DA-02` (Prioridad POS)

#### Contexto y Planteamiento del Problema
Materiales ferreteros de alta demanda (fierro corrugado, cemento Portland) presentan alta rotación. Si un cliente en el mostrador físico y otro en el portal web intentan adquirir las últimas existencias disponibles de forma simultánea, un control de concurrencia optimista provocaría cancelaciones de venta en mostrador frente al cliente en caja, lo cual es inaceptable comercialmente. Asimismo, las reservas web no pueden congelar inventario indefinidamente si el cliente no paga.

#### Alternativas Consideradas y Análisis Comparativo
1. **Concurrencia Optimista (RowVersion / Timestamp):**
   - *Desventajas:* Si hay colisión, la última transacción en confirmar falla con excepción de concurrencia. En el mostrador de la ferretería, esto forzaría al cajero a reintentar y redisparar el proceso de cobro frente a un cliente presencial con mercadería ya cargada en la camioneta.
2. **Bloqueo Pesimista a Nivel de Tabla (`LOCK TABLE`):**
   - *Desventajas:* Serializa todas las ventas del almacén, provocando cuellos de botella severos y latencias de más de 10 segundos en las terminales de caja.
3. **Bloqueo Pesimista Fino de Fila (`SELECT ... FOR UPDATE`) con Fórmulas Segregadas y TTL (Seleccionada):**
   - *Ventajas:* Bloquea únicamente las filas de los productos involucrados en la venta por una fracción de milisegundo. Fórmula segregada:
     $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$
     El *Stock Comprometido* se incrementa en web mediante reservas dinámicas con tiempo de vida (TTL): 20 minutos para pasarela con tarjeta; 1 hora para subir constancia y hasta 4 horas para validación de tesorería en pagos manuales.

#### Decisión Tomada
Implementar la fórmula segregada $Disponible = Físico - Comprometido$. En checkout web y cierre de venta POS, se ejecuta un bloqueo pesimista ordenado (`ORDER BY producto_id ASC`) seguido de un `UPDATE` condicionado atómico:
```sql
UPDATE inventario_stock
SET stock_comprometido = stock_comprometido + :cantidad
WHERE almacen_id = :almacen_id 
  AND producto_id = :producto_id 
  AND (stock_fisico - stock_comprometido) >= :cantidad;
```
Un servicio en segundo plano (*BackgroundService* en ASP.NET Core) ejecuta una tarea idempotente cada minuto liberando el stock comprometido de reservas vencidas.

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - 0 sobreventas y 0 inventarios negativos garantizados matemáticamente en la base de datos relacional.
  - Eliminación absoluta de *deadlocks* (bloqueos mutuos) gracias a la adquisición ordenada y determinista de bloqueos por ID de producto.
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* Posible contención en filas de cemento y fierro durante campañas de alta demanda.
  - *Mitigación:* La transacción de cobro o reserva se mantiene ultra-corta (únicamente lectura ordenada, update de stock e inserción de detalle), delegando la generación de comprobantes y notificaciones a tareas asíncronas posteriores.

---

### ADR-004: Segregación de Conexiones en PgBouncer y Desacoplamiento de Lecturas Web (CQRS Ligero)

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-02` (Prioridad Transaccional POS), `DA-05` (Desacoplamiento Omnicanal)

#### Contexto y Planteamiento del Problema
Picos de concurrencia en el portal e-commerce (hasta 200 sesiones simultáneas y 50 solicitudes por segundo en catálogo) pueden saturar el número máximo de conexiones disponibles de PostgreSQL (`max_connections`), provocando el agotamiento de recursos (*connection pool starvation*) y degradando las terminales físicas de mostrador más allá de los 2 segundos tolerables (`RNF-PERF-001`).

#### Alternativas Consideradas y Análisis Comparativo
1. **Pool Único de Conexiones Compartido:**
   - *Desventajas:* Una ráfaga de tráfico web agota inmediatamente las conexiones disponibles en el pool de ADO.NET/Npgsql, dejando a las cajas físicas en espera de conexión (*connection timeout*).
2. **Réplica de Lectura Física de Base de Datos (Streaming Replication):**
   - *Ventajas:* Separa físicamente las lecturas web de las escrituras de mostrador.
   - *Desventajas:* Requiere un segundo servidor de base de datos con costos adicionales de licencia/hardware y gestión de retraso de replicación (*replication lag*), sobredimensionado para la escala inicial del proyecto.
3. **Pool Segregado en PgBouncer + Modelo de Lectura Desacoplado (Seleccionada):**
   - *Ventajas:* PgBouncer opera en modo transacción gestionando eficientemente cientos de clientes sobre un número controlado de conexiones reales a PostgreSQL. Se configuran dos pools virtuales con cuotas aisladas.

#### Decisión Tomada
1. Desplegar **PgBouncer** frente a PostgreSQL 16 con dos pools segregados:
   - `pool_pos`: Conexiones reservadas exclusivas para el canal presencial y operaciones internas (alta prioridad).
   - `pool_web`: Conexiones limitadas con política de contención para tráfico web.
2. Desacoplamiento de lecturas (CQRS ligero): El catálogo web consulta vistas optimizadas precalculadas con caché temporal (hasta 30 s de tolerancia en disponibilidad). Al momento del checkout, se salta la caché y se revalida atómicamente contra el stock real en una transacción corta.

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - La degradación del tiempo de cobro en mostrador se mantiene estrictamente $\le 10$ % incluso bajo el pico máximo de estrés web (`RNF-PERF-005`).
  - Cero incidentes de agotamiento de conexiones en las terminales de venta física.
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* El catálogo web puede mostrar existencias que cambian en los últimos 30 segundos.
  - *Mitigación:* Se notifica explícitamente en la interfaz web que la confirmación final de stock ocurre en el paso de checkout (`RF-WEB-005`).

---

### ADR-005: Dualidad de Dominios de Identidad, Autenticación Asimétrica RS256 y RBAC en Servidor

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-03` (Segregación de Identidad y Superficie de Seguridad)

#### Contexto y Planteamiento del Problema
El sistema convive con dos poblaciones de usuarios radicalmente distintas: personal interno de la ferretería (cajeros, almaceneros, despachadores, tesoreros) que ejecutan arqueos de caja y movimientos de kardex, y clientes externos (maestros de obra, contratistas, público general) que navegan el portal web desde Internet. Compartir el mismo modelo de sesión o tokens abre vulnerabilidades de escalada de privilegios y ataques de inyección.

#### Alternativas Consideradas y Análisis Comparativo
1. **Tabla Única de Usuarios y Tokens Simétricos (HS256):**
   - *Desventajas:* Mezclar clientes externos y cajeros en la misma tabla de credenciales eleva el riesgo de fuga de datos. El uso de tokens simétricos HS256 requiere compartir la misma clave secreta entre múltiples componentes, dificultando la rotación de claves y auditorías.
2. **Servicio Externo SaaS de Identidad (Auth0 / Firebase Auth):**
   - *Desventajas:* Costos mensuales recurrentes por usuario activo, dependencia de conectividad a la nube para autenticar terminales locales en red interna (LAN) si hay cortes esporádicos de Internet en Huamanga.
3. **Dualidad de Identidades Interna/Externa con JWT Asimétrico RS256 y Argon2id (Seleccionada):**
   - *Ventajas:* Almacenes lógicos segregados de credenciales. Cifrado de contraseñas con **Argon2id** ($m=65536, t=3, p=4$). Emisión de Access Tokens firmados con par de claves RSA 2048 (RS256).

#### Decisión Tomada
1. Implementar dos dominios de identidad:
   - **Dominio Interno:** Colaboradores con roles estrictos (`Cajero`, `Almacenero`, `Despachador`, `Administrador`). Tokens de vida corta (15 min) con claims de rol evaluados obligatoriamente en servidor (`[Authorize(Roles = "...")]`).
   - **Dominio Externo:** Clientes web (`Público General`, `Maestro de Obra`, `Contratista`). Tokens con audiencia restringida `Audience: SIGIC_WebClient`.
2. En el pipeline de ASP.NET Core, un middleware de validación perimetral bloquea con `HTTP 403 Forbidden` cualquier solicitud que presente un token de cliente web intentando invocar endpoints del espacio de nombres interno (`/api/internal/*`).

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - Cumplimiento de la Ley N.° 29733 de Protección de Datos Personales en Perú.
  - Aislamiento completo de la superficie de ataque interna frente a accesos públicos.
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* Gestión y rotación periódica de pares de claves criptográficas RSA.
  - *Mitigación:* Se almacena la clave privada en el almacén de secretos corporativo (*User Secrets* / variables protegidas de entorno en servidor) y la clave pública se distribuye a los validadores del middleware.

---

### ADR-006: Desacoplamiento Operativo de Pagos: Consola Backoffice Asíncrona y Adaptadores de Pasarela

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-04` (Desacoplamiento de Fronteras Externas), `DA-02` (Prioridad POS)

#### Contexto y Planteamiento del Problema
En el mercado ferretero regional de Ayacucho, más del 70 % de las compras electrónicas se cancelan mediante billeteras digitales (Yape, Plin) o transferencias bancarias directas (BCP, BBVA), donde el cliente adjunta una imagen o PDF del comprobante de abono (hasta 5 MB). Las entidades financieras locales no ofrecen webhooks bancarios abiertos para micro y pequeñas empresas. Si los cajeros o despachadores en mostrador tuvieran que inspeccionar imágenes bancarias mientras atienden a la fila de clientes presenciales, la atención en caja colapsaría.

#### Alternativas Consideradas y Análisis Comparativo
1. **Validación de Pago en Mostrador al Momento del Despacho:**
   - *Desventajas:* El despachador tendría que abrir PDFs de 5 MB, verificar cuentas de banco y validar códigos de operación en el mostrador frente al cliente, demorando más de 5 a 8 minutos por atención y colapsando el flujo presencial.
2. **Exigencia Exclusiva de Tarjeta de Crédito/Débito vía Pasarela Automatizada:**
   - *Desventajas:* Tasa de abandono de compra superior al 70 % debido a la alta preferencia del sector construcción regional por transferencias directas y billeteras digitales.
3. **Flujo Desacoplado Asíncrono con Consola de Tesorería en Backoffice (Seleccionada):**
   - *Ventajas:* Especialización de roles. Tesorería valida en una pantalla administrativa independiente el voucher y el código de operación. La pantalla de mostrador físico solo ve pedidos en estado `Listo para Retiro`.

#### Decisión Tomada
1. Diseñar el flujo de validación en dos fases asíncronas:
   - **Fase de Validación Financiera:** El cliente web sube el código de operación y voucher. El pedido ingresa en estado `Pendiente de Validación`. El Asistente de Tesorería o Administrador valida el abono en su consola de Backoffice (`RF-WEB-003`). Al confirmar, el pedido pasa a `En Preparación en Depósito` y se emite la guía de traslado al mostrador (`RF-INV-003`).
   - **Fase de Despacho Ágil en Mostrador:** La pantalla del despachador (`RF-POS-011`) solo lista pedidos en estado `Listo para Retiro`. El despacho se ejecuta en menos de 1 minuto mediante lectura de código QR/barras y cotejo de DNI del cliente, sin manipular comprobantes bancarios.
2. Para pagos con tarjeta en línea, se define el puerto `IPagoGatewayPort` con adaptadores concretos que reciben webhooks seguros e idempotentes.

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - La atención en mostrador no sufre interferencia por archivos pesados ni validaciones bancarias complejas.
  - Mitigación del fraude por duplicidad de vouchers gracias a la validación de unicidad diaria de códigos de operación bancaria.
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* Tiempo de espera del cliente web para la validación humana de su transferencia.
  - *Mitigación:* Se establece un SLA operativo con reservas dinámicas garantizadas de 4 horas dentro del horario comercial y notificaciones de estado en tiempo real (`RF-WEB-004`).

---

### ADR-007: Segregación de Frontends: SPA React 18 (POS Mostrador) y Next.js 14 SSR (Portal Web)

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-02` (Prioridad Transaccional POS), `DA-05` (Desacoplamiento Omnicanal)

#### Contexto y Planteamiento del Problema
Los requisitos de experiencia de usuario de los dos canales de venta de la ferretería son mutuamente excluyentes:
- **Caja y Mostrador:** Demanda una interfaz de escritorio ligera en navegador sobre red local (LAN), 100 % operable por atajos de teclado (`F1` a `F12`) y pistolas lectoras de códigos de barra, con tiempo de renderizado de fracciones de milisegundo y cero recargas de página.
- **Portal E-commerce:** Demanda indexación orgánica en motores de búsqueda (SEO) para materiales de construcción, pre-renderizado en servidor (SSR), optimización para teléfonos inteligentes en redes móviles 4G y diseño responsivo sin desbordamiento horizontal.

#### Alternativas Consideradas y Análisis Comparativo
1. **Frontend Único Responsivo en Next.js para Mostrador y Web:**
   - *Desventajas:* La sobrecarga del servidor Node.js y la hidratación de páginas introduce latencias innecesarias en la red local de mostrador, donde no se requiere SEO ni SSR, complicando la captura ágil por atajos de teclado.
2. **Aplicación de Escritorio Nativa (WPF / Electron) para Mostrador:**
   - *Desventajas:* Viola la restricción `RNF-USA-001` de cero instalación local y mantenimiento centralizado en navegadores web estándar (Chrome, Edge).
3. **Segregación de Frontends Especializados (SPA React 18 + Next.js 14 SSR) (Seleccionada):**
   - *Ventajas:* Cada cliente web se diseña a la medida de su caso de uso operacional consumiendo la misma API REST unificada en ASP.NET Core 8.

#### Decisión Tomada
Implementar dos aplicaciones frontend separadas:
1. **SPA React 18 (Módulo POS Mostrador e Intranet):** Enfocada exclusivamente en productividad del cajero y almacenero, manipulación ágil de teclado, transiciones instantáneas en cliente y consumo directo de la API interna.
2. **Next.js 14 con SSR (Portal Web E-commerce):** Enfocado en renderizado del lado del servidor para catálogo público, fichas técnicas de materiales con metadatos SEO y checkout dinámico para clientes externos.

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - Experiencia óptima y ergonomía de trabajo para el personal en mostrador sin demoras de renderizado.
  - Rendimiento móvil y posicionamiento en buscadores para el canal comercial digital.
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* Mantenimiento de dos bases de código de interfaz de usuario.
  - *Mitigación:* Se comparten librerías de tipos TypeScript y modelos DTO generados automáticamente a partir de la especificación OpenAPI/Swagger del backend.

---

### ADR-008: Aislamiento de Base de Datos Transaccional (OLTP) y Datamart Dimensional (OLAP) con ETL Asíncrono

* **Estado:** Aceptado
* **Fecha:** 2026-10-08
* **Drivers Asociados:** `DA-07` (Aislamiento Operacional vs. Analítico)

#### Contexto y Planteamiento del Problema
El cálculo de Kardex mensual valorizado bajo el método de Costo Promedio Ponderado sobre más de 50 000 movimientos de almacén, junto con reportes de márgenes brutos, rotación de familias de productos y comparativos de ventas entre canales (`RF-REP-001` a `RF-REP-007`), requiere agregaciones masivas con escaneos secuenciales de tablas históricas. Si estas consultas se ejecutan directamente sobre la base transaccional de producción, bloquean páginas de datos, saturan la memoria de trabajo (*work_mem*) y provocan retardos severos en las cajas de cobro.

#### Alternativas Consideradas y Análisis Comparativo
1. **Ejecución de Reportes Directamente sobre Tablas Transaccionales (`sigic_oltp`):**
   - *Desventajas:* Colapso del rendimiento de ventas físicas durante la generación de reportes gerenciales en horario de oficina. Viola el atributo `RNF-PERF-002` ($\le 10$ % de degradación máxima).
2. **Vistas Materializadas en la Misma Base de Datos OLTP:**
   - *Desventajas:* El comando `REFRESH MATERIALIZED VIEW` compite intensamente por I/O de disco y memoria contra las transacciones de ventas y reservas.
3. **Datamart Dimensional Segregado (`sigic_olap`) con Esquema Estrella y ETL Nocturno (Seleccionada):**
   - *Ventajas:* Aislamiento físico/lógico total. El motor transaccional OLTP mantiene su caché de memoria *shared_buffers* dedicada exclusivamente a ventas físicas y reservas web. Los reportes gerenciales se consultan sobre una tabla de hechos (`Fact_Ventas`) y dimensiones conformadas optimizadas para agregaciones analíticas sub-5 segundos (`RNF-PERF-003`).

#### Decisión Tomada
1. Crear dos esquemas de datos separados:
   - `sigic_oltp`: Esquema relacional transaccional normalizado en tercera forma normal (3NF) para operaciones en línea de Mostrador y Portal Web.
   - `sigic_olap`: Datamart analítico modelado en Esquema Estrella compuesto por `Fact_Ventas` y dimensiones (`Dim_Tiempo`, `Dim_Producto`, `Dim_Cliente`, `Dim_Canal`, `Dim_Almacen`).
2. Implementar un proceso ETL asíncrono (*BackgroundService* programado fuera del horario comercial, 23:00 h) que extrae los datos cerrados de la jornada, calcula los promedios ponderados y carga el Datamart.

#### Consecuencias y Mitigaciones
- **Consecuencias Positivas:**
  - Garantía de degradación menor al 10 % en mostrador físico durante consultas analíticas.
  - Tiempos de generación de reportes históricos de 24 meses inferiores a 5 segundos (p95).
- **Trade-offs / Mitigaciones:**
  - *Riesgo:* Los reportes gerenciales tienen un desfase temporal de hasta 24 horas (datos consolidados al cierre del día anterior).
  - *Mitigación:* Se provee en la capa transaccional un reporte ligero y limitado de balance de caja del turno actual en tiempo real (`RF-POS-009`, `RF-CAJ-001`), reservando el Datamart para la reportería histórica y estratégica.
