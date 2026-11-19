---
doc: "docs/entregables/sprint-2/03-pruebas-carga.md"
tipo: "verificacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Especificación de Pruebas de Carga y Concurrencia — SIGIC Ferretería v2.2

Este documento describe la especificación del escenario de estrés y concurrencia diseñado para evaluar los atributos de calidad `AC01` (latencia bajo contención) y `AC02` (consistencia en checkout simultáneo).

## 1. Escenario y Configuración del Script k6

El escenario de prueba está codificado en [checkout-stress-test.js](../../../tests/k6/checkout-stress-test.js) y modela la contención concurrente sobre inventario crítico ferretero (cemento y fierro):
- **Carga Concurrente:** 100 usuarios virtuales constantes (`constant-vus`) durante una ventana de 30 segundos.
- **Operación Ejecutada:** Peticiones HTTP POST al endpoint de checkout multilínea con tokens de autenticación y carga útil JSON representativa.
- **Métricas Personalizadas y Umbrales:**
  - `http_req_duration`: Percentil 95 menor a 250 ms (`p(95)<250`).
  - `sigic_checkout_duration_ms`: Percentil 95 menor a 250 ms (`p(95)<250`).
  - `http_req_failed`: Tasa de fallos de red/infraestructura menor al 5% (`rate<0.05`).
  - `sigic_500_errors`: Cero errores HTTP 500 (`count==0`), tolerando respuestas controladas 200/201 (venta) o 409 (conflicto de stock agotado).
  - Tasas de monitoreo: `sigic_checkout_success_rate` y `sigic_stock_conflict_rate`.

## 2. Distinción entre Metas de Diseño y Resultados Reales

- Los umbrales descritos en el script representan objetivos de diseño y requisitos no funcionales, no resultados medidos en la presente fase.
- Detectar cadenas de error HTTP (como `HTTP 409 Conflict`) no prueba por sí solo la ausencia de deadlocks a nivel de motor PostgreSQL 16 ni garantiza el algoritmo de ordenamiento determinista de bloqueos.
- Durante la Fase 2C **no se ejecutó k6**, por lo que no se certifica el rendimiento en esta etapa documental.

## Plan vigente de carga — 2026-11-19 (pendiente de ejecución)


Corregir `tests/k6/checkout-stress-test.js`: hoy usa 100 VUs/30 s, mezcla canales en un DTO, invoca `/api/v1/checkout/procesar`, envía header de idempotencia sin contrato demostrado y carece de Bearer. Sustituir con escenarios etiquetados por ruta, canal, carga y estado esperado. La ordenación anti-deadlock debe resistir peticiones llegadas en distinto orden. Usar `SIGIC_API_URL` en entorno dedicado y un snapshot restaurable por tanda. Registrar versión de k6, SHA, recursos de generador y servidor, tamaños de datos, configuración, inicio y fin.

| Perfil | Trayectoria y duración propuesta | Tráfico y condición de aceptación |
|---|---|---|
| Humo | 1 VU por flujo, 1 min | Confirmar contrato, cuerpos y permisos de cada ruta. Fallar si todas las escrituras chocan o toda respuesta de catálogo está vacía. |
| Basal LAN | 20 VUs POS, 3 min de calentamiento + 5 min medidos | `POST /api/v1/pos/ventas` con Cajero y turno abierto; leer venta por ID. Aislar p95 de cobros exitosos. |
| Objetivo especificado | 20 POS + 200 web, 10 min | Medir además 50 solicitudes/s de catálogo; ejecutar perfil de tasa de llegada separado y registrar VUs reales, RPS alcanzados y dropped_iterations. GET listado/detalle, POST calcular-precio y POST reservar. |
| Escalera de estrés | 20 POS estables; rampa web 200 → 480 → 980 → 1480 → 1980. Transición 2 min y meseta 5 min en cada nivel | Total máximo simultáneo 2000; web con 70 % navegación, 20 % cálculo y 10 % reserva por iteración. Etiquetar mezcla, usar pausa representativa, medir p95/p99 por operación. Comparar p95 POS con basal. |
| Contención y recuperación | Tanda independiente con 2 SKU escasos, clientes POS/Web en orden inverso; rampa final de bajada a 20 POS + 0 web y observación 5 min | 409 solo bajo insuficiencia esperada; al menos una operación exitosa cuando hay saldo. Verificar contadores BD y capacidad recuperada tras drenar colas. |

Sembrar productos visibles, ocultos y stock abundante separado del par escaso; tiendas, tarifas, cajas y turnos de prueba, usuarios únicos o particionados. Adquirir tokens antes del intervalo medido, renovar por cohortes antes de 15 min. El `clienteId` del pedido es el del token y el turno pertenece al cajero sembrado. Enviar cantidades, unidades y precios coherentes con BD. Usar el cuerpo real de `RegistrarVentaPOSCommand` y `ReservarPedidoWebRequestDto` según §4.2. Acotar el generador al entorno de ensayo, abortar ante pérdida de integridad, deadlock o saturación del propio generador. Dimensionar datos para que expiración y agotamiento aparezcan solo cuando el escenario lo pide.

**Criterios.** En el objetivo contractual medir cobro POS LAN p95 <250 ms, escaneo ≤1 s y confirmación UI ≤2 s con temporización del navegador, API de catálogo p95 ≤400 ms y listado renderizado ≤1,5 s. Degradación POS ≤10 % y cero agotamiento POS. La escalera hasta 2000 caracteriza el punto de ruptura; publicar la primera meseta fuera de objetivo, incluso si se detiene antes. Separar éxito, 409 deliberado, rechazo por backpressure y fallos de red/5xx. En la meseta nominal exigir cero 5xx y fallos inesperados, registrar tasa de éxito por ruta y RPS útil. El `GlobalExceptionMiddleware` oculta detalles del motor. Tomar deltas de `pg_stat_database.deadlocks`, inventario por SKU/almacén, kardex y totales antes/después; capturar `SHOW POOLS`, espera/latencia de adquisición y conexiones por alias. Verificar saldo físico menos comprometido ≥0, conservación tras rollback, expiración idempotente y ausencia de efectos parciales. Una respuesta rápida de error no aprueba el p95 de éxito. Guardar resumen JSON, series temporales y conciliación con relojes alineados. Tras corregir el script: `k6 run --summary-export=/tmp/sigic-k6.json tests/k6/checkout-stress-test.js`.


El contenido anterior describe el diseño histórico. Este plan sustituye su perfil
de ejecución; el script k6 existente aún debe adaptarse en una fase posterior.
No ejecutar contra datos de negocio ni declarar capacidad por el mero plan.
