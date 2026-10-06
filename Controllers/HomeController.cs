using Microsoft.AspNetCore.Mvc;
using PlaneaUV_Economia.Models;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace PlaneaUV_Economia.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() { return View(); }

        [HttpPost]
        public IActionResult ExportarExcel([FromBody] AlumnoAvance avance)
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
                workbook.SaveAs(filePath);
            }
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult BuscarAlumnoExcel(string matricula)
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
                    Situacion = ws.Cell(3, 2).GetString()
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

        [HttpGet]
        public IActionResult ObtenerAlumnosRegistrados()
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
        public IActionResult DescargarWordTutorias(string matricula)
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula inválida");
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            if (!System.IO.File.Exists(filePath)) return NotFound("No hay datos.");

            string hojaSegura = SanitizarNombreHoja(matricula);
            using (var workbook = new XLWorkbook(filePath))
            {
                if (!workbook.Worksheets.Contains(hojaSegura)) return NotFound("Alumno no encontrado.");
                var ws = workbook.Worksheet(hojaSegura);

                string html = $@"<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
                <head><meta charset='utf-8'><title>Tutorías</title>
                <style>body {{ font-family: 'Calibri'; }} table {{ border-collapse: collapse; width: 100%; margin-bottom: 20px; }} th, td {{ border: 1px solid #000; padding: 5px; }} th {{ background-color: #f2f2f2; }} .periodo {{ background-color: #0dcaf0; color: #000; padding: 5px; font-weight: bold; font-size: 14pt; margin-top: 20px; }}</style>
                </head><body><h2>Evidencia de Tutoría Académica - Economía 2024</h2>
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

        [HttpGet]
        public IActionResult ObtenerReporteAlertas()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            var alertas = new List<object>();
            if (!System.IO.File.Exists(filePath)) return Json(new { success = true, data = alertas });

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    var sit = worksheet.Cell(3, 2).GetString();
                    if (sit != "Activo" && !string.IsNullOrEmpty(sit)) continue;

                    var materiasHistorial = new Dictionary<string, MateriaCursada>();
                    int fila = 6;
                    while (!string.IsNullOrWhiteSpace(worksheet.Cell(fila, 1).GetString()))
                    {
                        var m = new MateriaCursada { Nombre = worksheet.Cell(fila, 1).GetString(), TipoInscripcion = worksheet.Cell(fila, 4).GetString()?.ToLower(), UltimoExamen = worksheet.Cell(fila, 5).GetString()?.ToLower(), Estado = worksheet.Cell(fila, 6).GetString()?.ToLower() };
                        bool.TryParse(worksheet.Cell(fila, 7).GetString(), out bool enRiesgo);
                        m.EnRiesgo = enRiesgo;

                        materiasHistorial[m.Nombre] = m;
                        fila++;
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
            return Json(new { success = true, data = alertas.OrderByDescending(a => (int)((dynamic)a).score).ToList() });
        }

        [HttpGet]
        public IActionResult ObtenerReporteGlobal()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            if (!System.IO.File.Exists(filePath)) return Json(new { success = false });

            var listaRiesgos = new List<ReporteItem>();
            var listaRezagos = new List<ReporteItem>();
            var listaSeriadas = new List<ReporteItem>();
            var candados = new List<string> { "Lengua I", "Cálculo I", "Microeconomía I", "Microeconomía II", "Microeconomía III", "Estadística", "Economía mexicana I", "Macroeconomía I", "Macroeconomía II", "Econometría I" };

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    if (worksheet.Cell(3, 2).GetString() != "Activo") continue;

                    var materiasVigentes = new Dictionary<string, MateriaCursada>();
                    int fila = 6;
                    while (!string.IsNullOrWhiteSpace(worksheet.Cell(fila, 1).GetString()))
                    {
                        bool.TryParse(worksheet.Cell(fila, 7).GetString(), out bool riesgo);
                        bool.TryParse(worksheet.Cell(fila, 8).GetString(), out bool rezago);
                        materiasVigentes[worksheet.Cell(fila, 1).GetString()] = new MateriaCursada { Nombre = worksheet.Cell(fila, 1).GetString(), Estado = worksheet.Cell(fila, 6).GetString()?.ToLower(), EnRiesgo = riesgo, EnRezago = rezago };
                        fila++;
                    }

                    string mat = worksheet.Cell(1, 2).GetString(), nom = worksheet.Cell(2, 2).GetString();
                    foreach (var m in materiasVigentes.Values)
                    {
                        if (m.EnRiesgo) listaRiesgos.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
                        if (m.EnRezago) listaRezagos.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
                        if (candados.Contains(m.Nombre) && (m.Estado == "reprobada" || m.EnRezago)) listaSeriadas.Add(new ReporteItem { Matricula = mat, Nombre = nom, Materia = m.Nombre });
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

        private string SanitizarNombreHoja(string nombre) { if (string.IsNullOrWhiteSpace(nombre)) return "SinNombre"; foreach (char c in new[] { '\\', '/', '?', '*', '[', ']', ':' }) nombre = nombre.Replace(c.ToString(), ""); return nombre.Length > 31 ? nombre.Substring(0, 31) : nombre; }
    }
}