using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Trayectorias estándar y larga, carga menor a la del plan, materias críticas, temas con nombre de materia y ficha
    public class TrayectoriaYCriticasTests
    {
        private const string Periodo = "Agosto - Enero 2027";   // un S25 cursa el 3er periodo
        private static readonly DateTime Hoy = new(2026, 10, 22);
        private readonly ServicioAlertas _alertas = new(PlanesEnMemoria.ConPlanCorto());

        private List<Alerta> Alertas(AlumnoAvance a) => _alertas.Calcular(new[] { a }, Periodo);

        [Fact]
        public void LaTrayectoriaLargaPorDefectoEsUnaVezYMediaElPlan()
        {
            var plan = Datos.PlanCorto();
            Assert.Equal(6, ReglasTrayectoria.PeriodosTrayectoriaLarga(plan));            // 4 periodos x 1.5
            plan.PeriodosTrayectoriaLarga = 7;
            Assert.Equal(7, ReglasTrayectoria.PeriodosTrayectoriaLarga(plan));
        }

        [Fact]
        public void CreditosEsperadosYEstadoDeLaTrayectoria()
        {
            // Plan corto: periodo 1 = 10 créditos, periodo 2 = 11 (sin AFEL). Al cursar el 3° ya concluyó 2.
            var s = ReglasTrayectoria.Calcular(Datos.Alumno("S25000001", Datos.Materia("A", "aprobada")), Datos.PlanCorto(), Periodo);
            Assert.Equal(21, s.CreditosEstandar);    // materias de los periodos 1 y 2
            Assert.Equal(14, s.CreditosLarga);       // a ritmo de 6 periodos: 2 de 6 = periodo 1.33 del plan = 10 + 1/3 de 11
            Assert.Equal(6, s.CreditosAprobados);
            Assert.Equal("debajo-minimo", s.Trayectoria);

            Assert.Equal("estandar", ReglasTrayectoria.EstadoTrayectoria(23, 23, 15, 2));
            Assert.Equal("debajo-estandar", ReglasTrayectoria.EstadoTrayectoria(18, 23, 15, 2));
            Assert.Null(ReglasTrayectoria.EstadoTrayectoria(0, 0, 0, 0));                 // primer periodo
        }

        [Fact]
        public void AlertasDeTrayectoria()
        {
            var lejos = Alertas(Datos.Alumno("S25000001", Datos.Materia("A", "aprobada")));
            var minimo = Assert.Single(lejos, a => a.Mensaje.StartsWith("Debajo"));
            Assert.Equal("Debajo del mínimo (trayectoria larga)", minimo.Mensaje);
            Assert.True(minimo.Score >= ReglasSemaforo.ScoreGrave);                        // rojo

            var cerca = Alertas(Datos.Alumno("S25000001", Datos.Materia("A", "aprobada"), Datos.Materia("B", "aprobada"), Datos.Materia("C", "aprobada")));
            Assert.Contains(cerca, a => a.Mensaje == "Debajo de la trayectoria estándar");  // 18 créditos: entre 14 y 21
        }

        [Fact]
        public void AlumnoAlCorrienteNoQuedaDebajoDeLaEstandar()
        {
            // Caso real que salió mal: aprobó todo lo de sus periodos concluidos (más una AFEL), solo le falta una materia
            // del periodo que cursa y faltó a una tutoría. No debe salir debajo de la estándar ni en rojo.
            var alumno = Datos.Alumno("S25000001",
                Datos.Materia("A", "aprobada", creditos: 6), Datos.Materia("B", "aprobada", creditos: 4),
                Datos.Materia("C", "aprobada", creditos: 6), Datos.Materia("Teoría de Juegos", "aprobada", creditos: 5),
                Datos.Materia("AFEL 1", "aprobada", creditos: 6), Datos.Materia("E", "cursando", periodo: Periodo));
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "falto" });

            var alertas = Alertas(alumno);
            Assert.DoesNotContain(alertas, a => a.Mensaje.StartsWith("Debajo"));
            Assert.Contains(alertas, a => a.Mensaje == "Carga menor a la del plan");              // le falta D este periodo
            Assert.Equal("amarillo", ReglasSemaforo.Calcular(alumno, Periodo, alertas, Hoy).Sistema);  // la carga no escala a rojo
        }

        [Fact]
        public void CargaMenorALaDelPlan()
        {
            // En su 3er periodo el plan pide D y E; solo registró E
            var alumno = Datos.Alumno("S25000001", Datos.Materia("A", "aprobada"), Datos.Materia("E", "cursando", periodo: Periodo));
            var carga = Assert.Single(Alertas(alumno), a => a.Mensaje == "Carga menor a la del plan");
            Assert.Equal("Registró 1 de las 2 materias de su 3° periodo.", carga.Materia);
        }

        [Fact]
        public void SiAdelantoUnaMateriaDeSuPeriodoNoSeLePideDeNuevo()
        {
            // En su 3er periodo el plan pide D y E; D ya la aprobó antes y ahora cursa E: carga completa
            var alumno = Datos.Alumno("S25000001", Datos.Materia("A", "aprobada"), Datos.Materia("B", "aprobada"), Datos.Materia("C", "aprobada"),
                Datos.Materia("Teoría de Juegos", "aprobada"), Datos.Materia("D", "aprobada"), Datos.Materia("E", "cursando", periodo: Periodo));
            Assert.DoesNotContain(Alertas(alumno), a => a.Mensaje == "Carga menor a la del plan");
        }

        [Fact]
        public void MateriasCriticasPorPorcentajeDeReprobacion()
        {
            var alumnos = new[]
            {
                Datos.Alumno("S25000001", Datos.Materia("A", "reprobada"), Datos.Materia("A", "aprobada"), Datos.Materia("B", "reprobada")),
                Datos.Alumno("S25000002", Datos.Materia("A", "reprobada"), Datos.Materia("C", "cursando", periodo: Periodo, riesgo: true)),
                Datos.Alumno("S25000003", Datos.Materia("A", "cursando", periodo: Periodo)),
            };
            var lista = ReglasMateriasCriticas.Calcular(alumnos, Periodo);

            var a = lista[0];
            Assert.Equal("A", a.Materia);
            Assert.Equal(3, a.Intentos);             // 2 reprobadas + 1 aprobada; "cursando" no cuenta
            Assert.Equal(67, a.Porcentaje);
            Assert.Equal(3, a.Alumnos);
            Assert.Equal(1, a.CursandoAhora);
            Assert.True(a.Critica);

            Assert.False(lista.Single(m => m.Materia == "B").Critica);           // un solo intento: muestra insuficiente
            Assert.Equal(1, lista.Single(m => m.Materia == "C").EnRiesgoAhora);
        }

        [Fact]
        public void TemasConNombreDeMateriaYProblematicasComoSenalesInformativas()
        {
            var alumno = Datos.Alumno("PRUEBA10", Datos.Materia("A", "cursando", periodo: Periodo, riesgo: true));
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Asistencia = "asistio", Situaciones = { "Trabaja más de 20 horas a la semana", "Violencia o acoso" } });
            var criticas = new[] { new MateriaCritica { Materia = "A", Porcentaje = 40, Critica = true } };

            var s = ReglasSemaforo.Calcular(alumno, Periodo, Alertas(alumno), Hoy, criticas);

            Assert.Equal("amarillo", s.Sistema);                                       // las informativas no cambian el color
            Assert.Contains(s.TemasSugeridos, t => t.Contains("cómo va en A"));
            Assert.Contains(s.TemasSugeridos, t => t.Contains("horario de trabajo"));
            Assert.Contains(s.Senales, x => x.Informativa && x.Texto == "Trabaja más de 20 horas a la semana (registrado por su tutor)");
            Assert.Contains(s.Senales, x => x.Informativa && x.Texto.Contains("confidencial"));
            Assert.DoesNotContain(s.Senales, x => x.Texto.Contains("Violencia"));    // lo privado no se detalla
            Assert.Contains(s.Senales, x => x.Texto == "Cursa A, materia crítica (40 % de reprobación)");
        }

        [Fact]
        public void LaFichaResumeLoNecesarioSinDetallarLoConfidencial()
        {
            var plan = Datos.PlanCorto();
            plan.Apoyos.Add(new ApoyoPlan { Nombre = "Asesoría de pares", Detalle = "Martes 13:00, sala 2" });
            var alumno = Datos.Alumno("S25000001", Datos.Materia("A", "aprobada"), Datos.Materia("E", "cursando", periodo: Periodo, riesgo: true));
            alumno.Nombre = "Ruiz <Ana>";
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = Periodo, Sesion = 1, Fecha = "2026-09-10", Asistencia = "asistio", Situaciones = { "Violencia o acoso" }, Acuerdos = "Ir a CENDHIU" });

            var situacion = ReglasTrayectoria.Calcular(alumno, plan, Periodo);
            situacion.Semaforo = ReglasSemaforo.Calcular(alumno, Periodo, Alertas(alumno), Hoy);
            string html = GeneradorFichaTutoria.Generar(alumno, plan, situacion, Periodo, Hoy);

            Assert.Contains("Ruiz &lt;Ana&gt;", html);
            Assert.Contains("Riesgo alto", html);                                     // debajo del mínimo
            Assert.Contains("Debajo del mínimo (trayectoria larga)", html);
            Assert.Contains("2.ª tutoría del periodo", html);
            Assert.Contains("Martes 13:00, sala 2", html);
            Assert.Contains("situación personal o de salud (confidencial)", html);
            Assert.DoesNotContain("Violencia", html);
            Assert.Contains("Ir a CENDHIU", html);
            Assert.Contains("LGPDPPSO", html);
        }
    }
}
