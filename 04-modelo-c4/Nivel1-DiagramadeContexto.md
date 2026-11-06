---
id: "04-c4-nivel1-contexto"
doc: "04-modelo-c4/Nivel1-DiagramadeContexto.md"
version: "2.2.0"
estado: "VALIDADO_PARA_EXPOSICION"
tipo: "Modelo C4 - Nivel 1: Diagrama de Contexto del Sistema"
proyecto: "SIGIC-Ferreteria-v2.2"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 UNSCH"
fecha: "2026-10-30"
---

# Modelo C4 — Nivel 1: Diagrama de Contexto del Sistema

> **Documento:** `04-modelo-c4/Nivel1-DiagramadeContexto.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Estándar:** Modelo C4 para Visualización de Arquitectura de Software (Simon Brown) – Nivel 1 (Contexto de Sistema)  
> **Sistema:** SIGIC Ferretería (Sistema Integrado de Gestión de Inventario y Comercialización)  
> **Evaluación Académica:** IS-488 Arquitectura de Software (UNSCH)

---

## 1. Propósito del Nivel 1 (System Context)

El **Diagrama de Contexto de Sistema (Nivel 1)** establece la vista panorámica de más alto nivel de la arquitectura de software. Su objetivo primordial es delimitar formalmente la frontera (*boundary*) del software **SIGIC Ferretería**, identificando de manera inequívoca:
1. Los **Actores Humanos** que interactúan con la plataforma: Superadministrador, Cajero Mostrador, Almacenero, Cliente Web, Despachador y Tesorería.
2. Los **Sistemas Externos** de frontera: Proveedor PSE/OSE SUNAT, Pasarela de Pagos con Tarjeta y Entidades Bancarias / Billeteras Locales.
3. La garantía de los atributos de calidad rectores: Rendimiento $P95 < 250\text{ ms}$ (**AC01**), Consistencia ACID sin sobreventas (**AC02**) y Trazabilidad inmutable (**AC03**).

---

## 2. Diagrama de Contexto del Sistema (Flujo Jerárquico de 3 Niveles)

```mermaid
flowchart TD
    %% ========================================================
    %% NIVEL SUPERIOR: ACTORES DEL SISTEMA (VERDE)
    %% ========================================================
    subgraph ACTORES ["1. Nivel Superior: Actores del Negocio Ferretero"]
        ACT_CLI["<b>Cliente Web</b><br/>(Público, Maestro de Obra, Contratista B2B)"]
        ACT_CAJ["<b>Cajero Mostrador</b><br/>(Venta rápida LAN sin mouse y arqueo ciego)"]
        ACT_ALM["<b>Almacenero</b><br/>(Kardex CPP, guías de traslado y recepción)"]
        ACT_TES["<b>Personal de Tesorería</b><br/>(Validación de vouchers Yape/Plin/Transferencias)"]
        ACT_ADM["<b>Superadministrador</b><br/>(Configuración, tarifas, almacenes y RBAC)"]
    end

    %% ========================================================
    %% NIVEL CENTRAL: NÚCLEO TRANSACCIONAL SIGIC (AZUL)
    %% ========================================================
    subgraph NUCLEO ["2. Nivel Central: Plataforma Omnicanal"]
        SIGIC["<b>SIGIC Ferretería v2.2 (Plataforma Comercial Omnicanal)</b><br/>• Monolito Modular en ASP.NET Core 8 Web API (Clean Architecture)<br/>• Persistencia ACID en PostgreSQL 16 con PgBouncer Segregado<br/>• Gobernanza Invariante: Stock Disponible = Stock Físico - Stock Comprometido"]
    end

    %% ========================================================
    %% NIVEL INFERIOR: SISTEMAS EXTERNOS (MORADO)
    %% ========================================================
    subgraph SISTEMAS_EXTERNOS ["3. Nivel Inferior: Sistemas Externos de Integración"]
        EXT_PSE["<b>Proveedor PSE / SUNAT</b><br/>(Emisión y firma digital de Boleta/Factura XML)"]
        EXT_PAY["<b>Pasarelas de Pago</b><br/>(Niubiz / Culqi - Webhooks y TTL 20 min)"]
        EXT_BAN["<b>Entidades Bancarias</b><br/>(BCP, BBVA, Yape, Plin - Cuentas de recaudo)"]
    end

    %% Relaciones Actores -> Núcleo SIGIC
    ACT_CLI -->|"Catálogo, compras y reservas con TTL"| SIGIC
    ACT_CAJ -->|"Facturación LAN Gigabit y arqueo ciego"| SIGIC
    ACT_ALM -->|"Entradas, traslados y despacho central"| SIGIC
    ACT_TES -->|"Auditoría de pagos en Backoffice"| SIGIC
    ACT_ADM -->|"Gestión de catálogo, listas y seguridad"| SIGIC

    %% Relaciones Núcleo SIGIC -> Sistemas Externos
    SIGIC -->|"Payload estructurado con IGV 18%"| EXT_PSE
    SIGIC -->|"Tokenización y confirmación de cobro"| EXT_PAY
    ACT_TES -.->|"Cotejo de extractos bancarios"| EXT_BAN

    %% Paleta Semántica Ordenada: Verde (Actores), Azul (SIGIC), Morado (Externos)
    classDef actorStyle fill:#064e3b,stroke:#10b981,stroke-width:2px,color:#ffffff;
    classDef sigicStyle fill:#1e3a8a,stroke:#3b82f6,stroke-width:3px,color:#ffffff;
    classDef extStyle fill:#4c1d95,stroke:#8b5cf6,stroke-width:2px,color:#ffffff;

    class ACT_CLI,ACT_CAJ,ACT_ALM,ACT_TES,ACT_ADM actorStyle;
    class SIGIC sigicStyle;
    class EXT_PSE,EXT_PAY,EXT_BAN extStyle;
```

---

## 3. Catálogo de Actores y Sistemas de Frontera

### 3.1. Actores Humanos del Sistema

| Actor | Categoría | Interacción Principal con SIGIC | Frontera y Restricciones |
| :--- | :--- | :--- | :--- |
| **Superadministrador** | Interno | Gestión de seguridad (RBAC), catálogo global, almacenes, listas de precios mayoristas y auditoría general. | Acceso irrestricto a los paneles gerenciales y reportería sobre el Datamart analítico (`AC03`). |
| **Cajero Mostrador** | Interno | Venta presencial rápida por escáner/teclado (`RF-POS-003`), cobro mixto y cierre ciego de caja (`RF-CAJ-002`). | Operación en LAN de mostrador; no valida vouchers bancarios ni tiene acceso a la tesorería de pedidos web (`AC01`). |
| **Almacenero** | Interno | Recepción de mercadería contra orden de compra (`RF-INV-006`), despacho y recepción de guías internas (`RF-INV-003`). | Opera sobre el Almacén Central y depósitos; no puede alterar listas de precios ni cobrar ventas (`AC02`). |
| **Cliente Web** | Externo | Compra digital, consulta de precios por perfil (General, Maestro de Obra, Contratista) y seguimiento de órdenes. | Sujeto a rate-limiting; solo ve artículos con `disponible_en_web = true` (`RF-INV-001`). No accede a endpoints internos. |
| **Despachador** | Interno | Entrega ágil en mostrador de pedidos web (`RF-POS-011`) y marcación de despachos a obra. | **Frontera estricta:** Solo valida pedidos en estado `Listo para Retiro` cotejando DNI; no manipula comprobantes ni vouchers. |
| **Personal de Tesorería** | Interno | Auditoría de comprobantes bancarios (Yape/Plin/Transferencias de hasta 5 MB) en Backoffice (`RF-WEB-003`). | Desacoplado del mostrador para evitar cuellos de botella en la atención presencial (`AC01`). |

---

### 3.2. Sistemas Externos de Frontera

| Sistema Externo | Tipo de Integración | Responsabilidad Interna de SIGIC | Responsabilidad del Sistema Externo (Out-of-Scope) |
| :--- | :--- | :--- | :--- |
| **Proveedor PSE / OSE (Tributario SUNAT)** | API REST / JSON | Captura datos fiscales (RUC/DNI), calcula el IGV al 18 % y genera el payload normalizado (`RF-POS-008`). | Firma digital con certificado `.pfx`, envío de XML a SUNAT y recepción de Constancias de Recepción (CDR). |
| **Pasarela de Pagos (Tarjetas)** | SDK / Webhook HTTPS | Inicializa la transacción con TTL de 20 minutos (`RF-WEB-002`) y procesa el webhook firmado de confirmación. | Captura de datos sensibles de tarjetas (cumplimiento PCI-DSS), procesamiento bancario y tokenización. |
| **Entidades Bancarias Locales** | Conciliación Asíncrona | Captura manual obligatoria del Código de Operación bancaria y archivo de voucher ($\le 5\text{ MB}$) (`RF-POS-006`, `RF-WEB-003`). | No existen webhooks ni APIs bancarias directas para microempresas ferreteras; la acreditación es auditada por Tesorería. |
