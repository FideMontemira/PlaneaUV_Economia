using System;
using System.Collections.Generic;
using System.Linq;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Reporte global (riesgo, rezago, seriadas) y panel del grupo. Igual que las alertas:
    // recibe los alumnos y usa ReglasTrayectoria; no sabe nada de Excel.
    public class ServicioReportes
    {
        private readonly IRepositorioPlanes _planes;
        private readonly ServicioAlertas _alertas;

        public ServicioReportes(IRepositorioPlanes planes, ServicioAlertas alertas)
        {
            _planes = planes;
            _alertas = alertas;
        }

        public ReporteGlobal ReporteGlobal(IEnumerable<AlumnoAvance> alumnos, string? periodo)
        {
            var riesgos = new List<(string materia, AlumnoDeReporte alumno)>();
            var rezagos = new List<(string materia, AlumnoDeReporte alumno)>();
            var seriadas = new List<(string materia, AlumnoDeReporte alumno)>();

            foreach (var alumno in alumnos)
            {
                if (alumno.Situacion != "Activo") continue;

                var plan = ObtenerPlan(alumno.PlanEstudio);
                // Materias "candado" del plan del alumno (las que son prerrequisito de otra)
                var candados = plan?.Seriadas ?? new HashSet<string>();
                var quien = new AlumnoDeReporte { Nombre = alumno.Nombre ?? "", Matricula = alumno.Matricula ?? "" };

                // Último intento de cada materia
                var vigentes = new Dictionary<string, MateriaCursada>();
                foreach (var m in alumno.Materias) vigentes[m.Nombre ?? ""] = m;

                var enRezago = new HashSet<string>();
                var enSeriadas = new HashSet<string>();
                foreach (var m in vigentes.Values)
                {
                    string nombre = m.Nombre ?? "";
                    bool reprobada = string.Equals(m.Estado, "reprobada", StringComparison.OrdinalIgnoreCase);
                    if (m.EnRiesgo) riesgos.Add((nombre, quien));
                    if (m.EnRezago && enRezago.Add(nombre)) rezagos.Add((nombre, quien));
                    if (candados.Contains(nombre) && (reprobada || m.EnRezago) && enSeriadas.Add(nombre)) seriadas.Add((nombre, quien));
                }

                // Rezago automático: materias de periodos anteriores al que cursa que aún no aprueba
                int? periodoQueCursa = ReglasTrayectoria.PeriodoQueCursa(alumno.Matricula, periodo, alumno.PeriodosSinInscripcion ?? new List<string>());
                if (periodoQueCursa >= 1 && plan != null)
                {
                    foreach (var m in ReglasTrayectoria.MateriasEnRezago(plan, periodoQueCursa.Value, ReglasTrayectoria.MateriasAprobadas(alumno)))
                    {
                        if (enRezago.Add(m.Nombre)) rezagos.Add((m.Nombre, quien));
                        if (candados.Contains(m.Nombre) && enSeriadas.Add(m.Nombre)) seriadas.Add((m.Nombre, quien));
                    }
                }
            }

            // Agrupado por materia, en el orden en que aparecieron
            static List<GrupoReporte> Agrupar(List<(string materia, AlumnoDeReporte alumno)> lista) =>
                lista.GroupBy(x => x.materia).Select(g => new GrupoReporte { Materia = g.Key, Alumnos = g.Select(x => x.alumno).ToList() }).ToList();

            return new ReporteGlobal { Riesgos = Agrupar(riesgos), Rezagos = Agrupar(rezagos), Seriadas = Agrupar(seriadas) };
        }

        // Una fila por alumno con su periodo, avance, rezago y número de alertas
        public List<FilaPanelGrupo> PanelGrupo(IReadOnlyList<AlumnoAvance> alumnos, string? periodo, DateTime? hoy = null)
        {
            var alertasPorAlumno = _alertas.Calcular(alumnos, periodo).GroupBy(a => a.Matricula).ToDictionary(g => g.Key, g => g.ToList());
            var filas = new List<FilaPanelGrupo>();

            foreach (var alumno in alumnos)
            {
                string matricula = alumno.Matricula ?? "", planNombre = (alumno.PlanEstudio ?? "").Trim();
                var plan = ObtenerPlan(planNombre);

                // Créditos aprobados: cada materia aprobada cuenta una sola vez
                var aprobadas = new Dictionary<string, int>();
                int enCurso = 0;
                foreach (var m in alumno.Materias)
                {
                    string estado = (m.Estado ?? "").ToLower();
                    if (estado == "aprobada") aprobadas[m.Nombre ?? ""] = m.Creditos;
                    else if (estado == "cursando" && !string.IsNullOrEmpty(periodo) && m.Periodo == periodo) enCurso++;
                }

                int creditosAprobados = aprobadas.Values.Sum();
                int creditosTotales = plan?.CreditosTotales ?? 0;
                int? periodoQueCursa = ReglasTrayectoria.PeriodoQueCursa(matricula, periodo, alumno.PeriodosSinInscripcion ?? new List<string>());
                int rezago = (plan != null && periodoQueCursa >= 1) ? ReglasTrayectoria.MateriasEnRezago(plan, periodoQueCursa.Value, new HashSet<string>(aprobadas.Keys)).Count : 0;

                // Créditos que ya debería llevar según las trayectorias estándar y larga
                int estandar = 0, larga = 0;
                string? trayectoria = null;
                if (plan != null && periodoQueCursa >= 1)
                {
                    int concluidos = periodoQueCursa.Value - 1;
                    estandar = ReglasTrayectoria.CreditosEsperados(plan, concluidos, ReglasTrayectoria.DuracionPlan(plan));
                    larga = ReglasTrayectoria.CreditosEsperados(plan, concluidos, ReglasTrayectoria.PeriodosTrayectoriaLarga(plan));
                    trayectoria = ReglasTrayectoria.EstadoTrayectoria(creditosAprobados, estandar, larga, concluidos);
                }
                var alertasAlumno = alertasPorAlumno.TryGetValue(matricula, out var lista) ? lista : new List<Alerta>();
                var semaforo = ReglasSemaforo.Calcular(alumno, periodo, alertasAlumno, hoy ?? DateTime.Today);

                filas.Add(new FilaPanelGrupo
                {
                    Matricula = matricula,
                    Nombre = alumno.Nombre ?? "",
                    Situacion = alumno.Situacion ?? "",
                    Programa = plan?.Programa ?? (planNombre == "" || planNombre == "Generico" ? "Sin plan" : planNombre),
                    PeriodoQueCursa = periodoQueCursa,
                    DuracionPlan = ReglasTrayectoria.DuracionPlan(plan),
                    CreditosAprobados = creditosAprobados,
                    CreditosTotales = creditosTotales,
                    Avance = creditosTotales > 0 ? Math.Min(100, (int)Math.Round(creditosAprobados * 100.0 / creditosTotales)) : 0,
                    EnCurso = enCurso,
                    Rezago = rezago,
                    CreditosEstandar = estandar,
                    CreditosLarga = larga,
                    Trayectoria = trayectoria,
                    Alertas = alertasAlumno.Count,
                    Semaforo = semaforo.Final,
                    SemaforoSistema = semaforo.Sistema,
                    NivelSemaforo = ReglasSemaforo.Nivel(semaforo.Final),
                    EstadoCaso = semaforo.EstadoCaso,
                    DiasSinContacto = semaforo.DiasSinContacto
                });
            }
            return filas;
        }

        private PlanInfo? ObtenerPlan(string? planNombre)
        {
            try { return _planes.Obtener((planNombre ?? "").Trim()); } catch { return null; }
        }
    }
}
