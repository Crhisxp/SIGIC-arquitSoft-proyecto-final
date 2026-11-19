---
doc: "docs/implementacion/arqueo-caja.md"
tipo: "implementacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Realización de Dominio: Arqueo de Caja — SIGIC Ferretería v2.2

Este documento describe el modelo de dominio para el cierre y arqueo ciego de caja en Mostrador POS.

## 1. Clases de Dominio y Responsabilidades

En el archivo [ArqueoCaja.cs](../../src/SIGIC.Domain/Entities/ArqueoCaja.cs) se definen dos estructuras esenciales:
1. **`ArqueoDenominacion` (Record Inmutable):**
   - Representa el conteo de billetes y monedas físicas (`ValorUnitario`, `Cantidad`, `Subtotal`).
   - Valida que valor unitario y cantidad sean estrictamente positivos.
2. **`ArqueoCaja` (Clase de Dominio):**
   - Agrupa los metadatos del turno (`Id`, `TurnoCajaId`, `CajeroId`, `FechaAperturaUtc`, `FechaCierreUtc`).
   - Contiene el desglose físico inmutable (`DesgloseFisico`), fondo inicial y totales declarados (`TotalEfectivoDeclarado`, `TotalDigitalDeclarado`, `TotalDeclarado`).
   - Métodos funcionales: `ConSaldoTeorico(decimal saldoTeoricoSistema)` genera una nueva instancia vinculada al saldo teórico sin mutar el registro original; `CalcularDiferenciaContable` y `TieneDiscrepancia` evalúan faltantes o sobrantes.
   - Propiedad `EsAppendOnly => true`: descriptor de diseño de la entidad.

## 2. Relación con Pruebas y Exigencias de Infraestructura

Los cálculos inmutables y la precisión de tipo `decimal` están descritos en [01-pruebas-unitarias.md](../entregables/sprint-2/01-pruebas-unitarias.md).

Límites de responsabilidad:
- La propiedad `EsAppendOnly` de la entidad C# no equivale a las restricciones de persistencia física en PostgreSQL 16 (`REVOKE UPDATE, DELETE ON caja_arqueo`).
- La entidad no implementa ni certifica por sí misma la ocultación de saldos teóricos en los endpoints de la API (arqueo ciego en mostrador), lo cual depende del diseño del controlador y la capa de aplicación.
