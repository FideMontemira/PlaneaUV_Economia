using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    public class ServicioAlertasTests
    {
        private const string Periodo = "Agosto - Enero 2027";
        private readonly ServicioAlertas _alertas = new(PlanesEnMemoria.ConPlanCorto());

        private List<string> Mensajes(AlumnoAvance alumno) => _alertas.Calcular(new[] { alumno }, Periodo).Select(a => a.Mensaje).ToList();

        [Fact]
        public void AlumnoQueExcedeElPlanConMateriasPendientes()
        {
            // S21 en Agosto - Enero 2027 va en su 11° periodo; el plan dura 4
            var mensajes = Mensajes(Datos.Alumno("S21000001", Datos.Materia("A", "aprobada")));
            Assert.Contains("Excede la duración del plan", mensajes);
            Assert.Contains("Candados pendientes (1)", mensajes);          // C bloquea a D
            Assert.Contains(mensajes, m => m.StartsWith("Rezago: 6 materia(s)"));
            Assert.Contains("Sin materias registradas en el periodo", mensajes);
        }

        [Fact]
        public void AlumnoQueYaTerminoNoTieneAlertasDeRezago()
        {
            var todas = new[] { "A", "B", "C", "Teoría de Juegos", "D", "E", "F" }.Select(n => Datos.Materia(n, "aprobada")).ToArray();
            Assert.Empty(Mensajes(Datos.Alumno("S21000001", todas)));
        }

        [Fact]
        public void EnPausaNoSeExigenMateriasRegistradas()
        {
            var alumno = Datos.Alumno("S21000001");
            alumno.PeriodosSinInscripcion.Add(Periodo);
            Assert.DoesNotContain("Sin materias registradas en el periodo", Mensajes(alumno));
        }

        [Fact]
        public void FaltasATutoriaDelPeriodo()
        {
            var alumno = Datos.Alumno("PRUEBA10");
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "falto" });
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 2, Asistencia = "falto" });
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = "Febrero - Julio 2026", Sesion = 1, Asistencia = "falto" });
            Assert.Equal(new[] { "Faltó a 2 tutoría(s)" }, Mensajes(alumno));
        }

        [Fact]
        public void AlertasPorMateriaSegunSuUltimoIntento()
        {
            var alumno = Datos.Alumno("PRUEBA10",
                Datos.Materia("A", "reprobada"),                                         // 1er intento reprobado...
                Datos.Materia("A", "aprobada"),                                          // ...y luego aprobada: sin alerta
                Datos.Materia("B", "reprobada", inscripcion: "segunda", examen: "ultima"),
                Datos.Materia("C", "cursando", riesgo: true));

            var alertas = _alertas.Calcular(new[] { alumno }, Periodo);
            Assert.Equal(new[] { "Candidato a Baja", "Riesgo Manual" }, alertas.Select(a => a.Mensaje));
            Assert.Equal(new[] { 100, 60 }, alertas.Select(a => a.Score));   // de la más grave a la menos grave
        }

        [Fact]
        public void LosAlumnosDadosDeBajaNoGeneranAlertas()
        {
            var alumno = Datos.Alumno("S21000001", Datos.Materia("B", "reprobada", riesgo: true));
            alumno.Situacion = "Baja Definitiva";
            Assert.Empty(Mensajes(alumno));
        }
    }

    public class ServicioReportesTests
    {
        private const string Periodo = "Agosto - Enero 2027";
        private readonly ServicioReportes _reportes;

        public ServicioReportesTests()
        {
            var planes = PlanesEnMemoria.ConPlanCorto();
            _reportes = new ServicioReportes(planes, new ServicioAlertas(planes));
        }

        [Fact]
        public void PanelDelGrupo_CalculaPeriodoAvanceYRezago()
        {
            // S25 en Agosto - Enero 2027 cursa el 3°: rezago de los periodos 1-2 sin aprobar = B, C, Optativa 1
            var alumno = Datos.Alumno("S25000001",
                Datos.Materia("A", "aprobada", creditos: 6),
                Datos.Materia("A", "aprobada", creditos: 6),                 // repetida: cuenta una sola vez
                Datos.Materia("E", "cursando", periodo: Periodo));

            var fila = Assert.Single(_reportes.PanelGrupo(new[] { alumno }, Periodo));
            Assert.Equal(3, fila.PeriodoQueCursa);
            Assert.Equal(4, fila.DuracionPlan);
            Assert.Equal(6, fila.CreditosAprobados);
            Assert.Equal(45, fila.CreditosTotales);
            Assert.Equal(13, fila.Avance);                                    // 6 / 45 = 13 %
            Assert.Equal(1, fila.EnCurso);
            Assert.Equal(3, fila.Rezago);
            Assert.Equal("Licenciatura de Prueba", fila.Programa);
            Assert.True(fila.Alertas > 0);
        }

        [Fact]
        public void PanelDelGrupo_AlumnoSinPlan()
        {
            var alumno = Datos.Alumno("PRUEBA10");
            alumno.PlanEstudio = "Generico";
            var fila = Assert.Single(_reportes.PanelGrupo(new[] { alumno }, Periodo));
            Assert.Equal("Sin plan", fila.Programa);
            Assert.Equal(0, fila.Avance);
            Assert.Null(fila.PeriodoQueCursa);
        }

        [Fact]
        public void ReporteGlobal_IncluyeRezagoManualYAutomaticoSinRepetir()
        {
            var alumno = Datos.Alumno("S25000001",
                Datos.Materia("A", "aprobada"),
                Datos.Materia("B", "cursando", rezago: true),   // rezago manual Y automático: aparece una vez
                Datos.Materia("F", "cursando", riesgo: true));

            var reporte = _reportes.ReporteGlobal(new[] { alumno }, Periodo);

            Assert.Equal(new[] { "F" }, reporte.Riesgos.Select(g => g.Materia));
            Assert.Equal(new[] { "B", "C", "Optativa 1" }, reporte.Rezagos.Select(g => g.Materia));
            Assert.Equal(new[] { "C" }, reporte.Seriadas.Select(g => g.Materia));   // C es candado de D
            Assert.All(reporte.Rezagos, g => Assert.Single(g.Alumnos));
        }
    }

    public class GeneradorDocumentoTutoriasTests
    {
        [Fact]
        public void EscapaLosTextosParaQueNoRompanElDocumento()
        {
            var alumno = Datos.Alumno("S21000001");
            alumno.Nombre = "Pérez <López>";
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = "Agosto - Enero 2027", Sesion = 1, Asistencia = "asistio", Comentarios = "Promedio < 7 & faltas" });

            string html = GeneradorDocumentoTutorias.Generar(alumno, "Economía");

            Assert.Contains("Pérez &lt;López&gt;", html);              // los acentos se conservan
            Assert.Contains("Promedio &lt; 7 &amp; faltas", html);
            Assert.Contains("Reporte de Tutoría Académica", html);
            Assert.Contains("Economía", html);
            Assert.Contains("Asistió", html);
        }

        [Fact]
        public void ImprimeElFormatoGuiadoConCasillasYOmiteSesionesVacias()
        {
            var alumno = Datos.Alumno("S24000001");
            alumno.Tutorias.Add(new TutoriaRecord
            {
                Periodo = "Agosto - Enero 2027", Sesion = 1, Fecha = "2026-09-10", Asistencia = "asistio", Modalidad = "Presencial",
                Temas = { CatalogoTutorias.Temas[0] },
                Situaciones = { "Económica", "Emocional o psicológica" },
                Canalizaciones = { CatalogoTutorias.Canalizaciones[0] },
                OtraCanalizacion = "Bolsa de trabajo",
                Acuerdos = "Acudir a CENDHIU",
                RequiereSeguimiento = true
            });
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = "Agosto - Enero 2027", Sesion = 2, Asistencia = "pendiente" });   // vacía

            string html = GeneradorDocumentoTutorias.Generar(alumno, "Economía");

            Assert.Contains("&#9746; Económica", html);                                   // marcada
            Assert.Contains("&#9744; Salud física", html);                                // del catálogo, sin marcar
            Assert.Contains("&#9746; " + CatalogoTutorias.Canalizaciones[0], html);
            Assert.Contains("Otra: Bolsa de trabajo", html);
            Assert.Contains("10/09/2026", html);
            Assert.Contains("Requiere seguimiento</th><td><span class='alerta'>Sí</span>", html);   // resumen del periodo
            Assert.Contains("Sesión 1", html);
            Assert.DoesNotContain("Sesión 2", html);                                      // la sesión vacía no se imprime
            Assert.Contains("Personales y de salud</b> (confidencial", html);              // categoría privada
            Assert.Contains("&#9746; Abierto", html);                                     // estado del caso
        }
    }

    public class TutoriasEnAlertasTests
    {
        [Fact]
        public void LaSesionQueRequiereSeguimientoGeneraUnaAlertaConLaCanalizacion()
        {
            const string periodo = "Agosto - Enero 2027";
            var alumno = Datos.Alumno("PRUEBA10");
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = periodo, Sesion = 1, Asistencia = "asistio", Situaciones = { "Económica" }, Canalizaciones = { "Becas y apoyos económicos" }, RequiereSeguimiento = true });
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = "Febrero - Julio 2026", Sesion = 1, RequiereSeguimiento = true });   // el caso venía abierto del periodo anterior

            var alerta = Assert.Single(new ServicioAlertas(PlanesEnMemoria.ConPlanCorto()).Calcular(new[] { alumno }, periodo));
            Assert.Equal("Requiere seguimiento de tutoría", alerta.Mensaje);
            Assert.Equal("Canalizado a", alerta.Etiqueta);
            Assert.Equal("Becas y apoyos económicos · caso abierto desde Febrero - Julio 2026", alerta.Materia);   // una sola alerta, con su origen

            // Si en una sesión posterior el tutor cierra el caso, la alerta desaparece
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = periodo, Sesion = 2, Asistencia = "asistio", EstadoCaso = "Cerrado" });
            Assert.Empty(new ServicioAlertas(PlanesEnMemoria.ConPlanCorto()).Calcular(new[] { alumno }, periodo));
        }
    }
}
