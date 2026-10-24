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

        // "Agosto - Enero 2027" o "Febrero - Julio 2026"
        public static bool EsPeriodoValido(string? periodo) =>
            periodo != null && Regex.IsMatch(periodo, @"^(Agosto - Enero|Febrero - Julio) (19|20)\d{2}$");

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

        // Créditos aprobados: cada materia aprobada cuenta una sola vez (con los créditos de su registro)
        public static int CreditosAprobados(AlumnoAvance alumno)
        {
            var aprobadas = new Dictionary<string, int>();
            foreach (var m in alumno.Materias.Where(EstaAprobada)) aprobadas[m.Nombre ?? ""] = m.Creditos;
            return aprobadas.Values.Sum();
        }

        // ---------- TRAYECTORIAS ESTÁNDAR Y LARGA ----------
        // Estándar: terminar en la duración del plan. Larga: el ritmo mínimo para terminar en el plazo máximo.
        // Los créditos esperados salen de cómo reparte el PLAN sus materias por periodo (no de partes iguales):
        // al concluir 6 periodos de un plan de 8, la estándar pide los créditos de las materias de los periodos 1 a 6.
        // Las AFEL no se exigen antes de terminar porque se pueden cursar en cualquier periodo.
        public static int PeriodosTrayectoriaLarga(PlanInfo? plan)
        {
            if (plan == null) return 0;
            return plan.PeriodosTrayectoriaLarga > 0 ? plan.PeriodosTrayectoriaLarga : (int)Math.Ceiling(DuracionPlan(plan) * 1.5);
        }

        public static int CreditosEsperados(PlanInfo plan, int periodosConcluidos, int periodosParaTerminar)
        {
            int duracion = DuracionPlan(plan);
            if (periodosParaTerminar <= 0 || periodosConcluidos <= 0 || duracion <= 0) return 0;

            // Periodo del plan al que equivale ir a ese ritmo (en la larga se avanza más despacio: 6 de 12 = periodo 4 de 8)
            double equivalente = periodosConcluidos * (double)duracion / periodosParaTerminar;
            // Ya debería haber terminado: se exige el total oficial del plan (hoja "Datos"), AFEL y optativas incluidas
            if (equivalente >= duracion) return Math.Max(plan.CreditosTotales, plan.Materias.Sum(m => m.Creditos));

            int completos = (int)Math.Floor(equivalente);
            double fraccion = equivalente - completos;
            var materias = plan.Materias.Where(m => !m.EsAfel).ToList();
            double esperados = materias.Where(m => m.Semestre >= 1 && m.Semestre <= completos).Sum(m => m.Creditos)
                             + fraccion * materias.Where(m => m.Semestre == completos + 1).Sum(m => m.Creditos);
            return (int)Math.Round(esperados, MidpointRounding.AwayFromZero);
        }

        // "estandar" (va al corriente), "debajo-estandar" (amarillo), "debajo-minimo" (rojo) o null si no aplica
        // (primer periodo, sin plan o matrícula sin año)
        public static string? EstadoTrayectoria(int creditosAprobados, int creditosEstandar, int creditosLarga, int periodosConcluidos)
        {
            if (periodosConcluidos < 1 || creditosEstandar <= 0) return null;
            if (creditosAprobados >= creditosEstandar) return "estandar";
            return creditosAprobados >= creditosLarga ? "debajo-estandar" : "debajo-minimo";
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

            situacion.CreditosAprobados = CreditosAprobados(alumno);
            if (plan != null)
            {
                situacion.CreditosTotales = plan.CreditosTotales;
                situacion.PeriodosTrayectoriaLarga = PeriodosTrayectoriaLarga(plan);
                if (cursa >= 1)
                {
                    int concluidos = cursa.Value - 1;
                    situacion.CreditosEstandar = CreditosEsperados(plan, concluidos, situacion.DuracionPlan);
                    situacion.CreditosLarga = CreditosEsperados(plan, concluidos, situacion.PeriodosTrayectoriaLarga);
                    situacion.Trayectoria = EstadoTrayectoria(situacion.CreditosAprobados, situacion.CreditosEstandar, situacion.CreditosLarga, concluidos);
                }
            }

            return situacion;
        }
    }
}
