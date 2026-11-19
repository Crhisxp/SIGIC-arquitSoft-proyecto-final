---
doc: "docs/entregables/sprint-2/cierre.md"
tipo: "acta-cierre"
estado: "DOCUMENTADO"
aprobado: false
---

# Acta de Sprint 2 — cierre pendiente

**Fecha:** 2026-11-19. **Responsable:** equipo SIGIC.
**Alcance de esta entrega:** documentación, runbook y reparación del pipeline.
No cierra pruebas de carga ni aceptación de API/navegador.

La referencia histórica CHG-037 registra 196 pruebas .NET, 26 POS y 8 Web
(230 en total). Son resultados de una ejecución anterior y no acreditan el SHA
actual. El blueprint fue auditado sobre `d492724`.

| Evidencia requerida | Estado / criterio |
|---|---|
| Bootstrap desde BD vacía y consultas autenticadas por ambos alias | Receta y scripts preparados; corrida limpia pendiente |
| Cuatro TRX actuales y cobertura con denominador | Pipeline secuencial; Domain ≥95 %, unión por ensamblado/archivo/línea |
| Vitest POS y Web, builds | Jobs incorporados; adjuntar artefactos de la corrida |
| Newman y navegador | Colección propuesta; seed, voucher y aislamiento por cliente pendientes de aceptación |
| Carga nominal y escalera hasta 2000 usuarios | Plan actualizado; adaptación k6 y medición pendientes |
| 42501, rollback, stock disponible ≥0, cero deadlocks | Exigir evidencia de integración y conciliación |

Cada ejecución de cierre debe completar: commit, fecha, entorno, dataset,
comando, resultado, URL HTTPS de artefactos CI, umbral, desviación, responsable
y resolución. No sustituir resultados por conteos históricos.

- [Pruebas unitarias y cobertura](01-pruebas-unitarias.md).
- [API y E2E](02-pruebas-e2e.md).
- [Diseño de carga y contención](03-pruebas-carga.md).
- [Persistencia y caché](estrategia-bd-cache.md).

## Observación CHG-038 — sesión de reingeniería

Vitest POS 26/26 y Web 8/8 aprobados con dependencias locales y Node v26.13.1.
El CI fija Node 20 para su reproducción. El agregador pasó cuatro regresiones;
los dos XML históricos Domain se deduplican a 249/260, sin atribuirlos a una corrida
actual. VSTest abortó por socket interno denegado y Docker no pudo acceder al daemon.
No se certifica el bootstrap ni la suite .NET completa en esta sesión.
El gate real sigue bloqueado por cinco enlaces de `.agents` no escribibles; el
parche preparado pasa la simulación en memoria 85/107 con cero errores.
