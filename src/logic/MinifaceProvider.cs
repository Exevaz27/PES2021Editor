using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EditorLigasPes
{
    /// <summary>
    /// Proveedor de Minifaces reales (fotos oficiales de futbolistas)
    /// Extrae y decodifica archivos DDS (DXT1, DXT3, DXT5, ARGB) desde Sider / miniface_server
    /// y genera avatares estilizados vectoriales de alta definición en caso de no poseer foto.
    /// </summary>
    public static class MinifaceProvider
    {
        private static readonly Dictionary<uint, string> facePaths = new Dictionary<uint, string>();
        private static readonly Dictionary<uint, Bitmap> faceCache = new Dictionary<uint, Bitmap>();
        private static bool isIndexed = false;

        public static int IndexedCount { get { return facePaths.Count; } }
        public static string PrimaryFolder { get; private set; }
        private static List<string> _searchFolders = new List<string>();
        public static List<string> SearchFolders { get { return _searchFolders; } }

        // Último error de indexado (M117): antes se tragaba con catch {}.
        public static string LastInitializeError { get; private set; }

        public static void Initialize(string customSiderPath = null, bool forceReindex = false)
        {
            if (isIndexed && !forceReindex) return;
            if (forceReindex)
            {
                lock (faceCache)
                {
                    foreach (var cached in faceCache.Values)
                    {
                        if (cached != null) cached.Dispose();
                    }
                    faceCache.Clear();
                }
                isIndexed = false;
            }
            facePaths.Clear();
            LastInitializeError = null;

            AppConfig cfg = null;
            try { cfg = AppConfig.Load(); } catch { }

            // 1. Obtener o auto-crear la carpeta de minifaces principal en Sider
            string primary = null;
            if (cfg != null)
            {
                primary = cfg.GetOrDetectMinifaceFolder(true);
            }
            if (string.IsNullOrEmpty(primary) && !string.IsNullOrEmpty(customSiderPath) && Directory.Exists(customSiderPath))
            {
                primary = Path.Combine(customSiderPath, "content", "miniface-server");
                try
                {
                    if (!Directory.Exists(primary))
                    {
                        Directory.CreateDirectory(primary);
                        Directory.CreateDirectory(Path.Combine(primary, "minifaces"));
                    }
                }
                catch { }
            }
            PrimaryFolder = primary;

            // 2. Construir lista de raíces de búsqueda
            var searchRoots = new List<string>();
            if (!string.IsNullOrEmpty(primary) && Directory.Exists(primary))
            {
                searchRoots.Add(primary);
            }

            // Raíces del Sider configurado
            string siderPath = !string.IsNullOrEmpty(customSiderPath) ? customSiderPath : (cfg != null ? cfg.SiderFolder : null);
            string contentPath = cfg != null && !string.IsNullOrEmpty(cfg.ContentFolder) ? cfg.ContentFolder :
                                 (!string.IsNullOrEmpty(siderPath) ? Path.Combine(siderPath, "content") : null);

            if (!string.IsNullOrEmpty(contentPath) && Directory.Exists(contentPath))
            {
                string alt1 = Path.Combine(contentPath, "minifaces");
                if (Directory.Exists(alt1) && !searchRoots.Contains(alt1)) searchRoots.Add(alt1);
                string alt2 = Path.Combine(contentPath, "miniface_server", "MiniFaces");
                if (Directory.Exists(alt2) && !searchRoots.Contains(alt2)) searchRoots.Add(alt2);
                string alt3 = Path.Combine(contentPath, "miniface_server");
                if (Directory.Exists(alt3) && !searchRoots.Contains(alt3)) searchRoots.Add(alt3);
            }

            // Raíces en livecpk (common\render\symbol\player y MiniFaces)
            string livecpkPath = cfg != null && !string.IsNullOrEmpty(cfg.LiveCpkFolder) ? cfg.LiveCpkFolder :
                                 (!string.IsNullOrEmpty(siderPath) ? Path.Combine(siderPath, "livecpk") : null);

            if (!string.IsNullOrEmpty(livecpkPath) && Directory.Exists(livecpkPath))
            {
                try
                {
                    foreach (var modDir in Directory.GetDirectories(livecpkPath))
                    {
                        string renderPlayer = Path.Combine(modDir, "common", "render", "symbol", "player");
                        if (Directory.Exists(renderPlayer) && !searchRoots.Contains(renderPlayer))
                        {
                            searchRoots.Add(renderPlayer);
                        }
                        string liveMini = Path.Combine(modDir, "MiniFaces");
                        if (Directory.Exists(liveMini) && !searchRoots.Contains(liveMini))
                        {
                            searchRoots.Add(liveMini);
                        }
                    }
                }
                catch { }
            }

            // Raíces históricas / parches alternativos conocidos
            // (La raiz de un parche puntual del autor se elimino: las raices
            // salen de la configuracion y del Sider del usuario.)

            _searchFolders = searchRoots;

            // 3. Escaneo de archivos (.dds, .png, .jpg, .jpeg)
            string[] patterns = new string[] { "*.dds", "*.png", "*.jpg", "*.jpeg" };
            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root)) continue;

                try
                {
                    foreach (var pattern in patterns)
                    {
                        var files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
                        foreach (var f in files)
                        {
                            string fn = Path.GetFileNameWithoutExtension(f);
                            uint pid;
                            if (uint.TryParse(fn, out pid))
                            {
                                if (!facePaths.ContainsKey(pid))
                                {
                                    facePaths[pid] = f;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // No aborta el resto de raíces, pero ya no es silencioso (M117).
                    if (LastInitializeError == null) LastInitializeError = ex.Message;
                }
            }

            isIndexed = true;
        }

        public static bool HasRealFace(uint playerId)
        {
            if (!isIndexed) Initialize();
            return facePaths.ContainsKey(playerId);
        }

        public static Bitmap GetMiniface(uint playerId, int targetSize = 128, string posName = "CF", string playerName = "")
        {
            if (!isIndexed) Initialize();

            lock (faceCache)
            {
                Bitmap cached;
                if (faceCache.TryGetValue(playerId, out cached) && cached != null)
                {
                    // Copia propia (M118): nunca se entrega el Bitmap cacheado
                    // compartido; el llamador puede disponerlo sin romper la caché.
                    if (cached.Width == targetSize && cached.Height == targetSize)
                        return new Bitmap(cached);
                    return new Bitmap(cached, targetSize, targetSize);
                }
            }

            Bitmap bmp = null;
            string facePath;
            if (facePaths.TryGetValue(playerId, out facePath) && File.Exists(facePath))
            {
                try
                {
                    string ext = Path.GetExtension(facePath).ToLowerInvariant();
                    if (ext == ".dds")
                    {
                        bmp = DecodeDds(facePath);
                    }
                    else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                    {
                        byte[] fileBytes = File.ReadAllBytes(facePath);
                        using (var ms = new MemoryStream(fileBytes))
                        {
                            using (var temp = Image.FromStream(ms))
                            {
                                bmp = new Bitmap(temp);
                            }
                        }
                    }
                }
                catch { bmp = null; }
            }

            if (bmp == null)
            {
                bmp = GenerateAvatar(playerId, posName, playerName, 128);
            }

            if (bmp.Width != targetSize || bmp.Height != targetSize)
            {
                var resized = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(resized))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(bmp, 0, 0, targetSize, targetSize);
                }
                bmp = resized;
            }

            lock (faceCache)
            {
                if (faceCache.Count > 1000)
                {
                    // Evitar sobreconsumo de memoria, disponiendo antes (M118/B64).
                    foreach (var old in faceCache.Values)
                    {
                        if (old != null) old.Dispose();
                    }
                    faceCache.Clear();
                }
                faceCache[playerId] = bmp;
            }

            return new Bitmap(bmp);
        }

        private static Bitmap DecodeDds(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 128) return null;
            if (bytes[0] != 0x44 || bytes[1] != 0x44 || bytes[2] != 0x53 || bytes[3] != 0x20) // "DDS "
                return null;

            int height = BitConverter.ToInt32(bytes, 12);
            int width = BitConverter.ToInt32(bytes, 16);
            int flags = BitConverter.ToInt32(bytes, 80);
            string fourCC = System.Text.Encoding.ASCII.GetString(bytes, 84, 4);

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[width * height * 4];

            int offset = 128;

            if ((flags & 0x04) != 0) // FourCC
            {
                if (fourCC == "DXT1")
                {
                    DecodeDXT1(bytes, offset, pixels, width, height);
                }
                else if (fourCC == "DXT3")
                {
                    DecodeDXT3(bytes, offset, pixels, width, height);
                }
                else if (fourCC == "DXT5")
                {
                    DecodeDXT5(bytes, offset, pixels, width, height);
                }
                else
                {
                    bmp.UnlockBits(bmpData);
                    bmp.Dispose();
                    return null;
                }
            }
            else
            {
                // ARGB sin comprimir
                int copyLen = Math.Min(pixels.Length, bytes.Length - 128);
                Buffer.BlockCopy(bytes, 128, pixels, 0, copyLen);
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private static void DecodeDXT1(byte[] src, int srcOffset, byte[] dst, int width, int height)
        {
            int blockOffset = srcOffset;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    if (blockOffset + 8 > src.Length) break;
                    ushort c0 = BitConverter.ToUInt16(src, blockOffset);
                    ushort c1 = BitConverter.ToUInt16(src, blockOffset + 2);
                    uint code = BitConverter.ToUInt32(src, blockOffset + 4);
                    blockOffset += 8;

                    Color[] col = new Color[4];
                    col[0] = Decode565(c0);
                    col[1] = Decode565(c1);
                    if (c0 > c1)
                    {
                        col[2] = Color.FromArgb(255, (2 * col[0].R + col[1].R) / 3, (2 * col[0].G + col[1].G) / 3, (2 * col[0].B + col[1].B) / 3);
                        col[3] = Color.FromArgb(255, (col[0].R + 2 * col[1].R) / 3, (col[0].G + 2 * col[1].G) / 3, (col[0].B + 2 * col[1].B) / 3);
                    }
                    else
                    {
                        col[2] = Color.FromArgb(255, (col[0].R + col[1].R) / 2, (col[0].G + col[1].G) / 2, (col[0].B + col[1].B) / 2);
                        col[3] = Color.FromArgb(0, 0, 0, 0);
                    }

                    for (int py = 0; py < 4; py++)
                    {
                        int dy = y + py;
                        if (dy >= height) break;
                        for (int px = 0; px < 4; px++)
                        {
                            int dx = x + px;
                            if (dx >= width) break;
                            int idx = (int)((code >> (2 * (py * 4 + px)))) & 3;
                            Color c = col[idx];
                            int pi = (dy * width + dx) * 4;
                            dst[pi + 0] = c.B;
                            dst[pi + 1] = c.G;
                            dst[pi + 2] = c.R;
                            dst[pi + 3] = c.A;
                        }
                    }
                }
            }
        }

        private static void DecodeDXT3(byte[] src, int srcOffset, byte[] dst, int width, int height)
        {
            int blockOffset = srcOffset;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    if (blockOffset + 16 > src.Length) break;
                    ulong alphaBits = BitConverter.ToUInt64(src, blockOffset);
                    ushort c0 = BitConverter.ToUInt16(src, blockOffset + 8);
                    ushort c1 = BitConverter.ToUInt16(src, blockOffset + 10);
                    uint code = BitConverter.ToUInt32(src, blockOffset + 12);
                    blockOffset += 16;

                    Color[] col = new Color[4];
                    col[0] = Decode565(c0);
                    col[1] = Decode565(c1);
                    col[2] = Color.FromArgb(255, (2 * col[0].R + col[1].R) / 3, (2 * col[0].G + col[1].G) / 3, (2 * col[0].B + col[1].B) / 3);
                    col[3] = Color.FromArgb(255, (col[0].R + 2 * col[1].R) / 3, (col[0].G + 2 * col[1].G) / 3, (col[0].B + 2 * col[1].B) / 3);

                    for (int py = 0; py < 4; py++)
                    {
                        int dy = y + py;
                        if (dy >= height) break;
                        for (int px = 0; px < 4; px++)
                        {
                            int dx = x + px;
                            if (dx >= width) break;
                            int pixelIdx = py * 4 + px;
                            int idx = (int)((code >> (2 * pixelIdx))) & 3;
                            int a = (int)((alphaBits >> (4 * pixelIdx)) & 0x0F) * 17;
                            Color c = col[idx];
                            int pi = (dy * width + dx) * 4;
                            dst[pi + 0] = c.B;
                            dst[pi + 1] = c.G;
                            dst[pi + 2] = c.R;
                            dst[pi + 3] = (byte)a;
                        }
                    }
                }
            }
        }

        private static void DecodeDXT5(byte[] src, int srcOffset, byte[] dst, int width, int height)
        {
            int blockOffset = srcOffset;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    if (blockOffset + 16 > src.Length) break;
                    byte a0 = src[blockOffset];
                    byte a1 = src[blockOffset + 1];
                    ulong aBits = 0;
                    for (int b = 0; b < 6; b++) aBits |= ((ulong)src[blockOffset + 2 + b]) << (8 * b);

                    ushort c0 = BitConverter.ToUInt16(src, blockOffset + 8);
                    ushort c1 = BitConverter.ToUInt16(src, blockOffset + 10);
                    uint code = BitConverter.ToUInt32(src, blockOffset + 12);
                    blockOffset += 16;

                    byte[] alphas = new byte[8];
                    alphas[0] = a0;
                    alphas[1] = a1;
                    if (a0 > a1)
                    {
                        for (int k = 1; k < 7; k++) alphas[k + 1] = (byte)(((7 - k) * a0 + k * a1) / 7);
                    }
                    else
                    {
                        for (int k = 1; k < 5; k++) alphas[k + 1] = (byte)(((5 - k) * a0 + k * a1) / 5);
                        alphas[6] = 0;
                        alphas[7] = 255;
                    }

                    Color[] col = new Color[4];
                    col[0] = Decode565(c0);
                    col[1] = Decode565(c1);
                    col[2] = Color.FromArgb(255, (2 * col[0].R + col[1].R) / 3, (2 * col[0].G + col[1].G) / 3, (2 * col[0].B + col[1].B) / 3);
                    col[3] = Color.FromArgb(255, (col[0].R + 2 * col[1].R) / 3, (col[0].G + 2 * col[1].G) / 3, (col[0].B + 2 * col[1].B) / 3);

                    for (int py = 0; py < 4; py++)
                    {
                        int dy = y + py;
                        if (dy >= height) break;
                        for (int px = 0; px < 4; px++)
                        {
                            int dx = x + px;
                            if (dx >= width) break;
                            int pixelIdx = py * 4 + px;
                            int idx = (int)((code >> (2 * pixelIdx))) & 3;
                            int aIdx = (int)((aBits >> (3 * pixelIdx))) & 7;
                            Color c = col[idx];
                            int pi = (dy * width + dx) * 4;
                            dst[pi + 0] = c.B;
                            dst[pi + 1] = c.G;
                            dst[pi + 2] = c.R;
                            dst[pi + 3] = alphas[aIdx];
                        }
                    }
                }
            }
        }

        private static Color Decode565(ushort c)
        {
            int r = ((c >> 11) & 0x1F) * 255 / 31;
            int g = ((c >> 5) & 0x3F) * 255 / 63;
            int b = (c & 0x1F) * 255 / 31;
            return Color.FromArgb(255, r, g, b);
        }

        public static Bitmap GenerateAvatar(uint playerId, string posName, string playerName, int size)
        {
            // DrawString(null) lanzaba y abortaba DisplayPlayer (M116).
            if (posName == null) posName = "CF";
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Color c1, c2;
                if (posName == "GK") { c1 = Color.FromArgb(234, 179, 8); c2 = Color.FromArgb(161, 98, 7); }
                else if (posName == "CB" || posName == "LB" || posName == "RB") { c1 = Color.FromArgb(59, 130, 246); c2 = Color.FromArgb(29, 78, 216); }
                else if (posName == "DMF" || posName == "CMF" || posName == "LMF" || posName == "RMF" || posName == "AMF") { c1 = Color.FromArgb(16, 185, 129); c2 = Color.FromArgb(4, 120, 87); }
                else { c1 = Color.FromArgb(239, 68, 68); c2 = Color.FromArgb(185, 28, 28); }

                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, size, size), c1, c2, 45f))
                {
                    g.FillEllipse(brush, 4, 4, size - 8, size - 8);
                }

                using (var pen = new Pen(Color.FromArgb(180, 255, 255, 255), 3f))
                {
                    g.DrawEllipse(pen, 5, 5, size - 10, size - 10);
                }

                string initials = GetInitials(playerName);
                using (var font = new Font("Segoe UI", size * 0.32f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(initials, font, brush, new RectangleF(0, 0, size, size * 0.85f), sf);
                }

                // Badge de Posición en la parte inferior
                int badgeH = (int)(size * 0.24f);
                int badgeW = (int)(size * 0.50f);
                int bx = (size - badgeW) / 2;
                int by = size - badgeH - 2;

                using (var bBrush = new SolidBrush(Color.FromArgb(220, 15, 23, 42)))
                {
                    g.FillRectangle(bBrush, bx, by, badgeW, badgeH);
                }
                using (var bPen = new Pen(Color.FromArgb(255, 255, 255), 1.5f))
                {
                    g.DrawRectangle(bPen, bx, by, badgeW, badgeH);
                }

                using (var bFont = new Font("Segoe UI", size * 0.15f, FontStyle.Bold))
                using (var bTextBrush = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(posName, bFont, bTextBrush, new RectangleF(bx, by, badgeW, badgeH), sf);
                }
            }
            return bmp;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "PES";
            name = name.Trim();
            var parts = name.Split(new char[] { ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "PES"; // blanco o solo separadores (M115)
            if (parts.Length == 1)
            {
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            }
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpper();
        }
    }
}
