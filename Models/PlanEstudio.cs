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
        // Periodos de la trayectoria larga (el ritmo mínimo para terminar en el plazo máximo).
        // 0 = no se indicó en la hoja "Datos": se usa 1.5 veces la duración del plan.
        public int PeriodosTrayectoriaLarga { get; set; }
        // Apoyos de la facultad con horario y lugar (hoja opcional "Apoyos"); se imprimen en la ficha de tutoría
        public List<ApoyoPlan> Apoyos { get; set; } = new List<ApoyoPlan>();
    }

    public class ApoyoPlan
    {
        public string Nombre { get; set; } = "";
        public string Detalle { get; set; } = "";   // horario, lugar o cómo solicitarlo
    }

    public class MateriaPlan
    {
        public int Semestre { get; set; }
        public string Nombre { get; set; } = "";
        public int Creditos { get; set; }
        public string Tipo { get; set; } = "";            // Normal, Optativa o AFEL
        public string Prerrequisitos { get; set; } = "";  // nombres separados por coma
        public string Area { get; set; } = "";            // AFBG, Iniciación a la disciplina, Disciplinar o Terminal (también acepta General y Básica)
        // Porcentaje de los créditos totales que el alumno debe tener aprobados para inscribirla
        // (p. ej. 70 en servicio social, experiencia recepcional y prácticas profesionales). 0 = sin requisito.
        public int PorcentajeCreditos { get; set; }
        // Periodos que dura la materia (columna H; p. ej. servicio social: 2). Mientras siga "cursando" dentro de
        // ese plazo, en los periodos siguientes se toma como en curso, no como rezago. 1 = un solo periodo.
        public int PeriodosDuracion { get; set; } = 1;

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
