# SIGIC – Ferretería
## Sistema Integral de Gestión Comercial, Inventario Multialmacén, Control de Caja y Portal Web E-commerce

> **Documento de Entrega Académica y Especificación Técnica de Arquitectura de Software**  
> **Versión del Sistema:** 2.2 (Oficial – Corrección de Inconsistencias Operativas POS–Web)  
> **Ámbito Geográfico y Operativo:** Sector Comercial Ferretero y Materiales de Construcción – Ayacucho (Huamanga), Perú  
> **Stack Base:** ASP.NET Core 8 Web API | PostgreSQL 16 (PgBouncer) | React 18 (SPA Mostrador) | Next.js 14 SSR (Portal Web)  

---

## 1. Contexto del Problema y Solución Arquitectónica

### 1.1. Realidad Operativa del Negocio Ferretero en Ayacucho
El comercio de ferretería y venta de materiales pesados para la construcción en la provincia de Huamanga (Ayacucho) se caracteriza por un entorno transaccional exigente y heterogéneo:
1. **Flujo físico de mostrador de alta velocidad:** La atención presencial demanda tiempos de facturación mínimos; los operarios atienden a maestros de obra y público continuo utilizando preferentemente lectores de código de barras y teclado numérico sin depender del ratón.
2. **Naturaleza dual del inventario:** Coexisten materiales a granel o pesados (cemento, varillas corrugadas de acero, agregados) almacenados en Almacén Central / Depósito de Patio, con artículos menudos y de alta rotación (pernería, fittings, herramientas manuales, accesorios eléctricos) despachados directamente en la Tienda Mostrador.
3. **Expansión al canal digital (Omnicanalidad B2B/B2C):** La incorporación de un portal web de compras para público general, maestros de obra y empresas contratistas con tarifas diferenciadas introduce el riesgo crítico de colisiones de inventario (sobreventa o quiebre de stock) si las ventas físicas y electrónicas no se orquestan bajo una estricta coherencia transaccional.

### 1.2. Solución Arquitectónica Integral: SIGIC Ferretería
**SIGIC Ferretería** unifica en una sola plataforma corporativa la gestión comercial de mostrador y el canal digital de e-commerce sobre un **núcleo transaccional ACID cerrado** desarrollado en **ASP.NET Core 8** bajo los principios de **Clean Architecture**, soportado por una base de datos relacional **PostgreSQL 16** gestionada con **PgBouncer**.

El sistema resuelve las complejidades operativas mediante:
- **Segregación explícita de catálogo (`disponible_en_web`):** Filtro a nivel de modelo que excluye del portal web artículos sin empaque estándar, cortes fraccionados o pernería a granel.
- **Reservas de inventario dinámicas y transaccionales:** Asignación temporal de stock comprometido en Almacén Central con ventanas TTL adaptadas al medio de pago (20 minutos para pasarela de tarjetas; hasta 4 horas para transferencias y billeteras digitales Yape/Plin).
- **Revalidación atómica en checkout:** Verificación estricta de precio y saldo físico disponible en el momento del pago descartando proyecciones obsoletas de caché.
- **Desacoplamiento operativo:** La validación de constancias bancarias (Backoffice) se aísla de las terminales de mostrador, permitiendo que el personal de tienda despache pedidos web en estado `Listo para Retiro` validando únicamente código de pedido y DNI.
- **Aislamiento analítico:** Segregación física y lógica entre la carga transaccional operativa (OLTP) y la inteligencia de negocios (OLAP en esquema estrella).

---

## 2. Resumen Ejecutivo de Módulos de la Solución

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                              SISTEMA INTEGRAL SIGIC FERRETERÍA                                           |
+------------------------------------------------------------+------------------------------------------------------------+
|                MÓDULO TRANSACCIONAL (OLTP)                 |                  MÓDULO ANALÍTICO (OLAP)                   |
+------------------------------------------------------------+------------------------------------------------------------+
| • Catálogo Maestro y Control Multialmacén                  | • Datamart Dimensional en Esquema Estrella (`sigic_olap`)  |
| • Inventario Físico vs. Comprometido (Kardex Ponderado)    | • Tabla de Hechos Central: `Fact_Ventas`                   |
| • Gestión de Compras y Órdenes de Recepción                | • Dimensiones Conformadas: Tiempo, Producto, Cliente,     |
| • Transferencias Internas con Guías Formales               |   Canal y Almacén                                          |
| • Punto de Venta (POS) Mostrador de Alta Velocidad         | • Proceso ETL Desacoplado Asíncrono Nocturno               |
| • Control de Arqueo Ciego y Turnos de Caja                 | • Análisis Comparativo Multicanal (Mostrador vs. Web)      |
| • Portal E-commerce B2B/B2C con Listas Preferenciales      | • Cálculo de Márgenes Comerciales y Rotación ABC           |
| • Reservas Dinámicas con Expiración Automática             | • Cero Bloqueos o Penalizaciones sobre el Motor OLTP       |
| • Consola Backoffice para Validación de Pagos              | • Reportes Ejecutivos para la Gerencia General             |
+------------------------------------------------------------+------------------------------------------------------------+
```

### 2.1. Módulo Transaccional (OLTP)
- **Catálogo, Precios e Inventario:** Gestión jerárquica (Familia, Subfamilia, Marca, Línea), múltiples unidades de medida con factores de conversión y políticas de precios (Público General, Maestro de Obra, Contratista). Soporte multialmacén con transferencias auditadas por guía interna.
- **Punto de Venta Mostrador (POS):** Interfaz optimizada para teclado y lector óptico, pagos mixtos (efectivo, tarjeta, billeteras digitales) con registro de código de operación único, y entrega ligera de pedidos web (`RF-POS-011`).
- **Control de Turnos y Caja:** Registro de fondo inicial, movimientos de caja chica, cierres de turno y arqueo ciego donde el cajero declara montos físicos antes de conocer la recaudación teórica.
- **Canal E-commerce y Backoffice:** Navegación optimizada vía SSR, carrito con revalidación atómica, colas de validación de comprobantes de pago (hasta 5 MB) en Backoffice y generación automática de traslados internos para pedidos con modalidad "Retiro en Tienda".

### 2.2. Módulo Analítico (OLAP)
- **Datamart Comercial:** Esquema estrella alojado en volumen y base de datos independiente, desacoplado de la operación diaria.
- **Pipeline ETL Programado:** Extracción nocturna y transformaciones que consolidan ventas efectivas, devoluciones, órdenes web concluidas y reservas caídas.
- **Métricas e Indicadores Clave:** Comparativo de tickets promedio, volumen facturado por canal (físico vs. digital), comportamiento de carritos abandonados por expiración de reserva y margen de rentabilidad real por categoría de material.

---

## 3. Índice Estructurado del Repositorio

La documentación técnica del proyecto se encuentra organizada bajo la siguiente jerarquía de archivos:

```
.
├── .gitignore                                         <- Configuración de exclusión de binarios y temporales
├── README.md                                          <- Visión general, contexto y resumen ejecutivo
├── analisis-de-sistema/                               <- Especificación analítica de requerimientos de software
│   ├── 01-actores.md                                  <- Clasificación formal de actores internos y clientes web
│   ├── 02-historias-del-usuario.md                    <- Historias de usuario críticas en formato Given-When-Then
│   ├── 03-requisitos-funcionales.md                   <- Matriz exhaustiva v2.2 (Catálogo, POS, Caja, Web, Reportes)
│   ├── 04-atributos-de-calidad.md                     <- Requerimientos No Funcionales (RNF) y métricas cuantitativas
│   ├── 05-restricciones.md                            <- Delimitación de alcance y fronteras operativas/tecnológicas
│   └── 06-driver-arquitectonicos.md                   <- Decisiones rectoras y balance de compensaciones técnicas
└── arquitectura/                                      <- Modelado arquitectónico, diagramas y topología física
    └── arquitectura-inicial.md                        <- Diagrama conceptual, secuencia atómica y especificación de servidores
```

### Enlaces Rápidos de Navegación:
- [01. Clasificación de Actores del Sistema](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/analisis-de-sistema/01-actores.md)
- [02. Historias de Usuario Críticas (Given-When-Then)](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/analisis-de-sistema/02-historias-del-usuario.md)
- [03. Matriz Exhaustiva de Requisitos Funcionales](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/analisis-de-sistema/03-requisitos-funcionales.md)
- [04. Atributos de Calidad y Requerimientos No Funcionales](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/analisis-de-sistema/04-atributos-de-calidad.md)
- [05. Restricciones del Sistema y Fronteras Operativas](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/analisis-de-sistema/05-restricciones.md)
- [06. Drivers Arquitectónicos Rectores](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/analisis-de-sistema/06-driver-arquitectonicos.md)
- [07. Arquitectura Inicial y Topología de Infraestructura](file:///home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final/arquitectura/arquitectura-inicial.md)

---
*SIGIC Ferretería – Documento Técnico de Arquitectura de Sistemas – Ayacucho, 2026.*
