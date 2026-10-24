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
        public void UnAlumnoAntiguoConMatriculaNoValidaSePuedeSeguirGuardando()
        {
            // Registros de antes de la validación: no se deben bloquear
            var repo = Repo();
            var libro = new XLWorkbook();
            var ws = libro.Worksheets.Add("S24001");
            ws.Cell(1, 2).Value = "S24/001"; ws.Cell(2, 2).Value = "Antiguo"; ws.Cell(3, 2).Value = "Activo"; ws.Cell(4, 2).Value = "Plan_Corto";
            libro.SaveAs(Path.Combine(_carpeta.Ruta, "AvanceAlumnos.xlsx")); libro.Dispose();

            var antiguo = Datos.Alumno("S24/001"); antiguo.Nombre = "Antiguo";
            Assert.Null(ValidacionAlumno.Errores(antiguo));                   // el controlador no lo frena
            repo.Guardar(antiguo);                                             // y el repositorio lo deja guardar
            Assert.Equal("Antiguo", repo.Buscar("S24/001")!.Nombre);
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

        [Fact]
        public void ModificarLeeCambiaYGuardaSinTocarLoDemas()
        {
            var repo = Repo();
            var alumno = Datos.Alumno("S24000001", Datos.Materia("A", "aprobada"));
            alumno.Tutorias.Add(new TutoriaRecord { Periodo = "Febrero - Julio 2026", Sesion = 1, Asistencia = "asistio" });
            repo.Guardar(alumno);

            // Como hace la pantalla de tutoría: reemplaza solo las sesiones de un periodo
            repo.Modificar("S24000001", a =>
            {
                a!.Tutorias = a.Tutorias.Where(t => t.Periodo != "Agosto - Enero 2027")
                    .Append(new TutoriaRecord { Periodo = "Agosto - Enero 2027", Sesion = 1, Asistencia = "citado" }).ToList();
                return a;
            });

            var leido = repo.Buscar("S24000001")!;
            Assert.Equal(2, leido.Tutorias.Count);                       // la del otro periodo se conserva
            Assert.Single(leido.Materias);                                 // las materias no se tocan
            Assert.Equal("citado", leido.Tutorias.Single(t => t.Periodo == "Agosto - Enero 2027").Asistencia);
        }

        [Theory]
        [InlineData("Agosto - Enero 2027", true)]
        [InlineData("Febrero - Julio 2026", true)]
        [InlineData("Agosto 2027", false)]
        [InlineData("<b>Agosto - Enero 2027</b>", false)]
        public void PeriodoValido(string periodo, bool valido) => Assert.Equal(valido, ReglasTrayectoria.EsPeriodoValido(periodo));

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
