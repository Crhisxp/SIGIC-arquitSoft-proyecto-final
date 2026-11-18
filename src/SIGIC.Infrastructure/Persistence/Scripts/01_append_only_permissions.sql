-- Script de Inmutabilidad y Gobernanza Append-Only (AC03, RF-CAJ-002, RNF-SEG-004)
-- Aplica sobre: caja_arqueo, caja_arqueo_denominacion, caja_arqueo_detalle, audit_log, kardex_movimientos

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'sigic_app') THEN
        CREATE ROLE sigic_app WITH LOGIN;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'sigic_app_user') THEN
        CREATE ROLE sigic_app_user WITH LOGIN;
    END IF;
END
$$;

-- Otorgar permisos de conexión y uso de esquema
GRANT USAGE ON SCHEMA public TO sigic_app, sigic_app_user;

-- Permisos DML estándar en tablas mutables
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sigic_app, sigic_app_user;

-- Revocación absoluta de UPDATE, DELETE y TRUNCATE para garantizar Append-Only
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE caja_arqueo FROM PUBLIC;
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE caja_arqueo FROM sigic_app, sigic_app_user;

REVOKE UPDATE, DELETE, TRUNCATE ON TABLE caja_arqueo_denominacion FROM PUBLIC;
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE caja_arqueo_denominacion FROM sigic_app, sigic_app_user;

-- Protección explícita contra bypass vía vista actualizable (caja_arqueo_detalle)
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE caja_arqueo_detalle FROM PUBLIC;
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE caja_arqueo_detalle FROM sigic_app, sigic_app_user;

REVOKE UPDATE, DELETE, TRUNCATE ON TABLE audit_log FROM PUBLIC;
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE audit_log FROM sigic_app, sigic_app_user;

REVOKE UPDATE, DELETE, TRUNCATE ON TABLE kardex_movimientos FROM PUBLIC;
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE kardex_movimientos FROM sigic_app, sigic_app_user;

-- Garantizar que los roles de aplicación puedan hacer SELECT e INSERT en estructuras protegidas
GRANT SELECT, INSERT ON TABLE caja_arqueo TO sigic_app, sigic_app_user;
GRANT SELECT, INSERT ON TABLE caja_arqueo_denominacion TO sigic_app, sigic_app_user;
GRANT SELECT, INSERT ON TABLE caja_arqueo_detalle TO sigic_app, sigic_app_user;
GRANT SELECT, INSERT ON TABLE audit_log TO sigic_app, sigic_app_user;
GRANT SELECT, INSERT ON TABLE kardex_movimientos TO sigic_app, sigic_app_user;
