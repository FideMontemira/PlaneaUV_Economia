using System.Collections.Generic;

namespace PlaneaUV_Economia.Models
{
    // Plan de estudios leído de su plantilla. Nada del plan está fijo en el código:
    // materias, créditos totales, optativas y seriadas salen de aquí.
    public class PlanInfo
    {
        public string Programa { get; set; } = "";
        public int CreditosTotales { get; set; }
        public List<MateriaPlan> Materias { get; set; } = new List<MateriaPlan>();
        public List<OptativaPlan> Optativas { get; set; } = new List<OptativaPlan>();
        // Materias "candado": las que son prerrequisito de alguna otra materia del plan
        public HashSet<string> Seriadas { get; set; } = new HashSet<string>();
    }

    public class MateriaPlan
    {
        public int Semestre { get; set; }
        public string Nombre { get; set; } = "";
        public int Creditos { get; set; }
        public string Tipo { get; set; } = "";            // Normal, Optativa o AFEL
        public string Prerrequisitos { get; set; } = "";  // nombres separados por coma
        public string Area { get; set; } = "";            // General, Básica, Disciplinar o Terminal

        [System.Text.Json.Serialization.JsonIgnore]
        public bool EsAfel => Tipo.Equals("afel", System.StringComparison.OrdinalIgnoreCase);
        [System.Text.Json.Serialization.JsonIgnore]
        public bool EsOptativa => Tipo.Equals("optativa", System.StringComparison.OrdinalIgnoreCase);
    }

    public class OptativaPlan
    {
        public string Nombre { get; set; } = "";
        public string Grupo { get; set; } = "";
    }
}
