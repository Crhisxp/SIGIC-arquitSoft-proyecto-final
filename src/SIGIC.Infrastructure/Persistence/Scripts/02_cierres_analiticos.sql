-- RF-ANA-001. Un registro consolidado por día contable de America/Lima.
-- Contabilidad carga los días cerrados (incluidos los días sin actividad).
-- Un día ausente significa cobertura incompleta. Importes netos de IGV en PEN.
BEGIN;
CREATE SCHEMA IF NOT EXISTS reporting;
CREATE TABLE IF NOT EXISTS reporting.cierres_operativos_diarios (
    dia date PRIMARY KEY,
    ingresos_netos numeric(18, 2) NOT NULL,
    costo_ventas numeric(18, 2) NOT NULL,
    gastos_operativos numeric(18, 2) NOT NULL,
    inventario_medio_al_costo numeric(18, 2) NOT NULL CHECK (inventario_medio_al_costo >= 0)
);
GRANT USAGE ON SCHEMA reporting TO sigic_app, sigic_app_user;
GRANT SELECT ON reporting.cierres_operativos_diarios TO sigic_app, sigic_app_user;
COMMIT;
