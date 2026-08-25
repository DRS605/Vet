using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>
/// Cuadro vacunal por defecto (cuadro maestro de pautas recomendadas por especie). Es el mismo
/// catálogo que sembraba el script <c>scripts/inicializar-clinica.py</c>, pero vive ahora en el
/// servidor, en un solo sitio, para que el asistente de primer arranque de la SPA pueda cargarlo
/// sin tecnicismos. Son valores de partida sensatos y editables después desde Vacunas → Pautas.
/// </summary>
/// <remarks>
/// Perro: Polivalente (DHPPi/L) · Rabia · Tos de las perreras · Leishmania.
/// Gato: Trivalente felina · Leucemia felina · Rabia. Conejo: Mixomatosis · RHD/VHD. Hurón:
/// Moquillo · Rabia. Ave y Reptil quedan como marco ampliable (no hay calendario universal), así
/// que no traen pautas por defecto: la clínica añade las suyas según su criterio.
/// </remarks>
public static class CuadroVacunalPorDefecto
{
    /// <summary>Una pauta recomendada del cuadro por defecto.</summary>
    public sealed record Entrada(
        EspecieAnimal Especie,
        string Nombre,
        CaracterVacuna Caracter,
        int? EdadInicioSemanas,
        int? PeriodicidadRefuerzoMeses);

    /// <summary>Especies que sí tienen pautas recomendadas por defecto.</summary>
    public static readonly IReadOnlyList<EspecieAnimal> EspeciesConCuadro = new[]
    {
        EspecieAnimal.Perro, EspecieAnimal.Gato, EspecieAnimal.Conejo, EspecieAnimal.Huron,
    };

    /// <summary>Catálogo completo de pautas recomendadas por defecto, por especie.</summary>
    public static readonly IReadOnlyList<Entrada> Pautas = new[]
    {
        new Entrada(EspecieAnimal.Perro, "Polivalente (DHPPi/L)", CaracterVacuna.Recomendada, 6, 12),
        new Entrada(EspecieAnimal.Perro, "Rabia", CaracterVacuna.Legal, 12, 12),
        new Entrada(EspecieAnimal.Perro, "Tos de las perreras", CaracterVacuna.Opcional, 8, 12),
        new Entrada(EspecieAnimal.Perro, "Leishmania", CaracterVacuna.Recomendada, 26, 12),
        new Entrada(EspecieAnimal.Gato, "Trivalente felina", CaracterVacuna.Recomendada, 8, 12),
        new Entrada(EspecieAnimal.Gato, "Leucemia felina", CaracterVacuna.Recomendada, 8, 12),
        new Entrada(EspecieAnimal.Gato, "Rabia", CaracterVacuna.Legal, 12, 12),
        new Entrada(EspecieAnimal.Conejo, "Mixomatosis", CaracterVacuna.Recomendada, 5, 6),
        new Entrada(EspecieAnimal.Conejo, "RHD/VHD (enfermedad hemorrágica)", CaracterVacuna.Recomendada, 10, 12),
        new Entrada(EspecieAnimal.Huron, "Moquillo (Distemper)", CaracterVacuna.Recomendada, 8, 12),
        new Entrada(EspecieAnimal.Huron, "Rabia", CaracterVacuna.Legal, 12, 12),
    };
}

/// <summary>Petición para cargar las pautas recomendadas; si <c>Especies</c> es nulo o vacío, se cargan todas.</summary>
public sealed record CargarPautasRecomendadasComando(IReadOnlyCollection<EspecieAnimal>? Especies = null);

/// <summary>Resultado de la carga: cuántas se crearon y cuántas ya existían (por idempotencia).</summary>
public sealed record CargaPautasResultado(int Creadas, int YaExistentes);

/// <summary>
/// Caso de uso del asistente de primer arranque: carga en la empresa activa el cuadro vacunal
/// recomendado por defecto. Es <b>idempotente</b>: no duplica las pautas que ya existan (por
/// especie + nombre), de modo que se puede ejecutar más de una vez sin efectos. Reutiliza el caso
/// de uso <see cref="CrearPautaVacunal"/> para el alta de cada pauta que falte.
/// </summary>
public sealed class CargarPautasRecomendadas
{
    private readonly CrearPautaVacunal _crear;
    private readonly IConsultaPautasVacunales _consulta;

    public CargarPautasRecomendadas(CrearPautaVacunal crear, IConsultaPautasVacunales consulta)
    {
        _crear = crear;
        _consulta = consulta;
    }

    public async Task<Resultado<CargaPautasResultado>> EjecutarAsync(
        Guid empresaId, CargarPautasRecomendadasComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var especies = comando.Especies is { Count: > 0 }
            ? new HashSet<EspecieAnimal>(comando.Especies)
            : null;

        var creadas = 0;
        var yaExistentes = 0;
        foreach (var entrada in CuadroVacunalPorDefecto.Pautas)
        {
            if (especies is not null && !especies.Contains(entrada.Especie))
            {
                continue;
            }

            // Idempotencia: si ya existe una pauta con ese nombre para la especie (activa o no), se
            // deja como está y no se vuelve a crear.
            if (await _consulta.ExisteNombreAsync(empresaId, entrada.Especie, entrada.Nombre, null, ct).ConfigureAwait(false))
            {
                yaExistentes++;
                continue;
            }

            var datos = new DatosPautaVacunal(
                entrada.Especie, entrada.Nombre, entrada.Caracter, entrada.EdadInicioSemanas, entrada.PeriodicidadRefuerzoMeses);
            var resultado = await _crear.EjecutarAsync(empresaId, datos, ct).ConfigureAwait(false);
            if (resultado.EsFallo)
            {
                return Resultado.Fallo<CargaPautasResultado>(resultado.Error);
            }

            creadas++;
        }

        return Resultado.Ok(new CargaPautasResultado(creadas, yaExistentes));
    }
}
