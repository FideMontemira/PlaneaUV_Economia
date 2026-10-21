using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // =========================================================================
    // REGLAS ACADÉMICAS (única fuente de verdad)
    // Periodo que cursa el alumno, periodos sin inscripción y rezago automático.
    // No leen ni escriben archivos: reciben datos y regresan resultados, así se pueden probar
    // solas y el JavaScript nunca las repite (solo muestra lo que calcula el servidor).
    // =========================================================================
    public static class ReglasTrayectoria
    {
        // La matrícula lleva el año de ingreso: S21... o zS21... = agosto de 2021.
        public static int? AnioIngresoDeMatricula(string? matricula)
        {
            var m = Regex.Match((matricula ?? "").Trim(), @"^z?s(\d{2})", RegexOptions.IgnoreCase);
            return m.Success ? 2000 + int.Parse(m.Groups[1].Value) : (int?)null;
        }

        // Número de periodo corrido desde el ingreso. El año del texto es el de TÉRMINO:
        // "Agosto - Enero 2027" va de agosto 2026 a enero 2027. El 1er periodo es el "Agosto - Enero"
        // del ingreso, el 2do el "Febrero - Julio" siguiente, y así sucesivamente.
        public static int? NumeroDePeriodo(int? anioIngreso, string? periodo)
        {
            var m = Regex.Match(periodo ?? "", @"(\d{4})");
            if (anioIngreso == null || !m.Success) return null;
            int anio = int.Parse(m.Groups[1].Value);
            return periodo!.TrimStart().StartsWith("ago", StringComparison.OrdinalIgnoreCase)
                ? (anio - 1 - anioIngreso.Value) * 2 + 1
                : (anio - anioIngreso.Value) * 2;
        }

        // Nombre del periodo número i (1, 2, 3...) de un alumno que ingresó en 'anioIngreso'
        public static string NombreDePeriodo(int anioIngreso, int i)
        {
            return i % 2 == 1 ? $"Agosto - Enero {anioIngreso + (i + 1) / 2}" : $"Febrero - Julio {anioIngreso + i / 2}";
        }

        // Periodo que REALMENTE cursa: los periodos corridos menos los periodos anteriores sin inscripción.
        // Null si la matrícula no trae año de ingreso.
        public static int? PeriodoQueCursa(string? matricula, string? periodo, IEnumerable<string> periodosSinInscripcion)
        {
            int? ingreso = AnioIngresoDeMatricula(matricula);
            int? corridos = NumeroDePeriodo(ingreso, periodo);
            if (corridos == null) return null;
            int pausas = periodosSinInscripcion.Select(p => NumeroDePeriodo(ingreso, p)).Count(n => n >= 1 && n < corridos);
            return corridos - pausas;
        }

        // Último periodo del plan (las AFEL no pertenecen a un periodo)
        public static int DuracionPlan(PlanInfo? plan)
        {
            return plan == null ? 0 : plan.Materias.Where(m => !m.EsAfel).Select(m => m.Semestre).DefaultIfEmpty(0).Max();
        }

        // Nombres de las materias que el alumno ya aprobó (en cualquier intento)
        public static HashSet<string> MateriasAprobadas(AlumnoAvance alumno)
        {
            return new HashSet<string>(alumno.Materias.Where(EstaAprobada).Select(m => m.Nombre ?? ""));
        }

        public static bool EstaAprobada(MateriaCursada m) => string.Equals(m.Estado, "aprobada", StringComparison.OrdinalIgnoreCase);

        // REZAGO AUTOMÁTICO: materias de periodos ANTERIORES al que cursa que todavía no aprueba.
        // Las AFEL no cuentan. Las optativas se cuentan por número: cada optativa aprobada (con el nombre
        // que sea del catálogo) cubre un lugar de optativa del plan, en orden.
        // Regresa las materias del plan (su nombre es el de la tarjeta del mapa).
        public static List<MateriaPlan> MateriasEnRezago(PlanInfo plan, int periodoQueCursa, HashSet<string> aprobadas)
        {
            var nombresOptativa = new HashSet<string>(plan.Optativas.Select(o => o.Nombre));
            foreach (var m in plan.Materias.Where(m => m.EsOptativa)) nombresOptativa.Add(m.Nombre);
            int optativasAprobadas = aprobadas.Count(a => nombresOptativa.Contains(a));

            var rezago = new List<MateriaPlan>();
            foreach (var m in plan.Materias.Where(m => m.Semestre >= 1 && m.Semestre < periodoQueCursa).OrderBy(m => m.Semestre))
            {
                if (m.EsAfel) continue;
                if (m.EsOptativa)
                {
                    if (optativasAprobadas > 0) optativasAprobadas--; else rezago.Add(m);
                    continue;
                }
                if (!aprobadas.Contains(m.Nombre)) rezago.Add(m);
            }
            return rezago;
        }

        // Todo lo anterior junto, para mostrarlo en pantalla (la pantalla no calcula nada de esto)
        public static SituacionAlumno Calcular(AlumnoAvance alumno, PlanInfo? plan, string? periodo)
        {
            var pausas = alumno.PeriodosSinInscripcion ?? new List<string>();
            int? ingreso = AnioIngresoDeMatricula(alumno.Matricula);
            int? corridos = NumeroDePeriodo(ingreso, periodo);
            int? cursa = PeriodoQueCursa(alumno.Matricula, periodo, pausas);

            var situacion = new SituacionAlumno
            {
                AnioIngreso = ingreso,
                PeriodosCorridos = corridos,
                PeriodoQueCursa = cursa,
                PausasDescontadas = (corridos ?? 0) - (cursa ?? 0),
                EnPausa = !string.IsNullOrEmpty(periodo) && pausas.Contains(periodo),
                DuracionPlan = DuracionPlan(plan)
            };

            if (ingreso != null && corridos >= 1)
                for (int i = 1; i <= corridos; i++) situacion.PeriodosDesdeIngreso.Add(NombreDePeriodo(ingreso.Value, i));

            if (plan != null && cursa >= 1)
                situacion.MateriasEnRezago = MateriasEnRezago(plan, cursa.Value, MateriasAprobadas(alumno)).Select(m => m.Nombre).ToList();

            return situacion;
        }
    }
}
