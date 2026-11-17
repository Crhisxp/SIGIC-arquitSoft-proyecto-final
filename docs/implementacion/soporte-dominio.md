---
doc: "docs/implementacion/soporte-dominio.md"
tipo: "implementacion"
estado: "DOCUMENTADO"
aprobado: false
---

# Realización de Soporte: Proyecto de Dominio y Excepciones — SIGIC Ferretería v2.2

Este documento describe la configuración del proyecto base de dominio y las excepciones de negocio de SIGIC Ferretería v2.2.

## 1. Configuración del Proyecto

El archivo [SIGIC.Domain.csproj](../../src/SIGIC.Domain/SIGIC.Domain.csproj) define la biblioteca de clases central del sistema:
- Framework: `net8.0`.
- Habilitación de `Nullable` e `ImplicitUsings`.
- Cero dependencias hacia paquetes externos de infraestructura o presentación, preservando el principio de independencia de Clean Architecture.

## 2. Excepciones de Negocio Inviolables

El núcleo define excepciones especializadas para salvaguardar las invariantes comerciales:
- [MargenInvalidoException.cs](../../src/SIGIC.Domain/Exceptions/MargenInvalidoException.cs): lanzada cuando una operación intenta asignar un precio de venta menor al costo unitario, protegiendo el margen comercial mínimo (`RF-PRE-002`, `RF-INV-008`).
- [StockInsuficienteException.cs](../../src/SIGIC.Domain/Exceptions/StockInsuficienteException.cs): lanzada cuando una solicitud de reserva o venta excede el `StockDisponible` calculado en la entidad (`RNF-CON-003`).

Ambas excepciones encapsulan los valores numéricos y SKUs causantes del rechazo para facilitar el diagnóstico y la trazabilidad del error.
