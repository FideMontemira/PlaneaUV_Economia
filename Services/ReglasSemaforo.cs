using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Semáforo de riesgo de un alumno en un Periodo de Trabajo. Es la ÚNICA regla del semáforo:
    // el encabezado del alumno, el formato de tutoría y el panel del grupo muestran lo que calcula aquí.
    //
    //   Rojo:     alguna alerta grave (candidato a baja, excede el plan, 2da inscripción, candados), o
    //             tiene señales de riesgo y además faltó a tutoría (la inasistencia escala la alerta).
    //   Amarillo: cualquier otra alerta, o un acuerdo de tutoría con fecha de revisión vencida.
    //   Verde:    sin señales.
    //   "sin":    alumnos dados de baja (no se les calcula semáforo).
    //
    // El tutor puede confirmar, elevar o descartar el color del sistema (descartar exige motivo). Se guardan
    // los dos. Si después de la valoración el sistema detecta algo más grave, la valoración deja de aplicar.
    public static class ReglasSemaforo
    {
        public const int ScoreGrave = 75;

        public static int Nivel(string? color) => color switch { "rojo" => 3, "amarillo" => 2, "verde" => 1, _ => 0 };
        private static string Color(int nivel) => nivel >= 3 ? "rojo" : nivel == 2 ? "amarillo" : "verde";

        // 'alertas' son las del alumno en ese periodo (ServicioAlertas.Calcular). 'hoy' se recibe para poder probarlo.
        // 'criticas' (opcional): materias de alta reprobación; si el alumno cursa alguna se agrega una señal informativa.
        public static SemaforoAlumno Calcular(AlumnoAvance alumno, string? periodo, IEnumerable<Alerta> alertas, DateTime hoy,
                                              IEnumerable<MateriaCritica>? criticas = null)
        {
            var resultado = new SemaforoAlumno();
            var tutorias = alumno.Tutorias ?? new List<TutoriaRecord>();
            var delPeriodo = tutorias.Where(t => !string.IsNullOrEmpty(periodo) && t.Periodo == periodo).OrderBy(t => t.Sesion).ToList();

            // ---------- Seguimiento del caso (también se muestra a los alumnos de baja) ----------
            // El caso abierto en un periodo anterior sigue vigente hasta que se cierre (ReglasCaso)
            var caso = ReglasCaso.Vigente(alumno, periodo);
            resultado.EstadoCaso = caso?.Estado;
            resultado.CasoDesde = caso != null && caso.Abierto && caso.Desde != periodo ? caso.Desde : null;
            resultado.CanalizacionesCaso = caso?.Canalizaciones ?? new List<CanalizacionCaso>();

            var contactos = tutorias.Where(t => t.Asistencia == "asistio").Select(t => Fecha(t.Fecha)).Where(f => f.HasValue).Select(f => f!.Value).ToList();
            if (contactos.Count > 0)
            {
                var ultimo = contactos.Max();
                resultado.UltimoContacto = ultimo.ToString("yyyy-MM-dd");
                resultado.DiasSinContacto = Math.Max(0, (hoy.Date - ultimo).Days);
            }

            // Acuerdo pendiente: el último del caso abierto (aunque sea de un periodo anterior); sin caso, el último del periodo
            var conAcuerdo = caso != null ? (caso.Abierto ? caso.UltimoAcuerdo : null)
                                          : delPeriodo.LastOrDefault(t => !string.IsNullOrWhiteSpace(t.Acuerdos));
            if (conAcuerdo != null)
            {
                resultado.AcuerdoPendiente = conAcuerdo.Acuerdos!.Trim();
                resultado.FechaRevision = string.IsNullOrWhiteSpace(conAcuerdo.FechaRevision) ? null : conAcuerdo.FechaRevision;
                var revision = Fecha(conAcuerdo.FechaRevision);
                resultado.RevisionVencida = revision.HasValue && revision.Value < hoy.Date;
            }

            var situacion = alumno.Situacion ?? "";
            if (situacion != "Activo" && situacion != "")
            {
                resultado.Sistema = resultado.Final = "sin";
                return resultado;
            }

            // ---------- Color que calcula el sistema ----------
            var lista = alertas.ToList();
            // Las alertas por materia (sin etiqueta) se juntan y nombran la materia: "Riesgo Manual: A y B"
            var senales = lista.GroupBy(a => a.Mensaje).Select(g =>
            {
                var materias = g.Where(a => a.Etiqueta == null && a.Materia != "").Select(a => a.Materia).Distinct().ToList();
                return new SenalRiesgo { Texto = materias.Count > 0 ? $"{g.Key}: {Y(materias)}" : g.Key, Fuente = g.First().Fuente, Grave = g.Max(a => a.Score) >= ScoreGrave };
            }).ToList();
            bool faltoATutoria = lista.Any(a => a.Mensaje.StartsWith("Faltó a"));
            // Solo escalan las señales de riesgo académico; las de inscripción (carga menor, sin materias registradas)
            // suelen ser de captura y no bastan para pasar a rojo
            bool otrasSenales = lista.Any(a => !a.Mensaje.StartsWith("Faltó a") && a.Fuente != ServicioAlertas.Inscripcion);

            int nivel = 1;
            if (lista.Any(a => a.Score >= ScoreGrave)) nivel = 3;
            else if (lista.Count > 0) nivel = 2;

            if (resultado.RevisionVencida)
            {
                senales.Add(new SenalRiesgo { Texto = $"Acuerdo con revisión vencida ({FechaCorta(resultado.FechaRevision)})", Fuente = "Tutoría" });
                nivel = Math.Max(nivel, 2);
            }
            if (faltoATutoria && (otrasSenales || resultado.RevisionVencida) && nivel < 3)
            {
                senales.Add(new SenalRiesgo { Texto = "No asistió a tutoría teniendo señales de riesgo", Fuente = "Tutoría", Grave = true });
                nivel = 3;
            }

            // ---------- Señales informativas (no cambian el color) ----------
            // Lo que registró el tutor en las sesiones del periodo. Lo confidencial se muestra sin detalle.
            var problematicas = delPeriodo.SelectMany(t => t.Situaciones ?? new List<string>()).Distinct().ToList();
            foreach (var p in problematicas.Where(p => !CatalogoTutorias.EsPrivada(p)))
                senales.Add(new SenalRiesgo { Texto = $"{p} (registrado por su tutor)", Fuente = "Reporte de tutoría", Informativa = true });
            if (problematicas.Any(CatalogoTutorias.EsPrivada))
                senales.Add(new SenalRiesgo { Texto = "Situación personal o de salud registrada (confidencial)", Fuente = "Reporte de tutoría", Informativa = true });
            foreach (var c in resultado.CanalizacionesCaso.Where(c => c.Acudio == "no"))
                senales.Add(new SenalRiesgo { Texto = $"No acudió a {c.Area} (canalizado en {c.Periodo})", Fuente = "Reporte de tutoría", Informativa = true });

            // Cursa una materia con alta reprobación
            var criticasQueCursa = new List<MateriaCritica>();
            if (criticas != null && !string.IsNullOrEmpty(periodo))
            {
                var cursando = new HashSet<string>(alumno.Materias.Where(m => m.Periodo == periodo && string.Equals(m.Estado, "cursando", StringComparison.OrdinalIgnoreCase))
                    .Select(m => (m.Nombre ?? "").Trim()), StringComparer.OrdinalIgnoreCase);
                criticasQueCursa = criticas.Where(c => c.Critica && cursando.Contains(c.Materia)).ToList();
                foreach (var c in criticasQueCursa)
                    senales.Add(new SenalRiesgo { Texto = $"Cursa {c.Materia}, materia crítica ({c.Porcentaje} % de reprobación)", Fuente = "Estadística del grupo", Informativa = true });
            }

            // Sin repetir el mismo mensaje (p. ej. dos materias en "Riesgo Manual")
            resultado.Senales = senales.GroupBy(s => s.Texto).Select(g => g.First())
                .OrderByDescending(s => s.Grave).ThenBy(s => s.Informativa).ToList();
            resultado.Sistema = Color(nivel);
            resultado.TemasSugeridos = TemasSugeridos(lista, resultado, problematicas, delPeriodo, criticasQueCursa);

            // ---------- Valoración del tutor ----------
            // La última sesión del periodo con una valoración válida (descartar sin motivo no cuenta)
            var valorada = delPeriodo.LastOrDefault(t => !string.IsNullOrWhiteSpace(t.ValoracionRiesgo)
                && (t.ValoracionRiesgo != "descarta" || !string.IsNullOrWhiteSpace(t.MotivoValoracion)));

            resultado.Final = resultado.Sistema;
            if (valorada != null)
            {
                resultado.Valoracion = valorada.ValoracionRiesgo;
                resultado.MotivoValoracion = string.IsNullOrWhiteSpace(valorada.MotivoValoracion) ? null : valorada.MotivoValoracion!.Trim();

                int alValorar = Nivel(valorada.SemaforoSistema);   // 0 si no se guardó qué marcaba el sistema
                switch (valorada.ValoracionRiesgo)
                {
                    case "eleva":
                        // Un nivel arriba de lo que marcaba el sistema al valorar; si después el sistema subió más, manda el sistema.
                        // (Antes, el propio reporte del tutor podía subir el color y la valoración dejaba de contar.)
                        resultado.Final = Color(Math.Min(3, Math.Max(nivel, (alValorar > 0 ? alValorar : nivel) + 1)));
                        break;
                    case "descarta":
                        // Si después de descartar aparecieron señales más graves, el descarte ya no aplica
                        if (alValorar > 0 && nivel > alValorar) resultado.ValoracionDesactualizada = true;
                        else resultado.Final = "verde";
                        break;
                }
            }
            return resultado;
        }

        // Temas para conversar en la siguiente sesión, a partir de las señales. Nombran la materia cuando la hay.
        private static List<string> TemasSugeridos(List<Alerta> alertas, SemaforoAlumno s, List<string> problematicas,
                                                   List<TutoriaRecord> sesiones, List<MateriaCritica> criticas)
        {
            var temas = new List<string>();
            void Si(bool condicion, string tema) { if (condicion && !temas.Contains(tema)) temas.Add(tema); }
            // Materias de las alertas que empiezan con 'inicio' (p. ej. todas las "Riesgo Manual"), sin repetir
            string Materias(string inicio) => Y(alertas.Where(a => a.Mensaje.StartsWith(inicio)).Select(a => a.Materia).Where(m => m != "").Distinct().ToList());
            string Detalle(string inicio) => (alertas.FirstOrDefault(a => a.Mensaje.StartsWith(inicio))?.Materia ?? "").TrimEnd('.');
            string Minuscula(string texto) => texto.Length > 0 ? char.ToLower(texto[0]) + texto.Substring(1) : texto;
            int Cuantas(string inicio) => alertas.Where(a => a.Mensaje.StartsWith(inicio)).Select(a => a.Materia).Distinct().Count();
            bool Hay(string inicio) => alertas.Any(a => a.Mensaje.StartsWith(inicio));
            bool Registro(string problematica) => problematicas.Contains(problematica);

            // ----- Lo que detectó el sistema -----
            Si(Hay("Candidato a Baja"), $"Revisar con la coordinación su situación reglamentaria en {Materias("Candidato a Baja")} (agotó sus oportunidades) y qué opciones tiene.");
            Si(Hay("En 2da Inscripción"), $"Explicar qué implica cursar {Materias("En 2da Inscripción")} en segunda inscripción y armar un plan semanal de estudio con asesoría.");
            Si(Hay("Excede la duración"), "Plan para concluir dentro del plazo máximo: qué materias le faltan y en qué periodos cursarlas.");
            Si(Hay("Debajo del mínimo") || Hay("Debajo de la trayectoria"),
                $"Revisar su avance frente a la trayectoria ({Minuscula(Detalle("Debajo"))}) y qué materias le conviene priorizar el siguiente periodo.");
            Si(Hay("Candados pendientes"), $"Priorizar las materias seriadas que le impiden avanzar: {Detalle("Candados pendientes")}.");
            Si(Hay("Rezago"), $"Armar un plan para regularizar las materias atrasadas ({Detalle("Rezago")}); se puede apoyar en «Sugerir carga».");
            Si(Hay("Riesgo Manual"), $"Preguntar cómo va en {Materias("Riesgo Manual")} y ofrecer asesoría (de pares o PAFI) antes del siguiente parcial.");
            Si(Hay("Reprobada"), $"Revisar qué le funcionó y qué no en {Materias("Reprobada")}; confirmar si ya {(Cuantas("Reprobada") > 1 ? "las" : "la")} volvió a inscribir o cuándo lo hará.");
            Si(Hay("Carga menor"), "Revisar por qué inscribió menos materias y si su carga es compatible con su trabajo u otras responsabilidades.");
            Si(Hay("Sin materias registradas"), "Confirmar si está inscrito este periodo o si necesita una baja temporal.");
            Si(Hay("Faltó a"), "Preguntar por qué no asistió a tutoría y acordar un medio de contacto que sí use.");
            foreach (var c in criticas)
                Si(true, $"Preguntar cómo va en {c.Materia} (reprueba el {c.Porcentaje} % de quienes la cursan) y ofrecer asesoría antes de los parciales.");

            // ----- Lo que registró el tutor -----
            var materiaDificultad = sesiones.Select(t => t.MateriaDificultad).LastOrDefault(m => !string.IsNullOrWhiteSpace(m));
            Si(Registro("Dificultad en una materia"), materiaDificultad != null
                ? $"Revisar cómo va en {materiaDificultad} y si ya acudió a la asesoría acordada."
                : "Revisar cómo va en la materia con la que tiene dificultad y si ya acudió a asesoría.");
            Si(Registro("Bases previas insuficientes (matemáticas, lectura o redacción)"), "Identificar qué temas base le faltan y canalizar a asesoría o nivelación.");
            Si(Registro("Inasistencia a clases") || Registro("No entrega trabajos o tareas"), "Explorar la causa de las faltas o de los trabajos sin entregar (horario, trabajo, transporte, motivación).");
            Si(Registro("Trabaja más de 20 horas a la semana") || Registro("Laboral (trabaja y estudia)"), "Revisar su horario de trabajo y su carga académica; valorar una beca o un ajuste de carga.");
            Si(Registro("Económica") || Registro("Requiere o perdió una beca"), "Revisar las convocatorias de becas y apoyos vigentes y si ya hizo la solicitud.");
            Si(Registro("Traslape de horarios o no encontró cupo"), "Revisar con la coordinación del programa opciones de horario o cupo para el siguiente periodo.");
            Si(Registro("Duda vocacional o cambio de carrera"), "Conversar sobre su proyecto vocacional y, si lo considera, el procedimiento de cambio de programa.");
            Si(problematicas.Any(CatalogoTutorias.EsPrivada), "Dar seguimiento, con discreción, a la situación personal registrada y a su canalización.");
            // Seguimiento de las canalizaciones del caso
            Si(s.EstadoCaso != null && s.EstadoCaso != "Cerrado" && s.CanalizacionesCaso.Count > 0 && ReglasCaso.Activas(s.CanalizacionesCaso).Count == 0,
               "Todas las canalizaciones concluyeron: valorar si ya se puede cerrar el caso.");
            foreach (var c in s.CanalizacionesCaso)
            {
                Si(c.Acudio == "no", $"Preguntar por qué no acudió a {c.Area} y valorar volver a canalizarlo o buscar otra opción.");
                Si(c.Acudio == "pendiente", $"Confirmar si ya acudió a {c.Area} (canalizado en {c.Periodo}).");
            }
            Si(Hay("Requiere seguimiento"), "Revisar cómo va la canalización y los acuerdos de la sesión anterior.");
            Si(s.AcuerdoPendiente != null, $"Revisar el acuerdo: «{s.AcuerdoPendiente}»" + (s.FechaRevision != null ? $" (revisión: {FechaCorta(s.FechaRevision)})." : "."));
            Si(temas.Count == 0, "Seguimiento habitual: cómo va en sus materias, servicio social o prácticas, y sus planes para el siguiente periodo.");
            return temas;
        }

        // "A", "A y B", "A, B y C"
        private static string Y(List<string> nombres) =>
            nombres.Count == 0 ? "la materia señalada" : nombres.Count == 1 ? nombres[0] : string.Join(", ", nombres.Take(nombres.Count - 1)) + " y " + nombres[^1];

        private static DateTime? Fecha(string? texto) =>
            DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date
            : DateTime.TryParse(texto, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out d) ? d.Date : null;

        private static string FechaCorta(string? texto) => Fecha(texto)?.ToString("dd/MM/yyyy") ?? texto ?? "";
    }
}
