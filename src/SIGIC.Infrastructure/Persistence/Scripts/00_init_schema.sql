-- Esquema DDL para SIGIC Ferretería v2.2 (PostgreSQL 16)
CREATE TABLE IF NOT EXISTS productos (
    id UUID PRIMARY KEY,
    codigo_sku VARCHAR(50) NOT NULL UNIQUE,
    ean13 VARCHAR(50) NOT NULL DEFAULT '',
    nombre_comercial VARCHAR(200) NOT NULL,
    unidad_base VARCHAR(20) NOT NULL DEFAULT 'UND',
    precio_venta NUMERIC(18, 4) NOT NULL,
    costo_unitario NUMERIC(18, 4) NOT NULL DEFAULT 0,
    disponible_en_web BOOLEAN NOT NULL DEFAULT FALSE,
    stock_fisico NUMERIC(18, 4) NOT NULL DEFAULT 0,
    stock_comprometido NUMERIC(18, 4) NOT NULL DEFAULT 0,
    familia_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    subfamilia_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'
);

CREATE TABLE IF NOT EXISTS almacenes (
    id UUID PRIMARY KEY,
    codigo VARCHAR(50) NOT NULL UNIQUE,
    nombre VARCHAR(150) NOT NULL,
    es_almacen_central BOOLEAN NOT NULL DEFAULT FALSE,
    direccion VARCHAR(250) NOT NULL
);

CREATE TABLE IF NOT EXISTS stock_almacen (
    id UUID PRIMARY KEY,
    almacen_id UUID NOT NULL REFERENCES almacenes(id),
    producto_id UUID NOT NULL REFERENCES productos(id),
    stock_fisico NUMERIC(18, 4) NOT NULL DEFAULT 0,
    stock_comprometido NUMERIC(18, 4) NOT NULL DEFAULT 0,
    CONSTRAINT uq_stock_almacen_producto UNIQUE (almacen_id, producto_id),
    CONSTRAINT ck_stock_no_negativo CHECK (stock_fisico >= 0 AND stock_comprometido >= 0)
);

CREATE TABLE IF NOT EXISTS turnos_caja (
    id UUID PRIMARY KEY,
    caja_id UUID NOT NULL,
    cajero_id UUID NOT NULL,
    almacen_id UUID NOT NULL REFERENCES almacenes(id),
    fondo_inicial NUMERIC(18, 2) NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'Aperturado',
    fecha_apertura_utc TIMESTAMPTZ NOT NULL,
    fecha_cierre_utc TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS caja_arqueo (
    id UUID PRIMARY KEY,
    turno_caja_id UUID NOT NULL REFERENCES turnos_caja(id),
    cajero_id UUID NOT NULL,
    fecha_apertura_utc TIMESTAMPTZ NOT NULL,
    fecha_cierre_utc TIMESTAMPTZ NOT NULL,
    fondo_inicial_sencillo NUMERIC(18, 2) NOT NULL DEFAULT 0,
    total_digital_declarado NUMERIC(18, 2) NOT NULL DEFAULT 0,
    total_efectivo_declarado NUMERIC(18, 2) NOT NULL DEFAULT 0,
    saldo_sistema_teorico NUMERIC(18, 2),
    justificacion VARCHAR(500)
);

CREATE TABLE IF NOT EXISTS caja_arqueo_denominacion (
    id UUID PRIMARY KEY,
    arqueo_caja_id UUID NOT NULL REFERENCES caja_arqueo(id),
    denominacion NUMERIC(18, 2) NOT NULL,
    cantidad INT NOT NULL,
    subtotal NUMERIC(18, 2) NOT NULL
);

-- Reconciliación documental: vista caja_arqueo_detalle sobre caja_arqueo_denominacion
CREATE OR REPLACE VIEW caja_arqueo_detalle AS
SELECT id, arqueo_caja_id, denominacion, cantidad, subtotal
FROM caja_arqueo_denominacion;

CREATE TABLE IF NOT EXISTS ventas_pos (
    id UUID PRIMARY KEY,
    turno_caja_id UUID NOT NULL REFERENCES turnos_caja(id),
    cajero_id UUID NOT NULL,
    almacen_id UUID NOT NULL REFERENCES almacenes(id),
    total NUMERIC(18, 2) NOT NULL,
    fecha_utc TIMESTAMPTZ NOT NULL,
    metodo_pago VARCHAR(50) NOT NULL,
    codigo_operacion_digital VARCHAR(100),
    cliente_id UUID
);

CREATE TABLE IF NOT EXISTS ventas_pos_items (
    id UUID PRIMARY KEY,
    venta_pos_id UUID NOT NULL REFERENCES ventas_pos(id),
    producto_id UUID NOT NULL REFERENCES productos(id),
    cantidad NUMERIC(18, 4) NOT NULL,
    precio_unitario NUMERIC(18, 4) NOT NULL,
    subtotal NUMERIC(18, 2) NOT NULL
);

CREATE TABLE IF NOT EXISTS ventas_pos_pagos (
    id UUID PRIMARY KEY,
    venta_pos_id UUID NOT NULL REFERENCES ventas_pos(id) ON DELETE CASCADE,
    metodo_pago VARCHAR(50) NOT NULL,
    monto NUMERIC(18, 4) NOT NULL,
    codigo_operacion_digital VARCHAR(100)
);

CREATE TABLE IF NOT EXISTS pagos_digitales (
    id UUID PRIMARY KEY,
    metodo_pago VARCHAR(50) NOT NULL,
    codigo_operacion VARCHAR(100) NOT NULL,
    fecha DATE NOT NULL,
    venta_id UUID NOT NULL,
    fecha_registro_utc TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_pago_digital UNIQUE (metodo_pago, codigo_operacion, fecha)
);

CREATE TABLE IF NOT EXISTS pedidos_web (
    id UUID PRIMARY KEY,
    codigo VARCHAR(50) NOT NULL UNIQUE,
    cliente_id UUID NOT NULL,
    almacen_origen_id UUID NOT NULL REFERENCES almacenes(id),
    tienda_destino_id UUID NOT NULL,
    modalidad_entrega VARCHAR(50) NOT NULL,
    modalidad_pago VARCHAR(50) NOT NULL,
    estado VARCHAR(30) NOT NULL,
    total NUMERIC(18, 2) NOT NULL,
    fecha_creacion_utc TIMESTAMPTZ NOT NULL,
    fecha_expiracion_utc TIMESTAMPTZ NOT NULL,
    fecha_limite_voucher_utc TIMESTAMPTZ,
    fecha_subida_voucher_utc TIMESTAMPTZ,
    guia_remision_numero VARCHAR(60)
);

CREATE TABLE IF NOT EXISTS pedidos_web_items (
    id UUID PRIMARY KEY,
    pedido_web_id UUID NOT NULL REFERENCES pedidos_web(id),
    producto_id UUID NOT NULL REFERENCES productos(id),
    cantidad_comercial NUMERIC(18, 4) NOT NULL,
    unidad_comercial VARCHAR(20) NOT NULL,
    factor_conversion NUMERIC(18, 6) NOT NULL,
    cantidad_base_reservada NUMERIC(18, 4) NOT NULL,
    precio_unitario NUMERIC(18, 4) NOT NULL,
    subtotal NUMERIC(18, 2) NOT NULL
);

CREATE TABLE IF NOT EXISTS guias_traslado_interno (
    id UUID PRIMARY KEY,
    numero_guia VARCHAR(60) NOT NULL UNIQUE,
    pedido_web_id UUID NOT NULL REFERENCES pedidos_web(id),
    almacen_origen_id UUID NOT NULL REFERENCES almacenes(id),
    tienda_destino_id UUID NOT NULL,
    estado VARCHAR(50) NOT NULL DEFAULT 'EMITIDA',
    fecha_emision_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS guias_traslado_items (
    id UUID PRIMARY KEY,
    guia_traslado_id UUID NOT NULL REFERENCES guias_traslado_interno(id) ON DELETE CASCADE,
    producto_id UUID NOT NULL REFERENCES productos(id),
    cantidad NUMERIC(18, 4) NOT NULL
);

CREATE TABLE IF NOT EXISTS comprobantes_voucher (
    id UUID PRIMARY KEY,
    pedido_web_id UUID NOT NULL UNIQUE REFERENCES pedidos_web(id),
    nombre_archivo VARCHAR(255) NOT NULL,
    formato VARCHAR(50) NOT NULL,
    hash_sha256 VARCHAR(100) NOT NULL,
    tamano_bytes BIGINT NOT NULL,
    fecha_subida_utc TIMESTAMPTZ NOT NULL,
    validado BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_validacion_utc TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS kardex_movimientos (
    id UUID PRIMARY KEY,
    producto_id UUID NOT NULL REFERENCES productos(id),
    almacen_id UUID NOT NULL REFERENCES almacenes(id),
    tipo_operacion VARCHAR(50) NOT NULL,
    cantidad_base NUMERIC(18, 4) NOT NULL,
    costo_unitario NUMERIC(18, 4) NOT NULL,
    documento_referencia VARCHAR(100) NOT NULL,
    codigo_autorizacion VARCHAR(100),
    motivo VARCHAR(250),
    fecha_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS audit_log (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    accion VARCHAR(100) NOT NULL,
    entidad VARCHAR(100) NOT NULL,
    entidad_id VARCHAR(100) NOT NULL,
    detalles TEXT NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    terminal_scope VARCHAR(50) NOT NULL,
    timestamp_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS alertas_caja_outbox (
    id UUID PRIMARY KEY,
    turno_caja_id UUID NOT NULL,
    cajero_id UUID NOT NULL,
    diferencia NUMERIC(18, 2) NOT NULL,
    justificacion VARCHAR(500),
    fecha_utc TIMESTAMPTZ NOT NULL,
    procesado BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS tarifas_rol (
    id UUID PRIMARY KEY,
    rol VARCHAR(50) NOT NULL UNIQUE,
    porcentaje_descuento NUMERIC(5, 2) NOT NULL
);

CREATE TABLE IF NOT EXISTS descuentos_volumen (
    id UUID PRIMARY KEY,
    cantidad_minima NUMERIC(18, 4) NOT NULL,
    porcentaje_descuento NUMERIC(5, 2) NOT NULL
);

CREATE TABLE IF NOT EXISTS conversiones_unidad (
    id UUID PRIMARY KEY,
    unidad_origen VARCHAR(20) NOT NULL,
    unidad_destino VARCHAR(20) NOT NULL,
    factor_conversion NUMERIC(18, 6) NOT NULL,
    CONSTRAINT uq_conversion UNIQUE (unidad_origen, unidad_destino)
);

CREATE TABLE IF NOT EXISTS usuarios (
    id UUID PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    intentos_fallidos INT NOT NULL DEFAULT 0,
    bloqueado_hasta_utc TIMESTAMPTZ,
    esta_activo BOOLEAN NOT NULL DEFAULT TRUE,
    roles_json VARCHAR(1000) NOT NULL DEFAULT '[]',
    perfil_cliente VARCHAR(50) NOT NULL DEFAULT 'PublicoGeneral'
);
