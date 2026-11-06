---
id: "ESTRUCTURA-DIAPOSITIVAS-CANVA-LIMPIAS"
doc: "recursos-exposicion/ESTRUCTURA-DIAPOSITIVAS-CANVA-LIMPIAS.md"
version: "2.2.0"
sistema: "SIGIC - Sistema Integrado de Gestión de Inventario y Comercialización"
metodologia: "Spec-Driven Development (SDD)"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 Arquitectura de Software - UNSCH"
fecha: "2026-11-06"
proposito: "Diseño limpio de 12 láminas de alto impacto visual para Canva sin basura visual ni párrafos densos"
---

# Guía Visual de Diapositivas para Canva — SIGIC Ferretería v2.2

> **Documento:** `recursos-exposicion/ESTRUCTURA-DIAPOSITIVAS-CANVA-LIMPIAS.md`  
> **Asignatura:** IS-488 Arquitectura de Software (UNSCH)  
> **Arquitecto / Expositor:** Crhistian Emanuel Flores Marquina  
> **Criterio de Evaluación:** Criterios 15 y 16 (Subsanación de Observación 4: Cero sobrecarga visual, cero jerga abstracta vacía).  
> **Reglas de Oro del Diseño en Canva:**
> 1. **Cero Bloques de Código o Párrafos Densos:** La pantalla es un soporte visual para la audiencia; el detalle técnico lo expresa el expositor verbalmente.
> 2. **Máximo 3 o 4 Viñetas Cortas por Lámina:** Cada punto debe ser una sentencia concisa de máximo 1 línea.
> 3. **Estructura Dual en Títulos:** Titular enfocado en el impacto de negocio ferretero + Subtítulo con el driver arquitectónico.
> 4. **Métricas en Grande (*Stat Callouts*):** Cifras clave destacadas en tipografía grande (ej. `P95 < 250ms`, `0 Sobreventas`, `50 Conexiones`).

---

## Índice Maestro de las 12 Láminas Limpias

| Lámina | Título con Impacto de Negocio | Subtítulo / Driver Arquitectónico | Métrica o Cifra Destacada | Imagen Asociada (Arrastrar en Canva) |
| :---: | :--- | :--- | :---: | :--- |
| **01** | **Gestión Comercial y Control de Inventario Ferretero** | Plataforma Omnicanal de Misión Crítica sobre .NET 8 y PostgreSQL 16 | **12,000+ SKUs** | *(Diseño editorial / Portada en Canva)* |
| **02** | **La Fricción Operativa Real en Ayacucho** | Alta Densidad de Ventas y Cuello de Botella Omnicanal | **150 ventas/día** | `recursos-exposicion/06-Contexto-Problematica-Negocio.png` |
| **03** | **Arquitectura de Referencia: Robustez y Simplicidad** | Monolito Modular en Clean Architecture y Domain-Driven Design (DDD) | **< 15 ms Latencia** | `recursos-exposicion/09-Decision-Monolito-Modular.png` |
| **04** | **Carga Masiva de Catálogo en Segundos** | Procesamiento Streaming .NET 8 con Transacción Atómica All-or-Nothing | **250 ítems en < 5s** | `recursos-exposicion/01-C1-Contexto-Sistema.png` |
| **05** | **Gobernanza de Stock: Cero Sobreventas en Construcción** | Invariante Cardinal: $\text{Disponible} = \text{Físico} - \text{Comprometido}$ | **0% Sobreventas** | `recursos-exposicion/08-Atributos-Calidad-Metricas.png` |
| **06** | **Cero Colas en Caja: Blindaje de Red Mostrador** | Segregación de Conexiones LAN vs. Web mediante PgBouncer | **50 Conex. POS** | `recursos-exposicion/10-PgBouncer-Segregacion-Pools.png` |
| **07** | **Prevención Determinista de Deadlocks** | Ordenamiento en Memoria por `producto_id ASC` y Cerrojo Pesimista | **0 Deadlocks (40P01)** | `recursos-exposicion/05-Protocolo-Checkout-AntiDeadlock.png` |
| **08** | **Arqueo Ciego e Inmutable: Cero Fugas de Capital** | Seguridad Transaccional Append-Only con Bloqueo SQL de Modificaciones | **0 Modificaciones** | `recursos-exposicion/11-Seguridad-Arqueo-Ciego.png` |
| **09** | **Resistencia bajo Estrés Concurrente** | Validación de Carga k6 con 100 VUs y Telemetría OpenTelemetry | **P95 < 250 ms** | `recursos-exposicion/03-C3-Componentes-Checkout-Concurrente.png` |
| **10** | **Calidad Continua Automatizada** | Pipeline en GitHub Actions con Quality Gate Bloqueante $\ge 95\%$ | **97.83% Cobertura** | `recursos-exposicion/04-C4-Clases-Dominio.png` |
| **11** | **Retorno de Inversión y Eficiencia Ferretera** | Optimización de Despachos, Ahorro Administrativo y Fidelización B2B | **-80% Tiempo Carga** | `recursos-exposicion/02-C2-Contenedores-Segregacion.png` |
| **12** | **Conclusiones y Certificación SDD** | Desarrollo Guiado por Especificaciones y Cumplimiento IS-488 UNSCH | **100% Verificado** | `recursos-exposicion/12-Conclusiones-Certificacion-SDD.png` |

---

## Especificación Lámina por Lámina para Copiar y Pegar en Canva

---

### Lámina 01: Portada Ejecutiva y Propósito
* **TÍTULO EN CANVA:** SIGIC Ferretería v2.2
* **SUBTÍTULO:** Plataforma Omnicanal de Misión Crítica para Retail Ferretero
* **ELEMENTOS VISUALES EN CANVA:**
  * Fondo oscuro elegante (`#0b0f19`) con detalles en verde esmeralda (`#10b981`).
  * Badges de tecnología: `.NET 8 Web API` | `PostgreSQL 16` | `PgBouncer` | `Clean Architecture`.
  * Autor: **Crhistian Emanuel Flores Marquina** — Asignatura: **IS-488 Arquitectura de Software (UNSCH)**.
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Propósito:** Unificar la venta física de mostrador con el canal digital e-commerce sin sobreventas ni caídas de sistema.
  * **Catálogo Complejo:** Gobernanza de más de 12,000 SKUs con medidas técnicas fraccionadas y precios mayoristas diferenciados.
  * **Metodología:** Desarrollo Guiado por Especificaciones (SDD – *Spec-Driven Development*).
* **CIFRA DESTACADA (Stat Callout):**
  > **12,000+**  
  > Artículos técnicos en catálogo activo

---

### Lámina 02: La Problemática Real en Ayacucho
* **TÍTULO EN CANVA:** Fricción Comercial en el Mostrador Ferretero
* **SUBTÍTULO:** Diagnóstico Operativo de Campo en Huamanga
* **IMAGEN ASOCIADA:** `recursos-exposicion/06-Contexto-Problematica-Negocio.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Alta Densidad de Ventas:** 120 a 180 comprobantes diarios por caja con horas punta matutinas de alta congestión.
  * **Materiales Calientes de Alto Impacto:** Cemento Portland Tipo I, Fierro Corrugado de 1/2" y Tubos de PVC sanitarios.
  * **El Conflicto de las 40 Bolsas:** Reserva web desde Huanta y compra en mostrador intentando llevarse el mismo lote físico.
  * **Cierres Manuales Vulnerables:** Arqueos en papel propensos a descuadres y horas perdidas digitando guías de compra.
* **CIFRA DESTACADA (Stat Callout):**
  > **150 ventas/día**  
  > Ritmo comercial por caja física en tienda

---

### Lámina 03: Arquitectura General
* **TÍTULO EN CANVA:** Arquitectura de Referencia: Monolito Modular
* **SUBTÍTULO:** Desacoplamiento Limpio con Domain-Driven Design (DDD)
* **IMAGEN ASOCIADA:** `recursos-exposicion/09-Decision-Monolito-Modular.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Cero Latencia de Red Interna:** Comunicación en memoria mediante MediatR CQRS eliminando fallas de red entre servicios.
  * **Fronteras Claras de Dominio:** 5 módulos cohesivos (Inventario, Ventas POS, Pedidos Web, Caja/Tesorería y Tarifas).
  * **Garantía Transaccional ACID:** Una sola base de datos PostgreSQL 16 sin la complejidad ni sobrecosto de microservicios distribuidos.
* **CIFRA DESTACADA (Stat Callout):**
  > **< 15 ms**  
  > Latencia en consultas de persistencia Dapper

---

### Lámina 04: Carga Masiva de Catálogo
* **TÍTULO EN CANVA:** Carga e Importación Masiva en Segundos
* **SUBTÍTULO:** Ingesta Streaming en .NET 8 con Transacción Atómica (`RF-INV-008`)
* **IMAGEN ASOCIADA:** `recursos-exposicion/01-C1-Contexto-Sistema.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Fin a la Digitación Manual:** De 4 horas ingresando guías de 100 ítems a menos de 5 segundos automatizados vía Excel/CSV.
  * **Validación Fila por Fila en Memoria:** Verificación estricta de SKU único, categoría existente y margen comercial positivo.
  * **Persistencia All-or-Nothing:** Carga en bloque con PostgreSQL COPY. Si una fila falla, se revierte el 100% y emite reporte JSON detallado.
* **CIFRA DESTACADA (Stat Callout):**
  > **250 ítems en < 5s**  
  > Ingesta atómica masiva con rollback integral

---

### Lámina 05: Gobernanza de Stock y Cero Sobreventas
* **TÍTULO EN CANVA:** Control de Inventario Omnicanal sin Sobreventas
* **SUBTÍTULO:** Ecuación Inviolable de Disponibilidad de Existencias (`AC02`)
* **IMAGEN ASOCIADA:** `recursos-exposicion/08-Atributos-Calidad-Metricas.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Fórmula Cardinal de Dominio:** $\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$.
  * **Protección del Mostrador:** Las reservas web incrementan el stock comprometido sin restar físico hasta que el pago se convalida.
  * **Segregación de Catálogo:** Artículos exclusivos de tienda física responden `HTTP 404` absoluto en el portal web.
* **CIFRA DESTACADA (Stat Callout):**
  > **0%**  
  > Margen de error: Cero sobreventas de cemento y fierro

---

### Lámina 06: Cero Colas en Caja
* **TÍTULO EN CANVA:** Cero Colas en Caja: Blindaje de Conectividad
* **SUBTÍTULO:** Segregación de Conexiones LAN vs. Web con PgBouncer (`AC01`)
* **IMAGEN ASOCIADA:** `recursos-exposicion/10-PgBouncer-Segregacion-Pools.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Carril Exclusivo para Mostrador:** `sigic_pos_pool` con 50 conexiones reservadas en LAN privada (`192.168.1.0/24`).
  * **Contención del Tráfico Web:** `sigic_web_pool` con 30 conexiones acotadas y rate-limit de 60 req/min por IP.
  * **Inmunidad ante Avalanchas:** Si la web sufre un pico de visitas o scraping, las cajas de mostrador siguen cobrando sin retrasos.
* **CIFRA DESTACADA (Stat Callout):**
  > **50 Conexiones**  
  > Fijas y reservadas exclusivamente para mostrador LAN

---

### Lámina 07: Prevención de Deadlocks
* **TÍTULO EN CANVA:** Prevención Determinista de Bloqueos Mutuos
* **SUBTÍTULO:** Ordenamiento Ascendente y Cerrojo Pesimista (`RF-WEB-005`)
* **IMAGEN ASOCIADA:** `recursos-exposicion/05-Protocolo-Checkout-AntiDeadlock.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **La Causa del Deadlock:** Caja compra Ítems A y B mientras Web reserva B y A simultáneamente.
  * **La Solución en Memoria:** Ordenamiento determinista ascendente obligatorio (`OrderBy(p => p.ProductoId)`).
  * **Bloqueo Pesimista Corto:** `SELECT ... FOR UPDATE` ordenado en PostgreSQL seguido de `UPDATE` condicionado por saldo disponible.
* **CIFRA DESTACADA (Stat Callout):**
  > **0 Deadlocks**  
  > Cero excepciones PostgreSQL código 40P01

---

### Lámina 08: Arqueo Ciego e Inmutable
* **TÍTULO EN CANVA:** Arqueo Ciego: Cero Fugas de Capital
* **SUBTÍTULO:** Seguridad Transaccional Append-Only en Turnos de Caja (`AC03`)
* **IMAGEN ASOCIADA:** `recursos-exposicion/11-Seguridad-Arqueo-Ciego.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Cero Conocimiento Previo:** El cajero declara billetes y monedas por denominación sin ver el saldo teórico del sistema.
  * **Persistencia Append-Only:** Prohibición absoluta de `UPDATE` y `DELETE` en la tabla `caja_arqueo`.
  * **Defensa SQL Activa:** Cualquier intento de manipulación retroactiva es abortado con `PostgresException 42501 permission denied`.
* **CIFRA DESTACADA (Stat Callout):**
  > **Append-Only**  
  > Inmutabilidad estricta auditada ante SUNAT y gerencia

---

### Lámina 09: Métricas de Calidad y Estrés
* **TÍTULO EN CANVA:** Rendimiento Demostrado bajo Estrés Extremo
* **SUBTÍTULO:** Pruebas de Carga k6 con 100 Usuarios Concurrentes (`AC01`)
* **IMAGEN ASOCIADA:** `recursos-exposicion/03-C3-Componentes-Checkout-Concurrente.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Prueba de Estrés Real:** 100 usuarios virtuales (VUs) durante 30 segundos compitiendo por cemento y fierro.
  * **Latencia Ultrabaja:** Percentil 95 en red LAN inferior a 250 ms y cobro presencial en mostrador $\le 2.0\text{ s}$.
  * **Resiliencia de Infraestructura:** 0% de errores internos 500 y 0 caídas de conexión durante la contención.
* **CIFRA DESTACADA (Stat Callout):**
  > **P95 < 250 ms**  
  > Tiempo de respuesta transaccional certificado

---

### Lámina 10: Calidad y CI/CD Automatizado
* **TÍTULO EN CANVA:** Calidad Certificada en Pipeline CI/CD
* **SUBTÍTULO:** Quality Gate Bloqueante en GitHub Actions
* **IMAGEN ASOCIADA:** `recursos-exposicion/04-C4-Clases-Dominio.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Suite Automatizada:** 13 pruebas xUnit cubriendo escenarios de concurrencia, márgenes y arqueo ciego.
  * **Quality Gate Estricto:** Script en Python que cancela el build si la cobertura de líneas es $< 95\%$.
  * **Cobertura Alcanzada:** 97.83% en líneas y 96.77% en ramas lógicas sobre el núcleo de dominio.
* **CIFRA DESTACADA (Stat Callout):**
  > **97.83%**  
  > Cobertura de código certificada (Umbral $\ge 95\%$)

---

### Lámina 11: Retorno de Inversión y Valor Comercial
* **TÍTULO EN CANVA:** Impacto y Retorno de Inversión en la Ferretería
* **SUBTÍTULO:** Eficiencia Operativa, Ahorro de Costos y Confianza del Cliente
* **IMAGEN ASOCIADA:** `recursos-exposicion/02-C2-Contenedores-Segregacion.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Velocidad en Mostrador:** Reducción del tiempo de cobro a menos de 2 segundos, atendiendo más clientes en hora punta.
  * **Ahorro de Tiempo Administrativo:** Disminución del 80% en tiempo de digitación de guías de proveedores.
  * **Eliminación de Penalidades:** Cero costos por obras paralizadas gracias a la erradicación total de sobreventas.
* **CIFRA DESTACADA (Stat Callout):**
  > **-80%**  
  > Reducción de tiempo en tareas administrativas de almacén

---

### Lámina 12: Conclusiones y Certificación SDD
* **TÍTULO EN CANVA:** Conclusiones y Cumplimiento de Ingeniería
* **SUBTÍTULO:** Certificación Spec-Driven Development (SDD) e IS-488 UNSCH
* **IMAGEN ASOCIADA:** `recursos-exposicion/12-Conclusiones-Certificacion-SDD.png`
* **VIÑETAS LIMPIAS (Copiar y Pegar):**
  * **Ingeniería Guiada por Especificaciones:** Cada línea de código responde a un requerimiento funcional y atributo auditable.
  * **Arquitectura Pragmática y Sólida:** Monolito modular escalable, mantenible y ajustado a la realidad del retail regional.
  * **Criterios Académicos Aprobados:** Rúbrica de IS-488 UNSCH satisfecha con evidencia empírica en código, tests y carga.
* **CIFRA DESTACADA (Stat Callout):**
  > **100% SDD**  
  > Trazabilidad total entre negocio, arquitectura y código
