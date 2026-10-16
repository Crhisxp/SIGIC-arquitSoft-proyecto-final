# Escenarios Formales de Atributos de Calidad (SIGIC Ferretería)

> **Documento:** `01-analisis-de-sistema/08-escenarios-atributos-calidad.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Estándar:** Modelo Formal de 6 Partes del SEI (*Software Engineering Institute* - Carnegie Mellon / Bass, Clements & Kazman)  
> **Sistema:** SIGIC Ferretería (Sistema Integrado de Gestión de Inventario y Comercialización)

---

## 1. Fundamentación Metodológica y Estructura SEI

La especificación de Atributos de Calidad mediante **Escenarios Formales de Calidad** proporciona un mecanismo cuantitativo, no ambiguo y directamente verificable para evaluar la arquitectura del software. Conforme al estándar de ingeniería de software del **Software Engineering Institute (SEI)**, cada escenario se estructura estrictamente en seis dimensiones operativas:

1. **Fuente del Estímulo (*Source of Stimulus*):** La entidad o condición (interna o externa) que genera el evento (usuario web, cajero POS, temporizador de fondo, atacante).
2. **Estímulo (*Stimulus*):** La acción, petición o anomalía concreta que arriba al sistema.
3. **Artefacto (*Artifact*):** La porción o subsistema de software/infraestructura impactado directamente.
4. **Entorno (*Environment*):** Las condiciones operativas en las que ocurre el estímulo (operación pico simultánea, condiciones normales de red, estrés transaccional, degradación de servicios externos).
5. **Respuesta (*Response*):** La acción arquitectónica observable ejecutada por el sistema para responder al estímulo sin comprometer su integridad.
6. **Medida de Respuesta (*Response Measure*):** El criterio de aceptación métrico, cuantificable y reproducible que determina si la respuesta es satisfactoria.

---

## 2. Matriz Consolidada de Escenarios de Calidad

A continuación se presentan los cinco escenarios rectores que mitigan los riesgos arquitectónicos de mayor impacto en **SIGIC Ferretería v2.2**:

```mermaid
flowchart TD
    subgraph Escenarios ["Matriz de Escenarios Rectores (SEI)"]
        ESC1["ESC-01: Concurrencia Crítica Multicanal (RNF-CON-003)"]
        ESC2["ESC-02: Rendimiento Prioritario en Mostrador POS (RNF-PERF-001)"]
        ESC3["ESC-03: Estrés y Contención en Catálogo Web (RNF-PERF-004)"]
        ESC4["ESC-04: Aislamiento de Conexiones PgBouncer (RNF-PERF-005)"]
        ESC5["ESC-05: Seguridad y Fuerza Bruta en Clientes Web (RNF-SEG-005)"]
    end

    ESC1 --> DB[(PostgreSQL 16 - OLTP)]
    ESC2 --> POS[Terminal Mostrador POS]
    ESC3 --> WEB[Portal Next.js 14 SSR]
    ESC4 --> PGB[PgBouncer Pools Segregados]
    ESC5 --> SEC[Auth Middleware RS256 / Argon2id]
```

---

### Escenario 1: Concurrencia Crítica Multicanal en Materiales Pesados (RNF-CON-003)

- **ID Escenario:** `ESC-CON-001`
- **Atributo de Calidad Asociado:** Concurrencia e Integridad Transaccional (`RNF-CON-001`, `RNF-CON-002`, `RNF-CON-003`, `RF-WEB-005`).
- **Prioridad / Dificultad:** Alta (Misión Crítica) / Alta.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Múltiples clientes externos en el portal web (contratistas finalizando checkout) y cajeros internos en terminales de mostrador POS. |
| **Estímulo** | Envío concurrente y simultáneo de 100 solicitudes transaccionales (50 hilos desde POS y 50 hilos desde el checkout Web) intentando reservar o cobrar las últimas 5 unidades disponibles de un material de construcción de alta rotación (cemento Portland Tipo I o varillas de fierro de 1/2"). |
| **Artefacto** | Módulo de Dominio `Inventario`, Servicio `ReservaDinamicaService`, Repositorio de Persistencia transaccional y tablas relacionales `inventario_stock` y `reserva_pedido` en PostgreSQL 16. |
| **Entorno** | Modo de producción en hora punta comercial de sábado por la mañana (08:30 a 11:30 h), con alta concurrencia en LAN de mostrador y tráfico activo en Internet. |
| **Respuesta** | El sistema encola y serializa los bloqueos de fila aplicando de forma determinista `ORDER BY producto_id ASC` mediante `SELECT ... FOR UPDATE` y verifica atómicamente la condición estricta: `(stock_fisico - stock_comprometido) >= @CantidadSolicitada`. Autoriza exactamente la cantidad cubierta por el stock real e incrementa `stock_comprometido` (para web) o descuenta `stock_fisico` (para POS). Las transacciones excedentes son abortadas limpiamente con reversión de transacción (*ROLLBACK*) e informadas a la capa de presentación. |
| **Medida de Respuesta** | **1.** Sobreventa (*overselling*) = 0 unidades (saldo disponible resultante jamás es inferior a cero).<br>**2.** Exactamente 5 unidades vendidas/reservadas y las 95 restantes formalmente rechazadas (`HTTP 409 Conflict` en Web y advertencia inmediata en POS).<br>**3.** Tasa de deadlocks detectados por el motor de base de datos = 0 %.<br>**4.** Tiempo total de resolución de la contención transaccional $\le 400\text{ ms}$ (p95). |

---

### Escenario 2: Rendimiento y Prioridad de Atención en Mostrador POS (RNF-PERF-001)

- **ID Escenario:** `ESC-PERF-001`
- **Atributo de Calidad Asociado:** Rendimiento y Eficiencia de Recursos (`RNF-PERF-001`, `RF-POS-001`, `RF-POS-003`, `RF-POS-004`).
- **Prioridad / Dificultad:** Alta / Media-Alta.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Cajeros operando simultáneamente terminales de mostrador POS mediante escáneres láser y teclado físico. |
| **Estímulo** | Registro continuo de ítems de compra por código de barras (EAN-13/SKU) y solicitud de cierre de cobro con emisión de ticket/boleta electrónica. |
| **Artefacto** | SPA React 18 Mostrador, Backend ASP.NET Core 8 Web API (`VentasPOSController`, `CrearVentaCommandHandler`) y conexión de base de datos a través del `pool_pos` en PgBouncer. |
| **Entorno** | Concurrencia máxima sostenida de 20 terminales de mostrador operando en red local (LAN) en paralelo con un pico de tráfico en el portal e-commerce (200 sesiones web activas realizando consultas intensivas de catálogo). |
| **Respuesta** | El sistema despacha las solicitudes de mostrador a través de conexiones persistentes dedicadas del `pool_pos` con prioridad transaccional. La resolución de cálculo de IGV, validación de inventario en memoria y registro de pagos mixtos se procesa en el servidor con latencia ultrabaja, sin que las peticiones web compitan por las conexiones de mostrador. |
| **Medida de Respuesta** | **1.** Tiempo de respuesta para adición de ítem por escáner $\le 1.0\text{ segundo}$ (p95).<br>**2.** Tiempo total de confirmación y cierre de venta con emisión de comprobante $\le 2.0\text{ segundos}$ (p95).<br>**3.** La degradación de latencia en mostrador atribuible al tráfico web no supera el $10\text{ \%}$ frente a condiciones de reposo web.<br>**4.** 0 bloqueos de UI o caídas de conexión en los puestos de caja. |

---

### Escenario 3: Picos de Tráfico Web Concurrente y Contención en Catálogo (RNF-PERF-004)

- **ID Escenario:** `ESC-PERF-002`
- **Atributo de Calidad Asociado:** Rendimiento y Escalabilidad Web (`RNF-PERF-004`, `RF-WEB-001`, `RF-WEB-005`).
- **Prioridad / Dificultad:** Media-Alta / Media.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Múltiples contratistas, maestros de obra y público general navegando el portal e-commerce desde dispositivos móviles y navegadores de escritorio. |
| **Estímulo** | Ráfaga repentina de tráfico web entrante equivalente a 200 sesiones de usuario activas simultáneas generando una tasa sostenida de 50 solicitudes HTTP GET por segundo (`req/s`) sobre el catálogo, filtros por categoría y fichas técnicas. |
| **Artefacto** | Proxy Inverso Nginx, Frontend Next.js 14 SSR, endpoints públicos de catálogo en ASP.NET Core 8 y vistas materializadas/consultas optimizadas de lectura en PostgreSQL. |
| **Entorno** | Concurrencia web máxima durante campañas de descuento o inicio de jornada laboral de proyectos de construcción en la provincia de Huamanga. |
| **Respuesta** | Nginx sirve las páginas y recursos estáticos pre-renderizados desde su caché perimetral con cabeceras `stale-while-revalidate`. Las consultas dinámicas de artículos leen de una proyección optimizada desacoplada de la base transaccional (con desfase controlado de hasta 30 segundos). Ninguna consulta de lectura de catálogo adquiere bloqueos pesimistas (`FOR UPDATE`) sobre las tablas operativas de la ferretería. |
| **Medida de Respuesta** | **1.** Tiempo de renderizado inicial de página de catálogo $\le 1.5\text{ segundos}$ (p95) en conexión móvil de referencia (4G).<br>**2.** Tiempo de respuesta de la API pública de productos $\le 400\text{ ms}$ (p95).<br>**3.** Tasa de errores HTTP 5xx = $0\text{ \%}$.<br>**4.** Consumo de CPU del servidor transaccional por consultas de catálogo $\le 30\text{ \%}$. |

---

### Escenario 4: Aislamiento de Conexiones en PgBouncer ante Sobrecarga Web (RNF-PERF-005)

- **ID Escenario:** `ESC-PERF-003`
- **Atributo de Calidad Asociado:** Resiliencia y Aislamiento de Recursos (`RNF-PERF-005`, `RNF-CON-001`, `DA-02`).
- **Prioridad / Dificultad:** Alta / Alta.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Sobrecarga extrema o avalancha de conexiones concurrentes originadas en el portal público e-commerce (clientes abriendo múltiples pestañas o scripts automatizados de consulta). |
| **Estímulo** | El volumen de peticiones web supera la capacidad asignada al pool web en PgBouncer, demandando más de 100 conexiones simultáneas sobre la base de datos relacional. |
| **Artefacto** | Pooler de conexiones PgBouncer, configurado en Transaction Pooling con dos pools lógicos: `pool_pos` y `pool_web`. |
| **Entorno** | Incidente de tráfico o evento masivo de consulta digital durante la operación comercial regular de la ferretería física. |
| **Respuesta** | PgBouncer aplica contención estricta (*backpressure*): encola las conexiones web entrantes en el `pool_web` y rechaza con `timeout` aquellas que superen los umbrales configurados, impidiendo que el portal web tome conexiones adicionales. Las conexiones del `pool_pos` permanecen 100 % reservadas y aisladas físicamente para el tráfico del mostrador físico. |
| **Medida de Respuesta** | **1.** El número de conexiones activas asignadas a `pool_pos` se mantiene constante y disponible en todo momento (cero incidentes de *pool exhaustion* para el POS).<br>**2.** La degradación del tiempo de confirmación de cobro en POS es $\le 10\text{ \%}$ durante el incidente.<br>**3.** PostgreSQL 16 no supera su límite máximo de conexiones de motor (`max_connections = 150`), evitando caídas por falta de memoria *work_mem*. |

---

### Escenario 5: Seguridad, Rate Limiting y Resistencia a Fuerza Bruta en Clientes Web (RNF-SEG-005)

- **ID Escenario:** `ESC-SEG-001`
- **Atributo de Calidad Asociado:** Seguridad, Confidencialidad e Integridad de Identidad (`RNF-SEG-001`, `RNF-SEG-002`, `RNF-SEG-005`, Ley N.° 29733).
- **Prioridad / Dificultad:** Alta / Media.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Agente malicioso o botnet en Internet intentando vulnerar credenciales de clientes web (maestros de obra o contratistas con líneas de crédito o descuentos preferenciales). |
| **Estímulo** | Envío consecutivo de solicitudes HTTP POST al endpoint `/api/v1/auth/login` con contraseñas aleatorias o ataques de diccionario dirigidos a una misma cuenta de usuario o desde una misma subred IP. |
| **Artefacto** | Middleware de Rate Limiting en ASP.NET Core / Nginx, Servicio de Autenticación (`IdentityService`), algoritmo de hashing **Argon2id** y tabla de auditoría `security_login_attempt`. |
| **Entorno** | Sistema en producción expuesto públicamente sobre Internet bajo protocolo cifrado TLS 1.3. |
| **Respuesta** | El sistema evalúa el contador de intentos fallidos por identificador de cliente y por IP de origen. Al detectarse el quinto ($5^{\circ}$) intento fallido dentro de una ventana de 15 minutos, el middleware bloquea automáticamente la cuenta temporalmente por 15 minutos e interpone rate limiting inmediato devolviendo `HTTP 429 Too Many Requests`. Se genera una entrada inmutable en la bitácora de auditoría con la IP, fecha UTC y tipo de evento. La verificación criptográfica utiliza Argon2id con parámetros fijos para mitigar ataques de temporización (*timing attacks*). |
| **Medida de Respuesta** | **1.** Bloqueo efectivo de la cuenta verificado en el $100\text{ \%}$ de los casos tras el 5.° intento fallido consecutivo.<br>**2.** Tiempo de bloqueo temporal = exactamente 15 minutos antes de permitir un nuevo intento.<br>**3.** Sobrecarga en CPU del servidor por cálculo de hashing contenida dentro de límites tolerables ($\le 25\text{ \%}$ de CPU asignada a autenticación).<br>**4.** $0\text{ \%}$ de credenciales comprometidas o expuestas en logs o trazas de excepción. |

---

## 3. Matriz de Trazabilidad: Requisitos vs. Escenarios SEI

| Escenario SEI | Atributo Calidad Primario | Requisitos Funcionales Afectados | Decisión Arquitectónica (ADR) |
| :--- | :--- | :--- | :--- |
| **ESC-CON-001** | `RNF-CON-003`, `RNF-CON-001` | `RF-INV-001`, `RF-POS-001`, `RF-WEB-002`, `RF-WEB-005` | **ADR-001**, **ADR-003** |
| **ESC-PERF-001** | `RNF-PERF-001` | `RF-POS-003`, `RF-POS-004`, `RF-POS-006`, `RF-POS-011` | **ADR-003**, **ADR-004**, **ADR-007** |
| **ESC-PERF-002** | `RNF-PERF-004` | `RF-WEB-001`, `RF-WEB-005` | **ADR-004**, **ADR-007** |
| **ESC-PERF-003** | `RNF-PERF-005` | `RF-POS-001`, `RF-WEB-005` | **ADR-004** |
| **ESC-SEG-001** | `RNF-SEG-005`, `RNF-SEG-002` | `RF-PRE-002`, `RF-WEB-001`, `RF-CAJ-001` | **ADR-005** |
