using ClosedXML.Excel;
using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Plan de estudios subido como mapa curricular dibujado (formato de la UV)
    public class MapaCurricularTests : IDisposable
    {
        private readonly CarpetaTemporal _carpeta = new();
        public void Dispose() => _carpeta.Dispose();

        private static readonly XLColor Verde = XLColor.FromHtml("#8EEA00"), Azul = XLColor.FromHtml("#8CB4DF"),
                                        Blanco = XLColor.FromHtml("#FFFFFF"), Gris = XLColor.FromHtml("#EEF3E1");

        // Dos periodos de 6 columnas + 1 de flecha (B-G y I-N), como el mapa de la UV
        private void CrearMapa(string nombre, bool conLeyenda = true)
        {
            using var libro = new XLWorkbook();
            var ws = libro.Worksheets.Add("Mapa curricular");
            ws.Range("B3:N4").Merge().Value = "MAPA CURRICULAR ESTÁNDAR PRUEBAS";
            ws.Range("B6:G8").Merge().Value = "1er Periodo\n19 Créditos\n20 horas";
            ws.Range("I6:N8").Merge().Value = "2do Periodo\n31 Créditos\n30 horas";
            ws.Cell("B9").Value = "HT"; ws.Cell("B10").Value = 6;

            void Recuadro(string rango, string texto, XLColor color)
            {
                var r = ws.Range(rango).Merge();
                r.Value = texto;
                r.Style.Fill.BackgroundColor = color;
            }
            Recuadro("B13:G15", "Lengua I\nT  0  0  6  4", Gris);
            Recuadro("I13:N15", "Lengua II\nT  0  0  6  4", Gris);
            ws.Range("H13:H15").Merge().Value = "→";
            Recuadro("B17:G19", "Cálculo I\nCT  3  3  0  9", Verde);
            Recuadro("I17:N19", "Microeconomía\nI\nCT  4  2  0  10\nReq: Cálculo I", Azul);
            Recuadro("B21:G23", "Optativa 1\nCT  1  3  0  6", XLColor.FromHtml("#F2D200"));
            Recuadro("I21:N23", "Servicio social\nVC  0  4  0  12\n70 % de créditos · dura 2 periodos", Blanco);
            Recuadro("I25:N27", "Seminario\n5 créditos", Azul);
            Recuadro("B30:N31", "AFEL 18 créditos\n3 × 6 créditos", XLColor.FromHtml("#FFA34D"));

            ws.Range("C34:G38").Merge().Value = "Total de créditos: 74\n18 Créditos Elección Libre\n6 Créditos Optativas";
            if (conLeyenda)
            {
                void Leyenda(int fila, int col, XLColor color, string texto)
                {
                    ws.Range(fila, col, fila + 1, col + 1).Merge().Style.Fill.BackgroundColor = color;
                    ws.Range(fila, col + 2, fila + 1, col + 8).Merge().Value = texto;
                }
                Leyenda(34, 9, Gris, "Área básica");
                Leyenda(37, 9, Verde, "Área de Iniciación a la\nDisciplina");
                Leyenda(34, 18, Azul, "Área Disciplinar");
                Leyenda(37, 18, Blanco, "Área Terminal");
            }

            var d = libro.Worksheets.Add("Datos");
            d.Cell(1, 1).Value = "Programa"; d.Cell(1, 2).Value = "Licenciatura en Pruebas";
            libro.SaveAs(Path.Combine(_carpeta.Ruta, nombre + ".xlsx"));
        }

        private PlanInfo Leer(string nombre) => new RepositorioPlanesExcel(_carpeta.Ruta).Obtener(nombre)!;

        [Fact]
        public void LeeMateriasPeriodosYCreditos()
        {
            CrearMapa("Mapa");
            var plan = Leer("Mapa");

            Assert.Equal("Licenciatura en Pruebas", plan.Programa);
            Assert.Equal(new[] { "Lengua I", "Cálculo I", "Optativa 1", "Lengua II", "Microeconomía I", "Servicio social", "Seminario", "AFEL 1", "AFEL 2", "AFEL 3" },
                         plan.Materias.Select(m => m.Nombre));
            var calculo = plan.Materias.Single(m => m.Nombre == "Cálculo I");
            Assert.Equal((1, 9, "Normal"), (calculo.Semestre, calculo.Creditos, calculo.Tipo));
            Assert.Equal(2, plan.Materias.Single(m => m.Nombre == "Microeconomía I").Semestre);
            Assert.Equal(5, plan.Materias.Single(m => m.Nombre == "Seminario").Creditos);
            Assert.Equal("Optativa", plan.Materias.Single(m => m.Nombre == "Optativa 1").Tipo);
            Assert.All(plan.Materias.Where(m => m.EsAfel), m => Assert.Equal((0, 6), (m.Semestre, m.Creditos)));
            Assert.Equal(68, plan.CreditosTotales);   // sin "Creditos totales" en Datos: la suma de los recuadros
        }

        [Fact]
        public void FlechasYRenglonReqSonPrerrequisitos()
        {
            CrearMapa("Mapa");
            var plan = Leer("Mapa");

            Assert.Equal("Lengua I", plan.Materias.Single(m => m.Nombre == "Lengua II").Prerrequisitos);
            Assert.Equal("Cálculo I", plan.Materias.Single(m => m.Nombre == "Microeconomía I").Prerrequisitos);
            Assert.Equal("", plan.Materias.Single(m => m.Nombre == "Servicio social").Prerrequisitos);
            Assert.Contains("Lengua I", plan.Seriadas);
            Assert.Contains("Cálculo I", plan.Seriadas);
        }

        [Fact]
        public void ColorDelRecuadroDaElAreaSegunLaLeyenda()
        {
            CrearMapa("Mapa");
            var area = Leer("Mapa").Materias.ToDictionary(m => m.Nombre, m => m.Area);

            Assert.Equal("AFBG", area["Lengua I"]);
            Assert.Equal("Iniciación a la disciplina", area["Cálculo I"]);
            Assert.Equal("Disciplinar", area["Microeconomía I"]);
            Assert.Equal("Terminal", area["Servicio social"]);
            Assert.Equal("", area["Optativa 1"]);
        }

        [Fact]
        public void SinLeyendaLasMateriasQuedanSinAreaYSeAvisa()
        {
            CrearMapa("Mapa", conLeyenda: false);
            var plan = Leer("Mapa");
            Assert.All(plan.Materias, m => Assert.Equal("", m.Area));
            Assert.Contains(plan.Avisos, a => a.Contains("leyenda"));
        }

        // Así cambia el color alguien en Excel: de la paleta del tema o con un tono casi igual al de la leyenda
        [Fact]
        public void ColorDelTemaOParecidoTambienCuentaYElQueNoCoincideSeAvisa()
        {
            CrearMapa("Mapa");
            string ruta = Path.Combine(_carpeta.Ruta, "Mapa.xlsx");
            using (var libro = new XLWorkbook(ruta))
            {
                var ws = libro.Worksheet(1);
                ws.Range("I25:N27").Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Background1);   // "Blanco, Fondo 1"
                ws.Range("B17:G19").Style.Fill.BackgroundColor = XLColor.FromHtml("#92E80A");                   // casi el verde de la leyenda
                ws.Range("I21:N23").Style.Fill.BackgroundColor = XLColor.FromHtml("#E00000");                   // rojo: no está en la leyenda
                libro.Save();
            }
            var plan = Leer("Mapa");
            var area = plan.Materias.ToDictionary(m => m.Nombre, m => m.Area);

            Assert.Equal("Terminal", area["Seminario"]);
            Assert.Equal("Iniciación a la disciplina", area["Cálculo I"]);
            Assert.Equal("", area["Servicio social"]);
            var aviso = Assert.Single(plan.Avisos);
            Assert.Contains("Servicio social", aviso);
        }

        // Excel deja el archivo abierto para escritura mientras el coordinador lo edita
        [Fact]
        public void SeLeeAunqueElPlanEsteAbiertoEnExcel()
        {
            CrearMapa("Mapa");
            using var abiertoEnExcel = new FileStream(Path.Combine(_carpeta.Ruta, "Mapa.xlsx"), FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            Assert.Equal(10, Leer("Mapa").Materias.Count);
        }

        [Fact]
        public void UnMapaConColoresDeLaLeyendaNoTieneAvisos()
        {
            CrearMapa("Mapa");
            Assert.Empty(Leer("Mapa").Avisos);
        }

        [Fact]
        public void PorcentajeYDuracionSalenDelRecuadro()
        {
            CrearMapa("Mapa");
            var servicio = Leer("Mapa").Materias.Single(m => m.Nombre == "Servicio social");
            Assert.Equal((70, 2), (servicio.PorcentajeCreditos, servicio.PeriodosDuracion));
        }

        [Fact]
        public void LaTablaSigueFuncionando()
        {
            using (var libro = new XLWorkbook())
            {
                var ws = libro.Worksheets.Add("Materias");
                ws.Cell(1, 1).Value = "Semestre"; ws.Cell(1, 2).Value = "Materia"; ws.Cell(1, 3).Value = "Creditos";
                ws.Cell(2, 1).Value = 1; ws.Cell(2, 2).Value = "Periodo de prueba"; ws.Cell(2, 3).Value = 8;
                libro.SaveAs(Path.Combine(_carpeta.Ruta, "Tabla.xlsx"));
            }
            var plan = Leer("Tabla");
            Assert.Equal("Periodo de prueba", Assert.Single(plan.Materias).Nombre);
        }
    }
}
