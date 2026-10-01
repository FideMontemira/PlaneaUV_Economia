using System.Collections.Generic;

namespace PlaneaUV_Economia.Models
{
    public class AlumnoAvance
    {
        public string? Matricula { get; set; }
        public string? Nombre { get; set; }

        // Sesión 1
        public string? Tutoria1 { get; set; }
        public string? FechaTutoria1 { get; set; }
        public string? AsistenciaTutoria1 { get; set; }

        // Sesión 2
        public string? Tutoria2 { get; set; }
        public string? FechaTutoria2 { get; set; }
        public string? AsistenciaTutoria2 { get; set; }

        // Sesión 3
        public string? Tutoria3 { get; set; }
        public string? FechaTutoria3 { get; set; }
        public string? AsistenciaTutoria3 { get; set; }

        public List<MateriaCursada> Materias { get; set; } = new List<MateriaCursada>();
    }

    public class MateriaCursada
    {
        public string? Nombre { get; set; }
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