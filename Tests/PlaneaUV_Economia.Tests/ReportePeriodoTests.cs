using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Reporte de seguimiento por periodo (historial del alumno semestre por semestre)
    public class ReportePeriodoTests
    {
        // Un S25 ingresó en agosto de 2025: 1er periodo = "Agosto - Enero 2026", 2do = "Febrero - Julio 2026"
        private const string P1 = "Agosto - Enero 2026", P2 = "Febrero - Julio 2026", P3 = "Agosto - Enero 2027";
        private static readonly DateTime Hoy = new(2026, 10, 26);
        private readonly ServicioReportePeriodo _servicio;

        public ReportePeriodoTests()
        {
            var planes = PlanesEnMemoria.ConPlanCorto();
            _servicio = new ServicioReportePeriodo(planes, new ServicioAlertas(planes));
        }

        private static AlumnoAvance Alumno() => Datos.Alumno("S25000001",
            Datos.Materia("A", "aprobada", periodo: P1),
            Datos.Materia("B", "reprobada", periodo: P1),
            Datos.Materia("C", "aprobada", periodo: P2),
            Datos.Materia("E", "cursando", periodo: P3));

        [Fact]
        public void OrdenDeLosPeriodos()
        {
            Assert.True(ReglasTrayectoria.ClavePeriodo(P1) < ReglasTrayectoria.ClavePeriodo(P2));
            Assert.True(ReglasTrayectoria.ClavePeriodo(P2) < ReglasTrayectoria.ClavePeriodo(P3));
            Assert.Equal(new DateTime(2026, 1, 31), ReglasTrayectoria.FinDePeriodo(P1));
            Assert.Equal(new DateTime(2026, 7, 31), ReglasTrayectoria.FinDePeriodo(P2));
        }

        [Fact]
        public void UnPeriodoPasadoSeCalculaComoEstabaAlCierre()
        {
            var r = _servicio.Resumir(Alumno(), P1, Hoy);

            Assert.Equal(1, r.PeriodoQueCursa);
            Assert.True(r.Concluido);
            Assert.Equal(2, r.Materias.Count);                       // solo las de ese periodo
            Assert.Equal(1, r.Aprobadas);
            Assert.Equal(1, r.Reprobadas);
            Assert.Equal(0, r.CreditosAlInicio);
            Assert.Equal(6, r.CreditosGanados);
            Assert.Equal(6, r.CreditosAlCierre);                    // C (aprobada después) no cuenta aquí
            Assert.Equal(new[] { "B" }, r.Rezago);                  // al cerrar su 1er periodo le quedó B
        }

        [Fact]
        public void ElSiguientePeriodoParteDeLoQueTenia()
        {
            var r = _servicio.Resumir(Alumno(), P2, Hoy);

            Assert.Equal(2, r.PeriodoQueCursa);
            Assert.Equal(6, r.CreditosAlInicio);
            Assert.Equal(6, r.CreditosGanados);
            Assert.Equal(12, r.CreditosAlCierre);
            Assert.Equal(new[] { "B", "Optativa 1" }, r.Rezago);    // al cerrar el 2do: B y la optativa del 2do
            Assert.Equal("Atrasada", r.SiguientePeriodo.First(m => m.Nombre == "B").Motivo);
            Assert.Contains(r.SiguientePeriodo, m => m.Nombre == "D" && m.Motivo == "Del siguiente periodo");
        }

        [Fact]
        public void ElPeriodoEnCursoNoCuentaComoRezagoLoQueSeEstaCursando()
        {
            // Cursa su 3er periodo (en curso): el rezago es de los periodos 1 y 2, igual que en la pantalla principal
            var r = _servicio.Resumir(Alumno(), P3, Hoy);
            Assert.False(r.Concluido);
            Assert.Equal(new[] { "B", "Optativa 1" }, r.Rezago);
            Assert.DoesNotContain("D", r.Rezago);
            Assert.DoesNotContain("E", r.Rezago);

            // Para el siguiente periodo no se propone E (la está cursando); D, del 3ro y sin inscribir, sí, y F del 4to
            Assert.DoesNotContain(r.SiguientePeriodo, m => m.Nombre == "E");
            Assert.Equal("Atrasada", r.SiguientePeriodo.Single(m => m.Nombre == "D").Motivo);
            Assert.Equal("Del siguiente periodo", r.SiguientePeriodo.Single(m => m.Nombre == "F").Motivo);
        }

        [Fact]
        public void MateriaSinResultadoEnUnPeriodoQueYaTermino()
        {
            var alumno = Datos.Alumno("S25000001", Datos.Materia("A", "cursando", periodo: P1));
            Assert.Equal("sin-resultado", Assert.Single(_servicio.Resumir(alumno, P1, Hoy).Materias).Resultado);

            var enCurso = Datos.Alumno("S25000001", Datos.Materia("E", "cursando", periodo: P3));
            Assert.Equal("cursando", Assert.Single(_servicio.Resumir(enCurso, P3, Hoy).Materias).Resultado);
        }

        [Fact]
        public void HistorialConPausaYResumenDeTutoria()
        {
            var alumno = Alumno();
            alumno.PeriodosSinInscripcion.Add(P2);
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = P1, Sesion = 1, Asistencia = "asistio", Situaciones = { "Económica", "Violencia o acoso" }, Canalizaciones = { "Becas y apoyos económicos" } });
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = P1, Sesion = 2, Asistencia = "falto" });

            var periodos = _servicio.PeriodosDelHistorial(alumno, P3);
            Assert.Equal(new[] { P1, P2, P3 }, periodos);

            var resumenes = periodos.Select(p => _servicio.Resumir(alumno, p, Hoy)).ToList();
            Assert.True(resumenes[1].EnPausa);
            var t = resumenes[0].Tutoria;
            Assert.Equal(2, t.Sesiones);
            Assert.Equal(1, t.Asistio);
            Assert.Equal(1, t.NoAsistio);
            Assert.Contains("Situación personal o de salud (confidencial)", t.Problematicas);
            Assert.DoesNotContain("Violencia o acoso", t.Problematicas);   // lo privado no se detalla

            alumno.Nombre = "Ruiz <Ana>";
            string html = GeneradorReportePeriodo.Generar(new[] { new GeneradorReportePeriodo.Entrada { Alumno = alumno, Programa = "Licenciatura de Prueba", Periodos = resumenes } },
                "Historial", historial: true, Hoy);
            Assert.Contains("Resumen por periodo", html);
            Assert.Contains("Ruiz &lt;Ana&gt;", html);
            Assert.Contains("Sin inscripción (baja temporal)", html);
            Assert.Contains("Para el siguiente periodo", html);
            Assert.DoesNotContain("Violencia", html);
            Assert.Contains("LGPDPPSO", html);
        }
    }
}
