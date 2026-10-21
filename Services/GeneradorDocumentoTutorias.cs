using System.Linq;
using System.Text;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Arma el documento de evidencia de tutorías (HTML que Word abre como .doc)
    public static class GeneradorDocumentoTutorias
    {
        public static string Generar(AlumnoAvance alumno, string programa)
        {
            var html = new StringBuilder();
            html.Append($@"<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
                <head><meta charset='utf-8'><title>Tutorías</title>
                <style>body {{ font-family: 'Calibri'; }} table {{ border-collapse: collapse; width: 100%; margin-bottom: 20px; }} th, td {{ border: 1px solid #000; padding: 5px; }} th {{ background-color: #f2f2f2; }} .periodo {{ background-color: #0dcaf0; color: #000; padding: 5px; font-weight: bold; font-size: 14pt; margin-top: 20px; }}</style>
                </head><body><h2>Evidencia de Tutoría Académica - {E(programa)}</h2>
                <p><strong>Alumno:</strong> {E(alumno.Nombre)}</p><p><strong>Matrícula:</strong> {E(alumno.Matricula)}</p><hr/>");

            foreach (var grupo in alumno.Tutorias.GroupBy(t => t.Periodo))
            {
                html.Append($"<div class='periodo'>Periodo: {E(grupo.Key)}</div>");
                foreach (var sesion in grupo.OrderBy(s => s.Sesion))
                {
                    html.Append($@"<table><tr><th style='width:15%'>Sesión {sesion.Sesion}</th><td style='width:20%'>{E(sesion.Fecha)}</td><th style='width:15%'>Asistencia:</th><td>{Asistencia(sesion.Asistencia)}</td></tr>
                                   <tr><th colspan='4'>Comentarios:</th></tr><tr><td colspan='4' style='height: 60px;'>{E(sesion.Comentarios)}</td></tr></table>");
                }
            }
            html.Append("<br/><br/><table style='border: none; text-align: center; margin-top: 50px;'><tr style='border: none;'><td style='border: none; width: 50%;'>_________________________<br/>Firma Tutor</td><td style='border: none; width: 50%;'>_________________________<br/>Firma Estudiante</td></tr></table></body></html>");
            return html.ToString();
        }

        private static string Asistencia(string? valor) =>
            valor == "asistio" ? "Asistió" : valor == "falto" ? "Faltó" : valor == "reprogramada" ? "Reprog." : "Pendiente";

        // Escapa solo los caracteres especiales de HTML (los acentos se dejan tal cual).
        // Antes los comentarios se insertaban sin escapar: un "<" en un comentario rompía el documento.
        private static string E(string? texto) =>
            (texto ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }
}
