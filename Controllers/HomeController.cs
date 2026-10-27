using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Controllers
{
    // El controlador solo recibe las peticiones de la pantalla y responde. Los datos los maneja el
    // repositorio (hoy Excel, mañana base de datos) y las reglas viven en ReglasTrayectoria y los servicios.
    public class HomeController : Controller
    {
        private const string MensajeArchivoOcupado = "No se pudo acceder a AvanceAlumnos.xlsx. Si lo tienes abierto en Excel, ciérralo; los cambios pendientes se guardarán solos.";

        private readonly IRepositorioAlumnos _alumnos;
        private readonly IRepositorioPlanes _planes;
        private readonly ServicioAlertas _alertas;
        private readonly ServicioReportes _reportes;
        private readonly ServicioReportePeriodo _reportePeriodo;

        public HomeController(IRepositorioAlumnos alumnos, IRepositorioPlanes planes, ServicioAlertas alertas, ServicioReportes reportes, ServicioReportePeriodo reportePeriodo)
        {
            _alumnos = alumnos;
            _planes = planes;
            _alertas = alertas;
            _reportes = reportes;
            _reportePeriodo = reportePeriodo;
        }

        public IActionResult Index() { return View(); }

        // Pantalla de tutoría de un alumno (antes era una ventana emergente dentro del mapa)
        [HttpGet]
        public IActionResult Tutoria(string? matricula, string? periodo)
        {
            ViewData["Matricula"] = matricula ?? "";
            ViewData["Periodo"] = periodo ?? "";
            return View();
        }

        // Convierte los errores al leer o guardar los datos (p. ej. el Excel abierto en Excel) en una
        // respuesta 409 con un mensaje claro; la pantalla lo muestra y reintenta el guardado sola.
        private IActionResult ConDatosDeAlumnos(Func<IActionResult> accion)
        {
            try { return accion(); }
            catch (IOException) { return StatusCode(409, new { success = false, message = MensajeArchivoOcupado }); }
            // Datos no válidos (matrícula repetida o con formato incorrecto): es un error del usuario, no se reintenta
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { return BadRequest(new { success = false, message = ex.Message }); }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error con los datos de los alumnos: " + ex);
                return StatusCode(500, new { success = false, message = "No se pudieron procesar los datos de los alumnos. Si continúa, cierra y vuelve a abrir PlaneaUV." });
            }
        }

        // =========================================================================
        // ALUMNOS
        // =========================================================================

        [HttpPost]
        public IActionResult ExportarExcel([FromBody] AlumnoAvance avance) => ConDatosDeAlumnos(() =>
        {
            if (avance == null) return BadRequest(new { success = false, message = "Datos incompletos." });
            var error = ValidacionAlumno.Errores(avance);
            if (error != null) return BadRequest(new { success = false, message = error });
            // Las tutorías ya no se editan en el mapa sino en su propia pantalla: se conservan las guardadas,
            // así el guardado del mapa nunca borra una sesión capturada en la otra pantalla.
            _alumnos.Modificar(avance.Matricula!, existente =>
            {
                avance.Tutorias = existente?.Tutorias ?? new List<TutoriaRecord>();
                return avance;
            });
            return Json(new { success = true });
        });

        // Guarda SOLO las sesiones de tutoría de un periodo (las de otros periodos y las materias no se tocan)
        [HttpPost]
        public IActionResult GuardarTutorias([FromBody] SolicitudTutorias solicitud) => ConDatosDeAlumnos(() =>
        {
            if (solicitud == null || string.IsNullOrWhiteSpace(solicitud.Matricula)) return BadRequest(new { success = false, message = "Datos incompletos." });
            if (!ReglasTrayectoria.EsPeriodoValido(solicitud.Periodo)) return BadRequest(new { success = false, message = "El periodo no es válido." });
            var sesiones = (solicitud.Sesiones ?? new List<TutoriaRecord>())
                .Select(t => { t.Periodo = solicitud.Periodo; ValidacionAlumno.Normalizar(t); return t; })   // valores desconocidos fuera
                .Where(GeneradorDocumentoTutorias.TieneInformacion)
                .ToList();
            if (sesiones.Select(t => t.Sesion).Distinct().Count() != sesiones.Count || sesiones.Any(t => t.Sesion < 1 || t.Sesion > 10))
                return BadRequest(new { success = false, message = "Las sesiones no son válidas." });

            _alumnos.Modificar(solicitud.Matricula, alumno =>
            {
                if (alumno == null) throw new InvalidOperationException("El alumno ya no existe.");
                alumno.Tutorias = alumno.Tutorias.Where(t => t.Periodo != solicitud.Periodo).Concat(sesiones).ToList();
                var error = ValidacionAlumno.Errores(alumno);
                if (error != null) throw new ArgumentException(error);
                return alumno;
            });
            return Json(new { success = true });
        });

        [HttpGet]
        public IActionResult BuscarAlumnoExcel(string matricula) => ConDatosDeAlumnos(() =>
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");
            if (!_alumnos.HayRegistros) return Json(new { success = false, message = "No hay registros." });
            var alumno = _alumnos.Buscar(matricula);
            return alumno == null ? Json(new { success = false, message = "Alumno no encontrado." }) : Json(new { success = true, data = alumno });
        });

        [HttpPost]
        public IActionResult EliminarAlumno(string matricula) => ConDatosDeAlumnos(() =>
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");
            if (!_alumnos.HayRegistros) return Json(new { success = false, message = "No hay registros." });
            return _alumnos.Eliminar(matricula) ? Json(new { success = true }) : Json(new { success = false, message = "Alumno no encontrado." });
        });

        [HttpGet]
        public IActionResult ObtenerAlumnosRegistrados() => ConDatosDeAlumnos(() => Json(_alumnos.Listar()));

        // Situación del alumno en el Periodo de Trabajo: periodo que cursa (descontando pausas), si está en pausa,
        // duración del plan, materias en rezago y periodos desde su ingreso. La pantalla solo lo muestra.
        [HttpGet]
        public IActionResult ObtenerSituacionAlumno(string matricula, string? periodo) => ConDatosDeAlumnos(() =>
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");
            var alumno = _alumnos.Buscar(matricula);
            if (alumno == null) return Json(new { success = false, message = "Alumno no encontrado." });
            return Json(new { success = true, data = SituacionCompleta(alumno, periodo) });
        });

        // Situación del alumno con su semáforo y las materias críticas del grupo
        private SituacionAlumno SituacionCompleta(AlumnoAvance alumno, string? periodo)
        {
            var criticas = ReglasMateriasCriticas.Calcular(_alumnos.ObtenerTodos(), periodo);
            var situacion = ReglasTrayectoria.Calcular(alumno, ObtenerPlanSeguro(alumno.PlanEstudio), periodo);
            situacion.MateriasCriticas = criticas.Where(c => c.Critica).ToList();
            situacion.Semaforo = ReglasSemaforo.Calcular(alumno, periodo, _alertas.Calcular(new[] { alumno }, periodo), DateTime.Today, criticas);
            // Ya puede inscribir servicio social / experiencia recepcional / prácticas (solo informativo, alumnos activos)
            var aviso = ReglasTrayectoria.SenalRequisitosCumplidos(situacion.RequisitosCreditos);
            if (aviso != null && situacion.Semaforo.Sistema != "sin") situacion.Semaforo.Senales.Add(aviso);
            return situacion;
        }

        // Ficha para llevar a la sesión de tutoría (una hoja para imprimir o guardar en PDF)
        [HttpGet]
        public IActionResult FichaTutoria(string matricula, string? periodo) => ConDatosDeAlumnos(() =>
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");
            var alumno = _alumnos.Buscar(matricula);
            if (alumno == null) return NotFound("Alumno no encontrado.");
            var plan = ObtenerPlanSeguro(alumno.PlanEstudio);
            string html = GeneradorFichaTutoria.Generar(alumno, plan, SituacionCompleta(alumno, periodo), periodo ?? "", DateTime.Today);
            return Content(html, "text/html; charset=utf-8");
        });

        // Reporte de seguimiento del alumno: el cierre de un periodo o, con historial=true, todos sus periodos
        // hasta el indicado (para conservar el seguimiento semestre por semestre)
        [HttpGet]
        public IActionResult ReportePeriodo(string matricula, string? periodo, bool historial = false) => ConDatosDeAlumnos(() =>
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula requerida.");
            if (!ReglasTrayectoria.EsPeriodoValido(periodo)) return BadRequest("El periodo no es válido.");
            var alumno = _alumnos.Buscar(matricula);
            if (alumno == null) return NotFound("Alumno no encontrado.");

            var periodos = historial ? _reportePeriodo.PeriodosDelHistorial(alumno, periodo!) : new List<string> { periodo! };
            if (periodos.Count == 0) periodos.Add(periodo!);
            var entrada = EntradaDeReporte(alumno, periodos);
            string titulo = (historial ? "Historial de seguimiento · " : "Reporte del periodo · ") + alumno.Nombre;
            return Content(GeneradorReportePeriodo.Generar(new[] { entrada }, titulo, historial, DateTime.Today), "text/html; charset=utf-8");
        });

        // Cierre de periodo del grupo: el reporte del periodo de cada alumno activo, cada uno en hoja aparte
        [HttpGet]
        public IActionResult ReportesDelPeriodo(string? periodo) => ConDatosDeAlumnos(() =>
        {
            if (!ReglasTrayectoria.EsPeriodoValido(periodo)) return BadRequest("El periodo no es válido.");
            var entradas = _alumnos.ObtenerTodos()
                .Where(a => string.IsNullOrEmpty(a.Situacion) || a.Situacion == "Activo")
                .OrderBy(a => a.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .Select(a => EntradaDeReporte(a, new List<string> { periodo! }))
                .ToList();
            return Content(GeneradorReportePeriodo.Generar(entradas, "Reportes del periodo " + periodo, false, DateTime.Today), "text/html; charset=utf-8");
        });

        private GeneradorReportePeriodo.Entrada EntradaDeReporte(AlumnoAvance alumno, List<string> periodos)
        {
            string plan = (alumno.PlanEstudio ?? "").Trim();
            return new GeneradorReportePeriodo.Entrada
            {
                Alumno = alumno,
                Programa = ObtenerPlanSeguro(plan)?.Programa ?? plan.Replace('_', ' '),
                Periodos = periodos.Select(p => _reportePeriodo.Resumir(alumno, p, DateTime.Today)).ToList()
            };
        }

        [HttpGet]
        public IActionResult DescargarWordTutorias(string matricula) => ConDatosDeAlumnos(() =>
        {
            if (string.IsNullOrEmpty(matricula)) return BadRequest("Matrícula inválida");
            if (!_alumnos.HayRegistros) return NotFound("No hay datos.");
            var alumno = _alumnos.Buscar(matricula);
            if (alumno == null) return NotFound("Alumno no encontrado.");

            // El título lleva el programa del plan del alumno
            string planAlumno = (alumno.PlanEstudio ?? "").Trim();
            string programa = ObtenerPlanSeguro(planAlumno)?.Programa ?? planAlumno.Replace('_', ' ');
            string html = GeneradorDocumentoTutorias.Generar(alumno, programa);
            return File(Encoding.UTF8.GetBytes(html), "application/msword", $"Tutorias_{matricula}.doc");
        });

        // =========================================================================
        // ALERTAS Y REPORTES
        // 'periodo' es el Periodo de Trabajo elegido en pantalla; con él se sabe en qué periodo va cada alumno
        // =========================================================================

        [HttpGet]
        public IActionResult ObtenerReporteAlertas(string? periodo) => ConDatosDeAlumnos(() =>
            Json(new { success = true, data = _alertas.Calcular(_alumnos.ObtenerTodos(), periodo) }));

        [HttpGet]
        public IActionResult ObtenerReporteGlobal(string? periodo) => ConDatosDeAlumnos(() =>
            Json(new { success = true, data = _reportes.ReporteGlobal(_alumnos.ObtenerTodos(), periodo) }));

        [HttpGet]
        public IActionResult ObtenerPanelGrupo(string? periodo) => ConDatosDeAlumnos(() =>
            Json(new { success = true, data = _reportes.PanelGrupo(_alumnos.ObtenerTodos(), periodo) }));

        [HttpGet]
        public IActionResult ObtenerMateriasCriticas(string? periodo) => ConDatosDeAlumnos(() =>
            Json(new { success = true, data = ReglasMateriasCriticas.Calcular(_alumnos.ObtenerTodos(), periodo),
                       minimoIntentos = ReglasMateriasCriticas.MinimoIntentos, porcentajeCritico = ReglasMateriasCriticas.PorcentajeCritico }));

        // =========================================================================
        // PLANES DE ESTUDIO
        // =========================================================================

        [HttpPost]
        public async Task<IActionResult> SubirPlanEstudio(IFormFile archivoPlan)
        {
            if (archivoPlan == null || archivoPlan.Length == 0)
                return Json(new { success = false, message = "No se seleccionó ningún archivo." });

            if (!archivoPlan.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "El archivo debe ser un formato Excel válido (.xlsx)." });

            if (archivoPlan.Length > RepositorioPlanesExcel.TamanoMaximo)
                return Json(new { success = false, message = "El archivo es demasiado grande (máximo 5 MB)." });

            try
            {
                await _planes.GuardarAsync(archivoPlan);
                return Json(new { success = true, message = "Plan de estudios subido correctamente." });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });   // la plantilla no es válida: se explica por qué
            }
            catch (IOException)
            {
                return Json(new { success = false, message = "No se pudo guardar el plan. Si ese archivo está abierto en Excel, ciérralo e intenta de nuevo." });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error al subir un plan: " + ex);
                return Json(new { success = false, message = "No se pudo guardar el plan de estudios." });
            }
        }

        [HttpGet]
        public IActionResult ObtenerPlanesEstudio() => Json(_planes.Listar());

        // Opciones del formato guiado de tutoría (temas, situaciones, canalizaciones, modalidades)
        [HttpGet]
        public IActionResult ObtenerCatalogoTutorias() => Json(new
        {
            modalidades = CatalogoTutorias.Modalidades,
            temas = CatalogoTutorias.Temas,
            situaciones = CatalogoTutorias.Situaciones,
            problematicas = CatalogoTutorias.Problematicas,
            estadosCaso = CatalogoTutorias.EstadosCaso,
            canalizaciones = CatalogoTutorias.Canalizaciones
        });

        [HttpGet]
        public IActionResult ObtenerMapaCurricular(string planNombre)
        {
            if (string.IsNullOrEmpty(planNombre))
                return Json(new { success = false, message = "Nombre del plan no proporcionado." });

            if (!_planes.Existe(planNombre))
                return Json(new { success = false, message = "El plan de estudios no existe en el servidor." });

            try
            {
                var plan = _planes.Obtener(planNombre)!;
                return Json(new { success = true, data = plan.Materias, programa = plan.Programa, creditosTotales = plan.CreditosTotales,
                                  creditosMaterias = plan.Materias.Sum(m => m.Creditos), optativas = plan.Optativas });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error al leer un plan: " + ex);
                return Json(new { success = false, message = "No se pudo leer el plan: revisa que la plantilla tenga el formato correcto." });
            }
        }

        // Si el plan no existe o su archivo está dañado se trata como "sin plan"
        private PlanInfo? ObtenerPlanSeguro(string? planNombre)
        {
            try { return _planes.Obtener((planNombre ?? "").Trim()); } catch { return null; }
        }
    }
}
