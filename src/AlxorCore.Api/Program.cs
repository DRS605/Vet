using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using AlxorCore.Api.Comun;
using AlxorCore.Api.Endpoints;
using AlxorCore.Identidad.Infraestructura;
using AlxorCore.Identidad.Infraestructura.Persistencia;
using AlxorCore.Identidad.Infraestructura.Seguridad;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Organizacion.Infraestructura;
using AlxorCore.Terceros.Infraestructura;
using AlxorCore.Clinica.Infraestructura;
using AlxorCore.Catalogo.Infraestructura;
using AlxorCore.Facturacion.Infraestructura;
using AlxorCore.Gastos.Infraestructura;
using AlxorCore.Tesoreria.Infraestructura;
using AlxorCore.Documentos.Infraestructura;
using AlxorCore.Informes.Infraestructura;
using AlxorCore.Auditoria.Infraestructura;
using AlxorCore.Organizacion.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Despliegue en la nube (PaaS) -------------------------------------------
// 1) Puerto dinámico: muchos proveedores (Render, Railway, Heroku...) inyectan
//    el puerto por la variable de entorno PORT. Si está presente, escuchamos
//    ahí; si no, se mantiene ASPNETCORE_URLS (contenedor local o Fly.io con
//    puerto fijo, y el paquete Windows self-contained).
var puertoNube = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(puertoNube))
{
    builder.WebHost.UseUrls($"http://+:{puertoNube}");
}

// 2) Cadena de conexión: los PaaS suelen ofrecer la base de datos como una URL
//    estándar (DATABASE_URL = postgres://usuario:clave@host:puerto/basedatos).
//    Si no se ha fijado ConnectionStrings:AlxorCore explícitamente, traducimos
//    esa URL al formato clave-valor de Npgsql para que todos los módulos la
//    consuman igual que en local.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("AlxorCore")))
{
    var urlBd = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(urlBd))
    {
        builder.Configuration["ConnectionStrings:AlxorCore"] = CadenaNpgsqlDesdeUrl(urlBd);
    }
}

// Traduce una URL estilo postgres://usuario:clave@host:puerto/basedatos?sslmode=...
// a una cadena de conexión Npgsql (clave=valor). Escapa los valores según la
// regla ADO.NET (comillas dobles si hay ; = ' " o espacios en los extremos) para
// que una contraseña con caracteres especiales no rompa la cadena.
static string CadenaNpgsqlDesdeUrl(string url)
{
    var uri = new Uri(url);
    var credenciales = uri.UserInfo.Split(':', 2);
    var usuario = Uri.UnescapeDataString(credenciales[0]);
    var clave = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : string.Empty;
    var baseDatos = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
    var puerto = uri.Port > 0 ? uri.Port : 5432;

    // Modo SSL: si la URL lo indica, se respeta; por defecto Prefer (intenta TLS
    // y si el servidor no lo ofrece, texto plano), que funciona tanto con la BD
    // interna de un PaaS como con una externa gestionada.
    var modoSsl = "Prefer";
    foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = par.Split('=', 2);
        if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
        {
            modoSsl = kv[1].ToLowerInvariant() switch
            {
                "disable" => "Disable",
                "allow" => "Allow",
                "prefer" => "Prefer",
                "require" => "Require",
                "verify-ca" => "VerifyCA",
                "verify-full" => "VerifyFull",
                _ => "Prefer",
            };
        }
    }

    static string Escapar(string valor)
    {
        if (string.IsNullOrEmpty(valor)) { return valor ?? string.Empty; }
        var necesitaComillas = valor.Contains(';', StringComparison.Ordinal)
            || valor.Contains('=', StringComparison.Ordinal)
            || valor.Contains('\'', StringComparison.Ordinal)
            || valor.Contains('"', StringComparison.Ordinal)
            || valor != valor.Trim();
        if (necesitaComillas)
        {
            return "\"" + valor.Replace("\"", "\"\"") + "\"";
        }
        return valor;
    }

    return $"Host={Escapar(uri.Host)};Port={puerto};Database={Escapar(baseDatos)};"
         + $"Username={Escapar(usuario)};Password={Escapar(clave)};"
         + $"SSL Mode={modoSsl};Trust Server Certificate=true";
}

// --- Contexto de empresa (multiempresa) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ContextoEmpresaHttp>();
builder.Services.AddScoped<IContextoEmpresa>(sp => sp.GetRequiredService<ContextoEmpresaHttp>());
builder.Services.AddScoped<IContextoEmpresaMutable>(sp => sp.GetRequiredService<ContextoEmpresaHttp>());

// --- Módulos de ALXOR Core ---
builder.Services.AgregarModuloIdentidad(builder.Configuration);
builder.Services.AgregarModuloOrganizacion(builder.Configuration);
builder.Services.AgregarModuloTerceros(builder.Configuration);
builder.Services.AgregarModuloClinica(builder.Configuration);
builder.Services.AgregarModuloCatalogo(builder.Configuration);
builder.Services.AgregarModuloFacturacion(builder.Configuration);
builder.Services.AgregarModuloGastos(builder.Configuration);
builder.Services.AgregarModuloTesoreria(builder.Configuration);
builder.Services.AgregarModuloDocumentos(builder.Configuration);
builder.Services.AgregarModuloInformes();
builder.Services.AgregarModuloAuditoria(builder.Configuration);

// --- Facturación automática periódica (proceso en segundo plano) ---
builder.Services.Configure<AlxorCore.Api.Servicios.OpcionesFacturacionRecurrente>(
    builder.Configuration.GetSection(AlxorCore.Api.Servicios.OpcionesFacturacionRecurrente.Seccion));
builder.Services.AddHostedService<AlxorCore.Api.Servicios.ServicioFacturacionRecurrente>();

// --- Recordatorios clínicos automáticos (proceso en segundo plano, DESACTIVADO por defecto) ---
builder.Services.Configure<AlxorCore.Api.Servicios.OpcionesRecordatoriosAutomaticos>(
    builder.Configuration.GetSection(AlxorCore.Api.Servicios.OpcionesRecordatoriosAutomaticos.Seccion));
builder.Services.AddHostedService<AlxorCore.Api.Servicios.ServicioRecordatoriosAutomaticos>();

// Los enumerados se serializan por nombre en la API.
builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// --- Autenticación JWT ---
// Conservamos los nombres originales de los claims (sub, email) sin remapearlos.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// La validación se configura desde las MISMAS opciones (IOptions<OpcionesJwt>) que usa la
// emisión de tokens, garantizando una única fuente de verdad para la clave, el emisor y la
// audiencia (evita desajustes de clave entre firma y validación).
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<OpcionesJwt>>((jwt, opcionesJwt) =>
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = ConfiguracionJwt.ConstruirParametrosValidacion(opcionesJwt.Value);
    })
    // Fuerza la construcción de estas opciones al arrancar; al resolver IOptions<OpcionesJwt> se
    // disparan sus validaciones (DataAnnotations + rechazo del placeholder), de modo que la app no
    // arranca con un secreto JWT ausente, demasiado corto o el de ejemplo del .env.ejemplo.
    .ValidateOnStart();

builder.Services.AddAuthorization();

// --- OpenAPI (API First) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ALXOR Core API",
        Version = "v1",
        Description = "API del núcleo ALXOR Core. Módulo Identidad.",
    });

    var esquemaJwt = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Introduce el token JWT (sin el prefijo 'Bearer').",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };

    opciones.AddSecurityDefinition("Bearer", esquemaJwt);
    opciones.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquemaJwt] = Array.Empty<string>() });
});

builder.Services.AddProblemDetails();

var app = builder.Build();

// Aplicación de migraciones al arrancar. Controlado por la configuración
// «Migraciones:AplicarAlArrancar» (por defecto true) e INDEPENDIENTE del entorno: así una
// instalación nueva de la clínica crea el esquema sola también en Production. EF Core Migrate() es
// idempotente, por lo que reejecutarlo no tiene efecto si ya está al día.
if (app.Configuration.GetValue("Migraciones:AplicarAlArrancar", true))
{
    using var ambito = app.Services.CreateScope();
    await ambito.ServiceProvider.GetRequiredService<IdentidadDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await ambito.ServiceProvider.GetRequiredService<OrganizacionDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await ambito.ServiceProvider.GetRequiredService<AlxorCore.Terceros.Infraestructura.TercerosDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await ambito.ServiceProvider.GetRequiredService<AlxorCore.Clinica.Infraestructura.ClinicaDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await ambito.ServiceProvider.GetRequiredService<AlxorCore.Catalogo.Infraestructura.CatalogoDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    // (La migración de Clínica incluye la tabla clinica.acceso_portal de la Cartilla Viva.)
    await ambito.ServiceProvider.GetRequiredService<AlxorCore.Facturacion.Infraestructura.FacturacionDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await ambito.ServiceProvider.GetRequiredService<AlxorCore.Gastos.Infraestructura.GastosDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await ambito.ServiceProvider.GetRequiredService<AlxorCore.Tesoreria.Infraestructura.TesoreriaDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await ambito.ServiceProvider.GetRequiredService<AlxorCore.Auditoria.Infraestructura.AuditoriaDbContext>().Database.MigrateAsync().ConfigureAwait(false);
}

// Swagger / SwaggerUI: SOLO en desarrollo (no se exponen en la red de la clínica en Production).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sirve la interfaz web (SPA) desde wwwroot, en el mismo origen que la API (sin CORS).
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // El HTML y el sello de versión NUNCA se cachean: así, tras actualizar el
    // paquete, el navegador siempre recibe la interfaz nueva (evita el clásico
    // "sigue viéndose la versión vieja"). Los demás estáticos usan la caché normal.
    OnPrepareResponse = ctx =>
    {
        var nombre = ctx.File.Name;
        if (nombre.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            || nombre.Equals("version.json", StringComparison.OrdinalIgnoreCase))
        {
            var cabeceras = ctx.Context.Response.Headers;
            cabeceras["Cache-Control"] = "no-cache, no-store, must-revalidate";
            cabeceras["Pragma"] = "no-cache";
            cabeceras["Expires"] = "0";
        }
    },
});

app.UseAuthentication();
app.UseAuthorization();

// Auditoría: registra las operaciones que modifican datos (tras autenticar, para conocer al autor).
app.UseMiddleware<AlxorCore.Api.Comun.MiddlewareAuditoria>();

app.MapGet("/salud", () => Results.Ok(new { estado = "ok" }))
    .WithTags("Salud")
    .WithName("Salud")
    .AllowAnonymous();

app.MapearIdentidad();
app.MapearOrganizacion();
app.MapearUsuarios();
app.MapearTerceros();
app.MapearClinica();
app.MapearBusqueda();
app.MapearPortal();
app.MapearCatalogo();
app.MapearFacturacion();
app.MapearGastos();
app.MapearTesoreria();
app.MapearDocumentos();
app.MapearInformes();
app.MapearAuditoria();
app.MapearCuenta();

// Cualquier ruta no-API devuelve la SPA (enrutado en el cliente).
app.MapFallbackToFile("index.html");

await app.RunAsync().ConfigureAwait(false);

/// <summary>Punto de entrada expuesto para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program;
