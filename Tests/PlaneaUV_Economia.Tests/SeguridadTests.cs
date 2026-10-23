using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using PlaneaUV_Economia.Models;
using PlaneaUV_Economia.Services;

namespace PlaneaUV_Economia.Tests
{
    // Revisión de seguridad: datos no válidos, alumnos que se sobrescriben y planes dañados
    public class SeguridadTests : IDisposable
    {
        private readonly CarpetaTemporal _carpeta = new();
        public void Dispose() => _carpeta.Dispose();

        private RepositorioAlumnosExcel Repo() => new(Path.Combine(_carpeta.Ruta, "AvanceAlumnos.xlsx"));

        [Theory]
        [InlineData("S21012345", true)]
        [InlineData("zS21012345", true)]
        [InlineData("S24/001", false)]                      // "/" se quitaba del nombre de la hoja y chocaba con otro alumno
        [InlineData("S21 012345", false)]
        [InlineData("<script>", false)]
        [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", false)]   // más de 31 caracteres se recortaba
        [InlineData("", false)]
        public void FormatoDeMatricula(string matricula, bool valida)
        {
            Assert.Equal(valida, ValidacionAlumno.ErrorEnMatricula(matricula) == null);
        }

        [Fact]
        public void UnaMatriculaQueSoloCambiaEnMayusculasNoSobrescribeAlAlumno()
        {
            var repo = Repo();
            var original = Datos.Alumno("S24000001"); original.Nombre = "Original";
            repo.Guardar(original);

            var intruso = Datos.Alumno("s24000001"); intruso.Nombre = "Intruso";
            Assert.Throws<InvalidOperationException>(() => repo.Guardar(intruso));
            Assert.Equal("Original", repo.Buscar("S24000001")!.Nombre);
        }

        [Fact]
        public void ElRepositorioRechazaMatriculasNuevasConCaracteresNoValidos()
        {
            var repo = Repo();
            repo.Guardar(Datos.Alumno("S24000001"));
            Assert.Throws<ArgumentException>(() => repo.Guardar(Datos.Alumno("S24000001/")));
            Assert.Single(repo.Listar());
        }

        [Fact]
        public void TextosDemasiadoLargosSeRechazanConUnMensajeClaro()
        {
            var alumno = Datos.Alumno("S24000001");
            Assert.Null(ValidacionAlumno.Errores(alumno));

            alumno.Nombre = new string('x', 40000);                // una celda de Excel admite 32,767
            Assert.NotNull(ValidacionAlumno.Errores(alumno));

            alumno.Nombre = "Ana";
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = "Agosto - Enero 2027", Sesion = 1, Comentarios = new string('x', 5000) });
            Assert.Contains("acórtalo", ValidacionAlumno.Errores(alumno));

            alumno.Tutorias.Clear();
            alumno.Materias.Add(Datos.Materia("A", "aprobada", creditos: 999));
            Assert.NotNull(ValidacionAlumno.Errores(alumno));
        }

        private static IFormFile Archivo(string nombre, byte[] contenido) =>
            new FormFile(new MemoryStream(contenido), 0, contenido.Length, "archivoPlan", nombre);

        private static byte[] PlanValido()
        {
            using var libro = new XLWorkbook();
            var ws = libro.Worksheets.Add("Materias");
            ws.Cell(1, 1).Value = "Semestre"; ws.Cell(1, 2).Value = "Materia"; ws.Cell(1, 3).Value = "Creditos"; ws.Cell(1, 4).Value = "Tipo";
            ws.Cell(2, 1).Value = 1; ws.Cell(2, 2).Value = "Cálculo"; ws.Cell(2, 3).Value = 8; ws.Cell(2, 4).Value = "Normal";
            using var ms = new MemoryStream(); libro.SaveAs(ms); return ms.ToArray();
        }

        [Fact]
        public async Task UnPlanDanadoNoReemplazaAlQueFunciona()
        {
            var repo = new RepositorioPlanesExcel(_carpeta.Ruta);
            await repo.GuardarAsync(Archivo("Plan_X.xlsx", PlanValido()));
            Assert.Single(repo.Obtener("Plan_X")!.Materias);

            var danado = Archivo("Plan_X.xlsx", System.Text.Encoding.UTF8.GetBytes("esto no es un Excel"));
            await Assert.ThrowsAsync<ArgumentException>(() => repo.GuardarAsync(danado));
            Assert.Single(repo.Obtener("Plan_X")!.Materias);                     // el plan bueno sigue ahí
            Assert.Equal(new[] { "Plan_X" }, repo.Listar());                      // sin archivos temporales
        }

        [Theory]
        [InlineData("..\\..\\Windows\\plan.xlsx", true)]     // la ruta se ignora: queda en la carpeta de planes
        [InlineData("plan<script>.xlsx", false)]
        [InlineData("plan.xlsx.exe", false)]
        public async Task NombreDelArchivoDePlan(string nombre, bool aceptado)
        {
            var repo = new RepositorioPlanesExcel(_carpeta.Ruta);
            if (aceptado)
            {
                await repo.GuardarAsync(Archivo(nombre, PlanValido()));
                Assert.True(File.Exists(Path.Combine(_carpeta.Ruta, "plan.xlsx")));
            }
            else await Assert.ThrowsAsync<ArgumentException>(() => repo.GuardarAsync(Archivo(nombre, PlanValido())));
        }
    }
}
