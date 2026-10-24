using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Revisa los datos de un alumno ANTES de guardarlos. Si algo no es válido regresa el mensaje para el usuario.
    // Evita, por ejemplo, que una matrícula con "/" o demasiado larga se recorte y sobrescriba a otro alumno,
    // o que un texto enorme rompa el Excel (una celda admite como máximo 32,767 caracteres).
    public static class ValidacionAlumno
    {
        // Matrícula UV: letras y números (ej. S21012345 o zS21012345); se permite guion por si acaso
        private static readonly Regex FormatoMatricula = new(@"^[A-Za-z0-9-]{3,20}$");

        public const int MaxNombre = 200;
        public const int MaxTextoLargo = 4000;   // comentarios, acuerdos, motivos
        public const int MaxTextoCorto = 300;    // nombres de materia, periodos, opciones
        public const int MaxMaterias = 500;
        public const int MaxTutorias = 200;
        public const int MaxElementosLista = 60;

        public static string? ErrorEnMatricula(string? matricula)
        {
            if (string.IsNullOrWhiteSpace(matricula)) return "La matrícula es obligatoria.";
            if (!FormatoMatricula.IsMatch(matricula.Trim()))
                return "La matrícula solo puede tener letras y números (de 3 a 20 caracteres), por ejemplo S21012345.";
            return null;
        }

        // El FORMATO de la matrícula lo revisa el repositorio solo al crear un alumno nuevo, para no bloquear
        // a alumnos registrados antes con una matrícula que hoy no sería válida. Aquí solo que no venga vacía o enorme.
        public static string? Errores(AlumnoAvance a)
        {
            if (string.IsNullOrWhiteSpace(a.Matricula)) return "La matrícula es obligatoria.";
            if (a.Matricula.Length > 31) return "La matrícula es demasiado larga.";
            if (Largo(a.Nombre) > MaxNombre) return $"El nombre no puede pasar de {MaxNombre} caracteres.";
            if (Largo(a.Situacion) > MaxTextoCorto || Largo(a.PlanEstudio) > MaxTextoCorto) return "La situación o el plan de estudios tienen un texto demasiado largo.";

            var pausas = a.PeriodosSinInscripcion ?? new List<string>();
            if (pausas.Count > MaxElementosLista || pausas.Any(p => Largo(p) > MaxTextoCorto)) return "La lista de periodos sin inscripción no es válida.";

            var materias = a.Materias ?? new List<MateriaCursada>();
            if (materias.Count > MaxMaterias) return $"Un alumno no puede tener más de {MaxMaterias} registros de materias.";
            foreach (var m in materias)
            {
                if (new[] { m.Nombre, m.Periodo, m.TipoInscripcion, m.UltimoExamen, m.Estado }.Any(t => Largo(t) > MaxTextoCorto))
                    return "Hay una materia con un texto demasiado largo.";
                if (m.Creditos < 0 || m.Creditos > 100) return $"Los créditos de \"{m.Nombre}\" no son válidos.";
            }

            var tutorias = a.Tutorias ?? new List<TutoriaRecord>();
            if (tutorias.Count > MaxTutorias) return $"Un alumno no puede tener más de {MaxTutorias} sesiones de tutoría.";
            foreach (var t in tutorias)
            {
                if (new[] { t.Comentarios, t.Acuerdos, t.MotivoValoracion }.Any(x => Largo(x) > MaxTextoLargo))
                    return $"Un texto de la tutoría pasa de {MaxTextoLargo} caracteres; acórtalo.";
                if (new[] { t.Periodo, t.Fecha, t.Asistencia, t.Modalidad, t.OtraCanalizacion, t.MateriaDificultad, t.FechaRevision, t.EstadoCaso, t.ValoracionRiesgo, t.SemaforoSistema }
                        .Any(x => Largo(x) > MaxTextoCorto))
                    return "Un dato de la tutoría tiene un texto demasiado largo.";
                foreach (var lista in new[] { t.Temas, t.Situaciones, t.Canalizaciones })
                    if ((lista?.Count ?? 0) > MaxElementosLista || (lista ?? new List<string>()).Any(x => Largo(x) > MaxTextoCorto))
                        return "Una lista de la tutoría no es válida.";
            }
            return null;
        }

        private static int Largo(string? texto) => texto?.Length ?? 0;
    }
}
