---
doc: "docs/GUIA-EJECUCION.md"
tipo: "runbook"
estado: "DOCUMENTADO"
aprobado: false
---

# Guía de ejecución de SIGIC Ferretería v2.2

Ejecutar desde la raíz del repositorio. La sección 0 levanta el stack local con
Docker Compose en Windows, Linux y macOS; las secciones 1–8 requieren Bash/Linux
y preparan una BD **descartable** de pruebas, separada del stack de desarrollo.
Los resultados y limitaciones se registran en las actas de
[Sprint 2](entregables/sprint-2/cierre.md) y [Sprint 3](entregables/sprint-3/cierre.md).

## 0. Stack local con Docker Compose (Windows, Linux y macOS)

Requiere Docker con contenedores Linux y Compose v2 ≥2.23.1, con soporte para
`dockerfile_inline` y `configs.content`. En Windows usar Docker Desktop con
virtualización/WSL 2; en Linux, Docker Engine con el plugin Compose; en macOS,
Docker Desktop o un motor compatible. En ARM64 construir para la arquitectura
nativa y verificar las imágenes en el equipo de destino. La primera construcción
necesita acceso a registros de imágenes, NuGet, npm y al release oficial de PgBouncer.

El manifiesto `docker-compose.yml` incluye PostgreSQL 16 con 300 conexiones,
inicialización SQL y SCRAM, PgBouncer 1.22.1 compilado dentro de Linux, API .NET 8,
POS y Web. El contexto `infrastructure/pgbouncer/` excluye los verificadores
estáticos mediante `.dockerignore`: la receta de imagen, el ini, la inicialización
de autenticación y Caddy están integrados en el manifiesto, tal como propone Astra.
Los archivos `pgbouncer.ini`, `pgbouncer.ini.in` y `userlist.txt` existentes conservan
su función de referencia de despliegue o bootstrap Bash; Compose obtiene el mismo
verificador SCRAM del servidor inicializado en un volumen privado.

Con los puertos 54329, 6432, 5000, 3000 y 3001 libres, ejecutar desde PowerShell,
una terminal Linux o macOS:

```text
docker version
docker compose version
docker compose config --quiet
docker compose up -d
docker compose ps -a
```

`docker compose up -d` construye las imágenes que faltan. Para reconstruir y
esperar la disponibilidad de los servicios, usar la variante de aceptación:

```text
docker compose up --build -d --wait
docker compose exec -T postgres psql -U postgres -d postgres -c "SHOW max_connections"
docker compose exec -T pooler psql -h 127.0.0.1 -p 6432 -d pgbouncer -c "SHOW DATABASES"
docker compose exec -T pooler psql -h 127.0.0.1 -p 6432 -d pgbouncer -c "SHOW POOLS"
docker compose logs --tail 80 api pos web
```

`SHOW max_connections` debe devolver 300. El pooler usa transacciones, alias
`sigic_pos_pool` limitado a 50 y `sigic_web_pool` a 30 conexiones, sin reserva extra,
y espera global de cola de 3 s. Corre en primer plano como `postgres`; SQL y la
generación del par RSA se ejecutan dentro de contenedores. El volumen `jwt`
conserva el par de claves entre reinicios. No se requiere Bash ni un pooler en el
host para este modo de ejecución.

Abrir POS en http://localhost:3000 y Web en http://localhost:3001. Caddy sirve
el POS y reenvía Web y `/api` al destino interno, con origen único por canal.
La API también está disponible en http://localhost:5000. Los nombres internos
`postgres:5432`, `pooler:6432`, `api:8080` y `web:3000` solo se usan entre servicios.
Verificar los tres caminos HTTP en Linux/macOS:

```bash
curl -fsS http://localhost:5000/api/v1/public/catalogo/productos
curl -fsS http://localhost:3000/api/v1/public/catalogo/productos
curl -fsS http://localhost:3001/api/v1/public/catalogo/productos
```

En PowerShell:

```powershell
(Invoke-WebRequest http://localhost:5000/api/v1/public/catalogo/productos).StatusCode
(Invoke-WebRequest http://localhost:3000/api/v1/public/catalogo/productos).StatusCode
(Invoke-WebRequest http://localhost:3001/api/v1/public/catalogo/productos).StatusCode
```

Los tres deben responder 200; un catálogo vacío verifica el circuito de lectura.
Las credenciales del manifiesto son de ejemplo para desarrollo local y los
puertos publicados se enlazan a loopback. El backend recibe la cuenta de aplicación;
la cuenta administradora solo inicializa la BD y la autenticación. Los SQL 00/01
preparan esquema y permisos, pero una venta de demostración requiere almacén central,
tienda, productos, tarifas y usuarios staff con hash Argon2id mediante un seed
independiente de las fixtures. El registro web permite crear un cliente.

```text
docker compose stop
docker compose start
docker compose ps -a
docker compose down
```

`stop` pausa y `start` reanuda; repetir las comprobaciones HTTP tras reiniciar
con los mismos volúmenes. `down` retira contenedores y conserva los volúmenes
nombrados `datos`, `auth` y `jwt`. La inicialización SQL solo corre con un volumen
PostgreSQL vacío. Para ensayar otra instalación usar otro nombre de proyecto y
puertos libres. Las pruebas del fixture deben usar una BD descartable separada
del backend y su worker, siguiendo las secciones 1–8. Producción requiere gestión
propia de secretos, datos y ciclo de claves.

La validación de YAML comprueba estructura; la aceptación del stack requiere
`docker compose config --quiet`, construcción, healthchecks, tres smoke tests y
reinicio con volúmenes al menos en Windows y Linux. Registrar macOS/ARM64 cuando
se ensaye; un CI Linux satisfactorio no certifica las demás plataformas.

## 1. Requisitos y evidencia del entorno

Instalar Docker con acceso al daemon, cliente PostgreSQL, SDK .NET 8, Node 20,
npm, Python 3, OpenSSL, curl, make, compilador, pkg-config, libevent y OpenSSL de
desarrollo. Caddy se usa para el proxy local del Portal Web.

```bash
git rev-parse HEAD
dotnet --list-sdks
node --version
npm --version
docker --version
docker info
psql --version
python3 --version
# Ubuntu 24.04, dependencias del cliente y compilación de PgBouncer:
sudo apt-get update
sudo apt-get install -y postgresql-client build-essential pkg-config libevent-dev libssl-dev curl openssl
```

Los puertos 54329 (PostgreSQL), 6432 (PgBouncer), 5000 (API), 3000 (POS),
3001 (Next) y 3002 (proxy Web) deben estar libres. PgBouncer se ejecuta como usuario
sin privilegios, fijado a **1.22.1**. CI usa `ubuntu-24.04`, .NET 8 y Node 20.

## 2. PostgreSQL vacío y pools autenticados

`PostgreSqlTestFixture` lee estas nueve variables; los valores del servicio CI y
de ambos alias coinciden con ellas. Las claves de ejemplo son efímeras y exclusivas
de este contenedor. La API necesita además `ConnectionStrings__*` en la sección 3.

```bash
source scripts/ci/env.sh
# Valores por defecto, sobrescribibles antes de source:
# SIGIC_DB_HOST=127.0.0.1    SIGIC_DB_PORT=54329    SIGIC_DB_NAME=postgres
# SIGIC_DB_ADMIN_USER=postgres   SIGIC_DB_ADMIN_PASSWORD=sigic_ci_ephemeral
# SIGIC_DB_USER=sigic_app        SIGIC_DB_PASSWORD=sigic_fixture_ephemeral
# SIGIC_POS_CONNECTION_STRING='Host=127.0.0.1;Port=6432;Database=sigic_pos_pool;Username=sigic_app;Password=sigic_fixture_ephemeral;Pooling=false'
# SIGIC_WEB_CONNECTION_STRING='Host=127.0.0.1;Port=6432;Database=sigic_web_pool;Username=sigic_app;Password=sigic_fixture_ephemeral;Pooling=false'
docker run -d --name sigic-blueprint-postgres -p 127.0.0.1:54329:5432 \
  -e POSTGRES_USER="$SIGIC_DB_ADMIN_USER" -e POSTGRES_PASSWORD="$SIGIC_DB_ADMIN_PASSWORD" \
  -e POSTGRES_DB="$SIGIC_DB_NAME" postgres:16 -c max_connections=300
bash scripts/ci/bootstrap.sh
# Copiar el export SIGIC_POOL_DIR=... emitido al terminar: se usa para diagnóstico y parada.
```

El bootstrap espera PostgreSQL hasta 60 s, rechaza una BD ya inicializada, aplica
`src/SIGIC.Infrastructure/Persistence/Scripts/00_init_schema.sql` y
`01_append_only_permissions.sql` como administrador y fija la contraseña SCRAM
de `sigic_app`. No convierte la cuenta app en superusuario ni propietaria de tablas.
Extrae el mismo verificador SCRAM al `userlist.txt` temporal, privado (umask 077).

Usa el binario local si es exactamente 1.22.1; de lo contrario descarga el release
oficial, comprueba SHA256 y compila fuera del checkout. Renderiza la plantilla
compartida `infrastructure/pgbouncer/pgbouncer.ini.in`: POS 50, Web 30,
`pool_mode=transaction`, `query_wait_timeout=3` global y escucha en 127.0.0.1:6432.
No depende de una IP de bridge fija. El ini estático es referencia de despliegue.

En CI el job crea primero `sigic-ci-postgres` y luego ejecuta el bootstrap. El script
exporta su directorio temporal a `GITHUB_ENV` aun si un paso posterior falla.
Se verifican ambos alias autenticados, `SHOW POOLS` y ausencia de UPDATE/DELETE
efectivos en `caja_arqueo`.

```bash
export PGHOST="$SIGIC_DB_HOST" PGUSER="$SIGIC_DB_USER" PGPASSWORD="$SIGIC_DB_PASSWORD"
psql -X -v ON_ERROR_STOP=1 -p 6432 -d sigic_pos_pool -c 'SELECT current_user, current_database()'
psql -X -v ON_ERROR_STOP=1 -p 6432 -d sigic_web_pool -c 'SELECT current_user, current_database()'
psql -X -v ON_ERROR_STOP=1 -p 6432 -d pgbouncer -c 'SHOW POOLS'
psql -XAt -v ON_ERROR_STOP=1 -p "$SIGIC_DB_PORT" -d "$SIGIC_DB_NAME" -c \
  "SELECT has_table_privilege(current_user,'caja_arqueo','UPDATE') OR has_table_privilege(current_user,'caja_arqueo','DELETE')"
# Debe imprimir f. La suite Infrastructure comprueba SQLSTATE 42501.
```

El timeout global comparte la política de cola entre ambos alias en el ensayo;
para políticas distintas en producción hacen falta instancias separadas y pruebas
de sus DSN. `pool_size=50` no acredita precalentamiento ni adquisición submilisegundo.
El ensayo usa `max_connections=300` para dar margen a las conexiones de fixtures,
pools y administración. CI verifica explícitamente PostgreSQL 16 y el valor 300
mediante SQL antes de ejecutar las suites. Consultar configuración de
[PgBouncer 1.22.1](https://raw.githubusercontent.com/pgbouncer/pgbouncer/pgbouncer_1_22_1/doc/config.md)
y [distribución oficial](https://www.pgbouncer.org/downloads/).

## 3. Backend .NET 8

En una terminal con las variables anteriores, generar ambas claves del mismo par
para emitir y validar JWT. La clave pública estática de appsettings no basta.

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection="Host=$SIGIC_DB_HOST;Port=$SIGIC_DB_PORT;Database=$SIGIC_DB_NAME;Username=$SIGIC_DB_USER;Password=$SIGIC_DB_PASSWORD"
export ConnectionStrings__PosConnection="$SIGIC_POS_CONNECTION_STRING"
export ConnectionStrings__WebConnection="$SIGIC_WEB_CONNECTION_STRING"
sigic_key_dir=$(mktemp -d)
chmod 700 "$sigic_key_dir"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$sigic_key_dir/private.pem"
export JwtSettings__PrivateKeyPem="$(cat "$sigic_key_dir/private.pem")"
export JwtSettings__PublicKeyPem="$(openssl pkey -in "$sigic_key_dir/private.pem" -pubout)"
dotnet restore SIGIC.sln
dotnet build SIGIC.sln --configuration Release --no-restore -warnaserror
dotnet run --project src/SIGIC.WebApi/SIGIC.WebApi.csproj --no-launch-profile --urls http://localhost:5000
```

El DDL no deja una tienda operable. En una **instancia de desarrollo separada**
preparar almacén central `11111111-1111-1111-1111-111111111111` (o ajustar
`WarehouseSettings__CentralWarehouseId`), tienda destino, productos visibles y
ocultos, tarifas, stock por almacén, cajas y turnos; usuarios staff/web con hash
Argon2id y roles de Cajero, Tesoreria y Superadmin. Tomar como modelo las siembras
de `tests/SIGIC.WebApi.Tests/CustomWebApplicationFactory.cs` y las suites de API.
Para cargar el catálogo de demostración, usar `scripts/seed/seed_comercial.sql`.
La carga agrega diez productos con imágenes y stock central, dos tiendas y tarifas
de ejemplo. Se ejecuta manualmente y no crea usuarios. En un volumen existente,
aplicar primero la migración `03_producto_imagen.sql`.

```bash
docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 < src/SIGIC.Infrastructure/Persistence/Scripts/03_producto_imagen.sql
docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 < scripts/seed/seed_comercial.sql
```

## 4. Mostrador POS — React 18 / Vite

En otra terminal:

```bash
npm --prefix src/SIGIC.POS ci
npm --prefix src/SIGIC.POS run dev
# Vite exige puerto 3000; /api se envía por proxy a localhost:5000.
curl -fsS http://localhost:5000/api/v1/public/catalogo/productos
curl -fsS http://localhost:3000/api/v1/public/catalogo/productos
```

Abrir 3000, probar login staff, apertura de turno, escáner/teclado, cobro, despacho
F6 y cierre ciego con el seed preparado. Medir 1024×768 y foco en navegador.
Compilación: `npm --prefix src/SIGIC.POS run build`.

## 5. Portal Web — Next.js 14

Para inspección del catálogo público con URL absoluta:

```bash
npm --prefix src/SIGIC.Web ci
NEXT_PUBLIC_API_URL=http://localhost:5000/api/v1 npm --prefix src/SIGIC.Web run dev -- -p 3001
```

La página actual usa `use client`/`useEffect`, y la API no configura CORS.
Para login y POST desde navegador utilizar origen único. Detener el Next anterior
y ejecutar:

```bash
NEXT_PUBLIC_API_URL=/api/v1 npm --prefix src/SIGIC.Web run dev -- -p 3001
```

Con Caddy instalado, en una cuarta terminal:

```bash
caddy run --config - --adapter caddyfile <<'CADDY'
http://localhost:3002 {
    handle /api/v1/* {
        reverse_proxy 127.0.0.1:5000
    }
    handle {
        reverse_proxy 127.0.0.1:3001
    }
}
CADDY
```

Abrir **http://localhost:3002**, probar login web, PDP `/pdp/[id]`, reserva y
voucher. `curl` verifica HTTP, pero no reproduce las reglas CORS del navegador.
En producción utilizar el proxy perimetral. Para compilar y servir el mismo origen:

```bash
NEXT_PUBLIC_API_URL=/api/v1 npm --prefix src/SIGIC.Web run build
npm --prefix src/SIGIC.Web run start -- -p 3001
```

## 6. Regresión y cobertura

El runner de backend archiva el `TestResults/` anterior en un directorio temporal
que indica al inicio y crea uno limpio para la nueva corrida.
La receta de evaluación con ReportGenerator, Newman y k6 está en
`docs/pruebas/GUIA-PRUEBAS-Y-DEMOSTRACION.md`.

```bash
source scripts/ci/env.sh
dotnet restore SIGIC.sln
dotnet build SIGIC.sln --configuration Release --no-restore -warnaserror
bash scripts/ci/test-backend.sh
npm --prefix src/SIGIC.POS test
npm --prefix src/SIGIC.Web test
```

El runner ejecuta Domain, Application, Infrastructure y WebApi secuencialmente con
`--no-build --collect:"XPlat Code Coverage"`, TRX y directorios distintos. Esto evita
carreras entre assemblies que comparten esquema, sin quitar concurrencia interna
a los casos. Verificar cero fallos y cero pruebas omitidas en los reportes.
Como alternativa, en otra corrida vacía:

```bash
dotnet test SIGIC.sln -c Release --no-build -m:1 --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults --logger "trx;LogFilePrefix=sigic"
```

Esta alternativa genera reportes sin carpetas por capa; usar el runner para el gate
de CI que exige la carpeta `Domain`. El comando de revisión es
`python3 scripts/ci/coverage.py TestResults`: combina por ensamblado/archivo/línea,
une visitas por OR, publica numerador y denominador conjuntos y por ensamblado,
y exige ≥95 % a SIGIC.Domain dentro de su suite unitaria. Las copias adjuntas al
TRX y los tipos compilados del mismo archivo no duplican el denominador.
`TestResults/coverage-summary.json` pertenece a esa corrida; el informe local
histórico `coverage/coverage-summary.json` es evidencia distinta.

CI ejecuta `npm ci`, Vitest y build para ambos canales con Node 20; guarda JUnit,
TRX y Cobertura mediante `upload-artifact@v4` incluso ante fallo. Los diagnósticos
de PostgreSQL/PgBouncer se sanitizan antes de subirse. No publicar userlist ni claves.

## 7. Validación documental y entregables pendientes

Ejecutar exactamente ambos bloques Python del gate en memoria, sin crear un vault:

```bash
python3 - <<'PY'
from pathlib import Path
import re, textwrap
source = Path('.github/workflows/sigic-sdd-gate.yml').read_text()
blocks = re.findall(r"<< 'EOF'\n(.*?)\n          EOF", source, re.S)
assert len(blocks) == 2
for block in blocks:
    exec(compile(textwrap.dedent(block), '<sdd-gate>', 'exec'), {})
PY
```

Se exigen 85 nodos alcanzables desde AGENTS, 107 aristas, igualdad exacta de sets,
cero ciclos, enlaces rotos y wikilinks, y ≤4 destinos por nodo. El MCP de cadena
comprueba frontmatter por separado; no ejecutar `obsidian_vault_sync` sobre el DAG.
`docs/jwe/flattened/decrypt/functions/flattenedDecrypt.md` está vacío y es legado
fuera del recorrido; su procedencia requiere revisión independiente.

Los comandos siguientes son de **aceptación futura**, después de adaptar k6 y
Playwright, disponer de seed aislado y completar el entorno de Newman. No equivalen
a resultados obtenidos en esta fase:

```bash
k6 run --summary-export=/tmp/sigic-k6.json tests/k6/checkout-stress-test.js
newman run tests/postman/sigic-sprint-2.postman_collection.json \
  -e "$SIGIC_POSTMAN_ENV" --bail --reporters cli,junit \
  --reporter-junit-export /tmp/sigic-postman.xml
# Una vez añadido el runner/config reproducible de Playwright:
npx playwright test tests/e2e/checkout-omnicanal.spec.ts
```

## 8. Diagnóstico y parada

Ante 28P01 comprobar usuario, clave y verificador SCRAM compartido; ante 3D000
comprobar alias y BD; ante conexión rechazada comprobar puertos y readiness. Ante
42501 en arqueo, comprobar si la operación era un intento prohibido: no devolver
privilegios UPDATE/DELETE. Si Web recibe timeout bajo saturación, verificar 30
conexiones retenidas y el timeout global de 3 s. Latencias del runner compartido
no certifican por sí solas capacidad LAN industrial.

```bash
# Con SIGIC_POOL_DIR del bootstrap:
python3 scripts/ci/sanitize-log.py "$SIGIC_POOL_DIR/pgbouncer.log"
docker logs sigic-blueprint-postgres 2>&1 | python3 scripts/ci/sanitize-log.py
bash scripts/ci/stop-pgbouncer.sh
docker stop sigic-blueprint-postgres
docker rm sigic-blueprint-postgres
# Parar API, POS, Next y Caddy con Ctrl-C en sus terminales.
# Tras parar la API, eliminar solo el directorio de claves de esta sesión:
test -n "${sigic_key_dir:-}" && rm -r -- "$sigic_key_dir"
unset JwtSettings__PrivateKeyPem JwtSettings__PublicKeyPem PGPASSWORD
```

No detener contenedores ajenos ni usar la receta sobre una BD de negocio. Conservar
los artefactos del SHA de aceptación antes de iniciar otra corrida.
