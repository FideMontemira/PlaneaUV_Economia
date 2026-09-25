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
    }
}