# ALXOR Core

El **ERP más sencillo del mercado** para autónomos y pequeñas empresas españolas. El objetivo no
es tener más funcionalidades que SAP, Odoo o Ekon, sino que **cualquier persona pueda emitir una
factura en menos de cinco minutos sin leer un manual**.

> Documento de diseño técnico y funcional: [`docs/diseno-tecnico-funcional.md`](docs/diseno-tecnico-funcional.md)

## Principios

- Simplicidad visible, complejidad interna controlada. Nada de sobrearquitectura.
- **Monolito modular** con **Clean Architecture ligera** y **DDD práctico**.
- **API First** (OpenAPI/Swagger).
- **Multiempresa** desde el diseño (`empresa_id` obligatorio; preparado para Row-Level Security).
- Seguridad y permisos por diseño. Auditoría de operaciones críticas.
- Tests automáticos desde el primer día. **Un módulo se termina por completo antes de empezar el siguiente.**

## Pila tecnológica

| Área | Elección |
|---|---|
| Lenguaje / runtime | **.NET 8 LTS (C#)** |
| Base de datos | **PostgreSQL** |
| ORM | EF Core (Npgsql) |
| Autenticación | JWT |
| Tests | xUnit + FluentAssertions; integración contra PostgreSQL real |

Los nombres del dominio, las tablas y la API están en **español**.

## Estructura

```
src/
  AlxorCore.Nucleo                     # SharedKernel: Resultado, Error, EntidadBase, eventos, IContextoEmpresa, IReloj
  AlxorCore.Identidad                  # Módulo Identidad: Dominio + Aplicación (puro, sin frameworks)
  AlxorCore.Identidad.Infraestructura  # EF Core, hasher, JWT, repositorios, migraciones
  AlxorCore.Api                        # Host ASP.NET Core: endpoints REST, JWT, OpenAPI
tests/
  AlxorCore.Identidad.Tests            # Tests unitarios (dominio + aplicación)
  AlxorCore.IntegrationTests           # Tests de integración de extremo a extremo (API + PostgreSQL)
docs/
  diseno-tecnico-funcional.md          # Diseño del producto y del MVP
  modulos/identidad.md                 # Documentación del módulo Identidad
```

## Interfaz web

La API sirve también una **interfaz web** (SPA) en la raíz (`/`), en el mismo origen que la API. Con
`docker compose up` la tienes en `http://localhost:8080`: login, panel con KPIs, facturas, clientes,
productos, gastos, cobros e informes. Diseño limpio, pocos colores y pocos clics.

## Arranque rápido (Docker)

Con Docker basta un comando para levantar la API + PostgreSQL:

```bash
docker compose up --build
```

- API: `http://localhost:8080` · Swagger: `http://localhost:8080/swagger` · Salud: `/salud`
- En *Development* la API aplica las migraciones automáticamente.

> El archivo `docker-compose.override.yml` (incluido) publica la API en
> **`http://localhost:3400`** además del 8080; Docker Compose lo aplica solo.

### Datos de demostración

Con la API arrancada, rellena una empresa con clientes, artículos, facturas repartidas por el año,
cobros (alguno parcial), gastos y una factura recurrente, para ver el panel y los informes con
contenido desde el primer momento:

**Windows (PowerShell)** — no necesitas instalar nada más:

```powershell
.\scripts\datos-demo.ps1                             # contra http://localhost:3400
.\scripts\datos-demo.ps1 -BaseUrl http://localhost:8080
```

**macOS / Linux (Python)**:

```bash
python3 scripts/datos-demo.py                       # contra http://localhost:3400
python3 scripts/datos-demo.py http://localhost:8080 # otra URL base
```

Ambos usan solo lo que ya trae el sistema (Invoke-RestMethod en Windows; la biblioteca estándar de
Python en macOS/Linux). Crean la cuenta `demo@alxorcore.es` (contraseña `Demo1234!`) y **no vuelven a
sembrar** si la empresa ya tiene facturas. Pensados para bases de datos de desarrollo/demo, no para
producción.

## Puesta en marcha (desarrollo con SDK)

Requisitos: **.NET 8 SDK** y **PostgreSQL** (local o vía Docker).

1. Levanta PostgreSQL (opción Docker):

   ```bash
   docker compose up -d
   ```

   O usa un PostgreSQL propio y ajusta la cadena `ConnectionStrings:AlxorCore` en
   `src/AlxorCore.Api/appsettings.json`.

2. Compila y ejecuta los tests:

   ```bash
   dotnet build
   dotnet test
   ```

   Los tests de integración usan por defecto la base `alxor_test` en `localhost:5432`
   (usuario/contraseña `postgres`). Se puede sobrescribir con la variable de entorno
   `ALXOR_TEST_CONEXION`.

3. Arranca la API:

   ```bash
   dotnet run --project src/AlxorCore.Api
   ```

   En entorno *Development* la API aplica las migraciones automáticamente y publica Swagger en
   `/swagger`. Prueba de vida: `GET /salud`.

### Migraciones de base de datos

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> \
  --project src/AlxorCore.Identidad.Infraestructura \
  --startup-project src/AlxorCore.Identidad.Infraestructura \
  --output-dir Persistencia/Migraciones
```

## Estado del proyecto

| Módulo | Estado |
|---|---|
| **Identidad** (registro, login, JWT, perfil, roles/permisos) | ✅ Terminado |
| **Organización** (empresas, membresías, series, multiempresa/RLS) | ✅ Terminado |
| **Terceros** (Clientes) | ✅ Terminado |
| **Catálogo** (Productos e Impuestos) | ✅ Terminado |
| **Facturación** (facturas emitidas) | ✅ Terminado |
| **Gastos** | ✅ Terminado |
| **Tesorería** (cobros y pagos) | ✅ Terminado |
| **Documentos** (PDF y email) | ✅ Terminado |
| **Informes** (dashboard, libros de IVA, gestoría, beneficio) | ✅ Terminado |
| **Auditoría** (registro de quién hizo qué y cuándo) | ✅ Terminado |
| **Cuenta / RGPD** (exportación y borrado de datos, páginas legales) | ✅ Terminado |
| **Clínica** (producto veterinario: animales/mascotas, historial de consultas y vacunas) | ✅ Terminado |

**MVP completo**: los módulos están terminados (dominio · API · persistencia · tests · docs). El
desarrollo ha avanzado **módulo a módulo**, cada uno entregado por completo antes del siguiente.

## Flujo de extremo a extremo (API)

1. `POST /auth/registro` → `POST /auth/login` (JWT).
2. `POST /empresas` → `POST /empresas/{id}/seleccionar` (token con empresa activa, rol y permisos).
3. `POST /clientes`, `POST /productos`.
4. `POST /facturas` (numeración correlativa, IVA + IRPF) → `GET /facturas/{id}/pdf` →
   `POST /facturas/{id}/enviar`.
5. `POST /cobros` / `POST /gastos` + `POST /pagos`.
6. `GET /informes/dashboard`, `GET /informes/libro-iva`, `GET /informes/libro-iva/csv`.

Documentación por módulo en [`docs/modulos/`](docs/modulos/).
