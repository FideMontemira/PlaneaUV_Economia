using System;
using System.Collections.Generic;
using System.Linq;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // MATERIAS CRÍTICAS: las de mayor reprobación entre los alumnos registrados.
    // Se cuenta cada intento con resultado (aprobada o reprobada); "cursando" todavía no cuenta.
    // Una materia es crítica si tiene al menos MinimoIntentos con resultado y reprueba PorcentajeCritico % o más.
    // Con la base de datos institucional (Trayectorias UV) esta misma regla daría la estadística real de cada EE.
    public static class ReglasMateriasCriticas
    {
        public const int MinimoIntentos = 3;
        public const int PorcentajeCritico = 30;

        public static List<MateriaCritica> Calcular(IEnumerable<AlumnoAvance> alumnos, string? periodo)
        {
            var porMateria = new Dictionary<string, MateriaCritica>(StringComparer.OrdinalIgnoreCase);
            var alumnosPorMateria = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var alumno in alumnos)
            {
                string matricula = alumno.Matricula ?? "";
                var quien = new AlumnoDeReporte { Matricula = matricula, Nombre = alumno.Nombre ?? "" };
                var ultimoIntento = new Dictionary<string, MateriaCursada>(StringComparer.OrdinalIgnoreCase);

                foreach (var m in alumno.Materias)
                {
                    string nombre = (m.Nombre ?? "").Trim();
                    if (nombre == "") continue;
                    ultimoIntento[nombre] = m;

                    if (!porMateria.TryGetValue(nombre, out var c)) porMateria[nombre] = c = new MateriaCritica { Materia = nombre };
                    if (!alumnosPorMateria.TryGetValue(nombre, out var quienes)) alumnosPorMateria[nombre] = quienes = new HashSet<string>();
                    quienes.Add(matricula);

                    string estado = (m.Estado ?? "").ToLowerInvariant();
                    if (estado == "aprobada" || estado == "reprobada") c.Intentos++;
                    if (estado == "reprobada")
                    {
                        c.Reprobaciones++;
                        if (!c.Reprobaron.Any(a => a.Matricula == matricula)) c.Reprobaron.Add(quien);
                    }
                }

                // Lo que pasa ahora: se mira solo el último intento de cada materia
                foreach (var m in ultimoIntento.Values)
                {
                    var c = porMateria[(m.Nombre ?? "").Trim()];
                    if (string.Equals(m.Estado, "cursando", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(periodo) || m.Periodo == periodo)) { c.CursandoAhora++; c.Cursando.Add(quien); }
                    if (m.EnRiesgo && !ReglasTrayectoria.EstaAprobada(m)) { c.EnRiesgoAhora++; c.EnRiesgo.Add(quien); }
                }
            }

            foreach (var c in porMateria.Values)
            {
                c.Alumnos = alumnosPorMateria[c.Materia].Count;
                c.Porcentaje = c.Intentos > 0 ? (int)Math.Round(c.Reprobaciones * 100.0 / c.Intentos, MidpointRounding.AwayFromZero) : 0;
                c.Critica = c.Intentos >= MinimoIntentos && c.Porcentaje >= PorcentajeCritico;
                c.Motivo = c.Critica ? null
                         : c.Reprobaciones == 0 ? "sin-reprobaciones"
                         : c.Intentos < MinimoIntentos ? "pocos-casos"
                         : "baja-reprobacion";
            }

            // Primero las críticas, luego las de mayor reprobación
            return porMateria.Values.Where(c => c.Reprobaciones > 0 || c.EnRiesgoAhora > 0)
                .OrderByDescending(c => c.Critica).ThenByDescending(c => c.Porcentaje).ThenByDescending(c => c.Reprobaciones).ThenBy(c => c.Materia)
                .ToList();
        }
    }
}
