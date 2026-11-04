---
id: "08-escenarios-atributos-calidad"
doc: "08-escenarios-atributos-calidad"
version: "2.2.0"
estado: "VALIDADO_PARA_EXPOSICION"
tipo: "escenarios-atributos-calidad-sei"
proyecto: "SIGIC-Ferreteria-v2.2"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 UNSCH"
fecha: "2026-10-30"
---

# Escenarios Formales de Atributos de Calidad (SIGIC Ferretería v2.2)

> **Documento:** `01-analisis-de-sistema/08-escenarios-atributos-calidad.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Estándar:** Modelo Formal de 6 Partes del SEI (*Software Engineering Institute* - Carnegie Mellon / Bass, Clements & Kazman)  
> **Alineación con Atributos Rectores:** AC01 (Rendimiento P95 < 250ms), AC02 (Consistencia ACID Cero Sobreventas), AC03 (Trazabilidad Append-Only)  
> **Estado:** VALIDADO_PARA_EXPOSICION  

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
        ESC1["ESC-01: Concurrencia Crítica Multicanal (AC02 / RNF-CON-003)"]
        ESC2["ESC-02: Rendimiento Prioritario en Mostrador POS (AC01 / RNF-PERF-001)"]
        ESC3["ESC-03: Estrés y Contención en Catálogo Web (RNF-PERF-004)"]
        ESC4["ESC-04: Aislamiento de Conexiones PgBouncer (AC01 / RNF-PERF-005)"]
        ESC5["ESC-05: Seguridad, Trazabilidad y Fuerza Bruta (AC03 / RNF-SEG-005)"]
    end

    ESC1 --> DB[(PostgreSQL 16 - OLTP)]
    ESC2 --> POS[Terminal Mostrador POS]
    ESC3 --> WEB[Portal Next.js 14 SSR]
    ESC4 --> PGB[PgBouncer Pools Segregados]
    ESC5 --> SEC[Auth Middleware RS256 / Argon2id]
```

---

### Escenario 1: Concurrencia Crítica Multicanal en Materiales Pesados (AC02 / RNF-CON-003)

- **ID Escenario:** `ESC-CON-001`
- **Atributo de Calidad Rector:** **AC02 — Consistencia Transaccional ACID y Cero Sobreventas** (`RNF-CON-001`, `RNF-CON-002`, `RNF-CON-003`, `RF-WEB-005`).
- **Prioridad / Dificultad:** Alta (Misión Crítica) / Alta.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Múltiples clientes externos en el portal web (contratistas finalizando checkout) y cajeros internos en terminales de mostrador POS. |
| **Estímulo** | Envío concurrente y simultáneo de 100 solicitudes transaccionales (50 hilos desde POS y 50 hilos desde el checkout Web) intentando reservar o cobrar las últimas 5 unidades disponibles de un material de construcción de alta rotación (cemento Portland Tipo I o varillas de fierro de 1/2"). |
| **Artefacto** | Módulo de Dominio `Inventario`, Servicio `ReservaDinamicaService`, Repositorio de Persistencia transaccional y tablas relacionales `inventario_stock` y `reserva_stock_pedido` en PostgreSQL 16. |
| **Entorno** | Modo de producción en hora punta comercial de sábado por la mañana (08:30 a 11:30 h), con alta concurrencia en LAN de mostrador y tráfico activo en Internet. |
| **Respuesta** | El sistema encola y serializa los bloqueos de fila aplicando de forma determinista `ORDER BY producto_id ASC` mediante `SELECT ... FOR UPDATE` y verifica atómicamente la condición estricta: `(stock_fisico - stock_comprometido) >= @CantidadSolicitada`. Autoriza exactamente la cantidad cubierta por el stock real e incrementa `stock_comprometido` (para web) o descuenta `stock_fisico` (para POS). Las transacciones excedentes son abortadas limpiamente con reversión de transacción (*ROLLBACK*) e informadas a la capa de presentación. |
| **Medida de Respuesta** | **1.** Sobreventa (*overselling*) = 0 unidades (saldo disponible resultante jamás es inferior a cero).<br>**2.** Exactamente 5 unidades vendidas/reservadas y las 95 restantes formalmente rechazadas (`HTTP 409 Conflict` en Web y advertencia inmediata en POS).<br>**3.** Tasa de deadlocks detectados por el motor de base de datos = 0 %.<br>**4.** Tiempo total de resolución de la contención transaccional $\le 400\text{ ms}$ (p95).<br>**5. Instrumental Técnico:** Monitoreo y telemetría de bloqueos pesimistas vía consultas de catálogo a `pg_stat_activity` y `pg_locks` en PostgreSQL 16 (verificando asignación de cerrojos exclusivos de fila y ausencia de ciclos de espera circular). |

---

### Escenario 2: Rendimiento y Prioridad de Atención en Mostrador POS (AC01 / RNF-PERF-001)

- **ID Escenario:** `ESC-PERF-001`
- **Atributo de Calidad Rector:** **AC01 — Rendimiento Transaccional y Latencia de API (P95 < 250ms)** (`RNF-PERF-001`, `RF-POS-001`, `RF-POS-003`, `RF-POS-004`).
- **Prioridad / Dificultad:** Alta / Media-Alta.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Cajeros operando simultáneamente terminales de mostrador POS mediante escáneres láser y teclado físico. |
| **Estímulo** | Registro continuo de ítems de compra por código de barras (EAN-13/SKU) y solicitud de cierre de cobro con emisión de ticket/boleta electrónica. |
| **Artefacto** | SPA React 18 Mostrador, Backend ASP.NET Core 8 Web API (`VentasPOSController`, `CrearVentaCommandHandler`) y conexión de base de datos a través del `pool_pos` en PgBouncer. |
| **Entorno** | Concurrencia máxima sostenida de 20 terminales de mostrador operando en red local (LAN) en paralelo con un pico de tráfico en el portal e-commerce (200 sesiones web activas realizando consultas intensivas de catálogo). |
| **Respuesta** | El sistema despacha las solicitudes de mostrador a través de conexiones persistentes dedicadas del `pool_pos` con prioridad transaccional. La resolución de cálculo de IGV, validación de inventario en memoria y registro de pagos mixtos se procesa en el servidor con latencia ultrabaja, sin que las peticiones web compitan por las conexiones de mostrador. |
| **Medida de Respuesta** | **1.** Latencia de backend API Core en red LAN $P95 < 250\text{ ms}$, validada mediante pruebas de carga automatizadas con **k6** midiendo la métrica nativa `http_req_duration{quantile:0.95}`.<br>**2.** Latencia de consultas de persistencia Dapper sub-15ms, recolectada e instrumentada en tiempo real mediante **MiniProfiler** y exportadores de trazas **OpenTelemetry** en ASP.NET Core 8.<br>**3.** Tiempo de respuesta para adición de ítem por escáner $\le 1.0\text{ segundo}$ (p95) en UI.<br>**4.** Tiempo total de confirmación y cierre de venta con emisión de comprobante $\le 2.0\text{ segundos}$ (p95).<br>**5.** La degradación de latencia en mostrador atribuible al tráfico web no supera el $10\text{ \%}$ frente a condiciones de reposo web.<br>**6.** 0 bloqueos de UI o caídas de conexión en los puestos de caja. |

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

### Escenario 4: Aislamiento de Conexiones en PgBouncer ante Sobrecarga Web (AC01 / RNF-PERF-005)

- **ID Escenario:** `ESC-PERF-003`
- **Atributo de Calidad Asociado:** Resiliencia y Aislamiento de Recursos (`RNF-PERF-005`, `RNF-CON-001`, `DA-02`, AC01).
- **Prioridad / Dificultad:** Alta / Alta.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Sobrecarga extrema o avalancha de conexiones concurrentes originadas en el portal público e-commerce (clientes abriendo múltiples pestañas o scripts automatizados de consulta). |
| **Estímulo** | El volumen de peticiones web supera la capacidad asignada al pool web en PgBouncer, demandando más de 100 conexiones simultáneas sobre la base de datos relacional. |
| **Artefacto** | Pooler de conexiones PgBouncer, configurado en Transaction Pooling con dos pools lógicos: `pool_pos` y `pool_web`. |
| **Entorno** | Incidente de tráfico o evento masivo de consulta digital durante la operación comercial regular de la ferretería física. |
| **Respuesta** | PgBouncer aplica contención estricta (*backpressure*): encola las conexiones web entrantes en el `pool_web` y rechaza con `timeout` aquellas que superen los umbrales configurados, impidiendo que el portal web tome conexiones adicionales. Las conexiones del `pool_pos` permanecen 100 % reservadas y aisladas físicamente para el tráfico del mostrador físico. |
| **Medida de Respuesta** | **1.** El número de conexiones activas asignadas a `pool_pos` (50 fijas) se mantiene constante y disponible en todo momento (cero incidentes de *pool exhaustion* para el POS).<br>**2.** La degradación del tiempo de confirmación de cobro en POS es $\le 10\text{ \%}$ durante el incidente, manteniendo latencia API $P95 < 250\text{ ms}$.<br>**3.** PostgreSQL 16 no supera su límite máximo de conexiones de motor (`max_connections = 150`), evitando caídas por falta de memoria *work_mem*. |

---

### Escenario 5: Seguridad, Trazabilidad Append-Only y Resistencia a Fuerza Bruta (AC03 / RNF-SEG-005)

- **ID Escenario:** `ESC-SEG-001`
- **Atributo de Calidad Rector:** **AC03 — Trazabilidad y Auditoría de Arqueos Append-Only** y Seguridad de Identidad (`RNF-SEG-001`, `RNF-SEG-002`, `RNF-SEG-004`, `RNF-SEG-005`, Ley N.° 29733).
- **Prioridad / Dificultad:** Alta / Media.

| Dimensión SEI | Especificación Formal |
| :--- | :--- |
| **Fuente del Estímulo** | Agente malicioso intentando vulnerar credenciales de clientes web, o intento de manipulación retroactiva de un arqueo de caja por un colaborador. |
| **Estímulo** | Envío consecutivo de solicitudes HTTP POST al endpoint `/api/v1/auth/login` con contraseñas aleatorias (fuerza bruta), o intento de emisión de sentencias `UPDATE` o `DELETE` sobre registros de arqueo contable. |
| **Artefacto** | Middleware de Rate Limiting en ASP.NET Core / Nginx, Servicio de Autenticación (`IdentityService`), algoritmo de hashing **Argon2id**, tablas relacionales `caja_arqueo` y tabla de auditoría `Audit_Log`. |
| **Entorno** | Sistema en producción expuesto públicamente sobre Internet bajo protocolo cifrado TLS 1.3 y operación interna en LAN. |
| **Respuesta** | 1. Para fuerza bruta: al detectarse el quinto ($5^{\circ}$) intento fallido en 15 minutos, el middleware bloquea la cuenta por 15 minutos y retorna `HTTP 429 Too Many Requests`. Se genera una entrada inmutable en `Audit_Log`.<br>2. Para arqueos: el sistema procesa el arqueo mediante inserción pura **append-only**; los permisos del usuario de base de datos prohíben `UPDATE` y `DELETE` en el esquema contable. |
| **Medida de Respuesta** | **1.** Bloqueo efectivo de cuenta verificado en el $100\text{ \%}$ de los casos tras el 5.° intento fallido consecutivo.<br>**2.** Inmutabilidad contable y auditoría estricta de permisos SQL: cualquier intento de ejecución de sentencias `UPDATE` o `DELETE` sobre las tablas `caja_arqueo` o `caja_arqueo_detalle` es rechazado de inmediato por el motor PostgreSQL 16 con el código de error `42501 permission denied for table caja_arqueo`, garantizando cero ($0$) alteraciones retroactivas de arqueos.<br>**3.** Sobrecarga en CPU del servidor por cálculo de hashing Argon2id contenida ($\le 25\text{ \%}$ de CPU asignada a autenticación).<br>**4.** $0\text{ \%}$ de credenciales o montos comprometidos. |

---

## 3. Matriz de Trazabilidad: Requisitos vs. Escenarios SEI

| Escenario SEI | Atributo Calidad Rector | Requisitos Funcionales Afectados | Decisión Arquitectónica (ADR) |
| :--- | :--- | :--- | :--- |
| **ESC-CON-001** | **AC02** (`RNF-CON-003`, `RNF-CON-001`) | `RF-INV-001`, `RF-POS-001`, `RF-WEB-002`, `RF-WEB-005` | **ADR-001**, **ADR-003** |
| **ESC-PERF-001** | **AC01** (`RNF-PERF-001`) | `RF-POS-003`, `RF-POS-004`, `RF-POS-006`, `RF-POS-011` | **ADR-003**, **ADR-004**, **ADR-007** |
| **ESC-PERF-002** | **AC01** (`RNF-PERF-004`) | `RF-WEB-001`, `RF-WEB-005` | **ADR-004**, **ADR-007** |
| **ESC-PERF-003** | **AC01** (`RNF-PERF-005`) | `RF-POS-001`, `RF-WEB-005` | **ADR-004** |
| **ESC-SEG-001** | **AC03** (`RNF-SEG-004`, `RNF-SEG-005`) | `RF-CAJ-002`, `RF-PRE-002`, `RF-WEB-001`, `RF-CAJ-001` | **ADR-005**, **ADR-008** |

---

## 4. Instrumental Técnico de Medición y Telemetría de Calidad

Para garantizar que los atributos de calidad rectores sean auditables y reproducibles conforme a los criterios de evaluación de **IS-488 UNSCH**, el sistema implementa el siguiente instrumental tecnológico especializado:

| Atributo Rector | Métrica Cuantitativa | Instrumental Técnico de Medición | Protocolo y Comando de Verificación | Criterio de Aceptación |
| :--- | :--- | :--- | :--- | :--- |
| **AC01: Rendimiento Transaccional** | Latencia de API en red LAN **$P95 < 250\text{ ms}$**; Cobro en mostrador $\le 2.0\text{ s}$. | **k6 (Grafana Labs)** ejecutando suites de pruebas de carga automatizadas. | Script `tests/k6/checkout-stress-test.js` evaluando umbral: `http_req_duration{quantile:0.95} < 250`. | Percentil 95 de duración HTTP $\le 250\text{ ms}$ con 100 usuarios virtuales (VUs). |
| **AC01: Latencia de Persistencia** | Consultas Dapper de lectura de catálogo **$< 15\text{ ms}$**. | **MiniProfiler** y exportadores de trazas **OpenTelemetry** en ASP.NET Core 8. | Interceptores `MiniProfiler.AspNetCore` y trazas OTLP hacia Jaeger/Prometheus inspeccionando tiempos de ejecución SQL. | 100 % de consultas de catálogo indexadas resueltas en tiempo de CPU/red $\le 15\text{ ms}$. |
| **AC02: Consistencia Transaccional** | **0 sobreventas** y 0 saldos negativos bajo 100 hilos concurrentes compitiendo por el mismo SKU. | Vistas del catálogo administrativo de PostgreSQL 16: `pg_stat_activity` y `pg_locks`. | Consultas continuas sobre `pg_locks` filtrando `locktype = 'relation'` y `mode = 'ExclusiveLock'`, verificando que las transacciones bloqueen ordenadamente por `producto_id ASC`. | Cero registros con `stock_fisico - stock_comprometido < 0`. Ausencia total de eventos de interbloqueo (*deadlocks*). |
| **AC03: Trazabilidad y Auditoría** | Inmutabilidad estricta **Append-Only**; 0 modificaciones retroactivas en arqueos contables. | Auditoría de permisos de base de datos SQL sobre el rol operativo `rol_cajero`. | Ejecución forzada de `UPDATE caja_arqueo SET total_efectivo_declarado = 0;` o `DELETE FROM caja_arqueo;` mediante script de prueba. | El motor PostgreSQL 16 rechaza inmediatamente la sentencia emitiendo el código de error `42501 permission denied for table caja_arqueo`. |

