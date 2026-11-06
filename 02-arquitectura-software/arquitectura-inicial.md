---
id: "02-arquitectura-inicial"
doc: "02-arquitectura-software/arquitectura-inicial.md"
version: "2.2.0"
estado: "VALIDADO_PARA_EXPOSICION"
tipo: "Especificación de Arquitectura Inicial y Topología"
proyecto: "SIGIC-Ferreteria-v2.2"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 UNSCH"
fecha: "2026-10-30"
---

# Especificación Arquitectónica Inicial y Topología del Sistema (SIGIC Ferretería)

> **Documento:** `02-arquitectura-software/arquitectura-inicial.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Disciplina:** Arquitectura de Software y Diseño de Infraestructura  
> **Modelado:** Clean Architecture, Diagramas C4 Conceptual / UML Sequence y Topología Física  
> **Evaluación Académica:** IS-488 Arquitectura de Software (UNSCH)

---

## 1. Visión General de la Arquitectura

La arquitectura de **SIGIC Ferretería v2.2** está concebida bajo el paradigma de **Clean Architecture** (Arquitectura Limpia) sobre un **Monolito Modular en ASP.NET Core 8 Web API**, con un desacoplamiento estricto entre el núcleo de dominio transaccional, los mecanismos de persistencia y las capas de presentación de los dos canales de captura comercial: el **Punto de Venta Mostrador (SPA React 18 en LAN)** y el **Portal Web E-commerce B2B/B2C (Next.js 14 SSR en Internet)**.

La plataforma garantiza de manera determinante:
1. **Rendimiento de Misión Crítica (AC01):** Tiempos de respuesta $P95 < 250\text{ ms}$ en la API transaccional Core, consultas de catálogo con Dapper sub-15 ms, y cobro en mostrador físico $P95 \le 1.0\text{ s}$ sin degradación por ráfagas web.
2. **Consistencia Transaccional ACID y Cero Sobreventas (AC02):** Toda mutación de existencias opera sobre PostgreSQL 16 con bloqueo pesimista a nivel de fila (`SELECT ... FOR UPDATE`) ordenado ascendentemente por ID (`ORDER BY producto_id ASC`) y la fórmula inviolable:
   $$\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$$
3. **Trazabilidad y No Repudio Inmutable (AC03):** Registro inmutable en `Audit_Log` de toda operación sensible mediante Transactional Outbox, y política de **Arqueo Ciego de Caja** con persistencia estrictamente *append-only* en `caja_arqueo`.
4. **Prioridad Absoluta al Mostrador Físico:** Segregación de conexiones en PgBouncer mediante dos pools aislados: `pool_pos` (50 conexiones dedicadas de alta prioridad) y `pool_web` (30 conexiones limitadas con backpressure).
5. **Aislamiento Operacional vs. Analítico:** Segregación física entre la base de datos operacional (`sigic_oltp`) y el Datamart en esquema estrella (`sigic_olap`).
6. **Almacenamiento Seguro Privado:** Storage privado para comprobantes bancarios (vouchers $\le 5\text{ MB}$) y comprobantes electrónicos XML/CDR de SUNAT, inaccesibles directamente desde la web pública.

---

## 2. Diagrama Conceptual de Arquitectura

El siguiente diagrama modela la interacción integral de los componentes del sistema, desde los actores hasta los servicios externos y la capa de almacenamiento:

```mermaid
graph TD
    %% Actores del Sistema
    subgraph Actores ["Actores del Negocio Ferretero"]
        A1["Colaboradores Internos (Superadmin / Cajero / Almacenero / Despachador)"]
        A2["Clientes Web (Público General / Maestro de Obra / Contratista)"]
    end

    %% Capa de Presentación
    subgraph Presentacion ["Capa de Presentación (Frontends Desacoplados)"]
        UI_POS["SPA POS Mostrador (React 18) - Teclado F1-F12 / Escáner Óptico"]
        UI_WEB["Portal Web E-commerce (Next.js 14 SSR) - Móvil / Desktop"]
    end

    %% Capa de Control y Proxy
    subgraph Proxy ["Capa Perimetral y Ruteo"]
        NGINX["Proxy Inverso Nginx (Terminación TLS, Rate Limiting, Caché Catálogo)"]
    end

    %% Capa de Aplicación y Servicios
    subgraph Backend ["Backend Monolito Modular (ASP.NET Core 8 Web API - Clean Architecture)"]
        API_INT["Superficie REST Interna (/api/v1/internal/* - RBAC)"]
        API_PUB["Superficie REST Pública (/api/v1/public/* - Filtro 404 Web)"]
        
        subgraph Core ["Lógica de Dominio y Negocio (MediatR CQRS)"]
            DOM_INV["Módulo de Inventario y Kardex Ponderado (CPP)"]
            DOM_RES["Servicio de Reservas Dinámicas (TTL 20 min / 4 h)"]
            DOM_PRE["Motor de Precios y Listas Escalonadas"]
            DOM_CAJ["Gestión de Turnos y Arqueo Ciego de Caja (Append-Only)"]
            WORKER_RES["BackgroundService: Liberación Idempotente de Reservas"]
            WORKER_ETL["Job ETL Asíncrono: Consolidación Diaria Nocturna"]
        end
    end

    %% Capa de Almacenamiento y Persistencia
    subgraph Almacenamiento ["Capa de Persistencia y Storage Privado"]
        PGBOUNCER["PgBouncer (Transaction Pooling)"]
        POOL_POS["Pool POS (50 Conexiones Dedicadas LAN)"]
        POOL_WEB["Pool Web (30 Conexiones Limitadas Internet)"]
        
        DB_OLTP[("BD Transaccional OLTP - sigic_oltp (PostgreSQL 16)")]
        DB_OLAP[("Datamart Analítico OLAP - sigic_olap (Esquema Estrella)")]
        STORAGE_PRIV[("Storage Privado de Archivos (Vouchers <= 5MB y XML SUNAT)")]
    end

    %% Sistemas Externos
    subgraph Externos ["Sistemas Externos y Proveedores"]
        EXT_PSE["Proveedor de Servicios Electrónicos (PSE / OSE SUNAT)"]
        EXT_PAY["Pasarela de Pagos Tarjetas (Webhooks HMAC)"]
        EXT_BAN["Bancos / Billeteras Digitales (Yape / Plin / BCP)"]
    end

    %% Relaciones de Flujo
    A1 -->|Uso en Mostrador / Red LAN| UI_POS
    A2 -->|Acceso HTTPS Internet| UI_WEB

    UI_POS -->|Llamadas API Internas| NGINX
    UI_WEB -->|Llamadas API Públicas / SSR| NGINX

    NGINX -->|Enrutamiento Interno| API_INT
    NGINX -->|Enrutamiento Público| API_PUB

    API_INT --> Core
    API_PUB --> Core

    Core --> PGBOUNCER
    Core --> STORAGE_PRIV
    PGBOUNCER --> POOL_POS
    PGBOUNCER --> POOL_WEB
    
    POOL_POS --> DB_OLTP
    POOL_WEB --> DB_OLTP

    WORKER_RES -->|Desbloqueo y Reversión de Stock| DB_OLTP
    WORKER_ETL -->|Extracción Nocturna| DB_OLTP
    WORKER_ETL -->|Carga de Hechos y Dimensiones| DB_OLAP

    API_INT -->|Payload JSON/XML| EXT_PSE
    API_PUB -->|Inicialización / Webhooks| EXT_PAY
    A2 -.->|Transferencia manual / Voucher| EXT_BAN
```

---

## 3. Diagrama de Secuencia: Checkout Web y Revalidación Atómica

El flujo crítico de compra digital modela cómo el sistema resuelve la revalidación atómica de existencias y precios frente a una capa de lectura en caché, garantizando cero sobreventas y el manejo del tiempo de vida (*TTL*) de la reserva:

```mermaid
sequenceDiagram
    autonumber
    actor Cliente as Cliente Web (Contratista / Público)
    participant Webapp as Portal Web (Next.js 14 SSR)
    participant API as API Pública (ASP.NET Core 8)
    participant DB as PostgreSQL 16 (sigic_oltp via pool_web)
    participant Worker as BackgroundService (Reservas)

    Cliente->>Webapp: Presiona "Confirmar Pedido / Pagar"
    Webapp->>API: POST /api/v1/public/checkout (Payload: Items, MetodoPago, Modalidad)
    
    activate API
    API->>DB: Iniciar Transacción ACID (Isolation Level: Read Committed)
    
    Note over API,DB: Revalidación Atómica de Precios y Existencias
    Note over API: Ordenamiento determinista: items.OrderBy(x => x.ProductoId)
    API->>DB: SELECT precio FROM lista_precios WHERE producto_id = :id AND lista_id = :lista_cliente
    API->>DB: SELECT fisico, comprometido FROM inventario_stock WHERE almacen_id = Central AND producto_id = :id ORDER BY producto_id ASC FOR UPDATE
    
    alt Stock Insuficiente (fisico - comprometido < cantidad)
        DB-->>API: Saldo no disponible
        API->>DB: ROLLBACK Transacción
        API-->>Webapp: HTTP 409 Conflict ("Stock no disponible, ajuste su pedido")
        Webapp-->>Cliente: Notificación visual de agotado y actualización de carrito
    else Stock Suficiente y Precio Correcto
        API->>DB: UPDATE inventario_stock SET stock_comprometido = stock_comprometido + :n WHERE almacen_id = Central AND producto_id = :id
        
        alt Medio de Pago = Pasarela de Tarjetas
            Note over API,DB: Asignación de TTL = 20 Minutos
            API->>DB: INSERT INTO pedido_web (estado='Pendiente_Pago', expira_en=NOW() + INTERVAL '20 minutes')
        else Medio de Pago = Transferencia / Billetera Digital (Yape / Plin)
            Note over API,DB: Asignación de TTL = 4 Horas (Plazo de 1 h para adjuntar voucher)
            API->>DB: INSERT INTO pedido_web (estado='Pendiente_Comprobante', expira_voucher=NOW() + INTERVAL '1 hour', expira_en=NOW() + INTERVAL '4 hours')
        end
        
        API->>DB: COMMIT Transacción
        deactivate API
        API-->>Webapp: HTTP 201 Created (OrdenID, TTL, DatosDePago)
        Webapp-->>Cliente: Muestra instrucciones de abono / Pasarela de tarjeta
    end

    Note over Worker,DB: Ciclo Asíncrono de Expiración Idempotente (Cada 60 segundos)
    activate Worker
    Worker->>DB: SELECT orden_id, items FROM pedido_web WHERE estado IN ('Pendiente_Pago','Pendiente_Comprobante') AND expira_en < NOW() FOR UPDATE SKIP LOCKED
    opt Existen órdenes vencidas
        Worker->>DB: UPDATE inventario_stock SET stock_comprometido = stock_comprometido - :n WHERE almacen_id = Central AND producto_id = :id
        Worker->>DB: UPDATE pedido_web SET estado = 'Cancelado_Por_Expiracion' WHERE orden_id = :id
        Worker->>DB: COMMIT
    end
    deactivate Worker
```

---

## 4. Especificación de Topología de Infraestructura Real (Mononodo Docker Compose)

Aterrizando la infraestructura a la realidad operativa y económica de una ferretería mediana en Ayacucho (120 a 180 ventas/día, 2 cajas de mostrador LAN y catálogo web sincronizado), se sustituye cualquier sobredimensión de servidores distribuidos por una **Arquitectura Mononodo en Contenedores Docker Compose**, ejecutada sobre un único host de alto rendimiento:

### 4.1. Especificación del Host y Distribución de Recursos de Contenedores

* **Especificación del Host Físico / VPS:** **4 vCPU**, **8 GB RAM**, **100 GB SSD NVMe**, Sistema Operativo Ubuntu Server 22.04 LTS con Docker Engine 26+ y Docker Compose v2.

| Contenedor | Servicio y Rol | Imagen Base / Runtime | Límite CPU | Límite RAM | Justificación Técnica de Dimensionamiento |
| :--- | :--- | :--- | :---: | :---: | :--- |
| **`sigic-proxy`** | Proxy inverso perimetral, terminación SSL/TLS, compresión Brotli y Rate Limiting. | `nginx:1.25-alpine` | 0.5 vCPU | 256 MB | Ruteo de tráfico HTTPS hacia frontend y backend con consumo mínimo de recursos. |
| **`sigic-api`** | Backend Monolítico Modular (Clean Architecture, MediatR, CQRS, Dapper y EF Core 8). | `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` | 1.5 vCPU | 2048 MB | Procesa la lógica transaccional de ventas POS, checkout web y trabajadores en segundo plano con $P95 < 250\text{ ms}$. |
| **`sigic-web`** | Portal E-commerce B2B/B2C con Renderizado del Lado del Servidor (SSR). | `node:20-alpine` (Next.js 14) | 1.0 vCPU | 1024 MB | Renderizado interactivo para móviles y desktop con caché de catálogo. |
| **`sigic-pgbouncer`** | Connection Pooler transaccional con pools segregados (`pool_pos` y `pool_web`). | `edoburu/pgbouncer:1.22` | 0.5 vCPU | 256 MB | Mantiene 50 conexiones reservadas para cajas físicas y 30 compartidas para la web, evitando contención. |
| **`sigic-postgres`** | Motor de base de datos relacional ACID (`sigic_oltp` y tablas analíticas). | `postgres:16-alpine` | 1.5 vCPU | 3072 MB | Memoria asignada para `shared_buffers = 1GB` y `work_mem = 32MB`, suficiente para 12,000 SKUs y 180 ventas/día. |
| **`sigic-storage`** | Custodia privada de comprobantes de pago (<= 5 MB) y XMLs tributarios. | Volumen local cifrado (LUKS / Docker Volume) | 0.2 vCPU | 512 MB | Almacenamiento desacoplado sin acceso web directo para máxima seguridad tributaria. |

---

### 4.2. Presupuesto Comparativo de Despliegue

La solución está diseñada para ser extraordinariamente costo-eficiente y sostenible para el comercio ferretero regional:

| Rubro / Concepto | Entorno Dev / Demo / Evaluación UNSCH | Entorno Producción Regional (Ferretería Ayacucho) | Proveedor Seleccionado y Detalle |
| :--- | :---: | :---: | :--- |
| **Cómputo en la Nube (VPS / Host)** | **S/. 0.00 / mes** | **~$16.00 USD / mes**<br>(~S/. 60.00 PEN / mes) | **Dev:** Oracle Cloud Always Free (Instancia Ampere A1 ARM: 4 OCPU, 24 GB RAM, 200 GB Storage).<br>**Prod:** VPS Cloud (Hetzner CPX21 o DigitalOcean Droplet: 4 vCPU, 8 GB RAM, 100 GB SSD). |
| **Nombre de Dominio Oficial** | **S/. 0.00 / mes**<br>(Subdominio dinámico o IP) | **~S/. 9.17 PEN / mes**<br>(S/. 110.00 PEN / año) | Dominio comercial oficial `.pe` registrado ante NIC.pe / Punto.pe para identidad corporativa local. |
| **Seguridad Perimetral, CDN y SSL** | **S/. 0.00 / mes** | **S/. 0.00 / mes** | Cloudflare Free Tier (Gestión DNS Anycast, Certificado SSL/TLS Universal, Edge Caching y Mitigación DDoS). |
| **Costos de Licenciamiento** | **S/. 0.00** | **S/. 0.00** | Stack 100% Open Source (.NET 8 SDK, PostgreSQL 16, PgBouncer, Linux Ubuntu, Next.js, React). |
| **PRESUPUESTO TOTAL MENSUAL** | **S/. 0.00 / mes** | **< S/. 70.00 PEN / mes**<br>(~$18.50 USD / mes) | **Viabilidad Financiera Plena:** Representa menos del 0.2% del margen bruto mensual de una ferretería mediana en Ayacucho. |

---

## 5. Especificaciones de Terminales Cliente

Para garantizar el cumplimiento de los tiempos de respuesta definidos en los requerimientos funcionales y atributos de calidad (`AC01`, `RNF-USA-002`), se estandariza el equipamiento cliente en las sedes físicas:

### 5.1. Terminal de Mostrador (POS / Almacén / Despacho)
- **Sistema Operativo:** Windows 10/11 Pro (64 bits) o distribución Linux Desktop LTS.
- **Navegador Web:** Google Chrome, Microsoft Edge o Mozilla Firefox en versiones vigentes (cero instalación local de plugins).
- **Hardware Mínimo:** Procesador doble núcleo a 2.0 GHz, 4 GB de memoria RAM, monitor con resolución mínima nativa de $1024 \times 768$ píxeles sin desplazamiento horizontal (`RNF-USA-002`).
- **Periféricos de Mostrador:** Pistola lectora óptica de códigos de barras (1D/2D) con interfaz USB en modo emulación de teclado e impresora térmica de tickets de 80 mm (protocolo ESC/POS). Operación 100% por atajos de teclado (`F1`-`F12`).

### 5.2. Terminal de Cliente Externo (Portal Web)
- **Dispositivos Soportados:** Teléfonos inteligentes (iOS / Android) y computadoras personales / laptops.
- **Conectividad Mínima de Prueba:** Enlace móvil 4G comercial estándar en Huamanga, logrando renderizado interactivo completo de listados en menos de 1.5 segundos ($LCP \le 1.5\text{ s}$).
