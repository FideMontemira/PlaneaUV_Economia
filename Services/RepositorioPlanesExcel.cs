using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Acceso a los planes de estudio. Igual que con los alumnos, para usar la base de datos
    // basta con otra implementación de esta interfaz.
    public interface IRepositorioPlanes
    {
        IReadOnlyList<string> Listar();
        bool Existe(string planNombre);
        PlanInfo? Obtener(string planNombre);   // null si no existe
        Task GuardarAsync(IFormFile archivo);
    }

    // Implementación actual: un .xlsx por plan en la carpeta PlanesEstudio.
    //   Hoja 1 (obligatoria): Semestre | Materia | Creditos | Tipo | Prerrequisitos | Area (opcional)
    //   Hoja "Datos" (opcional): filas "Programa" y "Creditos totales" en la columna A, su valor en la B
    //   Hoja "Optativas" (opcional): columna A = nombre de la optativa, columna B = grupo (opcional)
    public class RepositorioPlanesExcel : IRepositorioPlanes
    {
        private readonly string _carpeta;
        // Cada plan se lee una sola vez mientras su archivo no cambie (antes se abría el Excel en cada consulta)
        private readonly ConcurrentDictionary<string, (DateTime modificado, PlanInfo plan)> _cache = new();

        public RepositorioPlanesExcel(IWebHostEnvironment env) : this(Path.Combine(env.ContentRootPath, "PlanesEstudio")) { }

        public RepositorioPlanesExcel(string carpeta) { _carpeta = carpeta; }

        public IReadOnlyList<string> Listar()
        {
            if (!Directory.Exists(_carpeta)) return new List<string>();
            return Directory.GetFiles(_carpeta, "*.xlsx").Select(f => Path.GetFileNameWithoutExtension(f)!).ToList();
        }

        public bool Existe(string planNombre) => !string.IsNullOrWhiteSpace(planNombre) && File.Exists(Ruta(planNombre));

        public PlanInfo? Obtener(string planNombre)
        {
            if (!Existe(planNombre)) return null;
            string ruta = Ruta(planNombre);
            DateTime modificado = File.GetLastWriteTimeUtc(ruta);
            if (_cache.TryGetValue(ruta, out var guardado) && guardado.modificado == modificado) return guardado.plan;

            var plan = Leer(ruta, planNombre);
            _cache[ruta] = (modificado, plan);
            return plan;
        }

        public async Task GuardarAsync(IFormFile archivo)
        {
            Directory.CreateDirectory(_carpeta);
            // GetFileName evita que un nombre con "..\" salga de la carpeta de planes
            string ruta = Path.Combine(_carpeta, Path.GetFileName(archivo.FileName));
            using var stream = new FileStream(ruta, FileMode.Create);
            await archivo.CopyToAsync(stream);
        }

        // GetFileName evita que un nombre con "..\" salga de la carpeta de planes
        private string Ruta(string planNombre) => Path.Combine(_carpeta, Path.GetFileName(planNombre) + ".xlsx");

        private static PlanInfo Leer(string ruta, string planNombre)
        {
            var plan = new PlanInfo { Programa = planNombre.Replace('_', ' ') };
            int creditosDeclarados = 0;

            using (var libro = new XLWorkbook(ruta))
            {
                var ws = libro.Worksheet(1);
                for (int fila = 2; !ws.Cell(fila, 2).IsEmpty(); fila++)
                {
                    // Los valores se leen siempre como texto primero (blindaje contra celdas con formato raro)
                    int.TryParse(ws.Cell(fila, 1).GetString().Trim(), out int semestre);
                    int.TryParse(ws.Cell(fila, 3).GetString().Trim(), out int creditos);
                    plan.Materias.Add(new MateriaPlan
                    {
                        Semestre = semestre,
                        Nombre = ws.Cell(fila, 2).GetString().Trim(),
                        Creditos = creditos,
                        Tipo = ws.Cell(fila, 4).GetString().Trim(),
                        Prerrequisitos = ws.Cell(fila, 5).GetString().Trim(),
                        Area = ws.Cell(fila, 6).GetString().Trim()
                    });
                }

                var wsDatos = libro.Worksheets.FirstOrDefault(h => h.Name.Trim().Equals("Datos", StringComparison.OrdinalIgnoreCase));
                if (wsDatos != null)
                {
                    for (int f = 1; f <= 20; f++)
                    {
                        string clave = wsDatos.Cell(f, 1).GetString().Trim().ToLowerInvariant();
                        string valor = wsDatos.Cell(f, 2).GetString().Trim();
                        if (clave.StartsWith("programa") && valor != "") plan.Programa = valor;
                        else if (clave.StartsWith("cr")) int.TryParse(valor, out creditosDeclarados);
                    }
                }

                var wsOptativas = libro.Worksheets.FirstOrDefault(h => h.Name.Trim().Equals("Optativas", StringComparison.OrdinalIgnoreCase));
                if (wsOptativas != null)
                {
                    // Si la primera fila es un encabezado ("Optativa", "Nombre"...) se salta
                    string primera = wsOptativas.Cell(1, 1).GetString().Trim().ToLowerInvariant();
                    int f = (primera.StartsWith("optativa") || primera.StartsWith("nombre") || primera.StartsWith("materia")) ? 2 : 1;
                    for (; !wsOptativas.Cell(f, 1).IsEmpty(); f++)
                        plan.Optativas.Add(new OptativaPlan { Nombre = wsOptativas.Cell(f, 1).GetString().Trim(), Grupo = wsOptativas.Cell(f, 2).GetString().Trim() });
                }
            }

            // Total oficial si el coordinador lo escribió en la hoja "Datos"; si no, la suma de las materias del plan
            plan.CreditosTotales = creditosDeclarados > 0 ? creditosDeclarados : plan.Materias.Sum(m => m.Creditos);

            foreach (var m in plan.Materias)
                foreach (var req in m.Prerrequisitos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    plan.Seriadas.Add(req);

            return plan;
        }
    }
}
