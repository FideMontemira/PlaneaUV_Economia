using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Arma el reporte de tutoría académica (HTML que Word abre como .doc).
    // Por cada periodo: un resumen (sesiones, situaciones detectadas, canalizaciones, seguimiento) y, por cada
    // sesión con información, el formato guiado con TODAS las opciones del catálogo como casillas marcadas o vacías,
    // para que se lea como un formato oficial. Las sesiones vacías no se imprimen.
    public static class GeneradorDocumentoTutorias
    {
        private const string Marcada = "&#9746;";  // ☒
        private const string Vacia = "&#9744;";    // ☐

        public static string Generar(AlumnoAvance alumno, string programa)
        {
            var html = new StringBuilder();
            html.Append($@"<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
<head><meta charset='utf-8'><title>Reporte de Tutoría Académica</title>
<style>
body {{ font-family: Calibri, Arial, sans-serif; font-size: 11pt; color: #1c2633; }}
h1 {{ font-size: 18pt; color: #0f2c55; margin: 0 0 4pt; }}
h2 {{ font-size: 13pt; color: #ffffff; background: #18529d; padding: 4pt 8pt; margin: 18pt 0 8pt; }}
h3 {{ font-size: 12pt; color: #0f2c55; border-bottom: 1pt solid #18529d; margin: 14pt 0 6pt; }}
table {{ border-collapse: collapse; width: 100%; margin-bottom: 8pt; }}
td, th {{ border: 1pt solid #9fb3ce; padding: 4pt 6pt; vertical-align: top; font-size: 10.5pt; }}
th {{ background: #e6eef8; text-align: left; width: 26%; }}
table.casillas td {{ border: none; padding: 1pt 4pt; width: 50%; }}
.etiqueta {{ font-weight: bold; color: #18529d; margin: 8pt 0 2pt; }}
.caja {{ border: 1pt solid #9fb3ce; padding: 5pt 7pt; min-height: 30pt; }}
.suave {{ color: #5a6778; }}
.alerta {{ color: #b42318; font-weight: bold; }}
</style></head><body>
<h1>Reporte de Tutoría Académica</h1>
<p class='suave'>{E(programa)}</p>
<table>
<tr><th>Estudiante</th><td>{E(alumno.Nombre)}</td></tr>
<tr><th>Matrícula</th><td>{E(alumno.Matricula)}</td></tr>
<tr><th>Fecha de emisión</th><td>{DateTime.Now:dd/MM/yyyy}</td></tr>
</table>");

            var periodos = alumno.Tutorias.Where(TieneInformacion).GroupBy(t => t.Periodo ?? "").ToList();
            if (periodos.Count == 0)
                html.Append("<p class='suave'>No hay sesiones de tutoría registradas.</p>");

            foreach (var periodo in periodos)
            {
                var sesiones = periodo.OrderBy(s => s.Sesion).ToList();
                html.Append($"<h2>Periodo: {E(periodo.Key)}</h2>");

                // ---------- Resumen del periodo ----------
                int realizadas = sesiones.Count(s => s.Asistencia == "asistio");
                var situaciones = sesiones.SelectMany(s => s.Situaciones ?? new List<string>()).Distinct().ToList();
                var canalizaciones = sesiones.SelectMany(Canalizaciones).Distinct().ToList();
                string? estadoCaso = EstadoDelCaso(sesiones);
                bool seguimiento = estadoCaso == "Abierto" || estadoCaso == "En atención";

                html.Append("<table>");
                html.Append($"<tr><th>Sesiones con asistencia</th><td>{realizadas} de {sesiones.Count} registradas</td></tr>");
                html.Append($"<tr><th>Situaciones detectadas</th><td>{(situaciones.Count == 0 ? "<span class='suave'>Ninguna</span>" : string.Join(", ", situaciones.Select(E)))}</td></tr>");
                html.Append($"<tr><th>Canalizado a</th><td>{(canalizaciones.Count == 0 ? "<span class='suave'>Sin canalización</span>" : string.Join(", ", canalizaciones.Select(E)))}</td></tr>");
                html.Append($"<tr><th>Requiere seguimiento</th><td>{(seguimiento ? "<span class='alerta'>Sí</span>" : "No")}</td></tr>");
                html.Append($"<tr><th>Estado del caso</th><td>{(estadoCaso == null ? "<span class='suave'>Sin caso abierto</span>" : E(estadoCaso))}</td></tr>");
                html.Append("</table>");

                // ---------- Cada sesión ----------
                foreach (var s in sesiones)
                {
                    html.Append($"<h3>Sesión {s.Sesion}</h3>");
                    html.Append("<table>");
                    html.Append($"<tr><th>Fecha</th><td>{(string.IsNullOrWhiteSpace(s.Fecha) ? "<span class='suave'>Sin fecha</span>" : E(FormatoFecha(s.Fecha)))}</td>");
                    html.Append($"<th>Asistencia</th><td>{Asistencia(s.Asistencia)}</td></tr>");
                    html.Append($"<tr><th>Modalidad</th><td colspan='3'>{(string.IsNullOrWhiteSpace(s.Modalidad) ? "<span class='suave'>No indicada</span>" : E(s.Modalidad))}</td></tr>");
                    html.Append("</table>");

                    if (!string.IsNullOrWhiteSpace(s.ValoracionRiesgo))
                    {
                        html.Append("<p class='etiqueta'>Valoración del riesgo</p><table>");
                        html.Append($"<tr><th>Semáforo del sistema</th><td>{E(NombreColor(s.SemaforoSistema))}</td></tr>");
                        html.Append($"<tr><th>Valoración del tutor</th><td>{Opcion(s.ValoracionRiesgo, "confirma", "Confirma")} &nbsp; {Opcion(s.ValoracionRiesgo, "eleva", "El riesgo es mayor")} &nbsp; {Opcion(s.ValoracionRiesgo, "descarta", "No hay riesgo")}</td></tr>");
                        if (!string.IsNullOrWhiteSpace(s.MotivoValoracion)) html.Append($"<tr><th>Motivo</th><td>{E(s.MotivoValoracion)}</td></tr>");
                        html.Append("</table>");
                    }

                    html.Append("<p class='etiqueta'>Temas tratados</p>");
                    html.Append(Casillas(CatalogoTutorias.Temas, s.Temas));
                    html.Append("<p class='etiqueta'>Problemáticas detectadas</p>");
                    var otras = new List<string>(s.Situaciones ?? new List<string>());
                    foreach (var categoria in CatalogoTutorias.Problematicas)
                    {
                        html.Append($"<p class='suave' style='margin:4pt 0 0'><b>{E(categoria.Nombre)}</b>{(categoria.Privada ? " (confidencial: solo tutor y coordinación de tutorías)" : "")}</p>");
                        html.Append(Casillas(categoria.Opciones, (s.Situaciones ?? new List<string>()).Where(categoria.Opciones.Contains).ToList()));
                        otras.RemoveAll(categoria.Opciones.Contains);
                    }
                    if (otras.Count > 0) html.Append(Casillas(new List<string>(), otras));   // opciones que ya no están en el catálogo
                    if (!string.IsNullOrWhiteSpace(s.MateriaDificultad))
                        html.Append($"<p><b>Materia con dificultad:</b> {E(s.MateriaDificultad)}</p>");
                    html.Append("<p class='etiqueta'>Canalización</p>");
                    html.Append(Casillas(CatalogoTutorias.Canalizaciones, s.Canalizaciones));
                    if (!string.IsNullOrWhiteSpace(s.OtraCanalizacion))
                        html.Append($"<p>{Marcada} Otra: {E(s.OtraCanalizacion)}</p>");
                    var respuestas = ReglasCaso.Respuestas(s).ToList();
                    if (respuestas.Count > 0)
                        html.Append($"<p><b>Seguimiento de canalizaciones:</b> {string.Join(" · ", respuestas.Select(r => E(r.area) + ": " + ReglasCaso.TextoAcudio(r.acudio)))}</p>");

                    html.Append("<p class='etiqueta'>Acuerdo principal</p>");
                    html.Append($"<div class='caja'>{Parrafos(s.Acuerdos)}</div>");
                    if (!string.IsNullOrWhiteSpace(s.FechaRevision))
                        html.Append($"<p><b>Revisar el:</b> {E(FormatoFecha(s.FechaRevision))}</p>");
                    string estado = string.IsNullOrWhiteSpace(s.EstadoCaso) ? (s.RequiereSeguimiento ? "Abierto" : "") : s.EstadoCaso!;
                    html.Append($"<p><b>Estado del caso:</b> {string.Join(" &nbsp; ", CatalogoTutorias.EstadosCaso.Select(e => Opcion(estado, e, e)))}</p>");
                }

                // ---------- Notas adicionales de todas las sesiones del periodo, en un solo recuadro ----------
                html.Append("<h3>Notas adicionales del periodo</h3>");
                html.Append($"<div class='caja'>{NotasDelPeriodo(sesiones)}</div>");
            }

            html.Append("<p class='suave' style='font-size:9pt'>" + LeyendaPrivacidad + "</p>");
            html.Append("<br/><br/><table style='border: none; text-align: center; margin-top: 40px;'><tr><td style='border: none; width: 50%;'>_________________________<br/>Firma del tutor</td><td style='border: none; width: 50%;'>_________________________<br/>Firma del estudiante</td></tr></table></body></html>");
            return html.ToString();
        }

        public const string LeyendaPrivacidad = "Documento confidencial para uso del tutor y de la coordinación de tutorías. Contiene datos personales tratados conforme a la Ley General de Protección de Datos Personales en Posesión de Sujetos Obligados (LGPDPPSO).";

        // Una sesión se imprime solo si tiene algo capturado
        public static bool TieneInformacion(TutoriaRecord t) =>
            !string.IsNullOrWhiteSpace(t.Fecha) || (t.Asistencia ?? "pendiente") != "pendiente" || !string.IsNullOrWhiteSpace(t.Comentarios)
            || !string.IsNullOrWhiteSpace(t.Modalidad) || (t.Temas?.Count ?? 0) > 0 || (t.Situaciones?.Count ?? 0) > 0
            || (t.Canalizaciones?.Count ?? 0) > 0 || !string.IsNullOrWhiteSpace(t.OtraCanalizacion)
            || !string.IsNullOrWhiteSpace(t.Acuerdos) || t.RequiereSeguimiento || !string.IsNullOrWhiteSpace(t.MateriaDificultad)
            || !string.IsNullOrWhiteSpace(t.FechaRevision) || !string.IsNullOrWhiteSpace(t.EstadoCaso) || !string.IsNullOrWhiteSpace(t.ValoracionRiesgo)
            || (t.AcudioCanalizaciones?.Count ?? 0) > 0;

        // Estado del caso según la última sesión que lo indica (los registros anteriores solo tenían "requiere seguimiento")
        private static string? EstadoDelCaso(List<TutoriaRecord> sesiones)
        {
            var ultima = sesiones.LastOrDefault(t => !string.IsNullOrWhiteSpace(t.EstadoCaso) || t.RequiereSeguimiento);
            return ultima == null ? null : string.IsNullOrWhiteSpace(ultima.EstadoCaso) ? "Abierto" : ultima.EstadoCaso;
        }

        private static string Opcion(string? elegido, string valor, string texto) => $"{(elegido == valor ? Marcada : Vacia)} {E(texto)}";

        private static string NombreColor(string? color) =>
            color == "rojo" ? "Rojo (riesgo alto)" : color == "amarillo" ? "Amarillo (atención)" : color == "verde" ? "Verde (sin riesgo)" : "No registrado";

        private static IEnumerable<string> Canalizaciones(TutoriaRecord t)
        {
            foreach (var c in t.Canalizaciones ?? new List<string>()) yield return c;
            if (!string.IsNullOrWhiteSpace(t.OtraCanalizacion)) yield return t.OtraCanalizacion!.Trim();
        }

        // Todas las opciones del catálogo en dos columnas, marcadas las elegidas.
        // Una opción guardada que ya no esté en el catálogo también se imprime (marcada), para no perderla.
        private static string Casillas(IReadOnlyList<string> catalogo, List<string>? elegidas)
        {
            var marcadas = new HashSet<string>(elegidas ?? new List<string>());
            var opciones = catalogo.Concat(marcadas.Where(m => !catalogo.Contains(m))).ToList();
            var sb = new StringBuilder("<table class='casillas'>");
            for (int i = 0; i < opciones.Count; i += 2)
            {
                sb.Append("<tr>");
                for (int k = i; k < i + 2; k++)
                    sb.Append(k < opciones.Count ? $"<td>{(marcadas.Contains(opciones[k]) ? Marcada : Vacia)} {E(opciones[k])}</td>" : "<td></td>");
                sb.Append("</tr>");
            }
            return sb.Append("</table>").ToString();
        }

        // Junta los comentarios de las sesiones del periodo: cada uno con su sesión (y fecha, si la tiene)
        public static string NotasDelPeriodo(IEnumerable<TutoriaRecord> sesiones)
        {
            var notas = sesiones.Where(s => !string.IsNullOrWhiteSpace(s.Comentarios)).OrderBy(s => s.Sesion).ToList();
            if (notas.Count == 0) return "<span class='suave'>Sin notas adicionales.</span>";
            return string.Join("<br/><br/>", notas.Select(s =>
            {
                string fecha = string.IsNullOrWhiteSpace(s.Fecha) ? "" : $" ({E(FormatoFecha(s.Fecha))})";
                return $"<b>Sesión {s.Sesion}{fecha}:</b> {Parrafos(s.Comentarios)}";
            }));
        }

        private static string Parrafos(string? texto) =>
            string.IsNullOrWhiteSpace(texto) ? "<span class='suave'>Sin registro.</span>" : E(texto).Replace("\r\n", "\n").Replace("\n", "<br/>");

        // "2026-09-10" -> "10/09/2026"
        private static string FormatoFecha(string fecha) =>
            DateTime.TryParse(fecha, out var d) ? d.ToString("dd/MM/yyyy") : fecha;

        private static string Asistencia(string? valor) =>
            valor switch
            {
                "asistio" => "Asistió",
                "falto" => "No asistió",
                "justificado" => "No asistió (justificado)",
                "citado" => "Citado",
                "reprogramada" => "Reprogramada",
                _ => "Sin citar todavía"
            };

        // Escapa solo los caracteres especiales de HTML (los acentos se dejan tal cual)
        private static string E(string? texto) =>
            (texto ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }
}
