using ClosedXML.Excel;
using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Prueban los repositorios contra archivos reales en una carpeta temporal que se borra al terminar
    public sealed class CarpetaTemporal : IDisposable
    {
        public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "planea_pruebas_" + Guid.NewGuid().ToString("N"));
        public CarpetaTemporal() { Directory.CreateDirectory(Ruta); }
        public void Dispose() { try { Directory.Delete(Ruta, true); } catch { } }
    }

    public class RepositorioAlumnosExcelTests : IDisposable
    {
        private readonly CarpetaTemporal _carpeta = new();
        private readonly RepositorioAlumnosExcel _repo;
        private string Archivo => Path.Combine(_carpeta.Ruta, "AvanceAlumnos.xlsx");

        public RepositorioAlumnosExcelTests() { _repo = new RepositorioAlumnosExcel(Archivo); }
        public void Dispose() => _carpeta.Dispose();

        [Fact]
        public void GuardaYLeeAlAlumnoCompleto()
        {
            var alumno = Datos.Alumno("S24000001", Datos.Materia("A", "aprobada", riesgo: true), Datos.Materia("B", "reprobada", rezago: true));
            alumno.Nombre = "Martínez Cruz Fernanda";
            alumno.PeriodosSinInscripcion.AddRange(new[] { "Agosto - Enero 2025", "Febrero - Julio 2025" });
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = "Agosto - Enero 2027", Sesion = 1, Fecha = "2026-09-10", Asistencia = "asistio", Comentarios = "Todo bien" });

            Assert.False(_repo.HayRegistros);
            _repo.Guardar(alumno);
            Assert.True(_repo.HayRegistros);

            var leido = _repo.Buscar("S24000001")!;
            Assert.Equal("Martínez Cruz Fernanda", leido.Nombre);
            Assert.Equal("Plan_Corto", leido.PlanEstudio);
            Assert.Equal(alumno.PeriodosSinInscripcion, leido.PeriodosSinInscripcion);
            Assert.Equal(2, leido.Materias.Count);
            Assert.True(leido.Materias[0].EnRiesgo);
            Assert.True(leido.Materias[1].EnRezago);
            Assert.Equal(6, leido.Materias[0].Creditos);
            Assert.Equal("Todo bien", Assert.Single(leido.Tutorias).Comentarios);
        }

        [Fact]
        public void GuardarDeNuevoReemplazaAlAlumnoYDejaRespaldo()
        {
            _repo.Guardar(Datos.Alumno("S24000001", Datos.Materia("A", "cursando")));
            _repo.Guardar(Datos.Alumno("S24000001", Datos.Materia("A", "aprobada")));

            var leido = _repo.Buscar("S24000001")!;
            Assert.Equal("aprobada", Assert.Single(leido.Materias).Estado);
            Assert.True(File.Exists(Path.Combine(_carpeta.Ruta, "AvanceAlumnos.respaldo.xlsx")));
            Assert.False(File.Exists(Path.Combine(_carpeta.Ruta, "AvanceAlumnos.guardando.xlsx")));
        }

        [Fact]
        public void ListarYEliminar()
        {
            _repo.Guardar(Datos.Alumno("S24000001"));
            _repo.Guardar(Datos.Alumno("S25000002"));
            Assert.Equal(new[] { "S24000001", "S25000002" }, _repo.Listar().Select(a => a.Matricula));

            Assert.True(_repo.Eliminar("S24000001"));
            Assert.False(_repo.Eliminar("NOEXISTE"));
            Assert.Null(_repo.Buscar("S24000001"));
            Assert.Single(_repo.ObtenerTodos());
        }

        [Fact]
        public void EliminarAlUltimoBorraElArchivoPeroConservaRespaldo()
        {
            _repo.Guardar(Datos.Alumno("S24000001"));
            Assert.True(_repo.Eliminar("S24000001"));
            Assert.False(_repo.HayRegistros);
            Assert.Empty(_repo.Listar());
            Assert.True(File.Exists(Path.Combine(_carpeta.Ruta, "AvanceAlumnos.respaldo.xlsx")));
        }

        [Fact]
        public void ConElArchivoAbiertoEnExcelLanzaIOException()
        {
            _repo.Guardar(Datos.Alumno("S24000001"));
            // Así bloquea Excel el archivo: permite leer a otros pero no escribir
            using var bloqueo = new FileStream(Archivo, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            Assert.ThrowsAny<IOException>(() => _repo.Buscar("S24000001"));
        }
    }

    public class RepositorioPlanesExcelTests : IDisposable
    {
        private readonly CarpetaTemporal _carpeta = new();
        public void Dispose() => _carpeta.Dispose();

        private void CrearPlan(string nombre, bool conHojasOpcionales)
        {
            using var libro = new XLWorkbook();
            var ws = libro.Worksheets.Add("Materias");
            ws.Cell(1, 1).Value = "Semestre"; ws.Cell(1, 2).Value = "Materia"; ws.Cell(1, 3).Value = "Creditos"; ws.Cell(1, 4).Value = "Tipo"; ws.Cell(1, 5).Value = "Prerrequisitos"; ws.Cell(1, 6).Value = "Area";
            object[][] filas =
            {
                new object[] { 1, "Cálculo I", 9, "Normal", "", "Básica" },
                new object[] { 2, "Cálculo II", 9, "Normal", "Cálculo I", "Disciplinar" },
                new object[] { 2, "Optativa 1", 6, "Optativa", "", "" },
                new object[] { 3, "AFEL 1", 6, "AFEL", "", "" },
            };
            for (int f = 0; f < filas.Length; f++)
                for (int c = 0; c < filas[f].Length; c++)
                    ws.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);

            if (conHojasOpcionales)
            {
                var d = libro.Worksheets.Add("Datos");
                d.Cell(1, 1).Value = "Programa"; d.Cell(1, 2).Value = "Licenciatura en Pruebas";
                d.Cell(2, 1).Value = "Creditos totales"; d.Cell(2, 2).Value = 100;
                var o = libro.Worksheets.Add("Optativas");
                o.Cell(1, 1).Value = "Optativa"; o.Cell(1, 2).Value = "Grupo";
                o.Cell(2, 1).Value = "Teoría de Juegos"; o.Cell(2, 2).Value = "Área 1";
            }
            libro.SaveAs(Path.Combine(_carpeta.Ruta, nombre + ".xlsx"));
        }

        [Fact]
        public void LeeMateriasDatosYOptativas()
        {
            CrearPlan("Plan_Completo", conHojasOpcionales: true);
            var plan = new RepositorioPlanesExcel(_carpeta.Ruta).Obtener("Plan_Completo")!;

            Assert.Equal("Licenciatura en Pruebas", plan.Programa);
            Assert.Equal(100, plan.CreditosTotales);
            Assert.Equal(4, plan.Materias.Count);
            Assert.Equal("Cálculo I", plan.Materias[1].Prerrequisitos);
            Assert.Equal("Teoría de Juegos", Assert.Single(plan.Optativas).Nombre);
            Assert.Equal(new[] { "Cálculo I" }, plan.Seriadas);
        }

        [Fact]
        public void SinHojasOpcionalesUsaElNombreDelArchivoYLaSumaDeCreditos()
        {
            CrearPlan("Plan_Minimo", conHojasOpcionales: false);
            var plan = new RepositorioPlanesExcel(_carpeta.Ruta).Obtener("Plan_Minimo")!;

            Assert.Equal("Plan Minimo", plan.Programa);
            Assert.Equal(30, plan.CreditosTotales);   // 9 + 9 + 6 + 6
            Assert.Empty(plan.Optativas);
        }

        [Fact]
        public void PlanInexistenteYNombresConRutaNoSalenDeLaCarpeta()
        {
            var repo = new RepositorioPlanesExcel(_carpeta.Ruta);
            Assert.Null(repo.Obtener("NoExiste"));
            Assert.False(repo.Existe(@"..\..\Windows\win"));
            Assert.Empty(repo.Listar());
        }
    }
}
