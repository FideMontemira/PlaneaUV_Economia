using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Caso de tutoría que pasa de un periodo a otro y seguimiento de las canalizaciones
    public class CasoTutoriaTests
    {
        private const string P1 = "Agosto - Enero 2027", P2 = "Febrero - Julio 2027";
        private static readonly DateTime Hoy = new(2027, 3, 1);
        private readonly ServicioAlertas _alertas = new(PlanesEnMemoria.ConPlanCorto());

        private static AlumnoAvance ConCasoEnCendhiu()
        {
            var a = Datos.Alumno("PRUEBA10");
            a.Tutorias.Add(new TutoriaRecord { Periodo = P1, Sesion = 1, Asistencia = "asistio", Canalizaciones = { "CENDHIU (desarrollo humano y apoyo psicológico)" },
                                               EstadoCaso = "Abierto", RequiereSeguimiento = true, Acuerdos = "Acudir a CENDHIU" });
            return a;
        }

        private SemaforoAlumno Semaforo(AlumnoAvance a, string periodo) =>
            ReglasSemaforo.Calcular(a, periodo, _alertas.Calcular(new[] { a }, periodo), Hoy);

        [Fact]
        public void ElCasoAbiertoPasaAlSiguientePeriodo()
        {
            var a = ConCasoEnCendhiu();
            var caso = ReglasCaso.Vigente(a, P2)!;
            Assert.True(caso.Abierto);
            Assert.Equal(P1, caso.Desde);
            Assert.Equal("pendiente", Assert.Single(caso.Canalizaciones).Acudio);

            var s = Semaforo(a, P2);
            Assert.Equal("Abierto", s.EstadoCaso);
            Assert.Equal(P1, s.CasoDesde);
            Assert.Equal("Acudir a CENDHIU", s.AcuerdoPendiente);                   // el acuerdo también sigue
            Assert.Contains(s.TemasSugeridos, t => t.StartsWith("Confirmar si ya acudió a CENDHIU"));

            var alerta = Assert.Single(_alertas.Calcular(new[] { a }, P2));
            Assert.Equal("Requiere seguimiento de tutoría", alerta.Mensaje);
            Assert.Contains("caso abierto desde " + P1, alerta.Materia);
        }

        [Fact]
        public void UnCasoCerradoEnUnPeriodoAnteriorYaNoSeMuestra()
        {
            var a = ConCasoEnCendhiu();
            a.Tutorias.Add(new TutoriaRecord { Periodo = P1, Sesion = 2, Asistencia = "asistio", EstadoCaso = "Cerrado" });
            Assert.Equal("Cerrado", ReglasCaso.Vigente(a, P1)!.Estado);           // en su periodo sí se ve que se cerró
            Assert.Null(ReglasCaso.Vigente(a, P2));
            Assert.Empty(_alertas.Calcular(new[] { a }, P2));
        }

        [Fact]
        public void UnPeriodoAnteriorAlCasoNoLoVe()
        {
            Assert.Null(ReglasCaso.Vigente(ConCasoEnCendhiu(), "Febrero - Julio 2026"));
        }

        [Fact]
        public void SeguimientoDeLaCanalizacion()
        {
            var a = ConCasoEnCendhiu();
            a.Tutorias.Add(new TutoriaRecord { Periodo = P2, Sesion = 1, Asistencia = "asistio",
                                               AcudioCanalizaciones = { "CENDHIU (desarrollo humano y apoyo psicológico)=no" } });
            var s = Semaforo(a, P2);
            Assert.Equal("no", Assert.Single(s.CanalizacionesCaso).Acudio);
            Assert.Contains(s.Senales, x => x.Informativa && x.Texto.StartsWith("No acudió a CENDHIU"));
            Assert.Contains(s.TemasSugeridos, t => t.StartsWith("Preguntar por qué no acudió a CENDHIU"));
            Assert.Contains("(no acudió)", Assert.Single(_alertas.Calcular(new[] { a }, P2)).Materia);

            // En una sesión posterior confirma que ya fue: cuenta la última respuesta
            a.Tutorias.Add(new TutoriaRecord { Periodo = P2, Sesion = 2, Asistencia = "asistio",
                                               AcudioCanalizaciones = { "CENDHIU (desarrollo humano y apoyo psicológico)=si" } });
            Assert.Equal("si", Assert.Single(Semaforo(a, P2).CanalizacionesCaso).Acudio);
        }

        [Fact]
        public void CadaCanalizacionSeConcluyePorSeparado()
        {
            const string A = "CENDHIU (desarrollo humano y apoyo psicológico)", B = "Becas y apoyos económicos";
            var a = Datos.Alumno("PRUEBA10");
            a.Tutorias.Add(new TutoriaRecord { Periodo = P1, Sesion = 1, Asistencia = "asistio", Canalizaciones = { A, B }, EstadoCaso = "Abierto" });
            a.Tutorias.Add(new TutoriaRecord { Periodo = P1, Sesion = 2, Asistencia = "asistio", AcudioCanalizaciones = { A + "=concluida", B + "=si" } });

            var caso = ReglasCaso.Vigente(a, P1)!;
            Assert.True(caso.Abierto);                                                   // B sigue en atención
            Assert.Equal(new[] { B }, ReglasCaso.Activas(caso.Canalizaciones).Select(c => c.Area));
            var alerta = Assert.Single(_alertas.Calcular(new[] { a }, P1));
            Assert.Equal(B, alerta.Materia);                                             // A ya no se reporta

            // B también concluye: el caso sigue abierto (lo cierra el tutor) y se sugiere cerrarlo
            a.Tutorias.Add(new TutoriaRecord { Periodo = P1, Sesion = 3, Asistencia = "asistio", AcudioCanalizaciones = { B + "=concluida" } });
            Assert.Contains("Todas las canalizaciones concluyeron", Assert.Single(_alertas.Calcular(new[] { a }, P1)).Materia);
            Assert.Contains(Semaforo(a, P1).TemasSugeridos, t => t.StartsWith("Todas las canalizaciones concluyeron"));

            // Si se vuelve a canalizar a A, empieza de nuevo como pendiente
            a.Tutorias.Add(new TutoriaRecord { Periodo = P2, Sesion = 1, Asistencia = "asistio", Canalizaciones = { A } });
            var deNuevo = ReglasCaso.Vigente(a, P2)!.Canalizaciones.Single(c => c.Area == A);
            Assert.Equal("pendiente", deNuevo.Acudio);
            Assert.Equal(P2, deNuevo.Periodo);
        }

        [Fact]
        public void LosValoresInvalidosSeDescartanAntesDeGuardar()
        {
            var t = new TutoriaRecord
            {
                Asistencia = "valor-raro", EstadoCaso = "EstadoInventado", ValoracionRiesgo = "zzz", SemaforoSistema = "morado",
                Fecha = "no-es-fecha", FechaRevision = "2026-13-45", OtraCanalizacion = "Bolsa de trabajo | DIF",
                Canalizaciones = { "CENDHIU", "CENDHIU", " " },
                AcudioCanalizaciones = { "=si", "sin-igual", "CENDHIU=quizas", "Bolsa de trabajo | DIF=si", "Área nueva=concluida" }
            };
            ValidacionAlumno.Normalizar(t);

            Assert.Equal("pendiente", t.Asistencia);
            Assert.Equal("", t.EstadoCaso);                      // un estado inventado ya no cuenta como caso abierto
            Assert.False(t.RequiereSeguimiento);
            Assert.Equal("", t.ValoracionRiesgo);
            Assert.Equal("", t.SemaforoSistema);
            Assert.Equal("", t.Fecha);
            Assert.Equal("", t.FechaRevision);
            Assert.Equal("Bolsa de trabajo / DIF", t.OtraCanalizacion);
            Assert.Equal(new[] { "CENDHIU" }, t.Canalizaciones);
            Assert.Equal(new[] { "Bolsa de trabajo / DIF=si", "Área nueva=concluida" }, t.AcudioCanalizaciones);
        }

        [Fact]
        public void UnaBarraEnElTextoNoParteLaListaEnElExcel()
        {
            using var carpeta = new CarpetaTemporal();
            var repo = new RepositorioAlumnosExcel(Path.Combine(carpeta.Ruta, "AvanceAlumnos.xlsx"));
            var a = Datos.Alumno("S24000001");
            a.Tutorias.Add(new TutoriaRecord { Periodo = P2, Sesion = 1, Asistencia = "asistio", OtraCanalizacion = "Bolsa | DIF",
                                               AcudioCanalizaciones = { "Bolsa | DIF=si" } });
            repo.Guardar(a);
            var t = Assert.Single(repo.Buscar("S24000001")!.Tutorias);
            Assert.Equal("Bolsa / DIF", t.OtraCanalizacion);
            Assert.Equal(new[] { "Bolsa / DIF=si" }, t.AcudioCanalizaciones);   // el área y su seguimiento siguen coincidiendo
        }

        [Fact]
        public void ElSeguimientoSeGuardaEnElExcel()
        {
            using var carpeta = new CarpetaTemporal();
            var repo = new RepositorioAlumnosExcel(Path.Combine(carpeta.Ruta, "AvanceAlumnos.xlsx"));
            var a = Datos.Alumno("S24000001");
            a.Tutorias.Add(new TutoriaRecord { Periodo = P2, Sesion = 1, Asistencia = "asistio", AcudioCanalizaciones = { "CENDHIU=si", "Becas y apoyos económicos=no" } });
            repo.Guardar(a);
            Assert.Equal(new[] { "CENDHIU=si", "Becas y apoyos económicos=no" }, Assert.Single(repo.Buscar("S24000001")!.Tutorias).AcudioCanalizaciones);
        }
    }
}
