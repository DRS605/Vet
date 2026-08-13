# Módulo Clínica

Primer módulo del **producto veterinario** de ALXOR Core. Introduce el agregado **Animal**
(la mascota), que pertenece a un **cliente** (su propietario) del módulo Terceros. Es la base
sobre la que se construirán las historias clínicas, las visitas y las vacunas.

> El animal no reemplaza al cliente: el **propietario** sigue siendo un `Cliente` de Terceros (a
> quien se factura). Clínica solo guarda el `cliente_id` del propietario y valida que exista en la
> empresa al dar de alta el animal, sin crear una clave foránea entre esquemas de módulos distintos.

## Responsabilidades

- Registrar animales (mascotas) y su ficha básica: especie, raza, sexo, fecha de nacimiento,
  microchip, esterilización, peso y notas.
- Asociar cada animal a su **propietario** (cliente de Terceros) dentro de la misma empresa.
- Calcular datos **derivados**: la **edad en meses** y si el animal es **cachorro**.
- Baja lógica del animal (no se borra: deja de aparecer en los listados).

## Estructura (Clean Architecture ligera)

```
AlxorCore.Clinica/                   # puro, sin frameworks
  Dominio/        Animal, EspecieAnimal, SexoAnimal, AnimalCreado (evento);
                  Consulta, ConsultaRegistrada (evento);
                  PautaVacunal, CaracterVacuna, PautaVacunalCreada (evento);
                  Vacunacion, VacunacionRegistrada (evento)
  Aplicacion/     DatosAnimal, AnimalDto, DatosConsulta, ConsultaDto, DatosPautaVacunal,
                  PautaVacunalDto, DatosVacunacion, VacunacionDto, casos de uso, contratos
                  (IRepositorioAnimales, IConsultaAnimales, IRepositorioConsultas,
                  IConsultaConsultas, IRepositorioPautasVacunales, IConsultaPautasVacunales,
                  IRepositorioVacunaciones, IConsultaVacunaciones, IUnidadDeTrabajoClinica)
AlxorCore.Clinica.Infraestructura/   # adaptadores
  Persistencia.cs ClinicaDbContext, ConfiguracionAnimal/Consulta/PautaVacunal/Vacunacion,
                  RepositorioAnimales/Consultas/PautasVacunales/Vacunaciones, DbContextFactory
  Persistencia/Migraciones/          MigracionInicialClinica, AgregarConsultas, AgregarVacunas
  RegistroServicios.AgregarModuloClinica(...)
```

El dominio y la aplicación no dependen de EF Core ni de ASP.NET: solo de `AlxorCore.Nucleo` y del
**contrato** `IConsultaClientes` del módulo Terceros (nunca de su infraestructura).

## Agregado `Animal`

Raíz de agregado multiempresa (`RaizAgregadoEmpresa<Guid>`). Esquema `clinica`, tabla `animal`.

| Campo | Tipo | Notas |
|---|---|---|
| `ClienteId` | `Guid` | Propietario (cliente de Terceros). Obligatorio. Índice `(empresa_id, cliente_id)`. |
| `Nombre` | `string` | Obligatorio, máx. 100. |
| `Especie` | `EspecieAnimal` | `Perro`, `Gato`, `Conejo`, `Ave`, `Huron`, `Reptil`, `Otro`. Persistido como texto. |
| `Raza` | `string?` | Máx. 100. |
| `Sexo` | `SexoAnimal` | `Macho`, `Hembra`, `Desconocido`. Persistido como texto. |
| `FechaNacimiento` | `DateOnly?` | Opcional. **No puede ser futura** (se valida con `IReloj`). |
| `Microchip` | `string?` | Máx. 20. Se normaliza (sin espacios, mayúsculas), como el IBAN del cliente. |
| `Esterilizado` | `bool` | |
| `PesoKg` | `decimal?` | `numeric(6,3)`. Si se indica, **> 0**. |
| `Notas` | `string?` | Máx. 1000. |
| `Activo` | `bool` | Baja lógica. |
| `CreadoEn` / `ActualizadoEn` | `DateTimeOffset` | |

Métodos derivados (no columnas): `int? EdadMeses(DateOnly hoy)` y `bool EsCachorro(DateOnly hoy)`.
Al crear un animal se emite el evento de dominio `AnimalCreado`.

## Cachorro

Un animal es **cachorro** si tiene fecha de nacimiento y su edad (en meses completos) es **inferior**
al umbral de su especie. Sin fecha de nacimiento, `EsCachorro` es siempre `false`. El umbral se compara
de forma **estricta**: justo en el umbral el animal ya deja de ser cachorro.

| Especie | Umbral (meses) |
|---|---|
| Perro | 12 |
| Gato | 12 |
| Conejo | 6 |
| Hurón | 12 |
| Ave | 12 |
| Reptil | 12 |
| Otro | 12 |

## API

Todas las rutas requieren empresa activa. Lectura → `animal.leer`; alta/edición/baja → `animal.gestionar`.

| Método | Ruta | Permiso | Descripción |
|---|---|---|---|
| `GET` | `/animales` | `animal.leer` | Lista los animales activos de la empresa. |
| `GET` | `/animales/{id}` | `animal.leer` | Obtiene un animal (con `edadMeses` y `esCachorro`). |
| `POST` | `/animales` | `animal.gestionar` | Crea un animal. Devuelve **201**. |
| `PUT` | `/animales/{id}` | `animal.gestionar` | Actualiza un animal. |
| `DELETE` | `/animales/{id}` | `animal.gestionar` | Baja lógica del animal (**204**). |
| `GET` | `/clientes/{clienteId}/animales` | `animal.leer` | Lista los animales de un cliente. |

Los errores se devuelven como `ProblemDetails` (RFC 7807) con un `codigo` estable, mapeados desde el
`Resultado` del dominio: validación → 400, no encontrado → 404.

## Reglas e invariantes

- El **propietario debe existir** en la empresa al crear el animal; si no, se devuelve
  `animal.cliente_no_encontrado` (400). La comprobación usa el contrato `IConsultaClientes` de
  Terceros (mismo patrón de composición entre módulos que emplea Facturación).
- El **nombre** es obligatorio (máx. 100). La **especie** y el **sexo** deben ser valores válidos.
- La **fecha de nacimiento** no puede ser futura.
- El **peso**, si se indica, debe ser mayor que cero.
- El **microchip** se normaliza (sin espacios y en mayúsculas) y se limita a 20 caracteres.
- **Multiempresa**: cada empresa solo ve sus propios animales (filtro global de EF Core por
  `empresa_id` + RLS de PostgreSQL como segunda barrera).

## Persistencia

- Esquema **`clinica`**, tabla **`animal`**. Índice `(empresa_id, cliente_id)`.
- Enumerados `especie` y `sexo` persistidos como **texto** (`HasConversion<string>`).
- `ClinicaDbContext` actúa como **Unidad de Trabajo**: al guardar, confirma y publica los eventos
  de dominio.
- Migración inicial: `MigracionInicialClinica` (activa la RLS por empresa sobre `animal`).

## Agregado `Consulta` (historial clínico)

Segundo agregado del módulo. Una **consulta** es una entrada del historial clínico de un animal;
cuelga del `Animal` (guarda solo su `animal_id`, sin clave foránea entre esquemas). El historial
**no se borra**: una consulta se **anula** con una baja lógica. Raíz de agregado multiempresa
(`RaizAgregadoEmpresa<Guid>`). Esquema `clinica`, tabla `consulta`.

| Campo | Tipo | Notas |
|---|---|---|
| `AnimalId` | `Guid` | Animal atendido. Obligatorio. Índice `(empresa_id, animal_id)`. |
| `Fecha` | `DateOnly` | Obligatoria. **No puede ser futura** (se valida con `IReloj`). |
| `Motivo` | `string?` | Motivo de la visita. Máx. 200. |
| `Diagnostico` | `string?` | Máx. 2000. |
| `Tratamiento` | `string?` | Máx. 2000. |
| `PesoKg` | `decimal?` | `numeric(6,3)`. Si se indica, **> 0**. Peso tomado en la visita. |
| `Veterinario` | `string?` | Profesional que atiende (texto libre; aún no hay entidad Profesional). Máx. 120. |
| `Activo` | `bool` | Baja lógica («anular»). |
| `CreadoEn` / `ActualizadoEn` | `DateTimeOffset` | |

Al registrar una consulta se emite el evento de dominio `ConsultaRegistrada`. Las cadenas se
normalizan (se recortan; vacías → `null`) igual que en `Animal`.

### API de consultas

Todas las rutas requieren empresa activa. Lectura → `consulta.leer`; alta/edición/anulación →
`consulta.gestionar`.

| Método | Ruta | Permiso | Descripción |
|---|---|---|---|
| `GET` | `/animales/{animalId}/consultas` | `consulta.leer` | Historial clínico del animal (más reciente primero). |
| `POST` | `/animales/{animalId}/consultas` | `consulta.gestionar` | Registra una consulta. Devuelve **201**. |
| `GET` | `/consultas/{id}` | `consulta.leer` | Obtiene una consulta. |
| `PUT` | `/consultas/{id}` | `consulta.gestionar` | Actualiza una consulta (el animal no cambia). |
| `DELETE` | `/consultas/{id}` | `consulta.gestionar` | Anula (baja lógica) una consulta (**204**). |

### Reglas e invariantes

- El **animal debe existir en la empresa activa** al registrar la consulta; si no, se devuelve
  `consulta.animal_no_encontrado` (400). La comprobación usa `IConsultaAnimales`, cuyo filtro
  multiempresa garantiza además que el animal pertenece a la empresa activa.
- La **fecha** es obligatoria y no puede ser futura.
- `Motivo` (≤ 200), `Diagnostico` (≤ 2000), `Tratamiento` (≤ 2000) y `Veterinario` (≤ 120) son
  opcionales; el **peso**, si se indica, debe ser mayor que cero.
- El **historial se ordena** por fecha descendente y, a igualdad, por fecha de creación descendente.
- **Multiempresa**: cada empresa solo ve las consultas de sus propios animales (filtro global de EF
  Core por `empresa_id` + RLS de PostgreSQL sobre `consulta`).

La migración `AgregarConsultas` crea la tabla `consulta` y activa su RLS por empresa.

## Agregado `PautaVacunal` (cuadro maestro de vacunación)

Tercer agregado del módulo. Una **pauta vacunal** es el **cuadro maestro** de vacunación de una
especie dentro de la empresa (p. ej. «Polivalente (DHPPi/L)» para perros, «Rabia»): describe qué
vacuna se pone, a qué edad se empieza y cada cuánto se refuerza. Las dosis concretas aplicadas a cada
animal son `Vacunacion`. Raíz de agregado multiempresa (`RaizAgregadoEmpresa<Guid>`). Esquema
`clinica`, tabla `pauta_vacunal`.

| Campo | Tipo | Notas |
|---|---|---|
| `Especie` | `EspecieAnimal` | Especie a la que aplica. Persistida como texto. |
| `Nombre` | `string` | Obligatorio, máx. 120. |
| `Caracter` | `CaracterVacuna` | `Legal`, `Recomendada`, `Opcional`. Persistido como texto. |
| `EdadInicioSemanas` | `int?` | Edad recomendada de inicio, en semanas. Si se indica, ≥ 0. |
| `PeriodicidadRefuerzoMeses` | `int?` | Meses entre refuerzos (12 = anual). Si se indica, > 0. `null` = dosis única / sin refuerzo. |
| `Activo` | `bool` | Baja lógica. |
| `CreadoEn` / `ActualizadoEn` | `DateTimeOffset` | |

Índice **único** `(empresa_id, especie, nombre)` (`ix_pauta_vacunal_empresa_especie_nombre`): dentro
de una empresa no puede repetirse el nombre de una vacuna para la misma especie. Al crear una pauta se
emite el evento de dominio `PautaVacunalCreada`.

El helper estático `PautaVacunal.CalcularProximaDosis(DateOnly fechaAplicacion, int? periodicidadMeses)`
devuelve `fechaAplicacion.AddMonths(periodicidad)` si la periodicidad es > 0, y `null` en otro caso.

## Agregado `Vacunacion` (dosis aplicada)

Cuarto agregado del módulo. Una **vacunación** es una **dosis concreta** aplicada a un animal; cuelga
del `Animal` (guarda solo su `animal_id`, sin clave foránea entre esquemas) y puede apoyarse en una
`PautaVacunal` o ser ad-hoc. El `Nombre` se guarda como **instantánea (snapshot)** para que el
historial sea estable aunque la pauta cambie o se borre. El historial **no se borra**: una vacunación
se **anula** con una baja lógica. Raíz de agregado multiempresa. Esquema `clinica`, tabla `vacunacion`.

| Campo | Tipo | Notas |
|---|---|---|
| `AnimalId` | `Guid` | Animal vacunado. Obligatorio. Índice `(empresa_id, animal_id)`. |
| `PautaVacunalId` | `Guid?` | Pauta maestra usada (`null` = ad-hoc). |
| `Nombre` | `string` | Obligatorio, máx. 120. Instantánea estable del historial. |
| `FechaAplicacion` | `DateOnly` | Obligatoria. **No puede ser futura**. |
| `Lote` | `string?` | Máx. 60. |
| `ProximaDosis` | `DateOnly?` | Puede venir dada o autocalcularse desde la periodicidad de la pauta. Índice `(empresa_id, proxima_dosis)`. |
| `Veterinario` | `string?` | Texto libre. Máx. 120. |
| `Notas` | `string?` | Máx. 1000. |
| `Activo` | `bool` | Baja lógica («anular»). |
| `CreadoEn` / `ActualizadoEn` | `DateTimeOffset` | |

Al registrar una vacunación se emite el evento de dominio `VacunacionRegistrada`. Las cadenas se
normalizan (se recortan; vacías → `null`) igual que en `Animal`.

### API de vacunas

Todas las rutas requieren empresa activa. Lectura → `vacuna.leer`; alta/edición/baja → `vacuna.gestionar`.

| Método | Ruta | Permiso | Descripción |
|---|---|---|---|
| `GET` | `/vacunas/pautas` | `vacuna.leer` | Lista las pautas (filtro opcional `?especie=`). |
| `POST` | `/vacunas/pautas` | `vacuna.gestionar` | Crea una pauta. Devuelve **201**. |
| `GET` | `/vacunas/pautas/{id}` | `vacuna.leer` | Obtiene una pauta. |
| `PUT` | `/vacunas/pautas/{id}` | `vacuna.gestionar` | Actualiza una pauta. |
| `DELETE` | `/vacunas/pautas/{id}` | `vacuna.gestionar` | Desactiva (baja lógica) una pauta (**204**). |
| `GET` | `/animales/{animalId}/vacunas` | `vacuna.leer` | Historial de vacunas del animal (más reciente primero). |
| `POST` | `/animales/{animalId}/vacunas` | `vacuna.gestionar` | Registra una vacunación (la ruta es la fuente del animal). **201**. |
| `GET` | `/vacunas/{id}` | `vacuna.leer` | Obtiene una vacunación. |
| `PUT` | `/vacunas/{id}` | `vacuna.gestionar` | Actualiza una vacunación (el animal no cambia). |
| `DELETE` | `/vacunas/{id}` | `vacuna.gestionar` | Anula (baja lógica) una vacunación (**204**). |
| `GET` | `/vacunas/proximas?dias=30` | `vacuna.leer` | Próximas dosis de la empresa en la ventana de días (por defecto 30). |

### Reglas e invariantes

- La combinación `(empresa, especie, nombre)` de una pauta es **única**: si ya existe se devuelve
  `pauta_vacunal.duplicada` (**409**). Se comprueba en el caso de uso antes de insertar; el índice
  único de la base de datos es la barrera final.
- Al **registrar una vacunación** el **animal debe existir en la empresa activa** (vía
  `IConsultaAnimales`); si no, `vacunacion.animal_no_encontrado` (400). Si se indica una pauta, debe
  existir en la empresa (`vacunacion.pauta_no_encontrada`) y su especie debe **coincidir** con la del
  animal (`vacunacion.pauta_otra_especie`).
- Si no se indica `Nombre`, se **copia** de la pauta. Si no se indica `ProximaDosis` y la pauta tiene
  periodicidad, se **autocalcula** con `CalcularProximaDosis`.
- La **fecha de aplicación** es obligatoria y no puede ser futura. `Lote` (≤ 60), `Veterinario`
  (≤ 120) y `Notas` (≤ 1000) son opcionales.
- El **historial de vacunas** de un animal se ordena por fecha de aplicación descendente. Las
  **próximas dosis** se listan por `proxima_dosis` ascendente (base para recordatorios/KPI).
- **Multiempresa**: cada empresa solo ve sus propias pautas y vacunaciones (filtro global de EF Core
  por `empresa_id` + RLS de PostgreSQL sobre ambas tablas).

La migración `AgregarVacunas` crea las tablas `pauta_vacunal` y `vacunacion` (con sus índices) y
activa la RLS por empresa sobre ambas.

## Agregado `Cirugia` (intervención quirúrgica)

`Cirugia` es la **quinta raíz de agregado** del producto veterinario y **cierra el historial
clínico**: junto a `Consulta` y `Vacunacion`, deja constancia de las operaciones realizadas a un
animal. Cuelga del animal (solo guarda su `AnimalId`, sin FK entre esquemas). El historial no se
borra físicamente: una cirugía se «anula» con una baja lógica (`Activo = false`).

| Campo | Tipo | Notas |
|---|---|---|
| `AnimalId` | `Guid` | Animal intervenido. Obligatorio. Índice `(empresa_id, animal_id)`. |
| `Fecha` | `DateOnly` | Fecha de la intervención. Obligatoria. **No puede ser futura**. |
| `Nombre` | `string` | Procedimiento (p. ej. «Esterilización (OVH)»). Obligatorio, máx. 200. |
| `Descripcion` | `string?` | Detalle o notas de la intervención. Máx. 2000. |
| `Cirujano` | `string?` | Texto libre (aún no hay entidad Profesional). Máx. 120. |
| `Anestesia` | `string?` | Tipo o pauta de anestesia. Máx. 200. |
| `Complicaciones` | `string?` | Complicaciones durante o tras la intervención. Máx. 2000. |
| `ProximaRevision` | `DateOnly?` | P. ej. retirada de puntos. Si se indica, **no puede ser anterior a `Fecha`**. Índice `(empresa_id, proxima_revision)`. |
| `Activo` | `bool` | Baja lógica («anular»). |
| `CreadoEn` / `ActualizadoEn` | `DateTimeOffset` | |

Al registrar una cirugía se emite el evento de dominio `CirugiaRegistrada`. Las cadenas se
normalizan (se recortan; vacías → `null`) igual que en el resto del módulo.

### API de cirugías

Todas las rutas requieren empresa activa. Lectura → `cirugia.leer`; alta/edición/baja →
`cirugia.gestionar`.

| Método | Ruta | Permiso | Descripción |
|---|---|---|---|
| `GET` | `/animales/{animalId}/cirugias` | `cirugia.leer` | Historial de cirugías del animal (más reciente primero). |
| `POST` | `/animales/{animalId}/cirugias` | `cirugia.gestionar` | Registra una cirugía (la ruta es la fuente del animal). **201**. |
| `GET` | `/cirugias/{id}` | `cirugia.leer` | Obtiene una cirugía. |
| `PUT` | `/cirugias/{id}` | `cirugia.gestionar` | Actualiza una cirugía (el animal no cambia). |
| `DELETE` | `/cirugias/{id}` | `cirugia.gestionar` | Anula (baja lógica) una cirugía (**204**). |
| `GET` | `/cirugias/proximas-revisiones?dias=30` | `cirugia.leer` | Próximas revisiones de la empresa en la ventana de días (por defecto 30). |

### Reglas e invariantes

- Al **registrar una cirugía** el **animal debe existir en la empresa activa** (vía
  `IConsultaAnimales`); si no, `cirugia.animal_no_encontrado` (400).
- La **fecha** es obligatoria y no puede ser futura (`cirugia.fecha_futura`). El **nombre** del
  procedimiento es obligatorio (`cirugia.nombre_vacio`, `cirugia.nombre_largo` ≤ 200).
  `Descripcion` (≤ 2000), `Cirujano` (≤ 120), `Anestesia` (≤ 200) y `Complicaciones` (≤ 2000) son
  opcionales.
- Si se indica `ProximaRevision`, **no puede ser anterior a `Fecha`** (`cirugia.revision_anterior_a_fecha`).
- El **historial de cirugías** de un animal se ordena por fecha descendente. Las **próximas
  revisiones** se listan por `proxima_revision` ascendente (base para recordatorios/KPI).
- **Multiempresa**: cada empresa solo ve sus propias cirugías (filtro global de EF Core por
  `empresa_id` + RLS de PostgreSQL sobre la tabla).

La migración `AgregarCirugias` crea la tabla `cirugia` (con sus índices) y activa la RLS por empresa.

## Tests

- **Unitarios** (`AlxorCore.Clinica.PruebasUnitarias`):
  - `Animal`: creación válida y sus rechazos (nombre vacío/largo, cliente vacío, especie/sexo
    inválidos, fecha futura, peso ≤ 0), normalización del microchip, `EdadMeses`, `EsCachorro` en
    los límites del umbral por especie, `Actualizar` y `Desactivar`.
  - `Consulta`: creación válida (emite `ConsultaRegistrada`), animal obligatorio, fecha futura,
    longitudes de motivo/diagnóstico/tratamiento/veterinario, peso ≤ 0, normalización, `Actualizar`
    y `Anular`.
  - `PautaVacunal`: creación válida (emite `PautaVacunalCreada`), nombre vacío/largo, especie/carácter
    inválidos, edad negativa, periodicidad ≤ 0, `Actualizar`, `Desactivar` y `CalcularProximaDosis`
    con y sin periodicidad.
  - `Vacunacion`: creación válida (emite `VacunacionRegistrada`), animal obligatorio, nombre
    vacío/largo, fecha futura, longitudes de lote/veterinario/notas, normalización, `Actualizar`
    y `Anular`.
  - `Cirugia`: creación válida (emite `CirugiaRegistrada`), animal obligatorio, fecha futura, nombre
    vacío/largo, longitudes de descripción/cirujano/anestesia/complicaciones, próxima revisión
    anterior a la fecha, normalización, `Actualizar` y `Anular`.
  - xUnit + FluentAssertions con un `IReloj` fijo.
- **Integración** (`AlxorCore.IntegrationTests`), contra un **PostgreSQL real**:
  - `Animal`: alta/obtención/edición por API, alta con cliente inexistente (400), listado por
    cliente, baja lógica, cálculo de cachorro y **aislamiento multiempresa**.
  - `Consulta`: registro/obtención/edición por API, orden del historial, registro con animal
    inexistente (400), anulación y **aislamiento multiempresa** (una empresa no ve ni registra
    consultas de animales de otra).
  - `Vacunas`: creación de pauta y listado por especie, pauta duplicada (409), registro de vacunación
    ligada a pauta (comprueba que la próxima dosis se **autocalcula** y el nombre se **copia**),
    registro con pauta de otra especie (400), orden del historial de vacunas, anulación,
    `GET /vacunas/proximas` (ventana) y **aislamiento multiempresa**.
  - `Cirugia`: registro/obtención/edición por API, orden del historial, registro con animal
    inexistente (400), `GET /cirugias/proximas-revisiones` (ventana), anulación y **aislamiento
    multiempresa** (una empresa no ve ni registra cirugías de animales de otra).
