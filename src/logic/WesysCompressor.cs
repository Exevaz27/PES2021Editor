using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace EditorLigasPes
{
    /// <summary>
    /// Manejador forense de compresión y descompresión del protocolo WESYS / zlib de Konami.
    /// Desarrollado por EXEVAZ27.
    /// </summary>
    public static class WesysCompressor
    {
        private static readonly byte[] WesysMagic = new byte[] { 0xFF, 0x10, 0x81, 0x57, 0x45, 0x53, 0x59, 0x53 }; // FF 10 81 W E S Y S

        public static byte[] Decompress(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("El archivo especificado no existe: " + filePath);

            byte[] fileBytes = File.ReadAllBytes(filePath);
            return DecompressBytes(fileBytes);
        }

        public static byte[] DecompressBytes(byte[] fileBytes)
        {
            if (fileBytes.Length < 16)
                throw new InvalidDataException("El archivo es demasiado pequeño para contener una cabecera WESYS válida.");

            string magic = Encoding.ASCII.GetString(fileBytes, 3, 5);
            if (magic != "WESYS")
                throw new InvalidDataException("Firma de contenedor inválida. Se esperaba 'WESYS', encontrado: " + magic);

            // Lectura little-endian explícita (B56): BitConverter depende de la
            // arquitectura; el formato WESYS guarda los tamaños en little-endian.
            uint compressedSize = ReadUInt32LE(fileBytes, 8);
            uint uncompressedSize = ReadUInt32LE(fileBytes, 12);

            // Validación de rango antes de cualquier cast a int (M94): tamaños
            // > 2 GB o un flujo menor que cabecera zlib + Adler-32 (6 bytes)
            // deben dar InvalidDataException, no Overflow/ArgumentOutOfRange.
            if (compressedSize < 6 || compressedSize > int.MaxValue ||
                (long)fileBytes.Length - 16 < (long)compressedSize)
                throw new InvalidDataException("El flujo comprimido está truncado o su tamaño es inválido.");

            // Cabecera zlib RFC 1950 (M95): CM = 8 (deflate), sin diccionario
            // preestablecido y checksum CMF/FLG múltiplo de 31.
            int cmf = fileBytes[16];
            int flg = fileBytes[17];
            if ((cmf & 0x0F) != 8 || (flg & 0x20) != 0 || ((cmf << 8) + flg) % 31 != 0)
                throw new InvalidDataException("Cabecera zlib inválida en el flujo WESYS.");

            // DeflateStream de .NET lee directamente el payload omitiendo los 2 bytes de cabecera y el checksum final
            using (var ms = new MemoryStream(fileBytes, 18, (int)compressedSize - 6))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                ds.CopyTo(outMs);
                byte[] decompressed = outMs.ToArray();
                if (decompressed.Length != uncompressedSize)
                    throw new InvalidDataException(string.Format("Tamaño de descompresión inconsistente: esperado {0}, obtenido {1}", uncompressedSize, decompressed.Length));
                // Adler-32 final en big-endian (M95): DeflateStream lo ignora.
                uint expectedAdler = (uint)((fileBytes[16 + (int)compressedSize - 4] << 24) |
                                            (fileBytes[16 + (int)compressedSize - 3] << 16) |
                                            (fileBytes[16 + (int)compressedSize - 2] << 8) |
                                             fileBytes[16 + (int)compressedSize - 1]);
                if (ComputeAdler32(decompressed) != expectedAdler)
                    throw new InvalidDataException("Checksum Adler-32 inconsistente en el flujo WESYS.");
                return decompressed;
            }
        }

        public static byte[] Compress(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new ArgumentException("El payload para comprimir no puede ser nulo ni vacío.");

            byte[] compressedData;
            using (var ms = new MemoryStream())
            {
                // Cabecera RFC 1950 zlib estándar
                ms.WriteByte(0x78);
                ms.WriteByte(0x01);

                using (var ds = new DeflateStream(ms, CompressionMode.Compress, true))
                {
                    ds.Write(payload, 0, payload.Length);
                }

                // Cálculo de Adler-32 (mismo algoritmo que verifica DecompressBytes)
                uint adler = ComputeAdler32(payload);
                byte[] adlerBytes = BitConverter.GetBytes(adler);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(adlerBytes); // Adler-32 se almacena en Big-Endian según RFC 1950
                ms.Write(adlerBytes, 0, 4);

                compressedData = ms.ToArray();
            }

            // Construir cabecera WESYS de 16 bytes
            byte[] result = new byte[16 + compressedData.Length];
            Buffer.BlockCopy(WesysMagic, 0, result, 0, 8);
            WriteUInt32LE(result, 8, (uint)compressedData.Length);
            WriteUInt32LE(result, 12, (uint)payload.Length);
            Buffer.BlockCopy(compressedData, 0, result, 16, compressedData.Length);

            return result;
        }

        public static void SaveCompressed(string filePath, byte[] payload)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException("filePath");
            byte[] packed = Compress(payload);
            // Respaldo previo (B57) y escritura atómica: temporal + reemplazo,
            // como el resto del programa (DatabaseManager.AtomicWriteFile); un
            // fallo a mitad ya no deja el destino corrupto sin respaldo.
            if (File.Exists(filePath))
            {
                try { File.Copy(filePath, filePath + ".bak", true); }
                catch { }
            }
            DatabaseManager.AtomicWriteFile(filePath, packed);
        }

        private static uint ReadUInt32LE(byte[] buf, int offset)
        {
            return (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
        }

        private static void WriteUInt32LE(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static uint ComputeAdler32(byte[] data)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }
}
