using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Ficha para llevar a la sesión de tutoría: lo que el tutor necesita saber ANTES de la sesión, en una hoja.
    // Por qué conviene atenderlo, su trayectoria, sus materias del periodo, temas sugeridos, apoyos disponibles,
    // historial y líneas para notas. Es una página HTML que se imprime o se guarda en PDF desde el navegador.
    // No calcula reglas: recibe la situación ya calculada (ReglasTrayectoria, ReglasSemaforo, ReglasMateriasCriticas).
    public static class GeneradorFichaTutoria
    {
        private static readonly Dictionary<string, (string texto, string color, string fondo)> Colores = new()
        {
            ["rojo"] = ("Riesgo alto", "#b42318", "#fde8e7"),
            ["amarillo"] = ("Atención", "#8a5a00", "#fff6db"),
            ["verde"] = ("Sin riesgo", "#1a7f45", "#e5f6ec"),
            ["sin"] = ("Sin semáforo (baja)", "#6c757d", "#f1f3f5"),
        };

        public static string Generar(AlumnoAvance alumno, PlanInfo? plan, SituacionAlumno s, string periodo, DateTime hoy)
        {
            var sem = s.Semaforo ?? new SemaforoAlumno();
            var (textoColor, color, fondo) = Colores.TryGetValue(sem.Final, out var c) ? c : Colores["sin"];
            string programa = plan?.Programa ?? (alumno.PlanEstudio ?? "").Replace('_', ' ');

            var sesionesPeriodo = alumno.Tutorias.Where(t => t.Periodo == periodo && GeneradorDocumentoTutorias.TieneInformacion(t)).OrderBy(t => t.Sesion).ToList();
            int realizadas = sesionesPeriodo.Count(t => t.Asistencia == "asistio" || t.Asistencia == "falto" || t.Asistencia == "justificado");
            string sesionTexto = realizadas >= 3 ? "Sesiones del periodo completas" : $"{realizadas + 1}.ª tutoría del periodo";

            var html = new StringBuilder();
            html.Append($@"<!DOCTYPE html><html lang='es'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>
<title>Ficha de tutoría · {E(alumno.Nombre)}</title>
<style>
@page {{ size: letter; margin: 1.2cm; }}
* {{ box-sizing: border-box; }}
body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #1c2633; background: #eef2f7; margin: 0; font-size: 13px; }}
.barra {{ max-width: 860px; margin: 16px auto 0; display: flex; gap: 8px; justify-content: flex-end; padding: 0 16px; }}
.barra button {{ border: 0; border-radius: 6px; padding: 8px 14px; font-weight: 600; cursor: pointer; background: #18529d; color: #fff; }}
.barra button.sec {{ background: #fff; color: #18529d; border: 1px solid #18529d; }}
.ficha {{ max-width: 860px; margin: 12px auto 24px; background: #fff; border: 1px solid #dce2ea; border-radius: 10px; padding: 26px 30px; display: flex; flex-direction: column; gap: 14px; }}
.encabezado {{ display: flex; justify-content: space-between; gap: 16px; border-bottom: 2px solid #18529d; padding-bottom: 10px; }}
.sup {{ text-transform: uppercase; letter-spacing: .06em; font-size: 11px; font-weight: 700; color: #5a6778; }}
h1 {{ font-size: 21px; margin: 2px 0; color: #0f2c55; }}
h3 {{ font-size: 13px; margin: 0 0 6px; color: #18529d; text-transform: uppercase; letter-spacing: .04em; }}
.suave {{ color: #5a6778; }}
.insignia {{ display: inline-flex; align-items: center; gap: 6px; border-radius: 999px; padding: 4px 12px; font-weight: 700; }}
.punto {{ width: 10px; height: 10px; border-radius: 50%; background: currentColor; display: inline-block; }}
.caja {{ border: 1px solid #dce2ea; border-radius: 8px; padding: 10px 14px; }}
.rejilla {{ display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }}
ul, ol {{ margin: 0; padding-left: 18px; }} li {{ margin: 2px 0; }}
.info {{ color: #5a6778; }}
.barra-avance {{ position: relative; height: 14px; background: #e9eef5; border-radius: 7px; margin: 8px 0 6px; }}
.barra-avance i {{ position: absolute; left: 0; top: 0; bottom: 0; background: #1f5fad; border-radius: 7px; }}
.barra-avance s {{ position: absolute; top: -4px; bottom: -4px; width: 3px; border-radius: 2px; }}
.leyenda {{ display: flex; flex-wrap: wrap; gap: 10px; font-size: 12px; }}
.leyenda span::before {{ content: ''; display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 4px; background: var(--c); vertical-align: -1px; }}
table {{ width: 100%; border-collapse: collapse; }} td {{ padding: 4px 2px; border-bottom: 1px solid #eef1f5; vertical-align: top; }}
.pastilla {{ display: inline-block; border-radius: 999px; padding: 1px 8px; font-size: 11px; font-weight: 600; margin-left: 4px; }}
.p-rojo {{ background: #fde8e7; color: #b42318; }} .p-amarillo {{ background: #fff6db; color: #8a5a00; }} .p-verde {{ background: #e5f6ec; color: #1a7f45; }} .p-gris {{ background: #f1f3f5; color: #5a6778; }}
.lineas div {{ border-bottom: 1px solid #c9d2de; height: 26px; }}
.legal {{ font-size: 10.5px; color: #5a6778; margin: 0; }}
@media print {{
  body {{ background: #fff; font-size: 11.5px; }}
  .barra {{ display: none; }}
  .ficha {{ border: 0; margin: 0; padding: 0; max-width: none; gap: 10px; }}
  .caja, .rejilla > div {{ break-inside: avoid; }}
  * {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
}}
@media (max-width: 700px) {{ .rejilla {{ grid-template-columns: 1fr; }} .ficha {{ padding: 16px; }} .encabezado {{ flex-direction: column; }} }}
</style></head><body>
<div class='barra'><button class='sec' onclick='if (window.opener) window.close(); else history.back();'>Regresar</button><button onclick='window.print()'>Imprimir o guardar en PDF</button></div>
<article class='ficha'>
<div class='encabezado'>
  <div><div class='sup'>Universidad Veracruzana · Ficha para la tutoría</div>
    <h1>{E(alumno.Nombre)}</h1>
    <div class='suave'>{E(alumno.Matricula)} · {E(programa)}{(s.PeriodoQueCursa >= 1 ? $" · cursa el {s.PeriodoQueCursa}° periodo" : "")}</div></div>
  <div style='text-align:right'><span class='insignia' style='color:{color};background:{fondo}'><span class='punto'></span>{textoColor}</span>
    <div class='suave' style='margin-top:6px'>{E(sesionTexto)}<br>{E(periodo)}<br>Emitida el {hoy:dd/MM/yyyy}</div></div>
</div>");

            // ---------- Por qué conviene atenderlo ----------
            var graves = sem.Senales.Where(x => !x.Informativa).ToList();
            var informativas = sem.Senales.Where(x => x.Informativa).ToList();
            html.Append($"<div class='caja' style='border-color:{color};background:{fondo}'><h3 style='color:{color}'>Por qué conviene atenderlo</h3>");
            if (graves.Count == 0 && informativas.Count == 0) html.Append("<p class='suave' style='margin:0'>Sin señales de riesgo este periodo.</p>");
            else
            {
                html.Append("<ul>");
                foreach (var x in graves) html.Append($"<li>{(x.Grave ? $"<b>{E(x.Texto)}</b>" : E(x.Texto))} <span class='suave'>· {E(x.Fuente)}</span></li>");
                foreach (var x in informativas) html.Append($"<li class='info'>{E(x.Texto)} <span class='suave'>· {E(x.Fuente)}</span></li>");
                html.Append("</ul>");
            }
            if (sem.Valoracion != null)
            {
                string decision = sem.Valoracion == "eleva" ? "indicó que el riesgo es mayor" : sem.Valoracion == "descarta" ? "descartó el riesgo" : "confirmó el semáforo";
                html.Append($"<p style='margin:6px 0 0'>El tutor {decision}{(sem.MotivoValoracion != null ? ": " + E(sem.MotivoValoracion) : "")}.</p>");
            }
            html.Append("</div>");

            // ---------- Trayectoria y periodo ----------
            html.Append("<div class='rejilla'><div class='caja'><h3>Su trayectoria</h3>");
            if (s.CreditosTotales > 0)
            {
                int Pct(int creditos) => Math.Min(100, (int)Math.Round(creditos * 100.0 / s.CreditosTotales));
                int avance = Pct(s.CreditosAprobados);
                html.Append($"<div class='barra-avance'><i style='width:{avance}%'></i>");
                if (s.Trayectoria != null)
                    html.Append($"<s style='left:{Pct(s.CreditosLarga)}%;background:#b06a00' title='Larga (mínimo)'></s><s style='left:{Pct(s.CreditosEstandar)}%;background:#0f2c55' title='Estándar'></s>");
                html.Append("</div><div class='leyenda'>");
                html.Append($"<span style='--c:#1f5fad'>Avance {avance} % ({s.CreditosAprobados} de {s.CreditosTotales} créditos)</span>");
                if (s.Trayectoria != null)
                    html.Append($"<span style='--c:#b06a00'>Larga (mínimo) {s.CreditosLarga} cr.</span><span style='--c:#0f2c55'>Estándar {s.CreditosEstandar} cr.</span>");
                html.Append("</div>");
                string estado = s.Trayectoria switch
                {
                    "estandar" => "<span class='pastilla p-verde'>Al corriente</span>",
                    "debajo-estandar" => "<span class='pastilla p-amarillo'>Debajo de la estándar</span>",
                    "debajo-minimo" => "<span class='pastilla p-rojo'>Debajo del mínimo</span>",
                    _ => "<span class='suave'>Primer periodo: todavía no hay créditos esperados.</span>"
                };
                html.Append($"<p style='margin:8px 0 0'>{estado}</p>");
                html.Append($"<p class='suave' style='margin:4px 0 0'>Plan de {s.DuracionPlan} periodos (trayectoria larga: {s.PeriodosTrayectoriaLarga}).");
                if (s.MateriasEnRezago.Count > 0) html.Append($" Materias de periodos anteriores sin aprobar: {s.MateriasEnRezago.Count}.");
                html.Append("</p>");
            }
            else html.Append("<p class='suave' style='margin:0'>El alumno no tiene un plan de estudios asignado.</p>");
            html.Append("</div>");

            // Materias del periodo
            var criticas = s.MateriasCriticas.ToDictionary(m => m.Materia, m => m.Porcentaje, StringComparer.OrdinalIgnoreCase);
            var delPeriodo = new Dictionary<string, MateriaCursada>();
            foreach (var m in alumno.Materias.Where(m => m.Periodo == periodo)) delPeriodo[m.Nombre ?? ""] = m;
            html.Append("<div class='caja'><h3>Sus materias este periodo</h3>");
            if (delPeriodo.Count == 0) html.Append("<p class='suave' style='margin:0'>Sin materias registradas en el periodo.</p>");
            else
            {
                html.Append("<table>");
                foreach (var m in delPeriodo.Values)
                {
                    string estado = (m.Estado ?? "").ToLowerInvariant();
                    var marcas = new StringBuilder();
                    marcas.Append(estado == "aprobada" ? "<span class='pastilla p-verde'>Aprobada</span>" : estado == "reprobada" ? "<span class='pastilla p-rojo'>Reprobada</span>" : "<span class='pastilla p-gris'>Cursando</span>");
                    if (m.EnRiesgo && estado != "aprobada") marcas.Append("<span class='pastilla p-amarillo'>En riesgo</span>");
                    if (string.Equals(m.TipoInscripcion, "segunda", StringComparison.OrdinalIgnoreCase)) marcas.Append("<span class='pastilla p-rojo'>2.ª inscripción</span>");
                    if (criticas.TryGetValue(m.Nombre ?? "", out int pct)) marcas.Append($"<span class='pastilla p-gris'>Crítica · {pct} %</span>");
                    html.Append($"<tr><td>{E(m.Nombre)}</td><td style='text-align:right'>{marcas}</td></tr>");
                }
                html.Append("</table>");
            }
            html.Append("</div></div>");

            // ---------- Temas sugeridos ----------
            html.Append("<div class='caja'><h3>Temas sugeridos para la conversación</h3><ol>");
            foreach (var t in sem.TemasSugeridos) html.Append($"<li>{E(t)}</li>");
            html.Append("</ol></div>");

            // ---------- Apoyos e historial ----------
            html.Append("<div class='rejilla'><div class='caja'><h3>Apoyos disponibles</h3>");
            if (plan != null && plan.Apoyos.Count > 0)
            {
                html.Append("<ul>");
                foreach (var a in plan.Apoyos) html.Append($"<li><b>{E(a.Nombre)}</b>{(a.Detalle != "" ? ": " + E(a.Detalle) : "")}</li>");
                html.Append("</ul>");
            }
            else
                html.Append("<p class='suave' style='margin:0'>Asesorías académicas, CENDHIU, becas y demás apoyos: consultar horarios con la coordinación de tutorías. "
                          + "(La coordinación puede agregar horarios y lugares en la hoja «Apoyos» de la plantilla del plan.)</p>");
            html.Append("</div>");

            html.Append("<div class='caja'><h3>Historial</h3><ul>");
            var porPeriodo = alumno.Tutorias.Where(GeneradorDocumentoTutorias.TieneInformacion).GroupBy(t => t.Periodo ?? "").ToList();
            if (porPeriodo.Count == 0) html.Append("<li class='suave'>Sin tutorías registradas.</li>");
            foreach (var g in porPeriodo.TakeLast(3))
                html.Append($"<li>{E(g.Key)}: {g.Count()} sesión(es), asistió a {g.Count(t => t.Asistencia == "asistio")}</li>");
            if (sem.UltimoContacto != null)
                html.Append($"<li>Último contacto: {FechaCorta(sem.UltimoContacto)} (hace {sem.DiasSinContacto} días)</li>");

            var registradas = alumno.Tutorias.SelectMany(t => t.Situaciones ?? new List<string>()).Distinct().ToList();
            var visibles = registradas.Where(x => !CatalogoTutorias.EsPrivada(x)).ToList();
            if (registradas.Any(CatalogoTutorias.EsPrivada)) visibles.Add("situación personal o de salud (confidencial)");
            html.Append($"<li>Problemáticas registradas: {(visibles.Count == 0 ? "ninguna" : E(string.Join(", ", visibles)))}</li>");
            var canalizaciones = alumno.Tutorias.SelectMany(t => (t.Canalizaciones ?? new List<string>())
                .Concat(string.IsNullOrWhiteSpace(t.OtraCanalizacion) ? Array.Empty<string>() : new[] { t.OtraCanalizacion!.Trim() })).Distinct().ToList();
            if (canalizaciones.Count > 0) html.Append($"<li>Canalizado a: {E(string.Join(", ", canalizaciones))}</li>");
            html.Append($"<li>Caso actual: {(sem.EstadoCaso == null ? "sin caso abierto" : E(sem.EstadoCaso.ToLowerInvariant()))}{(sem.CasoDesde != null ? $" (desde {E(sem.CasoDesde)})" : "")}</li>");
            foreach (var canal in sem.CanalizacionesCaso)
                html.Append($"<li>{E(canal.Area)}: <b>{ReglasCaso.TextoAcudio(canal.Acudio)}</b> <span class='suave'>(canalizado en {E(canal.Periodo)})</span></li>");
            if (sem.AcuerdoPendiente != null)
                html.Append($"<li>Acuerdo pendiente: {E(sem.AcuerdoPendiente)}{(sem.FechaRevision != null ? $" (revisar el {FechaCorta(sem.FechaRevision)}{(sem.RevisionVencida ? ", vencido" : "")})" : "")}</li>");
            html.Append("</ul></div></div>");

            // ---------- Notas ----------
            html.Append("<div class='caja'><h3>Notas de la sesión</h3><div class='lineas'><div></div><div></div><div></div><div></div></div>");
            html.Append("<p class='suave' style='margin:6px 0 0'>Después de la sesión, registra lo acordado en el reporte de tutoría de SITRA-UV: las casillas sustituyen estas notas.</p></div>");
            html.Append($"<p class='legal'>{E(GeneradorDocumentoTutorias.LeyendaPrivacidad)}</p>");
            html.Append("</article></body></html>");
            return html.ToString();
        }

        private static string FechaCorta(string fecha) => DateTime.TryParse(fecha, out var d) ? d.ToString("dd/MM/yyyy") : fecha;

        private static string E(string? texto) =>
            (texto ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }
}
