using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Clinica.Aplicacion;

// ---------------------------------------------------------------------------------------------
// La Cartilla Viva: el portal del DUEÑO de la mascota. Se accede por un enlace con TOKEN (sin
// contraseña) que la clínica genera. Los endpoints del portal son públicos (sin JWT) y se
// autorizan SOLO por el token: de él se resuelven la empresa y el cliente, y con ellos se FIJA el
// contexto de empresa del servidor (IContextoEmpresaMutable.Fijar) antes de cualquier consulta,
// de modo que el filtro multiempresa de EF Core (y la RLS en producción) sigan aplicando. Toda
// consulta del portal se acota a (empresa_id, cliente_id) DEL TOKEN; nunca a ids del cliente HTTP.
// ---------------------------------------------------------------------------------------------

/// <summary>Genera el token de un acceso de portal con aleatoriedad criptográfica (URL-safe, ≥32 bytes).</summary>
public interface IGeneradorTokenPortal
{
    /// <summary>Devuelve un token nuevo, aleatorio, URL-safe y con suficiente entropía.</summary>
    string Generar();
}

/// <summary>Repositorio de accesos de portal (escritura).</summary>
public interface IRepositorioAccesosPortal
{
    /// <summary>Obtiene el acceso activo de un cliente en la empresa activa, o <c>null</c> si no hay ninguno.</summary>
    Task<AccesoPortal?> ObtenerActivoPorClienteAsync(Guid clienteId, CancellationToken ct = default);

    void Agregar(AccesoPortal acceso);
}

/// <summary>Consultas de lectura de accesos de portal.</summary>
public interface IConsultaAccesosPortal
{
    /// <summary>
    /// Resuelve un acceso <b>activo</b> por su token, <b>ignorando el filtro multiempresa</b> (es la
    /// única consulta que cruza empresas, y solo mediante un token opaco): es el arranque del portal,
    /// que aún no tiene empresa fijada. Devuelve <c>null</c> si el token no existe o está revocado.
    /// </summary>
    Task<AccesoPortal?> ObtenerPorTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Obtiene el acceso activo de un cliente en la empresa activa (lado clínica).</summary>
    Task<AccesoPortal?> ObtenerPorClienteAsync(Guid clienteId, CancellationToken ct = default);
}

/// <summary>Vista del acceso de portal (lado clínica): estado y enlace para compartir.</summary>
public sealed record AccesoPortalDto(
    Guid ClienteId,
    string Token,
    bool Activo,
    DateTimeOffset CreadoEn,
    DateTimeOffset? RevocadoEn,
    string Enlace)
{
    /// <summary>Ruta relativa de la Cartilla Viva para el token indicado.</summary>
    public static string EnlaceDe(string token) => $"/cartilla.html?token={token}";

    public static AccesoPortalDto Desde(AccesoPortal a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new AccesoPortalDto(a.ClienteId, a.Token, a.Activo, a.CreadoEn, a.RevocadoEn, EnlaceDe(a.Token));
    }
}

/// <summary>Estado de un hito del plan de crecimiento del cachorro.</summary>
public enum EstadoHito
{
    /// <summary>Ya cumplido.</summary>
    Hecho,

    /// <summary>El siguiente que toca.</summary>
    Actual,

    /// <summary>Aún pendiente.</summary>
    Pendiente,
}

/// <summary>Hito del plan de crecimiento del cachorro (derivado de la edad y las vacunaciones).</summary>
public sealed record HitoCrecimientoDto(int Orden, string Titulo, string Detalle, EstadoHito Estado);

/// <summary>Una vacuna en la cartilla (última dosis de ese nombre) con su estado.</summary>
public sealed record CartillaVacunaDto(string Nombre, string Estado, DateOnly? ProximaDosis);

/// <summary>Una cita en la cartilla (próxima o de un animal).</summary>
public sealed record CartillaCitaDto(
    Guid CitaId,
    Guid AnimalId,
    string Animal,
    DateTimeOffset Inicio,
    EstadoCita Estado,
    string EstadoTexto,
    TipoCita Tipo,
    string? Motivo);

/// <summary>Un animal en la cartilla: ficha ligera + vacunas + próxima cita + (si cachorro) hitos.</summary>
public sealed record CartillaAnimalDto(
    Guid Id,
    string Nombre,
    string Especie,
    string EspecieTexto,
    int? EdadMeses,
    string? EdadTexto,
    bool EsCachorro,
    IReadOnlyList<CartillaVacunaDto> Vacunas,
    CartillaCitaDto? ProximaCita,
    IReadOnlyList<HitoCrecimientoDto> Hitos);

/// <summary>La Cartilla Viva completa que ve el dueño: clínica, su nombre, sus animales y sus próximas citas.</summary>
public sealed record CartillaDto(
    string NombreClinica,
    string NombreCliente,
    IReadOnlyList<CartillaAnimalDto> Animales,
    IReadOnlyList<CartillaCitaDto> ProximasCitas);

/// <summary>Reglas simples (y honestas: son orientativas) del plan de crecimiento del cachorro.</summary>
public static class PlanCrecimiento
{
    /// <summary>
    /// Deriva los hitos del cachorro de su edad, si está esterilizado y del número de vacunaciones
    /// registradas. No inventa datos clínicos: marca «hecho» lo que ya consta y señala el siguiente
    /// como «te toca». Devuelve la lista vacía si el animal no es cachorro.
    /// </summary>
    public static IReadOnlyList<HitoCrecimientoDto> Derivar(bool esCachorro, int? edadMeses, bool esterilizado, int numeroVacunaciones)
    {
        if (!esCachorro)
        {
            return Array.Empty<HitoCrecimientoDto>();
        }

        var meses = edadMeses ?? 0;
        var cumplidos = new[]
        {
            (Titulo: "Primera vacunación", Detalle: "6-8 semanas", Hecho: numeroVacunaciones >= 1),
            (Titulo: "Desparasitación", Detalle: "8 semanas", Hecho: meses >= 2),
            (Titulo: "Segunda dosis", Detalle: "12 semanas", Hecho: numeroVacunaciones >= 2),
            (Titulo: "Refuerzo final y rabia", Detalle: "16 semanas", Hecho: numeroVacunaciones >= 3),
            (Titulo: "Esterilización (opcional)", Detalle: "~6 meses", Hecho: esterilizado),
            (Titulo: "¡Ya es adulto! Primera revisión", Detalle: "12 meses", Hecho: false),
        };

        var hitos = new List<HitoCrecimientoDto>(cumplidos.Length);
        var actualAsignado = false;
        for (var i = 0; i < cumplidos.Length; i++)
        {
            var h = cumplidos[i];
            EstadoHito estado;
            if (h.Hecho)
            {
                estado = EstadoHito.Hecho;
            }
            else if (!actualAsignado)
            {
                estado = EstadoHito.Actual;
                actualAsignado = true;
            }
            else
            {
                estado = EstadoHito.Pendiente;
            }

            hitos.Add(new HitoCrecimientoDto(i + 1, h.Titulo, h.Detalle, estado));
        }

        return hitos;
    }
}

/// <summary>Textos de presentación en español para los enumerados de la cartilla.</summary>
internal static class TextosCartilla
{
    public static string EstadoCita(EstadoCita estado) => estado switch
    {
        Dominio.EstadoCita.Solicitada => "Por confirmar",
        Dominio.EstadoCita.Confirmada => "Confirmada",
        Dominio.EstadoCita.Atendida => "Atendida",
        Dominio.EstadoCita.Cancelada => "Cancelada",
        Dominio.EstadoCita.NoPresentado => "No presentado",
        _ => estado.ToString(),
    };

    public static string? Edad(int? edadMeses)
    {
        if (edadMeses is not { } meses)
        {
            return null;
        }

        if (meses < 1)
        {
            return "Recién nacido";
        }

        if (meses < 12)
        {
            return meses == 1 ? "1 mes" : $"{meses} meses";
        }

        var anios = meses / 12;
        var resto = meses % 12;
        var textoAnios = anios == 1 ? "1 año" : $"{anios} años";
        if (resto == 0)
        {
            return textoAnios;
        }

        var textoMeses = resto == 1 ? "1 mes" : $"{resto} meses";
        return $"{textoAnios} y {textoMeses}";
    }

    public static string EstadoVacuna(DateOnly? proximaDosis, DateOnly hoy)
    {
        if (proximaDosis is not { } proxima)
        {
            return "Al día";
        }

        if (proxima < hoy)
        {
            return "Pendiente";
        }

        return proxima <= hoy.AddDays(30) ? "Próxima" : "Al día";
    }

    /// <summary>Nombre de pila (primer término del nombre completo).</summary>
    public static string NombreDePila(string? nombreCompleto)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            return string.Empty;
        }

        var partes = nombreCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length > 0 ? partes[0] : nombreCompleto.Trim();
    }
}

/// <summary>
/// Caso de uso (lado clínica, autenticado): genera —o regenera— el acceso de portal de un cliente.
/// Valida que el cliente existe en la empresa (vía <see cref="IConsultaClientes"/>), revoca el acceso
/// activo anterior si lo hubiera (un cliente tiene como mucho uno activo) y crea uno nuevo con un
/// token aleatorio criptográfico.
/// </summary>
public sealed class GenerarAccesoPortal
{
    private readonly IRepositorioAccesosPortal _accesos;
    private readonly IConsultaClientes _clientes;
    private readonly IGeneradorTokenPortal _generador;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GenerarAccesoPortal(
        IRepositorioAccesosPortal accesos,
        IConsultaClientes clientes,
        IGeneradorTokenPortal generador,
        IUnidadDeTrabajoClinica unidadDeTrabajo,
        IReloj reloj)
    {
        _accesos = accesos;
        _clientes = clientes;
        _generador = generador;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<AccesoPortalDto>> EjecutarAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await _clientes.ObtenerAsync(clienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<AccesoPortalDto>(Error.Validacion("acceso_portal.cliente_no_encontrado", "El cliente no existe en esta empresa."));
        }

        var existente = await _accesos.ObtenerActivoPorClienteAsync(clienteId, ct).ConfigureAwait(false);
        existente?.Revocar(_reloj);

        var acceso = AccesoPortal.Crear(empresaId, clienteId, _generador.Generar(), _reloj);
        if (acceso.EsFallo)
        {
            return Resultado.Fallo<AccesoPortalDto>(acceso.Error);
        }

        _accesos.Agregar(acceso.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(AccesoPortalDto.Desde(acceso.Valor));
    }
}

/// <summary>Caso de uso (lado clínica): revoca el acceso de portal activo de un cliente.</summary>
public sealed class RevocarAccesoPortal
{
    private readonly IRepositorioAccesosPortal _accesos;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RevocarAccesoPortal(IRepositorioAccesosPortal accesos, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _accesos = accesos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var acceso = await _accesos.ObtenerActivoPorClienteAsync(clienteId, ct).ConfigureAwait(false);
        if (acceso is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("acceso_portal.no_encontrado", "El cliente no tiene un acceso de portal activo."));
        }

        acceso.Revocar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso (lado clínica): obtiene el estado y el enlace del acceso de portal de un cliente.</summary>
public sealed class ObtenerAccesoPortal
{
    private readonly IConsultaAccesosPortal _consulta;

    public ObtenerAccesoPortal(IConsultaAccesosPortal consulta) => _consulta = consulta;

    public async Task<Resultado<AccesoPortalDto>> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var acceso = await _consulta.ObtenerPorClienteAsync(clienteId, ct).ConfigureAwait(false);
        return acceso is null
            ? Resultado.Fallo<AccesoPortalDto>(Error.NoEncontrado("acceso_portal.no_encontrado", "El cliente no tiene un acceso de portal activo."))
            : Resultado.Ok(AccesoPortalDto.Desde(acceso));
    }
}

/// <summary>
/// Caso de uso (lado portal, por token): compone la Cartilla Viva del cliente. Resuelve el acceso
/// por el token (ignorando el filtro multiempresa, pues aún no hay empresa fijada), <b>fija el
/// contexto de empresa</b> a partir del token y, ya acotado a (empresa, cliente), reúne la clínica,
/// el nombre de pila del dueño, sus animales con vacunas y próxima cita, sus próximas citas y —para
/// los cachorros— los hitos del plan de crecimiento. Token inválido o revocado ⇒ no encontrado (404).
/// </summary>
public sealed class ObtenerCartillaPorToken
{
    private readonly IConsultaAccesosPortal _accesos;
    private readonly IContextoEmpresaMutable _contextoEmpresa;
    private readonly IConsultaEmpresas _empresas;
    private readonly IConsultaClientes _clientes;
    private readonly IConsultaAnimales _animales;
    private readonly IConsultaVacunaciones _vacunaciones;
    private readonly IConsultaCitas _citas;
    private readonly IReloj _reloj;

    public ObtenerCartillaPorToken(
        IConsultaAccesosPortal accesos,
        IContextoEmpresaMutable contextoEmpresa,
        IConsultaEmpresas empresas,
        IConsultaClientes clientes,
        IConsultaAnimales animales,
        IConsultaVacunaciones vacunaciones,
        IConsultaCitas citas,
        IReloj reloj)
    {
        _accesos = accesos;
        _contextoEmpresa = contextoEmpresa;
        _empresas = empresas;
        _clientes = clientes;
        _animales = animales;
        _vacunaciones = vacunaciones;
        _citas = citas;
        _reloj = reloj;
    }

    public async Task<Resultado<CartillaDto>> EjecutarAsync(string? token, CancellationToken ct = default)
    {
        var acceso = await ResolverAccesoAsync(_accesos, _contextoEmpresa, token, ct).ConfigureAwait(false);
        if (acceso is null)
        {
            return Resultado.Fallo<CartillaDto>(NoEncontrado());
        }

        // A partir de aquí el contexto de empresa está fijado desde el token: cada consulta se acota
        // a la empresa (filtro global de EF + RLS) y, además, filtramos por el cliente del token.
        var empresa = await _empresas.ObtenerAsync(acceso.EmpresaId, ct).ConfigureAwait(false);
        var cliente = await _clientes.ObtenerAsync(acceso.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            // El token apunta a un cliente que ya no existe en la empresa: no filtramos información.
            return Resultado.Fallo<CartillaDto>(NoEncontrado());
        }

        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var ahora = _reloj.AhoraUtc;

        var animales = await _animales.ListarPorClienteAsync(acceso.ClienteId, incluirInactivos: false, ct).ConfigureAwait(false);

        var animalesDto = new List<CartillaAnimalDto>(animales.Count);
        var proximasCitas = new List<CartillaCitaDto>();

        foreach (var animal in animales)
        {
            var vacunas = await _vacunaciones.ListarPorAnimalAsync(animal.Id, incluirAnuladas: false, ct).ConfigureAwait(false);
            var citas = await _citas.ListarPorAnimalAsync(animal.Id, incluirCanceladas: false, ct).ConfigureAwait(false);

            // Última dosis por nombre de vacuna (el listado ya viene de más reciente a más antigua).
            var vacunasDto = vacunas
                .GroupBy(v => v.Nombre, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(v => v.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .Select(v => new CartillaVacunaDto(v.Nombre, TextosCartilla.EstadoVacuna(v.ProximaDosis, hoy), v.ProximaDosis))
                .ToList();

            // Próximas citas del animal: aún por celebrar (solicitadas o confirmadas, en el futuro).
            var proximasAnimal = citas
                .Where(c => c.Inicio >= ahora && c.Estado is EstadoCita.Solicitada or EstadoCita.Confirmada)
                .OrderBy(c => c.Inicio)
                .Select(c => new CartillaCitaDto(
                    c.Id, c.AnimalId, animal.Nombre, c.Inicio, c.Estado, TextosCartilla.EstadoCita(c.Estado), c.Tipo, c.Motivo))
                .ToList();

            proximasCitas.AddRange(proximasAnimal);

            var hitos = PlanCrecimiento.Derivar(animal.EsCachorro, animal.EdadMeses, animal.Esterilizado, vacunas.Count);

            animalesDto.Add(new CartillaAnimalDto(
                animal.Id,
                animal.Nombre,
                animal.Especie,
                animal.Especie,
                animal.EdadMeses,
                TextosCartilla.Edad(animal.EdadMeses),
                animal.EsCachorro,
                vacunasDto,
                proximasAnimal.FirstOrDefault(),
                hitos));
        }

        var nombreClinica = empresa?.RazonSocial ?? "Tu clínica veterinaria";
        var nombreCliente = TextosCartilla.NombreDePila(cliente.Nombre);
        var ordenadas = proximasCitas.OrderBy(c => c.Inicio).ToList();

        return Resultado.Ok(new CartillaDto(nombreClinica, nombreCliente, animalesDto, ordenadas));
    }

    internal static async Task<AccesoPortal?> ResolverAccesoAsync(
        IConsultaAccesosPortal accesos,
        IContextoEmpresaMutable contextoEmpresa,
        string? token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var acceso = await accesos.ObtenerPorTokenAsync(token.Trim(), ct).ConfigureAwait(false);
        if (acceso is null || !acceso.Activo)
        {
            return null;
        }

        // Fija la empresa del token ANTES de cualquier otra consulta: todo el portal opera acotado.
        contextoEmpresa.Fijar(acceso.EmpresaId);
        return acceso;
    }

    internal static Error NoEncontrado() =>
        Error.NoEncontrado("portal.no_encontrado", "El enlace no es válido o ha caducado.");
}

/// <summary>
/// Caso de uso (lado portal, por token): confirma una cita de un toque desde la Cartilla Viva.
/// Resuelve el acceso por el token, fija el contexto de empresa y valida que la cita pertenece a un
/// animal de ESE cliente y empresa antes de confirmarla (reutiliza la transición del dominio
/// <see cref="Cita.Confirmar"/>). Token inválido, cita inexistente o de otro cliente/empresa ⇒ 404.
/// </summary>
public sealed class ConfirmarCitaPorToken
{
    private readonly IConsultaAccesosPortal _accesos;
    private readonly IContextoEmpresaMutable _contextoEmpresa;
    private readonly IRepositorioCitas _citas;
    private readonly IConsultaAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ConfirmarCitaPorToken(
        IConsultaAccesosPortal accesos,
        IContextoEmpresaMutable contextoEmpresa,
        IRepositorioCitas citas,
        IConsultaAnimales animales,
        IUnidadDeTrabajoClinica unidadDeTrabajo,
        IReloj reloj)
    {
        _accesos = accesos;
        _contextoEmpresa = contextoEmpresa;
        _citas = citas;
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CartillaCitaDto>> EjecutarAsync(string? token, Guid citaId, CancellationToken ct = default)
    {
        var acceso = await ObtenerCartillaPorToken.ResolverAccesoAsync(_accesos, _contextoEmpresa, token, ct).ConfigureAwait(false);
        if (acceso is null)
        {
            return Resultado.Fallo<CartillaCitaDto>(ObtenerCartillaPorToken.NoEncontrado());
        }

        // La cita se carga ya acotada por empresa (filtro global de EF + RLS).
        var cita = await _citas.ObtenerPorIdAsync(citaId, ct).ConfigureAwait(false);
        if (cita is null)
        {
            return Resultado.Fallo<CartillaCitaDto>(ObtenerCartillaPorToken.NoEncontrado());
        }

        // La cita debe ser de un animal de ESTE cliente (el del token). Si no, 404: no se filtra info.
        var animal = await _animales.ObtenerAsync(cita.AnimalId, ct).ConfigureAwait(false);
        if (animal is null || animal.ClienteId != acceso.ClienteId)
        {
            return Resultado.Fallo<CartillaCitaDto>(ObtenerCartillaPorToken.NoEncontrado());
        }

        if (cita.Estado != EstadoCita.Confirmada)
        {
            var confirmada = cita.Confirmar(_reloj);
            if (confirmada.EsFallo)
            {
                return Resultado.Fallo<CartillaCitaDto>(confirmada.Error);
            }

            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        }

        var dto = new CartillaCitaDto(
            cita.Id, cita.AnimalId, animal.Nombre, cita.Inicio, cita.Estado,
            TextosCartilla.EstadoCita(cita.Estado), cita.Tipo, cita.Motivo);
        return Resultado.Ok(dto);
    }
}
