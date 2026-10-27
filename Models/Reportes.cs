using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlaneaUV_Economia.Models
{
    // Lo que se muestra en las listas de alumnos
    public class AlumnoResumen
    {
        public string Matricula { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Situacion { get; set; } = "";
    }

    // Una tarjeta del panel de alertas
    public class Alerta
    {
        public string Matricula { get; set; } = "";
        public string Nombre { get; set; } = "";
        // Texto de la etiqueta del detalle ("Atrasadas", "Periodo"...). Si no viene, la pantalla usa "Materia".
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Etiqueta { get; set; }
        public string Materia { get; set; } = "";
        // Materia a resaltar en el mapa al dar "Revisar Mapa". Si no viene, se resalta 'Materia'.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Resaltar { get; set; }
        public string Mensaje { get; set; } = "";
        public string Badge { get; set; } = "";
        public int Score { get; set; }
        // De dónde sale la señal: Plan de estudios, Historial académico o Tutoría
        public string Fuente { get; set; } = "";
    }

    // Una fila del panel del grupo
    public class FilaPanelGrupo
    {
        public string Matricula { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Situacion { get; set; } = "";
        public string Programa { get; set; } = "";
        public int? PeriodoQueCursa { get; set; }
        public int DuracionPlan { get; set; }
        public int CreditosAprobados { get; set; }
        public int CreditosTotales { get; set; }
        public int Avance { get; set; }
        public int EnCurso { get; set; }
        public int Rezago { get; set; }
        public int CreditosEstandar { get; set; }
        public int CreditosLarga { get; set; }
        public string? Trayectoria { get; set; }
        public int Alertas { get; set; }
        public string Semaforo { get; set; } = "verde";         // color final (con la valoración del tutor)
        public string SemaforoSistema { get; set; } = "verde";  // color que calculó el sistema
        public int NivelSemaforo { get; set; }                   // para ordenar: 3 rojo, 2 amarillo, 1 verde, 0 sin
        public string? EstadoCaso { get; set; }
        public int? DiasSinContacto { get; set; }
    }

    // Reporte global: por cada materia, los alumnos en esa condición
    public class ReporteGlobal
    {
        public List<GrupoReporte> Riesgos { get; set; } = new List<GrupoReporte>();
        public List<GrupoReporte> Rezagos { get; set; } = new List<GrupoReporte>();
        public List<GrupoReporte> Seriadas { get; set; } = new List<GrupoReporte>();
    }

    public class GrupoReporte
    {
        public string Materia { get; set; } = "";
        public List<AlumnoDeReporte> Alumnos { get; set; } = new List<AlumnoDeReporte>();
    }

    public class AlumnoDeReporte
    {
        public string Nombre { get; set; } = "";
        public string Matricula { get; set; } = "";
    }

    // Situación académica del alumno en un Periodo de Trabajo. La calcula SOLO el servidor
    // (ReglasTrayectoria); la pantalla únicamente la muestra.
    public class SituacionAlumno
    {
        public int? AnioIngreso { get; set; }                // null si la matrícula no trae el año (S21...)
        public int? PeriodosCorridos { get; set; }           // periodos desde el ingreso, sin descontar pausas
        public int? PeriodoQueCursa { get; set; }            // descontando los periodos sin inscripción
        public int PausasDescontadas { get; set; }
        public bool EnPausa { get; set; }                    // el Periodo de Trabajo está marcado como sin inscripción
        public int DuracionPlan { get; set; }                // último periodo del plan (sin contar AFEL)
        public List<string> MateriasEnRezago { get; set; } = new List<string>();      // nombres de tarjeta del plan
        public List<string> PeriodosDesdeIngreso { get; set; } = new List<string>();  // para marcar pausas
        public SemaforoAlumno? Semaforo { get; set; }

        // Avance frente a las trayectorias (créditos que ya debería llevar al concluir los periodos anteriores)
        public int CreditosAprobados { get; set; }
        public int CreditosTotales { get; set; }
        public int CreditosEstandar { get; set; }
        public int CreditosLarga { get; set; }
        public int PeriodosTrayectoriaLarga { get; set; }
        public string? Trayectoria { get; set; }             // estandar, debajo-estandar, debajo-minimo o null

        // Materias con alta reprobación en el grupo (para marcarlas en el mapa)
        public List<MateriaCritica> MateriasCriticas { get; set; } = new List<MateriaCritica>();

        // Materias que piden un porcentaje de créditos (servicio social, experiencia recepcional, prácticas...)
        public List<RequisitoCreditos> RequisitosCreditos { get; set; } = new List<RequisitoCreditos>();
    }

    // Requisito de créditos de una materia del plan frente al avance del alumno
    public class RequisitoCreditos
    {
        public string Materia { get; set; } = "";
        public int Porcentaje { get; set; }          // p. ej. 70
        public int CreditosNecesarios { get; set; }  // el porcentaje de los créditos totales, redondeado hacia arriba
        public int Faltan { get; set; }              // 0 si ya lo cumple
        public bool Cumple { get; set; }
        public bool YaInscrita { get; set; }         // ya la aprobó o la está cursando
    }

    // Materia con alta reprobación entre los alumnos registrados (la calcula ReglasMateriasCriticas)
    public class MateriaCritica
    {
        public string Materia { get; set; } = "";
        public int Intentos { get; set; }           // intentos con resultado (aprobada o reprobada)
        public int Reprobaciones { get; set; }
        public int Porcentaje { get; set; }         // % de reprobación
        public int Alumnos { get; set; }            // alumnos distintos que la han cursado
        public int CursandoAhora { get; set; }      // alumnos que la cursan en el periodo de trabajo
        public int EnRiesgoAhora { get; set; }      // alumnos con la materia marcada en riesgo
        public bool Critica { get; set; }
        // Por qué no es crítica (si no lo es): "pocos casos" o "baja reprobación"
        public string? Motivo { get; set; }

        // Quiénes: para que coordinación sepa a qué alumnos atender, no solo cuántos son
        public List<AlumnoDeReporte> Reprobaron { get; set; } = new List<AlumnoDeReporte>();
        public List<AlumnoDeReporte> Cursando { get; set; } = new List<AlumnoDeReporte>();
        public List<AlumnoDeReporte> EnRiesgo { get; set; } = new List<AlumnoDeReporte>();
    }

    // Una canalización del caso de tutoría y si el alumno acudió
    public class CanalizacionCaso
    {
        public string Area { get; set; } = "";
        public string Periodo { get; set; } = "";        // periodo en que se canalizó
        public string Acudio { get; set; } = "pendiente"; // si, no, concluida, pendiente
        public string? ConcluidaEn { get; set; }          // periodo en que se marcó como concluida
    }

    // Una señal que explica el color del semáforo, con su fuente
    public class SenalRiesgo
    {
        public string Texto { get; set; } = "";
        public string Fuente { get; set; } = "";
        public bool Grave { get; set; }
        // Solo informa (lo registró el tutor, cursa una materia crítica...): no cambia el color
        public bool Informativa { get; set; }
    }

    // Semáforo de riesgo del alumno en un periodo (lo calcula ReglasSemaforo)
    public class SemaforoAlumno
    {
        public string Sistema { get; set; } = "verde";   // rojo, amarillo, verde, sin (alumno de baja)
        public string Final { get; set; } = "verde";     // con la valoración del tutor
        public List<SenalRiesgo> Senales { get; set; } = new List<SenalRiesgo>();
        public List<string> TemasSugeridos { get; set; } = new List<string>();

        public string? Valoracion { get; set; }          // confirma, eleva, descarta
        public string? MotivoValoracion { get; set; }
        public bool ValoracionDesactualizada { get; set; }

        public string? EstadoCaso { get; set; }          // Abierto, En atención, Cerrado
        public string? CasoDesde { get; set; }           // periodo en que se abrió, si viene de un periodo anterior
        public List<CanalizacionCaso> CanalizacionesCaso { get; set; } = new List<CanalizacionCaso>();
        public string? UltimoContacto { get; set; }      // última sesión a la que asistió (yyyy-MM-dd)
        public int? DiasSinContacto { get; set; }
        public string? AcuerdoPendiente { get; set; }
        public string? FechaRevision { get; set; }
        public bool RevisionVencida { get; set; }
    }

    // Lo que manda la pantalla de tutorías: las sesiones de UN periodo de un alumno
    public class SolicitudTutorias
    {
        public string? Matricula { get; set; }
        public string? Periodo { get; set; }
        public List<TutoriaRecord> Sesiones { get; set; } = new List<TutoriaRecord>();
    }

    // ===================== REPORTE DEL PERIODO (historial de seguimiento) =====================
    // Cómo estaba el alumno AL CIERRE de un periodo: se calcula solo con lo registrado hasta ese periodo.
    public class ResumenPeriodo
    {
        public string Periodo { get; set; } = "";
        public bool EnPausa { get; set; }                       // periodo marcado sin inscripción
        public bool Concluido { get; set; }                     // el periodo ya terminó (según la fecha)
        public int? PeriodoQueCursa { get; set; }

        public List<MateriaDelPeriodo> Materias { get; set; } = new List<MateriaDelPeriodo>();
        public int Aprobadas { get; set; }
        public int Reprobadas { get; set; }
        public int SinResultado { get; set; }

        public int CreditosAlInicio { get; set; }
        public int CreditosGanados { get; set; }
        public int CreditosAlCierre { get; set; }
        public int CreditosTotales { get; set; }
        public int CreditosEstandar { get; set; }
        public int CreditosLarga { get; set; }
        public string? Trayectoria { get; set; }                // estandar, debajo-estandar, debajo-minimo

        public List<string> Rezago { get; set; } = new List<string>();
        public List<string> SeriadasPendientes { get; set; } = new List<string>();
        public SemaforoAlumno Semaforo { get; set; } = new SemaforoAlumno();

        public ResumenTutoriaPeriodo Tutoria { get; set; } = new ResumenTutoriaPeriodo();
        public List<MateriaPendiente> SiguientePeriodo { get; set; } = new List<MateriaPendiente>();
    }

    public class MateriaDelPeriodo
    {
        public string Nombre { get; set; } = "";
        public int Creditos { get; set; }
        public string Inscripcion { get; set; } = "";           // primera / segunda
        public string Examen { get; set; } = "";                // ordinario, extraordinario, título, última
        public string Resultado { get; set; } = "";             // aprobada, reprobada, cursando, sin-resultado
        public bool EnRiesgo { get; set; }
    }

    public class MateriaPendiente
    {
        public string Nombre { get; set; } = "";
        public int Semestre { get; set; }
        public int Creditos { get; set; }
        public string Motivo { get; set; } = "";                // "Atrasada · candado", "Atrasada", "Del siguiente periodo"
    }

    // Solo el resumen: el detalle de cada sesión está en el reporte de tutoría
    public class ResumenTutoriaPeriodo
    {
        public int Sesiones { get; set; }
        public int Asistio { get; set; }
        public int NoAsistio { get; set; }
        public int Justificadas { get; set; }
        public List<string> Problematicas { get; set; } = new List<string>();   // lo confidencial, sin detalle
        public List<string> Canalizaciones { get; set; } = new List<string>();
        public string? EstadoCaso { get; set; }
    }
}
