-- Catálogo de demostración. Ejecutar con psql como administrador tras inicializar SIGIC.
-- Para una base existente, aplicar antes Persistence/Scripts/03_producto_imagen.sql.
-- Los SKU y saldos existentes se conservan al repetir la carga.
BEGIN;

INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion)
VALUES
    ('11111111-1111-1111-1111-111111111111', 'COM-AYAC-CENTRAL', 'Almacén Central Ayacucho', TRUE, 'Ayacucho, Huamanga'),
    ('00000000-0000-0000-0000-000000000002', 'COM-AYAC-01', 'Mostrador 28 de Julio', FALSE, 'Jr. 28 de Julio, Ayacucho'),
    ('00000000-0000-0000-0000-000000000003', 'COM-AYAC-02', 'Mostrador Mariscal Cáceres', FALSE, 'Av. Mariscal Cáceres, Ayacucho')
ON CONFLICT (id) DO NOTHING;

WITH catalogo (id, sku, nombre, unidad, precio, costo, existencias, imagen) AS (
    VALUES
      ('c011e000-0000-4000-8000-000000000001'::uuid, 'CEM-PORT-I-425', 'Cemento Portland Tipo I, bolsa de 42,5 kg', 'Bolsa', 33.50, 25.20, 180, 'cemento'),
      ('c011e000-0000-4000-8000-000000000002', 'CEM-ANT-425', 'Cemento adicionado antisalitre, bolsa de 42,5 kg', 'Bolsa', 38.90, 29.40, 95, 'cemento'),
      ('c011e000-0000-4000-8000-000000000003', 'FIE-COR-38-9M', 'Varilla de fierro corrugado grado 60, 3/8 pulg × 9 m', 'Varilla', 27.90, 20.00, 230, 'fierro'),
      ('c011e000-0000-4000-8000-000000000004', 'FIE-COR-12-9M', 'Varilla de fierro corrugado grado 60, 1/2 pulg × 9 m', 'Varilla', 47.50, 35.50, 130, 'fierro'),
      ('c011e000-0000-4000-8000-000000000005', 'HER-TAL-18V', 'Taladro percutor inalámbrico 18 V con batería', 'UND', 269.90, 183.50, 18, 'herramientas'),
      ('c011e000-0000-4000-8000-000000000006', 'HER-LLA-AJ-10', 'Llave ajustable de acero 10 pulg', 'UND', 36.90, 21.40, 38, 'ferreteria'),
      ('c011e000-0000-4000-8000-000000000007', 'TUB-PVC-DES-2', 'Tubo PVC sanitario de desagüe, 2 pulg × 3 m', 'UND', 29.90, 19.30, 80, 'tuberias'),
      ('c011e000-0000-4000-8000-000000000008', 'TUB-PVC-COD-2', 'Codo PVC sanitario de 90 grados, 2 pulg', 'UND', 5.60, 3.10, 140, 'tuberias'),
      ('c011e000-0000-4000-8000-000000000009', 'PIN-LAT-BLA-1G', 'Pintura látex mate blanca para interiores, 1 galón', 'Galón', 64.90, 43.50, 45, 'pinturas'),
      ('c011e000-0000-4000-8000-000000000010', 'PIN-SEL-1G', 'Sellador acrílico para muros, 1 galón', 'Galón', 48.00, 32.00, 32, 'pinturas')
),
nuevos AS (
    INSERT INTO productos (id, codigo_sku, nombre_comercial, unidad_base, precio_venta,
                           costo_unitario, stock_fisico, disponible_en_web, imagen_url)
    SELECT id, sku, nombre, unidad, precio, costo, existencias, TRUE, '/productos/' || imagen || '.webp'
    FROM catalogo
    ON CONFLICT DO NOTHING
    RETURNING id, stock_fisico
)
INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
SELECT gen_random_uuid(), '11111111-1111-1111-1111-111111111111', id, stock_fisico, 0
FROM nuevos;

INSERT INTO tarifas_rol (id, rol, porcentaje_descuento)
VALUES (gen_random_uuid(), 'MaestroObra', 0.06),
       (gen_random_uuid(), 'ContratistaB2B', 0.12)
ON CONFLICT (rol) DO NOTHING;

COMMIT;
