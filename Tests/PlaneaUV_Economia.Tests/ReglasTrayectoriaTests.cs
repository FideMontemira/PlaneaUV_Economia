using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    public class ReglasTrayectoriaTests
    {
        [Theory]
        [InlineData("S21016359", 2021)]
        [InlineData("s21016359", 2021)]
        [InlineData("zS22013691", 2022)]
        [InlineData("  S26000001 ", 2026)]
        public void AnioIngreso_SaleDeLaMatricula(string matricula, int esperado)
        {
            Assert.Equal(esperado, ReglasTrayectoria.AnioIngresoDeMatricula(matricula));
        }

        [Theory]
        [InlineData("PRUEBA10")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("21016359")]
        public void AnioIngreso_NullSiLaMatriculaNoTraeAnio(string? matricula)
        {
            Assert.Null(ReglasTrayectoria.AnioIngresoDeMatricula(matricula));
        }

        [Theory]
        [InlineData("Agosto - Enero 2022", 1)]   // primer periodo de un S21
        [InlineData("Febrero - Julio 2022", 2)]
        [InlineData("Agosto - Enero 2023", 3)]
        [InlineData("Agosto - Enero 2027", 11)]
        [InlineData("Febrero - Julio 2027", 12)]
        [InlineData("Agosto - Enero 2021", -1)]  // antes de ingresar
        public void NumeroDePeriodo_CuentaDesdeElIngreso(string periodo, int esperado)
        {
            Assert.Equal(esperado, ReglasTrayectoria.NumeroDePeriodo(2021, periodo));
        }

        [Fact]
        public void NombreDePeriodo_EsLaInversaDeNumeroDePeriodo()
        {
            for (int i = 1; i <= 14; i++)
                Assert.Equal(i, ReglasTrayectoria.NumeroDePeriodo(2021, ReglasTrayectoria.NombreDePeriodo(2021, i)));
            Assert.Equal("Agosto - Enero 2022", ReglasTrayectoria.NombreDePeriodo(2021, 1));
            Assert.Equal("Febrero - Julio 2022", ReglasTrayectoria.NombreDePeriodo(2021, 2));
        }

        [Fact]
        public void PeriodoQueCursa_DescuentaSoloLasPausasAnteriores()
        {
            // S24 en Agosto - Enero 2027 va en su 5° periodo corrido
            Assert.Equal(5, ReglasTrayectoria.PeriodoQueCursa("S24000001", "Agosto - Enero 2027", new List<string>()));

            // Dos pausas anteriores: 5 - 2 = 3
            var pausas = new List<string> { "Agosto - Enero 2026", "Febrero - Julio 2026" };
            Assert.Equal(3, ReglasTrayectoria.PeriodoQueCursa("S24000001", "Agosto - Enero 2027", pausas));

            // Una pausa futura o la del mismo periodo no se descuentan
            Assert.Equal(5, ReglasTrayectoria.PeriodoQueCursa("S24000001", "Agosto - Enero 2027", new List<string> { "Febrero - Julio 2027", "Agosto - Enero 2027" }));
        }

        [Fact]
        public void PeriodoQueCursa_NullSinAnioDeIngreso()
        {
            Assert.Null(ReglasTrayectoria.PeriodoQueCursa("PRUEBA10", "Agosto - Enero 2027", new List<string>()));
        }

        [Fact]
        public void DuracionPlan_NoCuentaLasAfel()
        {
            Assert.Equal(4, ReglasTrayectoria.DuracionPlan(Datos.PlanCorto()));
            Assert.Equal(0, ReglasTrayectoria.DuracionPlan(null));
        }

        [Fact]
        public void Rezago_SonLasMateriasDePeriodosAnterioresNoAprobadas()
        {
            var plan = Datos.PlanCorto();
            var aprobadas = new HashSet<string> { "A" };
            // Cursa el 3°: pendientes de los periodos 1 y 2 = B, C, Optativa 1 (en orden de periodo)
            var rezago = ReglasTrayectoria.MateriasEnRezago(plan, 3, aprobadas).Select(m => m.Nombre);
            Assert.Equal(new[] { "B", "C", "Optativa 1" }, rezago);
        }

        [Fact]
        public void Rezago_UnaOptativaAprobadaDelCatalogoCubreElLugarDeOptativa()
        {
            var aprobadas = new HashSet<string> { "A", "B", "C", "Teoría de Juegos" };
            Assert.Empty(ReglasTrayectoria.MateriasEnRezago(Datos.PlanCorto(), 3, aprobadas));
        }

        [Fact]
        public void Rezago_LasAfelNuncaCuentan()
        {
            var todasMenosAfel = new HashSet<string> { "A", "B", "C", "Optativa 1", "D", "E", "F" };
            Assert.Empty(ReglasTrayectoria.MateriasEnRezago(Datos.PlanCorto(), 99, todasMenosAfel));
        }

        [Fact]
        public void Calcular_JuntaTodoParaLaPantalla()
        {
            var alumno = Datos.Alumno("S24000001", Datos.Materia("A", "aprobada"), Datos.Materia("B", "reprobada"));
            alumno.PeriodosSinInscripcion.Add("Febrero - Julio 2025");

            var s = ReglasTrayectoria.Calcular(alumno, Datos.PlanCorto(), "Agosto - Enero 2027");

            Assert.Equal(2024, s.AnioIngreso);
            Assert.Equal(5, s.PeriodosCorridos);
            Assert.Equal(4, s.PeriodoQueCursa);        // 5 corridos - 1 pausa
            Assert.Equal(1, s.PausasDescontadas);
            Assert.False(s.EnPausa);
            Assert.Equal(4, s.DuracionPlan);
            Assert.Equal(new[] { "B", "C", "Optativa 1", "D", "E" }, s.MateriasEnRezago);
            Assert.Equal(5, s.PeriodosDesdeIngreso.Count);
            Assert.Equal("Agosto - Enero 2025", s.PeriodosDesdeIngreso[0]);
        }

        [Fact]
        public void Calcular_MarcaCuandoElPeriodoDeTrabajoEsUnaPausa()
        {
            var alumno = Datos.Alumno("S24000001");
            alumno.PeriodosSinInscripcion.Add("Agosto - Enero 2027");
            Assert.True(ReglasTrayectoria.Calcular(alumno, Datos.PlanCorto(), "Agosto - Enero 2027").EnPausa);
        }

        [Fact]
        public void Calcular_SinAnioDeIngresoNoHayPeriodoNiRezago()
        {
            var s = ReglasTrayectoria.Calcular(Datos.Alumno("PRUEBA10"), Datos.PlanCorto(), "Agosto - Enero 2027");
            Assert.Null(s.AnioIngreso);
            Assert.Null(s.PeriodoQueCursa);
            Assert.Empty(s.MateriasEnRezago);
            Assert.Empty(s.PeriodosDesdeIngreso);
        }
    }
}
