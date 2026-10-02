using System.Collections.Generic;

namespace PlaneaUV_Economia.Models
{
    public class AlumnoAvance
    {
        public string? Matricula { get; set; }
        public string? Nombre { get; set; }
        public string? Situacion { get; set; }
        public List<TutoriaRecord> Tutorias { get; set; } = new List<TutoriaRecord>();
        public List<MateriaCursada> Materias { get; set; } = new List<MateriaCursada>();
    }

    public class TutoriaRecord
    {
        public string? Periodo { get; set; }
        public int Sesion { get; set; }
        public string? Fecha { get; set; }
        public string? Asistencia { get; set; }
        public string? Comentarios { get; set; }
    }

    public class MateriaCursada
    {
        public string? Nombre { get; set; }
        public string? Periodo { get; set; }
        public int Creditos { get; set; }
        public string? TipoInscripcion { get; set; }
        public string? UltimoExamen { get; set; }
        public string? Estado { get; set; }
        public bool EnRezago { get; set; }
        public bool EnRiesgo { get; set; }
    }

    public class ReporteItem
    {
        public string? Matricula { get; set; }
        public string? Nombre { get; set; }
        public string? Materia { get; set; }
    }
}