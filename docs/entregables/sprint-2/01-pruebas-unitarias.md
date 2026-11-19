---
doc: "docs/entregables/sprint-2/01-pruebas-unitarias.md"
tipo: "verificacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Verificación de Pruebas Unitarias de Dominio — SIGIC Ferretería v2.2

Este documento describe la especificación y estructura de las pruebas unitarias que validan la lógica de negocio pura del núcleo de dominio de SIGIC Ferretería v2.2, implementadas con xUnit sobre .NET 8.0.

## 1. Configuración del Proyecto de Pruebas

El proyecto de pruebas de dominio está definido en [SIGIC.Domain.Tests.csproj](../../../tests/SIGIC.Domain.Tests/SIGIC.Domain.Tests.csproj), configurado bajo el SDK `Microsoft.NET.Sdk` con target `net8.0`, tipos anulables (`Nullable`) habilitados y usings implícitos. La importación global del framework de aserciones se centraliza en [GlobalUsings.cs](../../../tests/SIGIC.Domain.Tests/GlobalUsings.cs).

## 2. Comportamientos Comprobados en Entidades de Dominio

La suite de pruebas contiene dos clases principales de validación:

### 2.1. Pruebas de Inventario y Precios
La clase [ProductoTests.cs](../../../tests/SIGIC.Domain.Tests/ProductoTests.cs) comprueba los métodos de la entidad raíz `Producto`:
- `a_StockDisponible_DebeSerIgualA_StockFisicoMenosStockComprometido`: verifica la invariante fundamental `RNF-CON-003` (Stock Disponible = Stock Físico - Stock Comprometido).
- `b_ReservarStock_Exitoso_CuandoCantidadEsMenorOIgualA_StockDisponible`: valida el incremento de stock comprometido sin alterar el stock físico ante reservas válidas.
- `c_ReservarStock_LanzaStockInsuficienteException_CuandoCantidadSuperaStockDisponible`: asegura el lanzamiento de `StockInsuficienteException` si la cantidad solicitada supera el disponible.
- `d_LiberarReserva_RestauraStockComprometido`: verifica la reversión exacta del stock comprometido ante cancelaciones o expiraciones de reservas.
- `e_ActualizarPrecios_RechazaMargenNegativo_CuandoPrecioVentaEsMenorACostoUnitario`: comprueba el rechazo con `MargenInvalidoException` cuando se intenta fijar un precio inferior al costo (`RF-PRE-002`).
- Métodos adicionales para validación de constructores, salidas físicas y recálculo de costo promedio ponderado.

### 2.2. Pruebas de Cierre de Caja
La clase [ArqueoCajaTests.cs](../../../tests/SIGIC.Domain.Tests/ArqueoCajaTests.cs) valida la entidad `ArqueoCaja` y el record `ArqueoDenominacion`:
- `a_ConteoFisicoInmutable_SumaExactamenteDenominacionesDeBilletesYMonedas`: suma determinista de las denominaciones monetarias declaradas.
- `b_CalculoExactoDeFaltanteYSobrante_NoMutaElRegistroPrevio`: cálculo de diferencia contable preservando la inmutabilidad de la instancia original (`ConSaldoTeorico`).
- `c_ValidacionDeTiposDeDatosDecimal_GarantizaPrecisionMonetariaSinPuntoFlotante`: garantiza el uso estricto de tipos `decimal` para evitar errores de redondeo de punto flotante.
- Métodos de validación frente a argumentos fuera de rango y denominaciones no positivas.

## 3. Delimitación y Límites de la Verificación

Es indispensable separar la existencia del código de prueba de los resultados de ejecución real:
- Durante la Fase 2C no se ejecutó la suite mediante `dotnet test`.
- Las pruebas de objetos de dominio en memoria **NO demuestran**:
  1. La aplicación efectiva de permisos PostgreSQL 42501 (`REVOKE UPDATE, DELETE ON caja_arqueo`).
  2. El bloqueo pesimista en base de datos (`SELECT ... FOR UPDATE`).
  3. La ausencia de deadlocks en concurrencia real PostgreSQL 16.
  4. La segregación efectiva de pools en PgBouncer (`sigic_pos_pool` vs `sigic_web_pool`).
  5. La latencia real del sistema bajo carga.
- No se afirma cobertura >= 95% sin evidencia válida, medible y vigente.

## Ejecución reproducible actual

Desde la raíz: `dotnet restore SIGIC.sln`,
`dotnet build SIGIC.sln -c Release --no-restore -warnaserror` y
`bash scripts/ci/test-backend.sh` con servicios preparados. Los cuatro proyectos
se ejecutan secuencialmente para evitar carreras de esquema. Cada suite publica
TRX y Cobertura propios. `python3 scripts/ci/coverage.py TestResults` une líneas
únicas por ensamblado/archivo/número y exige ≥95 % en Domain de su suite unitaria;
publica también cobertura conjunta y resultados separados por ensamblado.
No promediar porcentajes ni elegir el último archivo lexicográfico.
