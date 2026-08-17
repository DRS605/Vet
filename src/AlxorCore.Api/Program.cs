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
builder.Services.AgregarModuloDocumentos();
builder.Services.AgregarModuloInformes();
builder.Services.AgregarModuloAuditoria(builder.Configuration);

// --- Facturación automática periódica (proceso en segundo plano) ---
builder.Services.Configure<AlxorCore.Api.Servicios.OpcionesFacturacionRecurrente>(
    builder.Configuration.GetSection(AlxorCore.Api.Servicios.OpcionesFacturacionRecurrente.Seccion));
builder.Services.AddHostedService<AlxorCore.Api.Servicios.ServicioFacturacionRecurrente>();

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
    });

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

// En desarrollo aplicamos las migraciones automáticamente para facilitar el arranque.
if (app.Environment.IsDevelopment())
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

    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sirve la interfaz web (SPA) desde wwwroot, en el mismo origen que la API (sin CORS).
app.UseDefaultFiles();
app.UseStaticFiles();

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
