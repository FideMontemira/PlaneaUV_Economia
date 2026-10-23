using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    public class SemaforoTests
    {
        private const string Periodo = "Agosto - Enero 2027";
        private static readonly DateTime Hoy = new(2026, 10, 22);
        private readonly ServicioAlertas _alertas = new(PlanesEnMemoria.ConPlanCorto());

        private SemaforoAlumno Semaforo(AlumnoAvance alumno) =>
            ReglasSemaforo.Calcular(alumno, Periodo, _alertas.Calcular(new[] { alumno }, Periodo), Hoy);

        // Alumno sin matrícula con año (no se calcula periodo) para controlar exactamente las señales
        private static AlumnoAvance SinSenales() => Datos.Alumno("PRUEBA10");

        [Fact]
        public void SinSenalesEsVerdeConUnTemaGeneral()
        {
            var s = Semaforo(SinSenales());
            Assert.Equal("verde", s.Sistema);
            Assert.Equal("verde", s.Final);
            Assert.Empty(s.Senales);
            Assert.Single(s.TemasSugeridos);
        }

        [Fact]
        public void UnaAlertaLeveEsAmarilloYUnaGraveEsRojo()
        {
            var leve = Semaforo(Datos.Alumno("PRUEBA10", Datos.Materia("A", "cursando", riesgo: true)));
            Assert.Equal("amarillo", leve.Sistema);
            Assert.Equal("Historial académico", Assert.Single(leve.Senales).Fuente);

            var grave = Semaforo(Datos.Alumno("PRUEBA10", Datos.Materia("B", "reprobada", inscripcion: "segunda", examen: "ultima")));
            Assert.Equal("rojo", grave.Sistema);
            Assert.Contains(grave.TemasSugeridos, t => t.Contains("situación reglamentaria en B"));   // nombra la materia
        }

        [Fact]
        public void FaltarATutoriaConSenalesEscalaARojo()
        {
            var soloFalta = SinSenales();
            soloFalta.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "falto" });
            Assert.Equal("amarillo", Semaforo(soloFalta).Sistema);   // la falta sola es atención, no riesgo alto

            var conRiesgo = Datos.Alumno("PRUEBA10", Datos.Materia("A", "cursando", riesgo: true));
            conRiesgo.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "falto" });
            var s = Semaforo(conRiesgo);
            Assert.Equal("rojo", s.Sistema);
            Assert.Contains(s.Senales, x => x.Texto == "No asistió a tutoría teniendo señales de riesgo");
        }

        [Fact]
        public void ElTutorPuedeElevarYDescartarConMotivo()
        {
            var alumno = Datos.Alumno("PRUEBA10", Datos.Materia("A", "cursando", riesgo: true));   // amarillo
            var sesion = new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "asistio", ValoracionRiesgo = "eleva", SemaforoSistema = "amarillo" };
            alumno.Tutorias.Add(sesion);
            Assert.Equal("rojo", Semaforo(alumno).Final);

            sesion.ValoracionRiesgo = "descarta";
            var sinMotivo = Semaforo(alumno);
            Assert.Equal("amarillo", sinMotivo.Final);                // descartar sin motivo no cuenta
            Assert.Null(sinMotivo.Valoracion);

            sesion.MotivoValoracion = "Ya aprobó el parcial de recuperación";
            var conMotivo = Semaforo(alumno);
            Assert.Equal("verde", conMotivo.Final);
            Assert.Equal("amarillo", conMotivo.Sistema);              // se conservan ambos
        }

        [Fact]
        public void SiAparecenSenalesMasGravesLaValoracionDejaDeAplicar()
        {
            var alumno = Datos.Alumno("PRUEBA10", Datos.Materia("B", "reprobada", inscripcion: "segunda", examen: "ultima"));   // rojo
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, ValoracionRiesgo = "descarta", MotivoValoracion = "x", SemaforoSistema = "amarillo" });
            var s = Semaforo(alumno);
            Assert.True(s.ValoracionDesactualizada);
            Assert.Equal("rojo", s.Final);
        }

        [Fact]
        public void ElevarSigueContandoAunqueElPropioReporteSubaElColor()
        {
            // El tutor elevó cuando el sistema marcaba verde; su mismo reporte abrió un caso (amarillo).
            // La valoración no debe perderse: queda al menos un nivel arriba de lo que marcaba al valorar.
            var alumno = SinSenales();
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "asistio", EstadoCaso = "En atención", RequiereSeguimiento = true,
                                                    ValoracionRiesgo = "eleva", SemaforoSistema = "verde" });
            var s = Semaforo(alumno);
            Assert.Equal("amarillo", s.Sistema);
            Assert.Equal("amarillo", s.Final);
            Assert.False(s.ValoracionDesactualizada);

            alumno.Tutorias[0].SemaforoSistema = "amarillo";                       // si elevó estando en amarillo, pasa a rojo
            Assert.Equal("rojo", Semaforo(alumno).Final);
        }

        [Fact]
        public void SoloNoAsistioCuentaComoInasistencia()
        {
            var alumno = SinSenales();
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "citado", Fecha = "2026-11-05" });
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 2, Asistencia = "justificado" });
            Assert.Equal("verde", Semaforo(alumno).Sistema);                       // citado o justificado no alertan
        }

        [Fact]
        public void EstadoDelCasoUltimoContactoYAcuerdoVencido()
        {
            var alumno = SinSenales();
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Fecha = "2026-09-02", Asistencia = "asistio", EstadoCaso = "Abierto", Acuerdos = "Acudir a CENDHIU", FechaRevision = "2026-10-01" });
            var s = Semaforo(alumno);
            Assert.Equal("Abierto", s.EstadoCaso);
            Assert.Equal(50, s.DiasSinContacto);
            Assert.True(s.RevisionVencida);
            Assert.Equal("amarillo", s.Sistema);                      // seguimiento abierto + revisión vencida
            Assert.Contains(s.TemasSugeridos, t => t.Contains("Acudir a CENDHIU"));

            // Al cerrar el caso en una sesión posterior ya no hay alerta de seguimiento ni acuerdo pendiente
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 2, Fecha = "2026-10-20", Asistencia = "asistio", EstadoCaso = "Cerrado" });
            var cerrado = Semaforo(alumno);
            Assert.Equal("Cerrado", cerrado.EstadoCaso);
            Assert.Equal(2, cerrado.DiasSinContacto);
            Assert.Null(cerrado.AcuerdoPendiente);
            Assert.Equal("verde", cerrado.Sistema);
        }

        [Fact]
        public void AlumnoDeBajaNoTieneSemaforo()
        {
            var alumno = Datos.Alumno("S21000001");
            alumno.Situacion = "Baja Temporal";
            Assert.Equal("sin", Semaforo(alumno).Final);
        }

        [Fact]
        public void ElPanelDelGrupoTraeElSemaforo()
        {
            var planes = PlanesEnMemoria.ConPlanCorto();
            var reportes = new ServicioReportes(planes, new ServicioAlertas(planes));
            var fila = Assert.Single(reportes.PanelGrupo(new[] { Datos.Alumno("PRUEBA10", Datos.Materia("A", "cursando", riesgo: true)) }, Periodo, Hoy));
            Assert.Equal("amarillo", fila.Semaforo);
            Assert.Equal(2, fila.NivelSemaforo);
        }
    }
}
