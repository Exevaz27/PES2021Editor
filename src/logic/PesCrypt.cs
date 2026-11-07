using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;

namespace EditorLigasPes
{
    /// <summary>
    /// Generador de números pseudoaleatorios Mersenne Twister MT19937 (32-bit).
    /// Implementación nativa de alta fidelidad para el cifrado y descifrado de archivos EDIT00000000 de Konami.
    /// Desarrollado por EXEVAZ27 e inspirado en libpesXcrypter / Pes-Editor-Rebuilt.
    /// </summary>
    public class MersenneTwister
    {
        private readonly uint[] mt = new uint[624];
        private int mti;

        public MersenneTwister(byte[] keyBytes)
        {
            if (keyBytes == null || keyBytes.Length < 64)
                throw new ArgumentException("La clave debe tener al menos 64 bytes para inicializar MT19937.");

            uint[] key = new uint[16];
            for (int k = 0; k < 16; k++)
            {
                key[k] = BitConverter.ToUInt32(keyBytes, k * 4);
            }

            mt[0] = 19650218U;
            for (mti = 1; mti < 624; mti++)
            {
                uint s = mt[mti - 1] ^ (mt[mti - 1] >> 30);
                mt[mti] = unchecked((1812433253U * s) + (uint)mti);
            }

            int i = 1, j = 0;
            for (int k = 624; k > 0; k--)
            {
                uint s = mt[i - 1] ^ (mt[i - 1] >> 30);
                mt[i] = unchecked((mt[i] ^ (s * 1664525U)) + key[j] + (uint)j);
                i++; j++;
                if (i >= 624) { mt[0] = mt[623]; i = 1; }
                if (j >= 16) j = 0;
            }

            for (int k = 623; k > 0; k--)
            {
                uint s = mt[i - 1] ^ (mt[i - 1] >> 30);
                mt[i] = unchecked((mt[i] ^ (s * 1566083941U)) - (uint)i);
                i++;
                if (i >= 624) { mt[0] = mt[623]; i = 1; }
            }

            mt[0] = 0x80000000U;
            mti = 624;
        }

        public uint Next()
        {
            if (mti >= 624)
            {
                int kk = 0;
                uint y;
                for (; kk < 227; kk++)
                {
                    y = (mt[kk] & 0x80000000U) | (mt[kk + 1] & 0x7fffffffU);
                    mt[kk] = mt[kk + 397] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908b0dfU : 0U);
                }
                for (; kk < 623; kk++)
                {
                    y = (mt[kk] & 0x80000000U) | (mt[kk + 1] & 0x7fffffffU);
                    mt[kk] = mt[kk - 227] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908b0dfU : 0U);
                }
                y = (mt[623] & 0x80000000U) | (mt[0] & 0x7fffffffU);
                mt[623] = mt[396] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908b0dfU : 0U);
                mti = 0;
            }

            uint z = mt[mti++];
            z ^= z >> 11;
            z ^= (z << 7) & 0x9d2c5680U;
            z ^= (z << 15) & 0xefc60000U;
            z ^= z >> 18;
            return z;
        }
    }

    /// <summary>
    /// Modelo de datos en memoria para un archivo de guardado EDIT00000000 desencriptado.
    /// </summary>
    public class PesEditFile
    {
        public string FilePath { get; set; }
        public string Game { get; set; } // "2021" o "2020"
        public byte[] EncHeader { get; set; } // 320 bytes
        public byte[] Hdr { get; set; }       // 208 bytes
        public byte[] Desc { get; set; }      // 384 bytes típicos
        public byte[] Logo { get; set; }      // Imagen PNG oficial del save
        public byte[] Data { get; set; }      // Payload data.dat (~10.99 MB)
        public byte[] Serial { get; set; }    // Cuenta Steam ID / SID en UTF-16LE

        public string Description { get; set; }
        public string SaveType { get; set; }
        public string SaveVersion { get; set; }
        public string SteamId { get; set; }
        public bool IsModified { get; set; }

        public int TotalSize
        {
            get
            {
                int encLen = EncHeader != null ? EncHeader.Length : 320;
                int hdrLen = Hdr != null ? Hdr.Length : 208;
                int descLen = Desc != null ? Desc.Length : 0;
                int logoLen = Logo != null ? Logo.Length : 0;
                int dataLen = Data != null ? Data.Length : 0;
                int serialLen = Serial != null ? Serial.Length : 0;
                return encLen + hdrLen + descLen + logoLen + dataLen + serialLen;
            }
        }
    }

    /// <summary>
    /// Información y diagnóstico forense de un archivo contenedor secundario EDIT00000000.fl26x.
    /// Utilizado por fl26edit.dll para persistir clubes > 749 modificados en Modo Editar.
    /// </summary>
    public class Fl26xFileInfo
    {
        public bool Exists { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public string Magic { get; set; }
        public int Version { get; set; }
        public byte[] VerificationHash { get; set; }
        public uint TeamRecordsCount { get; set; }
        public uint SquadRecordsCount { get; set; }
        public uint TacticsRecordsCount { get; set; }
        public uint ManagerRecordsCount { get; set; }
        public bool IsValid { get; set; }
        public string StatusMessage { get; set; }
        public bool HasBackupPrev { get; set; }
        public string BackupPrevPath { get; set; }
        public long BackupPrevSize { get; set; }
        public DateTime BackupPrevModified { get; set; }
    }

    /// <summary>
    /// Motor Criptográfico Nativo en C# para archivos de opciones y partidas EDIT00000000.
    /// Compatible con PES 2020, PES 2021 y SP Football Life.
    /// Desarrollado por EXEVAZ27.
    /// </summary>
    public static class PesCrypt
    {
        public static readonly byte[] KeyPes2021 = HexToBytes("9061d866437724f892bab87121c76063f0919a7ded4780de51f5ddd108fe3284f5099200b23e889feb244305587600229bfeecf6500029d3427550b9ecd2f675");
        public static readonly byte[] KeyPes2020 = HexToBytes("e2bf510754e621782c5e8d33137ac9159977d9a01bc295d9bb9bb100841cb362e540d956455b7c7c4ff1da26b45a0c5c4d6b9e9875a939074c4b55bd8e01a931");

        public const int ENC = 320;
        public const int HDR = 208;

        public static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private static uint Rol(uint a, int s) { return (a << s) | (a >> (32 - s)); }
        private static uint Ror(uint a, int s) { return (a >> s) | (a << (32 - s)); }

        public static byte[] CryptStream(byte[] key, byte[] input, int offset, int len)
        {
            byte[] output = new byte[len];
            var mt = new MersenneTwister(key);
            uint c0 = mt.Next();
            uint c1 = mt.Next();
            uint c2 = mt.Next();
            uint c3 = mt.Next();

            int n4 = len >> 2;
            for (int i = 0; i < n4; i++)
            {
                uint c4 = mt.Next();
                uint iv = BitConverter.ToUInt32(input, offset + i * 4);
                uint ov = c4 ^ c3 ^ c2 ^ c1 ^ c0 ^ iv;
                BitConverter.GetBytes(ov).CopyTo(output, i * 4);

                c0 = Ror(c1, 15);
                c1 = Rol(c2, 11);
                c2 = Rol(c3, 7);
                c3 = Ror(c4, 13);
            }

            int rem = len & 3;
            if (rem > 0)
            {
                uint ks = mt.Next() ^ c3 ^ c2 ^ c1 ^ c0;
                int baseOffset = n4 * 4;
                for (int b = 0; b < rem; b++)
                {
                    output[baseOffset + b] = (byte)(input[offset + baseOffset + b] ^ ((ks >> (8 * b)) & 0xFF));
                }
            }
            return output;
        }

        public static byte[] CryptHeader(byte[] input, int offset, byte[] master)
        {
            byte[] hk = new byte[64];
            Buffer.BlockCopy(input, offset + 256, hk, 0, 64);
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    hk[i * 8 + j] ^= master[i * 8 + 7 - j];
                }
            }
            byte[] encPart = new byte[ENC];
            Buffer.BlockCopy(input, offset, encPart, 0, ENC);
            byte[] outHeader = CryptStream(hk, encPart, 0, ENC);
            Buffer.BlockCopy(input, offset + 256, outHeader, 256, 64);
            return outHeader;
        }

        public static byte[] DeriveKey(byte[] encHeader, uint param)
        {
            byte[] rk = new byte[64];
            Buffer.BlockCopy(encHeader, 0, rk, 0, 64);
            for (int i = 0; i < 256; i++)
            {
                rk[i & 63] ^= encHeader[64 + i];
            }
            for (int i = 0; i < 8; i++)
            {
                rk[i * 8] ^= (byte)param;
            }
            return rk;
        }

        public static PesEditFile Decrypt(byte[] bytes)
        {
            var res = TryDecryptWith(bytes, KeyPes2021, "2021");
            if (res != null) return res;

            res = TryDecryptWith(bytes, KeyPes2020, "2020");
            if (res != null) return res;

            throw new InvalidOperationException("No es un archivo de guardado de PES 2020/2021 válido o la firma está corrupta.");
        }

        private static PesEditFile TryDecryptWith(byte[] bytes, byte[] masterKey, string gameName)
        {
            if (bytes == null || bytes.Length < ENC + HDR) return null;

            byte[] enc = CryptHeader(bytes, 0, masterKey);
            byte[] kHdr = DeriveKey(enc, (uint)HDR);

            int p = ENC;
            byte[] hdr = CryptStream(kHdr, bytes, p, HDR);
            p += HDR;

            uint dataSize = BitConverter.ToUInt32(hdr, 64);
            uint logoSize = BitConverter.ToUInt32(hdr, 68);
            uint descSize = BitConverter.ToUInt32(hdr, 72);
            uint serialLength = BitConverter.ToUInt32(hdr, 76);

            long totalExpected = (long)ENC + HDR + dataSize + logoSize + descSize + serialLength * 2;
            if (totalExpected != bytes.Length) return null;

            byte[] desc = CryptStream(DeriveKey(enc, 0), bytes, p, (int)descSize);
            p += (int)descSize;

            byte[] logo = CryptStream(DeriveKey(enc, 1), bytes, p, (int)logoSize);
            p += (int)logoSize;

            byte[] data = CryptStream(DeriveKey(enc, 2), bytes, p, (int)dataSize);
            p += (int)dataSize;

            byte[] serial = CryptStream(DeriveKey(enc, 3), bytes, p, (int)serialLength * 2);

            var edit = new PesEditFile();
            edit.Game = gameName;
            edit.EncHeader = enc;
            edit.Hdr = hdr;
            edit.Desc = desc;
            edit.Logo = logo;
            edit.Data = data;
            edit.Serial = serial;
            edit.IsModified = false;

            edit.SaveType = ReadNullTerminatedAscii(hdr, 144, 32);
            edit.SaveVersion = ReadNullTerminatedAscii(hdr, 176, 32);
            edit.Description = ReadNullTerminatedUtf8(desc, 0, desc.Length);

            var sbSid = new StringBuilder();
            for (int i = 0; i < (int)serialLength; i++)
            {
                char ch = (char)(serial[i * 2] | (serial[i * 2 + 1] << 8));
                sbSid.Append(ch);
            }
            edit.SteamId = sbSid.ToString();

            return edit;
        }

        public static byte[] Encrypt(PesEditFile f)
        {
            if (f == null) throw new ArgumentNullException("f");
            if (f.Hdr == null) throw new ArgumentNullException("f.Hdr");
            if (f.Hdr.Length < HDR) throw new ArgumentException("La cabecera debe tener al menos " + HDR + " bytes.", "f.Hdr");
            if (f.EncHeader == null) throw new ArgumentNullException("f.EncHeader");
            if (f.EncHeader.Length < ENC) throw new ArgumentException("La cabecera cifrada debe tener al menos " + ENC + " bytes.", "f.EncHeader");
            if (f.Desc == null) throw new ArgumentNullException("f.Desc");
            if (f.Logo == null) throw new ArgumentNullException("f.Logo");
            if (f.Data == null) throw new ArgumentNullException("f.Data");
            if (f.Serial == null) throw new ArgumentNullException("f.Serial");
            // Serial impar: la cabecera guarda Length/2 y se escribían Length
            // bytes, rompiendo el roundtrip (M122). Serial es UTF-16LE: par.
            if ((f.Serial.Length & 1) != 0) throw new ArgumentException("Serial debe tener longitud par (UTF-16LE).", "f.Serial");
            // Cualquier Game != "2020" usaba la clave 2021 en silencio (M122).
            byte[] master;
            if (f.Game == "2020") master = KeyPes2020;
            else if (f.Game == "2021") master = KeyPes2021;
            else throw new ArgumentException("Juego desconocido: '" + f.Game + "' (se esperaba \"2020\" o \"2021\").", "f.Game");

            byte[] hdr = (byte[])f.Hdr.Clone();
            BitConverter.GetBytes((uint)f.Data.Length).CopyTo(hdr, 64);
            BitConverter.GetBytes((uint)f.Logo.Length).CopyTo(hdr, 68);
            BitConverter.GetBytes((uint)f.Desc.Length).CopyTo(hdr, 72);
            BitConverter.GetBytes((uint)(f.Serial.Length / 2)).CopyTo(hdr, 76);

            byte[] encPart = CryptHeader(f.EncHeader, 0, master);
            byte[] hdrPart = CryptStream(DeriveKey(f.EncHeader, (uint)HDR), hdr, 0, HDR);
            byte[] descPart = CryptStream(DeriveKey(f.EncHeader, 0), f.Desc, 0, f.Desc.Length);
            byte[] logoPart = CryptStream(DeriveKey(f.EncHeader, 1), f.Logo, 0, f.Logo.Length);
            byte[] dataPart = CryptStream(DeriveKey(f.EncHeader, 2), f.Data, 0, f.Data.Length);
            byte[] serialPart = CryptStream(DeriveKey(f.EncHeader, 3), f.Serial, 0, f.Serial.Length);

            int totalLen = encPart.Length + hdrPart.Length + descPart.Length + logoPart.Length + dataPart.Length + serialPart.Length;
            byte[] result = new byte[totalLen];
            int p = 0;

            Buffer.BlockCopy(encPart, 0, result, p, encPart.Length); p += encPart.Length;
            Buffer.BlockCopy(hdrPart, 0, result, p, hdrPart.Length); p += hdrPart.Length;
            Buffer.BlockCopy(descPart, 0, result, p, descPart.Length); p += descPart.Length;
            Buffer.BlockCopy(logoPart, 0, result, p, logoPart.Length); p += logoPart.Length;
            Buffer.BlockCopy(dataPart, 0, result, p, dataPart.Length); p += dataPart.Length;
            Buffer.BlockCopy(serialPart, 0, result, p, serialPart.Length);

            return result;
        }

        public static PesEditFile Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("El archivo especificado no existe: " + filePath);

            byte[] raw = File.ReadAllBytes(filePath);
            var file = Decrypt(raw);
            file.FilePath = filePath;
            return file;
        }

        public static void Save(PesEditFile f, string outputPath = null, bool createBackup = true)
        {
            if (f == null) throw new ArgumentNullException("f");
            string targetPath = !string.IsNullOrEmpty(outputPath) ? outputPath : f.FilePath;
            if (string.IsNullOrEmpty(targetPath))
                throw new InvalidOperationException("No se ha especificado la ruta de destino para guardar el archivo EDIT00000000.");

            if (createBackup && File.Exists(targetPath))
            {
                string bakPath = targetPath + ".bak";
                try
                {
                    File.Copy(targetPath, bakPath, true);
                    // Respaldo con timestamp (mismo formato que DatabaseManager.CreateTimestampedBackup):
                    // nunca sobrescribe una generacion anterior, asi que un guardado fallido o un
                    // segundo guardado ya no destruye el unico respaldo bueno. El .bak se conserva
                    // como puntero a la generacion mas reciente porque RestoreBackupAction lo busca
                    // en targetPath + ".bak" (MainForm.cs).
                    string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    string timestampedBakPath = targetPath + "." + timeStr + ".bak";
                    int counter = 1;
                    while (File.Exists(timestampedBakPath))
                    {
                        timestampedBakPath = targetPath + "." + timeStr + "_" + counter + ".bak";
                        counter++;
                    }
                    File.Copy(targetPath, timestampedBakPath, false);
                }
                catch (Exception ex)
                {
                    throw new IOException("No se pudo crear la copia de respaldo de seguridad (.bak) de " + targetPath + ": " + ex.Message, ex);
                }
            }

            byte[] encrypted = Encrypt(f);
            DatabaseManager.AtomicWriteFile(targetPath, encrypted);
            f.FilePath = targetPath;
            f.IsModified = false;
        }

        public static void SetSteamId(PesEditFile f, string sid)
        {
            if (f == null) throw new ArgumentNullException("f");
            if (sid == null) sid = "";

            byte[] s = new byte[sid.Length * 2];
            for (int i = 0; i < sid.Length; i++)
            {
                char c = sid[i];
                s[i * 2] = (byte)(c & 0xFF);
                s[i * 2 + 1] = (byte)((c >> 8) & 0xFF);
            }
            f.Serial = s;
            f.SteamId = sid;
            f.IsModified = true;
        }

        public static void SetDescription(PesEditFile f, string text)
        {
            if (f == null) throw new ArgumentNullException("f");
            if (f.Desc == null) throw new ArgumentNullException("f.Desc");
            if (text == null) text = "";

            byte[] d = new byte[f.Desc.Length];
            byte[] encoded = Encoding.UTF8.GetBytes(text);
            int copyLen = d.Length > 0 ? Math.Min(encoded.Length, d.Length - 1) : 0;
            // Recortar en frontera de carácter UTF-8 (M123): partir un multibyte
            // dejaba un byte final inválido en el archivo.
            while (copyLen > 0)
            {
                int start = copyLen - 1;
                while (start > 0 && (encoded[start] & 0xC0) == 0x80) start--;
                int charLen;
                byte lead = encoded[start];
                if (lead < 0x80) charLen = 1;
                else if (lead < 0xE0) charLen = 2;
                else if (lead < 0xF0) charLen = 3;
                else charLen = 4;
                if (start + charLen > copyLen) copyLen = start;
                else break;
            }
            Buffer.BlockCopy(encoded, 0, d, 0, copyLen);
            f.Desc = d;
            // La propiedad refleja lo realmente guardado, no el texto completo (M123).
            f.Description = Encoding.UTF8.GetString(encoded, 0, copyLen);
            f.IsModified = true;
        }

        public static string GetCurrentWindowsSid()
        {
            try
            {
                var user = WindowsIdentity.GetCurrent().User;
                return user != null ? user.Value : "";
            }
            catch
            {
                return "";
            }
        }

        public static List<string> FindDefaultEditFiles()
        {
            var results = new List<string>();
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string konamiRoot = Path.Combine(docs, "KONAMI");
                if (Directory.Exists(konamiRoot))
                {
                    string[] candidates = Directory.GetFiles(konamiRoot, "EDIT00000000", SearchOption.AllDirectories);
                    foreach (var c in candidates)
                    {
                        if (!results.Contains(c)) results.Add(c);
                    }
                }
            }
            catch { }
            return results;
        }

        private static string ReadNullTerminatedAscii(byte[] buf, int offset, int maxLen)
        {
            int end = offset;
            int limit = Math.Min(buf.Length, offset + maxLen);
            while (end < limit && buf[end] != 0) end++;
            return Encoding.ASCII.GetString(buf, offset, end - offset);
        }

        private static string ReadNullTerminatedUtf8(byte[] buf, int offset, int maxLen)
        {
            int end = offset;
            int limit = Math.Min(buf.Length, offset + maxLen);
            while (end < limit && buf[end] != 0) end++;
            return Encoding.UTF8.GetString(buf, offset, end - offset);
        }

        /// <summary>
        /// Inspecciona y extrae metadatos forenses del archivo de persistencia extendido EDIT00000000.fl26x.
        /// Este archivo es generado por fl26edit.dll para almacenar clubes con ID >= 750.
        /// </summary>
        public static Fl26xFileInfo InspectFl26xFile(string editFilePath)
        {
            var info = new Fl26xFileInfo();
            if (string.IsNullOrEmpty(editFilePath))
            {
                info.StatusMessage = "Ruta de guardado no especificada.";
                return info;
            }

            string fl26xPath = editFilePath + ".fl26x";
            string prevPath = fl26xPath + ".prev";

            info.FilePath = fl26xPath;
            info.BackupPrevPath = prevPath;
            info.HasBackupPrev = File.Exists(prevPath);
            if (info.HasBackupPrev)
            {
                try
                {
                    var fiPrev = new FileInfo(prevPath);
                    info.BackupPrevSize = fiPrev.Length;
                    info.BackupPrevModified = fiPrev.LastWriteTime;
                }
                catch { }
            }

            if (!File.Exists(fl26xPath))
            {
                info.Exists = false;
                info.StatusMessage = "No existe archivo .fl26x aún. (Los clubes > 749 no han sido modificados en Modo Editar o no se ha iniciado con fl26edit activo).";
                return info;
            }

            info.Exists = true;
            try
            {
                var fi = new FileInfo(fl26xPath);
                info.FileSize = fi.Length;
                info.LastModified = fi.LastWriteTime;

                byte[] data = File.ReadAllBytes(fl26xPath);
                if (data.Length < 44) // 8 magic + 32 hash + 4 count
                {
                    info.IsValid = false;
                    info.StatusMessage = "Archivo .fl26x truncado o incompleto (tamaño < 44 bytes).";
                    return info;
                }

                string magic = Encoding.ASCII.GetString(data, 0, 8);
                info.Magic = magic;

                if (magic == "FL26EDX2")
                {
                    info.Version = 2;
                }
                else if (magic == "FL26EDX1")
                {
                    info.Version = 1;
                }
                else
                {
                    info.IsValid = false;
                    info.StatusMessage = string.Format("Cabecera mágica desconocida: '{0}' (se esperaba FL26EDX2 o FL26EDX1).", magic);
                    return info;
                }

                info.VerificationHash = new byte[32];
                Buffer.BlockCopy(data, 8, info.VerificationHash, 0, 32);

                int offset = 40;
                int tables = (info.Version == 2) ? 4 : 3;
                if (data.Length >= offset + (tables * 4))
                {
                    info.TeamRecordsCount = BitConverter.ToUInt32(data, offset);
                    info.SquadRecordsCount = BitConverter.ToUInt32(data, offset + 4);
                    info.TacticsRecordsCount = BitConverter.ToUInt32(data, offset + 8);
                    if (info.Version == 2)
                    {
                        info.ManagerRecordsCount = BitConverter.ToUInt32(data, offset + 12);
                    }
                    info.IsValid = true;
                    info.StatusMessage = string.Format("Válido ({0}): {1} clubes, {2} plantillas, {3} tácticas{4}.",
                        info.Magic, info.TeamRecordsCount, info.SquadRecordsCount, info.TacticsRecordsCount,
                        (info.Version == 2 ? string.Format(", {0} entrenadores", info.ManagerRecordsCount) : ""));
                }
                else
                {
                    info.IsValid = false;
                    info.StatusMessage = "Archivo .fl26x no contiene contadores de tablas completos.";
                }
            }
            catch (Exception ex)
            {
                info.IsValid = false;
                info.StatusMessage = "Error al leer .fl26x: " + ex.Message;
            }

            return info;
        }

        /// <summary>
        /// Restaura el respaldo previo (.fl26x.prev) generado automáticamente por fl26edit.dll.
        /// </summary>
        public static bool RestoreFl26xBackup(string editFilePath, out string message)
        {
            if (string.IsNullOrEmpty(editFilePath))
            {
                message = "Ruta de archivo no válida.";
                return false;
            }
            string fl26xPath = editFilePath + ".fl26x";
            string prevPath = fl26xPath + ".prev";

            if (!File.Exists(prevPath))
            {
                message = "No existe copia previa (.fl26x.prev) para restaurar.";
                return false;
            }

            try
            {
                // Validar el .prev antes de tocar el activo (M124): cabecera
                // FL26EDX1/FL26EDX2 y tamaño mínimo, como InspectFl26xFile.
                using (var fs = new FileStream(prevPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 44)
                    {
                        message = "La copia previa (.fl26x.prev) está truncada o incompleta; no se restauró nada.";
                        return false;
                    }
                    byte[] magicBytes = new byte[8];
                    fs.Read(magicBytes, 0, 8);
                    string magic = Encoding.ASCII.GetString(magicBytes, 0, 8);
                    if (magic != "FL26EDX1" && magic != "FL26EDX2")
                    {
                        message = "La copia previa (.fl26x.prev) tiene una cabecera desconocida ('" + magic + "'); no se restauró nada.";
                        return false;
                    }
                }

                // Respaldo del activo actual antes de sobrescribirlo (M124).
                if (File.Exists(fl26xPath))
                {
                    File.Copy(fl26xPath, fl26xPath + ".bak", true);
                }

                File.Copy(prevPath, fl26xPath, true);
                message = "Copia previa .fl26x.prev restaurada exitosamente como archivo activo.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Fallo al restaurar .fl26x.prev: " + ex.Message;
                return false;
            }
        }
    }
}
