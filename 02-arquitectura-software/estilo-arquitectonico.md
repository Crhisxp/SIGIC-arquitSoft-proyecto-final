---
id: "02-estilo-arquitectonico"
doc: "02-arquitectura-software/estilo-arquitectonico.md"
version: "2.2.0"
estado: "VALIDADO_PARA_EXPOSICION"
tipo: "Estilo Arquitectónico Global"
proyecto: "SIGIC-Ferreteria-v2.2"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 UNSCH"
fecha: "2026-10-30"
---

# Estilo Arquitectónico Global: Monolito Modular en Capas (Cliente-Servidor)

> **Documento:** `02-arquitectura-software/estilo-arquitectonico.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Disciplina:** Arquitectura de Software y Diseño de Sistemas Distribuidos  
> **Metodología y Estándar:** Alineado a la Guía 03 (Estilo Arquitectónico, Justificación Técnica, Reglas Estructurales y Topología Mermaid)  
> **Evaluación Académica:** IS-488 Arquitectura de Software (UNSCH)

---

## 1. Justificación del Estilo Seleccionado

Para dar respuesta rigurosa a los desafíos de concurrencia omnicanal, consistencia de inventario y mantenibilidad en **SIGIC Ferretería v2.2**, se adopta formalmente el estilo arquitectónico de **Monolito Modular con Clean Architecture bajo el Paradigma Cliente-Servidor Desacoplado**.

### 1.1. Monolito Modular con Clean Architecture frente a Microservicios
Frente a una aproximación basada en microservicios independientes, la adopción del Monolito Modular resuelve de raíz las siguientes fricciones técnicas y operativas:

| Criterio de Decisión | Arquitectura de Microservicios | Monolito Modular en SIGIC v2.2 | Justificación Técnica de la Elección |
| :--- | :--- | :--- | :--- |
| **Consistencia Transaccional (AC02)** | Consistencia Eventual (*Eventual Consistency*) mediante patrones Saga o Two-Phase Commit (2PC). | **Transacciones ACID Inmediatas** sobre una única instancia relacional PostgreSQL 16. | En una ferretería de materiales pesados (cemento, fierro de construcción), la consistencia eventual genera ventanas críticas de sobreventa cuando un cliente web reserva y un cajero factura en ventanilla simultáneamente. |
| **Latencia Inter-Módulo (AC01)** | 20 a 50 ms por llamada de red (HTTP/gRPC) entre servicios distribuidos. | **$< 1\text{ ms}$** mediante llamadas tipadas directas en memoria del proceso Kestrel. | Garantiza tiempo de respuesta en mostrador físico $P95 \le 1.0\text{ s}$ y $P95 < 250\text{ ms}$ en la API Core sin degradación de red LAN. |
| **Sobrecarga Operacional (DevOps)** | Múltiples repositorios, clúster Kubernetes, service mesh, tracing distribuido y monitoreo fragmentado. | **Un solo pipeline CI/CD**, un solo artefacto ejecutable desplegado en systemd/Docker. | Se ajusta con precisión al tamaño del equipo de ingeniería y presupuesto de una mediana empresa comercial en Ayacucho. |
| **Integridad de Datos y Auditoría (AC03)** | Dificultad para coordinar bitácoras distribuidas entre múltiples bases de datos. | **Transactional Outbox y Bitácora Unificada** (`Audit_Log`, `caja_arqueo` *append-only*). | Cero desfase entre el registro contable y la mutación física de stock en la misma transacción relacional. |

---

### 1.2. Justificación Técnica de la Pila Tecnológica Seleccionada

#### A. ASP.NET Core 8 Web API (Backend Monolítico Modular)
- **Runtime Kestrel de Ultra-Alto Rendimiento:** Capaz de procesar miles de peticiones HTTP concurrentes con consumo mínimo de CPU y memoria, optimizado con C# 12 y recolección de basura (*Server GC*) para baja latencia.
- **Implementación Estricta de Clean Architecture:** Estructuración concéntrica (`Domain`, `Application`, `Infrastructure`, `Presentation`) con el patrón Mediador (**MediatR**) que orquesta comandos y consultas (CQRS), aplicando comportamientos de validación declarativa con **FluentValidation**.
- **Acceso a Datos Híbrido:** Combinación sinérgica de **Entity Framework Core 8** para transacciones de escritura complejas con seguimiento de entidades, y **Dapper Micro-ORM** para consultas de catálogo y reportes de lectura ultrarrápidos ($< 15\text{ ms}$).

#### B. PostgreSQL 16 con PgBouncer (Motor Transaccional y Pooler Segregado)
- **Aislamiento ACID Robusto:** Soporte nativo para bloqueos pesimistas a nivel de fila (`SELECT ... FOR UPDATE`) con cláusulas `SKIP LOCKED` y tipos de datos avanzados (`numeric(12,2)`, `jsonb`, `timestamptz`).
- **Segregación de Conexiones mediante PgBouncer:** Configurado en modo transacción (*Transaction Pooling*) con dos pools aislados:
  - `pool_pos`: 50 conexiones dedicadas de alta prioridad para mostrador físico y LAN.
  - `pool_web`: 30 conexiones limitadas para el tráfico e-commerce, aplicando control de contención (*backpressure*) para que ningún pico de tráfico web sature la base de datos de las tiendas.

#### C. Portal Web en Next.js 14 (App Router y React Server Components)
- **Renderizado del Lado del Servidor (SSR):** Optimización para motores de búsqueda (SEO) de materiales de construcción y tiempos de carga interactiva $LCP \le 1.5\text{ s}$ en redes móviles 4G.
- **Desacoplamiento Total:** El portal web actúa como un cliente HTTP/REST externo; no posee lógica de cálculo comercial ni acceso directo a la base de datos, consumiendo exclusivamente la superficie de API pública (`/api/v1/public/*`).

#### D. Terminal Mostrador POS en SPA React 18
- **Ergonomía de Misión Crítica:** Aplicación SPA servida en red LAN, diseñada para monitores de $1024 \times 768$ píxeles sin scroll horizontal (`RNF-USA-002`), operada al 100% mediante atajos de teclado (`F1`-`F12`) y escáner láser sin tocar el ratón (`RF-POS-003`).

---

## 2. Reglas Cardinales de la Arquitectura

Para salvaguardar la integridad del sistema, prevenir el acoplamiento caótico y garantizar la evolución a largo plazo, se establecen tres reglas de observancia obligatoria:

### Regla 1: Comunicación Estricta entre Capas e Inversión de Dependencias
- La solución backend se rige bajo los principios de **Clean Architecture**:
  $$\text{Presentation} \longrightarrow \text{Infrastructure} \longrightarrow \text{Application} \longrightarrow \text{Domain}$$
- Las dependencias de código fuente solo apuntan hacia adentro. La capa de **Dominio** constituye el núcleo puro y no posee referencias a frameworks, bibliotecas externas ni bases de datos.
- Todo acceso a infraestructura periférica (acceso a base de datos relacional, pasarela de pago web, servicios del Proveedor de Servicios Electrónicos SUNAT - PSE/OSE, storage privado) debe realizarse a través de interfaces y abstracciones (puertos) inyectadas mediante el contenedor de Inversión de Control de ASP.NET Core (`IServiceCollection`).

### Regla 2: Límites Lógicos Modulares Estrictos y Encapsulamiento de Datos
- Cada módulo funcional (`Inventario`, `VentasPOS`, `PedidosWeb`, `Precios`, `Caja`, `Seguridad`) posee un *Bounded Context* rigurosamente demarcado.
- **Prohibición de acceso cruzado directo a tablas:** Queda terminantemente prohibido que el código de un módulo (por ejemplo, `PedidosWeb`) ejecute consultas SQL directas o instancie entidades del `DbContext` pertenecientes a otro módulo (por ejemplo, `Caja` o `Kardex`).
- La interacción inter-módulo se efectúa exclusivamente mediante:
  1. **Servicios de Aplicación o Contratos de Interfaz Pública:** Métodos tipados expuestos intencionalmente por el módulo receptor (ej. `IInventarioReservaService`).
  2. **Eventos de Dominio en Memoria:** Notificaciones asíncronas dentro del mismo proceso (vía mediadores en memoria) para efectos colaterales (ej. `VentaPOSCompletadaEvent` que desencadena la salida del Kardex y la actualización del saldo de caja).

### Regla 3: Persistencia Transaccional Unificada con Pools Segregados en PgBouncer
- El almacenamiento transaccional central reside en una única instancia de **PostgreSQL 16** (`sigic_oltp`), complementada con un Datamart analítico segregado (`sigic_olap`) para reportería y balance mensual.
- Para evitar que los picos de tráfico de usuarios concurrentes navegando el portal e-commerce saturen las conexiones de la base de datos y bloqueen la facturación del mostrador físico, se interpone **PgBouncer** en modo de asignación por transacción (*Transaction Pooling*), gestionando dos pools estrictamente segregados:
  - **`pool_pos` (Prioritario / LAN):** Conexiones reservadas y garantizadas de alta prioridad para las cajas y almacén físico, con tiempo de espera de conexión prácticamente nulo.
  - **`pool_web` (Contenido / Internet):** Pool con cuota estricta de conexiones concurrentes para las solicitudes públicas del portal e-commerce, protegiendo al núcleo del sistema de cualquier ataque de denegación de servicio o degradación por sobretráfico web.

---

## 3. Diagrama de Arquitectura Global

El siguiente diagrama modela la arquitectura integral de **SIGIC Ferretería v2.2**, reflejando la interacción completa entre actores, frontends segregados, proxy perimetral, la estructura modular en capas del backend ASP.NET Core 8, la capa de persistencia administrada con PgBouncer, el storage privado y las integraciones con sistemas externos:

```mermaid
graph TD
    %% =======================================================
    %% CAPA DE ACTORES DEL SISTEMA
    %% =======================================================
    subgraph ACTORES ["Actores del Negocio Ferretero"]
        ACT_CAJ["Cajero Mostrador"]
        ACT_DES["Despachador Almacén"]
        ACT_ADM["Administrador / Tesorería"]
        ACT_CLI["Clientes Web (Público / Maestro de Obra / Contratista)"]
    end

    %% =======================================================
    %% CAPA DE CLIENTES FRONTEND (SEGREGADOS)
    %% =======================================================
    subgraph FRONTENDS ["Capa de Clientes Frontend (Desacoplados)"]
        SPA_POS["SPA Mostrador POS (React 18)<br/>- Atajos Teclado F1-F12<br/>- Escáner Óptico USB/LAN<br/>- Impresión ESC/POS"]
        WEB_APP["Portal Web E-commerce (Next.js 14 SSR)<br/>- Renderizado Híbrido / SEO<br/>- Catálogo Interactivo<br/>- Checkout Responsivo"]
    end

    %% =======================================================
    %% CAPA DE PROXY INVERSO Y PERÍMETRO
    %% =======================================================
    subgraph PERIMETRO ["Capa Perimetral y Enrutamiento"]
        NGINX["Proxy Inverso Nginx<br/>- Terminación TLS (HTTPS)<br/>- Rate Limiting por IP<br/>- Compresión Gzip/Brotli<br/>- Caché de Catálogo Estático"]
    end

    %% =======================================================
    %% CAPA BACKEND MONOLITO MODULAR (ASP.NET CORE 8)
    %% =======================================================
    subgraph MONOLITO ["Backend Monolito Modular (ASP.NET Core 8 Web API)"]
        
        %% Middleware / Presentación
        subgraph CAPA_PRES ["Capa de Presentación y Middlewares"]
            MID_AUTH["Middleware de Seguridad<br/>(Validación JWT RS256 / RBAC)"]
            MID_RATE["Rate Limiting & Correlation ID"]
            MID_VAL["Filtros de Validación (FluentValidation)"]
            MID_LOG["Serilog / Registro de Errores Global"]
            CTRL_POS["Controladores API Internos<br/>(/api/v1/internal/*)"]
            CTRL_WEB["Controladores API Públicos<br/>(/api/v1/public/*)"]
            CTRL_ADM["Controladores Backoffice<br/>(/api/v1/internal/backoffice/*)"]
        end

        %% Módulos Funcionales Lógicos (Bounded Contexts)
        subgraph MODULOS ["Lógica de Negocio y Aplicación por Módulos (MediatR CQRS)"]
            MOD_INV["Módulo Catálogo e Inventario<br/>- Control Kardex Ponderado (CPP)<br/>- Fórmulas Disponible/Comprometido<br/>- Bloqueo Pesimista (FOR UPDATE)"]
            MOD_POS["Módulo POS y Caja<br/>- Emisión Rápida de Comprobantes<br/>- Apertura y Arqueo Ciego de Caja<br/>- Despacho Inmediato en Tienda"]
            MOD_WEB["Módulo PedidosWeb y Reservas<br/>- Reserva Dinámica con TTL (20m / 4h)<br/>- Tracking de Estados de Retiro<br/>- Worker de Liberación Automática"]
            MOD_PRC["Módulo Precios y Descuentos<br/>- Escalas: Minorista / Maestro / B2B<br/>- Conversión de Unidades (Bulto/Kg)<br/>- Matriz de Promociones Vigentes"]
            MOD_PAG["Módulo Backoffice Pagos<br/>- Validación de Vouchers Yape/Plin<br/>- Conciliación de Transferencias<br/>- Notificación de Estado de Pago"]
            MOD_SEG["Módulo Seguridad y Auditoría<br/>- Emisión JWT Asimétrico (RS256)<br/>- Políticas RBAC por Rol<br/>- Bitácora Inmutable de Operaciones"]
        end

        %% Capa de Persistencia y Acceso a Datos
        subgraph CAPA_ACCESO ["Capa de Persistencia y Acceso a Datos (Clean Architecture)"]
            EF_CORE["Entity Framework Core 8<br/>- Mapeo ORM por Esquemas<br/>- Repositorios e Interfaces Unit of Work<br/>- Dapper para Reportes de Alta Frecuencia"]
        end
    end

    %% =======================================================
    %% CAPA DE PERSISTENCIA Y BASES DE DATOS
    %% =======================================================
    subgraph PERSISTENCIA ["Capa de Persistencia Relacional, Analítica y Storage"]
        subgraph GESTOR_POOLS ["Pooler de Conexiones (PgBouncer en Modo Transacción)"]
            PGB_POS["pool_pos (Prioritario LAN)<br/>- 50 Conexiones Garantizadas POS/Caja<br/>- Cero Espera en Mostrador"]
            PGB_WEB["pool_web (Contenido Internet)<br/>- 30 Conexiones Web Limitadas<br/>- Aislamiento Frente a Sobrecarga"]
        end
        
        DB_OLTP[("PostgreSQL 16 OLTP<br/>(sigic_oltp)<br/>- Esquema Transaccional ACID<br/>- Tablas Particionadas / Kardex<br/>- Índices B-Tree y GiST")]
        DB_OLAP[("PostgreSQL 16 OLAP<br/>(sigic_olap - Datamart)<br/>- Esquema Estrella (Fact_Ventas)<br/>- ETL Nocturno Asíncrono")]
        STORAGE_PRIV[("Storage Privado de Archivos<br/>- Vouchers Bancarios <= 5MB<br/>- Facturas XML/CDR SUNAT<br/>- Acceso Restringido")]
    end

    %% =======================================================
    %% SISTEMAS EXTERNOS E INTEGRACIONES
    %% =======================================================
    subgraph EXTERNOS ["Sistemas Externos y Proveedores"]
        EXT_PAY["Pasarela de Pagos Online<br/>(Webhooks Seguros / HMAC)"]
        EXT_PSE["Proveedor de Servicios Electrónicos<br/>(PSE / OSE SUNAT - Facturación)"]
    end

    %% =======================================================
    %% FLUJOS Y CONEXIONES ENTRE COMPONENTES
    %% =======================================================
    
    %% Actores a Clientes
    ACT_CAJ -->|Operación de Ventas / Red LAN| SPA_POS
    ACT_DES -->|Validación de Entrega y Guías| SPA_POS
    ACT_ADM -->|Gestión y Conciliación de Pagos| SPA_POS
    ACT_CLI -->|Búsqueda y Compra HTTPS| WEB_APP

    %% Clientes a Perímetro Nginx
    SPA_POS -->|Peticiones REST Internas / JSON| NGINX
    WEB_APP -->|Peticiones REST Públicas / SSR| NGINX

    %% Nginx a Middlewares Backend
    NGINX -->|Enrutamiento y Reverse Proxy| MID_AUTH
    MID_AUTH --> MID_RATE
    MID_RATE --> MID_VAL
    MID_VAL --> MID_LOG

    %% Middlewares a Controladores
    MID_LOG --> CTRL_POS
    MID_LOG --> CTRL_WEB
    MID_LOG --> CTRL_ADM

    %% Controladores a Módulos Lógicos
    CTRL_POS --> MOD_POS
    CTRL_POS --> MOD_INV
    CTRL_WEB --> MOD_WEB
    CTRL_WEB --> MOD_PRC
    CTRL_ADM --> MOD_PAG
    CTRL_ADM --> MOD_INV
    MID_AUTH -.->|Autoriza vía| MOD_SEG

    %% Interacción entre Módulos Internos
    MOD_POS -->|Consulta Tarifas| MOD_PRC
    MOD_POS -->|Descuenta Stock ACID| MOD_INV
    MOD_WEB -->|Aplica Precios Especiales| MOD_PRC
    MOD_WEB -->|Genera Reserva Temporal| MOD_INV
    MOD_PAG -->|Confirma Pago y Reserva| MOD_WEB

    %% Módulos a Capa de Acceso a Datos (EF Core 8)
    MOD_INV --> EF_CORE
    MOD_POS --> EF_CORE
    MOD_WEB --> EF_CORE
    MOD_PRC --> EF_CORE
    MOD_PAG --> EF_CORE
    MOD_SEG --> EF_CORE
    MOD_WEB --> STORAGE_PRIV

    %% EF Core a Pools de PgBouncer
    EF_CORE -->|Conexiones POS/Almacén| PGB_POS
    EF_CORE -->|Conexiones Portal Web| PGB_WEB

    %% Pools a Base de Datos OLTP
    PGB_POS -->|Conexiones Dedicadas| DB_OLTP
    PGB_WEB -->|Conexiones Limitadas| DB_OLTP

    %% ETL entre OLTP y OLAP
    DB_OLTP -.->|Extracción Nocturna ETL Asíncrona| DB_OLAP

    %% Integraciones Externas
    MOD_POS -->|Envío de Comprobantes XML/JSON| EXT_PSE
    EXT_PAY -->|Webhook de Notificación de Pago| CTRL_WEB
    MOD_WEB -->|Confirmación de Cobro Tarjeta| EXT_PAY
```

---

## 4. Trazabilidad con Decisiones de Arquitectura y Atributos de Calidad

| Elemento Estructural | Justificación Técnica en SIGIC Ferretería v2.2 | Decisión / Requisito Asociado | Atributo de Calidad |
| :--- | :--- | :--- | :--- |
| **Monolito Modular ASP.NET Core 8** | Evita latencias inter-nodo y garantiza transacciones ACID unificadas sobre el inventario compartido. | `ADR-001`, `ADR-002`, `DA-01`, `DA-06` | **AC01**, **AC02** |
| **Doble Frontend (React 18 / Next.js 14)** | Interfaz ultrarrápida para escáner/teclado en LAN mostrador versus catálogo SEO con SSR en Web. | `ADR-007`, `DA-02`, `DA-05` | **AC01** |
| **Segregación PgBouncer (`pool_pos` / `pool_web`)** | Protege el mostrador físico frente a picos de tráfico en línea, blindando la disponibilidad transaccional. | `ADR-004`, `DA-02`, `DA-05` | **AC01**, **AC02** |
| **Reserva con TTL Dinámico** | Bloqueo temporal para evitar sobreventa en materiales pesados (cemento, fierro) sin congelar stock permanentemente. | `ADR-003`, `DA-01`, `DA-04` | **AC02** |
| **Dualidad de Identidad y JWT RS256** | Firma asimétrica con claims segregados que impide escalada de privilegios de usuarios web a funciones internas. | `ADR-005`, `DA-03` | **AC03** |
| **Aislamiento OLTP / OLAP** | El cálculo analítico pesado de Kardex valorizado no compite por recursos de CPU/RAM con las ventas en curso. | `ADR-008`, `DA-07` | **AC01** |
| **Arqueo Ciego de Caja Append-Only** | Prevención de fraude e inmutabilidad estricta sin exposición de saldos teóricos al cajero. | `ADR-002`, `DA-03`, `RF-CAJ-002` | **AC03** |
| **Storage Privado de Vouchers y XML** | Custodia cifrada de constancias bancarias ($\le 5\text{ MB}$) y documentos fiscales sin exposición pública directa. | `ADR-002`, `RF-WEB-003`, `RNF-SEG-004` | **AC03** |
