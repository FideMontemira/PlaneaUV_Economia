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
        public int Alertas { get; set; }
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
    }
}
