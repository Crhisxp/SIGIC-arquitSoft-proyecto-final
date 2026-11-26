-- Ejecutar después de fixtures.sql en una base descartable. Cada VU tiene sus propias filas de inventario y turno.
BEGIN;
WITH carriles AS (
    SELECT canal, prefijo, lpad(n::text, 12, '0') AS sufijo
    FROM (VALUES ('POS', 'a1520201'), ('WEB', 'a1520202')) AS canales(canal, prefijo)
    CROSS JOIN generate_series(1, 2000) AS serie(n)
), productos_capacidad AS (
    INSERT INTO productos (id, codigo_sku, nombre_comercial, precio_venta, costo_unitario, disponible_en_web)
    SELECT (prefijo || '-0000-4000-8000-' || sufijo)::uuid,
           'K6-CAP-' || canal || '-' || sufijo, 'Producto k6 de capacidad', 15, 5, canal = 'WEB'
    FROM carriles
    ON CONFLICT (id) DO NOTHING
    RETURNING id, disponible_en_web
)
INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
SELECT gen_random_uuid(),
       CASE WHEN disponible_en_web THEN '11111111-1111-1111-1111-111111111111'::uuid
            ELSE '22222222-2222-2222-2222-222222222222'::uuid END,
       id, 10000, 0
FROM productos_capacidad;

INSERT INTO turnos_caja (id, caja_id, cajero_id, almacen_id, fondo_inicial, estado, fecha_apertura_utc)
SELECT ('a1520203-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
       ('a1520204-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
       '33333333-3333-3333-3333-333333333333', '22222222-2222-2222-2222-222222222222', 100, 'Aperturado', now()
FROM generate_series(1, 2000) AS serie(n)
ON CONFLICT (id) DO NOTHING;
COMMIT;
