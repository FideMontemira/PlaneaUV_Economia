using Microsoft.AspNetCore.Mvc;
using PlaneaUV_Economia.Models;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace PlaneaUV_Economia.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _excelFilePath;
        // Todas las lecturas y escrituras de AvanceAlumnos.xlsx pasan por este candado, para que
        // un guardado automático nunca coincida con otra lectura o escritura del mismo archivo
        private static readonly object _excelLock = new object();
        private const string MensajeArchivoOcupado = "No se pudo acceder a AvanceAlumnos.xlsx. Si lo tienes abierto en Excel, ciérralo; los cambios pendientes se guardarán solos.";

        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
            _excelFilePath = Path.Combine(_env.ContentRootPath, "AvanceAlumnos.xlsx");
        }
        public IActionResult Index() { return View(); }

        // Ejecuta una acción sobre AvanceAlumnos.xlsx con el candado tomado y convierte los errores de
        // archivo (p. ej. abierto en Excel) en una respuesta 409 con un mensaje claro para el usuario
        private IActionResult ConArchivoAlumnos(Func<IActionResult> accion)
        {
            try
            {
                lock (_excelLock) return accion();
            }
            catch (IOException)
            {
                return StatusCode(409, new { success = false, message = MensajeArchivoOcupado });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error al procesar AvanceAlumnos.xlsx: " + ex.Message });
            }
        }

        // Guarda primero en un archivo temporal y luego lo intercambia por el original, dejando la versión
        // anterior como AvanceAlumnos.respaldo.xlsx. Así, si el programa se cierra a mitad del guardado,
        // el archivo original no queda dañado.
        private static void GuardarLibroSeguro(XLWorkbook workbook, string filePath)
        {
            string carpeta = Path.GetDirectoryName(filePath)!;
            string temporal = Path.Combine(carpeta, "AvanceAlumnos.guardando.xlsx");
            string respaldo = Path.Combine(carpeta, "AvanceAlumnos.respaldo.xlsx");

            workbook.SaveAs(temporal);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Replace(temporal, filePath, respaldo);
            else
                System.IO.File.Move(temporal, filePath);
        }

        [HttpPost]
        public IActionResult ExportarExcel([FromBody] AlumnoAvance avance) => ConArchivoAlumnos(() => ExportarExcelInterno(avance));

        private IActionResult ExportarExcelInterno(AlumnoAvance avance)
        {
            if (avance == null || string.IsNullOrEmpty(avance.Matricula)) return BadRequest("Datos incompletos.");

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            string hojaSegura = SanitizarNombreHoja(avance.Matricula);

            using (var workbook = System.IO.File.Exists(filePath) ? new XLWorkbook(filePath) : new XLWorkbook())
            {
                IXLWorksheet worksheet;
                if (workbook.Worksheets.Contains(hojaSegura))
                {
                    worksheet = workbook.Worksheet(hojaSegura);
                    worksheet.Clear();
                }
                else
                {
                    worksheet = workbook.Worksheets.Add(hojaSegura);
                }

                worksheet.Cell(1, 1).Value = "Matrícula:";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 2).Value = avance.Matricula;

                worksheet.Cell(2, 1).Value = "Nombre:";
                worksheet.Cell(2, 1).Style.Font.Bold = true;
                worksheet.Cell(2, 2).Value = avance.Nombre;

                worksheet.Cell(3, 1).Value = "Situación:";
                worksheet.Cell(3, 1).Style.Font.Bold = true;
                worksheet.Cell(3, 2).Value = string.IsNullOrEmpty(avance.Situacion) ? "Activo" : avance.Situacion;

                worksheet.Cell(4, 1).Value = "Plan de Estudios:";
                worksheet.Cell(4, 1).Style.Font.Bold = true;
                worksheet.Cell(4, 2).Value = string.IsNullOrEmpty(avance.PlanEstudio) ? "Generico" : avance.PlanEstudio;

                // Periodos sin inscripción: una sola celda (E1), separados por " | "
                worksheet.Cell(1, 4).Value = "Periodos sin inscripción:";
                worksheet.Cell(1, 4).Style.Font.Bold = true;
                worksheet.Cell(1, 5).Value = string.Join(" | ", (avance.PeriodosSinInscripcion ?? new List<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct());

                worksheet.Cell(5, 1).Value = "Materia";
                worksheet.Cell(5, 2).Value = "Periodo";
                worksheet.Cell(5, 3).Value = "Créditos";
                worksheet.Cell(5, 4).Value = "Inscripción";
                worksheet.Cell(5, 5).Value = "Examen";
                worksheet.Cell(5, 6).Value = "Estado";
                worksheet.Cell(5, 7).Value = "Riesgo";
                worksheet.Cell(5, 8).Value = "Rezago";

                var rangoM = worksheet.Range("A5:H5");
                rangoM.Style.Font.Bold = true;
                rangoM.Style.Fill.BackgroundColor = XLColor.LightGray;

                int rowM = 6;
                foreach (var m in avance.Materias)
                {
                    worksheet.Cell(rowM, 1).Value = m.Nombre;
                    worksheet.Cell(rowM, 2).Value = m.Periodo;
                    worksheet.Cell(rowM, 3).Value = m.Creditos;
                    worksheet.Cell(rowM, 4).Value = m.TipoInscripcion;
                    worksheet.Cell(rowM, 5).Value = m.UltimoExamen;
                    worksheet.Cell(rowM, 6).Value = m.Estado;
                    worksheet.Cell(rowM, 7).Value = m.EnRiesgo.ToString();
                    worksheet.Cell(rowM, 8).Value = m.EnRezago.ToString();
                    rowM++;
                }

                worksheet.Cell(5, 10).Value = "Periodo Tutoria";
                worksheet.Cell(5, 11).Value = "Sesión";
                worksheet.Cell(5, 12).Value = "Fecha";
                worksheet.Cell(5, 13).Value = "Asistencia";
                worksheet.Cell(5, 14).Value = "Comentarios";

                var rangoT = worksheet.Range("J5:N5");
                rangoT.Style.Font.Bold = true;
                rangoT.Style.Fill.BackgroundColor = XLColor.LightCyan;

                int rowT = 6;
                foreach (var t in avance.Tutorias)
                {
                    worksheet.Cell(rowT, 10).Value = t.Periodo;
                    worksheet.Cell(rowT, 11).Value = t.Sesion;
                    worksheet.Cell(rowT, 12).Value = t.Fecha;
                    worksheet.Cell(rowT, 13).Value = t.Asistencia;
                    worksheet.Cell(rowT, 14).Value = t.Comentarios;
                    rowT++;
                }

                worksheet.Columns().AdjustToContents();
                GuardarLibroSeguro(workbook, filePath);
            }
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult BuscarAlumnoExcel(string matricula) => ConArchivoAlumnos(() => BuscarAlumnoExcelInterno(matricula));

        private IActionResult BuscarAlumnoExcelInterno(string matricula)
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            if (!System.IO.File.Exists(filePath)) return Json(new { success = false, message = "No hay registros." });

            string hojaSegura = SanitizarNombreHoja(matricula);

            using (var workbook = new XLWorkbook(filePath))
            {
                if (!workbook.Worksheets.Contains(hojaSegura)) return Json(new { success = false, message = "Alumno no encontrado." });

                var ws = workbook.Worksheet(hojaSegura);
                var avance = new AlumnoAvance
                {
                    Matricula = ws.Cell(1, 2).GetString(),
                    Nombre = ws.Cell(2, 2).GetString(),
                    Situacion = ws.Cell(3, 2).GetString(),
                    PlanEstudio = ws.Cell(4, 2).GetString(),
                    PeriodosSinInscripcion = LeerPeriodosSinInscripcion(ws)
                };

                int rowM = 6;
                while (!ws.Cell(rowM, 1).IsEmpty())
                {
                    bool.TryParse(ws.Cell(rowM, 7).GetString(), out bool riesgo);
                    bool.TryParse(ws.Cell(rowM, 8).GetString(), out bool rezago);

                    int.TryParse(ws.Cell(rowM, 3).GetString(), out int creditosParsed);

                    avance.Materias.Add(new MateriaCursada
                    {
                        Nombre = ws.Cell(rowM, 1).GetString(),
                        Periodo = ws.Cell(rowM, 2).GetString(),
                        Creditos = creditosParsed,
                        TipoInscripcion = ws.Cell(rowM, 4).GetString(),
                        UltimoExamen = ws.Cell(rowM, 5).GetString(),
                        Estado = ws.Cell(rowM, 6).GetString(),
                        EnRiesgo = riesgo,
                        EnRezago = rezago
                    });
                    rowM++;
                }

                int rowT = 6;
                while (!ws.Cell(rowT, 10).IsEmpty())
                {
                    int.TryParse(ws.Cell(rowT, 11).GetString(), out int sesionParsed);

                    avance.Tutorias.Add(new TutoriaRecord
                    {
                        Periodo = ws.Cell(rowT, 10).GetString(),
                        Sesion = sesionParsed,
                        Fecha = ws.Cell(rowT, 12).GetString(),
                        Asistencia = ws.Cell(rowT, 13).GetString(),
                        Comentarios = ws.Cell(rowT, 14).GetString()
                    });
                    rowT++;
                }

                return Json(new { success = true, data = avance });
            }
        }

        [HttpPost]
        public IActionResult EliminarAlumno(string matricula) => ConArchivoAlumnos(() => EliminarAlumnoInterno(matricula));

        private IActionResult EliminarAlumnoInterno(string matricula)
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            if (!System.IO.File.Exists(filePath)) return Json(new { success = false, message = "No hay registros." });

            string hojaSegura = SanitizarNombreHoja(matricula);

            bool eraUltimaHoja;
            using (var workbook = new XLWorkbook(filePath))
            {
                if (!workbook.Worksheets.Contains(hojaSegura)) return Json(new { success = false, message = "Alumno no encontrado." });

                // Excel no permite un libro sin hojas: si es el último alumno se borra el archivo completo
                eraUltimaHoja = workbook.Worksheets.Count == 1;
                if (!eraUltimaHoja)
                {
                    workbook.Worksheet(hojaSegura).Delete();
                    GuardarLibroSeguro(workbook, filePath);
                }
            }
            if (eraUltimaHoja)
            {
                // Se conserva como respaldo antes de borrarlo
                System.IO.File.Copy(filePath, Path.Combine(Path.GetDirectoryName(filePath)!, "AvanceAlumnos.respaldo.xlsx"), true);
                System.IO.File.Delete(filePath);
            }
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult ObtenerAlumnosRegistrados() => ConArchivoAlumnos(ObtenerAlumnosRegistradosInterno);

        private IActionResult ObtenerAlumnosRegistradosInterno()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            var lista = new List<object>();
            if (System.IO.File.Exists(filePath))
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    foreach (var ws in workbook.Worksheets)
                    {
                        lista.Add(new
                        {
                            Matricula = ws.Cell(1, 2).GetString(),
                            Nombre = ws.Cell(2, 2).GetString(),
                            Situacion = ws.Cell(3, 2).GetString()
                        });
                    }
                }
            }
            return Json(lista);
        }

        [HttpGet]
        public IActionResult DescargarWordTutorias(string matricula) => ConArchivoAlumnos(() => DescargarWordTutoriasInterno(matricula));

        private IActionResult DescargarWordTutoriasInterno(string matricula)
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula inválida");
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            if (!System.IO.File.Exists(filePath)) return NotFound("No hay datos.");

            string hojaSegura = SanitizarNombreHoja(matricula);
            using (var workbook = new XLWorkbook(filePath))
            {
                if (!workbook.Worksheets.Contains(hojaSegura)) return NotFound("Alumno no encontrado.");
                var ws = workbook.Worksheet(hojaSegura);

                // El título lleva el programa del plan del alumno (antes decía siempre "Economía 2024")
                string planAlumno = ws.Cell(4, 2).GetString().Trim();
                string programa = planAlumno.Replace('_', ' ');
                try { programa = LeerPlan(planAlumno)?.Programa ?? programa; } catch { }
                programa = System.Net.WebUtility.HtmlEncode(programa);

                string html = $@"<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
                <head><meta charset='utf-8'><title>Tutorías</title>
                <style>body {{ font-family: 'Calibri'; }} table {{ border-collapse: collapse; width: 100%; margin-bottom: 20px; }} th, td {{ border: 1px solid #000; padding: 5px; }} th {{ background-color: #f2f2f2; }} .periodo {{ background-color: #0dcaf0; color: #000; padding: 5px; font-weight: bold; font-size: 14pt; margin-top: 20px; }}</style>
                </head><body><h2>Evidencia de Tutoría Académica - {programa}</h2>
                <p><strong>Alumno:</strong> {ws.Cell(2, 2).GetString()}</p><p><strong>Matrícula:</strong> {ws.Cell(1, 2).GetString()}</p><hr/>";

                var tutorias = new List<TutoriaRecord>();
                int r = 6;
                while (!ws.Cell(r, 10).IsEmpty())
                {
                    int.TryParse(ws.Cell(r, 11).GetString(), out int sesionParsed);

                    tutorias.Add(new TutoriaRecord
                    {
                        Periodo = ws.Cell(r, 10).GetString(),
                        Sesion = sesionParsed,
                        Fecha = ws.Cell(r, 12).GetString(),
                        Asistencia = ws.Cell(r, 13).GetString(),
                        Comentarios = ws.Cell(r, 14).GetString()
                    });
                    r++;
                }

                var agrupadoporPeriodo = tutorias.GroupBy(t => t.Periodo);
                string Asist(string val) => val == "asistio" ? "Asistió" : val == "falto" ? "Faltó" : val == "reprogramada" ? "Reprog." : "Pendiente";

                foreach (var grupo in agrupadoporPeriodo)
                {
                    html += $"<div class='periodo'>Periodo: {grupo.Key}</div>";
                    foreach (var sesion in grupo.OrderBy(s => s.Sesion))
                    {
                        html += $@"<table><tr><th style='width:15%'>Sesión {sesion.Sesion}</th><td style='width:20%'>{sesion.Fecha}</td><th style='width:15%'>Asistencia:</th><td>{Asist(sesion.Asistencia)}</td></tr>
                                   <tr><th colspan='4'>Comentarios:</th></tr><tr><td colspan='4' style='height: 60px;'>{sesion.Comentarios}</td></tr></table>";
                    }
                }
                html += "<br/><br/><table style='border: none; text-align: center; margin-top: 50px;'><tr style='border: none;'><td style='border: none; width: 50%;'>_________________________<br/>Firma Tutor</td><td style='border: none; width: 50%;'>_________________________<br/>Firma Estudiante</td></tr></table></body></html>";

                return File(Encoding.UTF8.GetBytes(html), "application/msword", $"Tutorias_{matricula}.doc");
            }
        }

        // =========================================================================
        // PERIODO QUE CURSA EL ALUMNO Y REZAGO AUTOMÁTICO
        // La matrícula lleva el año de ingreso (S21... o zS21... = agosto de 2021). El 1er periodo es el
        // "Agosto - Enero" de su ingreso, el 2do el "Febrero - Julio" siguiente, etc. (misma regla que en el JS).
        // =========================================================================
        private static int? AnioIngresoDeMatricula(string? matricula)
        {
            var m = System.Text.RegularExpressions.Regex.Match((matricula ?? "").Trim(), @"^z?s(\d{2})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? 2000 + int.Parse(m.Groups[1].Value) : (int?)null;
        }

        // En el texto del periodo el año es el de TÉRMINO: "Agosto - Enero 2027" va de agosto 2026 a enero 2027
        private static int? NumeroDePeriodo(int? anioIngreso, string? periodo)
        {
            var m = System.Text.RegularExpressions.Regex.Match(periodo ?? "", @"(\d{4})");
            if (anioIngreso == null || !m.Success) return null;
            int anio = int.Parse(m.Groups[1].Value);
            return periodo!.TrimStart().StartsWith("ago", StringComparison.OrdinalIgnoreCase)
                ? (anio - 1 - anioIngreso.Value) * 2 + 1
                : (anio - anioIngreso.Value) * 2;
        }

        // Periodos en que el alumno no estuvo inscrito (celda E1 de su hoja, separados por "|")
        private static List<string> LeerPeriodosSinInscripcion(IXLWorksheet ws)
        {
            return ws.Cell(1, 5).GetString().Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        // Periodo que REALMENTE cursa el alumno: los periodos corridos desde su ingreso menos los periodos
        // anteriores en que no estuvo inscrito (bajas temporales). Null si la matrícula no trae año de ingreso.
        private static int? PeriodoQueCursa(string? matricula, string? periodo, List<string> periodosSinInscripcion)
        {
            int? ingreso = AnioIngresoDeMatricula(matricula);
            int? corridos = NumeroDePeriodo(ingreso, periodo);
            if (corridos == null) return null;
            int pausas = periodosSinInscripcion.Select(p => NumeroDePeriodo(ingreso, p)).Count(n => n >= 1 && n < corridos);
            return corridos - pausas;
        }

        // Rezago automático: materias de periodos ANTERIORES al que cursa el alumno que todavía no aprueba.
        // Las AFEL no cuentan (no pertenecen a un periodo). Las optativas se cuentan por número: cada optativa
        // aprobada (con el nombre que sea del catálogo) cubre un lugar de optativa del plan, en orden.
        private static List<MateriaPlan> MateriasEnRezagoAutomatico(PlanInfo plan, int periodoQueCursa, HashSet<string> aprobadas)
        {
            var nombresOptativa = new HashSet<string>(plan.Optativas.Select(o => o.Nombre));
            foreach (var m in plan.Materias.Where(m => m.Tipo.Equals("optativa", StringComparison.OrdinalIgnoreCase))) nombresOptativa.Add(m.Nombre);
            int optativasAprobadas = aprobadas.Count(a => nombresOptativa.Contains(a));

            var rezago = new List<MateriaPlan>();
            foreach (var m in plan.Materias.Where(m => m.Semestre >= 1 && m.Semestre < periodoQueCursa).OrderBy(m => m.Semestre))
            {
                if (m.Tipo.Equals("afel", StringComparison.OrdinalIgnoreCase)) continue;
                if (m.Tipo.Equals("optativa", StringComparison.OrdinalIgnoreCase))
                {
                    if (optativasAprobadas > 0) optativasAprobadas--; else rezago.Add(m);
                    continue;
                }
                if (!aprobadas.Contains(m.Nombre)) rezago.Add(m);
            }
            return rezago;
        }

        // Lee el plan una sola vez por petición aunque lo compartan muchos alumnos
        private PlanInfo? LeerPlanConCache(string planNombre, Dictionary<string, PlanInfo?> cache)
        {
            if (!cache.TryGetValue(planNombre, out var plan))
            {
                try { plan = LeerPlan(planNombre); } catch { plan = null; }
                cache[planNombre] = plan;
            }
            return plan;
        }

        private static string ListarNombres(IEnumerable<string> nombres, int maximo)
        {
            var lista = nombres.ToList();
            return lista.Count <= maximo ? string.Join(", ", lista) : string.Join(", ", lista.Take(maximo)) + $" y {lista.Count - maximo} más";
        }

        // 'periodo' es el Periodo de Trabajo elegido en pantalla; con él se sabe en qué periodo va cada alumno
        [HttpGet]
        public IActionResult ObtenerReporteAlertas(string? periodo) => ConArchivoAlumnos(() => ObtenerReporteAlertasInterno(periodo));

        private IActionResult ObtenerReporteAlertasInterno(string? periodo)
        {
            return Json(new { success = true, data = CalcularAlertas(periodo) });
        }

        // Lista de alertas de todos los alumnos activos, de la más grave a la menos grave.
        // La usan el panel de alertas y el panel del grupo (que solo cuenta cuántas tiene cada alumno).
        private List<object> CalcularAlertas(string? periodo)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            var alertas = new List<object>();
            if (!System.IO.File.Exists(filePath)) return alertas;

            var planes = new Dictionary<string, PlanInfo?>();

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    var sit = worksheet.Cell(3, 2).GetString();
                    if (sit != "Activo" && !string.IsNullOrEmpty(sit)) continue;

                    var materiasHistorial = new Dictionary<string, MateriaCursada>();
                    var aprobadas = new HashSet<string>();
                    bool tieneRegistroEnPeriodo = false;
                    int fila = 6;
                    while (!string.IsNullOrWhiteSpace(worksheet.Cell(fila, 1).GetString()))
                    {
                        var m = new MateriaCursada { Nombre = worksheet.Cell(fila, 1).GetString(), TipoInscripcion = worksheet.Cell(fila, 4).GetString()?.ToLower(), UltimoExamen = worksheet.Cell(fila, 5).GetString()?.ToLower(), Estado = worksheet.Cell(fila, 6).GetString()?.ToLower() };
                        bool.TryParse(worksheet.Cell(fila, 7).GetString(), out bool enRiesgo);
                        m.EnRiesgo = enRiesgo;

                        if (m.Estado == "aprobada") aprobadas.Add(m.Nombre!);
                        if (!string.IsNullOrEmpty(periodo) && worksheet.Cell(fila, 2).GetString() == periodo) tieneRegistroEnPeriodo = true;

                        materiasHistorial[m.Nombre] = m;
                        fila++;
                    }

                    // ---------- Alertas que dependen del periodo que cursa el alumno ----------
                    string matriculaAlumno = worksheet.Cell(1, 2).GetString(), nombreAlumno = worksheet.Cell(2, 2).GetString();
                    var pausas = LeerPeriodosSinInscripcion(worksheet);
                    int? periodoQueCursa = PeriodoQueCursa(matriculaAlumno, periodo, pausas);
                    var plan = LeerPlanConCache(worksheet.Cell(4, 2).GetString().Trim(), planes);
                    // Si en el periodo de trabajo el alumno está en pausa, no se le exige tener materias registradas
                    bool enPausaEstePeriodo = !string.IsNullOrEmpty(periodo) && pausas.Contains(periodo);

                    if (periodoQueCursa >= 1 && plan != null && plan.Materias.Count > 0)
                    {
                        var materiasDelPlan = plan.Materias.Where(m => !m.Tipo.Equals("afel", StringComparison.OrdinalIgnoreCase)).ToList();
                        int duracionPlan = materiasDelPlan.Count > 0 ? materiasDelPlan.Max(m => m.Semestre) : 0;
                        var rezago = MateriasEnRezagoAutomatico(plan, periodoQueCursa.Value, aprobadas);
                        // Pendientes de todo el plan (no solo de periodos anteriores): si no le falta nada, ya terminó
                        bool leFaltanMaterias = MateriasEnRezagoAutomatico(plan, int.MaxValue, aprobadas).Count > 0;

                        if (rezago.Count > 0)
                        {
                            if (periodoQueCursa > duracionPlan)
                                alertas.Add(new { matricula = matriculaAlumno, nombre = nombreAlumno, etiqueta = "Detalle", materia = $"Cursa el {periodoQueCursa}° periodo de un plan de {duracionPlan}; le faltan {rezago.Count} materias.", resaltar = rezago[0].Nombre, mensaje = "Excede la duración del plan", badge = "bg-danger", score = 90 });

                            var candados = rezago.Where(m => plan.Seriadas.Contains(m.Nombre)).ToList();
                            if (candados.Count > 0)
                                alertas.Add(new { matricula = matriculaAlumno, nombre = nombreAlumno, etiqueta = "Bloquean otras materias", materia = ListarNombres(candados.Select(m => m.Nombre), 4), resaltar = candados[0].Nombre, mensaje = $"Candados pendientes ({candados.Count})", badge = "bg-danger", score = 75 });

                            alertas.Add(new { matricula = matriculaAlumno, nombre = nombreAlumno, etiqueta = "Atrasadas", materia = ListarNombres(rezago.Select(m => m.Nombre), 4), resaltar = rezago[0].Nombre, mensaje = $"Rezago: {rezago.Count} materia(s) de periodos anteriores", badge = "bg-secondary", score = 70 });
                        }

                        if (leFaltanMaterias && !tieneRegistroEnPeriodo && !enPausaEstePeriodo)
                            alertas.Add(new { matricula = matriculaAlumno, nombre = nombreAlumno, etiqueta = "Periodo", materia = periodo!, resaltar = "", mensaje = "Sin materias registradas en el periodo", badge = "bg-warning text-dark", score = 50 });
                    }

                    // Faltas a tutoría en el periodo de trabajo
                    if (!string.IsNullOrEmpty(periodo))
                    {
                        int faltas = 0, filaT = 6;
                        while (!worksheet.Cell(filaT, 10).IsEmpty())
                        {
                            if (worksheet.Cell(filaT, 10).GetString() == periodo && worksheet.Cell(filaT, 13).GetString() == "falto") faltas++;
                            filaT++;
                        }
                        if (faltas > 0)
                            alertas.Add(new { matricula = matriculaAlumno, nombre = nombreAlumno, etiqueta = "Periodo", materia = periodo, resaltar = "", mensaje = $"Faltó a {faltas} tutoría(s)", badge = "bg-warning text-dark", score = 45 });
                    }

                    foreach (var mat in materiasHistorial.Values)
                    {
                        int nivelRiesgo = 0;
                        string msg = "", badge = "";
                        if (mat.TipoInscripcion == "segunda" && mat.UltimoExamen == "ultima" && mat.Estado == "reprobada") { nivelRiesgo = 100; msg = "Candidato a Baja"; badge = "bg-danger"; }
                        else if (mat.TipoInscripcion == "segunda" && mat.Estado != "aprobada") { nivelRiesgo = 80; msg = "En 2da Inscripción"; badge = "bg-danger"; }
                        else if (mat.EnRiesgo) { nivelRiesgo = 60; msg = "Riesgo Manual"; badge = "bg-warning text-dark"; }
                        else if (mat.Estado == "reprobada" && mat.TipoInscripcion == "primera") { nivelRiesgo = 40; msg = "Reprobada 1ª Insc."; badge = "bg-warning text-dark"; }

                        if (nivelRiesgo > 0) alertas.Add(new { matricula = worksheet.Cell(1, 2).GetString(), nombre = worksheet.Cell(2, 2).GetString(), materia = mat.Nombre, mensaje = msg, badge = badge, score = nivelRiesgo });
                    }
                }
            }
            return alertas.OrderByDescending(a => (int)((dynamic)a).score).ToList();
        }

        // =========================================================================
        // PANEL DEL GRUPO: una fila por alumno con su periodo, avance, rezago y número de alertas
        // =========================================================================
        [HttpGet]
        public IActionResult ObtenerPanelGrupo(string? periodo) => ConArchivoAlumnos(() => ObtenerPanelGrupoInterno(periodo));

        private IActionResult ObtenerPanelGrupoInterno(string? periodo)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            var filas = new List<object>();
            if (!System.IO.File.Exists(filePath)) return Json(new { success = true, data = filas });

            var alertasPorAlumno = CalcularAlertas(periodo).GroupBy(a => (string)((dynamic)a).matricula).ToDictionary(g => g.Key, g => g.Count());
            var planes = new Dictionary<string, PlanInfo?>();

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var ws in workbook.Worksheets)
                {
                    string matricula = ws.Cell(1, 2).GetString(), planNombre = ws.Cell(4, 2).GetString().Trim();
                    var plan = LeerPlanConCache(planNombre, planes);

                    // Créditos aprobados: cada materia aprobada cuenta una sola vez
                    var aprobadas = new Dictionary<string, int>();
                    int enCurso = 0, fila = 6;
                    while (!string.IsNullOrWhiteSpace(ws.Cell(fila, 1).GetString()))
                    {
                        string estado = ws.Cell(fila, 6).GetString().ToLower();
                        if (estado == "aprobada") { int.TryParse(ws.Cell(fila, 3).GetString(), out int cr); aprobadas[ws.Cell(fila, 1).GetString()] = cr; }
                        else if (estado == "cursando" && !string.IsNullOrEmpty(periodo) && ws.Cell(fila, 2).GetString() == periodo) enCurso++;
                        fila++;
                    }

                    int creditosAprobados = aprobadas.Values.Sum();
                    int creditosTotales = plan?.CreditosTotales ?? 0;
                    int? periodoQueCursa = PeriodoQueCursa(matricula, periodo, LeerPeriodosSinInscripcion(ws));
                    int duracionPlan = plan == null ? 0 : plan.Materias.Where(m => !m.Tipo.Equals("afel", StringComparison.OrdinalIgnoreCase)).Select(m => m.Semestre).DefaultIfEmpty(0).Max();
                    int rezago = (plan != null && periodoQueCursa >= 1) ? MateriasEnRezagoAutomatico(plan, periodoQueCursa.Value, new HashSet<string>(aprobadas.Keys)).Count : 0;

                    filas.Add(new
                    {
                        matricula,
                        nombre = ws.Cell(2, 2).GetString(),
                        situacion = ws.Cell(3, 2).GetString(),
                        programa = plan?.Programa ?? (planNombre == "" || planNombre == "Generico" ? "Sin plan" : planNombre),
                        periodoQueCursa,
                        duracionPlan,
                        creditosAprobados,
                        creditosTotales,
                        avance = creditosTotales > 0 ? Math.Min(100, (int)Math.Round(creditosAprobados * 100.0 / creditosTotales)) : 0,
                        enCurso,
                        rezago,
                        alertas = alertasPorAlumno.TryGetValue(matricula, out int n) ? n : 0
                    });
                }
            }
            return Json(new { success = true, data = filas });
        }

        [HttpGet]
        public IActionResult ObtenerReporteGlobal(string? periodo) => ConArchivoAlumnos(() => ObtenerReporteGlobalInterno(periodo));

        private IActionResult ObtenerReporteGlobalInterno(string? periodo)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            // Sin alumnos registrados el reporte está vacío (antes regresaba error y la pantalla se quedaba cargando)
            if (!System.IO.File.Exists(filePath))
                return Json(new { success = true, data = new { riesgos = Array.Empty<object>(), rezagos = Array.Empty<object>(), seriadas = Array.Empty<object>() } });

            var listaRiesgos = new List<ReporteItem>();
            var listaRezagos = new List<ReporteItem>();
            var listaSeriadas = new List<ReporteItem>();
            // Las materias "candado" dependen del plan de cada alumno (las que son prerrequisito de otra).
            // Se guarda cada plan ya leído para no abrir el mismo Excel una vez por alumno.
            var planes = new Dictionary<string, PlanInfo?>();

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    if (worksheet.Cell(3, 2).GetString() != "Activo") continue;

                    var plan = LeerPlanConCache(worksheet.Cell(4, 2).GetString().Trim(), planes);
                    var candados = plan?.Seriadas ?? new HashSet<string>();

                    var materiasVigentes = new Dictionary<string, MateriaCursada>();
                    var aprobadas = new HashSet<string>();
                    int fila = 6;
                    while (!string.IsNullOrWhiteSpace(worksheet.Cell(fila, 1).GetString()))
                    {
                        bool.TryParse(worksheet.Cell(fila, 7).GetString(), out bool riesgo);
                        bool.TryParse(worksheet.Cell(fila, 8).GetString(), out bool rezago);
                        var registro = new MateriaCursada { Nombre = worksheet.Cell(fila, 1).GetString(), Estado = worksheet.Cell(fila, 6).GetString()?.ToLower(), EnRiesgo = riesgo, EnRezago = rezago };
                        if (registro.Estado == "aprobada") aprobadas.Add(registro.Nombre!);
                        materiasVigentes[registro.Nombre!] = registro;
                        fila++;
                    }

                    string mat = worksheet.Cell(1, 2).GetString(), nom = worksheet.Cell(2, 2).GetString();
                    var enRezago = new HashSet<string>();
                    var enSeriadas = new HashSet<string>();
                    foreach (var m in materiasVigentes.Values)
                    {
                        if (m.EnRiesgo) listaRiesgos.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
                        if (m.EnRezago && enRezago.Add(m.Nombre!)) listaRezagos.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
                        if (candados.Contains(m.Nombre!) && (m.Estado == "reprobada" || m.EnRezago) && enSeriadas.Add(m.Nombre!)) listaSeriadas.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
                    }

                    // Rezago automático: materias de periodos anteriores al que cursa que aún no aprueba
                    int? periodoQueCursa = PeriodoQueCursa(mat, periodo, LeerPeriodosSinInscripcion(worksheet));
                    if (periodoQueCursa >= 1 && plan != null)
                    {
                        foreach (var m in MateriasEnRezagoAutomatico(plan, periodoQueCursa.Value, aprobadas))
                        {
                            if (enRezago.Add(m.Nombre)) listaRezagos.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
                            if (candados.Contains(m.Nombre) && enSeriadas.Add(m.Nombre)) listaSeriadas.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
                        }
                    }
                }
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    riesgos = listaRiesgos.GroupBy(x => x.Materia).Select(g => new { materia = g.Key, alumnos = g.Select(a => new { nombre = a.Nombre, matricula = a.Matricula }) }),
                    rezagos = listaRezagos.GroupBy(x => x.Materia).Select(g => new { materia = g.Key, alumnos = g.Select(a => new { nombre = a.Nombre, matricula = a.Matricula }) }),
                    seriadas = listaSeriadas.GroupBy(x => x.Materia).Select(g => new { materia = g.Key, alumnos = g.Select(a => new { nombre = a.Nombre, matricula = a.Matricula }) })
                }
            });
        }
        // =========================================================================
        // GESTOR DE PLANES DE ESTUDIO (PLANTILLAS EXCEL)
        // =========================================================================

        [HttpPost]
        public async Task<IActionResult> SubirPlanEstudio(IFormFile archivoPlan)
        {
            if (archivoPlan == null || archivoPlan.Length == 0)
                return Json(new { success = false, message = "No se seleccionó ningún archivo." });

            if (!archivoPlan.FileName.EndsWith(".xlsx"))
                return Json(new { success = false, message = "El archivo debe ser un formato Excel válido (.xlsx)." });

            try
            {
                // Usamos ContentRootPath para que sea compatible con IIS y seguro
                string carpetaPlanes = Path.Combine(_env.ContentRootPath, "PlanesEstudio");

                // Si la carpeta no existe, la creamos mágicamente
                if (!Directory.Exists(carpetaPlanes))
                {
                    Directory.CreateDirectory(carpetaPlanes);
                }

                // Armamos la ruta final donde se guardará el Excel
                string rutaCompleta = Path.Combine(carpetaPlanes, archivoPlan.FileName);

                // Guardamos el archivo en el servidor
                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await archivoPlan.CopyToAsync(stream);
                }

                return Json(new { success = true, message = "Plan de estudios subido correctamente." });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Error interno al guardar: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ObtenerPlanesEstudio()
        {
            string carpetaPlanes = Path.Combine(_env.ContentRootPath, "PlanesEstudio");

            if (!Directory.Exists(carpetaPlanes))
            {
                return Json(new List<string>());
            }

            var archivos = Directory.GetFiles(carpetaPlanes, "*.xlsx")
                                    .Select(Path.GetFileNameWithoutExtension)
                                    .ToList();

            return Json(archivos);
        }
        [HttpGet]
        [HttpGet]
        public IActionResult ObtenerMapaCurricular(string planNombre)
        {
            if (string.IsNullOrEmpty(planNombre))
                return Json(new { success = false, message = "Nombre del plan no proporcionado." });

            string rutaArchivo = Path.Combine(_env.ContentRootPath, "PlanesEstudio", planNombre + ".xlsx");

            if (!System.IO.File.Exists(rutaArchivo))
                return Json(new { success = false, message = "El plan de estudios no existe en el servidor." });

            try
            {
                var plan = LeerPlan(planNombre)!;
                return Json(new
                {
                    success = true,
                    data = plan.Materias,
                    programa = plan.Programa,
                    creditosTotales = plan.CreditosTotales,
                    optativas = plan.Optativas
                });
            }
            catch (System.Exception ex)
            {
                // Si vuelve a fallar, nos dirá EXACTAMENTE por qué falló.
                return Json(new { success = false, message = "Error C#: " + ex.Message });
            }
        }

        // =========================================================================
        // LECTURA DE LA PLANTILLA DE UN PLAN DE ESTUDIOS
        // Hoja 1 (obligatoria): Semestre | Materia | Creditos | Tipo | Prerrequisitos | Area (opcional)
        // Hoja "Datos" (opcional):     filas "Programa" y "Creditos totales" en columna A, su valor en columna B
        // Hoja "Optativas" (opcional): columna A = nombre de la optativa, columna B = grupo/área (opcional)
        // Nada del plan está fijo en el código: créditos, optativas y seriadas salen de aquí.
        // =========================================================================
        private class MateriaPlan
        {
            public int Semestre { get; set; }
            public string Nombre { get; set; } = "";
            public int Creditos { get; set; }
            public string Tipo { get; set; } = "";
            public string Prerrequisitos { get; set; } = "";
            public string Area { get; set; } = "";
        }

        private class OptativaPlan
        {
            public string Nombre { get; set; } = "";
            public string Grupo { get; set; } = "";
        }

        private class PlanInfo
        {
            public string Programa { get; set; } = "";
            public int CreditosTotales { get; set; }
            public List<MateriaPlan> Materias { get; set; } = new List<MateriaPlan>();
            public List<OptativaPlan> Optativas { get; set; } = new List<OptativaPlan>();
            // Materias "candado": las que son prerrequisito de alguna otra materia del plan
            public HashSet<string> Seriadas { get; set; } = new HashSet<string>();
        }

        // Devuelve null si el plan no existe en la carpeta PlanesEstudio
        private PlanInfo? LeerPlan(string planNombre)
        {
            if (string.IsNullOrWhiteSpace(planNombre)) return null;
            // GetFileName evita que un nombre con "..\" salga de la carpeta de planes
            string rutaArchivo = Path.Combine(_env.ContentRootPath, "PlanesEstudio", Path.GetFileName(planNombre) + ".xlsx");
            if (!System.IO.File.Exists(rutaArchivo)) return null;

            var plan = new PlanInfo { Programa = planNombre.Replace('_', ' ') };
            int creditosDeclarados = 0;

            using (var workbook = new XLWorkbook(rutaArchivo))
            {
                var ws = workbook.Worksheet(1);
                int fila = 2;
                while (!ws.Cell(fila, 2).IsEmpty())
                {
                    // BLINDAJE: Extraemos los valores siempre como String primero
                    int.TryParse(ws.Cell(fila, 1).GetString().Trim(), out int semestre);
                    int.TryParse(ws.Cell(fila, 3).GetString().Trim(), out int creditos);

                    plan.Materias.Add(new MateriaPlan
                    {
                        Semestre = semestre,
                        Nombre = ws.Cell(fila, 2).GetString().Trim(),
                        Creditos = creditos,
                        Tipo = ws.Cell(fila, 4).GetString().Trim(),
                        Prerrequisitos = ws.Cell(fila, 5).GetString().Trim(),
                        // Columna F opcional: Área (General, Básica, Disciplinar, Terminal) para el color de la tarjeta
                        Area = ws.Cell(fila, 6).GetString().Trim()
                    });
                    fila++;
                }

                var wsDatos = workbook.Worksheets.FirstOrDefault(h => h.Name.Trim().Equals("Datos", StringComparison.OrdinalIgnoreCase));
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

                var wsOptativas = workbook.Worksheets.FirstOrDefault(h => h.Name.Trim().Equals("Optativas", StringComparison.OrdinalIgnoreCase));
                if (wsOptativas != null)
                {
                    // Si la primera fila es un encabezado ("Optativa", "Nombre"...) se salta
                    string primera = wsOptativas.Cell(1, 1).GetString().Trim().ToLowerInvariant();
                    int f = (primera.StartsWith("optativa") || primera.StartsWith("nombre") || primera.StartsWith("materia")) ? 2 : 1;
                    while (!wsOptativas.Cell(f, 1).IsEmpty())
                    {
                        plan.Optativas.Add(new OptativaPlan { Nombre = wsOptativas.Cell(f, 1).GetString().Trim(), Grupo = wsOptativas.Cell(f, 2).GetString().Trim() });
                        f++;
                    }
                }
            }

            // Total oficial si el coordinador lo escribió en la hoja "Datos"; si no, la suma de las materias del plan
            plan.CreditosTotales = creditosDeclarados > 0 ? creditosDeclarados : plan.Materias.Sum(m => m.Creditos);

            foreach (var m in plan.Materias)
                foreach (var req in m.Prerrequisitos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    plan.Seriadas.Add(req);

            return plan;
        }
        private string SanitizarNombreHoja(string nombre) { if (string.IsNullOrWhiteSpace(nombre)) return "SinNombre"; foreach (char c in new[] { '\\', '/', '?', '*', '[', ']', ':' }) nombre = nombre.Replace(c.ToString(), ""); return nombre.Length > 31 ? nombre.Substring(0, 31) : nombre; }
    }

}