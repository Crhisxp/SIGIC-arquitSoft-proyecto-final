# Drivers Arquitectónicos Rectores (SIGIC Ferretería)

> **Documento:** `analisis-de-sistema/06-driver-arquitectonicos.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Enfoque Metodológico:** Análisis de Requisitos Significativos para la Arquitectura (ASRs – *Architectural Significant Requirements*)  

---

## 1. Definición y Matriz de Fuerzas Arquitectónicas

Los **Drivers Arquitectónicos** representan el conjunto de requisitos funcionales críticos, atributos de calidad no negociables y restricciones técnicas/de negocio que moldean de manera determinante la estructura interna del software. En el caso de **SIGIC Ferretería**, la arquitectura debe resolver la tensión inherente entre dos dinámicas contrapuestas:
1. La necesidad de **atención presencial ultrarrápida y sub-segundo** en mostrador físico para evitar colas de maestros de obra.
2. La exposición de un **canal e-commerce B2B/B2C omnicanal abierto a Internet**, donde cientos de clientes remotos consultan, cotizan y compiten por el mismo inventario físico.

A continuación se analizan y justifican los **cinco drivers arquitectónicos rectores** del sistema:

---

## 2. Driver 1: Consistencia Transaccional Estricta de Inventario (ACID)

### Justificación de Negocio y Técnica
En el rubro ferretero de Huamanga, la sobreventa de materiales pesados y estructurales (como varillas de fierro corrugado o bolsas de cemento Portland) acarrea un perjuicio financiero directo, costos adicionales de transporte en flete y pérdida inmediata de confianza comercial con contratistas e ingenieros de obra.
- **Tensión Operativa:** Si el mostrador físico y el portal web leen únicamente un saldo estático de inventario, un cliente presencial y un usuario web pueden comprar en el mismo segundo las últimas unidades existentes, generando una rotura de stock crítica.
- **Mecanismo Arquitectónico de Solución:**
  1. **Modelo de Magnitudes Segregadas:**
     $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$
     El *Stock Físico* solo se debita ante un movimiento real de salida en el Kardex (venta confirmada o guía interna recibida). El *Stock Comprometido* acumula las reservas web activas con TTL y las guías de salida en tránsito.
  2. **Bloqueo Pesimista Fino a Nivel de Fila (*Row-Level Locking*):**
     Toda reserva o débito se realiza mediante la sentencia atómica:
     ```sql
     UPDATE inventario 
     SET comprometido = comprometido + :cantidad 
     WHERE almacen_id = :almacen_id 
       AND producto_id = :producto_id 
       AND (fisico - comprometido) >= :cantidad;
     ```
     Si la consulta afecta 0 filas, el sistema aborta la transacción inmediatamente e informa al cliente que el material se ha agotado.
  3. **Adquisición Determinista de Bloqueos:**
     Para transacciones compuestas por múltiples ítems, los registros de inventario se bloquean en **orden ascendente de ID de producto** (`ORDER BY producto_id ASC`), erradicando la posibilidad de bloqueos mutuos (*deadlocks*).

---

## 3. Driver 2: Prioridad de Rendimiento de la Venta Física en Mostrador

### Justificación de Negocio y Técnica
El mostrador físico es la principal fuente de ingresos inmediatos en efectivo de la ferretería. Una lentitud de 5 segundos al buscar un artículo o al procesar un cobro ocasiona aglomeraciones intolerables en la sala de ventas y reclamos del cliente presencial.
- **Tensión Operativa:** Picos de concurrencia en el portal web (campañas de descuentos, consultas simultáneas de cotizaciones por contratistas) generan una sobrecarga que, sin aislamiento, saturaría las conexiones de base de datos y la CPU del servidor central.
- **Mecanismo Arquitectónico de Solución:**
  1. **Segregación de Pools de Conexión en PgBouncer:**
     PgBouncer opera en modo transacción con dos pools estrictamente separados:
     - `pool_pos`: Reservado con conexiones dedicadas de máxima prioridad para terminales de mostrador, garantizando tiempos de respuesta sub-segundo ($p95 \le 1$ s en búsqueda y $\le 2$ s en confirmación).
     - `pool_web`: Limitado con cuotas de contención para el tráfico del e-commerce.
  2. **Desacoplamiento del Modelo de Lectura Web:**
     El catálogo público de Next.js SSR consulta un modelo de lectura optimizado con caché temporal de hasta 30 segundos, sin tocar las tablas transaccionales de inventario en mostrador.
  3. **Métrica Rectora de Degradación:**
     Bajo una prueba de estrés de 20 terminales POS y 200 sesiones web concurrentes (generando 50 req/s), la degradación del tiempo de cobro en mostrador debe ser estrictamente **$\le 10$ %**.

---

## 4. Driver 3: Aislamiento Operacional y Analítico (OLTP vs. OLAP)

### Justificación de Negocio y Técnica
La valorización del Kardex mensual con método de Costo Promedio Ponderado sobre más de 50 000 movimientos, así como los reportes de márgenes brutos y utilidades por línea de producto, demandan agregaciones matemáticas masivas sobre tablas de hechos históricas.
- **Tensión Operativa:** Si estas consultas analíticas se ejecutan directamente sobre las tablas operativas de PostgreSQL (`sigic_oltp`), provocarían bloqueos de lectura extendidos, consumo intensivo de memoria de trabajo (*work_mem*) y degradación crítica en las cajas de mostrador.
- **Mecanismo Arquitectónico de Solución:**
  1. **Esquema Estrella Segregado (`sigic_olap`):**
     Creación de un Datamart dimensional aislado con la tabla de hechos `Fact_Ventas` y dimensiones conformadas (`Dim_Tiempo`, `Dim_Producto`, `Dim_Cliente`, `Dim_Canal`, `Dim_Almacen`).
  2. **Pipeline ETL Asíncrono Desacoplado:**
     Proceso de extracción y transformación programado en ventana nocturna que consolida las ventas cerradas y reservas liberadas de ambos canales.
  3. **Alojamiento en Servidor y Almacenamiento Independientes:**
     El procesamiento OLAP reside en un servidor dedicado (4 vCPU, 16 GB RAM), asegurando que el motor OLTP mantenga su memoria *shared_buffers* enfocada exclusivamente en la transaccionalidad de ventas y reservas.

---

## 5. Driver 4: Desacoplamiento Operativo de Mostrador frente a Backoffice

### Justificación de Negocio y Técnica
En el comercio electrónico ferretero local, la gran mayoría de pagos no se procesa mediante pasarelas automatizadas, sino mediante billeteras digitales (Yape, Plin) y transferencias bancarias directas donde el cliente sube una fotografía o documento PDF de la constancia de abono (archivos de hasta 5 MB).
- **Tensión Operativa:** Si el cajero o despachador en el mostrador tuviera que abrir, inspeccionar y cotejar imágenes de comprobantes bancarios mientras atiende al público en fila, el flujo físico de despacho colapsaría y aumentaría el riesgo de fraudes por constancias falsificadas.
- **Mecanismo Arquitectónico de Solución:**
  1. **Consola Especializada de Backoffice (`RF-WEB-003`):**
     La revisión visual y el cotejo del código de operación bancario contra las cuentas corporativas recae exclusivamente en el Administrador o Asistente de Tesorería en un entorno desacoplado.
  2. **Entrega Ultraligera en Mostrador (`RF-POS-011`):**
     La terminal del despachador en mostrador solo muestra pedidos que ya alcanzaron el estado formal de `Listo para Retiro` (pago verificado previamente por Tesorería).
  3. **Operación de Despacho en Menos de 1 Minuto:**
     El despachador en mostrador no visualiza vouchers pesados ni valida transacciones bancarias; únicamente escanea el código del pedido (QR/barras), verifica el número de DNI del cliente y presiona `Confirmar Entrega`.

---

## 6. Driver 5: Dualidad de Identidad y Principio de Mínimo Privilegio

### Justificación de Negocio y Técnica
El sistema gestiona dos perfiles radicalmente distintos de usuarios: los colaboradores de la empresa que operan el negocio internamente y los clientes externos que navegan desde dispositivos móviles o computadoras personales a través de Internet.
- **Tensión de Seguridad:** Exponer la misma superficie de autenticación o los mismos tokens JWT a colaboradores y clientes públicos elevaría sustancialmente la superficie de ataque, exponiendo endpoints administrativos ante vectores de inyección o escalada de privilegios.
- **Mecanismo Arquitectónico de Solución:**
  1. **Dos Ámbitos de Emisión Criptográfica:**
     - **Colaboradores Internos:** Gestionados en el Directorio Central corporativo con RBAC estricto (`Cajero`, `Almacenero`, `Despachador`, `Administrador`). JWT firmados con clave privada RS256 y claims de roles internos.
     - **Clientes Web (Externos):** Gestionados en una tabla segregada con perfiles específicos (`Público General`, `Maestro de Obra`, `Contratista`). Tokens emitidos con audiencia pública restringida (`Audience: SIGIC_WebClient`).
  2. **Barrera en API Gateway / Middleware:**
     Cualquier intento de invocar endpoints de apertura de caja, movimientos de inventario o reportes administrativos con un token emitido para un cliente web es bloqueado de inmediato en el pipeline de ASP.NET Core con código `HTTP 403 Forbidden`, sin llegar siquiera a ejecutar lógica de aplicación.

---

## 7. Matriz de Síntesis: Drivers vs. Atributos de Calidad y Requerimientos

| Driver Arquitectónico | Requerimientos Funcionales Impactados | Atributos de Calidad Asociados (RNF) | Patrón Arquitectónico Implementado |
| :--- | :--- | :--- | :--- |
| **1. Consistencia ACID de Inventario** | `RF-INV-001`, `RF-INV-003`, `RF-WEB-002`, `RF-WEB-005` | `RNF-CON-001`, `RNF-CON-002`, `RNF-CON-003` | *Row-Level Locking*, Orden Determinista de Bloqueos, Actualizaciones Condicionadas. |
| **2. Prioridad Rendimiento Mostrador** | `RF-POS-001`, `RF-POS-003`, `RF-POS-004`, `RF-WEB-005` | `RNF-PERF-001`, `RNF-PERF-004`, `RNF-PERF-005` | *Connection Pooling* Segregado (PgBouncer), Modelo de Lectura Asíncrono en Catálogo. |
| **3. Aislamiento OLTP vs. OLAP** | `RF-REP-001`, `RF-REP-002`, `RF-REP-003`, `RF-REP-007` | `RNF-PERF-002`, `RNF-PERF-003` | *Database Segregation*, Esquema Estrella Dimensional, Pipeline ETL Nocturno. |
| **4. Desacoplamiento Backoffice vs. POS** | `RF-WEB-003`, `RF-POS-011`, `RF-WEB-004` | `RNF-PERF-001`, `RNF-SEG-004` | Consola Asíncrona de Validación, Flujo Ligero de Entrega por Código y DNI. |
| **5. Dualidad de Identidad y Mínimo Privilegio** | `RF-PRE-002`, `RF-WEB-001`, `RF-CAJ-001` | `RNF-SEG-001`, `RNF-SEG-003`, `RNF-SEG-005` | *Segregated Identity Domains*, JWT Asimétrico RS256, Middlewares de Claims RBAC. |
