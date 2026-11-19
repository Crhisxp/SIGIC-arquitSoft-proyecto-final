# Carga local de POS y Web

Desde la raíz del repositorio, use k6 y una base **descartable**. Ambos escenarios ejecutan ventas reales sobre la fixture y el escenario web también reserva existencias. El pico mixto suma 1.999 usuarios web y un cajero concurrente. La API debe usar los alias PgBouncer `sigic_pos_pool` y `sigic_web_pool`.

## Con Docker Compose

```bash
docker compose up -d --wait
docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 < tests/k6/fixtures.sql
export BASE_URL=http://127.0.0.1:5000
k6 run tests/k6/pos-checkout-spike.js
k6 run tests/k6/web-catalog-reserve-stress.js
```

## Con dotnet run

En Bash, use el mismo PostgreSQL 16 y PgBouncer 1.22.1 del manifiesto, con la API en el host. Este procedimiento parte de un proyecto Compose descartable con el puerto 5000 libre.

```bash
docker compose up -d --wait postgres init-auth pooler
docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 < tests/k6/fixtures.sql
source scripts/ci/env.sh
export ConnectionStrings__DefaultConnection="Host=$SIGIC_DB_HOST;Port=$SIGIC_DB_PORT;Database=$SIGIC_DB_NAME;Username=$SIGIC_DB_USER;Password=$SIGIC_DB_PASSWORD"
export ConnectionStrings__PosConnection="$SIGIC_POS_CONNECTION_STRING"
export ConnectionStrings__WebConnection="$SIGIC_WEB_CONNECTION_STRING"
export WarehouseSettings__CentralWarehouseId=11111111-1111-1111-1111-111111111111
sigic_key_dir=$(mktemp -d)
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$sigic_key_dir/private.pem"
export JwtSettings__PrivateKeyPem="$(cat "$sigic_key_dir/private.pem")"
export JwtSettings__PublicKeyPem="$(openssl pkey -in "$sigic_key_dir/private.pem" -pubout)"
dotnet run --project src/SIGIC.WebApi --no-launch-profile --urls http://127.0.0.1:5000
```

En otra terminal, desde la misma raíz:

```bash
export BASE_URL=http://127.0.0.1:5000
k6 run tests/k6/pos-checkout-spike.js
k6 run tests/k6/web-catalog-reserve-stress.js
```

## Interpretación

- **POS.** Cinco compradores compiten primero por un SKU con stock comprometido. Solo uno puede obtener la cantidad solicitada. Se mide después el POST de venta durante una rampa de 100, 500, 1.000 y 2.000 VUs. Se exige p95 inferior a 500 ms, más del 99 % de ventas confirmadas y menos del 1 % de fallos HTTP. Al terminar se concilian los movimientos de salida del Kardex con el saldo físico y comprometido del SKU de ensayo. La métrica `sobreventas_o_descuadres` debe valer cero. Ejecute un solo escenario a la vez y reserve esos SKU para la prueba.
- **Web.** Cada usuario consulta el catálogo y crea una reserva de tarjeta cada tres iteraciones. Un cajero mide el mismo POST antes de la carga y durante ella. Se exige que el p95 POS concurrente sea inferior a 250 ms y crezca como máximo un 10 % frente al p95 de referencia. Para observar los pools durante la prueba, ejecute `docker compose exec -T pooler psql -h 127.0.0.1 -p 6432 -U sigic_app -d pgbouncer -c 'SHOW POOLS'` con la contraseña local del manifiesto.
- **TTL.** El escenario compara las fechas de creación y expiración devueltas por la API. RF-WEB-002 y el backend establecen 20 minutos para tarjeta. Para ensayar la expectativa de 15 minutos de esta entrega ejecute `k6 run -e EXPECTED_CARD_TTL_MINUTES=15 tests/k6/web-catalog-reserve-stress.js`. El umbral de TTL falla frente a una reserva real de 20 minutos.
- **Prueba corta.** Añada `-e SMOKE=true` a cada comando k6 para verificar credenciales, respuestas y conciliación con un máximo de dos VUs. Las muestras cortas son sensibles a la variación de latencia. La certificación de capacidad requiere la corrida completa.
- **Repetición.** La siembra conserva ventas, reservas y movimientos previos. Cada ejecución toma un saldo inicial nuevo. Use otra base descartable cuando se agoten las existencias o para comparar corridas desde el mismo estado.
