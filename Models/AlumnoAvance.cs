using System.Collections.Generic;

namespace PlaneaUV_Economia.Models
{
    public class AlumnoAvance
    {
        public string Matricula { get; set; }
        public string Nombre { get; set; }

        public List<MateriaCursada> Materias { get; set; } = new List<MateriaCursada>();
    }

    public class MateriaCursada
    {
        public string Nombre { get; set; }
        public int Creditos { get; set; }
        public string TipoInscripcion { get; set; } 
        public string UltimoExamen { get; set; }  
        public string Estado { get; set; }          
    }
}