namespace AlxorCore.Nucleo.Autorizacion;

/// <summary>
/// Catálogo de permisos granulares de ALXOR Core. Son códigos estables (no datos editables):
/// modelarlos como constantes mantiene la autorización simple, versionada y sin una pantalla
/// de administración que el usuario objetivo no necesita.
/// Cada módulo futuro añadirá aquí sus permisos.
/// </summary>
public static class Permisos
{
    // Facturación
    public const string FacturaLeer = "factura.leer";
    public const string FacturaCrear = "factura.crear";
    public const string FacturaEmitir = "factura.emitir";

    // Gastos
    public const string GastoLeer = "gasto.leer";
    public const string GastoGestionar = "gasto.gestionar";

    // Tesorería (cobros y pagos)
    public const string CobroRegistrar = "cobro.registrar";
    public const string PagoRegistrar = "pago.registrar";

    // Terceros y catálogo
    public const string ClienteGestionar = "cliente.gestionar";
    public const string ProductoGestionar = "producto.gestionar";

    // Clínica (producto veterinario)
    public const string AnimalLeer = "animal.leer";
    public const string AnimalGestionar = "animal.gestionar";
    public const string ConsultaLeer = "consulta.leer";
    public const string ConsultaGestionar = "consulta.gestionar";
    public const string VacunaLeer = "vacuna.leer";
    public const string VacunaGestionar = "vacuna.gestionar";
    public const string CirugiaLeer = "cirugia.leer";
    public const string CirugiaGestionar = "cirugia.gestionar";
    public const string RecordatorioLeer = "recordatorio.leer";
    public const string RecordatorioGestionar = "recordatorio.gestionar";
    public const string CitaLeer = "cita.leer";
    public const string CitaGestionar = "cita.gestionar";

    // Informes y datos
    public const string InformeLeer = "informe.leer";
    public const string DatosExportar = "datos.exportar";

    // Administración de la empresa
    public const string EmpresaAjustes = "empresa.ajustes";
    public const string UsuarioGestionar = "usuario.gestionar";

    /// <summary>Todos los permisos definidos, para validación y semillas.</summary>
    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        FacturaLeer, FacturaCrear, FacturaEmitir,
        GastoLeer, GastoGestionar,
        CobroRegistrar, PagoRegistrar,
        ClienteGestionar, ProductoGestionar,
        AnimalLeer, AnimalGestionar,
        ConsultaLeer, ConsultaGestionar,
        VacunaLeer, VacunaGestionar,
        CirugiaLeer, CirugiaGestionar,
        RecordatorioLeer, RecordatorioGestionar,
        CitaLeer, CitaGestionar,
        InformeLeer, DatosExportar,
        EmpresaAjustes, UsuarioGestionar,
    };
}
