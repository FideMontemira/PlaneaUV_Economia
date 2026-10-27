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

        // Lee al alumno (null si no existe), aplica 'cambio' y guarda el resultado, todo sin que otra petición
        // se meta en medio. Así dos pantallas abiertas a la vez (mapa y tutorías) no se pisan los datos.
        void Modificar(string matricula, Func<AlumnoAvance?, AlumnoAvance> cambio);
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
                var ws = libro.Worksheets.Contains(hoja) ? libro.Worksheet(hoja) : null;
                string existente = ws?.Cell(1, 2).GetString().Trim() ?? "";
                bool esElMismoAlumno = ws != null && (existente == "" || string.Equals(existente, alumno.Matricula.Trim(), StringComparison.Ordinal));

                // Una matrícula con formato no válido se recortaría al nombrar la hoja y podría chocar con otra.
                // (Si ya existe exactamente esa matrícula se deja guardar, para no bloquear registros anteriores.)
                var errorMatricula = ValidacionAlumno.ErrorEnMatricula(alumno.Matricula);
                if (errorMatricula != null && !esElMismoAlumno) throw new ArgumentException(errorMatricula);

                // Excel no distingue mayúsculas en el nombre de la hoja: "S21..." y "s21..." serían la misma.
                // Si la hoja es de OTRO alumno, no se sobrescribe.
                if (ws != null && !esElMismoAlumno)
                    throw new InvalidOperationException($"Ya existe el alumno con matrícula {existente}. Revisa la matrícula (no puede diferir solo en mayúsculas).");

                if (ws != null) ws.Clear();
                else ws = libro.Worksheets.Add(hoja);

                EscribirHoja(ws, alumno);
                GuardarLibroSeguro(libro);
            }
        }

        public void Modificar(string matricula, Func<AlumnoAvance?, AlumnoAvance> cambio)
        {
            // El candado se puede volver a tomar dentro (Buscar y Guardar lo usan): nadie más entra en medio
            lock (_candado)
            {
                Guardar(cambio(Buscar(matricula)));
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
                    Comentarios = ws.Cell(fila, 14).GetString(),
                    // Formato guiado (columnas O-U). Los registros anteriores no las tienen y quedan vacías.
                    Modalidad = ws.Cell(fila, 15).GetString(),
                    Temas = Lista(ws.Cell(fila, 16).GetString()),
                    Situaciones = Lista(ws.Cell(fila, 17).GetString()),
                    Canalizaciones = Lista(ws.Cell(fila, 18).GetString()),
                    OtraCanalizacion = ws.Cell(fila, 19).GetString(),
                    Acuerdos = ws.Cell(fila, 20).GetString(),
                    RequiereSeguimiento = bool.TryParse(ws.Cell(fila, 21).GetString(), out bool seguimiento) && seguimiento,
                    // Seguimiento y valoración del riesgo (columnas V-AA)
                    MateriaDificultad = ws.Cell(fila, 22).GetString(),
                    FechaRevision = ws.Cell(fila, 23).GetString(),
                    // Los registros anteriores solo tenían "Requiere seguimiento": equivale a un caso abierto
                    EstadoCaso = ws.Cell(fila, 24).IsEmpty() && seguimiento ? "Abierto" : ws.Cell(fila, 24).GetString(),
                    ValoracionRiesgo = ws.Cell(fila, 25).GetString(),
                    MotivoValoracion = ws.Cell(fila, 26).GetString(),
                    SemaforoSistema = ws.Cell(fila, 27).GetString(),
                    AcudioCanalizaciones = Lista(ws.Cell(fila, 28).GetString())
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

            string[] encabezadoTutorias = { "Periodo Tutoria", "Sesión", "Fecha", "Asistencia", "Comentarios",
                                            "Modalidad", "Temas", "Situaciones", "Canalizaciones", "Otra canalización", "Acuerdos", "Seguimiento",
                                            "Materia con dificultad", "Fecha de revisión", "Estado del caso", "Valoración del riesgo", "Motivo de la valoración", "Semáforo del sistema",
                                            "¿Acudió a la canalización?" };
            for (int i = 0; i < encabezadoTutorias.Length; i++) ws.Cell(5, i + 10).Value = encabezadoTutorias[i];
            var rangoT = ws.Range("J5:AB5");
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
                ws.Cell(fila, 15).Value = t.Modalidad;
                ws.Cell(fila, 16).Value = Unir(t.Temas);
                ws.Cell(fila, 17).Value = Unir(t.Situaciones);
                ws.Cell(fila, 18).Value = Unir(t.Canalizaciones);
                ws.Cell(fila, 19).Value = ValidacionAlumno.SinSeparador(t.OtraCanalizacion);
                ws.Cell(fila, 20).Value = t.Acuerdos;
                ws.Cell(fila, 21).Value = t.RequiereSeguimiento.ToString();
                ws.Cell(fila, 22).Value = t.MateriaDificultad;
                ws.Cell(fila, 23).Value = t.FechaRevision;
                ws.Cell(fila, 24).Value = t.EstadoCaso;
                ws.Cell(fila, 25).Value = t.ValoracionRiesgo;
                ws.Cell(fila, 26).Value = t.MotivoValoracion;
                ws.Cell(fila, 27).Value = t.SemaforoSistema;
                ws.Cell(fila, 28).Value = Unir(t.AcudioCanalizaciones);
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

        // Varios valores en una celda, separados por " | " (un "|" dentro de un valor lo partiría: se cambia por "/")
        private static string Unir(List<string>? valores) =>
            string.Join(" | ", (valores ?? new List<string>()).Select(ValidacionAlumno.SinSeparador).Where(v => v != ""));

        // Celda con varios valores separados por "|" (temas, situaciones, canalizaciones)
        private static List<string> Lista(string texto) =>
            texto.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

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
