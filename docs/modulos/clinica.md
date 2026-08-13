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
  Dominio/        Animal, EspecieAnimal, SexoAnimal, AnimalCreado (evento)
  Aplicacion/     DatosAnimal, AnimalDto, casos de uso, contratos (IRepositorioAnimales,
                  IConsultaAnimales, IUnidadDeTrabajoClinica)
AlxorCore.Clinica.Infraestructura/   # adaptadores
  Persistencia.cs ClinicaDbContext, ConfiguracionAnimal, RepositorioAnimales, DbContextFactory
  Persistencia/Migraciones/          MigracionInicialClinica
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

## Tests

- **Unitarios** (`AlxorCore.Clinica.PruebasUnitarias`): creación válida y sus rechazos (nombre
  vacío/largo, cliente vacío, especie/sexo inválidos, fecha futura, peso ≤ 0), normalización del
  microchip, `EdadMeses`, `EsCachorro` en los límites del umbral por especie, `Actualizar` y
  `Desactivar`. xUnit + FluentAssertions con un `IReloj` fijo.
- **Integración** (`AlxorCore.IntegrationTests`): alta/obtención/edición por API, alta con cliente
  inexistente (400), listado por cliente, baja lógica, cálculo de cachorro y **aislamiento
  multiempresa**, contra un **PostgreSQL real**.
