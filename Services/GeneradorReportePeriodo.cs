using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Arma el reporte de seguimiento por periodo (una página HTML para imprimir o guardar en PDF).
    //   - Un alumno, un periodo:      el cierre de ese periodo.
    //   - Un alumno, historial:       tabla con todos sus periodos y el detalle de cada uno (del más reciente al primero).
    //   - Varios alumnos:             un reporte por alumno, cada uno en hoja aparte (cierre de periodo del grupo).
    // No calcula reglas: recibe los resúmenes hechos por ServicioReportePeriodo.
    public static class GeneradorReportePeriodo
    {
        public class Entrada
        {
            public AlumnoAvance Alumno { get; init; } = new();
            public string Programa { get; init; } = "";
            public List<ResumenPeriodo> Periodos { get; init; } = new();   // en orden cronológico
        }

        public static string Generar(IReadOnlyList<Entrada> alumnos, string titulo, bool historial, DateTime hoy)
        {
            var html = new StringBuilder();
            html.Append($@"<!DOCTYPE html><html lang='es'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>
<title>{E(titulo)}</title>
<style>
@page {{ size: letter; margin: 1.2cm; }}
* {{ box-sizing: border-box; }}
body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #1c2633; background: #f3f6fa; margin: 0; font-size: 13px; }}
.barra {{ max-width: 900px; margin: 16px auto 0; display: flex; gap: 8px; justify-content: flex-end; padding: 0 16px; }}
.barra button {{ border: 0; border-radius: 8px; padding: 8px 14px; font-weight: 600; cursor: pointer; background: #18529d; color: #fff; }}
.barra button.sec {{ background: #fff; color: #18529d; border: 1px solid #18529d; }}
.hoja {{ max-width: 900px; margin: 12px auto 24px; background: #fff; border: 1px solid #dde5ef; border-radius: 12px; padding: 26px 30px; }}
.hoja + .hoja {{ break-before: page; }}
.encabezado {{ display: flex; justify-content: space-between; gap: 16px; border-bottom: 3px solid #28ad56; padding-bottom: 10px; margin-bottom: 14px; }}
.sup {{ text-transform: uppercase; letter-spacing: .06em; font-size: 11px; font-weight: 700; color: #5a6778; }}
h1 {{ font-size: 21px; margin: 2px 0; color: #0f2c55; }}
h2 {{ font-size: 15px; margin: 18px 0 8px; color: #fff; background: #18529d; padding: 6px 10px; border-radius: 6px; display: flex; justify-content: space-between; align-items: center; gap: 8px; }}
h3 {{ font-size: 12px; margin: 0 0 6px; color: #18529d; text-transform: uppercase; letter-spacing: .04em; }}
.suave {{ color: #5a6778; }}
table {{ width: 100%; border-collapse: collapse; }}
th, td {{ padding: 5px 6px; border-bottom: 1px solid #e8edf3; text-align: left; vertical-align: top; }}
th {{ background: #eef3f9; color: #0f2c55; font-size: 11.5px; }}
.num {{ text-align: center; white-space: nowrap; }}
.rejilla {{ display: grid; grid-template-columns: 1.3fr 1fr; gap: 12px; }}
.caja {{ border: 1px solid #dde5ef; border-radius: 8px; padding: 10px 12px; margin-bottom: 10px; }}
.pastilla {{ display: inline-block; border-radius: 999px; padding: 1px 8px; font-size: 11px; font-weight: 600; white-space: nowrap; }}
.p-rojo {{ background: #fde8e7; color: #b42318; }} .p-amarillo {{ background: #fff6db; color: #8a5a00; }} .p-verde {{ background: #e6f6ec; color: #1e8443; }} .p-gris {{ background: #f1f3f5; color: #5a6778; }} .p-azul {{ background: #e8f0fa; color: #18529d; }}
.barra-avance {{ position: relative; height: 12px; background: #e9eef5; border-radius: 6px; margin: 6px 0; }}
.barra-avance i {{ position: absolute; left: 0; top: 0; bottom: 0; background: #28ad56; border-radius: 6px; }}
.barra-avance s {{ position: absolute; top: -3px; bottom: -3px; width: 3px; border-radius: 2px; }}
ul {{ margin: 0; padding-left: 18px; }} li {{ margin: 2px 0; }}
.legal {{ font-size: 10.5px; color: #5a6778; margin: 14px 0 0; }}
.periodo {{ break-inside: avoid; }}
@media print {{
  body {{ background: #fff; font-size: 11.5px; }}
  .barra {{ display: none; }}
  .hoja {{ border: 0; margin: 0; padding: 0; max-width: none; border-radius: 0; }}
  * {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
}}
@media (max-width: 700px) {{ .rejilla {{ grid-template-columns: 1fr; }} .hoja {{ padding: 16px; }} .encabezado {{ flex-direction: column; }} }}
</style></head><body>
<div class='barra'><button class='sec' onclick='if (window.opener) window.close(); else history.back();'>Regresar</button><button onclick='window.print()'>Imprimir o guardar en PDF</button></div>");

            if (alumnos.Count == 0)
                html.Append("<article class='hoja'><p class='suave'>No hay alumnos para este reporte.</p></article>");

            foreach (var a in alumnos) html.Append(Hoja(a, historial, hoy));
            html.Append("</body></html>");
            return html.ToString();
        }

        private static string Hoja(Entrada e, bool historial, DateTime hoy)
        {
            var a = e.Alumno;
            var ultimo = e.Periodos.LastOrDefault();
            var html = new StringBuilder("<article class='hoja'>");
            html.Append($@"<div class='encabezado'>
  <div><div class='sup'>Universidad Veracruzana · {(historial ? "Historial de seguimiento académico" : "Reporte de seguimiento del periodo")}</div>
    <h1>{E(a.Nombre)}</h1>
    <div class='suave'>{E(a.Matricula)} · {E(e.Programa)}{(string.IsNullOrWhiteSpace(a.Situacion) || a.Situacion == "Activo" ? "" : " · " + E(a.Situacion))}</div></div>
  <div style='text-align:right' class='suave'>{(ultimo != null ? (historial ? "Hasta " : "Periodo ") + E(ultimo.Periodo) + "<br>" : "")}Emitido el {hoy:dd/MM/yyyy}</div>
</div>");

            if (e.Periodos.Count == 0)
            {
                html.Append("<p class='suave'>No hay periodos que mostrar.</p></article>");
                return html.ToString();
            }

            // ---------- Resumen de todos los periodos (solo en el historial) ----------
            if (historial && e.Periodos.Count > 1)
            {
                html.Append("<h3>Resumen por periodo</h3><table><thead><tr><th>Periodo</th><th class='num'>Cursó</th><th class='num'>Aprobó</th><th class='num'>Reprobó</th><th class='num'>Créditos ganados</th><th class='num'>Acumulado</th><th>Trayectoria</th><th>Semáforo</th><th class='num'>Tutorías</th></tr></thead><tbody>");
                foreach (var p in e.Periodos)
                {
                    html.Append($"<tr><td><b>{E(p.Periodo)}</b>{(p.PeriodoQueCursa >= 1 ? $"<br><span class='suave'>{p.PeriodoQueCursa}° periodo</span>" : "")}</td>");
                    if (p.EnPausa) { html.Append("<td colspan='8'><span class='pastilla p-gris'>Sin inscripción (baja temporal)</span></td></tr>"); continue; }
                    html.Append($"<td class='num'>{p.Materias.Count}</td><td class='num'>{p.Aprobadas}</td><td class='num'>{(p.Reprobadas > 0 ? $"<b style='color:#b42318'>{p.Reprobadas}</b>" : "0")}</td>");
                    html.Append($"<td class='num'>{p.CreditosGanados}</td><td class='num'>{p.CreditosAlCierre}{(p.CreditosTotales > 0 ? $" / {p.CreditosTotales}" : "")}</td>");
                    html.Append($"<td>{Trayectoria(p.Trayectoria)}</td><td>{Semaforo(p.Semaforo.Final)}</td><td class='num'>{(p.Tutoria.Sesiones == 0 ? "<span class='suave'>—</span>" : $"{p.Tutoria.Asistio} de {p.Tutoria.Sesiones}")}</td></tr>");
                }
                html.Append("</tbody></table>");
            }

            // ---------- Detalle de cada periodo, del más reciente al primero ----------
            foreach (var p in Enumerable.Reverse(e.Periodos))
                html.Append(Periodo(p, p == ultimo));

            html.Append($"<p class='legal'>Cada periodo concluido muestra la situación del alumno al cierre de ese periodo, con lo registrado hasta entonces; el periodo en curso muestra la situación actual. {E(GeneradorDocumentoTutorias.LeyendaPrivacidad)}</p>");
            html.Append("</article>");
            return html.ToString();
        }

        private static string Periodo(ResumenPeriodo p, bool esElUltimo)
        {
            var html = new StringBuilder("<section class='periodo'>");
            string cursa = p.PeriodoQueCursa >= 1 ? $" · {p.PeriodoQueCursa}° periodo" : "";
            html.Append($"<h2><span>{E(p.Periodo)}{cursa}{(p.Concluido ? "" : " (en curso)")}</span>{(p.EnPausa ? "" : Semaforo(p.Semaforo.Final))}</h2>");

            if (p.EnPausa)
            {
                html.Append("<p class='suave'>El alumno no estuvo inscrito en este periodo (baja temporal o pausa). No cuenta para su avance.</p></section>");
                return html.ToString();
            }

            html.Append("<div class='rejilla'><div>");

            // Materias del periodo
            html.Append("<div class='caja'><h3>Materias del periodo</h3>");
            if (p.Materias.Count == 0) html.Append("<p class='suave' style='margin:0'>Sin materias registradas en este periodo.</p>");
            else
            {
                html.Append("<table><thead><tr><th>Materia</th><th class='num'>Cr.</th><th>Inscripción</th><th>Resultado</th></tr></thead><tbody>");
                foreach (var m in p.Materias)
                    html.Append($"<tr><td>{E(m.Nombre)}{(m.EnRiesgo ? " <span class='pastilla p-amarillo'>en riesgo</span>" : "")}</td><td class='num'>{m.Creditos}</td><td>{Inscripcion(m)}</td><td>{Resultado(m.Resultado)}</td></tr>");
                html.Append("</tbody></table>");
                html.Append($"<p class='suave' style='margin:6px 0 0'>Aprobó {p.Aprobadas} de {p.Materias.Count}{(p.Reprobadas > 0 ? $" · reprobó {p.Reprobadas}" : "")}{(p.SinResultado > 0 ? $" · {p.SinResultado} sin resultado registrado" : "")}.</p>");
            }
            html.Append("</div>");

            // Señales: cómo iba DURANTE ese periodo (o las actuales, si sigue en curso)
            string momento = p.Concluido ? "durante el periodo" : "actuales";
            var senales = p.Semaforo.Senales.Where(s => !s.Informativa).ToList();
            if (senales.Count > 0)
            {
                html.Append($"<div class='caja'><h3>Señales {momento}</h3><ul>");
                foreach (var s in senales) html.Append($"<li>{(s.Grave ? $"<b>{E(s.Texto)}</b>" : E(s.Texto))} <span class='suave'>· {E(s.Fuente)}</span></li>");
                html.Append("</ul></div>");
            }
            html.Append("</div><div>");

            // Créditos y trayectoria
            html.Append("<div class='caja'><h3>Créditos y trayectoria</h3>");
            string deTotal = p.CreditosTotales > 0 ? $" de {p.CreditosTotales}" : "";
            html.Append(p.Concluido
                ? $"<div>Ganó <b>{p.CreditosGanados}</b> créditos: pasó de {p.CreditosAlInicio} a <b>{p.CreditosAlCierre}</b>{deTotal}.</div>"
                : $"<div>Periodo en curso: lleva <b>{p.CreditosAlCierre}</b>{deTotal} créditos ({p.CreditosGanados} ganados en este periodo hasta ahora).</div>");
            if (p.CreditosTotales > 0)
            {
                int Pct(int c) => Math.Min(100, (int)Math.Round(c * 100.0 / p.CreditosTotales));
                html.Append($"<div class='barra-avance'><i style='width:{Pct(p.CreditosAlCierre)}%'></i>");
                if (p.Trayectoria != null)
                    html.Append($"<s style='left:{Pct(p.CreditosLarga)}%;background:#b06a00' title='Larga (mínimo)'></s><s style='left:{Pct(p.CreditosEstandar)}%;background:#0f2c55' title='Estándar'></s>");
                html.Append("</div>");
                if (p.Trayectoria != null)
                    html.Append($"<div>{Trayectoria(p.Trayectoria)} <span class='suave'>Estándar: {p.CreditosEstandar} · mínimo: {p.CreditosLarga}</span></div>");
            }
            html.Append("</div>");

            // Rezago
            html.Append(p.Concluido ? "<div class='caja'><h3>Rezago al cierre</h3>" : "<div class='caja'><h3>Rezago actual</h3>");
            if (p.Rezago.Count == 0) html.Append("<p style='margin:0'><span class='pastilla p-verde'>Sin rezago</span></p>");
            else
            {
                html.Append($"<p style='margin:0 0 4px'>{p.Rezago.Count} materia(s) de periodos anteriores sin aprobar:</p><p class='suave' style='margin:0'>{E(Lista(p.Rezago, 8))}</p>");
                if (p.SeriadasPendientes.Count > 0) html.Append($"<p style='margin:4px 0 0'><b>Bloquean otras materias:</b> {E(Lista(p.SeriadasPendientes, 6))}</p>");
            }
            html.Append("</div>");

            // Tutoría (resumen)
            var t = p.Tutoria;
            html.Append("<div class='caja'><h3>Tutoría del periodo</h3>");
            if (t.Sesiones == 0) html.Append("<p class='suave' style='margin:0'>Sin sesiones registradas.</p>");
            else
            {
                html.Append($"<div>Sesiones: {t.Sesiones} · asistió a {t.Asistio}{(t.NoAsistio > 0 ? $" · no asistió a {t.NoAsistio}" : "")}{(t.Justificadas > 0 ? $" · {t.Justificadas} justificada(s)" : "")}</div>");
                if (t.Problematicas.Count > 0) html.Append($"<div><b>Problemáticas:</b> {E(string.Join(", ", t.Problematicas))}</div>");
                if (t.Canalizaciones.Count > 0) html.Append($"<div><b>Canalizado a:</b> {E(string.Join(", ", t.Canalizaciones))}</div>");
                html.Append($"<div><b>Caso:</b> {(t.EstadoCaso == null ? "sin caso abierto" : E(t.EstadoCaso))}</div>");
                html.Append("<div class='suave' style='margin-top:4px'>El detalle de cada sesión está en el reporte de tutoría.</div>");
            }
            html.Append("</div>");
            html.Append("</div></div>");

            // Para el siguiente periodo (solo en el periodo más reciente)
            if (esElUltimo && p.SiguientePeriodo.Count > 0)
            {
                html.Append("<div class='caja'><h3>Para el siguiente periodo</h3><table><thead><tr><th>Materia</th><th class='num'>Semestre</th><th class='num'>Cr.</th><th>Motivo</th></tr></thead><tbody>");
                foreach (var m in p.SiguientePeriodo.Take(12))
                {
                    string clase = m.Motivo.StartsWith("Atrasada ·") ? "p-rojo" : m.Motivo == "Atrasada" ? "p-amarillo" : "p-azul";
                    html.Append($"<tr><td>{E(m.Nombre)}</td><td class='num'>{m.Semestre}°</td><td class='num'>{m.Creditos}</td><td><span class='pastilla {clase}'>{E(m.Motivo)}</span></td></tr>");
                }
                html.Append("</tbody></table>");
                html.Append($"<p class='suave' style='margin:6px 0 0'>Orientativo: primero lo atrasado que bloquea a otras materias. Para ajustarlo a un máximo de créditos usa «Sugerir carga».{(p.SiguientePeriodo.Count > 12 ? $" Se muestran 12 de {p.SiguientePeriodo.Count}." : "")}</p></div>");
            }
            if (esElUltimo && p.Semaforo.AcuerdoPendiente != null)
                html.Append($"<div class='caja'><h3>Acuerdo pendiente de tutoría</h3>{E(p.Semaforo.AcuerdoPendiente)}{(p.Semaforo.FechaRevision != null ? $" <span class='suave'>(revisar el {FechaCorta(p.Semaforo.FechaRevision)})</span>" : "")}</div>");

            html.Append("</section>");
            return html.ToString();
        }

        private static string Semaforo(string? color) => color switch
        {
            "rojo" => "<span class='pastilla p-rojo'>● Riesgo alto</span>",
            "amarillo" => "<span class='pastilla p-amarillo'>● Atención</span>",
            "verde" => "<span class='pastilla p-verde'>● Sin riesgo</span>",
            _ => "<span class='pastilla p-gris'>Sin semáforo</span>"
        };

        private static string Trayectoria(string? estado) => estado switch
        {
            "estandar" => "<span class='pastilla p-verde'>Al corriente</span>",
            "debajo-estandar" => "<span class='pastilla p-amarillo'>Debajo de la estándar</span>",
            "debajo-minimo" => "<span class='pastilla p-rojo'>Debajo del mínimo</span>",
            _ => "<span class='suave'>—</span>"
        };

        private static string Resultado(string resultado) => resultado switch
        {
            "aprobada" => "<span class='pastilla p-verde'>Aprobada</span>",
            "reprobada" => "<span class='pastilla p-rojo'>Reprobada</span>",
            "cursando" => "<span class='pastilla p-azul'>Cursando</span>",
            "continua" => "<span class='pastilla p-azul'>Continúa el siguiente periodo</span>",
            _ => "<span class='pastilla p-gris'>Sin resultado registrado</span>"
        };

        private static string Inscripcion(MateriaDelPeriodo m)
        {
            string insc = m.Inscripcion.Equals("segunda", StringComparison.OrdinalIgnoreCase) ? "2.ª" : "1.ª";
            string examen = m.Examen.ToLowerInvariant() switch
            {
                "ordinario" => "ordinario", "extraordinario" => "extraordinario", "titulo" => "título", "ultima" => "última oportunidad", _ => ""
            };
            return E(insc + (examen != "" ? " · " + examen : ""));
        }

        private static string Lista(List<string> nombres, int maximo) =>
            nombres.Count <= maximo ? string.Join(", ", nombres) : string.Join(", ", nombres.Take(maximo)) + $" y {nombres.Count - maximo} más";

        private static string FechaCorta(string fecha) => DateTime.TryParse(fecha, out var d) ? d.ToString("dd/MM/yyyy") : fecha;

        private static string E(string? texto) =>
            (texto ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }
}
