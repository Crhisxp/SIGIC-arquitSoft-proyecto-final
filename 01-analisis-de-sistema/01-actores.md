# Especificación de Actores del Sistema (SIGIC Ferretería)

> **Documento:** `analisis-de-sistema/01-actores.md`  
> **Versión de Especificación:** 2.2 (Oficial)  
> **Área:** Análisis de Sistema y Modelado de Seguridad RBAC  

---

## 1. Introducción y Enfoque de Seguridad

El sistema **SIGIC Ferretería** establece un modelo de seguridad basado en la **segregación estricta de dominios de identidad**:
1. **Dominio Interno (Colaboradores):** Administrado mediante un esquema de Control de Acceso Basado en Roles (**RBAC** – *Role-Based Access Control*), donde cada usuario pertenece a un directorio centralizado, autenticado mediante tokens **JWT firmados con algoritmo asimétrico RS256** con expiración corta (15 minutos) y almacenamiento de contraseñas bajo hashing **Argon2id**.
2. **Dominio Externo (Clientes Web):** Autenticado en un almacén de identidades independiente para el portal e-commerce (público general, maestros de obra y contratistas), con tokens desacoplados que impiden cualquier acceso directo a las APIs internas de mostrador, almacén o tesorería.

---

## 2. Dominio 1: Colaboradores Internos (Seguridad RBAC)

Los colaboradores internos interactúan con el sistema principalmente a través de la aplicación cliente **SPA POS Mostrador / Gestión Interna** (desarrollada en React 18) y servicios administrativos.

### 2.1. Cajero
- **Descripción del Rol:** Responsable directo de la atención económica y facturación en el mostrador físico de la ferretería.
- **Responsabilidades Principales:**
  - Apertura formal del turno de caja asignando el saldo inicial (fondo de sencillo).
  - Captura ultrarrápida de artículos mediante lector óptico de código de barras (EAN-13/SKU) y teclado alfanumérico, operando sin necesidad de ratón (`RF-POS-003`).
  - Registro de ventas con medios de pago mixtos (efectivo, tarjeta física vía POS bancario, billeteras digitales Yape/Plin), registrando manualmente el código de operación único de 4 a 12 dígitos (`RF-POS-005`).
  - Bloqueo por código de operación duplicado dentro del mismo día (`RF-POS-005`).
  - Cierre de turno y ejecución obligatoria del **Arqueo Ciego de Caja** (`RF-CAJ-002`), ingresando el conteo de billetes y monedas sin conocer el saldo teórico acumulado en el sistema.
- **Límites de Acceso:**
  - Acceso restringido exclusivamente a su terminal de caja y a su turno activo.
  - Sin privilegios para anular comprobantes emitidos en turnos cerrados (requiere autorización de Administrador).
  - No tiene visibilidad ni acceso a la cola de validación de comprobantes bancarios de pedidos web (Backoffice).
- **Interfaz Utilizada:** SPA React 18 – Módulo POS Mostrador (optimizada para atajos de teclado `F1`–`F12`).
- **Mecanismo de Autenticación:** Credenciales de colaborador (usuario/contraseña en Argon2id) validadas contra el Servidor de Directorio con emisión de JWT RS256 de ámbito interno.

### 2.2. Almacenero
- **Descripción del Rol:** Encargado del control operativo del inventario físico, recepción de proveedores y transferencias internas en el Almacén Central y depósitos de patio.
- **Responsabilidades Principales:**
  - Registro de recepción física de mercadería contra órdenes de compra emitidas (`RF-INV-001`, `RF-INV-002`), alimentando el stock y el Kardex en la unidad de medida base.
  - Emisión, despacho y recepción física de **Guías Internas de Traslado** entre Almacén Central y mostradores (`RF-INV-003`), gestionando las transiciones de estado: `Borrador` → `En Tránsito` → `Recibido` / `Rechazado`.
  - Preparación física (*picking* y embalaje) de pedidos generados en el Portal Web con pago previamente validado por Backoffice (`RF-WEB-002`).
  - Actualización del estado de pedidos web a `Listo para Retiro` (si el recojo es en Almacén Central) o emisión de la guía de traslado hacia la tienda correspondiente.
- **Límites de Acceso:**
  - Sin acceso a funciones de cobro de caja, recaudación ni arqueos.
  - No puede modificar precios, márgenes de ganancia ni habilitar artículos para el canal web (`disponible_en_web`).
- **Interfaz Utilizada:** SPA React 18 – Módulo de Inventarios y Guías de Remisión Interna.
- **Mecanismo de Autenticación:** JWT RS256 con rol `Almacenero`.

### 2.3. Despachador
- **Descripción del Rol:** Colaborador asignado a la zona de entrega física en el mostrador o patio de carga, y responsable del control de despachos en ruta.
- **Responsabilidades Principales:**
  - Entrega física y ágil en mostrador de pedidos web (`RF-POS-011`) que se encuentren estrictamente en estado `Listo para Retiro`.
  - Validación en mostrador limitada a escanear el código de pedido (código de barras o QR) y cotejar el documento de identidad (DNI/CE) del titular o persona autorizada.
  - Cambio de estado del pedido a `Entregado` con registro del DNI receptor y marca de tiempo.
  - Gestión manual del despacho de pedidos para envío a obra en Huamanga, marcando la salida en estado `En Ruta` y confirmando la entrega física en obra.
- **Límites de Acceso:**
  - **Frontera Operativa Clave:** El Despachador **no valida comprobantes de pago**, no revisa vouchers bancarios ni verifica códigos de operación financiera (operación ya resuelta por Backoffice).
  - No puede alterar el contenido del pedido ni emitir notas de crédito.
- **Interfaz Utilizada:** SPA React 18 – Módulo de Despacho y Entrega Rápida en Mostrador.
- **Mecanismo de Autenticación:** JWT RS256 con rol `Despachador`.

### 2.4. Administrador / Tesorería
- **Descripción del Rol:** Máxima autoridad operativa, responsable de la configuración del catálogo, políticas de precios, auditoría financiera y validación de transferencias bancarias.
- **Responsabilidades Principales:**
  - Alta, baja y modificación de familias, subfamilias, marcas y artículos del catálogo maestro.
  - Gobernanza del flag `disponible_en_web` (`RF-INV-001`): habilita o bloquea de forma explícita la visibilidad y comercialización de cada artículo en el portal web.
  - Configuración de listas de precios base y listas preferenciales (Público General, Maestro de Obra, Contratista) y umbrales de descuento (`RF-PRE-001`, `RF-PRE-002`).
  - Aprobación y asignación de perfiles especiales a clientes web (validación de solvencia de Contratistas y carnet de Maestros de Obra).
  - Operación de la **Cola de Backoffice para Validación de Pagos Web** (`RF-WEB-003`): revisión y cotejo de constancias de transferencia y capturas de Yape/Plin (archivos JPG/PNG/PDF de hasta 5 MB) contra el estado de cuenta bancario corporativo.
  - Aprobación o rechazo formal del comprobante con registro de motivo; al aprobar, el sistema consolida la reserva y ordena la preparación del pedido.
  - Visualización del reporte de diferencias de arqueo ciego de caja (`RF-CAJ-002`) y auditoría inmutable de transacciones (`RNF-SEG-004`).
- **Límites de Acceso:**
  - Acceso irrestricto a los módulos administrativos y analíticos internos.
  - Sujeto a registro inmutable en pistas de auditoría para toda acción de cambio de precios o anulación de operaciones.
- **Interfaz Utilizada:** SPA React 18 – Consola Integral de Administración, Tesorería y Backoffice.
- **Mecanismo de Autenticación:** JWT RS256 con rol `Administrador` / `Tesoreria`, con políticas de doble factor de verificación opcional para operaciones críticas.

---

## 3. Dominio 2: Clientes Externos (Canal Digital Web)

Los clientes externos interactúan con la ferretería a través del **Portal Web E-commerce** desarrollado en **Next.js 14** (React 18 con Renderizado en Servidor - SSR), consumiendo la superficie REST pública de la API de ASP.NET Core 8.

### 3.1. Público General (B2C)
- **Descripción del Rol:** Cliente minorista particular que adquiere materiales y herramientas para el hogar o reparaciones menores.
- **Características y Responsabilidades:**
  - Navegación libre por el catálogo público digital (exclusivamente artículos con `disponible_en_web = true`).
  - Consulta de precios de Lista 1 (Público General con IGV incluido).
  - Creación de cuenta de usuario con correo electrónico o DNI.
  - Ejecución de pedidos con reserva temporal dinámica (20 min para pasarela con tarjeta; 4 horas para pago con Yape/transferencia con plazo de 1 hora para adjuntar voucher).
  - Elección de modalidad de despacho: *Retiro en Tienda Mostrador* o *Envío a Domicilio/Obra* dentro del radio urbano de Huamanga.
- **Límites de Acceso:**
  - No puede visualizar artículos con `disponible_en_web = false`.
  - No accede a precios mayoristas ni condiciones de crédito.
  - No puede interactuar con APIs administrativas ni ver pedidos de terceros.

### 3.2. Maestro de Obra
- **Descripción del Rol:** Profesional técnico independiente de la construcción (albañiles, gasfiteros, electricistas, maestros contratistas independientes) de la región Ayacucho.
- **Características y Responsabilidades:**
  - Registro inicial en el portal web y solicitud de homologación técnica adjuntando constancia o certificación de oficio.
  - Una vez validado por el Administrador, su cuenta queda asociada a la **Lista 2 (Tarifa Técnica Preferencial)**.
  - Consulta de existencias en tiempo real de artículos de alta rotación (cemento, tuberías PVC, varillas de acero, agregados).
  - Posibilidad de programar pedidos para recojo temprano en Almacén Central o retiro en tienda evitando filas en mostrador.
- **Límites de Acceso:**
  - Las tarifas preferenciales aplican únicamente cuando su sesión está debidamente autenticada.
  - Sus pedidos quedan sujetos a las mismas reglas de reserva dinámica y validación de comprobante que el resto del canal digital.

### 3.3. Contratista (Perfil B2B)
- **Descripción del Rol:** Empresa constructora, consorcio o ingeniero residente a cargo de obras civiles y edificaciones de mediana y gran envergadura en Huamanga y alrededores.
- **Características y Responsabilidades:**
  - Cuenta corporativa vinculada a número de RUC validado.
  - Acceso a la **Lista 3 (Tarifa Mayorista / B2B)** y módulo de cotización rápida por volumen (`RF-PRE-002`).
  - Configuración de despachos programados en Almacén Central (carga de plataformas y camiones) o entrega en obra con indicación de dirección exacta y responsable de recepción en campo.
  - Posibilidad de adjuntar órdenes de compra corporativas y comprobantes de pago de montos elevados mediante transferencias interbancarias.
- **Límites de Acceso:**
  - Acceso a precios mayoristas supeditado a volúmenes mínimos de compra por línea de producto (definidos en la regla de negocio `RF-PRE-002`).
  - No posee facultades para alterar el estado de guías ni autorizar despachos sin validación previa de Tesorería.

---

## 4. Matriz Resumen de Roles, Permisos y Superficies de Interfaz

| Actor / Rol | Dominio | Tipo de Token / Identidad | Interfaz Primaria | Módulos Autorizados |
| :--- | :--- | :--- | :--- | :--- |
| **Cajero** | Colaborador Interno | JWT RS256 (RBAC `Cajero`) | SPA POS Mostrador (React 18) | Apertura/Cierre de Turno, Cobro en Mostrador, Arqueo Ciego, Registro de Pago Mixto. |
| **Almacenero** | Colaborador Interno | JWT RS256 (RBAC `Almacenero`) | SPA Gestión Interna (React 18) | Recepción de Compras, Kardex Físico, Guías Internas de Traslado, *Picking* Web en Depósito. |
| **Despachador** | Colaborador Interno | JWT RS256 (RBAC `Despachador`) | SPA Entrega Rápida (React 18) | Entrega de Pedidos Web en Mostrador (`RF-POS-011`), Registro de Envíos en Ruta. |
| **Administrador / Tesorería** | Colaborador Interno | JWT RS256 (RBAC `Admin`) | Consola Backoffice (React 18) | Catálogo, Precios, Flag Web, Cola de Validación de Pagos Web (`RF-WEB-003`), Reportes y Auditoría. |
| **Público General** | Cliente Externo Web | JWT Público (Rol `Customer_Retail`) | Portal Web (Next.js 14 SSR) | Catálogo Web, Carrito de Compras, Checkout con Reserva Temporal, Consulta de Estado de Pedido. |
| **Maestro de Obra** | Cliente Externo Web | JWT Público (Rol `Customer_Pro`) | Portal Web (Next.js 14 SSR) | Catálogo Web con Tarifa Técnica Preferencial (Lista 2), Reservas y Checkout. |
| **Contratista (B2B)** | Cliente Externo Web | JWT Público (Rol `Customer_B2B`) | Portal Web (Next.js 14 SSR) | Cotizaciones B2B, Tarifa Mayorista (Lista 3), Programación de Retiro en Almacén Central o Envío a Obra. |
