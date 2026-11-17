---
doc: "05-pruebas-y-calidad/03-pruebas-carga.md"
tipo: "verificacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Especificación de Pruebas de Carga y Concurrencia — SIGIC Ferretería v2.2

Este documento describe la especificación del escenario de estrés y concurrencia diseñado para evaluar los atributos de calidad `AC01` (latencia bajo contención) y `AC02` (consistencia en checkout simultáneo).

## 1. Escenario y Configuración del Script k6

El escenario de prueba está codificado en [checkout-stress-test.js](../tests/k6/checkout-stress-test.js) y modela la contención concurrente sobre inventario crítico ferretero (cemento y fierro):
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
