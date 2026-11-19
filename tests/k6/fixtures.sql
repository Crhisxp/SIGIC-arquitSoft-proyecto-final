-- Ejecutar únicamente en la base descartable del ensayo. Cada carga usa su propio SKU.
BEGIN;
INSERT INTO almacenes (id,codigo,nombre,es_almacen_central,direccion) VALUES
  ('11111111-1111-1111-1111-111111111111','ALM-CENTRAL','Central',true,'Local'),
  ('22222222-2222-2222-2222-222222222222','K6-TIENDA','Tienda k6',false,'Local')
ON CONFLICT (id) DO NOTHING;
INSERT INTO usuarios (id,username,password_hash,roles_json)
SELECT id::uuid, nombre, '$argon2id$v=19$m=65536,t=3,p=4$U0lHSUMtZml4dHVyZS0wMg==$H2G6lyPYwPlnxJ0J0jbAra4chwzIyjxNMz/4jzE+zD4=', roles
FROM (VALUES
  ('33333333-3333-3333-3333-333333333333','k6.cajero','["Cajero"]'),
  ('c1010101-0000-0000-0000-000000000001','k6.cliente','["ClienteWeb"]')
) AS u(id,nombre,roles) ON CONFLICT (id) DO NOTHING;
INSERT INTO productos (id,codigo_sku,nombre_comercial,precio_venta,costo_unitario,disponible_en_web)
VALUES ('d8279f63-3aa9-4746-bf94-638f4383fcd2','K6-POS','Arandela k6',15,5,false),
       ('a1520100-0000-0000-0000-000000000001','K6-CARRERA','Tuerca k6',15,5,false),
       ('e5a40b8e-5b12-4f2b-8a71-6c2e3f890123','K6-WEB','Perno k6',15,5,true)
ON CONFLICT (id) DO NOTHING;
INSERT INTO stock_almacen (id,almacen_id,producto_id,stock_fisico,stock_comprometido)
VALUES (gen_random_uuid(),'22222222-2222-2222-2222-222222222222','d8279f63-3aa9-4746-bf94-638f4383fcd2',500000,10),
       (gen_random_uuid(),'22222222-2222-2222-2222-222222222222','a1520100-0000-0000-0000-000000000001',41,10),
       (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','e5a40b8e-5b12-4f2b-8a71-6c2e3f890123',500000,10)
ON CONFLICT (almacen_id,producto_id) DO NOTHING;
INSERT INTO turnos_caja (id,caja_id,cajero_id,almacen_id,fondo_inicial,estado,fecha_apertura_utc)
VALUES ('44444444-4444-4444-4444-444444444444','55555555-5555-5555-5555-555555555555',
        '33333333-3333-3333-3333-333333333333','22222222-2222-2222-2222-222222222222',100,'Aperturado',now())
ON CONFLICT (id) DO NOTHING;
COMMIT;
