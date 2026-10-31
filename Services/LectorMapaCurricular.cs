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
        private class Recuadro { public MateriaPlan Materia = new(); public int FilaIni, FilaFin, ColIni, ColFin, Color; public bool AreaDeLista; }

        // La plantilla de captura de SITRA-UV lleva esta marca en A1 (texto blanco): sus recuadros tienen casillas
        public const string MarcaPlantilla = "SITRA-UV · plantilla de captura de mapa curricular";
        private static bool EsPlantillaDeCaptura(IXLWorksheet ws) => Texto(ws.Cell(1, 1)).StartsWith("SITRA-UV · plantilla", StringComparison.OrdinalIgnoreCase);

        // Recuadro de la plantilla (6 columnas):
        //   renglones 1-2: nombre
        //   renglón 3: Acred. (2 col.) | HT | HP | HO | Cr.
        //   renglón 4: Área (4 col., lista) | % de créditos | Periodos que dura
        //   renglón 5: Requisitos (6 col.)
        private static MateriaPlan LeerRecuadroDeCasillas(IXLWorksheet ws, string texto, int filaNombreFin, int col, HashSet<(int, int)> usadas, out bool conCreditos)
        {
            int fCodigos = filaNombreFin + 1, fArea = filaNombreFin + 2, fReq = filaNombreFin + 3;
            for (int f = filaNombreFin - 1; f <= fReq; f++)
                for (int c = col; c <= col + 5; c++) usadas.Add((f, c));

            // Si escribieron todo en el nombre ("Cálculo I" + "CT 3 3 0 9"), también se acepta
            var materia = LeerRecuadro(texto) ?? new MateriaPlan { Nombre = Regex.Replace(texto, @"\s+", " ").Trim(), Tipo = "Normal" };
            int? creditos = Numero(ws.Cell(fCodigos, col + 5));
            if (creditos != null) materia.Creditos = creditos.Value;
            conCreditos = creditos != null || LeerRecuadro(texto) != null;

            string area = SinAcentos(Texto(ws.Cell(fArea, col))).ToLowerInvariant();
            if (area.StartsWith("optativ") || area.StartsWith("electiv")) materia.Tipo = "Optativa";
            else if (area.Contains("iniciacion")) materia.Area = "Iniciación a la disciplina";
            else if (area.Contains("disciplinar")) materia.Area = "Disciplinar";
            else if (area.Contains("terminal")) materia.Area = "Terminal";
            else if (area.Contains("afbg") || area.Contains("basica")) materia.Area = "AFBG";
            if (materia.Nombre.StartsWith("Optativa", StringComparison.OrdinalIgnoreCase)) materia.Tipo = "Optativa";

            var pct = Numero(ws.Cell(fArea, col + 4));
            if (pct != null) materia.PorcentajeCreditos = RepositorioPlanesExcel.LeerPorcentaje(pct.Value.ToString(CultureInfo.InvariantCulture));
            var dura = Numero(ws.Cell(fArea, col + 5));
            if (dura != null) materia.PeriodosDuracion = RepositorioPlanesExcel.LeerDuracion(dura.Value.ToString(CultureInfo.InvariantCulture));

            string req = Regex.Replace(Texto(ws.Cell(fReq, col)), @"^\s*req(?:uisitos?)?\s*[:.]\s*", "", RegexOptions.IgnoreCase);
            foreach (var r in req.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AgregarRequisito(materia, r);
            return materia;
        }

        // Franja AFEL de la plantilla: "Número de experiencias" [n] y "Créditos de cada una" [c] en el mismo renglón
        private static IEnumerable<MateriaPlan> LeerAfelDeCasillas(IXLWorksheet ws, int fila)
        {
            int? n = null, cr = null;
            string etiqueta = "";
            foreach (var celda in ws.Row(fila).CellsUsed())
            {
                if (!EsInicioDeSuRango(celda)) continue;
                var numero = Numero(celda);
                if (numero == null) { etiqueta = SinAcentos(Texto(celda)).ToLowerInvariant(); continue; }
                if (etiqueta.Contains("experiencias")) n = numero;
                else if (etiqueta.Contains("cada una")) cr = numero;
            }
            if (n == null || cr == null || n <= 0) return Enumerable.Empty<MateriaPlan>();
            return Enumerable.Range(1, Math.Min(n.Value, 20)).Select(i => new MateriaPlan { Nombre = $"AFEL {i}", Creditos = cr.Value, Tipo = "AFEL" });
        }

        private static int AnchoCombinado(IXLCell celda)
        {
            var r = celda.MergedRange()?.RangeAddress;
            return r == null ? 1 : r.LastAddress.ColumnNumber - r.FirstAddress.ColumnNumber + 1;
        }

        // Texto de la celda; si es una fórmula que la librería no puede calcular, se toma como vacía
        private static string Texto(IXLCell celda)
        {
            try { return celda.GetString().Replace("\r", "").Trim(); }
            catch (Exception) { return ""; }
        }

        private static int? Numero(IXLCell celda)
        {
            string t = Texto(celda).TrimEnd('%').Trim().Replace(',', '.');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v == Math.Floor(v) ? (int)v : null;
        }

        private const int Blanco = 0xFFFFFF;
        // Qué tan distinto puede ser el color de un recuadro del de la leyenda (distancia RGB) para tomarlo como el mismo
        private const double DistanciaMaxima = 60;

        // Es un mapa si la primera hoja tiene al menos dos encabezados de periodo
        public static bool EsMapa(IXLWorksheet ws) => BuscarPeriodos(ws).Count >= 2;

        // 'avisos' (opcional) recibe lo que conviene revisar sin impedir la carga, p. ej. un recuadro cuyo color no está en la leyenda
        public static List<MateriaPlan> Leer(IXLWorksheet ws, List<string>? avisos = null)
        {
            var periodos = BuscarPeriodos(ws);
            int filaEncabezados = periodos.Count == 0 ? 0 : periodos.Max(p => p.filaFin);
            var bloques = periodos.Select(p => p.periodo).ToList();

            var recuadros = new List<Recuadro>();
            var flechas = new List<(int fila, int col)>();
            var afel = new List<MateriaPlan>();
            var leyenda = new List<(int color, string area)>();
            bool captura = EsPlantillaDeCaptura(ws);
            var usadas = new HashSet<(int fila, int col)>();   // casillas ya leídas como parte de un recuadro de la plantilla
            var sinCreditos = new List<string>();

            foreach (var celda in ws.CellsUsed(c => c.Address.RowNumber > filaEncabezados))
            {
                if (!EsInicioDeSuRango(celda) || usadas.Contains((celda.Address.RowNumber, celda.Address.ColumnNumber))) continue;
                string texto = Texto(celda);
                if (texto == "") continue;
                var rango = celda.MergedRange()?.RangeAddress;
                int filaIni = rango?.FirstAddress.RowNumber ?? celda.Address.RowNumber, filaFin = rango?.LastAddress.RowNumber ?? filaIni;
                int colIni = rango?.FirstAddress.ColumnNumber ?? celda.Address.ColumnNumber, colFin = rango?.LastAddress.ColumnNumber ?? colIni;

                if (Flechas.Contains(texto)) { flechas.Add((filaIni, colIni)); continue; }

                if (texto.StartsWith("AFEL", StringComparison.OrdinalIgnoreCase))
                {
                    var leidas = LeerAfel(texto).ToList();
                    afel.AddRange(leidas.Count > 0 || !captura ? leidas : LeerAfelDeCasillas(ws, filaIni));
                    continue;
                }

                // Plantilla de captura: el nombre ocupa 2 renglones al inicio del periodo y debajo van sus casillas
                // (la casilla de Área, 4 columnas combinadas dos renglones abajo, distingue un recuadro de una etiqueta de la leyenda)
                var bloqueCaptura = captura && filaFin - filaIni == 1 && AnchoCombinado(ws.Cell(filaFin + 2, colIni)) == 4
                    ? bloques.FirstOrDefault(b => b.ColIni == colIni && b.ColFin == colFin) : null;
                if (bloqueCaptura != null)
                {
                    var leida = LeerRecuadroDeCasillas(ws, texto, filaFin, colIni, usadas, out bool conCreditos);
                    if (!conCreditos) sinCreditos.Add(leida.Nombre);
                    leida.Semestre = bloqueCaptura.Numero;
                    recuadros.Add(new Recuadro { Materia = leida, FilaIni = filaIni, FilaFin = filaFin + 3, ColIni = colIni, ColFin = colFin, AreaDeLista = true });
                    continue;
                }

                var materia = LeerRecuadro(texto);
                if (materia != null)
                {
                    var bloque = bloques.FirstOrDefault(b => colIni >= b.ColIni && colIni <= b.ColFin);
                    if (bloque == null) continue;   // fuera de los periodos (notas, totales)
                    materia.Semestre = bloque.Numero;
                    // Un recuadro sin relleno se ve blanco: cuenta como blanco
                    recuadros.Add(new Recuadro { Materia = materia, FilaIni = filaIni, FilaFin = filaFin, ColIni = colIni, ColFin = colFin, Color = Color(celda) ?? Blanco });
                    continue;
                }

                // Leyenda: "Área Disciplinar" con su cuadro de color a la izquierda
                string? area = AreaDeLeyenda(texto);
                if (area != null)
                {
                    int? color = ColorALaIzquierda(ws, filaIni, colIni);
                    if (color != null && !leyenda.Any(l => l.color == color)) leyenda.Add((color.Value, area));
                }
            }

            var sinArea = new List<string>();
            foreach (var r in recuadros)
            {
                if (r.Materia.EsOptativa) continue;
                if (r.AreaDeLista)   // el área se eligió de la lista del recuadro, no se lee del color
                {
                    if (r.Materia.Area == "") sinArea.Add(r.Materia.Nombre);
                    continue;
                }
                // El color más parecido de la leyenda (así un tono casi igual, o el mismo color elegido de la paleta del tema, también cuenta)
                var cercano = leyenda.OrderBy(l => Distancia(l.color, r.Color)).FirstOrDefault();
                if (leyenda.Count > 0 && Distancia(cercano.color, r.Color) <= DistanciaMaxima)
                {
                    if (cercano.area != "Electiva") r.Materia.Area = cercano.area;
                }
                else sinArea.Add(r.Materia.Nombre);
            }
            if (avisos != null && recuadros.Count > 0 && captura)
            {
                if (sinArea.Count > 0)
                    avisos.Add($"Sin área (elígela en la lista del recuadro): {string.Join(", ", sinArea)}.");
                if (sinCreditos.Count > 0)
                    avisos.Add($"Sin créditos (casilla \"Cr.\" del recuadro): {string.Join(", ", sinCreditos)}.");
            }
            else if (avisos != null && recuadros.Count > 0)
            {
                if (leyenda.Count == 0)
                    avisos.Add("No se encontró la leyenda de áreas (\"Área Disciplinar\", \"Área Terminal\"... junto a un cuadro de color): las materias quedaron sin área.");
                else if (sinArea.Count > 0)
                    avisos.Add((sinArea.Count == 1
                        ? $"El color de \"{sinArea[0]}\" no se parece a ninguno de la leyenda y quedó sin área."
                        : $"El color de estas materias no se parece a ninguno de la leyenda y quedaron sin área: {string.Join(", ", sinArea)}.")
                        + " Usa el mismo color del cuadro de la leyenda (puedes copiarlo con Copiar formato, la brocha).");
            }

            // Flecha: requisito = recuadro a su izquierda en el mismo renglón; materia = el de su derecha
            foreach (var (fila, col) in flechas)
            {
                var enFila = recuadros.Where(r => fila >= r.FilaIni && fila <= r.FilaFin).ToList();
                var antes = enFila.Where(r => r.ColFin < col).OrderByDescending(r => r.ColFin).FirstOrDefault();
                var despues = enFila.Where(r => r.ColIni > col).OrderBy(r => r.ColIni).FirstOrDefault();
                if (antes != null && despues != null) AgregarRequisito(despues.Materia, antes.Materia.Nombre);
            }

            // Un requisito escrito a mano que no coincide con ninguna materia del mapa (p. ej. con otra ortografía) no se podría revisar
            if (avisos != null)
            {
                var nombres = new HashSet<string>(recuadros.Select(r => r.Materia.Nombre), StringComparer.OrdinalIgnoreCase);
                var malos = recuadros.SelectMany(r => r.Materia.Prerrequisitos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(q => !nombres.Contains(q)).Select(q => $"\"{q}\" (requisito de {r.Materia.Nombre})")).ToList();
                if (malos.Count > 0)
                    avisos.Add($"Requisitos que no coinciden con ninguna materia del mapa: {string.Join(", ", malos)}. Escríbelos igual que el nombre de la materia.");
            }

            return recuadros.OrderBy(r => r.Materia.Semestre).ThenBy(r => r.FilaIni).Select(r => r.Materia).Concat(afel).ToList();
        }

        private static List<(Periodo periodo, int filaFin)> BuscarPeriodos(IXLWorksheet ws)
        {
            var lista = new List<(Periodo, int)>();
            foreach (var celda in ws.CellsUsed(c => c.Address.RowNumber <= 40))
            {
                if (!EsInicioDeSuRango(celda)) continue;
                var m = Encabezado.Match(Texto(celda));
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
        private static int? ColorALaIzquierda(IXLWorksheet ws, int fila, int col)
        {
            for (int c = col - 1; c >= Math.Max(1, col - 4); c--)
            {
                var celda = ws.Cell(fila, c);
                var color = Color(celda.MergedRange()?.FirstCell() ?? celda);
                if (color != null) return color;
            }
            return null;
        }

        // Relleno de la celda como RGB (sin transparencia), o null si no tiene. Los colores del tema de Excel
        // ("Blanco, Fondo 1", "Azul, Énfasis 1" con su tono más claro u oscuro) se convierten a su RGB.
        private static int? Color(IXLCell celda)
        {
            var fill = celda.Style.Fill;
            if (fill.PatternType == XLFillPatternValues.None) return null;
            var c = fill.BackgroundColor;
            try
            {
                switch (c.ColorType)
                {
                    case XLColorType.Color:
                    case XLColorType.Indexed:
                        return c.Color.ToArgb() & 0xFFFFFF;
                    case XLColorType.Theme:
                        var baseColor = ColorDelTema(celda.Worksheet.Workbook.Theme, c.ThemeColor);
                        return baseColor == null ? null : AplicarTono(baseColor.Color.ToArgb() & 0xFFFFFF, c.ThemeTint);
                }
            }
            catch (Exception) { }   // un color que la librería no sabe resolver: se toma como sin relleno
            return null;
        }

        private static XLColor? ColorDelTema(IXLTheme tema, XLThemeColor cual) => cual switch
        {
            XLThemeColor.Background1 => tema.Background1,
            XLThemeColor.Text1 => tema.Text1,
            XLThemeColor.Background2 => tema.Background2,
            XLThemeColor.Text2 => tema.Text2,
            XLThemeColor.Accent1 => tema.Accent1,
            XLThemeColor.Accent2 => tema.Accent2,
            XLThemeColor.Accent3 => tema.Accent3,
            XLThemeColor.Accent4 => tema.Accent4,
            XLThemeColor.Accent5 => tema.Accent5,
            XLThemeColor.Accent6 => tema.Accent6,
            XLThemeColor.Hyperlink => tema.Hyperlink,
            XLThemeColor.FollowedHyperlink => tema.FollowedHyperlink,
            _ => null
        };

        // Tono de Excel: positivo aclara hacia blanco, negativo oscurece hacia negro
        private static int AplicarTono(int rgb, double tono)
        {
            int Canal(int v) => (int)Math.Round(tono >= 0 ? v + (255 - v) * tono : v * (1 + tono));
            return (Canal((rgb >> 16) & 0xFF) << 16) | (Canal((rgb >> 8) & 0xFF) << 8) | Canal(rgb & 0xFF);
        }

        private static double Distancia(int a, int b)
        {
            int dr = ((a >> 16) & 0xFF) - ((b >> 16) & 0xFF), dg = ((a >> 8) & 0xFF) - ((b >> 8) & 0xFF), db = (a & 0xFF) - (b & 0xFF);
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static string SinAcentos(string t) =>
            new string(t.Normalize(NormalizationForm.FormD).Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
