-- Fixture local de Newman. Requiere el esquema inicializado y una base de ensayo.
BEGIN;
INSERT INTO almacenes (id, codigo, nombre, es_almacen_central, direccion) VALUES
    ('11111111-1111-1111-1111-111111111111', 'ALM-CENTRAL', 'Almacén Central', TRUE, 'Ayacucho'),
    ('22222222-2222-2222-2222-222222222222', 'NEWMAN-TIENDA', 'Tienda Newman', FALSE, 'Ayacucho')
ON CONFLICT (id) DO NOTHING;

INSERT INTO usuarios (id, username, password_hash, roles_json)
SELECT id::uuid, nombre, '$argon2id$v=19$m=65536,t=3,p=4$U0lHSUMtZml4dHVyZS0wMg==$H2G6lyPYwPlnxJ0J0jbAra4chwzIyjxNMz/4jzE+zD4=', roles
FROM (VALUES
    ('a1520301-0000-4000-8000-000000000001', 'newman.cajero', '["Cajero"]'),
    ('a1520301-0000-4000-8000-000000000002', 'newman.cliente', '["ClienteWeb"]'),
    ('a1520301-0000-4000-8000-000000000003', 'newman.tesoreria', '["Tesoreria"]')
) AS usuarios_ensayo(id, nombre, roles)
ON CONFLICT (id) DO NOTHING;

INSERT INTO productos (id, codigo_sku, nombre_comercial, precio_venta, costo_unitario, disponible_en_web) VALUES
    ('a1520302-0000-4000-8000-000000000001', 'NEWMAN-VISIBLE', 'Perno de ensayo', 15, 5, TRUE),
    ('a1520302-0000-4000-8000-000000000002', 'NEWMAN-OCULTO', 'Tuerca de mostrador', 15, 5, FALSE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO stock_almacen (id, almacen_id, producto_id, stock_fisico, stock_comprometido)
VALUES
    (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'a1520302-0000-4000-8000-000000000001', 100, 0),
    (gen_random_uuid(), '22222222-2222-2222-2222-222222222222', 'a1520302-0000-4000-8000-000000000001', 100, 0)
ON CONFLICT (almacen_id, producto_id) DO NOTHING;
COMMIT;
