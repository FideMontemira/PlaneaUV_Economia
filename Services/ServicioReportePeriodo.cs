using System;
using System.Collections.Generic;
using System.Linq;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Reporte de seguimiento por periodo: cómo terminó el alumno cada periodo (materias, créditos, trayectoria,
    // rezago, semáforo y resumen de tutoría). Es el historial del alumno, semestre por semestre.
    //
    // Cada periodo se calcula como estaba AL CIERRE: solo con las materias y tutorías registradas hasta ese
    // periodo. Así el reporte de un periodo pasado no cambia aunque después se registren más cosas.
    public class ServicioReportePeriodo
    {
        private readonly IRepositorioPlanes _planes;
        private readonly ServicioAlertas _alertas;

        public ServicioReportePeriodo(IRepositorioPlanes planes, ServicioAlertas alertas)
        {
            _planes = planes;
            _alertas = alertas;
        }

        // Periodos del historial, del primero al indicado: desde el ingreso (según la matrícula) o, si la matrícula
        // no trae año, los periodos en que tiene registros.
        public List<string> PeriodosDelHistorial(AlumnoAvance alumno, string periodo)
        {
            int? ingreso = ReglasTrayectoria.AnioIngresoDeMatricula(alumno.Matricula);
            int? hasta = ReglasTrayectoria.NumeroDePeriodo(ingreso, periodo);
            if (ingreso != null && hasta >= 1)
                return Enumerable.Range(1, hasta.Value).Select(i => ReglasTrayectoria.NombreDePeriodo(ingreso.Value, i)).ToList();

            int? clave = ReglasTrayectoria.ClavePeriodo(periodo);
            return alumno.Materias.Select(m => m.Periodo).Concat(alumno.Tutorias.Select(t => t.Periodo))
                .Where(p => !string.IsNullOrWhiteSpace(p) && ReglasTrayectoria.ClavePeriodo(p) is int c && (clave == null || c <= clave))
                .Distinct().OrderBy(p => ReglasTrayectoria.ClavePeriodo(p)).Cast<string>().ToList();
        }

        public ResumenPeriodo Resumir(AlumnoAvance alumno, string periodo, DateTime hoy)
        {
            int clave = ReglasTrayectoria.ClavePeriodo(periodo) ?? int.MaxValue;
            // Registros sin año en el periodo no se pueden ordenar: se toman como anteriores
            bool HastaElCierre(string? p) => (ReglasTrayectoria.ClavePeriodo(p) ?? int.MinValue) <= clave;
            bool AntesDelPeriodo(string? p) => (ReglasTrayectoria.ClavePeriodo(p) ?? int.MinValue) < clave;

            var pausas = alumno.PeriodosSinInscripcion ?? new List<string>();
            var alCierre = Copia(alumno, alumno.Materias.Where(m => HastaElCierre(m.Periodo)), alumno.Tutorias.Where(t => HastaElCierre(t.Periodo)));
            var alInicio = Copia(alumno, alumno.Materias.Where(m => AntesDelPeriodo(m.Periodo)), new List<TutoriaRecord>());

            var fin = ReglasTrayectoria.FinDePeriodo(periodo) ?? hoy;
            var r = new ResumenPeriodo
            {
                Periodo = periodo,
                EnPausa = pausas.Contains(periodo),
                Concluido = fin.Date < hoy.Date,
                PeriodoQueCursa = ReglasTrayectoria.PeriodoQueCursa(alumno.Matricula, periodo, pausas),
                CreditosAlInicio = ReglasTrayectoria.CreditosAprobados(alInicio),
                CreditosAlCierre = ReglasTrayectoria.CreditosAprobados(alCierre)
            };
            r.CreditosGanados = Math.Max(0, r.CreditosAlCierre - r.CreditosAlInicio);

            // ---------- Materias del periodo (el último registro de cada una en ese periodo) ----------
            var delPeriodo = new Dictionary<string, MateriaCursada>();
            foreach (var m in alumno.Materias.Where(m => m.Periodo == periodo)) delPeriodo[m.Nombre ?? ""] = m;
            foreach (var m in delPeriodo.Values)
            {
                string estado = (m.Estado ?? "").ToLowerInvariant();
                string resultado = estado == "aprobada" || estado == "reprobada" ? estado : (r.Concluido ? "sin-resultado" : "cursando");
                r.Materias.Add(new MateriaDelPeriodo
                {
                    Nombre = m.Nombre ?? "", Creditos = m.Creditos, Inscripcion = m.TipoInscripcion ?? "", Examen = m.UltimoExamen ?? "",
                    Resultado = resultado, EnRiesgo = m.EnRiesgo && estado != "aprobada"
                });
            }
            r.Aprobadas = r.Materias.Count(m => m.Resultado == "aprobada");
            r.Reprobadas = r.Materias.Count(m => m.Resultado == "reprobada");
            r.SinResultado = r.Materias.Count(m => m.Resultado == "sin-resultado");

            // ---------- Trayectoria y rezago al cierre ----------
            var plan = ObtenerPlan(alumno.PlanEstudio);
            if (plan != null)
            {
                r.CreditosTotales = plan.CreditosTotales;
                if (r.PeriodoQueCursa >= 1)
                {
                    // Al cerrar el periodo, ese periodo ya cuenta como concluido (salvo que haya sido una pausa).
                    // Si todavía no termina, se calcula la situación actual, igual que en la pantalla principal.
                    int concluidos = r.EnPausa || !r.Concluido ? r.PeriodoQueCursa.Value - 1 : r.PeriodoQueCursa.Value;
                    r.CreditosEstandar = ReglasTrayectoria.CreditosEsperados(plan, concluidos, ReglasTrayectoria.DuracionPlan(plan));
                    r.CreditosLarga = ReglasTrayectoria.CreditosEsperados(plan, concluidos, ReglasTrayectoria.PeriodosTrayectoriaLarga(plan));
                    r.Trayectoria = ReglasTrayectoria.EstadoTrayectoria(r.CreditosAlCierre, r.CreditosEstandar, r.CreditosLarga, concluidos);

                    var aprobadas = ReglasTrayectoria.MateriasAprobadas(alCierre);
                    var rezago = ReglasTrayectoria.MateriasEnRezago(plan, concluidos + 1, aprobadas);
                    r.Rezago = rezago.Select(m => m.Nombre).ToList();
                    r.SeriadasPendientes = rezago.Where(m => plan.Seriadas.Contains(m.Nombre)).Select(m => m.Nombre).ToList();

                    // Para el siguiente periodo: primero las atrasadas que bloquean a otras, luego el resto del rezago
                    // y al final las del periodo que sigue (orientativo; "Sugerir carga" lo ajusta a los créditos).
                    // 'actual' es el último periodo del plan que ya pasó o que está pasando. Si el periodo sigue en curso,
                    // no se proponen las materias que está cursando ahora.
                    int actual = r.EnPausa ? concluidos : r.PeriodoQueCursa.Value;
                    var cursandoAhora = new HashSet<string>(r.Concluido ? new string[0]
                        : r.Materias.Where(m => m.Resultado == "cursando").Select(m => m.Nombre));
                    var siguientes = ReglasTrayectoria.MateriasEnRezago(plan, actual + 2, aprobadas).Where(m => !cursandoAhora.Contains(m.Nombre));
                    // Las que piden un porcentaje de créditos que aún no cubre van al final, con lo que le falta
                    r.SiguientePeriodo = siguientes
                        .Select(m => new MateriaPendiente
                        {
                            Nombre = m.Nombre, Semestre = m.Semestre, Creditos = m.Creditos,
                            Motivo = !ReglasTrayectoria.CumpleRequisitoCreditos(plan, m, r.CreditosAlCierre)
                                ? $"Cuando cubra el {m.PorcentajeCreditos} % de créditos (faltan {ReglasTrayectoria.CreditosParaRequisito(plan, m.PorcentajeCreditos) - r.CreditosAlCierre})"
                                : m.Semestre <= actual ? (plan.Seriadas.Contains(m.Nombre) ? "Atrasada · bloquea a otras" : "Atrasada") : "Del siguiente periodo"
                        })
                        .OrderBy(m => m.Motivo == "Atrasada · bloquea a otras" ? 0 : m.Motivo == "Atrasada" ? 1 : m.Motivo.StartsWith("Cuando") ? 3 : 2).ThenBy(m => m.Semestre)
                        .ToList();
                }
            }

            // ---------- Semáforo como estaba al cierre ----------
            var fecha = fin.Date < hoy.Date ? fin.Date : hoy.Date;
            r.Semaforo = ReglasSemaforo.Calcular(alCierre, periodo, _alertas.Calcular(new[] { alCierre }, periodo), fecha);

            // ---------- Resumen de tutoría del periodo ----------
            var sesiones = alumno.Tutorias.Where(t => t.Periodo == periodo && GeneradorDocumentoTutorias.TieneInformacion(t)).ToList();
            var problematicas = sesiones.SelectMany(t => t.Situaciones ?? new List<string>()).Distinct().ToList();
            r.Tutoria = new ResumenTutoriaPeriodo
            {
                Sesiones = sesiones.Count,
                Asistio = sesiones.Count(t => t.Asistencia == "asistio"),
                NoAsistio = sesiones.Count(t => t.Asistencia == "falto"),
                Justificadas = sesiones.Count(t => t.Asistencia == "justificado"),
                Problematicas = problematicas.Where(p => !CatalogoTutorias.EsPrivada(p))
                    .Concat(problematicas.Any(CatalogoTutorias.EsPrivada) ? new[] { "Situación personal o de salud (confidencial)" } : Array.Empty<string>()).ToList(),
                // Las canalizaciones del caso (aunque vengan de un periodo anterior) con si acudió
                Canalizaciones = r.Semaforo.CanalizacionesCaso.Count > 0
                    ? r.Semaforo.CanalizacionesCaso.Select(c => $"{c.Area} ({ReglasCaso.TextoAcudio(c.Acudio)})").ToList()
                    : sesiones.SelectMany(ReglasCaso.Areas).Distinct().ToList(),
                EstadoCaso = r.Semaforo.EstadoCaso == null ? null
                    : r.Semaforo.EstadoCaso + (r.Semaforo.CasoDesde != null ? $" (desde {r.Semaforo.CasoDesde})" : "")
            };
            return r;
        }

        private static AlumnoAvance Copia(AlumnoAvance a, IEnumerable<MateriaCursada> materias, IEnumerable<TutoriaRecord> tutorias) => new()
        {
            Matricula = a.Matricula, Nombre = a.Nombre, Situacion = a.Situacion, PlanEstudio = a.PlanEstudio,
            PeriodosSinInscripcion = a.PeriodosSinInscripcion ?? new List<string>(),
            Materias = materias.ToList(), Tutorias = tutorias.ToList()
        };

        private PlanInfo? ObtenerPlan(string? planNombre)
        {
            try { return _planes.Obtener((planNombre ?? "").Trim()); } catch { return null; }
        }
    }
}
