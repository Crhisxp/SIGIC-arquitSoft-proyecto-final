---
doc: "docs/implementacion/producto.md"
tipo: "implementacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Realización de Dominio: Entidad Producto — SIGIC Ferretería v2.2

Este documento describe la entidad aggregate root de inventario implementada en la capa de dominio de la solución.

## 1. Estructura y Operaciones de la Entidad

La clase [Producto.cs](../../src/SIGIC.Domain/Entities/Producto.cs) encapsula el estado y las operaciones invariantes del catálogo ferretero:
- **Identificación y Clasificación:** `Id`, `CodigoSku`, `Ean13`, `NombreComercial`, `FamiliaId`, `SubfamiliaId`, `UnidadMedida`.
- **Económicas:** `CostoUnitario`, `PrecioVenta`, validación de margen mediante `ValidarMargenSeguridad`.
- **Gobernanza de Stock:** `StockFisico`, `StockComprometido` y la propiedad calculada:
  $$\text{StockDisponible} = \text{StockFisico} - \text{StockComprometido}$$
- **Operaciones de Dominio:**
  - `ReservarStock(decimal cantidad)`: incrementa el stock comprometido verificando suficiencia de stock disponible.
  - `LiberarReserva(decimal cantidad)`: disminuye el stock comprometido sin afectar el físico.
  - `RegistrarSalidaFisica(decimal cantidad, bool esDesdeReserva)`: aplica el egreso físico real.
  - `RegistrarEntradaFisica(decimal cantidad, decimal costoUnitarioEntrante)`: ingresa stock y recalcula costo promedio ponderado.
  - `ActualizarPrecios(decimal nuevoPrecioVenta, decimal nuevoCostoUnitario)`: protege contra margen negativo.
  - `EsVisibleParaWeb()`: refleja la bandera de segregación `DisponibleEnWeb`.

## 2. Verificación y Alcance Arquitectónico

Los comportamientos lógicos de esta entidad se encuentran verificados unitariamente en memoria conforme a lo documentado en [01-pruebas-unitarias.md](../../05-pruebas-y-calidad/01-pruebas-unitarias.md).

Es fundamental destacar que la entidad `Producto` modela exclusivamente las reglas de negocio en memoria; no ejecuta ni sustituye las transacciones de base de datos (`SELECT ... FOR UPDATE`), el ordenamiento determinista anti-deadlock ni las conexiones a PostgreSQL 16.
