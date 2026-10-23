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

        // Fuentes de las señales (se muestran junto a cada razón del semáforo)
        public const string PlanDeEstudios = "Plan de estudios";
        public const string Historial = "Historial académico";
        public const string Tutoria = "Tutoría";
        public const string Inscripcion = "Inscripción del periodo";

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
                            alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Detalle", Materia = $"Cursa el {periodoQueCursa}° periodo de un plan de {duracionPlan}; le faltan {rezago.Count} materias.", Resaltar = rezago[0].Nombre, Mensaje = "Excede la duración del plan", Badge = "bg-danger", Score = 90, Fuente = PlanDeEstudios });

                        var candados = rezago.Where(m => plan.Seriadas.Contains(m.Nombre)).ToList();
                        if (candados.Count > 0)
                            alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Bloquean otras materias", Materia = ListarNombres(candados.Select(m => m.Nombre), 4), Resaltar = candados[0].Nombre, Mensaje = $"Candados pendientes ({candados.Count})", Badge = "bg-danger", Score = 75, Fuente = PlanDeEstudios });

                        alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Atrasadas", Materia = ListarNombres(rezago.Select(m => m.Nombre), 4), Resaltar = rezago[0].Nombre, Mensaje = $"Rezago: {rezago.Count} materia(s) de periodos anteriores", Badge = "bg-secondary", Score = 70, Fuente = PlanDeEstudios });
                    }

                    // Avance en créditos frente a las trayectorias estándar y larga (desde el 2° periodo)
                    if (leFaltanMaterias && periodoQueCursa >= 2)
                    {
                        int concluidos = periodoQueCursa.Value - 1;
                        int creditos = ReglasTrayectoria.CreditosAprobados(alumno);
                        int estandar = ReglasTrayectoria.CreditosEsperados(plan, concluidos, duracionPlan);
                        int larga = ReglasTrayectoria.CreditosEsperados(plan, concluidos, ReglasTrayectoria.PeriodosTrayectoriaLarga(plan));
                        string detalle = $"Lleva {creditos} créditos; la trayectoria estándar pide {estandar} y la larga (mínimo) {larga}.";
                        var estado = ReglasTrayectoria.EstadoTrayectoria(creditos, estandar, larga, concluidos);
                        if (estado == "debajo-minimo")
                            alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Créditos", Materia = detalle, Resaltar = "", Mensaje = "Debajo del mínimo (trayectoria larga)", Badge = "bg-danger", Score = 85, Fuente = Historial });
                        else if (estado == "debajo-estandar")
                            alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Créditos", Materia = detalle, Resaltar = "", Mensaje = "Debajo de la trayectoria estándar", Badge = "bg-warning text-dark", Score = 52, Fuente = Historial });
                    }

                    // Inscribió menos materias de las que le quedan pendientes de SU periodo (si adelantó alguna, no se le pide)
                    if (tieneRegistroEnPeriodo && !enPausaEstePeriodo && periodoQueCursa <= duracionPlan)
                    {
                        int registradas = alumno.Materias.Where(m => m.Periodo == periodo).Select(m => m.Nombre ?? "").Distinct().Count();
                        // Pendientes hasta su periodo menos pendientes de periodos anteriores = pendientes de su periodo
                        int esperadas = ReglasTrayectoria.MateriasEnRezago(plan, periodoQueCursa.Value + 1, aprobadas).Count - rezago.Count;
                        if (registradas < esperadas)
                            alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Inscripción", Materia = $"Registró {registradas} de las {esperadas} materias de su {periodoQueCursa}° periodo.", Resaltar = "", Mensaje = "Carga menor a la del plan", Badge = "bg-warning text-dark", Score = 48, Fuente = Inscripcion });
                    }

                    if (leFaltanMaterias && !tieneRegistroEnPeriodo && !enPausaEstePeriodo)
                        alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Periodo", Materia = periodo!, Resaltar = "", Mensaje = "Sin materias registradas en el periodo", Badge = "bg-warning text-dark", Score = 50, Fuente = Inscripcion });
                }

                // Faltas a tutoría en el periodo de trabajo
                if (!string.IsNullOrEmpty(periodo))
                {
                    int faltas = alumno.Tutorias.Count(t => t.Periodo == periodo && t.Asistencia == "falto");
                    if (faltas > 0)
                        alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = "Periodo", Materia = periodo, Resaltar = "", Mensaje = $"Faltó a {faltas} tutoría(s)", Badge = "bg-warning text-dark", Score = 45, Fuente = Tutoria });

                    // Caso de tutoría abierto o en atención: se avisa a qué se canalizó (o qué situación detectó).
                    // Cuenta el estado de la última sesión que lo indica; si el tutor lo cerró, ya no se alerta.
                    var delPeriodo = alumno.Tutorias.Where(t => t.Periodo == periodo).OrderBy(t => t.Sesion).ToList();
                    var ultimaConEstado = delPeriodo.LastOrDefault(t => !string.IsNullOrWhiteSpace(t.EstadoCaso) || t.RequiereSeguimiento);
                    bool casoAbierto = ultimaConEstado != null && (string.IsNullOrWhiteSpace(ultimaConEstado.EstadoCaso)
                        ? ultimaConEstado.RequiereSeguimiento : ultimaConEstado.EstadoCaso != "Cerrado");
                    var conSeguimiento = casoAbierto ? delPeriodo.Where(t => t.RequiereSeguimiento || (!string.IsNullOrWhiteSpace(t.EstadoCaso) && t.EstadoCaso != "Cerrado")).ToList() : new List<TutoriaRecord>();
                    if (conSeguimiento.Count > 0)
                    {
                        var canalizado = conSeguimiento.SelectMany(t => (t.Canalizaciones ?? new List<string>())
                            .Concat(string.IsNullOrWhiteSpace(t.OtraCanalizacion) ? Array.Empty<string>() : new[] { t.OtraCanalizacion!.Trim() })).Distinct().ToList();
                        var situaciones = conSeguimiento.SelectMany(t => t.Situaciones ?? new List<string>()).Distinct().ToList();
                        string detalle = canalizado.Count > 0 ? string.Join(", ", canalizado)
                                       : situaciones.Count > 0 ? "Situación: " + string.Join(", ", situaciones)
                                       : "Sin canalización registrada";
                        alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Etiqueta = canalizado.Count > 0 ? "Canalizado a" : "Detalle", Materia = detalle, Resaltar = "", Mensaje = "Requiere seguimiento de tutoría", Badge = "bg-warning text-dark", Score = 55, Fuente = Tutoria });
                    }
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

                    if (nivel > 0) alertas.Add(new Alerta { Matricula = matricula, Nombre = nombre, Materia = mat.Nombre ?? "", Mensaje = mensaje, Badge = badge, Score = nivel, Fuente = Historial });
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
