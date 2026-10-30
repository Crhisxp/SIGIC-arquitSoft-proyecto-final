import os
import re
import pptx
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN
from pptx.enum.shapes import MSO_SHAPE

OUTPUT_PPTX = "/home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/presentacion-sigic-v2.2.pptx"
RECURSOS_DIR = "/home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/recursos-exposicion"

# Color Palette
BG_COLOR = RGBColor(11, 15, 25)         # #0b0f19
CARD_BG = RGBColor(17, 24, 39)          # #111827
CARD_BORDER = RGBColor(31, 41, 55)      # #1f2937
EMERALD = RGBColor(16, 185, 129)        # #10b981
EMERALD_LIGHT = RGBColor(52, 211, 153)  # #34d399
WHITE = RGBColor(255, 255, 255)         # #ffffff
TEXT_MAIN = RGBColor(226, 232, 240)     # #e2e8f0
TEXT_MUTED = RGBColor(148, 163, 184)    # #94a3b8
CODE_SKY = RGBColor(56, 189, 248)       # #38bdf8
CALLOUT_BG = RGBColor(12, 31, 30)       # #0c1f1e
CALLOUT_BORDER = RGBColor(16, 185, 129) # #10b981
CALLOUT_TEXT = RGBColor(240, 253, 244)   # #f0fdf4
FOOTER_TEXT = RGBColor(100, 116, 139)   # #64748b

slides_data = [
    {
        "num": 1,
        "badge": "LÁMINA 01 | CONTEXTO DE NEGOCIO (IS-488 UNSCH)",
        "title": "Contexto de Negocio y Problemática Operativa en Retail Ferretero",
        "image": "06-Contexto-Problematica-Negocio.png",
        "sections": [
            {
                "header": "Volumetría y Complejidad Dimensional:",
                "items": [
                    "Catálogo activo superior a **12,000 SKUs** (fierro, cemento, PVC, pernería).",
                    "Variantes técnicas fraccionadas: pulgadas, milímetros, calibres y tipos de rosca.",
                    "Unidades multiescala: unidad, metro, ciento, millar, bolsa y caja master."
                ]
            },
            {
                "header": "Fricción Concurrente Omnicanal:",
                "items": [
                    "Clientes en mostrador físico POS y compradores web compitiendo por el mismo stock.",
                    "Riesgo de **sobreventa crítica (*overselling*)** por desfases en catálogo web (> 30 s)."
                ]
            },
            {
                "header": "Puntos Críticos de Falla Operativa:",
                "items": [
                    "**Connection Starvation:** ráfagas web saturan PostgreSQL y congelan cajas físicas.",
                    "Fugas de capital en cierres de turno por manipulación de arqueos no ciegos."
                ]
            }
        ],
        "callout_header": "INVARIANTE CARDINAL DEL SISTEMA (RNF-CON-003)",
        "callout_body": "Stock Disponible = Stock Físico - Stock Comprometido\n\nTolerancia cero: 0 sobreventas y 0 saldos negativos bajo cualquier condición de tráfico concurrente.",
        "notes": "Profesora, buenos días. SIGIC Ferretería v2.2 resuelve la problemática operativa de un retail ferretero de alta rotación en Ayacucho con más de doce mil artículos activos. La fricción crítica del negocio radica en el choque concurrente entre las cuadrillas de construcción que compran cemento y fierro en el mostrador físico y los clientes que compran por el portal web. Sin un control riguroso de inventario comprometido y sin aislar las conexiones de base de datos, las compras en línea agotan los recursos del servidor y generan sobreventas que paralizan los despachos en obra."
    },
    {
        "num": 2,
        "badge": "LÁMINA 02 | ACTORES Y FRONTERAS (IS-488 UNSCH)",
        "title": "Mapa de Actores, Segregación de Identidades y Sistemas Externos",
        "image": "07-Mapa-Actores-Fronteras.png",
        "sections": [
            {
                "header": "Dominio Interno (Colaboradores — RBAC / JWT RS256 / Argon2id):",
                "items": [
                    "**Superadministrador (`ROLE_SUPERADMIN`):** Gobernanza de catálogo maestro y tarifas.",
                    "**Cajero Mostrador (`ROLE_CAJERO`):** Facturación rápida 100% teclado (`F1`-`F12`) y escáner sin ratón en viewport 1024x768. Cobros mixtos y arqueo ciego.",
                    "**Almacenero (`ROLE_ALMACENERO`):** Kardex CPP, guías internas (`#GI-...`) y recepción.",
                    "**Despachador Mostrador (`ROLE_DESPACHADOR`):** Pantalla ligera (`RF-POS-011`) con DNI/Código. Aislamiento total: no toca dinero ni inventario.",
                    "**Personal de Tesorería (`ROLE_TESORERIA`):** Validación de vouchers (<= 5 MB) en storage privado."
                ]
            },
            {
                "header": "Dominio Externo y Sistemas de Frontera:",
                "items": [
                    "**Cliente Web (`SIGIC_WebClient`):** Catálogo desacoplado sin acceso a APIs internas.",
                    "**Proveedor PSE / OSE SUNAT:** Facturación electrónica asíncrona (XML con IGV 18%).",
                    "**Pasarela de Pagos / Bancos:** Cobros PCI-DSS, webhooks HMAC SHA-256 y Yape/Plin."
                ]
            }
        ],
        "callout_header": "SEGREGACIÓN ESTRICTA DE FRONTERAS",
        "callout_body": "Separación estanca de identidades: Tokens RS256 con Audience restringida impiden que clientes web o despachadores accedan a transacciones de caja o almacén central.",
        "notes": "En este mapa delimitamos formalmente la frontera de identidades del sistema dividida en dos dominios estancos. En el dominio interno, colaboradores como cajeros, almaceneros y personal de tesorería operan bajo control de acceso basado en roles autenticado con tokens JWT firmados asimétricamente mediante RS256. En el dominio externo, los clientes web acceden con tokens restringidos que impiden cualquier invocación a las APIs internas de mostrador. Observen cómo el despachador de tienda tiene una frontera estricta: entrega pedidos web cotejando el DNI sin manipular dinero ni vouchers bancarios, protegiendo la seguridad financiera de la empresa."
    },
    {
        "num": 3,
        "badge": "LÁMINA 03 | ATRIBUTOS DE CALIDAD (IS-488 UNSCH)",
        "title": "Atributos de Calidad Rectores y Métricas Cuantificables (ISO/IEC 25010)",
        "image": "08-Atributos-Calidad-Metricas.png",
        "sections": [
            {
                "header": "Matriz de Atributos de Calidad Rectores:",
                "items": [
                    "**AC01 (Rendimiento Transaccional):** Latencia en red local **P95 < 250 ms** en API Core. Captura de ítem <= 1.0 s; cierre de venta <= 2.0 s. PgBouncer (`pool_pos`, 50 conns) y Dapper sub-15ms.",
                    "**AC02 (Consistencia Transaccional):** **Cero (0) sobreventas** y 0 saldos negativos con 100 hilos concurrentes compitiendo por el mismo SKU. Bloqueo pesimista `SELECT ... FOR UPDATE` ordenado por `producto_id ASC`.",
                    "**AC03 (Trazabilidad y Auditoría):** **100% operaciones sensibles auditadas**. Estructura Append-Only en `caja_arqueo` y `Audit_Log`. Restricción DDL: `REVOKE UPDATE, DELETE ON caja_arqueo`."
                ]
            },
            {
                "header": "Atributos Secundarios Garantizados:",
                "items": [
                    "**RNF-USA-002 (Ergonomía POS):** Viewport 1024x768 px sin scroll horizontal; 100% teclado.",
                    "**RNF-DIS-001 (Alta Disponibilidad):** Uptime >= 99.5%, RTO <= 60 min, RPO <= 15 min."
                ]
            }
        ],
        "callout_header": "MÉTRICAS CUANTIFICABLES DE MISIÓN CRÍTICA",
        "callout_body": "Latencia P95 < 250 ms en API LAN • Cero sobreventas bajo 100 hilos concurrentes • Inmutabilidad contable con sentencias DDL estrictas en PostgreSQL 16.",
        "notes": "Nuestra arquitectura se fundamenta en tres atributos de calidad rectores medibles bajo el estándar ISO 25010. El atributo AC01 garantiza que una venta en caja física cierre en menos de dos segundos con una latencia de API P95 inferior a 250 milisegundos gracias al pool dedicado de PgBouncer. El atributo AC02 erradica totalmente las sobreventas forzando la ecuación de stock disponible y aplicando bloqueos pesimistas ordenados en PostgreSQL. Finalmente, el atributo AC03 asegura la inmutabilidad contable mediante tablas append-only donde revocamos formalmente los comandos de actualización y eliminación."
    },
    {
        "num": 4,
        "badge": "LÁMINA 04 | SELECCIÓN ARQUITECTÓNICA (IS-488 UNSCH)",
        "title": "Selección Arquitectónica: Monolito Modular vs. Microservicios",
        "image": "09-Decision-Monolito-Modular.png",
        "sections": [
            {
                "header": "Evaluación Comparativa de Trade-Offs Técnicos:",
                "items": [
                    "**Consistencia de Inventario (AC02):** Microservicios introducen consistencia eventual (Sagas) y sobreventa crítica. Monolito Modular garantiza **transacciones ACID inmediatas** en PostgreSQL 16.",
                    "**Latencia Inter-Módulo (AC01):** Microservicios añaden 20–50 ms por salto de red HTTP/gRPC. Monolito Modular logra **< 1 ms** en memoria del proceso Kestrel (P95 < 250 ms en LAN).",
                    "**Sobrecarga Operacional:** Microservicios exigen clúster Kubernetes y múltiples pipelines. Monolito Modular opera con **1 solo pipeline CI/CD** y 1 contenedor compilado en .NET 8.",
                    "**Auditoría Contable (AC03):** Transactional Outbox unificado (`Audit_Log`, `caja_arqueo`) en la misma transacción relacional sin desincronizaciones."
                ]
            },
            {
                "header": "Regla Estructural Cardinal:",
                "items": [
                    "Comunicación exclusiva mediante interfaces (`IInventarioReservaService`) y eventos en memoria con MediatR. Prohibido acceso cruzado directo a esquemas ajenos."
                ]
            }
        ],
        "callout_header": "DECISIÓN ARQUITECTÓNICA CLAVE",
        "callout_body": "Transacciones ACID unificadas sobre PostgreSQL 16 con llamadas tipadas en memoria (< 1 ms), eliminando de raíz las inconsistencias de inventario del modelo distribuido.",
        "notes": "Profesora, evaluamos rigurosamente el trade-off entre microservicios y un monolito modular. Desestimamos los microservicios porque su consistencia eventual basada en Sagas introduce ventanas de vulnerabilidad donde un producto se sobrevende antes de propagar el evento, sumado a una latencia de red de 20 a 50 milisegundos que degrada la atención en caja. El Monolito Modular en ASP.NET Core 8 nos permite ejecutar transacciones ACID inmediatas en la misma base de datos con llamadas en memoria inferiores a un milisegundo, simplificando drásticamente el despliegue a un solo pipeline de integración continua."
    },
    {
        "num": 5,
        "badge": "LÁMINA 05 | RESILIENCIA Y PERSISTENCIA (IS-488 UNSCH)",
        "title": "Segregación de Conexiones y Resiliencia con PgBouncer",
        "image": "10-PgBouncer-Segregacion-Pools.png",
        "sections": [
            {
                "header": "Mecanismo Anti-Inanición (Connection Starvation):",
                "items": [
                    "PgBouncer 1.21 configurado en modo asignación por transacción (`pool_mode = transaction`).",
                    "Desacoplamiento total entre conexiones cliente frontend y conexiones físicas en PostgreSQL 16."
                ]
            },
            {
                "header": "Segregación Estricta de Pools de Conexión:",
                "items": [
                    "**`pool_pos` (Alta Prioridad / Red LAN):** **50 conexiones reservadas exclusivas** para terminales de caja y almacén. Latencia < 1 ms; cero colas. Cobro garantizado <= 2.0 s.",
                    "**`pool_web` (Pool Limitado / Backpressure):** **30 conexiones máximas fijas** para portal e-commerce con timeout de cola de 5 s. Si la web se satura, las cajas operan sin retrasos."
                ]
            },
            {
                "header": "Aislamiento de Carga Analítica:",
                "items": [
                    "Datamart dimensional en esquema estrella (`sigic_olap`) para Kardex y reportes contables.",
                    "Cero contención de CPU y cero bloqueos de lectura sobre la base transaccional (`sigic_oltp`)."
                ]
            }
        ],
        "callout_header": "BLINDAJE DE ALTA CONCURRENCIA",
        "callout_body": "50 conexiones LAN reservadas (`pool_pos`) inmunes a ráfagas o ataques DoS en el canal web. Las cajas de mostrador nunca sufren inanición de conexiones.",
        "notes": "Para blindar la atención en tienda física contra el tráfico de Internet, interponemos PgBouncer operando en modo transacción con dos pools estrictamente aislados. El pool_pos reserva cincuenta conexiones exclusivas para las cajas de mostrador, garantizando que el cajero tenga acceso instantáneo al motor de base de datos con latencia sub-milisegundo. En contraste, el pool_web restringe el comercio electrónico a treinta conexiones máximas con control de backpressure; de este modo, aunque el sitio web experimente un pico masivo de visitas o un ataque de denegación de servicio, las cajas físicas continúan facturando con absoluta fluidez."
    },
    {
        "num": 6,
        "badge": "LÁMINA 06 | PROTOCOLO DE CONCURRENCIA (IS-488 UNSCH)",
        "title": "Protocolo de Checkout Atómico y Prevención Determinista de Deadlocks",
        "image": "05-Protocolo-Checkout-AntiDeadlock.png",
        "sections": [
            {
                "header": "Algoritmo Transaccional Paso a Paso (Invariantes SDD):",
                "items": [
                    "**Paso 1: Solicitud:** Carrito multilínea enviado hacia `CheckoutWebController`.",
                    "**Paso 2: Ordenamiento Determinista Anti-Deadlock (`RF-WEB-005`):** `items.OrderBy(x => x.ProductoId).ToList()` en memoria antes de abrir transacción. Erradica bloqueos cruzados en PostgreSQL.",
                    "**Paso 3: Apertura Transaccional:** Conexión asignada desde `pool_web` en PgBouncer.",
                    "**Paso 4: Bloqueo Pesimista:** `SELECT ... FOR UPDATE` ordenado por identificador de producto.",
                    "**Paso 5: Revalidación Atómica:** Predicado `WHERE (stock_fisico - stock_comprometido) >= @Cantidad`. Si no alcanza -> `HTTP 409 Conflict`.",
                    "**Paso 6: TTL Dinámico (`RF-WEB-002`):** Tarjetas: **20 min**. Pagos manuales (Yape/Plin): **240 min (4 h)** con ventana de **60 min** para subir voucher.",
                    "**Paso 7: Liberación Asíncrona Idempotente:** Background Worker cada **60 s** con `SELECT ... FOR UPDATE SKIP LOCKED` para liberar reservas caducadas sin contención."
                ]
            }
        ],
        "callout_header": "PREVENCIÓN MATEMÁTICA DE INTERBLOQUEOS",
        "callout_body": "El ordenamiento determinista por ProductoId en memoria elimina de raíz los grafos de espera circular (deadlocks) al adquirir cerraduras pesimistas en PostgreSQL 16.",
        "notes": "Este diagrama representa el núcleo de concurrencia de nuestro sistema: el protocolo de checkout atómico. Cuando un cliente compra varillas y cemento al mismo tiempo que el cajero factura en ventanilla, el riesgo clásico en bases de datos relacionales es el interbloqueo o deadlock. En SIGIC lo eliminamos ordenando deterministamente los identificadores de producto en memoria antes de solicitar la transacción relacional en PostgreSQL. Luego ejecutamos un SELECT FOR UPDATE, revalidamos atómicamente el precio y el stock disponible, e incrementamos el stock comprometido con su respectivo tiempo de vida útil, liberando automáticamente el inventario mediante un worker en segundo plano si el pago no se concreta."
    },
    {
        "num": 7,
        "badge": "LÁMINA 07 | MODELO C4: CONTEXTO DEL SISTEMA (C1)",
        "title": "Modelo C4 — Nivel 1: Diagrama de Contexto del Sistema",
        "image": "01-C1-Contexto-Sistema.png",
        "sections": [
            {
                "header": "Frontera Operativa y Enfoque de Caja Negra (Simon Brown):",
                "items": [
                    "Delimitación integral del sistema **SIGIC Ferretería** frente a su entorno comercial y fiscal."
                ]
            },
            {
                "header": "Interacción de los 6 Actores Humanos:",
                "items": [
                    "**Superadministrador:** Consola central de tarifas, almacenes y permisos RBAC.",
                    "**Cajero Mostrador:** Facturación presencial de alta velocidad en red LAN.",
                    "**Almacenero:** Recepción de materiales y control de Kardex CPP.",
                    "**Despachador Mostrador:** Entrega ágil de pedidos web presenciales (`RF-POS-011`).",
                    "**Personal de Tesorería:** Validación de extractos bancarios y comprobantes Yape/Plin.",
                    "**Cliente Web:** Compras en línea, catálogo asíncrono y seguimiento de pedidos."
                ]
            },
            {
                "header": "Sistemas Externos Integrados:",
                "items": [
                    "**Proveedor PSE/OSE:** Intercambio seguro HTTPS/REST para firma de comprobantes XML y CDR SUNAT.",
                    "**Pasarela de Pagos / Bancos:** Cobros digitales PCI-DSS con webhooks HMAC SHA-256."
                ]
            }
        ],
        "callout_header": "FRONTERA FISCAL Y COMERCIAL DESACOPLADA",
        "callout_body": "La interacción con SUNAT y pasarelas de pago se orquesta asincrónicamente para que contingencias externas nunca interrumpan la facturación presencial en tienda.",
        "notes": "Descendiendo a la formalización arquitectónica bajo el modelo C4 de Simon Brown, en el Nivel 1 delimitamos el sistema como una caja negra dentro de su entorno comercial. Observen cómo SIGIC no solo interactúa con los seis actores humanos de la ferretería, sino que mantiene integraciones directas con sistemas externos como el proveedor PSE/OSE homologado por SUNAT para facturación electrónica y las pasarelas de pago con tarjeta. Toda la interacción fiscal con SUNAT se realiza de forma asíncrona para que una eventual lentitud en los servidores tributarios nunca detenga la entrega física de mercadería en tienda."
    },
    {
        "num": 8,
        "badge": "LÁMINA 08 | MODELO C4: CONTENEDORES Y PERSISTENCIA (C2)",
        "title": "Modelo C4 — Nivel 2: Diagrama de Contenedores y Persistencia",
        "image": "02-C2-Contenedores-Segregacion.png",
        "sections": [
            {
                "header": "Topología de Cómputo y Procesos de Despliegue:",
                "items": [
                    "**Reverse Proxy / Gateway (Nginx 1.25):** Terminación TLS 1.3, rate limiting y compresión.",
                    "**SPA POS Mostrador (React 18 / Vite / Tailwind):** Frontend en red LAN, navegación 100% teclado (1024x768 px).",
                    "**Portal Web E-Commerce (Next.js 14 SSR):** Frontend público optimizado para SEO y mobile.",
                    "**Backend API Core (ASP.NET Core 8 Web API):** Núcleo monolítico modular Clean Architecture.",
                    "**Background Workers (.NET 8):** Expiración de reservas cada 60 s y consolidación ETL nocturna."
                ]
            },
            {
                "header": "Infraestructura de Datos y Aislamiento:",
                "items": [
                    "**PgBouncer 1.21:** Segregación física (`pool_pos`: 50 conns LAN vs. `pool_web`: 30 conns WAN).",
                    "**PostgreSQL 16 (`sigic_oltp`):** Base transaccional ACID, tablas append-only y bloqueos pesimistas.",
                    "**Datamart Analítico (`sigic_olap`):** Esquema dimensional estrella para reportería pesada.",
                    "**Storage Privado Cifrado (MinIO / FS):** Custodia de vouchers (<= 5 MB) y XMLs SUNAT."
                ]
            }
        ],
        "callout_header": "DESACOPLAMIENTO DE CAPAS Y PROCESOS",
        "callout_body": "Separación completa entre SPA LAN, Web SSR WAN, API Core y persistencia, garantizando escalabilidad controlada y aislamiento de fallos.",
        "notes": "En el Nivel 2 de contenedores mostramos la descomposición física y los procesos de despliegue de nuestra solución. Contamos con dos aplicaciones cliente completamente desacopladas: una SPA en React 18 optimizada para red LAN sin uso de ratón y un portal web en Next.js 14 con renderizado en servidor. Ambos frontends se comunican con el backend central en ASP.NET Core 8, el cual intermedia sus accesos a PostgreSQL mediante PgBouncer. Adicionalmente, incorporamos un storage privado cifrado para custodiar los vouchers bancarios de hasta 5 megabytes y un Datamart en esquema estrella que aísla totalmente los reportes gerenciales de la base transaccional."
    },
    {
        "num": 9,
        "badge": "LÁMINA 09 | MODELO C4: COMPONENTES INTERNOS (C3)",
        "title": "Modelo C4 — Nivel 3: Diagrama de Componentes de Checkout Concurrente",
        "image": "03-C3-Componentes-Checkout-Concurrente.png",
        "sections": [
            {
                "header": "Clean Architecture Concéntrica en ASP.NET Core 8:",
                "items": [
                    "**Capa Presentación:** `VentasPOSController` (cierre LAN), `CheckoutWebController` (filtro 404 estricto para artículos no web) y `DespachoMostradorController` (`RF-POS-011`).",
                    "**Capa Aplicación (MediatR CQRS):** `IniciarCheckoutCommandHandler`, `CrearVentaPOSCommandHandler` y `LiberarReservaVencidaCommandHandler`.",
                    "**Capa Dominio Puro:** `InventarioDomainService` (`Disponible = Físico - Comprometido`), `ReservaStockService` (TTL dinámico) y `MotorTarifasDomainService`.",
                    "**Capa Infraestructura:** `InventarioRepository` (`SIGICDbContext` EF Core 8 para mutaciones pesimistas), `CatalogoDapperRepository` (consultas sub-15ms) y `StoragePrivadoAdapter`."
                ]
            }
        ],
        "callout_header": "PERSISTENCIA HÍBRIDA OPTIMIZADA",
        "callout_body": "EF Core 8 para mutaciones pesimistas ACID complejas + Dapper para consultas de catálogo sub-15ms en lectura masiva, uniendo rigor transaccional y alto rendimiento.",
        "notes": "Al realizar un zoom en el Nivel 3 al interior del API Core, observamos la implementación estricta de Clean Architecture organizada en cuatro capas concéntricas. La capa de presentación expone controladores especializados para POS y Web, los cuales despachan comandos a través del mediador MediatR sin acoplarse directamente a la lógica de negocio. En el dominio puro, InventarioDomainService gobierna las reglas de inventario ordenando los identificadores antes del bloqueo pesimista. Finalmente, en la capa de persistencia combinamos EF Core 8 para escrituras transaccionales complejas y Dapper para lecturas de catálogo a velocidades sub-15 milisegundos."
    },
    {
        "num": 10,
        "badge": "LÁMINA 10 | MODELO C4: CLASES DE DOMINIO (C4)",
        "title": "Modelo C4 — Nivel 4: Diagrama de Código y Clases del Dominio",
        "image": "04-C4-Clases-Dominio.png",
        "sections": [
            {
                "header": "Diseño de Dominio Rico (DDD — Principio Tell, Don't Ask):",
                "items": [
                    "**`Producto` (Aggregate Root):** Atributos dimensionales, flag `DisponibleEnWeb`, método `ActualizarCostoPonderado(cant, costo)` y margen comercial.",
                    "**`InventarioStock`:** Stock físico y comprometido inmutables. Regla cardinal: `StockDisponible() => StockFisico - StockComprometido`. Mutación con `BloquearParaReserva()` y `LiberarReserva()`.",
                    "**`Venta` y `DetalleVenta`:** Composición de comprobantes SUNAT con cálculo de base imponible e IGV (18%). Cobros mixtos.",
                    "**`ArqueoCaja` (Append-Only):** Procedimiento de arqueo ciego por denominaciones monetarias. Cálculo automático: `MontoDiferencia => Declarado - CalculadoSistema`."
                ]
            }
        ],
        "callout_header": "ENCAPSULAMIENTO DE INVARIANTES",
        "callout_body": "Cero modelos anémicos. Toda regla de negocio (validación de stock, cálculo de CPP y cálculo de diferencias en caja) reside estrictamente dentro de las clases de dominio.",
        "notes": "Llegando al Nivel 4 de código, presentamos las cuatro entidades de dominio medulares exigidas por la especificación: Producto, Venta, DetalleVenta y ArqueoCaja. En SIGIC repudiamos el modelo anémico: las clases encapsulan su propio comportamiento bajo el principio 'Tell, Don't Ask'. Por ejemplo, la entidad InventarioStock protege la disponibilidad impidiendo que el stock comprometido supere al stock físico. Asimismo, la clase ArqueoCaja materializa nuestra política de arqueo ciego calculando discrepancias contables de forma automática, garantizando que el cajero no pueda alterar la información histórica de caja."
    },
    {
        "num": 11,
        "badge": "LÁMINA 11 | SEGURIDAD TRANSACCIONAL (IS-488 UNSCH)",
        "title": "Seguridad Transaccional: Arqueo Ciego y Custodia de Vouchers",
        "image": "11-Seguridad-Arqueo-Ciego.png",
        "sections": [
            {
                "header": "Procedimiento de Arqueo Ciego (`RF-CAJ-002` / `AC03`):",
                "items": [
                    "Declaración obligatoria de efectivo físico por denominaciones de billetes y monedas.",
                    "**Cierre a ciegas:** el cajero desconoce formalmente el saldo teórico del sistema antes de contar.",
                    "Triple Conciliación: Efectivo físico vs. Saldo de ventas del sistema vs. Pasarelas."
                ]
            },
            {
                "header": "Inmutabilidad Append-Only en Base de Datos:",
                "items": [
                    "Tablas de inserción exclusiva: `caja_arqueo`, `caja_arqueo_detalle` y `Audit_Log`.",
                    "Sentencia DDL en PostgreSQL 16:\n`REVOKE UPDATE, DELETE ON caja_arqueo, caja_arqueo_detalle FROM rol_cajero;`\n*(Garantiza no repudio y valor probatorio en auditorías contables).*"
                ]
            },
            {
                "header": "Custodia de Comprobantes Bancarios (`RF-WEB-003` / Ley 29733):",
                "items": [
                    "Vouchers de hasta 5 MB (JPG, PNG, PDF) en storage privado cifrado con URLs pre-firmadas temporales de 15 min.",
                    "Autenticación asimétrica: JWT firmados con RS256 (2048 bits) y hashes Argon2id."
                ]
            }
        ],
        "callout_header": "INMUTABILIDAD Y BLINDAJE CONTABLE",
        "callout_body": "Estructura Append-Only estricta con revocación de UPDATE y DELETE en PostgreSQL 16. Garantiza valor probatorio legal y no repudio en auditorías de caja.",
        "notes": "En esta lámina exponemos las defensas de seguridad transaccional que protegen los activos de la empresa. Implementamos el procedimiento de arqueo ciego donde el cajero debe declarar el efectivo físico desglosando billetes y monedas sin conocer el saldo calculado por el sistema, lo que elimina cualquier posibilidad de encubrir faltantes. Para garantizar el no repudio legal, revocamos formalmente los permisos de modificación y borrado en PostgreSQL mediante sentencias DDL. Además, custodiamos los comprobantes bancarios de los clientes en un storage privado cifrado bajo la Ley 29733, permitiendo su visualización únicamente al personal de tesorería a través de URLs temporales pre-firmadas."
    },
    {
        "num": 12,
        "badge": "LÁMINA 12 | CONCLUSIONES Y VALOR DE NEGOCIO (IS-488 UNSCH)",
        "title": "Conclusiones Técnicas, Certificación SDD y Valor de Negocio",
        "image": "12-Conclusiones-Certificacion-SDD.png",
        "sections": [
            {
                "header": "Metodología Spec-Driven Development (SDD):",
                "items": [
                    "**19 documentos técnicos validados** que actúan como la Única Fuente de Verdad.",
                    "Servidor MCP Guardian (`sigic-sdd-guardian`) que audita invariantes de stock antes de generar código."
                ]
            },
            {
                "header": "Logros de Ingeniería Demostrados:",
                "items": [
                    "**Cero Sobreventas (0%):** Invariante Disponible = Físico - Comprometido validada bajo estrés con 100 hilos concurrentes.",
                    "**Erradicación de Deadlocks:** Ordenamiento determinista en memoria `producto_id ASC` con bloqueos `SELECT ... FOR UPDATE`.",
                    "**Resiliencia Operativa:** PgBouncer con `pool_pos` asegura que el mostrador físico opere ininterrumpidamente (P95 < 250 ms).",
                    "**Modelo Rico DDD:** Clases con lógica encapsulada y arquitectura limpia concéntrica."
                ]
            },
            {
                "header": "Cumplimiento Fiscal y Legal:",
                "items": [
                    "Facturación electrónica homologada con SUNAT (PSE/OSE, XML con IGV 18% y CDR).",
                    "Custodia de datos personales y financieros alineada a la Ley N.° 29733."
                ]
            }
        ],
        "callout_header": "VEREDICTO DE ARQUITECTURA DE SOFTWARE",
        "callout_body": "Arquitectura formalizada, verificada y aprobada conforme a los estándares de IS-488 UNSCH, integrando rigor matemático, alta concurrencia y valor operativo comercial.",
        "notes": "Para concluir, SIGIC Ferretería v2.2 demuestra cómo una metodología guiada estrictamente por especificaciones resuelve de raíz los problemas críticos de un negocio comercial complejo. Hemos garantizado cero sobreventas y cero interbloqueos en transacciones concurrentes, blindando el mostrador físico mediante la segregación de conexiones en PgBouncer. Toda la solución cumple rigurosamente con la normativa fiscal de SUNAT y con las directivas de seguridad informática, respaldada por un modelo C4 completo que formaliza la arquitectura requerida en la asignatura IS-488. Muchas gracias por su atención."
    }
]

def add_formatted_runs(paragraph, text, base_color=TEXT_MAIN, bold_color=WHITE, code_color=CODE_SKY, size=Pt(10)):
    tokens = re.split(r"(\*\*[^*]+\*\*|`[^`]+`)", text)
    for token in tokens:
        if not token:
            continue
        run = paragraph.add_run()
        run.font.size = size
        run.font.name = "Arial"
        if token.startswith("**") and token.endswith("**"):
            run.text = token[2:-2]
            run.font.bold = True
            run.font.color.rgb = bold_color
        elif token.startswith("`") and token.endswith("`"):
            run.text = token[1:-1]
            run.font.bold = True
            run.font.color.rgb = code_color
        else:
            run.text = token
            run.font.bold = False
            run.font.color.rgb = base_color

def create_presentation():
    prs = pptx.Presentation()
    prs.slide_width = Inches(13.333)
    prs.slide_height = Inches(7.5)
    blank_layout = prs.slide_layouts[6]

    for data in slides_data:
        slide = prs.slides.add_slide(blank_layout)

        # 1. Native slide background fill
        bg = slide.background
        fill = bg.fill
        fill.solid()
        fill.fore_color.rgb = BG_COLOR

        # 2. Background base rectangle (guarantees dark canvas in Canva)
        bg_rect = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, prs.slide_width, prs.slide_height)
        bg_rect.fill.solid()
        bg_rect.fill.fore_color.rgb = BG_COLOR
        bg_rect.line.fill.background()

        # 3. Header Text Box
        header_box = slide.shapes.add_textbox(Inches(0.8), Inches(0.4), Inches(11.733), Inches(0.95))
        tf_header = header_box.text_frame
        tf_header.word_wrap = True
        tf_header.margin_left = tf_header.margin_top = tf_header.margin_right = tf_header.margin_bottom = 0

        # Badge paragraph
        p_badge = tf_header.paragraphs[0]
        p_badge.space_after = Pt(2)
        r_badge = p_badge.add_run()
        r_badge.text = data["badge"]
        r_badge.font.name = "Arial"
        r_badge.font.size = Pt(11)
        r_badge.font.bold = True
        r_badge.font.color.rgb = EMERALD

        # Title paragraph
        p_title = tf_header.add_paragraph()
        r_title = p_title.add_run()
        r_title.text = data["title"]
        r_title.font.name = "Arial"
        r_title.font.size = Pt(21)
        r_title.font.bold = True
        r_title.font.color.rgb = WHITE

        # 4. Left Column: Content Container Card
        left_card = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(0.8), Inches(1.45), Inches(5.95), Inches(5.35))
        left_card.fill.solid()
        left_card.fill.fore_color.rgb = CARD_BG
        left_card.line.color.rgb = CARD_BORDER
        left_card.line.width = Pt(1)

        # Left Column: Text Box
        content_box = slide.shapes.add_textbox(Inches(0.95), Inches(1.55), Inches(5.65), Inches(5.15))
        tf_content = content_box.text_frame
        tf_content.word_wrap = True
        tf_content.margin_left = tf_content.margin_top = tf_content.margin_right = tf_content.margin_bottom = 0

        first_p = True
        for sec in data["sections"]:
            # Section Header
            p_sec = tf_content.paragraphs[0] if first_p else tf_content.add_paragraph()
            first_p = False
            p_sec.space_before = Pt(6) if not first_p else Pt(0)
            p_sec.space_after = Pt(2)
            r_sec = p_sec.add_run()
            r_sec.text = sec["header"]
            r_sec.font.name = "Arial"
            r_sec.font.size = Pt(11)
            r_sec.font.bold = True
            r_sec.font.color.rgb = EMERALD_LIGHT

            # Items / Bullets
            for it in sec["items"]:
                p_it = tf_content.add_paragraph()
                p_it.space_after = Pt(3)
                p_it.level = 0
                # Bullet bullet symbol run
                r_dot = p_it.add_run()
                r_dot.text = "▪ "
                r_dot.font.name = "Arial"
                r_dot.font.size = Pt(9)
                r_dot.font.color.rgb = EMERALD
                # Body runs with formatting
                add_formatted_runs(p_it, it, base_color=TEXT_MAIN, bold_color=WHITE, code_color=CODE_SKY, size=Pt(9.5))

        # 5. Right Column: High-Res Diagram Image
        img_filename = data["image"]
        img_path = os.path.join(RECURSOS_DIR, img_filename)
        if os.path.exists(img_path):
            img_width = Inches(5.7)
            img_height = Inches(5.7 * 1080 / 1920) # 3.206 inches
            slide.shapes.add_picture(img_path, Inches(6.9), Inches(1.45), width=img_width, height=img_height)

        # 6. Right Column: Callout Card below Image
        callout_top = Inches(4.8)
        callout_height = Inches(2.0)
        callout_card = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(6.9), callout_top, Inches(5.7), callout_height)
        callout_card.fill.solid()
        callout_card.fill.fore_color.rgb = CALLOUT_BG
        callout_card.line.color.rgb = CALLOUT_BORDER
        callout_card.line.width = Pt(1.5)

        callout_box = slide.shapes.add_textbox(Inches(7.05), callout_top + Inches(0.1), Inches(5.4), callout_height - Inches(0.2))
        tf_callout = callout_box.text_frame
        tf_callout.word_wrap = True
        tf_callout.margin_left = tf_callout.margin_top = tf_callout.margin_right = tf_callout.margin_bottom = 0

        p_ch = tf_callout.paragraphs[0]
        p_ch.space_after = Pt(3)
        r_ch = p_ch.add_run()
        r_ch.text = "★ " + data["callout_header"]
        r_ch.font.name = "Arial"
        r_ch.font.size = Pt(9.5)
        r_ch.font.bold = True
        r_ch.font.color.rgb = EMERALD

        p_cb = tf_callout.add_paragraph()
        add_formatted_runs(p_cb, data["callout_body"], base_color=CALLOUT_TEXT, bold_color=WHITE, code_color=CODE_SKY, size=Pt(9))

        # 7. Slide Footer
        footer_box = slide.shapes.add_textbox(Inches(0.8), Inches(6.9), Inches(11.733), Inches(0.35))
        tf_footer = footer_box.text_frame
        tf_footer.margin_left = tf_footer.margin_top = tf_footer.margin_right = tf_footer.margin_bottom = 0
        p_f = tf_footer.paragraphs[0]
        
        r_f1 = p_f.add_run()
        r_f1.text = "Crhistian Emanuel Flores Marquina • IS-488 Arquitectura de Software (UNSCH)"
        r_f1.font.name = "Arial"
        r_f1.font.size = Pt(8.5)
        r_f1.font.color.rgb = FOOTER_TEXT

        r_f_sep = p_f.add_run()
        r_f_sep.text = "   |   "
        r_f_sep.font.name = "Arial"
        r_f_sep.font.size = Pt(8.5)
        r_f_sep.font.color.rgb = FOOTER_TEXT

        r_f2 = p_f.add_run()
        r_f2.text = "SIGIC Ferretería v2.2 • Spec-Driven Development"
        r_f2.font.name = "Arial"
        r_f2.font.size = Pt(8.5)
        r_f2.font.color.rgb = FOOTER_TEXT

        # 8. Speaker Notes
        notes_slide = slide.notes_slide
        tf_notes = notes_slide.notes_text_frame
        tf_notes.text = data["notes"]

    prs.save(OUTPUT_PPTX)
    print(f"Presentation saved successfully at: {OUTPUT_PPTX}")

if __name__ == "__main__":
    create_presentation()
