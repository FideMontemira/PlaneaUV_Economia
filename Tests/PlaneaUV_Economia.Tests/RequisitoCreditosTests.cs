using ClosedXML.Excel;
using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Materias que solo se inscriben con un porcentaje de los créditos (servicio social, experiencia recepcional,
    // prácticas profesionales): columna G de la plantilla
    public class RequisitoCreditosTests
    {
        private const string P1 = "Agosto - Enero 2026", P2 = "Febrero - Julio 2026", P3 = "Agosto - Enero 2027";

        // Plan corto (45 créditos) con F (8 cr., 4° periodo) que pide el 70 %: 31.5 → 32 créditos
        private static PlanInfo PlanConRequisito()
        {
            var plan = Datos.PlanCorto();
            plan.Materias.Single(m => m.Nombre == "F").PorcentajeCreditos = 70;
            return plan;
        }

        [Theory]
        [InlineData("70", 70)]
        [InlineData("70 %", 70)]
        [InlineData("70%", 70)]
        [InlineData("0.7", 70)]          // celda con formato de porcentaje
        [InlineData("", 0)]
        [InlineData("no aplica", 0)]
        [InlineData("150", -1)]          // fuera de rango: la plantilla se rechaza al subirla
        [InlineData("-5", -1)]
        public void LeeElPorcentajeDeLaColumnaG(string texto, int esperado)
        {
            Assert.Equal(esperado, RepositorioPlanesExcel.LeerPorcentaje(texto));
        }

        [Fact]
        public void LosCreditosNecesariosSeRedondeanHaciaArriba()
        {
            Assert.Equal(259, ReglasTrayectoria.CreditosParaRequisito(new PlanInfo { CreditosTotales = 369 }, 70));   // 258.3
            Assert.Equal(252, ReglasTrayectoria.CreditosParaRequisito(new PlanInfo { CreditosTotales = 360 }, 70));   // exacto
            Assert.Equal(244, ReglasTrayectoria.CreditosParaRequisito(new PlanInfo { CreditosTotales = 348 }, 70));   // 243.6
            Assert.Equal(0, ReglasTrayectoria.CreditosParaRequisito(new PlanInfo { CreditosTotales = 348 }, 0));
        }

        [Fact]
        public void NoCumpleHastaLlegarAlPorcentaje()
        {
            var plan = PlanConRequisito();
            var alumno = Datos.Alumno("S25000001", Datos.Materia("A", "aprobada", creditos: 30));

            var r = Assert.Single(ReglasTrayectoria.RequisitosDeCreditos(plan, alumno, 30));
            Assert.Equal("F", r.Materia);
            Assert.Equal(32, r.CreditosNecesarios);
            Assert.False(r.Cumple);
            Assert.Equal(2, r.Faltan);
            Assert.Null(ReglasTrayectoria.SenalRequisitosCumplidos(new[] { r }));   // aún no hay aviso

            var cumple = Assert.Single(ReglasTrayectoria.RequisitosDeCreditos(plan, alumno, 32));
            Assert.True(cumple.Cumple);
            Assert.Equal(0, cumple.Faltan);
        }

        [Fact]
        public void AvisaAlTutorCuandoYaPuedeInscribirlaYNoLaHaInscrito()
        {
            var plan = PlanConRequisito();
            var alumno = Datos.Alumno("S25000001", Datos.Materia("A", "aprobada", creditos: 32));
            var aviso = ReglasTrayectoria.SenalRequisitosCumplidos(ReglasTrayectoria.RequisitosDeCreditos(plan, alumno, 32));
            Assert.NotNull(aviso);
            Assert.True(aviso!.Informativa);                                   // no cambia el color del semáforo
            Assert.Equal("Ya cubre el 70 % de créditos: puede inscribir F", aviso.Texto);

            // Si ya la está cursando, no hay nada que avisar
            alumno.Materias.Add(Datos.Materia("F", "cursando", periodo: P3));
            Assert.Null(ReglasTrayectoria.SenalRequisitosCumplidos(ReglasTrayectoria.RequisitosDeCreditos(plan, alumno, 32)));
        }

        [Fact]
        public void LaSituacionDelAlumnoTraeLosRequisitos()
        {
            var s = ReglasTrayectoria.Calcular(Datos.Alumno("S25000001", Datos.Materia("A", "aprobada")), PlanConRequisito(), P3);
            var r = Assert.Single(s.RequisitosCreditos);
            Assert.Equal(26, r.Faltan);                                        // 32 - 6

            // Un plan sin la columna G no tiene requisitos
            Assert.Empty(ReglasTrayectoria.Calcular(Datos.Alumno("S25000001"), Datos.PlanCorto(), P3).RequisitosCreditos);
        }

        [Fact]
        public void ElReporteDelPeriodoLaPoneAlFinalConLoQueLeFalta()
        {
            var planes = new PlanesEnMemoria(new Dictionary<string, PlanInfo> { ["Plan_Corto"] = PlanConRequisito() });
            var servicio = new ServicioReportePeriodo(planes, new ServicioAlertas(planes));
            var alumno = Datos.Alumno("S25000001",
                Datos.Materia("A", "aprobada", periodo: P1), Datos.Materia("B", "aprobada", periodo: P1, creditos: 4),
                Datos.Materia("C", "aprobada", periodo: P2), Datos.Materia("Teoría de Juegos", "aprobada", periodo: P2, creditos: 5));

            var r = servicio.Resumir(alumno, P3, new DateTime(2026, 10, 26));   // 3er periodo en curso: lleva 21 créditos
            var f = r.SiguientePeriodo.Single(m => m.Nombre == "F");
            Assert.Equal("Cuando cubra el 70 % de créditos (faltan 11)", f.Motivo);
            Assert.Equal(f, r.SiguientePeriodo.Last());
        }

        [Fact]
        public void LaPlantillaConColumnaGSeLeeYUnPorcentajeInvalidoSeRechaza()
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "planea_req_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(carpeta);
            try
            {
                using (var libro = new XLWorkbook())
                {
                    var ws = libro.Worksheets.Add("Materias");
                    string[] enc = { "Semestre", "Materia", "Creditos", "Tipo", "Prerrequisitos", "Area", "Creditos minimos (%)" };
                    for (int c = 0; c < enc.Length; c++) ws.Cell(1, c + 1).Value = enc[c];
                    ws.Cell(2, 1).Value = 1; ws.Cell(2, 2).Value = "Cálculo"; ws.Cell(2, 3).Value = 9; ws.Cell(2, 4).Value = "Normal";
                    ws.Cell(3, 1).Value = 8; ws.Cell(3, 2).Value = "Servicio social"; ws.Cell(3, 3).Value = 12; ws.Cell(3, 4).Value = "Normal"; ws.Cell(3, 7).Value = 70;
                    ws.Cell(4, 1).Value = 9; ws.Cell(4, 2).Value = "Experiencia recepcional"; ws.Cell(4, 3).Value = 12; ws.Cell(4, 4).Value = "Normal";
                    ws.Cell(4, 7).Value = 0.7; ws.Cell(4, 7).Style.NumberFormat.Format = "0%";
                    libro.SaveAs(Path.Combine(carpeta, "Plan_G.xlsx"));
                }
                var plan = new RepositorioPlanesExcel(carpeta).Obtener("Plan_G")!;
                Assert.Equal(0, plan.Materias[0].PorcentajeCreditos);
                Assert.Equal(70, plan.Materias[1].PorcentajeCreditos);
                Assert.Equal(70, plan.Materias[2].PorcentajeCreditos);
            }
            finally { try { Directory.Delete(carpeta, true); } catch (IOException) { } }
        }
    }
}
