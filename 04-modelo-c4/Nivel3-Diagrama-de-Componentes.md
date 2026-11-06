---
id: "04-c4-nivel3-componentes"
doc: "04-modelo-c4/Nivel3-Diagrama-de-Componentes.md"
version: "2.2.0"
estado: "VALIDADO_PARA_EXPOSICION"
tipo: "Modelo C4 - Nivel 3: Diagrama de Componentes"
proyecto: "SIGIC-Ferreteria-v2.2"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 UNSCH"
fecha: "2026-10-30"
---

# Modelo C4 — Nivel 3: Diagrama de Componentes

> **Documento:** `04-modelo-c4/Nivel3-Diagrama-de-Componentes.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Estándar:** Modelo C4 para Visualización de Arquitectura de Software (Simon Brown) – Nivel 3 (Componentes)  
> **Contenedor Analizado:** Backend Monolítico Modular (ASP.NET Core 8 Web API)  
> **Estilo Arquitectónico:** Clean Architecture (Arquitectura Limpia con Puertos y Adaptadores)  
> **Evaluación Académica:** IS-488 Arquitectura de Software (UNSCH)

---

## 1. Propósito del Nivel 3 (Components)

El **Diagrama de Componentes (Nivel 3)** realiza un acercamiento (*zoom-in*) al interior del contenedor principal de cómputo: el **Backend Monolítico Modular en ASP.NET Core 8 Web API**. Detalla cómo se descompone internamente el código fuente respetando las cuatro capas concéntricas de **Clean Architecture**:
1. **Controlador Venta (`VentasPOSController`):** Puerta de entrada para las ventas en mostrador físico (`RF-POS-003`).
2. **Orquestador Checkout (`IniciarCheckoutCommandHandler`):** Orquestador de aplicación que gobierna la revalidación atómica y prevención de bloqueos mutuos (`RF-WEB-002`, `RF-WEB-005`).
3. **Servicio de Reserva de Stock (`ReservaStockService`):** Lógica pura de dominio para el cálculo del TTL dinámico y retención temporal de existencias.
4. **Repositorio de Inventario (`InventarioRepository` / `SIGICDbContext`):** Adaptador de persistencia con bloqueo pesimista a nivel de fila (`SELECT ... FOR UPDATE`).
5. **Storage Privado Adapter (`StoragePrivadoAdapter`):** Adaptador de infraestructura para custodia de comprobantes y archivos XML SUNAT.

---

## 2. Diagramas de Componentes en 2 Vistas Especializadas (C4 Nivel 3)

Para erradicar la sobrecarga visual y garantizar la máxima legibilidad técnica evaluada en **IS-488 UNSCH**, la descomposición interna del backend en ASP.NET Core 8 Web API se estructura en **dos vistas arquitectónicas segregadas**:

### 2.1. Vista 3.1: Flujo Crítico Mostrador POS (LAN Gigabit)
Modela la ruta de ejecución prioritaria de facturación rápida presencial sin ratón, con asignación de conexiones de alta prioridad en PgBouncer y tiempo de respuesta sub-segundo ($P95 < 250\text{ ms}$):

```mermaid
flowchart TD
    %% ========================================================
    %% ACTOR Y FRONTEND (VERDE)
    %% ========================================================
    subgraph UI_POS ["1. Canal de Mostrador Físico (LAN Gigabit)"]
        ACT_CAJ["<b>Cajero de Tienda</b><br/>(Operación 100% Teclado F1-F12)"]
        SPA_POS["<b>SPA POS Mostrador (React 18)</b><br/>Viewport 1024x768 / Lector EAN-13 Láser"]
    end

    %% ========================================================
    %% CAPA DE SEGURIDAD (MORADO)
    %% ========================================================
    subgraph SEC_POS ["2. Perímetro de Seguridad LAN"]
        MW_AUTH["<b>Middleware Auth POS</b><br/>JWT RS256 (scope: pos_terminal, IP LAN 192.168.1.0/24)"]
    end

    %% ========================================================
    %% CAPA PRESENTACIÓN Y APLICACIÓN (AZUL)
    %% ========================================================
    subgraph APP_POS ["3. Controladores y Comandos (Presentation & Application)"]
        CTRL_POS["<b>VentasPOSController</b><br/>POST /api/v1/pos/ventas"]
        CMD_POS["<b>CrearVentaPOSCommandHandler</b><br/>(MediatR Command Handler)"]
    end

    %% ========================================================
    %% CAPA DOMINIO (AMARILLO)
    %% ========================================================
    subgraph DOM_POS ["4. Servicios de Dominio Puro (Domain Layer)"]
        SRV_INV["<b>InventarioDomainService</b><br/>Validar Stock Físico Directo"]
        SRV_TAR["<b>MotorTarifasDomainService</b><br/>Lista 1: Retail / Lista 2: Maestro Obra"]
    end

    %% ========================================================
    %% PERSISTENCIA Y POOLS (ROJO)
    %% ========================================================
    subgraph DATA_POS ["5. Persistencia Transaccional Prioritaria"]
        EF_POS["<b>EF Core 8 / Unit of Work</b><br/>Transacción ACID inmediata"]
        POOL_POS["<b>PgBouncer: sigic_pos_pool:6432</b><br/>(50 Conexiones Dedicadas LAN - Prioridad Alta)"]
        DB_POS[("<b>PostgreSQL 16 (sigic_oltp)</b><br/>Descuento atómico sobre stock_fisico")]
    end

    %% INTEGRACIÓN EXTERNA
    EXT_PSE["<b>PseFacturacionAdapter</b><br/>Envío síncrono PSE Boleta/Factura XML"]

    %% Conexiones de Flujo
    ACT_CAJ -->|"Escaneo EAN-13 y teclas rápidas"| SPA_POS
    SPA_POS -->|"HTTP POST /pos/ventas (LAN)"| MW_AUTH
    MW_AUTH -->|"Petición autorizada con rol Cajero"| CTRL_POS
    CTRL_POS -->|"Despacha CrearVentaPOSCommand"| CMD_POS
    CMD_POS -->|"Aplica precio según lista"| SRV_TAR
    CMD_POS -->|"Valida y rebaja stock físico"| SRV_INV
    CMD_POS -->|"Ejecuta persistencia ACID"| EF_POS
    EF_POS -->|"Conexión directa garantizada"| POOL_POS
    POOL_POS -->|"UPDATE inventario_stock SET stock_fisico"| DB_POS
    CMD_POS -.->|"Genera payload fiscal electrónico"| EXT_PSE

    %% Paleta Semántica: Verde (UI), Azul (API/App), Amarillo (Domain), Rojo (Data), Morado (Seguridad)
    classDef verdeUI fill:#064e3b,stroke:#10b981,stroke-width:2px,color:#ffffff;
    classDef azulAPI fill:#1e3a8a,stroke:#3b82f6,stroke-width:2px,color:#ffffff;
    classDef amarilloDOM fill:#78350f,stroke:#f59e0b,stroke-width:2px,color:#ffffff;
    classDef rojoDATA fill:#7f1d1d,stroke:#ef4444,stroke-width:2px,color:#ffffff;
    classDef moradoSEC fill:#4c1d95,stroke:#8b5cf6,stroke-width:2px,color:#ffffff;

    class ACT_CAJ,SPA_POS verdeUI;
    class CTRL_POS,CMD_POS azulAPI;
    class SRV_INV,SRV_TAR amarilloDOM;
    class EF_POS,POOL_POS,DB_POS rojoDATA;
    class MW_AUTH,EXT_PSE moradoSEC;
```

---

### 2.2. Vista 3.2: Flujo E-commerce y Pedidos Web (WAN)
Modela el flujo de compras digitales, orquestación del checkout determinista anti-deadlock, retención temporal de existencias con TTL dinámico y liberación asíncrona mediante worker en segundo plano:

```mermaid
flowchart TD
    %% ========================================================
    %% ACTOR Y FRONTEND WEB (VERDE)
    %% ========================================================
    subgraph UI_WEB ["1. Canal Digital E-commerce (Internet WAN)"]
        ACT_WEB["<b>Cliente Web Externo</b><br/>(Navegador Móvil / Desktop)"]
        PORTAL_WEB["<b>Portal Web E-commerce (Next.js 14)</b><br/>App Router SSR / Filtro 404 estricto"]
    end

    %% ========================================================
    %% SEGURIDAD Y PERÍMETRO (MORADO)
    %% ========================================================
    subgraph SEC_WEB ["2. Perímetro Perimetral y Protección"]
        WAF_WEB["<b>WAF & Rate Limiting</b><br/>Cloudflare / Nginx (60 req/min/IP)"]
        MW_WEB["<b>Middleware Auth Web</b><br/>JWT RS256 (scope: web_customer)"]
        WORKER_EXP["<b>ReservaExpirationWorker</b><br/>IHostedService (Barrido cada 60s)"]
    end

    %% ========================================================
    %% CAPA PRESENTACIÓN Y APLICACIÓN (AZUL)
    %% ========================================================
    subgraph APP_WEB ["3. Controladores y Orquestadores (Application)"]
        CTRL_CHECK["<b>CheckoutWebController</b><br/>POST /api/v1/public/checkout"]
        CMD_CHECK["<b>IniciarCheckoutCommandHandler</b><br/>Orquestador MediatR Anti-Deadlock"]
        CMD_LIB["<b>LiberarReservaVencidaCommandHandler</b><br/>(Liberación Idempotente)"]
    end

    %% ========================================================
    %% CAPA DOMINIO (AMARILLO)
    %% ========================================================
    subgraph DOM_WEB ["4. Servicios de Dominio Puro (Domain Layer)"]
        SRV_INV_WEB["<b>InventarioDomainService</b><br/>• Orden determinista: OrderBy(p => p.ProductoId)<br/>• Stock Disponible = Stock Físico - Stock Comprometido"]
        SRV_RES["<b>ReservaStockService</b><br/>TTL Dinámico: 20 min tarjeta / 240 min manual"]
    end

    %% ========================================================
    %% PERSISTENCIA Y STORAGE (ROJO)
    %% ========================================================
    subgraph DATA_WEB ["5. Persistencia y Almacenamiento Segregado"]
        EF_WEB["<b>EF Core 8 / DbContext</b><br/>Transacción pesimista con FOR UPDATE"]
        POOL_WEB["<b>PgBouncer: sigic_web_pool:6432</b><br/>(30 Conexiones Compartidas - Backpressure)"]
        DB_WEB[("<b>PostgreSQL 16 (sigic_oltp)</b><br/>reserva_stock_pedido & stock_comprometido")]
        STORAGE_PRIV[("<b>Storage Privado de Archivos</b><br/>Vouchers de pago <= 5 MB cifrados")]
    end

    %% Conexiones de Flujo Web
    ACT_WEB -->|"Navega catálogo y confirma compra"| PORTAL_WEB
    PORTAL_WEB -->|"HTTPS / WAN"| WAF_WEB
    WAF_WEB -->|"Inspección perimetral"| MW_WEB
    MW_WEB -->|"Petición web autorizada"| CTRL_CHECK
    CTRL_CHECK -->|"Despacha IniciarCheckoutCommand"| CMD_CHECK
    CMD_CHECK -->|"1. Ordena IDs y evalúa Disponible"| SRV_INV_WEB
    CMD_CHECK -->|"2. Asigna TTL según pasarela/voucher"| SRV_RES
    CMD_CHECK -->|"3. Ejecuta SELECT ... FOR UPDATE"| EF_WEB
    EF_WEB -->|"Ruteado con control de contención"| POOL_WEB
    POOL_WEB -->|"Incrementa stock_comprometido e inserta pedido"| DB_WEB
    CMD_CHECK -.->|"Guarda comprobante JPG/PDF cifrado"| STORAGE_PRIV

    %% Flujo Asíncrono de Expiración
    WORKER_EXP -->|"Detecta pedidos expirados (NOW > expira_en)"| CMD_LIB
    CMD_LIB -->|"Libera stock_comprometido de forma idempotente"| EF_WEB

    %% Paleta Semántica: Verde (UI), Azul (API/App), Amarillo (Domain), Rojo (Data), Morado (Seguridad)
    classDef verdeUI fill:#064e3b,stroke:#10b981,stroke-width:2px,color:#ffffff;
    classDef azulAPI fill:#1e3a8a,stroke:#3b82f6,stroke-width:2px,color:#ffffff;
    classDef amarilloDOM fill:#78350f,stroke:#f59e0b,stroke-width:2px,color:#ffffff;
    classDef rojoDATA fill:#7f1d1d,stroke:#ef4444,stroke-width:2px,color:#ffffff;
    classDef moradoSEC fill:#4c1d95,stroke:#8b5cf6,stroke-width:2px,color:#ffffff;

    class ACT_WEB,PORTAL_WEB verdeUI;
    class CTRL_CHECK,CMD_CHECK,CMD_LIB azulAPI;
    class SRV_INV_WEB,SRV_RES amarilloDOM;
    class EF_WEB,POOL_WEB,DB_WEB,STORAGE_PRIV rojoDATA;
    class WAF_WEB,MW_WEB,WORKER_EXP moradoSEC;
```

---

## 3. Tabla Resumen de Componentes del Backend Monolítico (C4 Nivel 3)

La siguiente tabla sintetiza la descomposición técnica del contenedor backend en ASP.NET Core 8 Web API, mapeando cada componente con su responsabilidad en la ferretería, contratos de entrada y reglas de concurrencia:

| Nombre del Componente / Clase | Responsabilidad en el Negocio de la Ferretería | Interfaz de Entrada / Métodos Clave | Persistencia / Regla de Concurrencia Asociada |
| :--- | :--- | :--- | :--- |
| **`VentasPOSController`** | Entrada REST para terminales de mostrador en tienda física. Procesa facturación de cemento, fierro y herramientas con teclado y escáner láser (`RF-POS-003`). | `POST /api/v1/pos/ventas`<br>`POST /api/v1/pos/cajas/arqueo`<br>`GET /api/v1/pos/productos/buscar` | Ruteado prioritario a `sigic_pos_pool`. Ejecución síncrona de baja latencia ($P95 < 250\text{ ms}$). |
| **`DespachoMostradorController`** | Pantalla ligera de entrega para retiro en tienda física de compras originadas en el portal web (`RF-POS-011`). | `POST /api/v1/pos/despacho/{codigo}/entregar`<br>`GET /api/v1/pos/despacho/buscar` | Valida código de retiro y DNI receptor. Actualiza estado del pedido y rebaja física desde Almacén de Despacho. |
| **`CatalogoPublicoController`** | API pública de consulta de catálogo para clientes web. Protege el stock interno de la ferretería (`RF-INV-001`, `RF-WEB-001`). | `GET /api/v1/public/catalogo`<br>`GET /api/v1/public/productos/{id}` | Ruteado a `sigic_web_pool`. Filtro estricto `disponible_en_web = true` (retorna HTTP 404 estricto si es exclusivo de mostrador). |
| **`CheckoutWebController`** | Recepción de órdenes de compra del portal web con revalidación atómica (`RF-WEB-002`, `RF-WEB-005`). | `POST /api/v1/public/checkout`<br>`POST /api/v1/public/pedidos/{id}/comprobante` | Invoca orquestador transaccional en `sigic_web_pool` con TTL dinámico y control de cuota. |
| **`IniciarCheckoutCommandHandler`** | Orquestador MediatR de checkout transaccional. Elimina condiciones de carrera entre la web y mostrador (`AC02`). | `Handle(IniciarCheckoutCommand, CancellationToken)` | Transacción ACID corta: ordena IDs en memoria (`OrderBy(p => p.ProductoId)`), ejecuta `SELECT ... FOR UPDATE` y actualiza `stock_comprometido`. |
| **`CrearVentaPOSCommandHandler`** | Orquestador MediatR de facturación presencial inmediata. Descuenta inventario físico y dispara comprobante electrónico. | `Handle(CrearVentaPOSCommand, CancellationToken)` | Transacción inmediata: rebaja directa sobre `stock_fisico`, emisión síncrona a PSE SUNAT y registro en turno de caja. |
| **`InventarioDomainService`** | Servicio de dominio puro. Gobierna la regla cardinal de disponibilidad y el algoritmo anti-deadlock. | `ValidarDisponible(stockFisico, stockComp, cant)`<br>`OrdenarParaBloqueo(items)`<br>`CalcularCostoPonderado(exist, ent)` | Invariante 1 (`RNF-CON-003`): $\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$. Cero acceso a I/O de base de datos. |
| **`ReservaStockService`** | Gobernanza de tiempos de caducidad (TTL) de reservas de inventario web según modalidad de pago. | `CalcularTtl(MedioPago)`<br>`AsignarVentanaComprobante()` | Tarjeta/Pasarela: TTL 20 min exacto. Pagos manuales (Yape/Plin/Transferencia): TTL 4 h con ventana de 60 min para comprobante. |
| **`InventarioRepository` / `SIGICDbContext`** | Adaptador de persistencia relacional sobre PostgreSQL 16 con aislamiento `Read Committed`. | `ObtenerParaBloqueoAsync(ids, almacenId)`<br>`ActualizarComprometidoAsync(reserva)` | Bloqueo pesimista de fila (`FOR UPDATE`). Conectado mediante PgBouncer segregado (`sigic_pos_pool` / `sigic_web_pool`). |
| **`CatalogoDapperRepository`** | Micro-ORM de alta velocidad para lecturas de catálogo sin bloqueo ni overhead de tracking (`AC01`). | `GetCatalogoPaginadoAsync(filtros)`<br>`GetProductoPublicoPorIdAsync(id)` | Consultas SQL indexadas sobre vistas materializadas con latencia de ejecución sub-15ms. |
| **`StoragePrivadoAdapter`** | Custodia aislada de archivos de voucher de pago y archivos XML tributarios SUNAT (`AC03`). | `GuardarComprobanteAsync(stream, ext)`<br>`ObtenerVoucherProtegidoAsync(id)` | Almacenamiento local en volumen cifrado `/var/sigic/storage/` sin acceso web directo. Tamaño máx 5MB, SHA-256. |
| **`ReservaExpirationWorker`** | Background Worker (`IHostedService`) que se ejecuta cada 60 segundos para sanear reservas abandonadas. | `ExecuteAsync(CancellationToken)` | Identifica reservas con `FechaExpiracionUtc < Now` y ejecuta comando de liberación idempotente de `stock_comprometido`. |
