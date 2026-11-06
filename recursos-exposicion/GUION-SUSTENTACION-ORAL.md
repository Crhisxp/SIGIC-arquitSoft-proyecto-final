---
id: "GUION-SUSTENTACION-ORAL"
doc: "recursos-exposicion/GUION-SUSTENTACION-ORAL.md"
version: "2.2.0"
sistema: "SIGIC - Sistema Integrado de Gestión de Inventario y Comercialización"
metodologia: "Spec-Driven Development (SDD)"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 Arquitectura de Software - UNSCH"
fecha: "2026-11-06"
tiempo-total: "15:00 minutos exactos"
---

# Guion Maestro de Sustentación y Defensa Oral — SIGIC Ferretería v2.2

> **Documento:** `recursos-exposicion/GUION-SUSTENTACION-ORAL.md`  
> **Expositor:** Crhistian Emanuel Flores Marquina  
> **Asignatura:** IS-488 Arquitectura de Software — UNSCH  
> **Tiempo Límite:** 15 minutos de presentación continua + 5 minutos de preguntas del jurado.  
> **Enfoque Pedagógico:** Discurso en primera persona con aplomo técnico. Cada lámina inicia exponiendo el **dolor comercial de la ferretería** y remata demostrando la **solución arquitectónica en código y métricas**.

---

## 1. Cronograma y Distribución de Tiempos (15 Minutos)

| Intervalo | Lámina | Tema Específico | Hito Clave que Debe Quedar Grabado en el Jurado |
| :---: | :---: | :--- | :--- |
| **00:00 - 01:00** | **01** | Portada y Propósito | Propósito omnicanal y alcance de más de 12,000 SKUs técnicos. |
| **01:00 - 02:15** | **02** | Problemática Real en Ayacucho | 150 ventas/día, materiales críticos (cemento/fierro) y el choque web vs. caja. |
| **02:15 - 03:30** | **03** | Monolito Modular en Clean Arch | Por qué no microservicios: latencia sub-15ms y consistencia ACID local. |
| **03:30 - 04:30** | **04** | Carga Masiva Transaccional | `RF-INV-008`: 250 productos en <5s con ClosedXML y COPY binario. |
| **04:30 - 06:00** | **05** | Gobernanza de Stock sin Sobreventas | Fórmula cardinal $\text{Disponible} = \text{Físico} - \text{Comprometido}$ y filtro 404. |
| **06:00 - 07:15** | **06** | Segregación de Conexiones PgBouncer | Aislamiento físico: 50 conexiones LAN reservadas vs 30 conexiones web. |
| **07:15 - 08:30** | **07** | Prevención Determinista de Deadlocks | Ordenamiento `OrderBy(p => p.ProductoId)` antes del `SELECT FOR UPDATE`. |
| **08:30 - 09:45** | **08** | Arqueo Ciego e Inmutabilidad | Seguridad Append-Only: excepción `PostgresException 42501` ante manipulación. |
| **09:45 - 11:15** | **09** | Estrés Concurrente con k6 | 100 usuarios virtuales, $P95 < 250\text{ ms}$ y 0% de errores internos 500. |
| **11:15 - 12:30** | **10** | CI/CD y Suite de Pruebas xUnit | Quality Gate en Python que bloquea el build si la cobertura es $< 95\%$. |
| **12:30 - 13:45** | **11** | Retorno de Inversión y Negocio | Ahorro del 80% en horas administrativas y cobro presencial en $< 2.0\text{ s}$. |
| **13:45 - 15:00** | **12** | Conclusiones y Cierre Formal | Rigor del Spec-Driven Development y cumplimiento total de IS-488. |

---

## 2. Libreto de Exposición Verbal en Primera Persona

---

### [00:00 - 01:00] Lámina 01: Portada y Propósito
> *"Buenos días, estimada docente y miembros del jurado evaluador. Mi nombre es Crhistian Emanuel Flores Marquina y el día de hoy presento la defensa arquitectónica de **SIGIC Ferretería versión 2.2**, un Sistema Integrado de Gestión de Inventario y Comercialización desarrollado bajo la metodología estricta **Spec-Driven Development (SDD)**.*
>
> *SIGIC nace para resolver un problema de misión crítica en el retail ferretero de alta rotación: gobernar un catálogo técnico superior a los 12,000 SKUs —desde pernos y tuberías hasta cemento y varillas de acero— unificando la venta física en mostrador con el portal de comercio electrónico, garantizando transacciones seguras, sin sobreventas y con latencia ultrabaja en ASP.NET Core 8 y PostgreSQL 16."*

---

### [01:00 - 02:15] Lámina 02: La Problemática Real en Ayacucho
> *"Para que una arquitectura sea válida, debe responder a la realidad empírica de su entorno. En nuestro levantamiento de campo en Huamanga, identificamos un negocio ferretero que emite **entre 120 y 180 comprobantes diarios por caja** en horas punta matutinas. Los maestros de obra y cuadrillas de construcción compran materiales pesados como Cemento Portland Tipo I y Fierro Corrugado de media pulgada.*
>
> *Aquí surge la primera fricción de negocio: **el conflicto de las 40 bolsas de cemento**. Un cliente en Huanta reserva 40 bolsas desde el portal web a las 09:15; un minuto después, un contratista se acerca a la ventanilla intentando pagar en efectivo las últimas 40 bolsas físicas de la tienda. Si el software solo mirara el stock físico en vitrina, ambos comprarían el mismo material, paralizando una obra en construcción y exponiendo a la empresa a severas penalidades.*
>
> *A esto se sumaban descuadres continuos por arqueos de caja en papel y la pérdida de hasta 4 horas digitando guías de remisión manuales."*

---

### [02:15 - 03:30] Lámina 03: Arquitectura General (Monolito Modular)
> *"Frente a este escenario, la primera gran decisión de arquitectura fue: **¿Monolito Modular o Microservicios?** Adoptamos con firmeza un **Monolito Modular estructurado en Clean Architecture y Domain-Driven Design**.*
>
> *¿Por qué descartamos microservicios? Porque en un punto de venta físico de mostrador, donde el cliente espera que el código de barras se procese en menos de un segundo, introducir latencia de red entre microservicios y transacciones distribuidas en dos fases (2PC) aumentaría los puntos de fallo y multiplicaría los costos operativos innecesariamente.*
>
> *Descompusimos el sistema en 5 Bounded Contexts cohesivos: Inventario, Ventas POS, Pedidos Web, Caja/Tesorería y Tarifas. Se comunican en memoria mediante MediatR, asegurando consultas Dapper sub-15ms y transacciones ACID locales dentro de PostgreSQL 16."*

---

### [03:30 - 04:30] Lámina 04: Carga Masiva Transaccional (`RF-INV-008`)
> *"Para resolver el dolor administrativo de los camiones de distribuidores con cientos de ítems, diseñamos el requerimiento **RF-INV-008: Carga e Importación Masiva vía Excel y CSV**.*
>
> *En lugar de cargar pesados DataSets en memoria que saturen la RAM del servidor, implementamos procesamiento en streaming mediante `ClosedXML` y `ExcelDataReader` en .NET 8. El sistema valida fila por fila en memoria la existencia de familias, unicidad de SKU y una regla comercial inquebrantable: el precio de venta debe superar el costo de compra, impidiendo ventas a pérdida.*
>
> *La persistencia se ejecuta como un bloque atómico con PostgreSQL COPY. Si una sola fila del archivo de 250 artículos está corrupta, se dispara un **ROLLBACK total**: cero registros ingresados a medias y un informe JSON detallando fila, columna y motivo del rechazo."*

---

### [04:30 - 06:00] Lámina 05: Gobernanza de Stock y Cero Sobreventas
> *"Pasemos al corazón transaccional del sistema: la **Gobernanza de Stock** definida en el atributo **AC02** y requerimiento **RNF-CON-003**.*
>
> *La regla matemática cardinal que gobierna todo nuestro código es:*
> $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$
>
> *Cuando el cliente web reserva materiales, no descuenta el inventario físico de inmediato; en su lugar, incrementa el `stock_comprometido` y asigna un TTL dinámico. Por ende, la terminal de caja POS consulta el disponible real y sabe al milisegundo que esas bolsas ya no están a la venta.*
>
> *Asimismo, protegemos el negocio segregando el catálogo: artículos como pernos a granel o cortes de cable tienen `disponible_en_web = false`. Si alguien intenta forzar la API pública, el servidor retorna un `HTTP 404 Not Found` absoluto, protegiendo las existencias exclusivas de tienda."*

---

### [06:00 - 07:15] Lámina 06: Segregación de Conexiones con PgBouncer
> *"Para asegurar que el mostrador físico nunca sufra colas ni demoras (**AC01**), implementamos la **segregación de conexiones mediante PgBouncer** en modo de transacción.*
>
> *Configuramos dos pools físicamente aislados:  
> Primero, `sigic_pos_pool` con **50 conexiones dedicadas y reservadas** para las terminales de red local privada (segmento `192.168.1.0/24`), autenticadas con JWT RS256 con claim `scope: pos_terminal`.*
>
> *Segundo, `sigic_web_pool` con **30 conexiones compartidas** y rate limiting estricto de 60 peticiones por minuto por IP.*
>
> *¿Qué logramos con esto? Si un bot o una campaña masiva en internet genera una avalancha de 500 visitas simultáneas en el e-commerce, el pool web aplica contención (backpressure) y responde timeouts; pero el cajero en la ferretería física sigue facturando a 150 milisegundos sin percibir la más mínima degradación."*

---

### [07:15 - 08:30] Lámina 07: Prevención Determinista de Deadlocks
> *"Uno de los mayores riesgos en bases de datos con concurrencia es el interbloqueo o **Deadlock**.*
>
> *Imaginen que la Caja 1 vende Cemento y Fierro en ese orden, mientras la Web procesa un pedido de Fierro y Cemento simultáneamente. En PostgreSQL tradicional, ambas transacciones se bloquearían mutuamente, arrojando el error 40P01.*
>
> *En SIGIC Ferretería v2.2 erradicamos los deadlocks desde la raíz algorítmica: antes de tocar la base de datos, el orquestador MediatR toma la lista de artículos y ejecuta obligatoriamente un ordenamiento en memoria ascendente: `.OrderBy(p => p.ProductoId)`. Al adquirir los cerrojos con `SELECT ... FOR UPDATE` siempre en orden estrictamente correlativo, la espera circular es matemáticamente imposible. Cero deadlocks garantizados."*

---

### [08:30 - 09:45] Lámina 08: Arqueo Ciego e Inmutabilidad Append-Only
> *"En el aspecto de seguridad y control interno (**AC03**), transformamos el cierre de caja tradicional en un **Arqueo Ciego e Inmutable**.*
>
> *Al cerrar turno, la pantalla no muestra las ventas acumuladas ni el saldo del sistema. El cajero está obligado a ingresar el conteo físico real billete por billete y moneda por moneda.*
>
> *Pero el verdadero mérito de ingeniería está en la persistencia: la tabla `caja_arqueo` es estrictamente **Append-Only**. A nivel de motor relacional en PostgreSQL revocamos los privilegios `UPDATE` y `DELETE`. Si un usuario o atacante intenta modificar un arqueo para ocultar un faltante, el driver Npgsql captura de inmediato una excepción `PostgresException 42501 permission denied`. El historial queda blindado para auditorías internas y SUNAT."*

---

### [09:45 - 11:15] Lámina 09: Métricas de Estrés Concurrente con k6
> *"Para demostrar con rigor empírico que nuestra arquitectura resiste la carga, sometimos al sistema a pruebas de estrés automatizadas con **k6**.*
>
> *Ejecutamos un script con **100 usuarios virtuales concurrentes durante 30 segundos continuos** bombardeando el endpoint de checkout con payloads multilínea de cemento y fierro.*
>
> *Los resultados validados por el instrumental técnico fueron contundentes:  
> • Percentil 95 de latencia HTTP inferior a 250 milisegundos (`http_req_duration{p95} < 250ms`).  
> • Exactamente cero errores internos de servidor: **tasa de error HTTP 500 = 0%**.  
> • Y lo más importante: **cero unidades de sobreventa**; cuando el stock disponible se agotó, el sistema rechazó limpiamente con `HTTP 409 Conflict`, preservando la consistencia ACID."*

---

### [11:15 - 12:30] Lámina 10: CI/CD y Suite de Pruebas xUnit
> *"En la industria del software moderno, la calidad no es un deseo, es un mecanismo automatizado. En nuestro repositorio configuramos un pipeline de integración continua en **GitHub Actions** que compila en Release con `-warnaserror` y ejecuta nuestra suite de pruebas de dominio y concurrencia.*
>
> *No nos conformamos con pruebas genéricas: programamos un **Quality Gate bloqueante en Python** que inspecciona el archivo `coverage.cobertura.xml` y cancela el despliegue si la cobertura de líneas cae por debajo del 95%.*
>
> *Actualmente, nuestras 13 pruebas xUnit certifican una **cobertura de líneas del 97.83%** y una cobertura de ramas del **96.77%**, validando las excepciones de margen comercial, el cálculo de inventario disponible y el arqueo ciego."*

---

### [12:30 - 13:45] Lámina 11: Retorno de Inversión y Valor de Negocio
> *"¿Qué significa toda esta ingeniería en el día a día de la ferretería?  
> En primer lugar, **cero clientes insatisfechos y cero penalidades** por ventas de mercadería inexistente.  
> En segundo lugar, **agilidad total en ventanilla**: un cajero atiende 100% por teclado y lector láser, totalizando ventas en menos de 2 segundos.  
> En tercer lugar, **eficiencia operativa medible**: redujimos en un 80% el tiempo dedicado a registrar guías de proveedores gracias a la carga masiva en streaming.  
> La arquitectura de software no es un fin en sí misma; es el motor que optimiza la rentabilidad y protege el capital de la empresa."*

---

### [13:45 - 15:00] Lámina 12: Conclusiones y Cierre Formal
> *"En conclusión: SIGIC Ferretería v2.2 demuestra cómo el **Desarrollo Guiado por Especificaciones (SDD)** permite construir sistemas confiables, predecibles y de alto rendimiento. Cada requerimiento de la rúbrica de IS-488 UNSCH está respaldado por especificaciones formales, código limpio en .NET 8, esquemas DDL relacionales y pruebas reproducibles.*
>
> *Agradezco a la cátedra por la exigencia metodológica a lo largo del semestre y quedo a entera disposición del jurado evaluador para sus preguntas técnicas. Muchas gracias."*

---

## 3. Banco de Preguntas Trampa del Jurado y Respuestas Infalibles

---

### Pregunta 1: *"¿Por qué eligieron un monolito modular en lugar de una arquitectura de microservicios, que es el estándar actual en la industria?"*

> **RESPUESTA INFALIBLE DEL ESTUDIANTE:**
> *"Profesora, esa es una excelente pregunta de evaluación arquitectónica. Elegir microservicios para esta ferretería habría sido un grave error de sobreingeniería (*over-engineering*). Los microservicios introducen tres costos críticos:  
> 1. **Latencia de Red:** Cada llamada RPC o HTTP entre servicios añade entre 10 y 50 milisegundos, lo que violaría nuestro atributo **AC01** de facturar en mostrador presencial en menos de 2 segundos.  
> 2. **Transaccionalidad Distribuida:** Para evitar sobreventas entre inventario y ventas en microservicios tendríamos que implementar patrones como Saga o Two-Phase Commit (2PC), que sufren de consistencia eventual y ventanas de vulnerabilidad donde dos clientes pueden comprar el mismo cemento.  
> 3. **Costo Operativo:** Una ferretería regional no justifica la complejidad de despliegue de Kubernetes ni mallas de servicios (service meshes).  
> Nuestro **Monolito Modular** brinda exactamente el mismo desacoplamiento conceptual con Bounded Contexts y DDD, pero con la velocidad de llamadas en memoria y la garantía absoluta de transacciones ACID locales en PostgreSQL 16."*

---

### Pregunta 2: *"¿Qué sucede si un cliente web reserva mercadería pero se le cae el internet mientras está pagando? ¿Ese stock queda bloqueado para siempre?"*

> **RESPUESTA INFALIBLE DEL ESTUDIANTE:**
> *"No, profesora, el sistema contempla específicamente esa contingencia mediante la **asignación de TTL dinámico gobernada por el requerimiento RF-WEB-002**.  
> Cuando el cliente inicia el checkout con pasarela de tarjeta, el sistema incrementa el `stock_comprometido` pero asigna una reserva con un TTL exacto de **20 minutos**.  
> En el backend opera en segundo plano el worker `ReservaExpirationWorker`, un servicio `IHostedService` que consulta cada 60 segundos las reservas cuya fecha de expiración haya sido superada sin confirmación de pago.  
> Si el cliente perdió conexión, el worker ejecuta un comando idempotente que decrementa el `stock_comprometido`, devolviendo esas existencias al `StockDisponible` del mostrador físico automáticamente. El stock nunca queda congelado."*

---

### Pregunta 3: *"¿Por qué no utilizar simplemente Entity Framework Core con concurrencia optimista mediante tokens de fila (`xmin` o rowversion) en lugar de Dapper y bloqueos pesimistas `SELECT ... FOR UPDATE`?"*

> **RESPUESTA INFALIBLE DEL ESTUDIANTE:**
> *"Esa es una decisión de diseño fundamental que evaluamos en el **ADR-003**. La concurrencia optimista funciona muy bien cuando la probabilidad de colisión es baja (pocas personas compran el mismo artículo al mismo tiempo).  
> Sin embargo, en una ferretería de materiales de construcción, productos como el Cemento Portland Tipo I o el Fierro de 1/2 pulgada son **artículos calientes (*hotspots*)** con altísima contención en horas punta.  
> Si usáramos concurrencia optimista, ante 100 usuarios compitiendo, 1 ganaría y 99 lanzarían `DbUpdateConcurrencyException`. El cliente tendría que reintentar, consumir CPU inútilmente y sufrir una pésima experiencia de usuario.  
> Con el bloqueo pesimista ordenado (`SELECT ... FOR UPDATE` ordenado por `producto_id ASC`), serializamos la adquisición de cerrojos limpiamente a nivel de fila en PostgreSQL. Quienes alcanzan stock compran con certeza en menos de 250 ms, y quienes no, son rechazados atómicamente con `HTTP 409 Conflict`, sin desperdiciar cómputo en reintentos fallidos."*

---

### Pregunta 4: *"¿Cómo demostraron empíricamente que en su sistema no ocurren sobreventas? ¿En qué evidencia técnica se basan?"*

> **RESPUESTA INFALIBLE DEL ESTUDIANTE:**
> *"Lo demostramos en dos niveles de verificación complementarios:  
> 1. **Nivel Unitario y de Dominio:** En la suite de pruebas xUnit ([`ProductoTests.cs`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/tests/SIGIC.Domain.Tests/ProductoTests.cs)), evaluamos el caso de prueba `ValidarDisponibilidad_ConcurrenciaMostradorVsWeb()`. Simulamos la condición de carrera de las 40 bolsas y verificamos que el método de dominio rechace la operación lanzando `StockInsuficienteException` en cuanto la cantidad solicitada excede la resta `StockFisico - StockComprometido`.  
> 2. **Nivel de Estrés de Infraestructura:** Ejecutamos el script de k6 ([`checkout-stress-test.js`](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/tests/k6/checkout-stress-test.js)) lanzando 100 usuarios virtuales simultáneos compitiendo por un saldo limitado de 5 unidades. El resultado en PostgreSQL demostró que exactamente 5 transacciones resultaron en código HTTP 200/201 y las 95 restantes en HTTP 409, con saldo final en inventario exactamente en cero, demostrando matemáticamente cero sobreventas."*

---

### Pregunta 5: *"¿Por qué afirman que el arqueo de caja es inmutable si cualquier usuario con permisos en la base de datos podría hacer un UPDATE?"*

> **RESPUESTA INFALIBLE DEL ESTUDIANTE:**
> *"Porque aplicamos el principio de privilegio mínimo a nivel de DDL en PostgreSQL 16. La aplicación ASP.NET Core se conecta mediante el usuario operativo `sigic_app_user`.  
> En el script de inicialización de la base de datos ejecutamos de forma explícita:  
> `REVOKE UPDATE, DELETE ON caja_arqueo FROM sigic_app_user;`  
> `REVOKE UPDATE, DELETE ON caja_arqueo_denominacion FROM sigic_app_user;`  
> De este modo, aunque un desarrollador o atacante intente inyectar una sentencia `UPDATE` a través de la API, el motor de PostgreSQL 16 aborta la transacción y arroja el código de error `42501 (insufficient_privilege)`. Ni siquiera la aplicación tiene permiso para modificar o borrar un arqueo ya insertado."*
