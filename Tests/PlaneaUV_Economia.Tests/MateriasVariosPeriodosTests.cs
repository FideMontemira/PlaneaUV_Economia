using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Materias que duran más de un periodo (columna H de la plantilla; p. ej. servicio social: 2).
    // Mientras sigan "cursando" dentro de su duración, en el periodo siguiente no son rezago.
    public class MateriasVariosPeriodosTests
    {
        // S25... ingresó en agosto de 2025: periodo 4 = Febrero - Julio 2027, 5 = Agosto - Enero 2028, 6 = Febrero - Julio 2028
        private const string P4 = "Febrero - Julio 2027", P5 = "Agosto - Enero 2028", P6 = "Febrero - Julio 2028";

        // Plan corto con F (4° periodo) que dura 2 periodos, como el servicio social
        private static PlanInfo PlanConDuracion(int duracion = 2)
        {
            var plan = Datos.PlanCorto();
            plan.Materias.Single(m => m.Nombre == "F").PeriodosDuracion = duracion;
            return plan;
        }

        // Todo aprobado menos F, que empezó a cursar en P4
        private static AlumnoAvance AlumnoCursandoF(params MateriaCursada[] extra)
        {
            var a = Datos.Alumno("S25000001",
                Datos.Materia("A", "aprobada"), Datos.Materia("B", "aprobada"), Datos.Materia("C", "aprobada"),
                Datos.Materia("Teoría de Juegos", "aprobada"), Datos.Materia("D", "aprobada"), Datos.Materia("E", "aprobada"),
                Datos.Materia("F", "cursando", periodo: P4, creditos: 8));
            a.Materias.AddRange(extra);
            return a;
        }

        [Theory]
        [InlineData("", 1)]
        [InlineData("no aplica", 1)]
        [InlineData("1", 1)]
        [InlineData("2", 2)]
        [InlineData("0", -1)]      // fuera de rango: la plantilla se rechaza al subirla
        [InlineData("5", -1)]
        [InlineData("1.5", -1)]
        public void LeeLosPeriodosQueDuraDeLaColumnaH(string texto, int esperado)
        {
            Assert.Equal(esperado, RepositorioPlanesExcel.LeerDuracion(texto));
        }

        [Fact]
        public void EnElPeriodoSiguienteSigueEnCursoYNoEsRezago()
        {
            var s = ReglasTrayectoria.Calcular(AlumnoCursandoF(), PlanConDuracion(), P5);
            Assert.Equal(5, s.PeriodoQueCursa);
            Assert.DoesNotContain("F", s.MateriasEnRezago);
            Assert.Equal(new[] { "F" }, s.EnCursoDesdeAntes);
        }

        [Fact]
        public void PasadaSuDuracionSinResultadoSiEsRezago()
        {
            var s = ReglasTrayectoria.Calcular(AlumnoCursandoF(), PlanConDuracion(), P6);
            Assert.Contains("F", s.MateriasEnRezago);
            Assert.Empty(s.EnCursoDesdeAntes);
        }

        [Fact]
        public void UnaMateriaDeUnSoloPeriodoSigueSiendoRezago()
        {
            var s = ReglasTrayectoria.Calcular(AlumnoCursandoF(), PlanConDuracion(1), P5);
            Assert.Contains("F", s.MateriasEnRezago);
        }

        [Fact]
        public void SiTambienSeRegistraCursandoEnElSegundoPeriodoCuentaDesdeElPrimero()
        {
            var alumno = AlumnoCursandoF(Datos.Materia("F", "cursando", periodo: P5, creditos: 8));
            Assert.DoesNotContain("F", ReglasTrayectoria.Calcular(alumno, PlanConDuracion(), P5).MateriasEnRezago);
            Assert.Contains("F", ReglasTrayectoria.Calcular(alumno, PlanConDuracion(), P6).MateriasEnRezago);   // 3er periodo: ya pasó
        }

        [Fact]
        public void AlAprobarlaEnElSegundoPeriodoYaNoQuedaPendiente()
        {
            var alumno = AlumnoCursandoF(Datos.Materia("F", "aprobada", periodo: P5, creditos: 8));
            var s = ReglasTrayectoria.Calcular(alumno, PlanConDuracion(), P6);
            Assert.DoesNotContain("F", s.MateriasEnRezago);
            Assert.Empty(s.EnCursoDesdeAntes);
        }

        [Fact]
        public void LasAlertasNoMarcanRezagoNiFaltaDeInscripcion()
        {
            var planes = new PlanesEnMemoria(new Dictionary<string, PlanInfo> { ["Plan_Corto"] = PlanConDuracion() });
            var alertas = new ServicioAlertas(planes).Calcular(new[] { AlumnoCursandoF() }, P5);
            Assert.DoesNotContain(alertas, a => a.Mensaje.StartsWith("Rezago"));
            Assert.DoesNotContain(alertas, a => a.Mensaje == "Sin materias registradas en el periodo");
        }

        [Fact]
        public void ElReporteDelPeriodoLaMuestraComoQueContinua()
        {
            var planes = new PlanesEnMemoria(new Dictionary<string, PlanInfo> { ["Plan_Corto"] = PlanConDuracion() });
            var servicio = new ServicioReportePeriodo(planes, new ServicioAlertas(planes));
            var alumno = AlumnoCursandoF();

            // Al cerrar P4 le queda otro periodo: "continúa" (no "sin resultado") y no se propone para el siguiente
            var cierre = servicio.Resumir(alumno, P4, new DateTime(2027, 9, 1));
            Assert.Equal("continua", cierre.Materias.Single(m => m.Nombre == "F").Resultado);
            Assert.DoesNotContain(cierre.SiguientePeriodo, m => m.Nombre == "F");
            Assert.DoesNotContain("F", cierre.Rezago);

            // En P5 (en curso) aparece entre las materias del periodo
            var actual = servicio.Resumir(alumno, P5, new DateTime(2027, 10, 1));
            Assert.Equal("cursando", actual.Materias.Single(m => m.Nombre == "F").Resultado);
        }
    }
}
