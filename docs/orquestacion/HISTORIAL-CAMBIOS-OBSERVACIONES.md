# Historial de cambios y observaciones de la entrega

## 2026-11-26 · Evidencia reproducible de QA

- k6 registra HTTP 409 de inventario, fallos técnicos y respuestas inesperadas
  por operación. La carrera de POS produjo un HTTP 201 y cuatro HTTP 409 con
  conciliación correcta. La prueba Web con dos unidades disponibles produjo dos
  reservas y 22 HTTP 409. Ambas pruebas registraron cero fallos técnicos.
- La fixture de capacidad asigna distinto SKU por VU y, para POS, también
  distinto turno. En la primera comprobación corta mixta se midió un p95 POS de
  37,68 ms, equivalente a 137,29 % del p95 de referencia. El umbral relativo
  de 110 % se conserva en la corrida completa. La repetición corta pasó los
  umbrales de contrato y el p95 absoluto. En las reservas exitosas se verificó
  el TTL de 20 minutos.
- El runner archiva `TestResults/` antes de recolectar TRX y Cobertura y puede
  producir el informe HTML con ReportGenerator. El guardián lee el resumen
  actual, exige cobertura de la suite unitaria Domain y devuelve NO-GO si
  existe el directorio de la corrida pero todavía falta su resumen.
- La colección Newman usa una fixture local con su propio cajero y productos.
  La imagen de Docker del portal incorpora los WebP de `public/productos/`
  para servir las imágenes referenciadas por la siembra comercial.

Consulte el [estado de certificación](ESTADO-ORQUESTACION.md) y el
[historial canónico](../bitacora/HISTORIAL-CAMBIOS-OBSERVACIONES.md).
