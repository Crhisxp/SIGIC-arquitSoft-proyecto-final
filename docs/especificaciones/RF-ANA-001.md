---
doc: "RF-ANA-001"
tipo: "requisito-funcional"
estado: "DOCUMENTADO"
aprobado: false
---

# RF-ANA-001 — Copiloto Analítico IA

**Estado:** especificación propuesta, implementación y aceptación pendientes.
**Actor:** Superadmin interno. **Prioridad:** Sprint 3.
**Trazabilidad:** HU-10, RF-REP-001/002/007, RNF-PERF-002/003.
El backend permanece un monolito modular; la consulta analítica es transversal y
no introduce un séptimo contexto transaccional.


**Artefacto canónico** `docs/especificaciones/RF-ANA-001.md`. Incorporar una fila en la matriz funcional y una HU del actor Superadmin: «Quiero preguntar por ventas, tickets y ticket promedio de un periodo para comparar Mostrador y Web y poder comprobar la cifra usada en la respuesta». Trazar a RF-REP-001/002/007 y RNF-PERF-002/003. El corte inicial abarca ventas efectivas en PEN y esas tres métricas. Margen, pronósticos y disponibilidad de inventario requieren contratos de datos propios. El checkout contiene OLTP y describe un datamart, pero carece de migración OLAP, ETL productivo, adaptador Gemini y página administrativa. El proyecto de calidad debe presupuestar esas dependencias.

**Contrato HTTP propuesto.** `POST /api/v1/admin/analitica/consultas`, Bearer interno. Petición JSON: `{"pregunta":"¿Cuál fue el ticket promedio por canal?","desde":"2026-10-01","hastaExclusivo":"2026-11-01","canales":["POS","WEB"]}`. Pregunta entre 1 y 500 caracteres, canales de enumeración, fechas ISO, inicio menor que fin, rango máximo 366 días por petición, calendario America/Lima. Datos históricos de prueba de 24 meses. Resolver periodo, canales y universo desde filtros explícitos. Ante ambigüedad pedir precisión. Prohibir cambios de filtro implícitos por el modelo. Resolver métrica por catálogo de intenciones soportadas; aceptar también elección explícita en UI para desambiguar.

Respuesta 200 de ejemplo de contrato con **datos ficticios**:
```json
{
  "consultaId": "e627ed24-e366-433c-8405-204eee0cbc8f",
  "estado": "con_datos",
  "metrica": "ticket_promedio",
  "periodo": {"desde":"2026-10-01","hastaExclusivo":"2026-11-01","zona":"America/Lima"},
  "lote": "etl-20261101-01",
  "corteUtc": "2026-11-01T05:00:00Z",
  "hechos": [{"id":"h1","canal":"POS","valor":"32.50","unidad":"PEN","tickets":2}],
  "respuesta": "El ticket promedio de Mostrador fue S/ 32,50.",
  "referencias": ["h1"],
  "modo": "asistido"
}
```

Usar decimales exactos y redondeo monetario explícito a 2 posiciones al presentar. Para el ejemplo, la BD del caso de prueba contiene dos ventas POS por S/ 65 en total y ninguna Web. Identificar en UI los canales sin observaciones. `sin_datos` devuelve lista vacía, corte conocido y texto fijo. `requiere_aclaracion` devuelve opciones sin cifras. Si el ETL aún no publicó un corte completo para el periodo, devolver `datos_incompletos` con fecha cubierta, sin cifras concluyentes. 400 para forma/rango inválidos; 401 para firma, expiración o credencial ausente; 403 para audiencia cruzada válida o rol insuficiente; 429 por cuota; 503 para datamart indisponible. Un fallo del proveedor con hechos válidos devuelve 200 `modo:"determinista"`. Todas las respuestas privadas con `Cache-Control: no-store`.

| Capa y ubicación propuesta | Responsabilidad y contrato |
|---|---|
| `SIGIC.Domain` | Mantener invariantes monetarias y de stock existentes. La lectura de gerencia no crea entidades transaccionales. |
| `SIGIC.Application/Analytics/` | DTOs inmutables `ConsultaAnalitica`, `FiltroAnalitico`, `SnapshotAnalitico`, `Hecho`, `PlanRespuesta`. `ConsultarAnaliticaQueryHandler.Handle(consulta, CancellationToken)` exige rol al inicio, valida filtros, resuelve intención, solicita exactamente un snapshot y entrega hechos + respuesta. Puerto `ILecturaAnalitica.LeerAsync(filtro, ct) → SnapshotAnalitico`; `IAsistenteAnalitico.PlanificarAsync(intencion, hechosMinimos, ct) → PlanRespuesta`. Tipos del proveedor y Dapper terminan en Infrastructure. |
| `SIGIC.Infrastructure/Adapters/Analytics/` | `PostgresLecturaAnalitica` usa una conexión exclusiva `ConnectionStrings:AnalyticsReadOnly` a la BD del datamart con rol lector. `GeminiAnaliticaClient` usa `HttpClient` tipado registrado con `AddHttpClient` (declarar dependencia .NET 8 correspondiente si se registra en Infrastructure). Inyectar opciones validadas en arranque habilitado, URL HTTPS fija de Google, modelo configurado y clave del almacén de secretos. |
| `SIGIC.WebApi` | Controlador nuevo, inyección del handler y registro en composición. Ampliar clasificación interna de `UserContextMiddleware` a segmentos `/api/v1/admin`. Validar RS256 mediante `RsaJwtTokenService`, issuer `SIGIC_Backend`, expiración y audiencia exacta `SIGIC_InternalStaff`; reusar `IAuthorizationService.ExigirRol("Superadmin")`. Un token de cliente válido devuelve 403 aun si contiene un rol de igual nombre. Ningún intento denegado toca OLAP ni Gemini. |
| `src/SIGIC.Web/src/app/admin/analitica/page.tsx` | Sesión interna independiente del `AuthContext` de clientes; entrada staff por `/api/v1/auth/login-staff`, cookie de sesión HttpOnly/Secure/SameSite y validación en servidor antes del render y del POST. En el BFF validar origen/CSRF, transmitir Bearer solo al backend y mantener la clave Gemini en Infrastructure. Mostrar filtros, consulta, corte, fuente y unidades; tabla navegable con teclado, foco visible y números tabulares según DESIGN. Estados inicial, carga cancelable, vacío, aclaración, resultado, datos parciales, expirado, denegado, error y reintento. |

**Datos y SQL de referencia para implementar.** Proponer vista agregada `reporting.ventas_diarias` dentro de la **BD separada** `sigic_olap`, con clave única `(lote, dia_local, canal)`, totales decimales, tickets enteros y registro de control de lote/intervalo completo. ETL nocturno incremental: extraer `ventas_pos.id,total,fecha_utc` por ticket y pedidos Web efectivamente pagados (`PagadoConfirmado` es transición observada en `ValidarVoucherPagoCommandHandler`). Para manual tomar hora efectiva validada de comprobante; para pasarela exigir hecho persistido de liquidación. Si falta ese hecho o una reversión, señalar cobertura incompleta. Fijar política contable para anulaciones antes de certificar. Excluir pendientes, expirados y rechazados. Grano de extracción único por canal e id de transacción; evitar multiplicación por líneas/pagos. Convertir UTC a día local antes de agrupar, reimportar correcciones por clave, conciliar conteos y soles con la fuente, publicar lote completo y watermark de forma atómica. El lector fija el lote publicado en una transacción breve de solo lectura y cierra la conexión antes de llamar al modelo.

```sql
SELECT canal, SUM(importe_pen) AS ventas_pen, SUM(tickets) AS tickets,
       SUM(importe_pen) / NULLIF(SUM(tickets), 0) AS ticket_promedio_pen
FROM reporting.ventas_diarias
WHERE lote = @LotePublicado AND dia_local >= @Desde AND dia_local < @Hasta
  AND canal = ANY(@Canales)
GROUP BY canal ORDER BY canal;
```

SQL constante con parámetros Dapper tipados, máximo dos filas para los dos canales. Para un total combinado dividir sumas globales. Un rol separado de ETL publica la vista; al lector solo CONNECT, USAGE y SELECT de vistas y watermark, sin privilegios heredados de aplicación, propiedad ni CREATE. Definir `default_transaction_read_only` y `statement_timeout`, verificar privilegios efectivos. La consulta al datamart debe cumplir p95 ≤5 s en la muestra de 24 meses; medir aparte espera al proveedor y latencia HTTP total. Comparar POS con y sin ETL dentro del límite de degradación del 10 %.

**Generación verificable.** Enviar por HTTPS exclusivamente intención normalizada, aliases de hechos y dimensiones permitidas. Sin SQL, DNI, tickets individuales, cuentas, JWT, datos de caja o instrucciones libres procedentes del cliente. `POST /v1beta/models/{modelo}:generateContent`, `x-goog-api-key`, respuesta JSON restringida a `plantilla` y `idsHechos`. El adaptador interpreta `candidates[].content.parts`, valida tamaño y esquema; el handler comprueba IDs, métrica, filtros, cobertura y semántica de la plantilla. Comparaciones y rankings se calculan en el servidor. El renderizador sustituye valores exclusivamente desde las filas SQL. Así se evita que una cifra o conclusión inventada llegue al usuario. Descartar texto libre, IDs desconocidos y selecciones sesgadas/incompletas. En esos casos renderizar la tabla y el resumen fijo a partir del mismo snapshot. Presupuesto propuesto de 4 s para Gemini, una petición por consulta, propagación de cancelación y límite de concurrencia independiente de POS. Auditar actor, hora, huella de filtros, lote, plantilla, IDs, duración y modo, sin contenido sensible. [Contrato REST de Gemini](https://ai.google.dev/api/generate-content)

**Aceptación de Sprint 3.** (1) Tests de Application con dobles y fixture monetario de S/ 65, filas vacías, corte incompleto, intento de cambiar periodo e ID fabricado. (2) Integración con PostgreSQL y rol real: igualdad contra agregados conocidos, permisos de solo lectura, segunda ejecución ETL sin doble conteo, frontera de medianoche local y promedio ponderado. (3) Adaptador con HttpMessageHandler simulado para JSON válido, respuesta bloqueada, 429, 5xx, timeout y cancelación, sin clave externa en CI. (4) WebApi con la matriz público/cliente/Cajero/Superadmin, expiración y comprobación de cero invocaciones a los puertos en denegaciones. (5) UI muestra fuente y corte, recupera sesión expirada y respeta teclado. (6) Prueba controlada con proveedor habilitado en entorno de aceptación y cuota acotada; registrar modelo y modo sin secretos. Registrar resultados en `docs/entregables/sprint-3/cierre.md`.


## Matriz de aceptación verificable

| Caso | Resultado obligatorio |
|---|---|
| AC-ANA-01: dos ventas POS que suman S/ 65 | 2 tickets, total 65.00 PEN, promedio 32.50 PEN; Web sin observaciones |
| AC-ANA-02: vacío, periodo ambiguo o lote parcial | `sin_datos`, `requiere_aclaracion` o `datos_incompletos`; sin cifras concluyentes inventadas |
| AC-ANA-03: cliente o Cajero; token ausente/expirado | 403 o 401 según contrato, cero llamadas a OLAP/proveedor |
| AC-ANA-04: lote reimportado y frontera UTC/Lima | Igualdad con fuente, sin doble conteo, promedio combinado ponderado |
| AC-ANA-05: proveedor falla o fabrica ID/filtro | Respuesta determinista desde el mismo snapshot, tabla y corte visibles |
| AC-ANA-06: rol lector y carga simultánea | Sin permisos de escritura; p95 consulta ≤5 s y degradación POS ≤10 % en entorno medido |

Presentación monetaria: conservar decimal exacto en cálculos y redondear al
presentar a dos posiciones, mitades alejándose de cero. Nunca promediar promedios
diarios o de canales. El modelo no ejecuta SQL ni calcula cifras.
