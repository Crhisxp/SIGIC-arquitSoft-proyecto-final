# Guía de pruebas y demostración de SIGIC Ferretería

Ejecute los comandos en Bash sobre Linux desde la raíz del repositorio. Use el SDK de .NET 8,
Node.js 20, Python 3, Docker con Compose, curl y k6. La receta asume que los
puertos locales 54329, 6432, 5000, 3000 y 3001 están libres y que el proyecto
Compose `sigic-evaluacion` tiene una base descartable. El backend de integración,
Newman y k6 escriben datos.

## 1. xUnit y cobertura HTML

Arranque PostgreSQL 16 y PgBouncer. Inicie la API pública después de las pruebas
de integración, para evitar que el worker de reservas compita con sus fixtures.

```bash
docker compose -p sigic-evaluacion up -d --build --wait postgres init-auth pooler
dotnet restore SIGIC.sln
dotnet build SIGIC.sln -c Release --no-restore -warnaserror -m:1
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.5.10
REPORTGENERATOR="$HOME/.dotnet/tools/reportgenerator" bash scripts/ci/test-backend.sh --html
```

Instale ReportGenerator una vez. Si ya tiene la versión instalada, indique su
binario en `REPORTGENERATOR`. El runner mueve el `TestResults/` anterior a un
directorio temporal e imprime la ruta del archivo de evidencia. Ejecútelo
antes de crear los subdirectorios de Vitest, Newman y k6 para la nueva corrida.

El runner ejecuta este comando para cada capa, en el orden Domain, Application,
Infrastructure y WebApi. Para reproducir un caso aislado, use otro directorio
de resultados o archive previamente la evidencia.

```bash
layer=Domain
dotnet test "tests/SIGIC.$layer.Tests/SIGIC.$layer.Tests.csproj" -c Release --no-build \
  --collect:"XPlat Code Coverage" --results-directory "./TestResults/$layer" \
  --logger "trx;LogFileName=$layer.trx"
```

Para regenerar solo el informe HTML con los XML existentes:

```bash
"$HOME/.dotnet/tools/reportgenerator" \
  '-reports:TestResults/**/coverage.cobertura.xml' \
  '-targetdir:TestResults/coverage-html' '-reporttypes:Html'
```

Abra `TestResults/coverage-html/index.html`. El informe HTML reúne los resultados
de las cuatro suites. `TestResults/coverage-summary.json` registra por separado
el porcentaje de líneas de `SIGIC.Domain` cubiertas por su suite unitaria y el
umbral de **95 %**. El runner comprueba que cada TRX tiene pruebas y que todas
pasaron. Para repetir la comprobación:

```bash
python3 scripts/ci/verify-trx.py TestResults
python3 scripts/ci/coverage.py TestResults
```

## 2. Vitest para Web y POS

```bash
npm --prefix src/SIGIC.Web ci
npm --prefix src/SIGIC.POS ci
mkdir -p TestResults/vitest
npm --prefix src/SIGIC.Web test -- --reporter=default --reporter=junit \
  --outputFile.junit="$PWD/TestResults/vitest/web.xml"
npm --prefix src/SIGIC.POS test -- --reporter=default --reporter=junit \
  --outputFile.junit="$PWD/TestResults/vitest/pos.xml"
```

## 3. Catálogo comercial y demostración

Después de xUnit, aplique la migración idempotente de imagen y cargue el catálogo.
La siembra agrega diez SKU de cemento, fierro, herramientas, tuberías y pinturas.
Los SKU existentes conservan su stock.

```bash
docker compose -p sigic-evaluacion exec -T postgres \
  psql -X -U postgres -d postgres -v ON_ERROR_STOP=1 \
  < src/SIGIC.Infrastructure/Persistence/Scripts/03_producto_imagen.sql
docker compose -p sigic-evaluacion exec -T postgres \
  psql -X -U postgres -d postgres -v ON_ERROR_STOP=1 < scripts/seed/seed_comercial.sql
mkdir -p TestResults/seed
docker compose -p sigic-evaluacion exec -T postgres \
  psql -XAt -U postgres -d postgres -v ON_ERROR_STOP=1 -c \
  "SELECT count(*), count(imagen_url) FROM productos
   WHERE id::text LIKE 'c011e000-%' AND imagen_url LIKE '/productos/%.webp'" \
  | tee TestResults/seed/comercial.txt
grep -qx '10|10' TestResults/seed/comercial.txt

docker compose -p sigic-evaluacion up -d --build --wait api web pos
curl -fsS http://127.0.0.1:5000/api/v1/public/catalogo/productos/c011e000-0000-4000-8000-000000000001 \
  | tee TestResults/seed/pdp.json
curl -fsS http://127.0.0.1:3001/productos/cemento.webp \
  -o TestResults/seed/cemento.webp
```

Abra el [portal](http://localhost:3001/) y busque «Cemento». La tarjeta y la
[PDP del cemento](http://localhost:3001/pdp/c011e000-0000-4000-8000-000000000001)
deben mostrar su imagen. El [mostrador](http://localhost:3000/) permite demostrar
la navegación con F1–F12 y Escape.

## 4. Newman para el contrato v2.2

Cargue la fixture de contrato antes de ejecutar la colección. El cajero de
Newman debe iniciar sin turno abierto porque la colección abre y cierra uno.
Si la ejecución se interrumpe con el turno abierto, repita en una base nueva.
La fixture crea usuarios locales de ensayo; las credenciales siguientes solo
corresponden a dichos usuarios. La parte analítica admite el modo determinista
sin clave de Gemini.

```bash
docker compose -p sigic-evaluacion exec -T postgres \
  psql -X -U postgres -d postgres -v ON_ERROR_STOP=1 < tests/postman/fixtures.sql
mkdir -p TestResults/postman
npm exec --yes --package=newman@6.2.3 -- newman run \
  tests/postman/SIGIC-Ferreteria-v2.2.postman_collection.json \
  -e tests/postman/SIGIC-Local.postman_environment.json \
  --env-var baseUrl=http://127.0.0.1:5000/api/v1 \
  --env-var staffUsername=newman.cajero --env-var staffPassword=K6-solo-local \
  --env-var webUsername=newman.cliente --env-var webPassword=K6-solo-local \
  --env-var tesoreriaUsername=newman.tesoreria --env-var tesoreriaPassword=K6-solo-local \
  --env-var almacenId=22222222-2222-2222-2222-222222222222 \
  --env-var tiendaDestinoId=22222222-2222-2222-2222-222222222222 \
  --env-var cajaId=a1520303-0000-4000-8000-000000000001 \
  --env-var productoId=a1520302-0000-4000-8000-000000000001 \
  --env-var productoOcultoId=a1520302-0000-4000-8000-000000000002 \
  --env-var search=NEWMAN --env-var desde="$(date -u +%F)" \
  --env-var hastaExclusivo="$(date -u -d tomorrow +%F)" \
  --timeout-request 10000 --reporters cli,junit \
  --reporter-junit-export TestResults/postman/newman.xml
```

La colección debe completar 24 peticiones y 38 aserciones sin fallos. Incluye
los rechazos de acceso esperados, el cierre ciego y las consultas analíticas.

## 5. Carga y cierre SDD

Siga los comandos y los criterios de lectura de la [guía k6](../../tests/k6/README.md).
Guarde los JSON por escenario y modalidad. Para certificar capacidad a 2.000 VUs,
use la corrida completa y el perfil de recursos del entorno de entrega.

Compile el guardián y reinicie la sesión MCP que lo ejecuta.

```bash
npm --prefix mcp-server ci
npm --prefix mcp-server run build
```

Invoque `sdd_chain_validator` con `strictMode=true`,
`architecture_drift_auditor` sobre `src` y `quality_gate_enforcer` con
`checkType=full` y `coverageThreshold=95`. Compruebe que el último veredicto está
en GO y que `rutaReporte` apunta a `TestResults/coverage-summary.json` de la
corrida. Archive `TestResults/` junto con el SHA evaluado.
