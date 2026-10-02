# Restricciones Formales y Delimitación de Alcance (SIGIC Ferretería)

> **Documento:** `analisis-de-sistema/05-restricciones.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Propósito:** Definición de Fronteras Operativas, Supuestos de Integración y Restricciones Tecnológicas del Sistema  

---

## 1. Introducción y Marco de Delimitación

La delimitación formal del alcance y las restricciones del sistema constituyen la base sobre la cual se definen las responsabilidades arquitectónicas de **SIGIC Ferretería**. Establecer qué funcionalidades pertenecen estrictamente al núcleo del sistema (*In-Scope*) y cuáles son derivadas a componentes de terceros (*Out-of-Scope*) mitiga riesgos operativos, sobrecostos de integración y desbordamientos en el cronograma de ingeniería.

---

## 2. Fronteras Operativas del Sistema

```
+---------------------------------------------------------------------------------------------------------+
|                                        FRONTERAS DEL SISTEMA SIGIC                                      |
+------------------------------+------------------------------+-------------------------------------------+
|     FRONTERA TRIBUTARIA      |       FRONTERA BANCARIA      |            FRONTERA LOGÍSTICA             |
+------------------------------+------------------------------+-------------------------------------------+
| SIGIC genera el payload      | Conciliación manual basada   | Despacho en mostrador ágil y              |
| estándar (JSON/XML) y        | en captura de Código de      | gestión de estado "En Ruta"               |
| calcula el IGV (18 %).       | Operación único.             | manual por el Despachador.                |
|                              |                              |                                           |
| -> Firma digital, envío OSE/ | -> Sin webhooks bancarios    | -> Sin rastreo GPS satelital              |
|    PSE y canal SUNAT son     |    ni APIs privadas de Yape, |    en tiempo real de flotas               |
|    externos.                 |    Plin o bancos.            |    vehiculares.                           |
+------------------------------+------------------------------+-------------------------------------------+
```

### 2.1. Frontera Tributaria (Facturación Electrónica y SUNAT)
- **Responsabilidad Interna de SIGIC:**
  - Captura y validación de formato de datos fiscales del cliente: DNI (8 dígitos numéricos) y RUC (11 dígitos validados mediante algoritmo módulo 11).
  - Cálculo formal del Impuesto General a las Ventas (**IGV al 18 %**) con precisión a dos decimales, desglosando base imponible, impuesto y monto total.
  - Distinción taxonómica de comprobantes:
    - **Ticket / Nota de Venta Interna:** Numeración con serie interna; control netamente comercial sin efecto fiscal ni payload tributario.
    - **Comprobante Electrónico Oficial (Boleta de Venta / Factura Comercial):** Generación de la estructura estándar de datos normalizada (**Payload JSON / XML**) conteniendo emisor, adquirente, líneas de detalle con código Sunat, impuestos y montos calculados.
- **Frontera de Exclusión (Out-of-Scope):**
  - SIGIC **no gestiona certificados digitales tributarios (`.pfx`)** ni firma criptográficamente los archivos XML en el servidor propio.
  - SIGIC **no mantiene canal directo con los web services de la SUNAT** ni actúa como Proveedor de Servicios Electrónicos (PSE) u Operador de Servicios Electrónicos (OSE).
  - La suscripción con un PSE/OSE autorizado y el envío/recepción de Constancias de Recepción (CDR) es responsabilidad del tercero contratado por la empresa ferretera.

### 2.2. Frontera Bancaria y Financiera
- **Responsabilidad Interna de SIGIC:**
  - Registro de pagos mixtos en mostrador (combinando efectivo, transferencias, billeteras digitales y tarjetas físicas).
  - Captura manual obligatoria del **Código de Operación Bancaria** (entre 4 y 12 dígitos) y validación de unicidad diaria para prevenir reutilización fraudulenta del mismo comprobante dentro de la jornada.
  - En el canal web, recepción y almacenamiento de archivos de constancia de pago (formatos JPG, PNG, PDF de hasta 5 MB) vinculados al código de operación.
  - Consola de Backoffice para que el personal de Tesorería apruebe o rechace manualmente las transferencias cotejando los extractos de cuenta bancaria.
- **Frontera de Exclusión (Out-of-Scope):**
  - **No se asumen APIs privadas ni webhooks directos de entidades bancarias locales** (Banco de Crédito del Perú - BCP, BBVA, Interbank, Banco de la Nación) ni de billeteras digitales (Yape, Plin), debido a la inexistencia de APIs públicas abiertas para microempresas ferreteras en la región.
  - SIGIC no almacena números de tarjeta de crédito/débito, fechas de expiración ni códigos CVV (cumplimiento PCI-DSS). En el caso de pasarelas web, la transacción ocurre en el entorno embebido del proveedor homologado, limitándose SIGIC a recibir el webhook firmado de confirmación.

### 2.3. Frontera Logística y Flota de Reparto
- **Responsabilidad Interna de SIGIC:**
  - Gestión integral del flujo de pedidos web con despacho: `Pendiente de Validación` $\rightarrow$ `En Preparación en Depósito` $\rightarrow$ `Listo para Retiro` (Tienda / Mostrador) o `En Ruta` (Envío a Obra) $\rightarrow$ `Entregado`.
  - Control de transferencias entre Almacén Central y puntos de venta mediante **Guías Internas de Traslado** con control de existencias en tránsito.
  - Registro del operador, vehículo asignado y marca de tiempo en la transición a `En Ruta`.
- **Frontera de Exclusión (Out-of-Scope):**
  - **No se incluye el desarrollo de algoritmos de rastreo satelital GPS en tiempo real** de vehículos de reparto sobre mapas interactivos en esta fase.
  - La navegación vehicular en ruta y el mantenimiento físico o telemático de las unidades de transporte son gestionados mediante la logística externa de la empresa.

---

## 3. Restricciones del Stack Tecnológico Corporativo

Para asegurar la gobernanza, escalabilidad y mantenibilidad a largo plazo de la solución, la arquitectura técnica queda cerrada bajo las siguientes decisiones tecnológicas obligatorias:

| Capa Arquitectónica | Tecnología Cerrada | Restricción y Justificación Técnica |
| :--- | :--- | :--- |
| **Backend Core** | **ASP.NET Core 8 Web API** | Implementación estricta del patrón **Clean Architecture** estructurado en cuatro capas: *Domain*, *Application*, *Infrastructure* y *Presentation (API)*. Prohibido el acoplamiento de lógica de negocio dentro de controladores o scripts de base de datos. Centraliza las reglas de validación y cálculo de precios para el POS y el portal web en una única librería de dominio compartida. |
| **Frontend POS Mostrador** | **React 18 (SPA)** | Aplicación de Página Única (*Single Page Application*) optimizada para baja latencia en red local. Diseñada específicamente para interacción ultrarrápida mediante atajos de teclado y pistolas lectoras ópticas de código de barras, sin recargas de página que interrumpan la atención en mostrador. |
| **Frontend Portal Web** | **Next.js 14 (React 18 con SSR)** | Portal de comercio electrónico con Renderizado en Servidor (*Server-Side Rendering* - SSR). Permite tiempos de carga inferiores a 1.5 s en dispositivos móviles sobre redes 4G y posicionamiento SEO del catálogo público. Node.js actúa estrictamente como servidor de renderizado de vistas; ninguna regla transaccional reside en Next.js. |
| **Persistencia OLTP** | **PostgreSQL 16 con PgBouncer** | Motor relacional transaccional gobernado por el modelo multiversión (MVCC). PgBouncer opera en modo transacción con dos pools segregados (`pool_pos` prioritario y `pool_web` controlado). El inventario se gobierna mediante transacciones ACID y bloqueos pesimistas de fila (`SELECT ... FOR UPDATE`). |
| **Capa Analítica (OLAP)** | **Datamart en Esquema Estrella** | Alojado en el esquema segregado `sigic_olap` sobre una instancia y volumen de almacenamiento independiente. Se compone de una tabla de hechos (`Fact_Ventas`) y dimensiones conformadas, alimentado por un proceso ETL asíncrono programado fuera del horario comercial. Prohibido ejecutar reportes analíticos pesados contra el motor OLTP. |
| **Mecanismo de Seguridad** | **JWT RS256 y Argon2id** | Identidades internas y externas gobernadas por tokens JWT firmados asimétricamente con clave RSA de 2048 bits (RS256) y caducidad de 15 minutos. Contraseñas protegidas mediante Argon2id con salt criptográfico. Matriz RBAC para colaboradores y aislamiento de superficie de API para clientes web. |

---

## 4. Supuestos y Condiciones de Borde del Despliegue

1. **Infraestructura de Red en Sucursales:** Cada tienda física cuenta con conexión LAN cableada (Fast Ethernet o Gigabit) para las terminales de mostrador, asegurando latencias sub-milisegundo hacia el servidor de aplicaciones y base de datos local/corporativa.
2. **Homologación de Clientes B2B:** La asignación de tarifas preferenciales a Maestros de Obra (Lista 2) y Contratistas (Lista 3) es un proceso administrativo humano; el sistema nunca auto-promueve el perfil tarifario de un cliente web sin la aprobación explícita del Administrador.
3. **Migración Inicial de Datos:** La carga de datos maestros al inicio del proyecto asume que el cliente proveerá hojas de cálculo consolidadas con la estructura requerida para familias, marcas y artículos; el sistema no asume la corrección automática de inventarios físicos desfasados previos al arranque.
