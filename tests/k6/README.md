# Carga de POS y Web

Ejecute k6 desde la raíz contra el stack local con PostgreSQL 16 y los alias
PgBouncer `sigic_pos_pool` (50 conexiones) y `sigic_web_pool` (30 conexiones,
espera de cola de 3 s). Los escenarios venden y reservan en una base descartable.

## Preparación y ejecución

```bash
docker compose -p sigic-evaluacion up -d --build --wait api
docker compose -p sigic-evaluacion exec -T postgres \
  psql -X -U postgres -d postgres -v ON_ERROR_STOP=1 < tests/k6/fixtures.sql
docker compose -p sigic-evaluacion exec -T postgres \
  psql -X -U postgres -d postgres -v ON_ERROR_STOP=1 < tests/k6/fixtures-capacity.sql
mkdir -p TestResults/k6
export BASE_URL=http://127.0.0.1:5000

k6 run -e LOAD_MODE=contention --summary-export=TestResults/k6/pos-contention.json tests/k6/pos-checkout-spike.js
k6 run -e LOAD_MODE=contention --summary-export=TestResults/k6/web-contention.json tests/k6/web-catalog-reserve-stress.js
k6 run -e LOAD_MODE=capacity --summary-export=TestResults/k6/pos-capacity.json tests/k6/pos-checkout-spike.js
k6 run -e LOAD_MODE=capacity --summary-export=TestResults/k6/web-capacity.json tests/k6/web-catalog-reserve-stress.js
```

Con la API ejecutada en el host, use el arranque y las cadenas de conexión
de la [guía de ejecución](../../docs/GUIA-EJECUCION.md). La URL base se pasa sin
`/api/v1`. Ejecute un escenario por vez. Para una comprobación corta, añada
`-e SMOKE=true` y use otro nombre de resumen. Este modo usa dos VUs y etapas de
2 s. `-e VUS=50` permite progresar por escalones antes de la corrida predeterminada
de 2.000 VUs. La prueba corta comprueba la fixture y el contrato. Informa la
razón de p95 del cajero, cuyo umbral se aplica en la prueba completa.

## Contención y capacidad

| Modo | Qué mide | Fixture y comprobaciones |
| --- | --- | --- |
| `contention` (predeterminado) | Conducta cuando varias transacciones bloquean las mismas filas | POS inicia una carrera de cinco solicitudes por `K6-CARRERA`. Debe haber un HTTP 201, cuatro HTTP 409 y conciliación exacta de Kardex, físico y comprometido. La rampa comparte `K6-POS` y un turno. Web comparte `K6-WEB` y mantiene un cajero concurrente. |
| `capacity` | Latencia y errores al retirar la contención artificial por SKU y turno de la fixture | Cada VU reserva para un SKU distinto. En POS, cada VU también registra contra un turno distinto. Se exige cero 409, p95 POS menor a 500 ms en el pico y saldos válidos en todos los carriles. En el escenario mixto, el p95 del cajero debe ser menor a 250 ms y como máximo 110 % de su referencia sin carga. |

En ambos modos, el portal consulta el catálogo en cada iteración y solicita una
reserva de tarjeta cada tres iteraciones. El modo mixto predeterminado usa hasta
1.999 VUs web y un cajero. La capacidad distribuida usa la misma infraestructura,
autenticación, aplicación y pools del stack. Registre los recursos asignados y la
versión del generador junto con los resultados.

Para observar el 409 Web en una comprobación corta de contención, deje dos
unidades disponibles del SKU compartido antes de ejecutar ese escenario en la
base descartable. El modo de capacidad reserva sobre otros SKU.

```bash
docker compose -p sigic-evaluacion exec -T postgres psql -X -U postgres -d postgres \
  -v ON_ERROR_STOP=1 -c "UPDATE stock_almacen SET stock_fisico = stock_comprometido + 2
    WHERE producto_id = 'e5a40b8e-5b12-4f2b-8a71-6c2e3f890123';"
```

## Lectura de umbrales

- `rechazos_stock` registra HTTP 409 de ventas y reservas. La API usa ese
  estado para stock insuficiente en estos endpoints. En contención se informa
  su tasa y se exige el resultado exacto de la carrera POS. En capacidad, un
  409 incumple el escenario y obliga a revisar los saldos de partida.
- `fallos_tecnicos` exige menos del 1 % por operación. Incluye 5xx, HTTP 408 y
  estado 0 de k6 (timeout o falta de respuesta por otro error de transporte).
  `respuestas_inesperadas` debe quedar en cero y detecta, por ejemplo, un
  401/403 por credenciales o un 404 por un SKU ausente.
- `reservas_confirmadas` y `ventas_confirmadas` exigen al menos un HTTP 201.
  `ttl_reserva_correcto` verifica en todos los HTTP 201 de reserva la diferencia
  entre `fechaExpiracionUtc` y `fechaCreacionUtc`. El valor esperado por defecto
  es **20 minutos** para tarjeta (RF-WEB-002). La opción
  `EXPECTED_CARD_TTL_MINUTES` permite probar que el umbral falla con una
  expectativa deliberadamente incorrecta.
- El SKU compartido serializa escrituras sobre la misma fila y POS también
  bloquea la fila del turno. Esa espera puede elevar la latencia y producir
  timeouts incluso con inventario suficiente. Los resúmenes antiguos mezclaban
  fallos HTTP y 409. Para atribuir un fallo de capacidad a la cola del pool,
  capture `cl_waiting`, `maxwait` y `sv_active` de `SHOW POOLS` durante el pico,
  junto con los logs de la API y las métricas por operación.

```bash
docker compose -p sigic-evaluacion exec -T pooler \
  psql -X -h 127.0.0.1 -p 6432 -U sigic_app -d pgbouncer -c 'SHOW POOLS'
```

Las siembras conservan ventas, reservas y movimientos previos. Para comparar
corridas con el mismo saldo inicial, use una base descartable nueva.
