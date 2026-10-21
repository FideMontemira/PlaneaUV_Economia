using System;
using System.Collections.Generic;
using System.Linq;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Calcula las alertas de los alumnos activos. No lee archivos: recibe los alumnos y consulta
    // los planes por medio del repositorio (en las pruebas se le pasa uno falso en memoria).
    public class ServicioAlertas
    {
        private readonly IRepositorioPlanes _planes;

        public ServicioAlertas(IRepositorioPlanes planes) { _planes = planes; }

        // 'periodo' es el Periodo de Trabajo elegido en pantalla; con él se sabe en qué periodo va cada alumno.
        // Regresa las alertas de la más grave a la menos grave.
        public List<Alerta> Calcular(IEnumerable<AlumnoAvance> alumnos, string? periodo)
        {
            var alertas = new List<Alerta>();

            foreach (var alumno in alumnos)
            {
                var situacion = alumno.Situacion ?? "";
                if (situacion != "Activo" && situacion != "") continue;

                string matricula = alumno.Matricula ?? "", nombre = alumno.Nombre ?? "";

                // Último intento registrado de cada materia (las claves conservan el orden en que aparecieron)
                var ultimoIntento = new Dictionary<string, MateriaCursada>();
                foreach (var m in alumno.Materias) ultimoIntento[m.Nombre ?? ""] = m;

                var aprobadas = ReglasTrayectoria.MateriasAprobadas(alumno);
                bool tieneRegistroEnPeriodo = !string.IsNullOrEmpty(periodo) && alumno.Materias.Any(m => m.Periodo == periodo);
                var pausas = alumno.PeriodosSinInscripcion ?? new List<string>();
                // Si en el periodo de trabajo el alumno está en pausa, no se le exige tener materias registradas
                bool enPausaEstePeriodo = !string.IsNullOrEmpty(periodo) && pausas.Contains(periodo);

                // ---------- Alertas que dependen del periodo que cursa el alumno ----------
                int? periodoQueCursa = ReglasTrayectoria.PeriodoQueCursa(matricula, periodo, pausas);
                var plan = ObtenerPlan(alumno.PlanEstudio);

                if (periodoQueCursa >= 1 && plan != null && plan.Materias.Count > 0)
                {
                    int duracionPlan = ReglasTrayectoria.DuracionPlan(plan);
                    var rezago = ReglasTrayectoria.MateriasEnRezago(plan, periodoQueCursa.Value, aprobadas);
                    // Pendientes de todo el plan (no solo de periodos anteriores): si no le falta nada, ya terminó
                    bool leFaltanMaterias = ReglasTrayectoria.MateriasEnRezago(plan, int.MaxValue, aprobadas).Count > 0;

                    if (rezago.Count > 0)
                    {
                        if (periodoQueCursa > duracionPlan)
                            alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Detalle", Materia = $"Cursa el {periodoQueCursa}° periodo de un plan de {duracionPlan}; le faltan {rezago.Count} materias.", Resaltar = rezago[0].Nombre, Mensaje = "Excede la duración del plan", Badge = "bg-danger", Score = 90 });

                        var candados = rezago.Where(m => plan.Seriadas.Contains(m.Nombre)).ToList();
                        if (candados.Count > 0)
                            alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Bloquean otras materias", Materia = ListarNombres(candados.Select(m => m.Nombre), 4), Resaltar = candados[0].Nombre, Mensaje = $"Candados pendientes ({candados.Count})", Badge = "bg-danger", Score = 75 });

                        alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Atrasadas", Materia = ListarNombres(rezago.Select(m => m.Nombre), 4), Resaltar = rezago[0].Nombre, Mensaje = $"Rezago: {rezago.Count} materia(s) de periodos anteriores", Badge = "bg-secondary", Score = 70 });
                    }

                    if (leFaltanMaterias && !tieneRegistroEnPeriodo && !enPausaEstePeriodo)
                        alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Periodo", Materia = periodo!, Resaltar = "", Mensaje = "Sin materias registradas en el periodo", Badge = "bg-warning text-dark", Score = 50 });
                }

                // Faltas a tutoría en el periodo de trabajo
                if (!string.IsNullOrEmpty(periodo))
                {
                    int faltas = alumno.Tutorias.Count(t => t.Periodo == periodo && t.Asistencia == "falto");
                    if (faltas > 0)
                        alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Periodo", Materia = periodo, Resaltar = "", Mensaje = $"Faltó a {faltas} tutoría(s)", Badge = "bg-warning text-dark", Score = 45 });
                }

                // ---------- Alertas por materia (según su último intento) ----------
                foreach (var mat in ultimoIntento.Values)
                {
                    string insc = (mat.TipoInscripcion ?? "").ToLower(), examen = (mat.UltimoExamen ?? "").ToLower(), estado = (mat.Estado ?? "").ToLower();
                    int nivel = 0;
                    string mensaje = "", badge = "";
                    if (insc == "segunda" && examen == "ultima" && estado == "reprobada") { nivel = 100; mensaje = "Candidato a Baja"; badge = "bg-danger"; }
                    else if (insc == "segunda" && estado != "aprobada") { nivel = 80; mensaje = "En 2da Inscripción"; badge = "bg-danger"; }
                    else if (mat.EnRiesgo) { nivel = 60; mensaje = "Riesgo Manual"; badge = "bg-warning text-dark"; }
                    else if (estado == "reprobada" && insc == "primera") { nivel = 40; mensaje = "Reprobada 1ª Insc."; badge = "bg-warning text-dark"; }

                    if (nivel > 0) alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Materia = mat.Nombre ?? "", Mensaje = mensaje, Badge = badge, Score = nivel });
                }
            }

            // OrderByDescending es estable: a igual gravedad se respeta el orden en que se generaron
            return alertas.OrderByDescending(a => a.Score).ToList();
        }

        // Si el plan no existe o su archivo está dañado, el alumno simplemente no recibe las alertas que dependen del plan
        private PlanInfo? ObtenerPlan(string? planNombre)
        {
            try { return _planes.Obtener((planNombre ?? "").Trim()); } catch { return null; }
        }

        private static string ListarNombres(IEnumerable<string> nombres, int maximo)
        {
            var lista = nombres.ToList();
            return lista.Count <= maximo ? string.Join(", ", lista) : string.Join(", ", lista.Take(maximo)) + $" y {lista.Count - maximo} más";
        }
    }
}
