using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EditorLigasPes
{
    public class CpkFileEntry
    {
        public string FileName { get; set; }
        public string DirName { get; set; }
        public long FileOffset { get; set; }
        public long FileSize { get; set; }
        public long ExtractSize { get; set; }
    }

    public static class CpkExtractor
    {
        // Des-enmascarado XOR de tablas @UTF de CRI (formato CPK).
        // Cada byte se XOR-ea con una mascara evolutiva: empieza en 0x5F y, tras
        // cada byte, se multiplica por 0x15 (mod 256, por desborde de byte). La
        // secuencia es determinista e independiente de los datos, asi que aplicar
        // la misma operacion dos veces restaura los datos originales.
        private static byte[] UnmaskUtf(byte[] data)
        {
            byte[] res = new byte[data.Length];
            byte m = 0x5F;
            byte mult = 0x15;
            for (int i = 0; i < data.Length; i++)
            {
                res[i] = (byte)(data[i] ^ m);
                m = (byte)(m * mult);
            }
            return res;
        }

        public static List<Dictionary<string, object>> ParseUtfTable(byte[] data)
        {
            if (data == null || data.Length < 32) return new List<Dictionary<string, object>>();

            // Verificar o des-enmascarar si no empieza con @UTF
            if (data[0] != 0x40 || data[1] != 0x55 || data[2] != 0x54 || data[3] != 0x46)
            {
                data = UnmaskUtf(data);
            }

            if (data.Length < 32 || data[0] != 0x40 || data[1] != 0x55 || data[2] != 0x54 || data[3] != 0x46)
                throw new Exception("El bloque no es una tabla @UTF valida de CRI.");

            int t = 8;
            uint rowsOff = ReadUInt32BE(data, t);
            uint strOff = ReadUInt32BE(data, t + 4);
            ushort nCols = ReadUInt16BE(data, t + 16);
            ushort rowLen = ReadUInt16BE(data, t + 18);
            uint nRows = ReadUInt32BE(data, t + 20);

            // Validar límites razonables de columnas y filas para evitar loops infinitos u OOM
            if (nCols > 256 || nRows > 100000)
                return new List<Dictionary<string, object>>();

            if (t + rowsOff > data.Length || t + strOff > data.Length)
                return new List<Dictionary<string, object>>();

            Func<uint, string> getStr = (o) =>
            {
                long sLong = (long)t + (long)strOff + (long)o;
                if (sLong < 0 || sLong >= data.Length) return string.Empty;
                int s = (int)sLong;
                int e = s;
                while (e < data.Length && data[e] != 0) e++;
                if (e > s)
                    return Encoding.UTF8.GetString(data, s, e - s);
                return string.Empty;
            };

            var cols = new List<Tuple<string, byte, byte, object>>();
            int p = t + 24;
            for (int i = 0; i < nCols; i++)
            {
                if (p + 5 > data.Length) break;
                byte fl = data[p];
                uint nameOff = ReadUInt32BE(data, p + 1);
                string colName = getStr(nameOff);
                p += 5;

                byte st = (byte)(fl & 0xF0);
                byte type = (byte)(fl & 0x0F);
                object cVal = null;

                if (st == 0x30)
                {
                    if (type == 0 || type == 1) { if (p + 1 <= data.Length) { cVal = data[p]; p += 1; } }
                    else if (type == 2 || type == 3) { if (p + 2 <= data.Length) { cVal = ReadUInt16BE(data, p); p += 2; } }
                    else if (type == 4 || type == 5) { if (p + 4 <= data.Length) { cVal = ReadUInt32BE(data, p); p += 4; } }
                    else if (type == 6 || type == 7) { if (p + 8 <= data.Length) { cVal = ReadUInt64BE(data, p); p += 8; } }
                    else if (type == 0x0A) { if (p + 4 <= data.Length) { cVal = getStr(ReadUInt32BE(data, p)); p += 4; } }
                    else if (type == 8) { if (p + 4 <= data.Length) { cVal = ReadFloat32BE(data, p); p += 4; } }
                    else if (type == 0x0B) { if (p + 12 <= data.Length) { ulong doff = ReadUInt64BE(data, p); uint dsz = ReadUInt32BE(data, p + 8); cVal = "DATA:" + doff + "+" + dsz; p += 12; } }
                    else { throw new Exception("Tipo de columna @UTF desconocido en la cabecera: " + type + " (columna '" + colName + "'). Sin tamano conocido no se puede avanzar: abortar es mejor que desalinnear las columnas siguientes."); }
                }

                cols.Add(new Tuple<string, byte, byte, object>(colName, st, type, cVal));
            }

            var rows = new List<Dictionary<string, object>>();
            for (int r = 0; r < nRows; r++)
            {
                long qLong = (long)t + (long)rowsOff + ((long)r * (long)rowLen);
                if (qLong + rowLen > data.Length) break;
                int q = (int)qLong;
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                foreach (var col in cols)
                {
                    string name = col.Item1;
                    byte st = col.Item2;
                    byte type = col.Item3;
                    object cVal = col.Item4;

                    if (st == 0x30)
                    {
                        row[name] = cVal;
                    }
                    else if (st == 0x10)
                    {
                        row[name] = null;
                    }
                    else
                    {
                        object val = null;
                        if (type == 0 || type == 1) { if (q + 1 <= data.Length) { val = data[q]; q += 1; } }
                        else if (type == 2 || type == 3) { if (q + 2 <= data.Length) { val = ReadUInt16BE(data, q); q += 2; } }
                        else if (type == 4 || type == 5) { if (q + 4 <= data.Length) { val = ReadUInt32BE(data, q); q += 4; } }
                        else if (type == 6 || type == 7) { if (q + 8 <= data.Length) { val = ReadUInt64BE(data, q); q += 8; } }
                        else if (type == 0x0A) { if (q + 4 <= data.Length) { val = getStr(ReadUInt32BE(data, q)); q += 4; } }
                        else if (type == 8) { if (q + 4 <= data.Length) { val = ReadFloat32BE(data, q); q += 4; } }
                        else if (type == 0x0B) { if (q + 12 <= data.Length) { ulong doff = ReadUInt64BE(data, q); uint dsz = ReadUInt32BE(data, q + 8); val = "DATA:" + doff + "+" + dsz; q += 12; } }
                        else { throw new Exception("Tipo de columna @UTF desconocido en las filas: " + type + " (columna '" + name + "'). Sin tamano conocido no se puede avanzar: abortar es mejor que desalinnear las columnas siguientes."); }
                        row[name] = val;
                    }
                }
                rows.Add(row);
            }

            return rows;
        }

        public static byte[] DecompressCrilayla(byte[] src)
        {
            if (src == null || src.Length <= 16) return src;
            string magic = Encoding.ASCII.GetString(src, 0, 8);
            if (magic != "CRILAYLA") return src;

            uint usize = BitConverter.ToUInt32(src, 8);
            uint hsize = BitConverter.ToUInt32(src, 12);

            // Un usize cercano a 4 GB intentaria reservar ~4 GB de una vez. Se rechaza
            // todo lo que no quepa en un indice int (el resto sigue pudiendo agotar
            // memoria si el flujo miente: un tope de negocio menor queda pendiente).
            if (usize > int.MaxValue - 0x100)
                throw new InvalidDataException("Tamano descomprimido CRILAYLA fuera de rango: " + usize);
            // La copia del diccionario inicial (0x100 bytes) debe caber en el origen.
            long headerEnd = 16L + hsize;
            if (headerEnd + 0x100 > src.Length)
                throw new InvalidDataException("Cabecera CRILAYLA truncada: hsize=" + hsize + ", origen=" + src.Length + " bytes.");

            byte[] plain = new byte[0x100];
            Array.Copy(src, (int)headerEnd, plain, 0, 0x100);

            byte[] outBuf = new byte[0x100 + usize];
            Array.Copy(plain, 0, outBuf, 0, 0x100);

            int pos = (int)(headerEnd - 1);
            byte pool = 0;
            int cnt = 0;

            Func<int, int> readBits = (n) =>
            {
                int v = 0;
                while (n > 0)
                {
                    if (cnt == 0)
                    {
                        if (pos < 0) throw new InvalidDataException("Flujo CRILAYLA truncado: se pidieron bits mas alla del inicio del buffer.");
                        pool = src[pos--];
                        cnt = 8;
                    }
                    int t = Math.Min(n, cnt);
                    v = (v << t) | ((pool >> (cnt - t)) & ((1 << t) - 1));
                    cnt -= t;
                    n -= t;
                }
                return v;
            };

            int[] vle = new int[] { 2, 3, 5, 8 };
            int o = (int)(usize - 1);

            while (o >= 0)
            {
                if (readBits(1) != 0)
                {
                    int refOff = readBits(13) + 3;
                    int len = 3;
                    for (int lvl = 0; ; lvl++)
                    {
                        int w = vle[Math.Min(lvl, 3)];
                        int v = readBits(w);
                        len += v;
                        if (v != (1 << w) - 1) break;
                    }
                    // La referencia debe apuntar a datos ya escritos (o + refOff < usize).
                    // Sin esta guarda, un flujo corrupto lee fuera del buffer o trunca
                    // la salida en silencio cuando o se agota a mitad de copia.
                    if ((long)o + refOff >= (long)usize)
                        throw new InvalidDataException("Referencia CRILAYLA fuera de rango: o=" + o + ", refOff=" + refOff + ", usize=" + usize);
                    for (; len > 0 && o >= 0; len--, o--)
                    {
                        outBuf[0x100 + o] = outBuf[0x100 + o + refOff];
                    }
                }
                else
                {
                    outBuf[0x100 + o--] = (byte)readBits(8);
                }
            }

            return outBuf;
        }

        public static List<string> DetectGameInstallations()
        {
            // Sin rutas escritas a mano: el programa no adivina carpetas de
            // la PC de nadie. La carpeta del juego la elige el usuario en
            // Inicio y de ahi se deriva todo lo demas (ver AppConfig).
            return new List<string>();
        }

        // Primer numero tras un guion bajo (dt80_1000E -> 1000). Sin numero, -1 para
        // que esos archivos queden ultimos y se desempaten por nombre.
        private static int ExtractDtNumber(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return -1;
            var m = Regex.Match(fileName, @"_(\d+)");
            int n;
            return (m.Success && int.TryParse(m.Groups[1].Value, out n)) ? n : -1;
        }

        public static string FindBestDatabaseCpk(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir)) return null;

            // 1. Revisar carpeta download buscando DLCs oficiales o de Football Life
            string downloadDir = Path.Combine(gameDir, "download");
            if (Directory.Exists(downloadDir))
            {
                // Ordenar dt80_ descendente por NUMERO (dt80_1000E > dt80_900E). El orden
                // lexicografico anterior dejaba dt80_900E antes que dt80_1000E.
                var dt80Files = Directory.GetFiles(downloadDir, "dt80_*_x64.cpk")
                                         .OrderByDescending(f => ExtractDtNumber(Path.GetFileName(f)))
                                         .ThenByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                                         .ToList();
                if (dt80Files.Count > 0) return dt80Files[0];

                // Football Life data_s2526*.cpk
                var flFiles = Directory.GetFiles(downloadDir, "data_s*.cpk")
                                       .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                                       .ToList();
                if (flFiles.Count > 0) return flFiles[0];
            }

            // 2. Fallback base del juego dt10_x64.cpk
            string dt10 = Path.Combine(gameDir, "Data", "dt10_x64.cpk");
            if (File.Exists(dt10)) return dt10;

            return null;
        }

        // Lectura compartida de cabecera + TOC de un CPK (B60: antes estaba duplicada en
        // ExtractPesDbFromCpk y GetTocEntries). Devuelve false con un mensaje en `error`
        // ante cabecera truncada, magia invalida, tabla ilegible u offsets ausentes; el
        // llamador decide si lanzar (Extract) o devolver vacio (GetTocEntries).
        // NOTA sobre baseOffset: los FileOffset del TOC se interpretan relativos a este
        // valor; se usa el menor de TocOffset/ContentOffset como heuristica historica del
        // codigo, validada por uso con CPK reales pero no por la especificacion CPK. Si un
        // CPK real extrae archivos desplazados, revisar este criterio antes de cambiarlo.
        private static bool TryReadCpkToc(BinaryReader br, FileStream fs, out List<Dictionary<string, object>> tocRows, out long baseOffset, out string error)
        {
            tocRows = null;
            baseOffset = 0;
            error = null;

            byte[] head = ReadExactBytes(br, 16);
            if (head == null) { error = "El archivo CPK esta truncado: cabecera de menos de 16 bytes."; return false; }
            if (head[0] != 0x43 || head[1] != 0x50 || head[2] != 0x4B || head[3] != 0x20)
            { error = "El archivo no es un paquete CPK valido."; return false; }

            ulong hs = BitConverter.ToUInt64(head, 8);
            if (hs > ulong.MaxValue - 8) { error = "Tamano de tabla de cabecera CPK fuera de rango."; return false; }
            byte[] cpkTableBytes = ReadExactBytes(br, hs + 8);
            if (cpkTableBytes == null) { error = "El archivo CPK esta truncado: tabla de cabecera incompleta."; return false; }
            var cpkRows = ParseUtfTable(cpkTableBytes);
            if (cpkRows.Count == 0) { error = "No se pudo leer el encabezado CPK."; return false; }

            var cpkHeader = cpkRows[0];
            long tocOffset;
            try { tocOffset = Convert.ToInt64(cpkHeader["TocOffset"]); }
            catch (Exception ex) { error = "El encabezado CPK no trae TocOffset: " + ex.Message; return false; }
            long contentOffset = 0;
            if (cpkHeader.ContainsKey("ContentOffset"))
            {
                try { contentOffset = Convert.ToInt64(cpkHeader["ContentOffset"]); }
                catch (Exception ex) { error = "ContentOffset del CPK ilegible: " + ex.Message; return false; }
            }
            baseOffset = (contentOffset > 0) ? Math.Min(tocOffset, contentOffset) : tocOffset;

            fs.Seek(tocOffset, SeekOrigin.Begin);
            byte[] th = ReadExactBytes(br, 16);
            if (th == null) { error = "El archivo CPK esta truncado: cabecera TOC de menos de 16 bytes."; return false; }
            ulong ts = BitConverter.ToUInt64(th, 8);
            if (ts > ulong.MaxValue - 8) { error = "Tamano de tabla TOC del CPK fuera de rango."; return false; }
            byte[] tocBytes = ReadExactBytes(br, ts + 8);
            if (tocBytes == null) { error = "El archivo CPK esta truncado: tabla TOC incompleta."; return false; }
            tocRows = ParseUtfTable(tocBytes);
            return true;
        }

        // ReadBytes puede devolver menos bytes que los pedidos ante un archivo truncado
        // sin avisar: se exige la cantidad exacta (null si el tamano no es representable).
        private static byte[] ReadExactBytes(BinaryReader br, ulong count)
        {
            if (count > int.MaxValue) return null;
            byte[] buf = br.ReadBytes((int)count);
            if (buf.Length != (int)count) return null;
            return buf;
        }

        public static int ExtractPesDbFromCpk(string cpkPath, string targetFolder, Action<string, int, int> onProgress = null)
        {
            if (!File.Exists(cpkPath)) throw new FileNotFoundException("Archivo CPK no encontrado.", cpkPath);
            Directory.CreateDirectory(targetFolder);

            using (var fs = new FileStream(cpkPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                List<Dictionary<string, object>> tocRows;
                long baseOffset;
                string headerError;
                if (!TryReadCpkToc(br, fs, out tocRows, out baseOffset, out headerError))
                    throw new Exception(headerError);

                // Filtrar archivos de base de datos common/etc/pesdb
                var pesDbEntries = new List<CpkFileEntry>();
                foreach (var row in tocRows)
                {
                    string dir = row.ContainsKey("DirName") && row["DirName"] != null ? row["DirName"].ToString() : string.Empty;
                    string name = row.ContainsKey("FileName") && row["FileName"] != null ? row["FileName"].ToString() : string.Empty;

                    // La segunda condicion original (EndsWith "common/etc/pesdb") estaba
                    // subsumida por la primera: todo dir que termina asi contiene "pesdb".
                    if (dir.IndexOf("pesdb", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pesDbEntries.Add(new CpkFileEntry
                        {
                            DirName = dir,
                            FileName = name,
                            FileOffset = baseOffset + Convert.ToInt64(row["FileOffset"]),
                            FileSize = Convert.ToInt64(row["FileSize"]),
                            ExtractSize = row.ContainsKey("ExtractSize") ? Convert.ToInt64(row["ExtractSize"]) : Convert.ToInt64(row["FileSize"])
                        });
                    }
                }

                int count = 0;
                for (int i = 0; i < pesDbEntries.Count; i++)
                {
                    var entry = pesDbEntries[i];
                    fs.Seek(entry.FileOffset, SeekOrigin.Begin);
                    if (entry.FileSize < 0 || entry.FileSize > int.MaxValue)
                        throw new Exception("Tamano de archivo CPK fuera de rango para '" + entry.FileName + "': " + entry.FileSize);
                    byte[] fileData = br.ReadBytes((int)entry.FileSize);
                    if (fileData.Length != entry.FileSize)
                        throw new Exception("Archivo CPK truncado: se esperaban " + entry.FileSize + " bytes de '" + entry.FileName + "' y se leyeron " + fileData.Length + ".");

                    // Si esta comprimido con CRILAYLA, descomprimirlo
                    bool wasCompressed = fileData.Length > 8 && Encoding.ASCII.GetString(fileData, 0, 8) == "CRILAYLA";
                    if (wasCompressed)
                    {
                        fileData = DecompressCrilayla(fileData);
                    }

                    // ExtractSize antes se calculaba y nunca se contrastaba. Sin comprimir
                    // debe coincidir exacto; comprimido, el descomprimido incluye el
                    // diccionario inicial de 0x100 bytes, asi que solo se exige que no
                    // sea menor (un valor menor indica salida truncada o corrupta).
                    if (entry.ExtractSize > 0)
                    {
                        if (!wasCompressed && fileData.Length != entry.ExtractSize)
                            throw new Exception("ExtractSize incoherente para '" + entry.FileName + "': esperado " + entry.ExtractSize + ", obtenido " + fileData.Length + ".");
                        if (wasCompressed && fileData.Length < entry.ExtractSize)
                            throw new Exception("Descompresion CRILAYLA truncada para '" + entry.FileName + "': esperado al menos " + entry.ExtractSize + ", obtenido " + fileData.Length + ".");
                    }

                    // Anti Path Traversal: asegurar que el archivo se extraiga estrictamente dentro de targetFolder
                    string fullTargetDir = Path.GetFullPath(targetFolder);
                    string rawName = entry.FileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    string outPath = Path.GetFullPath(Path.Combine(fullTargetDir, rawName));
                    if (!outPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        outPath = Path.Combine(fullTargetDir, Path.GetFileName(entry.FileName));
                    }
                    string dir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllBytes(outPath, fileData);
                    count++;

                    if (onProgress != null)
                    {
                        onProgress(entry.FileName, count, pesDbEntries.Count);
                    }
                }

                return count;
            }
        }

        public static List<CpkFileEntry> GetTocEntries(string cpkPath, Func<string, string, bool> filter = null)
        {
            if (string.IsNullOrEmpty(cpkPath) || !File.Exists(cpkPath)) return new List<CpkFileEntry>();
            var results = new List<CpkFileEntry>();
            using (var fs = new FileStream(cpkPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                List<Dictionary<string, object>> tocRows;
                long baseOffset;
                string headerError;
                if (!TryReadCpkToc(br, fs, out tocRows, out baseOffset, out headerError))
                    return results;

                foreach (var row in tocRows)
                {
                    string dir = row.ContainsKey("DirName") && row["DirName"] != null ? row["DirName"].ToString() : string.Empty;
                    string name = row.ContainsKey("FileName") && row["FileName"] != null ? row["FileName"].ToString() : string.Empty;

                    if (filter == null || filter(dir, name))
                    {
                        results.Add(new CpkFileEntry
                        {
                            DirName = dir,
                            FileName = name,
                            FileOffset = baseOffset + Convert.ToInt64(row["FileOffset"]),
                            FileSize = Convert.ToInt64(row["FileSize"]),
                            ExtractSize = row.ContainsKey("ExtractSize") ? Convert.ToInt64(row["ExtractSize"]) : Convert.ToInt64(row["FileSize"])
                        });
                    }
                }
            }
            return results;
        }

        public static byte[] ExtractEntryBytes(string cpkPath, CpkFileEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(cpkPath) || !File.Exists(cpkPath)) return null;
            using (var fs = new FileStream(cpkPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(entry.FileOffset, SeekOrigin.Begin);
                byte[] data = br.ReadBytes((int)entry.FileSize);
                if (data.Length > 8 && Encoding.ASCII.GetString(data, 0, 8) == "CRILAYLA")
                {
                    data = DecompressCrilayla(data);
                }
                return data;
            }
        }

        private static ushort ReadUInt16BE(byte[] b, int offset)
        {
            return (ushort)((b[offset] << 8) | b[offset + 1]);
        }

        private static uint ReadUInt32BE(byte[] b, int offset)
        {
            return (uint)((b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3]);
        }

        private static float ReadFloat32BE(byte[] b, int offset)
        {
            byte[] fb = new byte[4];
            fb[0] = b[offset + 3]; fb[1] = b[offset + 2]; fb[2] = b[offset + 1]; fb[3] = b[offset];
            return BitConverter.ToSingle(fb, 0);
        }

        private static ulong ReadUInt64BE(byte[] b, int offset)
        {
            return ((ulong)b[offset] << 56) | ((ulong)b[offset + 1] << 48) | ((ulong)b[offset + 2] << 40) | ((ulong)b[offset + 3] << 32) |
                   ((ulong)b[offset + 4] << 24) | ((ulong)b[offset + 5] << 16) | ((ulong)b[offset + 6] << 8) | (ulong)b[offset + 7];
        }
    }
}
