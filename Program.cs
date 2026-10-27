using System.Diagnostics;

// Al ejecutar el .exe publicado, usar la carpeta del ejecutable como raíz
// (wwwroot y AvanceAlumnos.xlsx) aunque se lance desde un acceso directo.
var esDesarrollo = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
if (!esDesarrollo)
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = esDesarrollo ? null : AppContext.BaseDirectory
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Datos: hoy en Excel. Para conectar la base de datos se cambian SOLO estas dos líneas por las
// implementaciones de IRepositorioAlumnos / IRepositorioPlanes que lean y escriban en ella.
builder.Services.AddSingleton<PlaneaUV_Economia.Services.IRepositorioAlumnos, PlaneaUV_Economia.Services.RepositorioAlumnosExcel>();
builder.Services.AddSingleton<PlaneaUV_Economia.Services.IRepositorioPlanes, PlaneaUV_Economia.Services.RepositorioPlanesExcel>();
// Reglas de negocio (no dependen de dónde estén guardados los datos)
builder.Services.AddSingleton<PlaneaUV_Economia.Services.ServicioAlertas>();
builder.Services.AddSingleton<PlaneaUV_Economia.Services.ServicioReportes>();
builder.Services.AddSingleton<PlaneaUV_Economia.Services.ServicioReportePeriodo>();

var app = builder.Build();

// Si algo falla sin control se responde un mensaje simple (sin detalles internos)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(error => error.Run(async contexto =>
    {
        contexto.Response.StatusCode = 500;
        contexto.Response.ContentType = "application/json; charset=utf-8";
        await contexto.Response.WriteAsync("{\"success\":false,\"message\":\"Ocurrió un error inesperado. Si continúa, cierra y vuelve a abrir PlaneaUV.\"}");
    }));
}

// ---------- Seguridad ----------
// PlaneaUV corre en la computadora del usuario (localhost) y no tiene inicio de sesión, así que hay que evitar
// que una página web cualquiera, abierta en el mismo navegador, use el sistema a escondidas:
//   1. Solo se aceptan peticiones dirigidas a localhost (evita el ataque de "DNS rebinding", que permitiría
//      a otra página leer los datos de los alumnos).
//   2. Las peticiones que modifican datos (POST) deben venir de la propia página de PlaneaUV; si el navegador
//      indica que vienen de otro sitio, se rechazan (evita que otra página borre o cambie alumnos: CSRF).
//   3. La página no se puede mostrar dentro de otra (evita engañar al usuario para que haga clic: clickjacking).
//   4. Las respuestas con datos de alumnos no se guardan en la caché del navegador.
var hostsPermitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1", "[::1]", "::1" };
app.Use(async (contexto, siguiente) =>
{
    var peticion = contexto.Request;
    if (!hostsPermitidos.Contains(peticion.Host.Host))
    {
        contexto.Response.StatusCode = 400;
        await contexto.Response.WriteAsync("Host no permitido.");
        return;
    }

    if (!HttpMethods.IsGet(peticion.Method) && !HttpMethods.IsHead(peticion.Method) && !HttpMethods.IsOptions(peticion.Method))
    {
        string sitio = peticion.Headers["Sec-Fetch-Site"].ToString();
        string origen = peticion.Headers.Origin.ToString();
        bool otroSitio = sitio != "" && sitio != "same-origin" && sitio != "none";
        bool otroOrigen = origen != "" && origen != "null" &&
            !(Uri.TryCreate(origen, UriKind.Absolute, out var uriOrigen) && hostsPermitidos.Contains(uriOrigen.Host)
              && uriOrigen.Port == (peticion.Host.Port ?? (peticion.IsHttps ? 443 : 80)));
        if (otroSitio || otroOrigen || origen == "null")
        {
            contexto.Response.StatusCode = 403;
            contexto.Response.ContentType = "application/json; charset=utf-8";
            await contexto.Response.WriteAsync("{\"success\":false,\"message\":\"Petición rechazada: no viene de PlaneaUV.\"}");
            return;
        }
    }

    var encabezados = contexto.Response.Headers;
    encabezados["X-Frame-Options"] = "DENY";
    encabezados["Content-Security-Policy"] = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";
    encabezados["X-Content-Type-Options"] = "nosniff";
    encabezados["Referrer-Policy"] = "no-referrer";
    if (peticion.Path.StartsWithSegments("/Home")) encabezados["Cache-Control"] = "no-store";
    await siguiente();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (!app.Environment.IsDevelopment())
{
    const string url = "http://localhost:5137";
    app.Urls.Add(url);
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Console.WriteLine($"PlaneaUV corriendo en {url}");
        Console.WriteLine("Cierra esta ventana para detener la aplicacion.");
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    });
}

app.Run();
