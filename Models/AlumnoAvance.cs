using System.Collections.Generic;

namespace PlaneaUV_Economia.Models
{
    public class AlumnoAvance
    {
        public string? Matricula { get; set; }
        public string? Nombre { get; set; }
        public string? Situacion { get; set; }
        public string PlanEstudio { get; set; }
        // Periodos en que el alumno NO estuvo inscrito (baja temporal, pausa). No cuentan para saber
        // en qué periodo va. Ej.: "Agosto - Enero 2024"
        public List<string> PeriodosSinInscripcion { get; set; } = new List<string>();
        public List<TutoriaRecord> Tutorias { get; set; } = new List<TutoriaRecord>();
        public List<MateriaCursada> Materias { get; set; } = new List<MateriaCursada>();
    }

    public class TutoriaRecord
    {
        public string? Periodo { get; set; }
        public int Sesion { get; set; }
        public string? Fecha { get; set; }
        public string? Asistencia { get; set; }
        public string? Comentarios { get; set; }       // observaciones del tutor

        // Formato guiado de la sesión (las opciones salen de Services/CatalogoTutorias.cs)
        public string? Modalidad { get; set; }                                   // presencial / en línea
        public List<string> Temas { get; set; } = new List<string>();            // temas tratados
        public List<string> Situaciones { get; set; } = new List<string>();      // situaciones detectadas
        public List<string> Canalizaciones { get; set; } = new List<string>();   // áreas a las que se canaliza
        public string? OtraCanalizacion { get; set; }
        public string? Acuerdos { get; set; }                                    // acuerdo principal y compromisos
        public bool RequiereSeguimiento { get; set; }                            // caso abierto o en atención

        public string? MateriaDificultad { get; set; }     // materia con la que tiene dificultad (problemática académica)
        public string? FechaRevision { get; set; }         // cuándo se revisa el acuerdo (yyyy-MM-dd)
        public string? EstadoCaso { get; set; }            // "", Abierto, En atención, Cerrado

        // Valoración del tutor sobre el semáforo que calculó el sistema. Se guardan ambos: lo que dijo el
        // sistema en ese momento (SemaforoSistema) y lo que decidió el tutor.
        public string? ValoracionRiesgo { get; set; }      // "", confirma, eleva, descarta
        public string? MotivoValoracion { get; set; }      // obligatorio para descartar
        public string? SemaforoSistema { get; set; }       // rojo, amarillo, verde
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
}