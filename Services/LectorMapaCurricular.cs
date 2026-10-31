using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using PlaneaUV_Economia.Models;

namespace PlaneaUV_Economia.Services
{
    // Lee un plan de estudios dibujado como mapa curricular de la UV (la otra forma es la tabla, ver RepositorioPlanesExcel):
    //   - Encabezados "1er Periodo", "2do Periodo"...: las materias debajo de cada uno son de ese periodo.
    //   - Cada materia es un recuadro: nombre y, en otro renglón, "CT  4  2  0  10" (acreditación, HT, HP, HO, créditos)
    //     o "10 créditos".
    //   - Una flecha "→" entre dos recuadros del mismo renglón: el de la izquierda es requisito del de la derecha.
    //   - Renglones opcionales del recuadro: "Req: A, B" (requisitos que no están juntos) y
    //     "70 % de créditos · dura 2 periodos".
    //   - El color del recuadro da el área según la leyenda del mapa ("Área Disciplinar" junto a un cuadro de color).
    //   - "Optativa ..." es optativa; un recuadro "AFEL 18 créditos" con "3 × 6 créditos" son 3 AFEL de 6.
    public static class LectorMapaCurricular
    {
        private static readonly Regex Encabezado = new(@"^\s*(\d{1,2})\s*(?:er|do|ro|to|vo|mo|no|°|º|o)?\.?\s*periodo", RegexOptions.IgnoreCase);
        private static readonly Regex LineaCodigo = new(@"^\s*(?:[A-ZÁÉÍÓÚÑ]{1,3}\s+)?\d+\s+\d+\s+\d+\s+(\d+)\s*$");
        private static readonly Regex LineaCreditos = new(@"^\s*(\d+)\s*cr[ée]ditos?\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex Porcentaje = new(@"(\d{1,3})\s*%");
        private static readonly Regex Duracion = new(@"(\d+)\s*periodos", RegexOptions.IgnoreCase);
        private static readonly Regex Requisito = new(@"^\s*req(?:uisitos?)?\s*[:.]\s*(.+)$", RegexOptions.IgnoreCase);
        private static readonly Regex AfelVarias = new(@"(\d+)\s*[×xX]\s*(\d+)");
        private static readonly string[] Flechas = { "→", "->", "⟶", "➜", "➝", "⇒" };

        private class Periodo { public int Numero, ColIni, ColFin; }
        private class Recuadro { public MateriaPlan Materia = new(); public int FilaIni, FilaFin, ColIni, ColFin; public string Color = ""; }

        // Es un mapa si la primera hoja tiene al menos dos encabezados de periodo
        public static bool EsMapa(IXLWorksheet ws) => BuscarPeriodos(ws).Count >= 2;

        public static List<MateriaPlan> Leer(IXLWorksheet ws)
        {
            var periodos = BuscarPeriodos(ws);
            int filaEncabezados = periodos.Count == 0 ? 0 : periodos.Max(p => p.filaFin);
            var bloques = periodos.Select(p => p.periodo).ToList();

            var recuadros = new List<Recuadro>();
            var flechas = new List<(int fila, int col)>();
            var afel = new List<MateriaPlan>();
            var leyenda = new Dictionary<string, string>();

            foreach (var celda in ws.CellsUsed(c => c.Address.RowNumber > filaEncabezados))
            {
                if (!EsInicioDeSuRango(celda)) continue;
                string texto = celda.GetString().Replace("\r", "").Trim();
                if (texto == "") continue;
                var rango = celda.MergedRange()?.RangeAddress;
                int filaIni = rango?.FirstAddress.RowNumber ?? celda.Address.RowNumber, filaFin = rango?.LastAddress.RowNumber ?? filaIni;
                int colIni = rango?.FirstAddress.ColumnNumber ?? celda.Address.ColumnNumber, colFin = rango?.LastAddress.ColumnNumber ?? colIni;

                if (Flechas.Contains(texto)) { flechas.Add((filaIni, colIni)); continue; }

                if (texto.StartsWith("AFEL", StringComparison.OrdinalIgnoreCase)) { afel.AddRange(LeerAfel(texto)); continue; }

                var materia = LeerRecuadro(texto);
                if (materia != null)
                {
                    var bloque = bloques.FirstOrDefault(b => colIni >= b.ColIni && colIni <= b.ColFin);
                    if (bloque == null) continue;   // fuera de los periodos (notas, totales)
                    materia.Semestre = bloque.Numero;
                    recuadros.Add(new Recuadro { Materia = materia, FilaIni = filaIni, FilaFin = filaFin, ColIni = colIni, ColFin = colFin, Color = Color(celda) });
                    continue;
                }

                // Leyenda: "Área Disciplinar" con su cuadro de color a la izquierda
                string? area = AreaDeLeyenda(texto);
                if (area != null)
                {
                    string color = ColorALaIzquierda(ws, filaIni, colIni);
                    if (color != "" && !leyenda.ContainsKey(color)) leyenda[color] = area;
                }
            }

            foreach (var r in recuadros)
            {
                if (r.Materia.EsOptativa) continue;
                if (leyenda.TryGetValue(r.Color, out var area) && area != "Electiva") r.Materia.Area = area;
            }

            // Flecha: requisito = recuadro a su izquierda en el mismo renglón; materia = el de su derecha
            foreach (var (fila, col) in flechas)
            {
                var enFila = recuadros.Where(r => fila >= r.FilaIni && fila <= r.FilaFin).ToList();
                var antes = enFila.Where(r => r.ColFin < col).OrderByDescending(r => r.ColFin).FirstOrDefault();
                var despues = enFila.Where(r => r.ColIni > col).OrderBy(r => r.ColIni).FirstOrDefault();
                if (antes != null && despues != null) AgregarRequisito(despues.Materia, antes.Materia.Nombre);
            }

            return recuadros.OrderBy(r => r.Materia.Semestre).ThenBy(r => r.FilaIni).Select(r => r.Materia).Concat(afel).ToList();
        }

        private static List<(Periodo periodo, int filaFin)> BuscarPeriodos(IXLWorksheet ws)
        {
            var lista = new List<(Periodo, int)>();
            foreach (var celda in ws.CellsUsed(c => c.Address.RowNumber <= 40))
            {
                if (!EsInicioDeSuRango(celda)) continue;
                var m = Encabezado.Match(celda.GetString());
                if (!m.Success) continue;
                var rango = celda.MergedRange()?.RangeAddress;
                int numero = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (lista.Any(x => x.Item1.Numero == numero)) continue;
                lista.Add((new Periodo
                {
                    Numero = numero,
                    ColIni = rango?.FirstAddress.ColumnNumber ?? celda.Address.ColumnNumber,
                    ColFin = rango?.LastAddress.ColumnNumber ?? celda.Address.ColumnNumber
                }, rango?.LastAddress.RowNumber ?? celda.Address.RowNumber));
            }
            // Sin celdas combinadas, cada periodo llega hasta antes del siguiente
            var orden = lista.OrderBy(x => x.Item1.ColIni).ToList();
            for (int i = 0; i < orden.Count; i++)
                if (orden[i].Item1.ColFin == orden[i].Item1.ColIni && i + 1 < orden.Count)
                    orden[i].Item1.ColFin = orden[i + 1].Item1.ColIni - 1;
            return orden;
        }

        // Nombre en los renglones antes del de créditos; los renglones después son requisitos, % y duración
        private static MateriaPlan? LeerRecuadro(string texto)
        {
            var lineas = texto.Split('\n').Select(l => l.Trim()).ToList();
            int k = lineas.FindIndex(l => LineaCodigo.IsMatch(l) || LineaCreditos.IsMatch(l));
            if (k < 1) return null;
            var mc = LineaCodigo.Match(lineas[k]);
            int creditos = int.Parse(mc.Success ? mc.Groups[1].Value : LineaCreditos.Match(lineas[k]).Groups[1].Value, CultureInfo.InvariantCulture);
            string nombre = Regex.Replace(string.Join(" ", lineas.Take(k)), @"\s+", " ").Trim();
            var materia = new MateriaPlan
            {
                Nombre = nombre,
                Creditos = creditos,
                Tipo = nombre.StartsWith("Optativa", StringComparison.OrdinalIgnoreCase) ? "Optativa" : "Normal"
            };
            foreach (var linea in lineas.Skip(k + 1))
            {
                var req = Requisito.Match(linea);
                if (req.Success)
                {
                    foreach (var r in req.Groups[1].Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        AgregarRequisito(materia, r);
                    continue;
                }
                var pct = Porcentaje.Match(linea);
                if (pct.Success) materia.PorcentajeCreditos = RepositorioPlanesExcel.LeerPorcentaje(pct.Groups[1].Value);
                var dura = Duracion.Match(linea);
                if (dura.Success) materia.PeriodosDuracion = RepositorioPlanesExcel.LeerDuracion(dura.Groups[1].Value);
            }
            return materia;
        }

        // "AFEL 18 créditos" + "3 × 6 créditos" -> AFEL 1, AFEL 2, AFEL 3 de 6 créditos. Sin el desglose, una sola AFEL.
        private static IEnumerable<MateriaPlan> LeerAfel(string texto)
        {
            var varias = AfelVarias.Match(texto);
            if (varias.Success)
            {
                int n = int.Parse(varias.Groups[1].Value, CultureInfo.InvariantCulture), cr = int.Parse(varias.Groups[2].Value, CultureInfo.InvariantCulture);
                return Enumerable.Range(1, Math.Min(n, 20)).Select(i => new MateriaPlan { Nombre = $"AFEL {i}", Creditos = cr, Tipo = "AFEL" });
            }
            var total = Regex.Match(texto, @"(\d+)\s*cr", RegexOptions.IgnoreCase);
            if (!total.Success) return Enumerable.Empty<MateriaPlan>();
            return new[] { new MateriaPlan { Nombre = "AFEL", Creditos = int.Parse(total.Groups[1].Value, CultureInfo.InvariantCulture), Tipo = "AFEL" } };
        }

        private static void AgregarRequisito(MateriaPlan materia, string requisito)
        {
            var actuales = materia.Prerrequisitos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (actuales.Contains(requisito, StringComparer.OrdinalIgnoreCase)) return;
            actuales.Add(requisito);
            materia.Prerrequisitos = string.Join(", ", actuales);
        }

        // Algunos Excel guardan el texto de un recuadro combinado en todas sus celdas: solo cuenta la primera
        private static bool EsInicioDeSuRango(IXLCell celda)
        {
            var rango = celda.MergedRange();
            return rango == null || rango.FirstCell().Address.Equals(celda.Address);
        }

        private static string? AreaDeLeyenda(string texto)
        {
            string t = SinAcentos(texto).ToLowerInvariant();
            if (!(t.StartsWith("area") || t.StartsWith("afbg"))) return null;
            if (t.Contains("iniciacion")) return "Iniciación a la disciplina";
            if (t.Contains("disciplinar")) return "Disciplinar";
            if (t.Contains("terminal")) return "Terminal";
            if (t.Contains("electiva") || t.Contains("optativ")) return "Electiva";
            if (t.Contains("afbg") || t.Contains("basica")) return "AFBG";
            return null;
        }

        // Primer cuadro con relleno a la izquierda de la etiqueta (hasta 4 columnas)
        private static string ColorALaIzquierda(IXLWorksheet ws, int fila, int col)
        {
            for (int c = col - 1; c >= Math.Max(1, col - 4); c--)
            {
                var celda = ws.Cell(fila, c);
                var inicio = celda.MergedRange()?.FirstCell() ?? celda;
                string color = Color(inicio);
                if (color != "") return color;
            }
            return "";
        }

        private static string Color(IXLCell celda)
        {
            var fill = celda.Style.Fill;
            if (fill.PatternType == XLFillPatternValues.None) return "";
            var c = fill.BackgroundColor;
            return c.ColorType switch
            {
                XLColorType.Color => c.Color.ToArgb().ToString("X8"),
                XLColorType.Theme => $"tema {c.ThemeColor} {c.ThemeTint.ToString("0.00", CultureInfo.InvariantCulture)}",
                XLColorType.Indexed => $"indice {c.Indexed}",
                _ => ""
            };
        }

        private static string SinAcentos(string t) =>
            new string(t.Normalize(NormalizationForm.FormD).Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
