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
        // Devuelve los avisos de la plantilla (lo que conviene revisar aunque el plan sí se guardó)
        Task<IReadOnlyList<string>> GuardarAsync(IFormFile archivo);
    }

    // Implementación actual: un .xlsx por plan en la carpeta PlanesEstudio.
    //   Hoja 1 (obligatoria), en una de dos formas:
    //     - mapa curricular dibujado como el de la UV (ver LectorMapaCurricular), o
    //     - tabla: Semestre | Materia | Creditos | Tipo | Prerrequisitos | Area (opcional) | Creditos minimos (%) (opcional)
    //   Hoja "Datos" (opcional): filas "Programa" y "Creditos totales" en la columna A, su valor en la B
    //   Hoja "Optativas" (opcional): columna A = nombre de la optativa, columna B = grupo (opcional)
    //   En "Datos" también puede ir "Periodos trayectoria larga" (si no, se usa 1.5 veces la duración del plan)
    //   Hoja "Apoyos" (opcional): columna A = apoyo, columna B = horario, lugar o cómo solicitarlo
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
            // Se omiten los temporales de Excel (~$...) y los de una subida en curso (.xxx.subiendo.xlsx)
            return Directory.GetFiles(_carpeta, "*.xlsx").Select(f => Path.GetFileNameWithoutExtension(f)!)
                .Where(n => !n.StartsWith("~$") && !n.StartsWith(".")).ToList();
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

        public const long TamanoMaximo = 5 * 1024 * 1024;   // 5 MB: una plantilla de plan pesa unos KB

        // Guarda primero en un archivo temporal, revisa que sea una plantilla válida y solo entonces
        // reemplaza al plan anterior. Así un archivo dañado o equivocado no borra un plan que funciona.
        public async Task<IReadOnlyList<string>> GuardarAsync(IFormFile archivo)
        {
            if (archivo.Length > TamanoMaximo) throw new ArgumentException("El archivo es demasiado grande (máximo 5 MB).");

            // GetFileName evita que un nombre con "..\" salga de la carpeta de planes
            string nombre = Path.GetFileName(archivo.FileName ?? "");
            string sinExtension = Path.GetFileNameWithoutExtension(nombre);
            if (!nombre.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || sinExtension.Length == 0 || sinExtension.Length > 80
                || sinExtension.Any(c => !(char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.' || c == '(' || c == ')')))
                throw new ArgumentException("El nombre del archivo solo puede tener letras, números, espacios, guiones y guion bajo, y terminar en .xlsx.");

            Directory.CreateDirectory(_carpeta);
            string ruta = Path.Combine(_carpeta, sinExtension + ".xlsx");
            string temporal = Path.Combine(_carpeta, "." + Guid.NewGuid().ToString("N") + ".subiendo.xlsx");
            try
            {
                using (var stream = new FileStream(temporal, FileMode.CreateNew))
                    await archivo.CopyToAsync(stream);

                PlanInfo plan;
                try { plan = Leer(temporal, sinExtension); }
                catch (Exception) { throw new ArgumentException("El archivo no se pudo leer como plantilla de plan de estudios. Revisa que sea un Excel (.xlsx) válido."); }
                if (plan.Materias.Count == 0)
                    throw new ArgumentException("La plantilla no tiene materias. La primera hoja debe ser el mapa curricular (encabezados \"1er Periodo\", \"2do Periodo\"... y un recuadro por materia con sus créditos) o la tabla con encabezados en la fila 1 y las materias desde la fila 2 (columna B = nombre).");
                if (plan.Materias.Any(m => m.Creditos < 0 || m.Creditos > 100))
                    throw new ArgumentException("Hay materias con créditos no válidos en la plantilla.");
                var malPorcentaje = plan.Materias.FirstOrDefault(m => m.PorcentajeCreditos < 0);
                if (malPorcentaje != null)
                    throw new ArgumentException($"El porcentaje de créditos de \"{malPorcentaje.Nombre}\" debe estar entre 0 y 100 (columna G o \"N % de créditos\" en el mapa).");
                var malaDuracion = plan.Materias.FirstOrDefault(m => m.PeriodosDuracion < 1);
                if (malaDuracion != null)
                    throw new ArgumentException($"Los periodos que dura \"{malaDuracion.Nombre}\" deben ser un número entero de 1 a {MaxPeriodosDuracion} (columna H o \"dura N periodos\" en el mapa).");

                File.Move(temporal, ruta, overwrite: true);
                _cache.TryRemove(ruta, out _);
                return plan.Avisos;
            }
            finally
            {
                BorrarTemporal(temporal);
            }
        }

        // Si el Excel no era válido, la librería puede tardar en soltar el archivo: se reintenta una vez
        private static void BorrarTemporal(string ruta)
        {
            if (!File.Exists(ruta)) return;
            try { File.Delete(ruta); }
            catch (IOException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                try { File.Delete(ruta); } catch (IOException) { }   // queda oculto (empieza con ".") y no se lista
            }
        }

        // Columna G "Creditos minimos (%)": acepta 70, "70 %" o 0.7 (celda con formato de porcentaje).
        // Vacío o no numérico = sin requisito. Un valor fuera de 0-100 se marca con -1 para rechazar la plantilla.
        public static int LeerPorcentaje(string texto)
        {
            texto = (texto ?? "").Trim().TrimEnd('%').Trim().Replace(',', '.');
            if (!double.TryParse(texto, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double valor)) return 0;
            if (valor > 0 && valor <= 1) valor *= 100;
            if (valor < 0 || valor > 100) return -1;
            return (int)Math.Round(valor, MidpointRounding.AwayFromZero);
        }

        // Columna H: periodos que dura la materia. Vacía o no numérica = 1; de 1 a 4 es válido; otro número = -1
        // (la plantilla se rechaza al subirla).
        public const int MaxPeriodosDuracion = 4;
        public static int LeerDuracion(string texto)
        {
            texto = (texto ?? "").Trim();
            if (!double.TryParse(texto, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double valor)) return 1;
            if (valor != Math.Floor(valor) || valor < 1 || valor > MaxPeriodosDuracion) return -1;
            return (int)valor;
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
                // La primera hoja puede ser el mapa curricular dibujado (formato de la UV) o la tabla de materias
                if (LectorMapaCurricular.EsMapa(ws)) plan.Materias.AddRange(LectorMapaCurricular.Leer(ws, plan.Avisos));
                else for (int fila = 2; !ws.Cell(fila, 2).IsEmpty(); fila++)
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
                        Area = ws.Cell(fila, 6).GetString().Trim(),
                        PorcentajeCreditos = LeerPorcentaje(ws.Cell(fila, 7).GetString()),
                        PeriodosDuracion = LeerDuracion(ws.Cell(fila, 8).GetString())
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
                        else if (clave.StartsWith("periodos") && int.TryParse(valor, out int larga) && larga > 0) plan.PeriodosTrayectoriaLarga = larga;
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

                var wsApoyos = libro.Worksheets.FirstOrDefault(h => h.Name.Trim().Equals("Apoyos", StringComparison.OrdinalIgnoreCase));
                if (wsApoyos != null)
                {
                    string primera = wsApoyos.Cell(1, 1).GetString().Trim().ToLowerInvariant();
                    int f = (primera.StartsWith("apoyo") || primera.StartsWith("nombre") || primera.StartsWith("servicio")) ? 2 : 1;
                    for (; !wsApoyos.Cell(f, 1).IsEmpty(); f++)
                        plan.Apoyos.Add(new ApoyoPlan { Nombre = wsApoyos.Cell(f, 1).GetString().Trim(), Detalle = wsApoyos.Cell(f, 2).GetString().Trim() });
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
