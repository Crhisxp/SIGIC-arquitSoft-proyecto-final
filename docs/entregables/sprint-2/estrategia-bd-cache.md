---
doc: "docs/entregables/sprint-2/estrategia-bd-cache.md"
tipo: "estrategia-persistencia"
estado: "DOCUMENTADO"
aprobado: false
---

# Estrategia de BD y caché — Sprint 2

**Estado:** plan de verificación; no se certifican planes ni latencias medidas.


Crear `docs/entregables/sprint-2/estrategia-bd-cache.md`. Documentar esquema, grano por almacén, llaves e índices existentes y planes medidos con volumen realista. Priorizar índice único ya existente (almacen_id,producto_id), EXPLAIN (ANALYZE, BUFFERS) sobre listado con búsqueda, reservas vencidas y kardex. Justificar cada índice adicional con planes, costo de escritura y latencia medida. Auditar duplicación de filas en listado sin filtro de almacén (LEFT JOIN a stock_almacen). Validar conciliación del inventario y transacciones usando los tests de Concurrency y muestreo de locks. El worker puede consumir el pool POS en OperationChannelContext: medir durante la expiración.

La estrategia vigente describe L1 de metadatos a 60 min, Redis L2 de catálogo a 5 min y CDN. El código consultado usa Dapper y fetch con no-store. No hay aprovisionamiento de Redis ni implementación de ese cache-aside en el flujo auditado. Medir primero bypass actual; luego, como experimento separado, solo metadatos con clave versionada por catálogo/almacén/lista comercial, invalidación post-commit y acotación del desfase web ≤30 s. TTL de cinco minutos aislado no satisface ese límite. Cambios de visibilidad requieren exclusión inmediata y 404 incluso en PDP; comprobar invalidación o filtrar en fuente autorizada. Precios personalizados y respuestas que incluyen disponibilidad requieren composición por petición, sin caché pública compartida. Checkout, arqueo, tokens, vouchers y consultas privadas siempre con bypass. Comparar frío/caliente, hit ratio, latencia, edad de entradas y caída de caché; probar rollback al acceso directo. Dejar constancia de qué capa se midió.

`docs/entregables/sprint-2/cierre.md` reúne por RF/RNF: commit, fecha, entorno, dataset, comando, resultado, URL de artefacto CI, umbral, desviación, responsable y resolución. Conservar cifras de cierre como mediciones fechadas. Los totales de la bitácora y los diagramas de despliegue no acreditan por sí mismos el rendimiento.


## Inspección reproducible

Trabajar sobre una copia de ensayo y registrar SHA, tamaño de tablas, cardinalidad,
versión de motor, índices y estadísticas. `EXPLAIN ANALYZE` ejecuta la sentencia:
limitar los siguientes planes a SELECT, sin mutaciones de negocio.

```bash
PGPASSWORD="$SIGIC_DB_PASSWORD" psql -X -h "$SIGIC_DB_HOST" -p "$SIGIC_DB_PORT" \
  -U "$SIGIC_DB_USER" -d "$SIGIC_DB_NAME" -c '\d+ stock_almacen'
PGPASSWORD="$SIGIC_DB_PASSWORD" psql -X -h "$SIGIC_DB_HOST" -p "$SIGIC_DB_PORT" \
  -U "$SIGIC_DB_USER" -d "$SIGIC_DB_NAME" -c \
  'SELECT schemaname, tablename, indexname, indexdef FROM pg_indexes WHERE schemaname = '''public''' ORDER BY tablename, indexname;'
```

El grano operativo es producto por almacén. Registrar PK/FK, restricciones de
unicidad y planes de las consultas parametrizadas reales. Comparar conexiones
directas y ambos alias con el mismo dataset; verificar ausencia de sobreventa,
rollback multilínea, expiración idempotente y SQLSTATE 42501 del arqueo.

| Evidencia | Estado de esta fase |
|---|---|
| DDL, índice único por almacén/producto y lectura Dapper | Referencia de implementación existente |
| EXPLAIN con volumen realista; p95 frío/caliente | Pendiente |
| Redis/L1/CDN en flujo auditado | Diseño; sin aprovisionamiento demostrado |
| OLAP separado y ETL conciliado | Pendientes de RF-ANA-001 |

La [estrategia arquitectónica de caché](../../arquitectura/estrategia-cache.md)
es diseño. Una política de cinco minutos necesita invalidación para cumplir el
desfase web máximo de treinta segundos. Checkout revalida siempre en OLTP.
