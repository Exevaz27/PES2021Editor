using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EditorLigasPes
{
    /// <summary>
    /// Motor forense de procesamiento y formateo automático de logos y escudos para PES 2021 / Football Life.
    /// Desarrollado por EXEVAZ27.
    /// </summary>
    public static class ImageProcessor
    {
        // Umbral de visibilidad alfa (M113): antes era el literal 12 sin nombre.
        private const int AlphaVisibilityThreshold = 12;
        public static Bitmap LoadSafe(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            // Límite de tamaño (M114): un archivo gigante no debe agotar la
            // memoria; 256 MB está muy por encima de cualquier logo.
            try
            {
                if (new FileInfo(filePath).Length > 256L * 1024 * 1024)
                    return null;
            }
            catch { return null; }

            try
            {
                // Sin doble carga (M114): FileStream directo en vez de
                // ReadAllBytes + MemoryStream; el Bitmap copiado ya no depende
                // del stream al retornar.
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var temp = Image.FromStream(fs))
                    {
                        return new Bitmap(temp);
                    }
                }
            }
            catch
            {
                // Archivo corrupto o ilegible: null, como promete el nombre (M114).
                return null;
            }
        }

        public static Bitmap TrimTransparency(Bitmap src)
        {
            if (src == null) return null;

            int w = src.Width;
            int h = src.Height;

            BitmapData data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int minX = w, maxX = -1, minY = h, maxY = -1;

            // try/finally (M113): antes el UnlockBits quedaba fuera y una
            // excepción dejaba el Bitmap bloqueado.
            try
            {
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int stride = data.Stride;

                for (int y = 0; y < h; y++)
                {
                    byte* row = ptr + (y * stride);
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = row[x * 4 + 3];
                        if (alpha > AlphaVisibilityThreshold)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }
            }
            finally
            {
                src.UnlockBits(data);
            }

            if (maxX < minX || maxY < minY)
            {
                // Imagen completamente transparente o vacía
                return new Bitmap(src);
            }

            // Si ya ocupa todo el espacio, no recortar
            if (minX == 0 && minY == 0 && maxX == w - 1 && maxY == h - 1)
            {
                return new Bitmap(src);
            }

            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;

            Bitmap cropped = new Bitmap(cropW, cropH, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(src, new Rectangle(0, 0, cropW, cropH), minX, minY, cropW, cropH, GraphicsUnit.Pixel);
            }
            return cropped;
        }

        public static Bitmap FitToSquare(Image src, int targetSize, int padding = 4)
        {
            if (src == null) return null;
            if (targetSize <= 0)
                throw new ArgumentOutOfRangeException("targetSize", "El tamaño destino debe ser mayor que cero.");
            if (padding < 0)
                throw new ArgumentOutOfRangeException("padding", "El padding no puede ser negativo.");

            int maxDim = targetSize - (padding * 2);
            if (maxDim <= 0) maxDim = targetSize;

            float scale = Math.Min((float)maxDim / src.Width, (float)maxDim / src.Height);
            int dw = Math.Max(1, (int)Math.Round(src.Width * scale));
            int dh = Math.Max(1, (int)Math.Round(src.Height * scale));

            int dx = (targetSize - dw) / 2;
            int dy = (targetSize - dh) / 2;

            Bitmap result = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.Clamp);
                    g.DrawImage(src, new Rectangle(dx, dy, dw, dh), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return result;
        }

        public static Bitmap GenerateSilhouette(Bitmap src, Color tintColor)
        {
            if (src == null) return null;

            int w = src.Width;
            int h = src.Height;

            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData srcData = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            // try/finally (M113): ambos UnlockBits garantizados aunque falle el bucle.
            try
            {
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                int srcStride = srcData.Stride;
                int dstStride = dstData.Stride;

                for (int y = 0; y < h; y++)
                {
                    byte* srcRow = srcPtr + (y * srcStride);
                    byte* dstRow = dstPtr + (y * dstStride);

                    for (int x = 0; x < w; x++)
                    {
                        byte a = srcRow[x * 4 + 3];
                        dstRow[x * 4 + 0] = tintColor.B;
                        dstRow[x * 4 + 1] = tintColor.G;
                        dstRow[x * 4 + 2] = tintColor.R;
                        dstRow[x * 4 + 3] = a;
                    }
                }
            }
            }
            finally
            {
                result.UnlockBits(dstData);
            }
            }
            finally
            {
                src.UnlockBits(srcData);
            }

            return result;
        }

        public static void SaveCompetitionEmblems(Image sourceImage, string liveCpkRoot, int competitionId)
        {
            if (sourceImage == null) throw new ArgumentNullException("sourceImage");
            if (string.IsNullOrEmpty(liveCpkRoot)) throw new ArgumentException("Ruta de LiveCPK no especificada.", "liveCpkRoot");

            string destFolder = Path.Combine(liveCpkRoot, "common", "render", "symbol", "emblemLc");
            Directory.CreateDirectory(destFolder);

            // 1. Recortar bordes transparentes
            Bitmap trimmed = null;
            if (sourceImage is Bitmap)
                trimmed = TrimTransparency((Bitmap)sourceImage);
            else
            {
                using (Bitmap temp = new Bitmap(sourceImage))
                    trimmed = TrimTransparency(temp);
            }

            try
            {
                // 2. Generar tamaños principales a todo color
                using (Bitmap l256 = FitToSquare(trimmed, 256, 4))
                using (Bitmap ll512 = FitToSquare(trimmed, 512, 8))
                {
                    string baseName = string.Format("emb_{0:D4}", competitionId);

                    // Guardar variantes estándar y alternativas siempre a todo color
                    // (PES 2021 consulta _b_l en menús de exhibición; nunca debe ser silueta monocroma negra)
                    l256.Save(Path.Combine(destFolder, baseName + "_l.png"), ImageFormat.Png);
                    ll512.Save(Path.Combine(destFolder, baseName + "_ll.png"), ImageFormat.Png);
                    l256.Save(Path.Combine(destFolder, baseName + "_b_l.png"), ImageFormat.Png);
                    ll512.Save(Path.Combine(destFolder, baseName + "_b_ll.png"), ImageFormat.Png);
                    l256.Save(Path.Combine(destFolder, baseName + "_w_l.png"), ImageFormat.Png);
                    ll512.Save(Path.Combine(destFolder, baseName + "_w_ll.png"), ImageFormat.Png);
                }
            }
            finally
            {
                if (trimmed != null) trimmed.Dispose();
            }
        }

        public static void SaveTeamCrests(Image sourceImage, string liveCpkRoot, uint teamId)
        {
            if (sourceImage == null) throw new ArgumentNullException("sourceImage");
            if (string.IsNullOrEmpty(liveCpkRoot)) throw new ArgumentException("Ruta de LiveCPK no especificada.", "liveCpkRoot");

            string destFolder = Path.Combine(liveCpkRoot, "common", "render", "symbol", "flag");
            Directory.CreateDirectory(destFolder);

            Bitmap trimmed = null;
            if (sourceImage is Bitmap)
                trimmed = TrimTransparency((Bitmap)sourceImage);
            else
            {
                using (Bitmap temp = new Bitmap(sourceImage))
                    trimmed = TrimTransparency(temp);
            }

            try
            {
                using (Bitmap r256 = FitToSquare(trimmed, 256, 4))
                using (Bitmap r512 = FitToSquare(trimmed, 512, 8))
                using (Bitmap dark256 = GenerateSilhouette(r256, Color.FromArgb(32, 32, 38)))
                using (Bitmap light256 = GenerateSilhouette(r256, Color.FromArgb(245, 245, 250)))
                {
                    string baseName = string.Format("e_{0:D6}", teamId);

                    r256.Save(Path.Combine(destFolder, baseName + "_r.png"), ImageFormat.Png);
                    r256.Save(Path.Combine(destFolder, baseName + "_r_l.png"), ImageFormat.Png);
                    r512.Save(Path.Combine(destFolder, baseName + "_r_ll.png"), ImageFormat.Png);

                    r256.Save(Path.Combine(destFolder, baseName + "_f.png"), ImageFormat.Png);
                    r512.Save(Path.Combine(destFolder, baseName + "_f_l.png"), ImageFormat.Png);
                    r512.Save(Path.Combine(destFolder, baseName + "_f_ll.png"), ImageFormat.Png);

                    dark256.Save(Path.Combine(destFolder, baseName + "_r_b.png"), ImageFormat.Png);
                    light256.Save(Path.Combine(destFolder, baseName + "_r_w.png"), ImageFormat.Png);
                }
            }
            finally
            {
                if (trimmed != null) trimmed.Dispose();
            }
        }

        public static Bitmap CreateCheckerboard(int width, int height, int cellSize = 8)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Color light = Color.FromArgb(32, 40, 58);
                Color dark = Color.FromArgb(20, 26, 40);
                using (var bLight = new SolidBrush(light))
                using (var bDark = new SolidBrush(dark))
                {
                    for (int y = 0; y < height; y += cellSize)
                    {
                        for (int x = 0; x < width; x += cellSize)
                        {
                            bool isEven = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                            g.FillRectangle(isEven ? bLight : bDark, x, y, cellSize, cellSize);
                        }
                    }
                }
            }
            return bmp;
        }

        public static int RepairLiveCpkEmblems(string liveCpkRoot)
        {
            if (string.IsNullOrEmpty(liveCpkRoot)) return 0;
            string destFolder = Path.Combine(liveCpkRoot, "common", "render", "symbol", "emblemLc");
            if (!Directory.Exists(destFolder)) return 0;

            int repaired = 0;
            var lFiles = Directory.GetFiles(destFolder, "emb_*_l.png");
            foreach (var lFile in lFiles)
            {
                string fn = Path.GetFileName(lFile);
                if (fn.Contains("_b_") || fn.Contains("_w_")) continue;
                if (!fn.EndsWith("_l.png", StringComparison.OrdinalIgnoreCase)) continue;
                // Base real quitando el sufijo "_l.png" (M112): asumir 8 caracteres
                // ("emb_XXXX") con 5 dígitos copiaba sobre otro emblema (emb_1234).
                string baseName = fn.Substring(0, fn.Length - "_l.png".Length);
                if (baseName.Length <= 4) continue; // sin ID tras "emb_"
                string bFile = Path.Combine(destFolder, baseName + "_b_l.png");
                string bllFile = Path.Combine(destFolder, baseName + "_b_ll.png");
                string llFile = Path.Combine(destFolder, baseName + "_ll.png");
                string wFile = Path.Combine(destFolder, baseName + "_w_l.png");
                string wllFile = Path.Combine(destFolder, baseName + "_w_ll.png");

                try
                {
                    File.Copy(lFile, bFile, true);
                    File.Copy(lFile, wFile, true);
                    // Solo cuenta como reparado el set completo (M112): si falta
                    // _ll no hay variantes _b_ll/_w_ll que generar.
                    if (File.Exists(llFile))
                    {
                        File.Copy(llFile, bllFile, true);
                        File.Copy(llFile, wllFile, true);
                        repaired++;
                    }
                }
                catch { }
            }
            return repaired;
        }

        public static bool DeleteCompetitionEmblems(string liveCpkRoot, int competitionId)
        {
            if (string.IsNullOrEmpty(liveCpkRoot)) return false;
            string destFolder = Path.Combine(liveCpkRoot, "common", "render", "symbol", "emblemLc");
            if (!Directory.Exists(destFolder)) return false;

            string pattern = string.Format("emb_{0:D4}*.png", competitionId);
            var files = Directory.GetFiles(destFolder, pattern);
            bool deletedAny = false;
            foreach (var f in files)
            {
                try
                {
                    File.Delete(f);
                    deletedAny = true;
                }
                catch { }
            }
            return deletedAny;
        }

        public static Bitmap CreateClassicEmblem()
        {
            int size = 512;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                // Escudo exterior dorado con degradado
                using (var outerPath = new GraphicsPath())
                {
                    outerPath.AddEllipse(24, 24, 464, 464);
                    using (var lgb = new LinearGradientBrush(new Point(0, 0), new Point(512, 512),
                        Color.FromArgb(255, 223, 0), Color.FromArgb(180, 120, 20)))
                    {
                        g.FillPath(lgb, outerPath);
                    }
                }

                // Círculo interior azul oscuro elegante (Dark Navy)
                using (var innerPath = new GraphicsPath())
                {
                    innerPath.AddEllipse(46, 46, 420, 420);
                    using (var sb = new SolidBrush(Color.FromArgb(15, 23, 42)))
                    {
                        g.FillPath(sb, innerPath);
                    }
                }

                // Anillo dorado interno
                using (var ringPen = new Pen(Color.FromArgb(255, 215, 0), 4f))
                {
                    g.DrawEllipse(ringPen, 64, 64, 384, 384);
                }

                // 5 Estrellas doradas en el arco superior
                DrawStar(g, 256, 110, 26, Color.FromArgb(255, 223, 0));
                DrawStar(g, 195, 125, 20, Color.FromArgb(245, 195, 30));
                DrawStar(g, 317, 125, 20, Color.FromArgb(245, 195, 30));
                DrawStar(g, 142, 155, 15, Color.FromArgb(220, 170, 20));
                DrawStar(g, 370, 155, 15, Color.FromArgb(220, 170, 20));

                // Texto "CLASSIC" y "TEAMS"
                using (var fontTitle = new Font("Arial Black", 44f, FontStyle.Bold))
                using (var brushTitle = new LinearGradientBrush(new Point(0, 190), new Point(0, 280),
                    Color.FromArgb(255, 245, 160), Color.FromArgb(255, 195, 0)))
                using (var penOutline = new Pen(Color.FromArgb(20, 15, 5), 4f))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    
                    var pathText = new GraphicsPath();
                    pathText.AddString("CLASSIC", fontTitle.FontFamily, (int)FontStyle.Bold, 50f, new Point(256, 215), sf);
                    g.DrawPath(penOutline, pathText);
                    g.FillPath(brushTitle, pathText);

                    var pathSub = new GraphicsPath();
                    pathSub.AddString("TEAMS", fontTitle.FontFamily, (int)FontStyle.Bold, 38f, new Point(256, 280), sf);
                    g.DrawPath(penOutline, pathSub);
                    g.FillPath(brushTitle, pathSub);
                }

                // Balón retro / icono geométrico
                using (var penGold = new Pen(Color.FromArgb(255, 215, 0), 3f))
                {
                    g.DrawEllipse(penGold, 236, 335, 40, 40);
                    g.DrawLine(penGold, 236, 355, 276, 355);
                    g.DrawLine(penGold, 256, 335, 256, 375);
                }

                // Texto "PES LEGENDS"
                using (var fontFoot = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (var brushFoot = new SolidBrush(Color.FromArgb(220, 200, 140)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("PES LEGENDS", fontFoot, brushFoot, new PointF(256, 400), sf);
                }
            }
            return bmp;
        }

        private static void DrawStar(Graphics g, float cx, float cy, float radius, Color color)
        {
            var points = new PointF[10];
            double rx = radius;
            double ry = radius * 0.42;
            for (int i = 0; i < 10; i++)
            {
                double r = (i % 2 == 0) ? rx : ry;
                double angle = (i * 36 - 90) * Math.PI / 180.0;
                points[i] = new PointF((float)(cx + r * Math.Cos(angle)), (float)(cy + r * Math.Sin(angle)));
            }
            using (var b = new SolidBrush(color))
            {
                g.FillPolygon(b, points);
            }
        }
    }
}
