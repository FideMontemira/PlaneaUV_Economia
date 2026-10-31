using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;
using Microsoft.AspNetCore.Http;

namespace PlaneaUV_Economia.Tests
{
    // Datos de prueba en memoria (sin archivos)
    public static class Datos
    {
        // Plan pequeño de 4 periodos:
        //   1: A (6), B (4)          2: C (6, req. A), Optativa 1 (5)
        //   3: D (6, req. C), E (4)  4: F (8)
        //   AFEL en el periodo 5 (no cuenta como periodo del plan)
        public static PlanInfo PlanCorto()
        {
            var plan = new PlanInfo
            {
                Programa = "Licenciatura de Prueba",
                CreditosTotales = 45,
                Materias =
                {
                    new MateriaPlan { Semestre = 1, Nombre = "A", Creditos = 6, Tipo = "Normal", Area = "Básica" },
                    new MateriaPlan { Semestre = 1, Nombre = "B", Creditos = 4, Tipo = "Normal", Area = "General" },
                    new MateriaPlan { Semestre = 2, Nombre = "C", Creditos = 6, Tipo = "Normal", Prerrequisitos = "A" },
                    new MateriaPlan { Semestre = 2, Nombre = "Optativa 1", Creditos = 5, Tipo = "Optativa" },
                    new MateriaPlan { Semestre = 3, Nombre = "D", Creditos = 6, Tipo = "Normal", Prerrequisitos = "C" },
                    new MateriaPlan { Semestre = 3, Nombre = "E", Creditos = 4, Tipo = "Normal" },
                    new MateriaPlan { Semestre = 4, Nombre = "F", Creditos = 8, Tipo = "Normal" },
                    new MateriaPlan { Semestre = 5, Nombre = "AFEL 1", Creditos = 6, Tipo = "AFEL" },
                },
                Optativas = { new OptativaPlan { Nombre = "Teoría de Juegos", Grupo = "Área 1" } },
            };
            plan.Seriadas.Add("A");
            plan.Seriadas.Add("C");
            return plan;
        }

        public static MateriaCursada Materia(string nombre, string estado, string periodo = "Agosto - Enero 2024", bool riesgo = false, bool rezago = false,
                                             string inscripcion = "primera", string examen = "ordinario", int creditos = 6) =>
            new MateriaCursada { Nombre = nombre, Estado = estado, Periodo = periodo, EnRiesgo = riesgo, EnRezago = rezago, TipoInscripcion = inscripcion, UltimoExamen = examen, Creditos = creditos };

        public static AlumnoAvance Alumno(string matricula, params MateriaCursada[] materias)
        {
            var a = new AlumnoAvance { Matricula = matricula, Nombre = "Alumno " + matricula, Situacion = "Activo", PlanEstudio = "Plan_Corto" };
            a.Materias.AddRange(materias);
            return a;
        }
    }

    // Repositorio de planes en memoria para probar alertas y reportes sin archivos
    public class PlanesEnMemoria : IRepositorioPlanes
    {
        private readonly Dictionary<string, PlanInfo> _planes;
        public PlanesEnMemoria(Dictionary<string, PlanInfo> planes) { _planes = planes; }
        public static PlanesEnMemoria ConPlanCorto() => new(new Dictionary<string, PlanInfo> { ["Plan_Corto"] = Datos.PlanCorto() });

        public IReadOnlyList<string> Listar() => _planes.Keys.ToList();
        public bool Existe(string planNombre) => _planes.ContainsKey(planNombre);
        public PlanInfo? Obtener(string planNombre) => _planes.TryGetValue(planNombre, out var p) ? p : null;
        public Task<IReadOnlyList<string>> GuardarAsync(IFormFile archivo) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
    }
}
