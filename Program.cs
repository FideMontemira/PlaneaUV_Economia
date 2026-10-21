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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

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
