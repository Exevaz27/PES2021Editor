using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EditorLigasPes
{
    /// <summary>
    /// Una entrada lua.module de sider.ini.
    /// </summary>
    public class SiderModuleEntry
    {
        /// <summary>Nombre del módulo, por ejemplo "league_manager.lua".</summary>
        public string Name { get; set; }

        /// <summary>True si la línea NO está comentada.</summary>
        public bool IsActive { get; set; }

        /// <summary>Línea original tal cual aparece en sider.ini.</summary>
        public string RawLine { get; set; }
    }

    /// <summary>
    /// Manejo de sider.ini (gestor de módulos del asistente de inicio).
    /// Reimplementación en C# de la idea de FL26 Mod Studio: una línea
    /// lua.module está ACTIVA si no empieza con ';' (ni '#') e INACTIVA
    /// si está comentada con ';' al inicio (tras espacios). Activar es
    /// quitar el ';' inicial y desactivar es agregarlo; el resto de la
    /// línea y el orden del archivo se preservan intactos.
    ///
    /// Las líneas cpk.root se ignoran siempre: no se listan ni se tocan.
    /// Todas las escrituras conservan los saltos de línea originales
    /// (CRLF o LF) y hacen backup previo a "sider.ini.bak".
    /// </summary>
    public static class SiderIniManager
    {
        /// <summary>
        /// Lee sider.ini y devuelve los módulos lua.module, sin duplicados
        /// (ante un nombre repetido se conserva la primera aparición).
        /// Si sider.ini no existe, devuelve una lista vacía.
        /// </summary>
        public static List<SiderModuleEntry> GetModules(string siderDir)
        {
            List<SiderModuleEntry> result = new List<SiderModuleEntry>();
            string iniPath = GetIniPath(siderDir);
            if (iniPath == null || !File.Exists(iniPath))
            {
                return result;
            }

            string[] lines;
            try
            {
                string raw = File.ReadAllText(iniPath, Encoding.UTF8);
                lines = SplitLines(raw);
            }
            catch (IOException)
            {
                return result;
            }
            catch (UnauthorizedAccessException)
            {
                return result;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                string name;
                bool isActive;
                if (!TryParseModuleLine(line, out name, out isActive))
                {
                    continue;
                }
                if (!seen.Add(name))
                {
                    continue;
                }
                result.Add(new SiderModuleEntry
                {
                    Name = name,
                    IsActive = isActive,
                    RawLine = line
                });
            }
            return result;
        }

        /// <summary>
        /// Activa o desactiva un módulo cambiando SOLO el prefijo ';' de
        /// su línea (comparación sin distinción de mayúsculas y con o sin
        /// ".lua"). Si no hay sider.ini, no hace nada y no crea archivos.
        /// Si el módulo ya está en el estado pedido, no reescribe nada.
        /// </summary>
        public static void SetModuleActive(string siderDir, string moduleName, bool active)
        {
            string iniPath = GetIniPath(siderDir);
            if (iniPath == null || !File.Exists(iniPath))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return;
            }

            string[] lines;
            string newline;
            bool endsWithNewline;
            if (!TryReadIni(iniPath, out lines, out newline, out endsWithNewline))
            {
                return;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string name;
                bool isActive;
                if (!TryParseModuleLine(lines[i], out name, out isActive))
                {
                    continue;
                }
                if (!NamesMatch(name, moduleName))
                {
                    continue;
                }
                if (isActive == active)
                {
                    return;
                }

                string line = lines[i];
                if (active)
                {
                    // Quitar el ';' (o '#') inicial, tras los espacios.
                    int idx = 0;
                    while (idx < line.Length && (line[idx] == ' ' || line[idx] == '\t'))
                    {
                        idx++;
                    }
                    if (idx >= line.Length || (line[idx] != ';' && line[idx] != '#'))
                    {
                        return;
                    }
                    lines[i] = line.Remove(idx, 1);
                }
                else
                {
                    // Agregar ';' al inicio de la línea.
                    lines[i] = ";" + line;
                }

                BackupIni(iniPath);
                WriteIni(iniPath, lines, newline, endsWithNewline);
                return;
            }
            // Módulo no encontrado: no se escribe nada.
        }

        /// <summary>
        /// Asegura "luajit.ext.enabled = 1" en sider.ini. Si ya está en 1
        /// no toca nada. Si está en otro valor lo reemplaza. Si no está,
        /// lo agrega tras la sección [sider] si existe, o al inicio si no.
        /// Devuelve un mensaje corto de qué hizo.
        /// </summary>
        public static string EnsureLuajitEnabled(string siderDir)
        {
            string iniPath = GetIniPath(siderDir);
            if (iniPath == null)
            {
                return "no se encontró sider.ini";
            }

            try
            {
                if (!File.Exists(iniPath))
                {
                    File.WriteAllText(iniPath, "[sider]\r\nluajit.ext.enabled = 1\r\n", new UTF8Encoding(false));
                    return "agregado";
                }

                string[] lines;
                string newline;
                bool endsWithNewline;
                if (!TryReadIni(iniPath, out lines, out newline, out endsWithNewline))
                {
                    return "no se pudo leer sider.ini";
                }

                // 1) Línea activa con la clave.
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '#')
                    {
                        continue;
                    }
                    string value;
                    if (!TryParseLuajitLine(trimmed, out value))
                    {
                        continue;
                    }
                    if (value == "1")
                    {
                        return "ya estaba en 1";
                    }
                    lines[i] = LeadingWhitespace(lines[i]) + "luajit.ext.enabled = 1";
                    BackupIni(iniPath);
                    WriteIni(iniPath, lines, newline, endsWithNewline);
                    return "cambiado de " + value + " a 1";
                }

                // 2) Línea comentada con la clave: se activa y se pone en 1
                // en lugar de agregar una segunda línea duplicada.
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.Length == 0 || (trimmed[0] != ';' && trimmed[0] != '#'))
                    {
                        continue;
                    }
                    string work = trimmed.Substring(1).TrimStart();
                    string value;
                    if (!TryParseLuajitLine(work, out value))
                    {
                        continue;
                    }
                    lines[i] = LeadingWhitespace(lines[i]) + "luajit.ext.enabled = 1";
                    BackupIni(iniPath);
                    WriteIni(iniPath, lines, newline, endsWithNewline);
                    return "cambiado de " + value + " a 1";
                }

                // 3) No está: agregar tras [sider] si existe, o al inicio.
                List<string> list = new List<string>(lines);
                int insertAt = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (IsSiderSection(list[i]))
                    {
                        insertAt = i + 1;
                        break;
                    }
                }
                if (insertAt >= 0)
                {
                    list.Insert(insertAt, "luajit.ext.enabled = 1");
                }
                else
                {
                    list.Insert(0, "luajit.ext.enabled = 1");
                }
                BackupIni(iniPath);
                WriteIni(iniPath, list.ToArray(), newline, true);
                return "agregado";
            }
            catch (IOException ex)
            {
                return "error al escribir sider.ini: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                return "error al escribir sider.ini: " + ex.Message;
            }
        }

        /// <summary>
        /// Si falta la línea lua.module para ese módulo, la agrega activa
        /// al final del archivo. Si ya existe (activa o inactiva) no la
        /// toca. Devuelve un mensaje corto de qué hizo.
        /// </summary>
        public static string EnsureModuleLine(string siderDir, string moduleName)
        {
            string iniPath = GetIniPath(siderDir);
            if (iniPath == null)
            {
                return "no se encontró sider.ini";
            }
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return "nombre de módulo vacío";
            }

            string wanted = moduleName.Trim();
            if (!wanted.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                wanted += ".lua";
            }

            try
            {
                if (!File.Exists(iniPath))
                {
                    File.WriteAllText(iniPath, "lua.module = \"" + wanted + "\"\r\n", new UTF8Encoding(false));
                    return "agregada";
                }

                string[] lines;
                string newline;
                bool endsWithNewline;
                if (!TryReadIni(iniPath, out lines, out newline, out endsWithNewline))
                {
                    return "no se pudo leer sider.ini";
                }

                foreach (string line in lines)
                {
                    string name;
                    bool isActive;
                    if (TryParseModuleLine(line, out name, out isActive) && NamesMatch(name, wanted))
                    {
                        return "ya existía";
                    }
                }

                List<string> list = new List<string>(lines);
                list.Add("lua.module = \"" + wanted + "\"");
                BackupIni(iniPath);
                WriteIni(iniPath, list.ToArray(), newline, true);
                return "agregada";
            }
            catch (IOException ex)
            {
                return "error al escribir sider.ini: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                return "error al escribir sider.ini: " + ex.Message;
            }
        }

        // ----- Utilidades internas -----

        private static string GetIniPath(string siderDir)
        {
            if (string.IsNullOrWhiteSpace(siderDir))
            {
                return null;
            }
            return Path.Combine(siderDir, "sider.ini");
        }

        // Parsea una línea lua.module. Devuelve false para cualquier otra
        // línea. name sale sin comillas y recortado; isActive es false si
        // la línea empieza con ';' o '#' (tras espacios).
        private static bool TryParseModuleLine(string line, out string name, out bool isActive)
        {
            name = null;
            isActive = true;
            if (line == null)
            {
                return false;
            }
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }
            string work = trimmed;
            if (work[0] == ';' || work[0] == '#')
            {
                isActive = false;
                work = work.Substring(1).TrimStart();
            }
            if (!work.StartsWith("lua.module", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            int eq = work.IndexOf('=');
            if (eq < 0)
            {
                return false;
            }
            string after = work.Substring(eq + 1).TrimStart();
            if (after.Length == 0)
            {
                return false;
            }
            if (after[0] == '"')
            {
                int close = after.IndexOf('"', 1);
                if (close < 0)
                {
                    return false;
                }
                name = after.Substring(1, close - 1);
            }
            else
            {
                int semi = after.IndexOf(';');
                string token = semi >= 0 ? after.Substring(0, semi) : after;
                name = token.Trim();
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            name = name.Trim();
            return true;
        }

        // Parsea una línea "luajit.ext.enabled = valor" ya sin comentario.
        // value sale como primer token tras el '=' (sin comentario final).
        private static bool TryParseLuajitLine(string work, out string value)
        {
            value = null;
            if (!work.StartsWith("luajit.ext.enabled", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            int eq = work.IndexOf('=');
            if (eq < 0)
            {
                return false;
            }
            string after = work.Substring(eq + 1).Trim();
            if (after.Length == 0)
            {
                return false;
            }
            int semi = after.IndexOf(';');
            if (semi >= 0)
            {
                after = after.Substring(0, semi).Trim();
            }
            int ws = after.IndexOfAny(new char[] { ' ', '\t' });
            if (ws >= 0)
            {
                after = after.Substring(0, ws);
            }
            if (after.Length == 0)
            {
                return false;
            }
            value = after;
            return true;
        }

        private static bool IsSiderSection(string line)
        {
            if (line == null)
            {
                return false;
            }
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("[sider]", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (trimmed.Length == 7)
            {
                return true;
            }
            char next = trimmed[7];
            return next == ' ' || next == '\t' || next == ';' || next == '#';
        }

        private static string LeadingWhitespace(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            {
                i++;
            }
            return line.Substring(0, i);
        }

        // Comparación de nombres de módulo: sin distinción de mayúsculas
        // y con o sin la extensión ".lua".
        private static bool NamesMatch(string a, string b)
        {
            return string.Equals(NormalizeModuleName(a), NormalizeModuleName(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeModuleName(string name)
        {
            if (name == null)
            {
                return string.Empty;
            }
            string n = name.Trim();
            if (n.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                n = n.Substring(0, n.Length - 4);
            }
            return n;
        }

        private static string[] SplitLines(string raw)
        {
            string normalized = raw.Replace("\r\n", "\n");
            string[] parts = normalized.Split('\n');
            // Si el texto termina en salto de línea, el último elemento
            // del Split es vacío y no es una línea real.
            if (normalized.EndsWith("\n") && parts.Length > 0 && parts[parts.Length - 1].Length == 0)
            {
                string[] trimmed = new string[parts.Length - 1];
                Array.Copy(parts, trimmed, parts.Length - 1);
                return trimmed;
            }
            return parts;
        }

        private static bool TryReadIni(string iniPath, out string[] lines, out string newline, out bool endsWithNewline)
        {
            lines = null;
            newline = "\n";
            endsWithNewline = false;
            try
            {
                string raw = File.ReadAllText(iniPath, Encoding.UTF8);
                newline = raw.Contains("\r\n") ? "\r\n" : "\n";
                string normalized = raw.Replace("\r\n", "\n");
                endsWithNewline = normalized.EndsWith("\n");
                if (raw.Length == 0)
                {
                    lines = new string[0];
                }
                else
                {
                    lines = SplitLines(raw);
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void WriteIni(string iniPath, string[] lines, string newline, bool endsWithNewline)
        {
            string text = string.Join(newline, lines);
            if (endsWithNewline)
            {
                text += newline;
            }
            File.WriteAllText(iniPath, text, new UTF8Encoding(false));
        }

        // Quita la marca BOM inicial si existe, dejando el resto del
        // archivo byte a byte identico. Devuelve true si habia BOM.
        public static bool StripBom(string siderDir)
        {
            string iniPath = Path.Combine(siderDir, "sider.ini");
            if (!File.Exists(iniPath)) return false;
            byte[] bytes = File.ReadAllBytes(iniPath);
            if (bytes.Length < 3 || bytes[0] != 0xEF || bytes[1] != 0xBB || bytes[2] != 0xBF) return false;
            BackupIni(iniPath);
            byte[] rest = new byte[bytes.Length - 3];
            Array.Copy(bytes, 3, rest, 0, rest.Length);
            File.WriteAllBytes(iniPath, rest);
            return true;
        }

        private static void BackupIni(string iniPath)
        {
            File.Copy(iniPath, iniPath + ".bak", true);
        }
    }
}
