import os

HTML_OUTPUT = "/home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion/resumen-defensa-12slides.html"

slides_data = [
    {
        "num": "01",
        "title": "Contexto de Negocio y Problemática Operativa en Retail Ferretero",
        "image": "06-Contexto-Problematica-Negocio.png",
        "fase": "FASE 1: CONTEXTO Y DIAGNÓSTICO",
        "concepto": "Diagnóstico de misión crítica en retail ferretero de alta rotación (Ayacucho) con >12,000 SKUs activos, geometrías complejas y concurrencia omnicanal física (POS LAN) vs. digital (Web WAN) sobre inventario unificado.",
        "puntos_clave": [
            "<strong>Volumetría y Granularidad:</strong> Catálogo de ferretería pesada y pernería con atributos fraccionados (pulgadas, mm, cédulas) y unidades multiescala (unidad, metro, bolsa, caja master).",
            "<strong>Fricción Concurrente Omnicanal:</strong> Cuadrillas de obra compitiendo en caja física simultáneamente con contratistas y público en e-commerce por las mismas existencias.",
            "<strong>Riesgos Críticos:</strong> Desfases de catálogo (> 30s) provocan sobreventa crítica de materiales no disponibles; ráfagas web saturan PostgreSQL congelando terminales de mostrador (Connection Starvation); manipulación de arqueos de caja no ciegos."
        ],
        "formulas_metricas": [
            "<strong>Invariante Cardinal (RNF-CON-003):</strong> <code>Stock Disponible = Stock Físico - Stock Comprometido</code> (Tolerancia: 0 sobreventas, 0 saldos negativos).",
            "<strong>Catálogo Maestro:</strong> &gt; 12,000 SKUs activos con actualización de costo promedio ponderado (CPP).",
            "<strong>Desfase Máximo Web Permitido:</strong> &le; 30 segundos en sincronización asíncrona de catálogo."
        ],
        "speech": "Profesora, buenos días. SIGIC Ferretería v2.2 resuelve la problemática operativa de un retail ferretero con más de doce mil artículos activos en Ayacucho. El problema crítico radica en la colisión concurrente entre las cuadrillas de construcción que compran en el mostrador físico y los clientes que compran por el portal web. Sin un control riguroso de inventario comprometido y sin aislar las conexiones de base de datos, el tráfico web satura el servidor y genera sobreventas que paralizan los despachos en obra."
    },
    {
        "num": "02",
        "title": "Mapa de Actores, Segregación de Identidades y Sistemas Externos",
        "image": "07-Mapa-Actores-Fronteras.png",
        "fase": "FASE 1: ACTORES Y SEGREGACIÓN DE ROLES",
        "concepto": "Delimitación estricta de identidades entre colaboradores internos (RBAC sobre API Core) y clientes externos (Next.js SSR), reforzada mediante autenticación criptográfica asimétrica (JWT RS256) y segregación de privilegios.",
        "puntos_clave": [
            "<strong>Dominio Interno (Colaboradores):</strong> <code>ROLE_SUPERADMIN</code> (tarifas, catálogos, almacenes), <code>ROLE_CAJERO</code> (facturación por teclado F1-F12 y escáner sin ratón en 1024x768), <code>ROLE_ALMACENERO</code> (Kardex CPP, recepción, guías inter-almacén #GI-...), <code>ROLE_DESPACHADOR</code> (pantalla ligera RF-POS-011 para retiro web; aislado de dinero e inventario), <code>ROLE_TESORERIA</code> (auditoría de comprobantes bancarios).",
            "<strong>Dominio Externo:</strong> <code>Cliente Web</code> con tokens restringidos (<code>Audience: SIGIC_WebClient</code>) que bloquean cualquier acceso a endpoints internos (HTTP 403 Forbidden).",
            "<strong>Sistemas Externos de Frontera:</strong> Proveedor PSE/OSE SUNAT (asíncrono con IGV 18% y CDR), pasarela de pagos digitales PCI-DSS con firmas HMAC SHA-256, y validación de abonos bancarios Yape/Plin."
        ],
        "formulas_metricas": [
            "<strong>Firma Criptográfica:</strong> JWT asimétricos RS256 (claves RSA 2048-bit) + derivación Argon2id (m=65536, t=3, p=4).",
            "<strong>Aislamiento Operativo:</strong> El despachador físico NO manipula dinero ni modifica stock directamente.",
            "<strong>Límite de Carga de Vouchers:</strong> Máximo 5 MB por constancia bancaria (JPG, PNG, PDF)."
        ],
        "speech": "En este mapa delimitamos formalmente la frontera de identidades del sistema dividida en dos dominios estancos. En el dominio interno, cajeros, almaceneros y personal de tesorería operan bajo control de acceso basado en roles con tokens JWT firmados mediante RS256. En el dominio externo, los clientes web poseen credenciales desacopladas sin acceso a las APIs internas. Destaco que el despachador de tienda tiene una frontera estricta: entrega pedidos web cotejando el DNI sin tocar dinero ni modificar stock, eliminando riesgos de fraude."
    },
    {
        "num": "03",
        "title": "Atributos de Calidad Rectores y Métricas Cuantificables (ISO/IEC 25010)",
        "image": "08-Atributos-Calidad-Metricas.png",
        "fase": "FASE 1: ATRIBUTOS DE CALIDAD Y DRIVERS",
        "concepto": "Definición cuantitativa de los atributos de calidad rectores que gobiernan las decisiones de diseño arquitectónico y concurrencia bajo el estándar formal ISO/IEC 25010.",
        "puntos_clave": [
            "<strong>AC01 — Rendimiento Transaccional:</strong> Operación en ventanilla de alta velocidad protegida por conexiones dedicadas en PgBouncer y consultas optimizadas.",
            "<strong>AC02 — Consistencia Transaccional:</strong> Erradicación total de sobreventas bajo concurrencia extrema de múltiples clientes y cajeros sobre el mismo producto.",
            "<strong>AC03 — Trazabilidad y Auditoría:</strong> Registro inmutable de transacciones financieras y cierres de caja sin posibilidad de alteración retroactiva.",
            "<strong>Atributos Secundarios:</strong> RNF-USA-002 (Ergonomía POS en viewport 1024x768 px sin scroll horizontal) y RNF-DIS-001 (Alta disponibilidad >= 99.5%, RTO <= 60 min, RPO <= 15 min)."
        ],
        "formulas_metricas": [
            "<strong>Métrica AC01:</strong> Latencia en red LAN <code>P95 &lt; 250 ms</code> en API Core; captura de ítem &le; 1.0s; cobro total &le; 2.0s.",
            "<strong>Métrica AC02:</strong> <code>0 sobreventas</code> y <code>0 saldos negativos</code> comprobados bajo estrés con 100 hilos concurrentes.",
            "<strong>Métrica AC03:</strong> <code>100% de operaciones sensibles auditadas</code> en tablas Append-Only con DDL restrictivo."
        ],
        "speech": "Nuestra arquitectura se fundamenta en tres atributos de calidad rectores medibles bajo ISO 25010. El atributo AC01 garantiza que una venta en caja cierre en menos de dos segundos con latencia P95 inferior a 250 milisegundos gracias al pool dedicado de PgBouncer. El atributo AC02 erradica totalmente las sobreventas forzando la ecuación de stock disponible y aplicando bloqueos pesimistas ordenados en PostgreSQL. Por último, el atributo AC03 asegura la inmutabilidad contable mediante tablas append-only donde revocamos formalmente los comandos de modificación."
    },
    {
        "num": "04",
        "title": "Selección Arquitectónica: Monolito Modular vs. Microservicios",
        "image": "09-Decision-Monolito-Modular.png",
        "fase": "FASE 2: ESTILO ARQUITECTÓNICO Y TRADE-OFFS",
        "concepto": "Justificación rigurosa de ingeniería para la selección de un Monolito Modular con Clean Architecture concéntrica en ASP.NET Core 8 Web API frente al paradigma de microservicios distribuidos.",
        "puntos_clave": [
            "<strong>Trade-Off de Consistencia (AC02):</strong> Microservicios imponen consistencia eventual (Sagas) que genera ventanas de sobreventa crítica en materiales pesados. El Monolito Modular garantiza transacciones ACID inmediatas en PostgreSQL 16.",
            "<strong>Trade-Off de Latencia (AC01):</strong> Microservicios introducen 20-50 ms de sobrecarga por saltos de red HTTP/gRPC. El Monolito Modular resuelve la comunicación inter-módulo en memoria del proceso Kestrel en &lt; 1 ms.",
            "<strong>Trade-Off Operacional:</strong> Microservicios demandan clúster Kubernetes, service mesh y pipelines dispersos. El Monolito Modular opera con 1 solo pipeline CI/CD y 1 contenedor de despliegue.",
            "<strong>Regla Cardinal:</strong> Comunicación inter-módulo exclusivamente vía interfaces tipadas (<code>IInventarioReservaService</code>) y MediatR en memoria; prohibido el acceso directo a tablas de esquemas ajenos."
        ],
        "formulas_metricas": [
            "<strong>Latencia Inter-Módulo:</strong> <code>&lt; 1 ms</code> en memoria (vs. 20-50 ms en microservicios por salto de red).",
            "<strong>Auditoría Contable:</strong> Transactional Outbox y bitácora relacional unificada (<code>Audit_Log</code>, <code>caja_arqueo</code>) en la misma transacción ACID.",
            "<strong>Despliegue:</strong> 1 contenedor compilado en .NET 8 (Kestrel) con bajo consumo de memoria."
        ],
        "speech": "Profesora, evaluamos rigurosamente el trade-off entre microservicios y un monolito modular. Desestimamos los microservicios porque su consistencia eventual basada en Sagas introduce ventanas de vulnerabilidad donde un material pesado se sobrevende antes de propagar el evento, sumado a una latencia de red de 20 a 50 milisegundos que degrada la atención en caja. El Monolito Modular en ASP.NET Core 8 nos brinda transacciones ACID inmediatas en la misma base de datos con llamadas en memoria menores a un milisegundo, simplificando drásticamente el despliegue a un solo pipeline de integración continua."
    },
    {
        "num": "05",
        "title": "Segregación de Conexiones y Resiliencia con PgBouncer",
        "image": "10-PgBouncer-Segregacion-Pools.png",
        "fase": "FASE 2: RESILIENCIA E INFRAESTRUCTURA DE DATOS",
        "concepto": "Aislamiento físico y mitigación de inanición de conexiones (Connection Starvation) mediante PgBouncer 1.21 operando en modo asignación por transacción entre la red LAN de tienda y la red WAN de Internet.",
        "puntos_clave": [
            "<strong>Desacoplamiento Transaccional:</strong> PgBouncer asigna conexiones físicas al motor PostgreSQL 16 únicamente durante la duración de una transacción y las libera de inmediato (<code>pool_mode = transaction</code>).",
            "<strong>Pool POS (Alta Prioridad / LAN):</strong> 50 conexiones reservadas exclusivas para cajas de mostrador y terminales de almacén. Latencia de asignación &lt; 1 ms; cero colas de espera.",
            "<strong>Pool Web (Limitado / WAN):</strong> 30 conexiones máximas fijas con control de backpressure y timeout de espera en cola de 5 segundos.",
            "<strong>Comportamiento bajo Ataque DoS:</strong> Si miles de usuarios o bots saturan el portal web, el pool_web encola o rechaza solicitudes externas, pero las cajas físicas facturan con disponibilidad del 100% sin degradación.",
            "<strong>Aislamiento Analítico:</strong> Datamart en esquema estrella (<code>sigic_olap</code>) para Kardex y reportes contables, con cero impacto en la base transaccional (<code>sigic_oltp</code>)."
        ],
        "formulas_metricas": [
            "<strong>pool_pos:</strong> 50 conexiones reservadas LAN (prioridad absoluta, latencia &lt; 1 ms).",
            "<strong>pool_web:</strong> 30 conexiones máximas WAN (timeout de cola: 5s, backpressure estricto).",
            "<strong>Garantía de Cobro:</strong> &le; 2.0 s por ticket en caja aun con el e-commerce bajo sobrecarga masiva."
        ],
        "speech": "Para blindar la atención en tienda física contra el tráfico de Internet, interponemos PgBouncer operando en modo transacción con dos pools estrictamente aislados. El pool_pos reserva cincuenta conexiones exclusivas para las cajas de mostrador, garantizando acceso instantáneo a la base de datos con latencia sub-milisegundo. En contraste, el pool_web restringe el comercio electrónico a treinta conexiones máximas con control de backpressure; de este modo, aunque el sitio web experimente un pico masivo de visitas o un ataque DoS, las cajas físicas continúan facturando con absoluta fluidez."
    },
    {
        "num": "06",
        "title": "Protocolo de Checkout Atómico y Prevención Determinista de Deadlocks",
        "image": "05-Protocolo-Checkout-AntiDeadlock.png",
        "fase": "FASE 2: CONCURRENCIA Y PREVENCIÓN DE DEADLOCKS",
        "concepto": "Protocolo transaccional de 9 pasos para compras multilínea concurrentes con prevención matemática de interbloqueos (deadlocks) e incremento atómico de stock comprometido con TTL dinámico.",
        "puntos_clave": [
            "<strong>Prevención de Deadlocks (RF-WEB-005):</strong> Ordenamiento determinista en memoria <code>items.OrderBy(x => x.ProductoId).ToList()</code> antes de abrir transacción relacional. Al solicitar cerrojos en el mismo orden global, se eliminan los ciclos en el grafo de espera.",
            "<strong>Bloqueo Pesimista Atómico:</strong> Ejecución de <code>SELECT ... FOR UPDATE</code> ordenado por identificador de producto.",
            "<strong>Revalidación de Stock (RNF-CON-003):</strong> Predicado condicionado <code>WHERE (stock_fisico - stock_comprometido) >= @Cantidad</code>. Si no alcanza, emite <code>HTTP 409 Conflict</code> inmediato.",
            "<strong>TTL Dinámico de Reserva (RF-WEB-002):</strong> Pasarela de tarjetas: TTL exacto de 20 minutos; Pagos manuales (Yape/Plin): TTL de 240 minutos (4 h) con ventana máxima de 60 minutos para cargar voucher.",
            "<strong>Liberación Asíncrona Idempotente:</strong> Background Worker cada 60 segundos con <code>SELECT ... FOR UPDATE SKIP LOCKED</code> para liberar reservas vencidas sin contención."
        ],
        "formulas_metricas": [
            "<strong>Ordenamiento Determinista:</strong> <code>ORDER BY producto_id ASC</code> obligatorio antes del cerrojo pesimista.",
            "<strong>TTL Tarjeta:</strong> 20 min (improrrogable). | <strong>TTL Manual:</strong> 240 min (voucher en 60 min).",
            "<strong>Worker de Liberación:</strong> Cada 60 segundos con <code>SKIP LOCKED</code> (cero contención)."
        ],
        "speech": "Este diagrama representa el núcleo de concurrencia de nuestro sistema: el protocolo de checkout atómico. Cuando un cliente compra varillas y cemento al mismo tiempo que el cajero factura en ventanilla, el riesgo clásico en bases de datos relacionales es el interbloqueo o deadlock. En SIGIC lo eliminamos ordenando deterministamente los identificadores de producto en memoria antes de solicitar la transacción relacional en PostgreSQL. Luego ejecutamos un SELECT FOR UPDATE, revalidamos atómicamente el precio y el stock disponible, e incrementamos el stock comprometido con su respectivo tiempo de vida útil, liberando automáticamente el inventario mediante un worker en segundo plano si el pago no se concreta."
    },
    {
        "num": "07",
        "title": "Modelo C4 — Nivel 1: Diagrama de Contexto del Sistema",
        "image": "01-C1-Contexto-Sistema.png",
        "fase": "FASE 3: MODELO C4 — CONTEXTO (C1)",
        "concepto": "Vista de más alto nivel según el modelo C4 de Simon Brown, representando a SIGIC Ferretería como una caja negra delimitada en su ecosistema humano y tecnológico.",
        "puntos_clave": [
            "<strong>6 Actores Humanos Especializados:</strong> Superadministrador (políticas globales), Cajero (facturación rápida LAN), Almacenero (Kardex CPP y guías), Despachador (pantalla ligera RF-POS-011), Tesorería (auditoría financiera), Cliente Web (catálogo y compras en línea).",
            "<strong>Frontera Fiscal con SUNAT:</strong> Integración HTTPS/REST con Proveedor de Servicios Electrónicos (PSE/OSE) homologado para emisión de Facturas/Boletas XML con IGV al 18% y recepción de constancias CDR.",
            "<strong>Pasarela de Pagos Digitales:</strong> Procesamiento seguro PCI-DSS con webhooks firmados con HMAC SHA-256.",
            "<strong>Desacoplamiento Asíncrono:</strong> Las comunicaciones fiscales y bancarias operan de forma asíncrona; contingencias en SUNAT o bancos jamás detienen la emisión de comprobantes en mostrador físico."
        ],
        "formulas_metricas": [
            "<strong>Alcance:</strong> 6 actores humanos + 3 sistemas externos integrados.",
            "<strong>Normativa Fiscal:</strong> Facturación electrónica SUNAT con IGV 18% y contingencia asíncrona.",
            "<strong>Seguridad de Webhooks:</strong> Firmas criptográficas HMAC SHA-256 contra adulteraciones."
        ],
        "speech": "Descendiendo a la formalización arquitectónica bajo el modelo C4 de Simon Brown, en el Nivel 1 delimitamos el sistema como una caja negra dentro de su entorno comercial. Observen cómo SIGIC no solo interactúa con los seis actores humanos de la ferretería, sino que mantiene integraciones directas con sistemas externos como el proveedor PSE/OSE homologado por SUNAT para facturación electrónica y las pasarelas de pago con tarjeta. Toda la interacción fiscal con SUNAT se realiza de forma asíncrona para que una eventual lentitud en los servidores tributarios nunca detenga la entrega física de mercadería en tienda."
    },
    {
        "num": "08",
        "title": "Modelo C4 — Nivel 2: Diagrama de Contenedores y Persistencia",
        "image": "02-C2-Contenedores-Segregacion.png",
        "fase": "FASE 3: MODELO C4 — CONTENEDORES (C2)",
        "concepto": "Descomposición física de aplicaciones ejecutables, procesos de computación y subsistemas de persistencia con segregación estricta de responsabilidades.",
        "puntos_clave": [
            "<strong>Nginx 1.25 (Reverse Proxy):</strong> Punto de entrada único HTTPS (TLS 1.3), rate limiting y compresión.",
            "<strong>SPA POS Mostrador (React 18 / Vite / Tailwind):</strong> Aplicación cliente en red LAN, optimizada para 1024x768 px, 100% atajos de teclado sin mouse (RF-POS-003).",
            "<strong>Portal Web E-Commerce (Next.js 14 SSR):</strong> Catálogo desacoplado con desfase &le; 30s y checkout con revalidación atómica.",
            "<strong>Backend API Core (ASP.NET Core 8 Web API):</strong> Monolito modular Clean Architecture sobre runtime Kestrel.",
            "<strong>Background Workers (.NET 8):</strong> Expiración de reservas cada 60s y sincronización ETL nocturna hacia el Datamart.",
            "<strong>Persistencia Segregada:</strong> PgBouncer 1.21 intermediando hacia PostgreSQL 16 (<code>sigic_oltp</code>), réplica dimensional (<code>sigic_olap</code>) y Storage Privado Cifrado (MinIO) para comprobantes bancarios."
        ],
        "formulas_metricas": [
            "<strong>Protocolos:</strong> TLS 1.3 externo; TCP/IP binario en red LAN interna.",
            "<strong>Conexiones a BD:</strong> PgBouncer <code>pool_pos</code> (50) + <code>pool_web</code> (30).",
            "<strong>Storage Privado:</strong> Almacenamiento cifrado en reposo para vouchers &le; 5 MB."
        ],
        "speech": "En el Nivel 2 de contenedores mostramos la descomposición física y los procesos de despliegue de nuestra solución. Contamos con dos aplicaciones cliente completamente desacopladas: una SPA en React 18 optimizada para red LAN sin uso de ratón y un portal web en Next.js 14 con renderizado en servidor. Ambos frontends se comunican con el backend central en ASP.NET Core 8, el cual intermedia sus accesos a PostgreSQL mediante PgBouncer. Adicionalmente, incorporamos un storage privado cifrado para custodiar los vouchers bancarios de hasta 5 megabytes y un Datamart en esquema estrella que aísla totalmente los reportes gerenciales de la base transaccional."
    },
    {
        "num": "09",
        "title": "Modelo C4 — Nivel 3: Diagrama de Componentes de Checkout Concurrente",
        "image": "03-C3-Componentes-Checkout-Concurrente.png",
        "fase": "FASE 3: MODELO C4 — COMPONENTES (C3)",
        "concepto": "Estructura modular concéntrica de Clean Architecture al interior del contenedor Backend API Core en ASP.NET Core 8, aplicando el patrón CQRS con MediatR.",
        "puntos_clave": [
            "<strong>Capa Presentación (Controllers):</strong> <code>VentasPOSController</code> (cobros presenciales LAN), <code>CheckoutWebController</code> (filtro HTTP 404 estricto para artículos no web RF-INV-001) y <code>DespachoMostradorController</code> (RF-POS-011).",
            "<strong>Capa Aplicación (MediatR CQRS):</strong> <code>IniciarCheckoutCommandHandler</code> (orquesta ordenamiento y reserva), <code>CrearVentaPOSCommandHandler</code> (facturación y rebaja de stock) y <code>LiberarReservaVencidaCommandHandler</code>.",
            "<strong>Capa Dominio Puro:</strong> <code>InventarioDomainService</code> (calcula stock disponible y ordena en memoria), <code>ReservaStockService</code> (aplica TTL dinámico) y <code>MotorTarifasDomainService</code> (reglas multiescala).",
            "<strong>Capa Infraestructura (Persistencia Híbrida):</strong> EF Core 8 (<code>SIGICDbContext</code>) para mutaciones pesimistas ACID y <code>CatalogoDapperRepository</code> para lecturas ultrarrápidas de catálogo (latencia &lt; 15 ms)."
        ],
        "formulas_metricas": [
            "<strong>Persistencia Híbrida:</strong> EF Core 8 (escrituras ACID pesimistas) + Dapper (lecturas &lt; 15 ms).",
            "<strong>Desacoplamiento:</strong> Controladores sin acoplamiento a base de datos (todo vía comandos MediatR).",
            "<strong>Filtro de Catálogo Web:</strong> Retorno forzado de <code>HTTP 404</code> si <code>disponible_en_web == false</code>."
        ],
        "speech": "Al realizar un zoom en el Nivel 3 al interior del API Core, observamos la implementación estricta de Clean Architecture organizada en cuatro capas concéntricas. La capa de presentación expone controladores especializados para POS y Web, los cuales despachan comandos a través del mediador MediatR sin acoplarse directamente a la lógica de negocio. En el dominio puro, InventarioDomainService gobierna las reglas de inventario ordenando los identificadores antes del bloqueo pesimista. Finalmente, en la capa de persistencia combinamos EF Core 8 para escrituras transaccionales complejas y Dapper para lecturas de catálogo a velocidades sub-15 milisegundos."
    },
    {
        "num": "10",
        "title": "Modelo C4 — Nivel 4: Diagrama de Código y Clases del Dominio",
        "image": "04-C4-Clases-Dominio.png",
        "fase": "FASE 3: MODELO C4 — CÓDIGO Y CLASES (C4)",
        "concepto": "Diseño de Dominio Rico (Domain-Driven Design) que repudia modelos anémicos y encapsula comportamiento de negocio bajo el principio 'Tell, Don't Ask'.",
        "puntos_clave": [
            "<strong><code>Producto</code> (Aggregate Root):</strong> Encapsula atributos dimensionales (pulgadas, mm, calibres), clasificación jerárquica y el flag <code>DisponibleEnWeb</code>. Métodos de mutación: <code>ActualizarCostoPonderado(cant, costo)</code> y <code>ValidarMargenSeguridad(precio)</code>.",
            "<strong><code>InventarioStock</code>:</strong> Atributos inmutables <code>StockFisico</code> y <code>StockComprometido</code>. Invariante encapsulada: <code>StockDisponible() => StockFisico - StockComprometido</code>. Métodos de mutación: <code>BloquearParaReserva(cant)</code> y <code>LiberarReserva(cant)</code>.",
            "<strong><code>Venta</code> y <code>DetalleVenta</code>:</strong> Composición de comprobantes SUNAT con cálculo exacto de base imponible e IGV al 18%. Soporte para cobro mixto (efectivo + billeteras digitales + tarjetas).",
            "<strong><code>ArqueoCaja</code> (Append-Only):</strong> Encapsula el procedimiento de arqueo ciego sin revelar saldos teóricos al cajero. Desglose físico por denominaciones monetarias y cálculo automático: <code>MontoDiferencia => MontoDeclarado - MontoCalculadoSistema</code>."
        ],
        "formulas_metricas": [
            "<strong>Principio DDD:</strong> 'Tell, Don't Ask' — Prohibidos setters públicos en propiedades de stock.",
            "<strong>Fórmula CPP:</strong> <code>NuevoCPP = ((StockActual * CPPActual) + (CantIngreso * CostoIngreso)) / StockTotal</code>.",
            "<strong>Conciliación de Caja:</strong> <code>Diferencia = EfectivoDeclarado - SaldoCalculado</code>."
        ],
        "speech": "Llegando al Nivel 4 de código, presentamos las cuatro entidades de dominio medulares exigidas por la especificación: Producto, Venta, DetalleVenta y ArqueoCaja. En SIGIC repudiamos el modelo anémico: las clases encapsulan su propio comportamiento bajo el principio 'Tell, Don't Ask'. Por ejemplo, la entidad InventarioStock protege la disponibilidad impidiendo que el stock comprometido supere al stock físico. Asimismo, la clase ArqueoCaja materializa nuestra política de arqueo ciego calculando discrepancias contables de forma automática, garantizando que el cajero no pueda alterar la información histórica de caja."
    },
    {
        "num": "11",
        "title": "Seguridad Transaccional: Arqueo Ciego y Custodia de Vouchers",
        "image": "11-Seguridad-Arqueo-Ciego.png",
        "fase": "FASE 4: SEGURIDAD, AUDITORÍA Y COMPLIANCE",
        "concepto": "Mecanismos de defensa en profundidad para blindar el capital de la empresa contra fraudes internos en cierres de turno y proteger datos financieros bajo la Ley N.° 29733.",
        "puntos_clave": [
            "<strong>Procedimiento de Arqueo Ciego (RF-CAJ-002 / AC03):</strong> Declaración obligatoria de efectivo físico por denominaciones (billetes de 200, 100, 50, 20, 10; monedas de 5, 2, 1, 0.50, 0.20, 0.10). El cajero desconoce el saldo teórico calculado por el sistema antes del cierre, eliminando encubrimiento de faltantes.",
            "<strong>Triple Conciliación:</strong> 1. Efectivo físico contado vs. 2. Saldo de ventas del sistema vs. 3. Pasarelas electrónicas de tarjeta y billeteras.",
            "<strong>Inmutabilidad Append-Only en PostgreSQL 16:</strong> Tablas <code>caja_arqueo</code>, <code>caja_arqueo_detalle</code> y <code>Audit_Log</code> con prohibición DDL explícita: <code>REVOKE UPDATE, DELETE ON caja_arqueo, caja_arqueo_detalle FROM rol_cajero;</code>.",
            "<strong>Custodia de Vouchers Bancarios (RF-WEB-003 / Ley 29733):</strong> Comprobantes de hasta 5 MB almacenados en Storage Privado cifrado (MinIO); cero exposición pública; visualización exclusiva para Tesorería mediante URLs pre-firmadas temporales de 15 minutos.",
            "<strong>Autenticación Criptográfica:</strong> Tokens JWT firmados con RS256 (claves RSA de 2048 bits) y hashes de contraseñas con Argon2id."
        ],
        "formulas_metricas": [
            "<strong>DDL Anti-Fraude:</strong> <code>REVOKE UPDATE, DELETE ON caja_arqueo, caja_arqueo_detalle FROM rol_cajero;</code>",
            "<strong>Expiración Pre-Firmada:</strong> 15 minutos máximo por URL de comprobante bancario para Tesorería.",
            "<strong>Parámetros Argon2id:</strong> <code>m=65536 (64 MB), t=3 iteraciones, p=4 paralelismo</code>."
        ],
        "speech": "En esta lámina exponemos las defensas de seguridad transaccional que protegen los activos de la empresa. Implementamos el procedimiento de arqueo ciego donde el cajero debe declarar el efectivo físico desglosando billetes y monedas sin conocer el saldo calculado por el sistema, lo que elimina cualquier posibilidad de encubrir faltantes. Para garantizar el no repudio legal, revocamos formalmente los permisos de modificación y borrado en PostgreSQL mediante sentencias DDL. Además, custodiamos los comprobantes bancarios de los clientes en un storage privado cifrado bajo la Ley 29733, permitiendo su visualización únicamente al personal de tesorería a través de URLs temporales pre-firmadas."
    },
    {
        "num": "12",
        "title": "Conclusiones Técnicas, Certificación SDD y Valor de Negocio",
        "image": "12-Conclusiones-Certificacion-SDD.png",
        "fase": "FASE 5: CONCLUSIONES Y CERTIFICACIÓN SDD",
        "concepto": "Balance integral de resultados de ingeniería que demuestran el cumplimiento riguroso de la especificación SDD y la generación de valor tangible para la empresa comercial.",
        "puntos_clave": [
            "<strong>Metodología Spec-Driven Development (SDD):</strong> 19 documentos técnicos que constituyen la Única Fuente de Verdad, auditados y certificados por el servidor MCP Guardian (<code>sigic-sdd-guardian</code>).",
            "<strong>Logros de Concurrencia Comprobados:</strong> 0% de sobreventas bajo pruebas de carga con 100 hilos concurrentes; erradicación matemática de interbloqueos (deadlocks) por ordenamiento determinista.",
            "<strong>Resiliencia de Infraestructura:</strong> Separación física de pools en PgBouncer garantizando mostrador LAN ininterrumpido (P95 &lt; 250 ms) aun frente a degradaciones severas en el canal web.",
            "<strong>Cumplimiento Tributario y Legal:</strong> Facturación electrónica oficial homologada con SUNAT (PSE/OSE, XML, CDR, IGV 18%) y protección de datos conforme a la Ley N.° 29733.",
            "<strong>Veredicto Académico:</strong> Solución formalizada y aprobada conforme a los más altos estándares de la asignatura IS-488 Arquitectura de Software (UNSCH)."
        ],
        "formulas_metricas": [
            "<strong>Certificación SDD:</strong> 19 documentos técnicos completados y auditados al 100%.",
            "<strong>Tasa de Sobreventa:</strong> <code>0.00%</code> bajo estrés de concurrencia en simulación.",
            "<strong>Cumplimiento Normativo:</strong> SUNAT (Facturación electrónica) + Ley N.° 29733 (Datos personales)."
        ],
        "speech": "Para concluir, SIGIC Ferretería v2.2 demuestra cómo una metodología guiada estrictamente por especificaciones resuelve de raíz los problemas críticos de un negocio comercial complejo. Hemos garantizado cero sobreventas y cero interbloqueos en transacciones concurrentes, blindando el mostrador físico mediante la segregación de conexiones en PgBouncer. Toda la solución cumple rigurosamente con la normativa fiscal de SUNAT y con las directivas de seguridad informática, respaldada por un modelo C4 completo que formaliza la arquitectura requerida en la asignatura IS-488. Muchas gracias por su atención."
    }
]

banco_preguntas = [
    {
        "pregunta": "¿Por qué eligieron un Monolito Modular en lugar de Microservicios?",
        "respuesta": "Para evitar la <strong>consistencia eventual</strong> que introducen los microservicios con patrones Sagas o Two-Phase Commit en un negocio de materiales pesados de construcción donde la sobreventa es crítica e irreparable. El Monolito Modular garantiza <strong>transacciones ACID inmediatas</strong> sobre una instancia unificada de PostgreSQL 16 y latencias inter-módulo intra-memoria sub-milisegundo (<strong>&lt; 1 ms</strong> en Kestrel) necesarias para cumplir la métrica P95 &lt; 250 ms en caja física, simplificando además la operación a un solo pipeline de CI/CD."
    },
    {
        "pregunta": "¿Cómo garantizan que un ataque DoS al e-commerce no detenga las terminales de caja física?",
        "respuesta": "Mediante <strong>PgBouncer 1.21 configurado en modo asignación por transacción</strong> con dos pools estrictamente segregados: <code>pool_pos</code> reserva <strong>50 conexiones exclusivas</strong> y de alta prioridad para la red LAN de tienda con latencia sub-milisegundo (&lt; 1 ms) y cero colas de espera; mientras que <code>pool_web</code> restringe el tráfico WAN público a <strong>30 conexiones máximas fijas</strong> con control de backpressure y timeout de 5 segundos. Si el portal web colapsa o sufre un ataque DoS masivo, el pool web encola o rechaza el tráfico externo, pero el pool de mostrador permanece inmune."
    },
    {
        "pregunta": "¿Qué ocurre si dos clientes intentan comprar la última bolsa de cemento simultáneamente?",
        "respuesta": "El protocolo de checkout aplica un <strong>ordenamiento determinista en memoria</strong> (<code>items.OrderBy(x => x.ProductoId)</code>) y ejecuta un <strong>bloqueo pesimista de fila</strong> (<code>SELECT ... FOR UPDATE</code>) en PostgreSQL 16. La primera transacción en llegar adquiere el cerrojo pesimista; la segunda transacción se bloquea en espera. Al confirmarse la primera compra o reserva, la segunda despierta y revalida atómicamente el predicado condicionado: <code>WHERE (stock_fisico - stock_comprometido) >= @Cantidad</code>. Al detectar que el stock disponible es cero, la transacción aborta de inmediato y retorna <code>HTTP 409 Conflict</code>, garantizando cero sobreventas."
    },
    {
        "pregunta": "¿Por qué el Arqueo de Caja se define como Append-Only y cómo se implementa a nivel de motor de datos?",
        "respuesta": "Para dar cumplimiento irrestricto a los atributos <code>AC03</code> y <code>RNF-SEG-004</code> de auditoría y no repudio contable. Se implementa en PostgreSQL 16 mediante restricción DDL explícita: <code>REVOKE UPDATE, DELETE ON caja_arqueo, caja_arqueo_detalle FROM rol_cajero;</code>. De este modo, las tablas de arqueo son de <strong>inserción exclusiva (Append-Only)</strong>: una vez que el cajero declara su conteo físico a ciegas (desglosado en billetes y monedas), ni el propio cajero ni administradores pueden alterar o eliminar el registro histórico para encubrir faltantes de dinero."
    },
    {
        "pregunta": "¿Dónde y cómo se protegen los comprobantes de pago de los clientes según la Ley N.° 29733?",
        "respuesta": "Los comprobantes de abono bancario (vouchers Yape, Plin y transferencias de hasta 5 MB) se custodian en un <strong>Storage Privado cifrado</strong> (MinIO o sistema de archivos local seguro). Se prohíbe taxativamente la exposición de archivos bajo rutas o URLs web públicas. El personal de Tesorería es el único autorizado para visualizarlos mediante <strong>URLs pre-firmadas temporales con vigencia de 15 minutos</strong>, revocando el acceso automáticamente y protegiendo el secreto bancario y la información personal del usuario."
    }
]

html_content = f"""<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="UTF-8">
  <title>SIGIC Ferretería v2.2 — Resumen Técnico para Defensa de Arquitectura</title>
  <style>
    @page {{
      size: A4 portrait;
      margin: 8mm 12mm 8mm 12mm;
    }}
    * {{
      box-sizing: border-box;
      margin: 0;
      padding: 0;
    }}
    body {{
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      color: #1e293b;
      background-color: #ffffff;
      line-height: 1.3;
      font-size: 8.2pt;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }}

    .page-break {{
      page-break-before: always;
      break-before: always;
    }}

    /* PAGE 1: COVER & INDEX */
    .cover-container {{
      page-break-after: always;
      break-after: always;
    }}
    .doc-header {{
      background: linear-gradient(135deg, #0b0f19 0%, #111827 100%);
      color: #ffffff;
      padding: 14px 18px;
      border-radius: 6px;
      border-left: 5px solid #10b981;
      margin-bottom: 10px;
    }}
    .doc-header h1 {{
      font-size: 16pt;
      font-weight: 800;
      letter-spacing: -0.5px;
      color: #ffffff;
      margin-bottom: 3px;
    }}
    .doc-header .subtitle {{
      font-size: 9.5pt;
      color: #34d399;
      font-weight: 600;
      margin-bottom: 8px;
    }}
    .doc-header .meta-bar {{
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 6px;
      font-size: 7.8pt;
      color: #cbd5e1;
      border-top: 1px solid #1e293b;
      padding-top: 8px;
    }}
    .meta-bar span strong {{
      color: #ffffff;
    }}

    .executive-summary-card {{
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      padding: 10px 14px;
      margin-bottom: 10px;
    }}
    .executive-summary-card h3 {{
      font-size: 9.5pt;
      color: #0f172a;
      margin-bottom: 4px;
      font-weight: 700;
      border-bottom: 2px solid #10b981;
      padding-bottom: 2px;
      display: inline-block;
    }}
    .executive-summary-card p {{
      color: #334155;
      font-size: 8pt;
      margin-bottom: 4px;
      line-height: 1.35;
    }}

    .index-table {{
      width: 100%;
      border-collapse: collapse;
      font-size: 7.4pt;
    }}
    .index-table th {{
      background: #0f172a;
      color: #ffffff;
      text-align: left;
      padding: 4px 8px;
      font-weight: 700;
      border: 1px solid #1e293b;
    }}
    .index-table td {{
      padding: 3px 8px;
      border: 1px solid #e2e8f0;
      color: #334155;
    }}
    .index-table tr:nth-child(even) {{
      background: #f8fafc;
    }}
    .index-table .badge-cell {{
      font-weight: 700;
      color: #059669;
    }}

    /* SLIDE CARD PAGE (1 Page per Slide) */
    .slide-page {{
      page-break-before: always;
      break-before: always;
      display: flex;
      flex-direction: column;
    }}

    .slide-card-header {{
      background: #0f172a;
      color: #ffffff;
      padding: 6px 12px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-radius: 6px 6px 0 0;
      border-bottom: 3px solid #10b981;
    }}
    .slide-card-header .slide-tag {{
      background: #064e3b;
      color: #34d399;
      font-size: 7pt;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      border: 1px solid #10b981;
    }}
    .slide-card-header h3 {{
      font-size: 10pt;
      font-weight: 700;
      color: #ffffff;
      margin-left: 8px;
      flex-grow: 1;
    }}

    .slide-body {{
      border: 1px solid #e2e8f0;
      border-top: none;
      border-radius: 0 0 6px 6px;
      padding: 8px 12px;
      background: #ffffff;
    }}

    /* Diagram Image Preview */
    .diagram-wrapper {{
      text-align: center;
      background: #0b0f19;
      border-radius: 6px;
      padding: 3px;
      border: 1px solid #1e293b;
      margin-bottom: 6px;
    }}
    .diagram-wrapper img {{
      max-width: 100%;
      height: auto;
      max-height: 185px;
      border-radius: 4px;
      display: block;
      margin: 0 auto;
    }}
    .diagram-caption {{
      font-size: 6.8pt;
      color: #94a3b8;
      margin-top: 2px;
      font-family: monospace;
    }}

    .concept-box {{
      background: #f8fafc;
      border-left: 3px solid #3b82f6;
      padding: 5px 8px;
      font-size: 7.8pt;
      color: #334155;
      border-radius: 0 4px 4px 0;
      margin-bottom: 5px;
      line-height: 1.3;
    }}
    .concept-box strong {{
      color: #1e3a8a;
    }}

    .points-box {{
      font-size: 7.7pt;
      margin-bottom: 5px;
    }}
    .points-box ul {{
      list-style-type: none;
      padding-left: 0;
    }}
    .points-box li {{
      position: relative;
      padding-left: 11px;
      margin-bottom: 2px;
      color: #334155;
      line-height: 1.3;
    }}
    .points-box li::before {{
      content: "▪";
      position: absolute;
      left: 0;
      color: #10b981;
      font-size: 8pt;
    }}

    .formulas-box {{
      background: #ecfdf5;
      border: 1px solid #a7f3d0;
      border-radius: 5px;
      padding: 5px 8px;
      font-size: 7.6pt;
      color: #065f46;
      margin-bottom: 5px;
      line-height: 1.3;
    }}
    .formulas-box .box-title {{
      font-size: 7pt;
      font-weight: 700;
      text-transform: uppercase;
      color: #047857;
      margin-bottom: 2px;
      display: block;
      letter-spacing: 0.5px;
    }}
    .formulas-box code {{
      background: #ffffff;
      padding: 1px 3px;
      border-radius: 3px;
      border: 1px solid #6ee7b7;
      font-family: "SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace;
      font-size: 7.2pt;
      color: #065f46;
      font-weight: 600;
    }}

    /* Speech Box */
    .speech-box {{
      background: #fffbeb;
      border: 1px solid #fde68a;
      border-left: 4px solid #f59e0b;
      border-radius: 0 5px 5px 0;
      padding: 5px 8px;
      font-size: 7.6pt;
      color: #78350f;
    }}
    .speech-box .speech-title {{
      font-size: 7pt;
      font-weight: 700;
      text-transform: uppercase;
      color: #b45309;
      margin-bottom: 2px;
      display: block;
      letter-spacing: 0.5px;
    }}
    .speech-box p {{
      font-style: italic;
      margin: 0;
      line-height: 1.3;
    }}

    /* Q&A Section */
    .qa-card {{
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      padding: 8px 12px;
      margin-bottom: 8px;
      page-break-inside: avoid;
      break-inside: avoid;
    }}
    .qa-card .question {{
      font-size: 8.5pt;
      font-weight: 700;
      color: #0f172a;
      margin-bottom: 4px;
      display: flex;
      align-items: baseline;
    }}
    .qa-card .question-badge {{
      background: #10b981;
      color: #ffffff;
      font-size: 6.8pt;
      padding: 1px 5px;
      border-radius: 3px;
      margin-right: 6px;
      font-weight: 800;
    }}
    .qa-card .answer {{
      font-size: 7.8pt;
      color: #334155;
      line-height: 1.35;
      padding-left: 24px;
    }}
    .qa-card .answer strong {{
      color: #0f172a;
    }}
    .qa-card .answer code {{
      background: #f1f5f9;
      padding: 1px 3px;
      border-radius: 3px;
      font-family: monospace;
      color: #0369a1;
      font-size: 7.2pt;
    }}

    /* Footer */
    .doc-footer {{
      margin-top: 6px;
      border-top: 1px solid #cbd5e1;
      padding-top: 4px;
      font-size: 7pt;
      color: #64748b;
      display: flex;
      justify-content: space-between;
    }}
  </style>
</head>
<body>

  <!-- PAGE 1: COVER & OVERVIEW -->
  <div class="cover-container">
    <div class="doc-header">
      <h1>SIGIC Ferretería v2.2 — Resumen Técnico para Defensa de Tesis / Examen</h1>
      <div class="subtitle">Guía Maestra de Repaso Rápido de las 12 Láminas de Arquitectura (IS-488 UNSCH)</div>
      <div class="meta-bar">
        <span><strong>Asignatura:</strong> IS-488 Arquitectura de Software</span>
        <span><strong>Universidad:</strong> Universidad Nacional de San Cristóbal de Huamanga (UNSCH)</span>
        <span><strong>Autor:</strong> Crhistian Emanuel Flores Marquina</span>
        <span><strong>Metodología:</strong> Spec-Driven Development (SDD)</span>
        <span><strong>Tecnologías:</strong> ASP.NET Core 8 • PostgreSQL 16 • PgBouncer • React 18 • Next.js 14</span>
        <span><strong>Fuente de Verdad:</strong> 19 Documentos Validados en Sistema</span>
      </div>
    </div>

    <div class="executive-summary-card">
      <h3>Resumen Ejecutivo de la Arquitectura SIGIC v2.2</h3>
      <p>
        <strong>SIGIC Ferretería v2.2</strong> implementa una solución de misión crítica para el comercio minorista ferretero de alta rotación en Ayacucho. El sistema resuelve el conflicto concurrente entre el <strong>Mostrador Físico POS</strong> (red LAN) y el <strong>Portal Web E-Commerce</strong> (red WAN) que compiten simultáneamente por más de 12,000 SKUs de materiales de construcción.
      </p>
      <p>
        Bajo la metodología <strong>Spec-Driven Development (SDD)</strong>, se seleccionó un <strong>Monolito Modular con Clean Architecture</strong> sobre ASP.NET Core 8 Web API, respaldado por <strong>PostgreSQL 16</strong> e intermediado por <strong>PgBouncer</strong> en modo transacción con dos pools físicamente aislados (<code>pool_pos</code> con 50 conexiones LAN reservadas y <code>pool_web</code> con 30 conexiones WAN limitadas). La consistencia de inventario se blinda con la invariante cardinal <code>Stock Disponible = Stock Físico - Stock Comprometido</code>, ordenamiento determinista anti-deadlock (<code>OrderBy(ProductoId)</code>) y arqueos ciegos inmutables en base de datos.
      </p>
    </div>

    <div>
      <h3 style="font-size: 9pt; color: #0f172a; margin-bottom: 4px; font-weight: 700;">Índice Maestro de las 12 Láminas Técnicas</h3>
      <table class="index-table">
        <thead>
          <tr>
            <th style="width: 7%;">Lám.</th>
            <th style="width: 20%;">Fase Metodológica</th>
            <th style="width: 43%;">Título Técnico de la Lámina</th>
            <th style="width: 30%;">Recurso Gráfico PNG</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td class="badge-cell">01</td>
            <td>Contexto</td>
            <td>Contexto de Negocio y Problemática en Retail Ferretero</td>
            <td><code>06-Contexto-Problematica-Negocio.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">02</td>
            <td>Actores</td>
            <td>Mapa de Actores, Segregación de Identidades y Sistemas Externos</td>
            <td><code>07-Mapa-Actores-Fronteras.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">03</td>
            <td>Atributos Calidad</td>
            <td>Atributos de Calidad Rectores y Métricas ISO/IEC 25010</td>
            <td><code>08-Atributos-Calidad-Metricas.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">04</td>
            <td>Monolito Modular</td>
            <td>Selección Arquitectónica: Monolito Modular vs Microservicios</td>
            <td><code>09-Decision-Monolito-Modular.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">05</td>
            <td>PgBouncer</td>
            <td>Segregación de Conexiones y Resiliencia con PgBouncer</td>
            <td><code>10-PgBouncer-Segregacion-Pools.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">06</td>
            <td>Anti-Deadlock</td>
            <td>Protocolo de Checkout Atómico y Prevención de Deadlocks</td>
            <td><code>05-Protocolo-Checkout-AntiDeadlock.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">07</td>
            <td>C4: Contexto (C1)</td>
            <td>Modelo C4 — Nivel 1: Diagrama de Contexto del Sistema</td>
            <td><code>01-C1-Contexto-Sistema.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">08</td>
            <td>C4: Contenedores (C2)</td>
            <td>Modelo C4 — Nivel 2: Diagrama de Contenedores y Persistencia</td>
            <td><code>02-C2-Contenedores-Segregacion.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">09</td>
            <td>C4: Componentes (C3)</td>
            <td>Modelo C4 — Nivel 3: Diagrama de Componentes de Checkout</td>
            <td><code>03-C3-Componentes-Checkout-Concurrente.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">10</td>
            <td>C4: Clases (C4)</td>
            <td>Modelo C4 — Nivel 4: Diagrama de Código y Clases del Dominio</td>
            <td><code>04-C4-Clases-Dominio.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">11</td>
            <td>Seguridad Transaccional</td>
            <td>Seguridad Transaccional: Arqueo Ciego y Custodia de Vouchers</td>
            <td><code>11-Seguridad-Arqueo-Ciego.png</code></td>
          </tr>
          <tr>
            <td class="badge-cell">12</td>
            <td>Conclusiones SDD</td>
            <td>Conclusiones Técnicas, Certificación SDD y Valor de Negocio</td>
            <td><code>12-Conclusiones-Certificacion-SDD.png</code></td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="doc-footer">
      <span>Crhistian Emanuel Flores Marquina • IS-488 UNSCH</span>
      <span>SIGIC Ferretería v2.2 • Hoja de Carátula y Resumen Ejecutivo</span>
    </div>
  </div>
"""

# PAGES 2 TO 13: THE 12 SLIDES (Each strictly 1 page)
for s in slides_data:
    html_content += f"""
  <!-- SLIDE {s['num']} -->
  <div class="slide-page">
    <div class="slide-card-header">
      <span class="slide-tag">{s['fase']} | LÁMINA {s['num']}</span>
      <h3>{s['title']}</h3>
    </div>
    <div class="slide-body">
      <!-- Diagram Image -->
      <div class="diagram-wrapper">
        <img src="{s['image']}" alt="Diagrama Lámina {s['num']}">
        <div class="diagram-caption">Recurso Gráfico: recursos-exposicion/{s['image']} (Renderizado 1080p, 16:9)</div>
      </div>

      <!-- Details -->
      <div class="concept-box">
        <strong>Concepto Central:</strong> {s['concepto']}
      </div>

      <div class="points-box">
        <ul>
"""
    for pt in s['puntos_clave']:
        html_content += f"          <li>{pt}</li>\n"

    html_content += f"""        </ul>
      </div>

      <div class="formulas-box">
        <span class="box-title">★ Fórmulas Cardinales y Métricas P95:</span>
"""
    for f in s['formulas_metricas']:
        html_content += f"        <div>▪ {f}</div>\n"

    html_content += f"""      </div>

      <div class="speech-box">
        <span class="speech-title">🎙️ Guion Verbal de 30 Segundos (Respuesta Clave ante la Docente):</span>
        <p>"{s['speech']}"</p>
      </div>

      <div class="doc-footer">
        <span>Crhistian Emanuel Flores Marquina • IS-488 UNSCH</span>
        <span>Lámina {s['num']}: {s['title'][:45]}...</span>
      </div>
    </div>
  </div>
"""

# PAGE 14: BANCO DE PREGUNTAS (Page 14)
html_content += """
  <!-- BANCO DE PREGUNTAS (PAGE 14) -->
  <div class="slide-page">
    <div class="slide-card-header">
      <span class="slide-tag">FASE FINAL | DEFENSA ANTE EL JURADO</span>
      <h3>Banco de Respuestas Rápidas para la Evaluación Oral (IS-488 UNSCH)</h3>
    </div>
    <div class="slide-body">
      <p style="font-size: 8pt; color: #64748b; margin-bottom: 6px;">
        Argumentación técnica contundente para las preguntas de alta exigencia formuladas por el jurado evaluador:
      </p>
"""

for i, q in enumerate(banco_preguntas, 1):
    html_content += f"""
      <div class="qa-card">
        <div class="question">
          <span class="question-badge">PREGUNTA {i:02d}</span>
          <span>{q['pregunta']}</span>
        </div>
        <div class="answer">
          {q['respuesta']}
        </div>
      </div>
"""

html_content += """
      <div class="doc-footer">
        <span>Crhistian Emanuel Flores Marquina • IS-488 UNSCH</span>
        <span>SIGIC Ferretería v2.2 • Banco de Respuestas Rápidas para la Defensa</span>
      </div>
    </div>
  </div>

</body>
</html>
"""

with open(HTML_OUTPUT, "w", encoding="utf-8") as f:
    f.write(html_content)

print(f"HTML regenerated successfully at: {HTML_OUTPUT}")
