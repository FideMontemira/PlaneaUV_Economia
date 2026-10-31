using System;
using System.Collections.Generic;
using System.Linq;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Caso de tutoría vigente de un alumno en un Periodo de Trabajo. Es la ÚNICA regla del caso: la usan el
    // semáforo, las alertas, la ficha y los reportes.
    //
    //   - El caso lo abre o cierra el tutor en cada sesión (Abierto / En atención / Cerrado).
    //   - Un caso abierto PASA a los periodos siguientes hasta que alguien lo cierre (una atención en CENDHIU
    //     suele durar más de un semestre). Un caso cerrado en un periodo anterior ya no se muestra.
    //   - Cada canalización del caso lleva su propio seguimiento (la última respuesta registrada):
    //     pendiente, "si" (acudió y sigue en atención), "no" (no acudió) o "concluida" (ya terminó).
    //     Una canalización concluida deja de pedir seguimiento; si se vuelve a canalizar, empieza de nuevo.
    public static class ReglasCaso
    {
        public class CasoTutoria
        {
            public string Estado { get; set; } = "Abierto";        // Abierto, En atención, Cerrado
            public bool Abierto { get; set; }
            public string Desde { get; set; } = "";                // periodo en que se abrió
            public List<CanalizacionCaso> Canalizaciones { get; set; } = new();
            public List<string> Situaciones { get; set; } = new();
            public TutoriaRecord? UltimoAcuerdo { get; set; }      // última sesión del caso con acuerdo
        }

        private static bool TieneEstado(TutoriaRecord t) => !string.IsNullOrWhiteSpace(t.EstadoCaso) || t.RequiereSeguimiento;
        private static bool EstaAbierto(TutoriaRecord t) => string.IsNullOrWhiteSpace(t.EstadoCaso) ? t.RequiereSeguimiento : t.EstadoCaso != "Cerrado";

        // Áreas a las que se canalizó en una sesión (las del catálogo y "Otra")
        public static IEnumerable<string> Areas(TutoriaRecord t)
        {
            foreach (var c in t.Canalizaciones ?? new List<string>()) yield return c;
            if (!string.IsNullOrWhiteSpace(t.OtraCanalizacion)) yield return t.OtraCanalizacion!.Trim();
        }

        // "CENDHIU=si" -> ("CENDHIU", "si")
        public static IEnumerable<(string area, string acudio)> Respuestas(TutoriaRecord t)
        {
            foreach (var r in t.AcudioCanalizaciones ?? new List<string>())
            {
                int i = r.LastIndexOf('=');
                if (i <= 0) continue;
                string valor = r[(i + 1)..].Trim().ToLowerInvariant();
                if (valor == "si" || valor == "no" || valor == "concluida") yield return (r[..i].Trim(), valor);
            }
        }

        // Null si no hay caso que mostrar en ese periodo
        public static CasoTutoria? Vigente(AlumnoAvance alumno, string? periodo)
        {
            int clave = ReglasTrayectoria.ClavePeriodo(periodo) ?? int.MaxValue;
            int Clave(TutoriaRecord t) => ReglasTrayectoria.ClavePeriodo(t.Periodo) ?? int.MinValue;

            // Sesiones hasta el periodo de trabajo, en orden en el tiempo
            var sesiones = (alumno.Tutorias ?? new List<TutoriaRecord>()).Where(t => Clave(t) <= clave)
                .OrderBy(Clave).ThenBy(t => t.Sesion).ToList();
            var conEstado = sesiones.Where(TieneEstado).ToList();
            if (conEstado.Count == 0) return null;

            var ultima = conEstado[^1];
            bool abierto = EstaAbierto(ultima);
            // Un caso que se cerró en un periodo anterior ya no forma parte de este periodo
            if (!abierto && ultima.Periodo != periodo) return null;

            // Inicio del caso: la primera sesión de la racha de sesiones "abiertas" que termina en la última
            int j = conEstado.Count - 1;
            while (j > 0 && EstaAbierto(conEstado[j - 1])) j--;
            var inicio = conEstado[j];
            // Sesiones del caso: desde que se abrió; si sigue abierto, también las posteriores (aunque no cambien el
            // estado, p. ej. la que solo registra si acudió); si se cerró, hasta la sesión que lo cerró
            var desdeElInicio = sesiones.Skip(sesiones.IndexOf(inicio));
            var delCaso = abierto ? desdeElInicio.ToList() : desdeElInicio.TakeWhile(t => t != ultima).Append(ultima).ToList();

            var caso = new CasoTutoria
            {
                Estado = string.IsNullOrWhiteSpace(ultima.EstadoCaso) ? "Abierto" : ultima.EstadoCaso!,
                Abierto = abierto,
                Desde = inicio.Periodo ?? "",
                Situaciones = delCaso.SelectMany(t => t.Situaciones ?? new List<string>()).Distinct().ToList(),
                UltimoAcuerdo = delCaso.LastOrDefault(t => !string.IsNullOrWhiteSpace(t.Acuerdos))
            };

            // Canalizaciones del caso y, de cada una, la última respuesta de si acudió
            foreach (var t in delCaso)
            {
                // Primero el seguimiento de lo canalizado antes; luego las canalizaciones nuevas de esta sesión
                foreach (var (area, acudio) in Respuestas(t))
                {
                    var c = caso.Canalizaciones.FirstOrDefault(x => x.Area == area);
                    if (c == null) caso.Canalizaciones.Add(c = new CanalizacionCaso { Area = area, Periodo = t.Periodo ?? "" });
                    c.Acudio = acudio;
                    c.ConcluidaEn = acudio == "concluida" ? t.Periodo : null;
                }
                foreach (var area in Areas(t))
                {
                    var c = caso.Canalizaciones.FirstOrDefault(x => x.Area == area);
                    if (c == null) caso.Canalizaciones.Add(new CanalizacionCaso { Area = area, Periodo = t.Periodo ?? "" });
                    else if (c.Acudio == "concluida") { c.Acudio = "pendiente"; c.Periodo = t.Periodo ?? ""; c.ConcluidaEn = null; }   // se volvió a canalizar
                }
            }
            return caso;
        }

        public static string TextoAcudio(string acudio) => acudio switch
        {
            "si" => "acudió, sigue en atención",
            "no" => "no acudió",
            "concluida" => "concluyó",
            _ => "pendiente de confirmar"
        };

        // Canalizaciones que todavía requieren seguimiento (las concluidas ya no)
        public static List<CanalizacionCaso> Activas(IEnumerable<CanalizacionCaso> canalizaciones) =>
            canalizaciones.Where(c => c.Acudio != "concluida").ToList();
    }
}
