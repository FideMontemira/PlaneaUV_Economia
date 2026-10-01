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
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ExportarExcel([FromBody] AlumnoAvance avance)
        {
            if (avance == null || string.IsNullOrEmpty(avance.Matricula))
            {
                return BadRequest("Datos del alumno incompletos.");
            }

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");

            using (var workbook = System.IO.File.Exists(filePath) ? new XLWorkbook(filePath) : new XLWorkbook())
            {
                IXLWorksheet worksheet;

                if (workbook.Worksheets.Contains(avance.Matricula))
                {
                    worksheet = workbook.Worksheet(avance.Matricula);
                    worksheet.Clear();
                }
                else
                {
                    worksheet = workbook.Worksheets.Add(avance.Matricula);
                }

                // Datos del alumno
                worksheet.Cell(1, 1).Value = "Matrícula:";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 2).Value = avance.Matricula;

                worksheet.Cell(2, 1).Value = "Nombre:";
                worksheet.Cell(2, 1).Style.Font.Bold = true;
                worksheet.Cell(2, 2).Value = avance.Nombre;

                // GUARDAR DATOS DE TUTORÍAS EN COLUMNAS H a K
                worksheet.Cell(1, 8).Value = "TUTORÍAS REGISTRADAS";
                worksheet.Cell(1, 8).Style.Font.Bold = true;

                worksheet.Cell(2, 8).Value = "Sesión 1";
                worksheet.Cell(2, 9).Value = avance.FechaTutoria1;
                worksheet.Cell(2, 10).Value = avance.AsistenciaTutoria1;
                worksheet.Cell(2, 11).Value = avance.Tutoria1;

                worksheet.Cell(3, 8).Value = "Sesión 2";
                worksheet.Cell(3, 9).Value = avance.FechaTutoria2;
                worksheet.Cell(3, 10).Value = avance.AsistenciaTutoria2;
                worksheet.Cell(3, 11).Value = avance.Tutoria2;

                worksheet.Cell(4, 8).Value = "Sesión 3";
                worksheet.Cell(4, 9).Value = avance.FechaTutoria3;
                worksheet.Cell(4, 10).Value = avance.AsistenciaTutoria3;
                worksheet.Cell(4, 11).Value = avance.Tutoria3;

                // Encabezados de materias
                worksheet.Cell(5, 1).Value = "Materia";
                worksheet.Cell(5, 2).Value = "Créditos";
                worksheet.Cell(5, 3).Value = "Inscripción";
                worksheet.Cell(5, 4).Value = "Último Examen";
                worksheet.Cell(5, 5).Value = "Estado";
                worksheet.Cell(5, 6).Value = "En Riesgo";
                worksheet.Cell(5, 7).Value = "En Rezago";

                var rangoEncabezados = worksheet.Range("A5:G5");
                rangoEncabezados.Style.Font.Bold = true;
                rangoEncabezados.Style.Fill.BackgroundColor = XLColor.LightGray;

                int row = 6;
                int creditosTotales = 0;

                foreach (var materia in avance.Materias)
                {
                    worksheet.Cell(row, 1).Value = materia.Nombre;
                    worksheet.Cell(row, 2).Value = materia.Creditos;
                    worksheet.Cell(row, 3).Value = materia.TipoInscripcion;
                    worksheet.Cell(row, 4).Value = materia.UltimoExamen;
                    worksheet.Cell(row, 5).Value = materia.Estado;
                    worksheet.Cell(row, 6).Value = materia.EnRiesgo.ToString();
                    worksheet.Cell(row, 7).Value = materia.EnRezago.ToString();

                    if (materia.Estado == "aprobada")
                    {
                        creditosTotales += materia.Creditos;
                    }
                    row++;
                }

                worksheet.Cell(3, 4).Value = "Créditos Totales Aprobados:";
                worksheet.Cell(3, 4).Style.Font.Bold = true;
                worksheet.Cell(3, 5).Value = creditosTotales;

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }

            return Json(new { success = true, message = "Datos guardados con éxito." });
        }

        [HttpGet]
        public IActionResult BuscarAlumnoExcel(string matricula)
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");

            if (!System.IO.File.Exists(filePath))
                return Json(new { success = false, message = "No hay registros en el sistema." });

            using (var workbook = new XLWorkbook(filePath))
            {
                if (!workbook.Worksheets.Contains(matricula))
                    return Json(new { success = false, message = "Alumno no encontrado." });

                var worksheet = workbook.Worksheet(matricula);

                var avance = new AlumnoAvance
                {
                    Matricula = matricula,
                    Nombre = worksheet.Cell(2, 2).GetString(),
                    // LEER DATOS DE TUTORÍAS DESDE EL EXCEL
                    FechaTutoria1 = worksheet.Cell(2, 9).GetString(),
                    AsistenciaTutoria1 = worksheet.Cell(2, 10).GetString(),
                    Tutoria1 = worksheet.Cell(2, 11).GetString(),

                    FechaTutoria2 = worksheet.Cell(3, 9).GetString(),
                    AsistenciaTutoria2 = worksheet.Cell(3, 10).GetString(),
                    Tutoria2 = worksheet.Cell(3, 11).GetString(),

                    FechaTutoria3 = worksheet.Cell(4, 9).GetString(),
                    AsistenciaTutoria3 = worksheet.Cell(4, 10).GetString(),
                    Tutoria3 = worksheet.Cell(4, 11).GetString()
                };

                int row = 6;
                while (!worksheet.Cell(row, 1).IsEmpty())
                {
                    bool.TryParse(worksheet.Cell(row, 6).GetString(), out bool riesgo);
                    bool.TryParse(worksheet.Cell(row, 7).GetString(), out bool rezago);

                    avance.Materias.Add(new MateriaCursada
                    {
                        Nombre = worksheet.Cell(row, 1).GetString(),
                        Creditos = worksheet.Cell(row, 2).GetValue<int>(),
                        TipoInscripcion = worksheet.Cell(row, 3).GetString(),
                        UltimoExamen = worksheet.Cell(row, 4).GetString(),
                        Estado = worksheet.Cell(row, 5).GetString(),
                        EnRiesgo = riesgo,
                        EnRezago = rezago
                    });
                    row++;
                }

                return Json(new { success = true, data = avance });
            }
        }

        // NUEVO MÉTODO PARA EXPORTAR EL WORD
        [HttpGet]
        public IActionResult DescargarWordTutorias(string matricula)
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula inválida");

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            if (!System.IO.File.Exists(filePath)) return NotFound("No hay datos registrados.");

            using (var workbook = new XLWorkbook(filePath))
            {
                if (!workbook.Worksheets.Contains(matricula)) return NotFound("Alumno no encontrado.");

                var ws = workbook.Worksheet(matricula);
                var nombre = ws.Cell(2, 2).GetString();

                // Formato de Asistencia para visualización limpia
                string Asist(string val) => val == "asistio" ? "Asistió" : val == "falto" ? "Faltó" : val == "reprogramada" ? "Reprogramada" : "Pendiente";

                // Este es un truco nativo. MS Word entiende el HTML si se lo enviamos estructurado y con extensión .doc
                string html = $@"
                <html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
                <head>
                    <meta charset='utf-8'>
                    <title>Reporte de Tutorías</title>
                    <style>
                        body {{ font-family: 'Calibri', sans-serif; font-size: 12pt; }}
                        table {{ border-collapse: collapse; width: 100%; margin-bottom: 25px; }}
                        th, td {{ border: 1px solid #000; padding: 8px; text-align: left; vertical-align: top; }}
                        th {{ background-color: #f2f2f2; font-weight: bold; width: 20%; }}
                        h2, h3 {{ text-align: center; margin: 0; }}
                        .session-title {{ background-color: #17a2b8; color: white; padding: 5px; font-weight: bold; margin-bottom: 0; border: 1px solid #000; border-bottom: none; }}
                    </style>
                </head>
                <body>
                    <br/>
                    <h2>Evidencia de Tutoría Académica</h2>
                    <h3>Licenciatura en Economía (Plan 2024)</h3>
                    <hr/>
                    <p><strong>Alumno:</strong> {nombre}</p>
                    <p><strong>Matrícula:</strong> {matricula}</p>
                    <p><strong>Fecha de Expedición:</strong> {System.DateTime.Now.ToString("dd/MM/yyyy")}</p>
                    <br/>

                    <div class='session-title'>Primera Sesión</div>
                    <table>
                        <tr><th>Fecha:</th><td>{ws.Cell(2, 9).GetString()}</td><th>Asistencia:</th><td>{Asist(ws.Cell(2, 10).GetString())}</td></tr>
                        <tr><th colspan='4'>Comentarios y Acuerdos:</th></tr>
                        <tr><td colspan='4' style='height: 80px;'>{ws.Cell(2, 11).GetString()}</td></tr>
                    </table>

                    <div class='session-title'>Segunda Sesión</div>
                    <table>
                        <tr><th>Fecha:</th><td>{ws.Cell(3, 9).GetString()}</td><th>Asistencia:</th><td>{Asist(ws.Cell(3, 10).GetString())}</td></tr>
                        <tr><th colspan='4'>Comentarios y Acuerdos:</th></tr>
                        <tr><td colspan='4' style='height: 80px;'>{ws.Cell(3, 11).GetString()}</td></tr>
                    </table>

                    <div class='session-title'>Tercera Sesión</div>
                    <table>
                        <tr><th>Fecha:</th><td>{ws.Cell(4, 9).GetString()}</td><th>Asistencia:</th><td>{Asist(ws.Cell(4, 10).GetString())}</td></tr>
                        <tr><th colspan='4'>Comentarios y Acuerdos:</th></tr>
                        <tr><td colspan='4' style='height: 80px;'>{ws.Cell(4, 11).GetString()}</td></tr>
                    </table>
                    
                    <br/><br/>
                    <table style='border: none; width: 100%; text-align: center; margin-top: 50px;'>
                        <tr style='border: none;'>
                            <td style='border: none; width: 50%;'>___________________________________<br/>Firma del Tutor</td>
                            <td style='border: none; width: 50%;'>___________________________________<br/>Firma del Estudiante</td>
                        </tr>
                    </table>
                </body>
                </html>";

                // Retornar como archivo descargable Word (.doc)
                var bytes = Encoding.UTF8.GetBytes(html);
                return File(bytes, "application/msword", $"Tutorias_{matricula}.doc");
            }
        }

        [HttpGet]
        public IActionResult ObtenerAlumnosRegistrados()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");
            var listaAlumnos = new List<object>();
            if (System.IO.File.Exists(filePath))
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        var matricula = worksheet.Name;
                        var nombre = worksheet.Cell(2, 2).GetString();
                        listaAlumnos.Add(new { Matricula = matricula, Nombre = nombre });
                    }
                }
            }
            return Json(listaAlumnos);
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
                    var matricula = worksheet.Name;
                    var nombreAlumno = worksheet.Cell(2, 2).GetString();

                    int fila = 6;
                    while (!string.IsNullOrWhiteSpace(worksheet.Cell(fila, 1).GetString()))
                    {
                        var materia = worksheet.Cell(fila, 1).GetString();
                        var inscripcion = worksheet.Cell(fila, 3).GetString()?.ToLower();
                        var examen = worksheet.Cell(fila, 4).GetString()?.ToLower();
                        var estado = worksheet.Cell(fila, 5).GetString()?.ToLower();

                        bool.TryParse(worksheet.Cell(fila, 6).GetString(), out bool enRiesgo);

                        int nivelRiesgo = 0;
                        string mensajeAlerta = "";
                        string badgeClass = "";

                        if (inscripcion == "segunda" && examen == "ultima" && estado == "reprobada")
                        {
                            nivelRiesgo = 100;
                            mensajeAlerta = "Candidato a Baja (Reprobó Última Op.)";
                            badgeClass = "bg-danger";
                        }
                        else if (inscripcion == "segunda" && estado != "aprobada")
                        {
                            nivelRiesgo = 80;
                            mensajeAlerta = "Cursando Segunda Inscripción";
                            badgeClass = "bg-danger";
                        }
                        else if (enRiesgo)
                        {
                            nivelRiesgo = 60;
                            mensajeAlerta = "Materia en Riesgo Manual";
                            badgeClass = "bg-warning text-dark";
                        }
                        else if (estado == "reprobada" && inscripcion == "primera")
                        {
                            nivelRiesgo = 40;
                            mensajeAlerta = $"Reprobada en 1ª Insc. ({examen})";
                            badgeClass = "bg-warning text-dark";
                        }

                        if (nivelRiesgo > 0)
                        {
                            alertas.Add(new { matricula = matricula, nombre = nombreAlumno, materia = materia, mensaje = mensajeAlerta, badge = badgeClass, score = nivelRiesgo });
                        }
                        fila++;
                    }
                }
            }
            var alertasOrdenadas = alertas.OrderByDescending(a => (int)((dynamic)a).score).ToList();
            return Json(new { success = true, data = alertasOrdenadas });
        }

        [HttpGet]
        public IActionResult ObtenerReporteGlobal()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "AvanceAlumnos.xlsx");

            if (!System.IO.File.Exists(filePath)) return Json(new { success = false });

            var listaRiesgos = new List<ReporteItem>();
            var listaRezagos = new List<ReporteItem>();
            var listaSeriadas = new List<ReporteItem>();

            var materiasPreRequisitoEconomia = new List<string> {
                "Lengua I", "Cálculo I", "Microeconomía I", "Microeconomía II",
                "Microeconomía III", "Probabilidad", "Estadística", "Economía mexicana I",
                "Macroeconomía I", "Macroeconomía II", "Econometría I", "Organización industrial"
            };

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    string matricula = worksheet.Name;
                    string nombre = worksheet.Cell(2, 2).GetString();

                    int fila = 6;
                    while (!string.IsNullOrWhiteSpace(worksheet.Cell(fila, 1).GetString()))
                    {
                        var materia = worksheet.Cell(fila, 1).GetString();
                        var estado = worksheet.Cell(fila, 5).GetString()?.ToLower();
                        bool.TryParse(worksheet.Cell(fila, 6).GetString(), out bool enRiesgo);
                        bool.TryParse(worksheet.Cell(fila, 7).GetString(), out bool enRezago);

                        if (enRiesgo) listaRiesgos.Add(new ReporteItem { Matricula = matricula, Nombre = nombre, Materia = materia });
                        if (enRezago) listaRezagos.Add(new ReporteItem { Matricula = matricula, Nombre = nombre, Materia = materia });

                        if (materiasPreRequisitoEconomia.Contains(materia) && (estado == "reprobada" || enRezago))
                        {
                            if (!listaSeriadas.Any(x => x.Matricula == matricula && x.Materia == materia))
                            {
                                listaSeriadas.Add(new ReporteItem { Matricula = matricula, Nombre = nombre, Materia = materia });
                            }
                        }
                        fila++;
                    }
                }
            }

            var jsonResult = new
            {
                success = true,
                data = new
                {
                    riesgos = listaRiesgos.GroupBy(x => x.Materia).Select(g => new { materia = g.Key, alumnos = g.Select(a => new { nombre = a.Nombre, matricula = a.Matricula }) }),
                    rezagos = listaRezagos.GroupBy(x => x.Materia).Select(g => new { materia = g.Key, alumnos = g.Select(a => new { nombre = a.Nombre, matricula = a.Matricula }) }),
                    seriadas = listaSeriadas.GroupBy(x => x.Materia).Select(g => new { materia = g.Key, alumnos = g.Select(a => new { nombre = a.Nombre, matricula = a.Matricula }) })
                }
            };
            return Json(jsonResult);
        }

       
    }
}