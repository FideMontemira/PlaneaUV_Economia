using System.Collections.Generic;
using System.Linq;

namespace PlaneaUV_Economia.Services
{
    // Una categoría de problemáticas del reporte de tutoría. Las "privadas" (personales y de salud) solo deben
    // verlas el tutor y la coordinación de tutorías; en vistas de dirección se mostrarán solo como conteos.
    public class CategoriaProblematica
    {
        public string Nombre { get; init; } = "";
        public string Icono { get; init; } = "";
        public bool Privada { get; init; }
        public IReadOnlyList<string> Opciones { get; init; } = new List<string>();
    }

    // Opciones del formato guiado de tutoría. Es el ÚNICO lugar donde se definen: la pantalla las pide
    // al servidor (ObtenerCatalogoTutorias) y el documento Word las imprime como casillas.
    // Para agregar, quitar o renombrar una opción basta con cambiarla aquí.
    // Nota: los nombres de las áreas de canalización deben validarse con la institución.
    public static class CatalogoTutorias
    {
        public static readonly IReadOnlyList<string> Modalidades = new[] { "Presencial", "En línea" };

        public static readonly IReadOnlyList<string> Temas = new[]
        {
            "Trayectoria y avance académico",
            "Selección de carga e inscripción",
            "Rendimiento y hábitos de estudio",
            "Reglamento y trámites escolares",
            "Servicio social y prácticas profesionales",
            "Experiencia recepcional y titulación",
            "Movilidad estudiantil",
            "Orientación vocacional y proyecto de vida",
        };

        // Problemáticas por categoría. Se conservan los nombres de la versión anterior (Económica, Salud física...)
        // para que lo que ya se capturó siga marcado.
        public static readonly IReadOnlyList<CategoriaProblematica> Problematicas = new[]
        {
            new CategoriaProblematica
            {
                Nombre = "Académicas", Icono = "bi-book",
                Opciones = new[]
                {
                    "Académica (reprobación o rezago)", "Dificultad en una materia", "Bases previas insuficientes (matemáticas, lectura o redacción)",
                    "Hábitos y técnicas de estudio", "No entrega trabajos o tareas", "Inasistencia a clases", "Carga académica excesiva",
                    "Traslape de horarios o no encontró cupo", "Seriación o prerrequisitos pendientes", "Requisito de idioma (inglés)",
                    "Servicio social, prácticas o Experiencia Recepcional"
                }
            },
            new CategoriaProblematica
            {
                Nombre = "Económicas y laborales", Icono = "bi-cash-coin",
                Opciones = new[] { "Económica", "Requiere o perdió una beca", "Laboral (trabaja y estudia)", "Trabaja más de 20 horas a la semana", "Transporte o traslado", "Vivienda o alimentación" }
            },
            new CategoriaProblematica
            {
                Nombre = "Personales y de salud", Icono = "bi-heart-pulse", Privada = true,
                Opciones = new[] { "Emocional o psicológica", "Salud física", "Familiar", "Cuidado de dependientes (hijos, familiares)", "Violencia o acoso", "Consumo de sustancias" }
            },
            new CategoriaProblematica
            {
                Nombre = "Vocacionales e institucionales", Icono = "bi-compass",
                Opciones = new[] { "Adaptación a la vida universitaria", "Duda vocacional o cambio de carrera", "Trámites y servicios escolares", "Dificultad en la relación con un docente" }
            },
        };

        // Todas las problemáticas en una sola lista (es lo que se guarda en TutoriaRecord.Situaciones)
        public static readonly IReadOnlyList<string> Situaciones = Problematicas.SelectMany(c => c.Opciones).ToList();

        // Las personales y de salud son confidenciales: en la ficha impresa y en las señales se muestran sin detalle
        public static bool EsPrivada(string situacion) => Problematicas.Any(c => c.Privada && c.Opciones.Contains(situacion));

        public static readonly IReadOnlyList<string> Canalizaciones = new[]
        {
            "CENDHIU (desarrollo humano y apoyo psicológico)",
            "Asesoría de pares",
            "Asesoría del profesor de la materia",
            "Becas y apoyos económicos",
            "Servicios de salud",
            "Asesorías académicas (PAFI)",
            "Coordinación de tutorías de la facultad",
            "Coordinación del programa educativo (trámites, seriación)",
            "Ajuste de carga o de horario",
            "Centro de Idiomas / Autoacceso",
            "Movilidad estudiantil",
            "Defensoría de los Derechos Universitarios",
            "Coordinación de la Unidad de Género",
        };

        public static readonly IReadOnlyList<string> EstadosCaso = new[] { "Abierto", "En atención", "Cerrado" };
    }
}
