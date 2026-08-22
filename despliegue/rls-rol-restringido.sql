-- =============================================================================================
-- ALXOR Vet — Rol de aplicación restringido para endurecer la Row-Level Security (RLS)
-- =============================================================================================
--
-- ¿Por qué?
--   El aislamiento multiempresa de ALXOR se refuerza con RLS de PostgreSQL. Las políticas usan
--   FORCE ROW LEVEL SECURITY, pero PostgreSQL SIEMPRE deja pasar a los roles SUPERUSER y a los que
--   tienen BYPASSRLS (y el owner solo queda sujeto gracias a FORCE). Por defecto la app se conecta
--   con el rol «postgres» (superusuario), cómodo pero que ignora la RLS: la única barrera activa es
--   entonces el filtro global de EF Core. Para que la RLS actúe como segunda barrera real, la app
--   debe conectarse con un rol SIN superusuario y SIN BYPASSRLS. Este script crea ese rol.
--
-- Es OPCIONAL (recomendado para endurecer). NO se ejecuta en el arranque: se aplica a mano una vez.
--
-- Uso (ajusta la contraseña y, si procede, el nombre de la base de datos):
--   1) Aplica antes las migraciones con el rol admin/owner (p. ej. arrancando la API una vez, o
--      con `dotnet ef database update`), para que existan todas las tablas y esquemas.
--   2) Ejecuta este script como superusuario/owner sobre la base de datos de la clínica:
--        psql -U postgres -d alxor -v clave='UNA_CLAVE_FUERTE' -f despliegue/rls-rol-restringido.sql
--      (o edita la línea CREATE ROLE de abajo y ejecútalo sin la variable -v).
--   3) Apunta la conexión de la app a este rol (ver docs/veterinaria/INSTALACION.md, sección 9).
--
-- Nota: cámbiate a la base de datos correcta antes de ejecutarlo (\c alxor) si tu cliente no lo hace.
-- =============================================================================================

-- --- 1) Crear el rol de aplicación (login, SIN superusuario, SIN BYPASSRLS) --------------------
-- Si defines la variable psql «clave» (-v clave='...') se usa; si no, sustituye el literal.
DO $$
DECLARE
    v_clave text := current_setting('psql.clave', true);
BEGIN
    IF v_clave IS NULL OR v_clave = '' THEN
        v_clave := 'CAMBIA_esta_clave_del_rol_de_app';  -- <-- EDITA esto si no usas -v clave=...
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'alxor_app') THEN
        EXECUTE format(
            'CREATE ROLE alxor_app LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS INHERIT',
            v_clave);
    ELSE
        -- Ya existe: solo garantizamos que NO es superusuario ni salta la RLS.
        ALTER ROLE alxor_app NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;
END
$$;

-- Nota sobre la contraseña con -v: psql expone las variables como «psql.<nombre>» a current_setting
-- solo en algunas versiones; si tu versión no lo hace, edita el literal de arriba directamente.

-- --- 2) Permitir conectar a la base de datos ---------------------------------------------------
-- (\gexec permite usar el nombre de la BD actual sin escribirlo a mano)
SELECT format('GRANT CONNECT ON DATABASE %I TO alxor_app', current_database())
\gexec

-- --- 3) Permisos mínimos por esquema de negocio ------------------------------------------------
-- USAGE en el esquema + DML (SELECT/INSERT/UPDATE/DELETE) en sus tablas + uso de secuencias
-- (columnas identity). NO se conceden permisos de DDL: las migraciones las aplica el owner/admin.
DO $$
DECLARE
    v_esquema text;
    v_esquemas text[] := ARRAY[
        'identidad', 'organizacion', 'terceros', 'clinica',
        'catalogo', 'facturacion', 'gastos', 'tesoreria', 'auditoria'
    ];
BEGIN
    FOREACH v_esquema IN ARRAY v_esquemas LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO alxor_app', v_esquema);
        EXECUTE format(
            'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO alxor_app', v_esquema);
        EXECUTE format(
            'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO alxor_app', v_esquema);

        -- Privilegios por defecto para las tablas/secuencias que creen FUTURAS migraciones,
        -- siempre que las cree el mismo rol que ejecuta este script (el owner/admin).
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO alxor_app',
            v_esquema);
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO alxor_app',
            v_esquema);
    END LOOP;
END
$$;

-- =============================================================================================
-- Comprobación rápida (opcional): el rol NO debe ser superusuario ni tener BYPASSRLS.
--   SELECT rolname, rolsuper, rolbypassrls FROM pg_roles WHERE rolname = 'alxor_app';
-- Debe devolver rolsuper=f y rolbypassrls=f. Con eso, la RLS por empresa actúa para la app.
-- =============================================================================================
