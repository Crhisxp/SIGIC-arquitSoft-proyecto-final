# Modelo C4 — Nivel 2: Diagrama de Contenedores

> **Documento:** `04-modelo-c4/Nivel2-DiagramadeContenedores.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Estándar:** Modelo C4 para Visualización de Arquitectura de Software (Simon Brown) – Nivel 2 (Contenedores)  
> **Sistema:** SIGIC Ferretería (Monolito Modular con Frontends Desacoplados y Pools Segregados)

---

## 1. Propósito del Nivel 2 (Containers)

El **Diagrama de Contenedores (Nivel 2)** descompone el sistema **SIGIC Ferretería** en sus unidades de software y almacenamiento ejecutables de manera autónoma (*contenedores* o procesos de despliegue). Muestra:
1. La segregación de clientes frontends especializados (SPA Mostrador en React 18 vs. Portal E-commerce en Next.js 14).
2. El contenedor backend monolítico modular en ASP.NET Core 8 Web API con sus procesos asíncronos en segundo plano (*Background Workers*).
3. La topología de base de datos relacional y el subsistema de intermediación de conexiones **PgBouncer**, garantizando el aislamiento de recursos entre las transacciones de mostrador y la carga web.
4. El desacoplamiento entre el almacenamiento transaccional (OLTP) y el almacenamiento analítico (OLAP).

---

## 2. Diagrama de Contenedores en Mermaid C4

```mermaid
C4Container
    title Diagrama de Contenedores (C4 Nivel 2) - SIGIC Ferretería v2.2

    Person(clienteWeb, "Cliente Web", "Público General / Contratista B2B")
    Person(colaborador, "Colaborador Mostrador", "Cajero / Almacenero / Despachador")

    Boundary(c1, "Infraestructura SIGIC Ferretería") {
        Container(nginxProxy, "Proxy Inverso / Gateway", "Nginx 1.25", "Terminación TLS, compresión gzip, enrutamiento perimetral, rate limiting y caché estática.")
        
        Container(spaPos, "SPA POS Mostrador", "React 18 / Vite / Tailwind", "Aplicación de una sola página ultra-ligera en LAN. Operación 100% por teclado y escáner láser sin ratón (1024x768).")
        Container(portalWeb, "Portal E-commerce", "Next.js 14 / SSR / React", "Portal web con renderizado del lado del servidor (SSR), optimizado para SEO, catálogo interactivo y checkout.")

        Container(webApi, "Backend Monolítico Modular", "ASP.NET Core 8 Web API", "Núcleo transaccional de negocio (Clean Architecture). Expone APIs REST internas y públicas, gestiona reglas de inventario, tarifas y auditoría.")
        Container(workerService, "Servicios en Segundo Plano", ".NET 8 BackgroundService", "Hilos asíncronos (IHostedService): Liberación idempotente de reservas con TTL dinámico y Job ETL diario.")

        Container(pgBouncer, "Pooler de Conexiones", "PgBouncer 1.21", "Transaction pooling con aislamiento estricto: 'pool_pos' (50 conex. garantizadas) y 'pool_web' (30 conex. limitadas).")
        
        ContainerDb(dbOltp, "Base de Datos Transaccional", "PostgreSQL 16 (sigic_oltp)", "Motor relacional ACID. Almacena inventario físico y comprometido, ventas, órdenes web, kardex y usuarios.")
        ContainerDb(dbOlap, "Datamart Analítico", "PostgreSQL 16 (sigic_olap)", "Almacén dimensional en esquema estrella (Fact_Ventas, Fact_Kardex). Optimizado para reportería pesada gerencial.")
    }

    System_Ext(pseOse, "Proveedor PSE / OSE", "Servicio externo tributario SUNAT")
    System_Ext(pasarelaPago, "Pasarela de Pagos", "Procesador de cobros con tarjetas")

    Rel(clienteWeb, nginxProxy, "Navega catálogo y compra", "HTTPS / Port 443")
    Rel(colaborador, spaPos, "Opera caja y entrega pedidos", "HTTP / LAN / Port 80")
    
    Rel(nginxProxy, portalWeb, "Rutea tráfico web", "HTTP / Port 3000")
    Rel(nginxProxy, webApi, "Rutea llamadas a la API pública", "HTTP / Port 5000")
    Rel(spaPos, webApi, "Consume API REST interna (RBAC)", "HTTPS / LAN / Port 5000")
    Rel(portalWeb, webApi, "Consume API pública para checkout y catálogo", "HTTP / Internal / Port 5000")

    Rel(webApi, pgBouncer, "Envía transacciones POS y checkout", "TCP / PostgreSQL Protocol / Port 6432")
    Rel(workerService, pgBouncer, "Ejecuta liberación de reservas vencidas", "TCP / PostgreSQL Protocol / Port 6432")

    Rel(pgBouncer, dbOltp, "Mapea conexiones físicas a través de pool_pos y pool_web", "TCP / Port 5432")
    Rel(workerService, dbOltp, "Extrae hechos transaccionales diarios (ETL)", "TCP / Port 5432")
    Rel(workerService, dbOlap, "Carga dimensiones y tablas de hechos (ETL)", "TCP / Port 5433")

    Rel(webApi, pseOse, "Envía payload JSON/XML para firma", "HTTPS / REST")
    Rel(webApi, pasarelaPago, "Inicia cobro y recibe webhooks", "HTTPS / Webhook")
```

---

## 3. Matriz Tecnológica y Protocolos de Comunicación

| Contenedor | Tecnología Principal | Propósito y Responsabilidad | Protocolo / Puerto | Mecanismo de Seguridad |
| :--- | :--- | :--- | :--- | :--- |
| **Proxy Inverso / Gateway** | Nginx 1.25 en Alpine Linux | Escudo perimetral, terminador TLS 1.3, rate-limiting y caché de catálogo. | HTTPS (443), HTTP (80) | Certificados TLS ECDSA, Nginx `limit_req_zone`. |
| **SPA POS Mostrador** | React 18, Vite, Tailwind CSS | Terminal de caja física en red LAN, optimizada para escáner óptico y atajos `F1`–`F12`. | HTTP / HTTPS (LAN) | JWT RS256 (`Audience: SIGIC_InternalStaff`). |
| **Portal E-commerce** | Next.js 14, Node.js runtime | Catálogo web B2B/B2C, renderizado SSR interactivo y carrito digital. | HTTP (3000 local) | JWT RS256 (`Audience: SIGIC_WebClient`), HttpOnly Cookies. |
| **Backend Web API** | ASP.NET Core 8 Web API (Kestrel) | Núcleo transaccional de Clean Architecture, validación de reglas e inventario. | HTTP (5000 local) | Middleware de autorización RBAC en servidor. |
| **Servicios de Fondo** | .NET 8 `IHostedService` | Worker de expiración de reservas dinámicas (cada 60 s) y consolidación ETL nocturna. | En memoria del proceso | Ejecución asíncrona interna e idempotente. |
| **Pooler de Conexiones** | PgBouncer 1.21 | Asignación transaccional (*Transaction Pooling*) con dos pools segregados (`pool_pos` y `pool_web`). | TCP (6432) | Autenticación SCRAM-SHA-256 local. |
| **Base de Datos OLTP** | PostgreSQL 16 Relacional | Almacén operacional ACID con bloqueos de fila deterministas (`SELECT ... FOR UPDATE`). | TCP (5432) | Tráfico aislado en red privada; backups WAL continuos. |
| **Datamart OLAP** | PostgreSQL 16 Dimensional | Almacén analítico en esquema estrella para reportería pesada y kardex mensual valorizado. | TCP (5433) | Aislamiento físico de CPU y memoria frente al motor OLTP. |
