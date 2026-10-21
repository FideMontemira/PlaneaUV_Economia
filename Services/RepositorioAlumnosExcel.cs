using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Acceso a los datos de los alumnos. Es lo ÚNICO que sabe dónde y cómo se guardan:
    // para conectar la base de datos basta con escribir otra clase que implemente esta interfaz
    // y registrarla en Program.cs; controlador, reglas y reportes no cambian.
    public interface IRepositorioAlumnos
    {
        bool HayRegistros { get; }
        IReadOnlyList<AlumnoResumen> Listar();
        IReadOnlyList<AlumnoAvance> ObtenerTodos();
        AlumnoAvance? Buscar(string matricula);
        void Guardar(AlumnoAvance alumno);
        bool Eliminar(string matricula);   // false si el alumno no existe
    }

    // Implementación actual: AvanceAlumnos.xlsx, una hoja por alumno.
    //   Fila 1-4: matrícula, nombre, situación, plan (columna B); E1: periodos sin inscripción separados por " | "
    //   Desde la fila 6: materias en A-H y tutorías en J-N.
    // Los errores de acceso al archivo (p. ej. abierto en Excel) salen como IOException.
    public class RepositorioAlumnosExcel : IRepositorioAlumnos
    {
        // Todas las lecturas y escrituras pasan por este candado: un guardado nunca coincide con otra operación
        private static readonly object _candado = new object();
        private readonly string _ruta;

        public RepositorioAlumnosExcel(IWebHostEnvironment env) : this(Path.Combine(env.ContentRootPath, "AvanceAlumnos.xlsx")) { }

        public RepositorioAlumnosExcel(string rutaArchivo) { _ruta = rutaArchivo; }

        public bool HayRegistros => File.Exists(_ruta);

        public IReadOnlyList<AlumnoResumen> Listar()
        {
            lock (_candado)
            {
                if (!File.Exists(_ruta)) return new List<AlumnoResumen>();
                using var libro = new XLWorkbook(_ruta);
                return libro.Worksheets.Select(ws => new AlumnoResumen
                {
                    Matricula = ws.Cell(1, 2).GetString(),
                    Nombre = ws.Cell(2, 2).GetString(),
                    Situacion = ws.Cell(3, 2).GetString()
                }).ToList();
            }
        }

        public IReadOnlyList<AlumnoAvance> ObtenerTodos()
        {
            lock (_candado)
            {
                if (!File.Exists(_ruta)) return new List<AlumnoAvance>();
                using var libro = new XLWorkbook(_ruta);
                return libro.Worksheets.Select(LeerHoja).ToList();
            }
        }

        public AlumnoAvance? Buscar(string matricula)
        {
            lock (_candado)
            {
                if (!File.Exists(_ruta)) return null;
                using var libro = new XLWorkbook(_ruta);
                string hoja = NombreDeHoja(matricula);
                return libro.Worksheets.Contains(hoja) ? LeerHoja(libro.Worksheet(hoja)) : null;
            }
        }

        public void Guardar(AlumnoAvance alumno)
        {
            if (string.IsNullOrEmpty(alumno.Matricula)) throw new ArgumentException("La matrícula es obligatoria.");
            lock (_candado)
            {
                using var libro = File.Exists(_ruta) ? new XLWorkbook(_ruta) : new XLWorkbook();
                string hoja = NombreDeHoja(alumno.Matricula);
                IXLWorksheet ws;
                if (libro.Worksheets.Contains(hoja)) { ws = libro.Worksheet(hoja); ws.Clear(); }
                else ws = libro.Worksheets.Add(hoja);

                EscribirHoja(ws, alumno);
                GuardarLibroSeguro(libro);
            }
        }

        public bool Eliminar(string matricula)
        {
            lock (_candado)
            {
                if (!File.Exists(_ruta)) return false;
                bool eraUltimaHoja;
                using (var libro = new XLWorkbook(_ruta))
                {
                    string hoja = NombreDeHoja(matricula);
                    if (!libro.Worksheets.Contains(hoja)) return false;

                    // Excel no permite un libro sin hojas: si es el último alumno se borra el archivo completo
                    eraUltimaHoja = libro.Worksheets.Count == 1;
                    if (!eraUltimaHoja)
                    {
                        libro.Worksheet(hoja).Delete();
                        GuardarLibroSeguro(libro);
                    }
                }
                if (eraUltimaHoja)
                {
                    // Se conserva como respaldo antes de borrarlo
                    File.Copy(_ruta, RutaHermana("AvanceAlumnos.respaldo.xlsx"), true);
                    File.Delete(_ruta);
                }
                return true;
            }
        }

        // ---------------------------------------------------------------------------------

        private static AlumnoAvance LeerHoja(IXLWorksheet ws)
        {
            var alumno = new AlumnoAvance
            {
                Matricula = ws.Cell(1, 2).GetString(),
                Nombre = ws.Cell(2, 2).GetString(),
                Situacion = ws.Cell(3, 2).GetString(),
                PlanEstudio = ws.Cell(4, 2).GetString(),
                PeriodosSinInscripcion = ws.Cell(1, 5).GetString().Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            };

            for (int fila = 6; !ws.Cell(fila, 1).IsEmpty(); fila++)
            {
                bool.TryParse(ws.Cell(fila, 7).GetString(), out bool riesgo);
                bool.TryParse(ws.Cell(fila, 8).GetString(), out bool rezago);
                int.TryParse(ws.Cell(fila, 3).GetString(), out int creditos);
                alumno.Materias.Add(new MateriaCursada
                {
                    Nombre = ws.Cell(fila, 1).GetString(),
                    Periodo = ws.Cell(fila, 2).GetString(),
                    Creditos = creditos,
                    TipoInscripcion = ws.Cell(fila, 4).GetString(),
                    UltimoExamen = ws.Cell(fila, 5).GetString(),
                    Estado = ws.Cell(fila, 6).GetString(),
                    EnRiesgo = riesgo,
                    EnRezago = rezago
                });
            }

            for (int fila = 6; !ws.Cell(fila, 10).IsEmpty(); fila++)
            {
                int.TryParse(ws.Cell(fila, 11).GetString(), out int sesion);
                alumno.Tutorias.Add(new TutoriaRecord
                {
                    Periodo = ws.Cell(fila, 10).GetString(),
                    Sesion = sesion,
                    Fecha = ws.Cell(fila, 12).GetString(),
                    Asistencia = ws.Cell(fila, 13).GetString(),
                    Comentarios = ws.Cell(fila, 14).GetString()
                });
            }
            return alumno;
        }

        private static void EscribirHoja(IXLWorksheet ws, AlumnoAvance a)
        {
            void Dato(int fila, int col, string etiqueta, XLCellValue valor)
            {
                ws.Cell(fila, col).Value = etiqueta;
                ws.Cell(fila, col).Style.Font.Bold = true;
                ws.Cell(fila, col + 1).Value = valor;
            }

            Dato(1, 1, "Matrícula:", a.Matricula);
            Dato(2, 1, "Nombre:", a.Nombre);
            Dato(3, 1, "Situación:", string.IsNullOrEmpty(a.Situacion) ? "Activo" : a.Situacion);
            Dato(4, 1, "Plan de Estudios:", string.IsNullOrEmpty(a.PlanEstudio) ? "Generico" : a.PlanEstudio);
            Dato(1, 4, "Periodos sin inscripción:", string.Join(" | ", (a.PeriodosSinInscripcion ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct()));

            string[] encabezadoMaterias = { "Materia", "Periodo", "Créditos", "Inscripción", "Examen", "Estado", "Riesgo", "Rezago" };
            for (int i = 0; i < encabezadoMaterias.Length; i++) ws.Cell(5, i + 1).Value = encabezadoMaterias[i];
            var rangoM = ws.Range("A5:H5");
            rangoM.Style.Font.Bold = true;
            rangoM.Style.Fill.BackgroundColor = XLColor.LightGray;

            int fila = 6;
            foreach (var m in a.Materias)
            {
                ws.Cell(fila, 1).Value = m.Nombre;
                ws.Cell(fila, 2).Value = m.Periodo;
                ws.Cell(fila, 3).Value = m.Creditos;
                ws.Cell(fila, 4).Value = m.TipoInscripcion;
                ws.Cell(fila, 5).Value = m.UltimoExamen;
                ws.Cell(fila, 6).Value = m.Estado;
                ws.Cell(fila, 7).Value = m.EnRiesgo.ToString();
                ws.Cell(fila, 8).Value = m.EnRezago.ToString();
                fila++;
            }

            string[] encabezadoTutorias = { "Periodo Tutoria", "Sesión", "Fecha", "Asistencia", "Comentarios" };
            for (int i = 0; i < encabezadoTutorias.Length; i++) ws.Cell(5, i + 10).Value = encabezadoTutorias[i];
            var rangoT = ws.Range("J5:N5");
            rangoT.Style.Font.Bold = true;
            rangoT.Style.Fill.BackgroundColor = XLColor.LightCyan;

            fila = 6;
            foreach (var t in a.Tutorias)
            {
                ws.Cell(fila, 10).Value = t.Periodo;
                ws.Cell(fila, 11).Value = t.Sesion;
                ws.Cell(fila, 12).Value = t.Fecha;
                ws.Cell(fila, 13).Value = t.Asistencia;
                ws.Cell(fila, 14).Value = t.Comentarios;
                fila++;
            }

            ws.Columns().AdjustToContents();
        }

        // Guarda primero en un archivo temporal y luego lo intercambia por el original, dejando la versión
        // anterior como AvanceAlumnos.respaldo.xlsx. Si el programa se cierra a mitad del guardado,
        // el archivo original no queda dañado.
        private void GuardarLibroSeguro(XLWorkbook libro)
        {
            string temporal = RutaHermana("AvanceAlumnos.guardando.xlsx");
            libro.SaveAs(temporal);
            if (File.Exists(_ruta)) File.Replace(temporal, _ruta, RutaHermana("AvanceAlumnos.respaldo.xlsx"));
            else File.Move(temporal, _ruta);
        }

        private string RutaHermana(string nombre) => Path.Combine(Path.GetDirectoryName(_ruta)!, nombre);

        // Excel no permite ciertos caracteres ni más de 31 en el nombre de una hoja
        private static string NombreDeHoja(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "SinNombre";
            foreach (char c in new[] { '\\', '/', '?', '*', '[', ']', ':' }) nombre = nombre.Replace(c.ToString(), "");
            return nombre.Length > 31 ? nombre.Substring(0, 31) : nombre;
        }
    }
}
