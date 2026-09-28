using Microsoft.AspNetCore.Mvc;
using PlaneaUV_Economia.Models;
using ClosedXML.Excel;
using System.IO;
using System.Linq;

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

                worksheet.Cell(1, 1).Value = "Matrícula:";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 2).Value = avance.Matricula;

                worksheet.Cell(2, 1).Value = "Nombre:";
                worksheet.Cell(2, 1).Style.Font.Bold = true;
                worksheet.Cell(2, 2).Value = avance.Nombre;

                worksheet.Cell(4, 1).Value = "Materia";
                worksheet.Cell(4, 2).Value = "Créditos";
                worksheet.Cell(4, 3).Value = "Inscripción";
                worksheet.Cell(4, 4).Value = "Último Examen";
                worksheet.Cell(4, 5).Value = "Estado";

                var rangoEncabezados = worksheet.Range("A4:E4");
                rangoEncabezados.Style.Font.Bold = true;
                rangoEncabezados.Style.Fill.BackgroundColor = XLColor.LightGray;

                int row = 5;
                int creditosTotales = 0;

                foreach (var materia in avance.Materias)
                {
                    worksheet.Cell(row, 1).Value = materia.Nombre;
                    worksheet.Cell(row, 2).Value = materia.Creditos;
                    worksheet.Cell(row, 3).Value = materia.TipoInscripcion;
                    worksheet.Cell(row, 4).Value = materia.UltimoExamen;
                    worksheet.Cell(row, 5).Value = materia.Estado;

                    if (materia.Estado == "aprobada")
                    {
                        creditosTotales += materia.Creditos;
                    }
                    row++;
                }

                worksheet.Cell(2, 4).Value = "Créditos Totales Aprobados:";
                worksheet.Cell(2, 4).Style.Font.Bold = true;
                worksheet.Cell(2, 5).Value = creditosTotales;

                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }

            return Json(new
            {
                success = true,
                message = $"El avance se guardó correctamente en Excel.\nCréditos Totales Aprobados: {avance.Materias.Where(m => m.Estado == "aprobada").Sum(m => m.Creditos)}"
            });
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
                    Nombre = worksheet.Cell(2, 2).GetString()
                };

                int row = 5;
                while (!worksheet.Cell(row, 1).IsEmpty())
                {
                    avance.Materias.Add(new MateriaCursada
                    {
                        Nombre = worksheet.Cell(row, 1).GetString(),
                        Creditos = worksheet.Cell(row, 2).GetValue<int>(),
                        TipoInscripcion = worksheet.Cell(row, 3).GetString(),
                        UltimoExamen = worksheet.Cell(row, 4).GetString(),
                        Estado = worksheet.Cell(row, 5).GetString()
                    });
                    row++;
                }

                return Json(new { success = true, data = avance });
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
                    // Recorremos cada pestaña del Excel
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        var matricula = worksheet.Name;
                        // Leemos el nombre del alumno que siempre guardamos en la celda B2
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

            if (!System.IO.File.Exists(filePath))
                return Json(new { success = true, data = alertas });

            using (var workbook = new ClosedXML.Excel.XLWorkbook(filePath))
            {
                // Recorrer todos los alumnos (cada pestaña es un alumno)
                foreach (var worksheet in workbook.Worksheets)
                {
                    var matricula = worksheet.Name;
                    var nombreAlumno = worksheet.Cell(2, 2).GetString();

                    int fila = 5; // Asume que las materias empiezan en la fila 5
                    while (!string.IsNullOrWhiteSpace(worksheet.Cell(fila, 1).GetString()))
                    {
                        var materia = worksheet.Cell(fila, 1).GetString();
                        var inscripcion = worksheet.Cell(fila, 3).GetString()?.ToLower();
                        var examen = worksheet.Cell(fila, 4).GetString()?.ToLower();
                        var estado = worksheet.Cell(fila, 5).GetString()?.ToLower();

                        // Si la columna 6 está vacía, devuelve falso por defecto
                        bool enRiesgo = false;
                        bool.TryParse(worksheet.Cell(fila, 6).GetString(), out enRiesgo);

                        int nivelRiesgo = 0;
                        string mensajeAlerta = "";
                        string badgeClass = "";

                        // Reglas de negocio (Prioridad)
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
                            alertas.Add(new
                            {
                                matricula = matricula,
                                nombre = nombreAlumno,
                                materia = materia,
                                mensaje = mensajeAlerta,
                                badge = badgeClass,
                                score = nivelRiesgo
                            });
                        }
                        fila++;
                    }
                }
            }

            // Ordenar descendente (los puntajes más altos primero)
            var alertasOrdenadas = alertas.OrderByDescending(a => (int)((dynamic)a).score).ToList();
            return Json(new { success = true, data = alertasOrdenadas });
        }
    }
}