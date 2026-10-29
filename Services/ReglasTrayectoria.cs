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

        // Número para ordenar periodos en el tiempo: "Febrero - Julio 2026" < "Agosto - Enero 2027" < "Febrero - Julio 2027".
        // Null si el texto no trae año.
        public static int? ClavePeriodo(string? periodo)
        {
            var m = Regex.Match(periodo ?? "", @"(\d{4})");
            if (!m.Success) return null;
            int anio = int.Parse(m.Groups[1].Value);
            return periodo!.TrimStart().StartsWith("ago", StringComparison.OrdinalIgnoreCase) ? (anio - 1) * 2 + 1 : anio * 2;
        }

        // Último día del periodo (31 de enero o 31 de julio del año del texto)
        public static DateTime? FinDePeriodo(string? periodo)
        {
            var m = Regex.Match(periodo ?? "", @"(\d{4})");
            if (!m.Success) return null;
            int anio = int.Parse(m.Groups[1].Value);
            return periodo!.TrimStart().StartsWith("ago", StringComparison.OrdinalIgnoreCase) ? new DateTime(anio, 1, 31) : new DateTime(anio, 7, 31);
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
        public static bool EstaCursando(MateriaCursada m) => string.Equals(m.Estado, "cursando", StringComparison.OrdinalIgnoreCase);

        // ---------- MATERIAS DE VARIOS PERIODOS ----------
        // Algunas materias duran más de un periodo (columna H de la plantilla; p. ej. servicio social: 2).
        // Si se inscribió en un periodo anterior y su último registro sigue "cursando" dentro de su duración,
        // en 'periodo' la sigue cursando aunque no tenga un registro nuevo: no es rezago ni falta de inscripción.
        public static HashSet<string> EnCursoDesdePeriodoAnterior(PlanInfo? plan, AlumnoAvance alumno, string? periodo)
        {
            var resultado = new HashSet<string>();
            int? clave = ClavePeriodo(periodo);
            if (plan == null || clave == null) return resultado;
            foreach (var m in plan.Materias.Where(m => m.PeriodosDuracion > 1))
            {
                int? inicio = InicioDelCursoAbierto(alumno, m.Nombre, clave.Value);
                if (inicio != null && inicio < clave && clave - inicio < m.PeriodosDuracion) resultado.Add(m.Nombre);
            }
            return resultado;
        }

        // Periodo (clave) en que empezó el curso que sigue abierto en 'clave': los registros "cursando" seguidos,
        // del más reciente hacia atrás, hasta ese periodo. Null si el último registro hasta 'clave' no es "cursando".
        private static int? InicioDelCursoAbierto(AlumnoAvance alumno, string nombre, int clave)
        {
            var registros = alumno.Materias
                .Where(r => r.Nombre == nombre && ClavePeriodo(r.Periodo) is int k && k <= clave)
                .OrderBy(r => ClavePeriodo(r.Periodo)).ToList();
            int? inicio = null;
            for (int i = registros.Count - 1; i >= 0; i--)
            {
                if (!EstaCursando(registros[i])) break;
                int k = ClavePeriodo(registros[i].Periodo)!.Value;
                if (inicio != null && inicio - k > 1) break;   // hay un periodo de por medio: es otro curso
                inicio = k;
            }
            return inicio;
        }

        // Lo que no se cuenta como rezago: lo aprobado y lo que sigue cursando desde un periodo anterior
        public static HashSet<string> NoCuentanComoRezago(PlanInfo plan, AlumnoAvance alumno, string? periodo)
        {
            var nombres = MateriasAprobadas(alumno);
            nombres.UnionWith(EnCursoDesdePeriodoAnterior(plan, alumno, periodo));
            return nombres;
        }

        // Nombre del periodo con esa clave (la inversa de ClavePeriodo)
        public static string PeriodoDeClave(int clave) =>
            clave % 2 == 1 ? $"Agosto - Enero {(clave - 1) / 2 + 1}" : $"Febrero - Julio {clave / 2}";

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
                situacion.MateriasEnRezago = MateriasEnRezago(plan, cursa.Value, NoCuentanComoRezago(plan, alumno, periodo)).Select(m => m.Nombre).ToList();
            if (plan != null) situacion.EnCursoDesdeAntes = EnCursoDesdePeriodoAnterior(plan, alumno, periodo).OrderBy(n => n).ToList();

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
                situacion.RequisitosCreditos = RequisitosDeCreditos(plan, alumno, situacion.CreditosAprobados);
            }

            return situacion;
        }

        // ---------- REQUISITO DE CRÉDITOS ----------
        // Algunas materias (servicio social, experiencia recepcional, prácticas profesionales) solo se inscriben
        // con cierto porcentaje de los créditos totales aprobado. El porcentaje sale de la columna G de la plantilla.
        // 70 % de 369 = 258.3: se piden 259 (no basta con estar "casi" en el porcentaje).
        public static int CreditosParaRequisito(PlanInfo plan, int porcentaje) =>
            porcentaje <= 0 ? 0 : (int)Math.Ceiling(plan.CreditosTotales * porcentaje / 100.0 - 1e-9);

        public static bool CumpleRequisitoCreditos(PlanInfo plan, MateriaPlan materia, int creditosAprobados) =>
            materia.PorcentajeCreditos <= 0 || plan.CreditosTotales <= 0 || creditosAprobados >= CreditosParaRequisito(plan, materia.PorcentajeCreditos);

        public static List<RequisitoCreditos> RequisitosDeCreditos(PlanInfo plan, AlumnoAvance alumno, int creditosAprobados)
        {
            var inscritas = new HashSet<string>(alumno.Materias
                .Where(m => EstaAprobada(m) || string.Equals(m.Estado, "cursando", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Nombre ?? ""));
            return plan.Materias.Where(m => m.PorcentajeCreditos > 0 && plan.CreditosTotales > 0).Select(m =>
            {
                int necesarios = CreditosParaRequisito(plan, m.PorcentajeCreditos);
                return new RequisitoCreditos
                {
                    Materia = m.Nombre, Porcentaje = m.PorcentajeCreditos, CreditosNecesarios = necesarios,
                    Faltan = Math.Max(0, necesarios - creditosAprobados), Cumple = creditosAprobados >= necesarios,
                    YaInscrita = inscritas.Contains(m.Nombre)
                };
            }).ToList();
        }

        // Aviso para el tutor (no cambia el color del semáforo): ya cubre el porcentaje y aún no las inscribe
        public static SenalRiesgo? SenalRequisitosCumplidos(IEnumerable<RequisitoCreditos> requisitos)
        {
            var pendientes = requisitos.Where(r => r.Cumple && !r.YaInscrita).ToList();
            if (pendientes.Count == 0) return null;
            var nombres = pendientes.Select(r => r.Materia).ToList();
            string lista = nombres.Count == 1 ? nombres[0] : string.Join(", ", nombres.Take(nombres.Count - 1)) + " y " + nombres[^1];
            int porcentaje = pendientes.Min(r => r.Porcentaje);
            return new SenalRiesgo { Texto = $"Ya cubre el {porcentaje} % de créditos: puede inscribir {lista}", Fuente = "Avance en créditos", Informativa = true };
        }
    }
}
