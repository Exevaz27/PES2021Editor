using System;
using System.IO;
using System.Text;

namespace EditorLigasPes
{
    /// <summary>
    /// Configuración del asistente de inicio del editor.
    /// Se guarda en "editor_config.json" junto al ejecutable.
    /// El JSON se parsea y se escribe a mano (sin librerías externas):
    /// un objeto plano con pares "clave": "valor" de tipo string.
    /// </summary>
    public class AppConfig
    {
        public string GameFolder { get; set; }
        public string SiderFolder { get; set; }
        public string LiveCpkFolder { get; set; }
        public string ModulesFolder { get; set; }
        public string ContentFolder { get; set; }
        public string PesdbFolder { get; set; }
        public string EditFile { get; set; }
        public string EnableUefaSwissFormat { get; set; }
        public string MinifaceFolder { get; set; }

        public static string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "editor_config.json");

        /// <summary>
        /// Obtiene o detecta la ruta estándar de la carpeta de minifaces dentro del Sider.
        /// Si la carpeta de Sider existe pero la de minifaces no, la inicializa automáticamente.
        /// </summary>
        public string GetOrDetectMinifaceFolder(bool createIfMissing = true)
        {
            if (!string.IsNullOrWhiteSpace(MinifaceFolder) && Directory.Exists(MinifaceFolder))
            {
                return MinifaceFolder;
            }

            string baseFolder = !string.IsNullOrWhiteSpace(ContentFolder) ? ContentFolder :
                               (!string.IsNullOrWhiteSpace(SiderFolder) ? Path.Combine(SiderFolder, "content") : null);

            if (!string.IsNullOrEmpty(baseFolder))
            {
                string[] candidates = new string[]
                {
                    Path.Combine(baseFolder, "miniface-server"),
                    Path.Combine(baseFolder, "miniface_server", "MiniFaces"),
                    Path.Combine(baseFolder, "minifaces"),
                    Path.Combine(baseFolder, "miniface_server")
                };

                foreach (var c in candidates)
                {
                    if (Directory.Exists(c)) return c;
                }

                // Estándar de Sider: content\miniface-server
                string defaultPath = Path.Combine(baseFolder, "miniface-server");
                if (createIfMissing && Directory.Exists(baseFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(defaultPath);
                        Directory.CreateDirectory(Path.Combine(defaultPath, "minifaces"));
                        string readme = Path.Combine(defaultPath, "LEEME_MINIFACES.txt");
                        if (!File.Exists(readme))
                        {
                            File.WriteAllText(readme,
                                "PES 2021 EDITOR PRO - CARPETA DE MINIFACES (SIDER)\r\n" +
                                "===================================================\r\n" +
                                "Esta carpeta esta vinculada y reconocida automaticamente por el editor.\r\n\r\n" +
                                "Coloca aqui las fotos de los futbolistas con su ID de jugador:\r\n" +
                                "Ejemplo: 7511.dds (o 7511.png), 38249.dds, etc.\r\n" +
                                "Formatos aceptados: .dds (DXT1/DXT3/DXT5/ARGB), .png, .jpg.\r\n" +
                                "Podes organizarlas en subcarpetas o colocarlas sueltas; el editor las detecta todas.\r\n",
                                Encoding.UTF8);
                        }
                    }
                    catch { }
                }
                return defaultPath;
            }

            return null;
        }

        /// <summary>
        /// True si las carpetas imprescindibles están configuradas.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                return !string.IsNullOrWhiteSpace(GameFolder)
                    && !string.IsNullOrWhiteSpace(SiderFolder)
                    && !string.IsNullOrWhiteSpace(PesdbFolder);
            }
        }

        /// <summary>
        /// Carga la configuración. Si el archivo no existe, está vacío o
        /// está corrupto, devuelve una instancia vacía (nunca lanza).
        /// </summary>
        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return new AppConfig();
                }

                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                return Parse(json);
            }
            catch
            {
                return new AppConfig();
            }
        }

        /// <summary>
        /// Guarda la configuración en JSON plano, de forma atómica:
        /// escribe a un archivo temporal y luego lo mueve/reemplaza
        /// sobre el destino, para no dejar nunca el archivo a medias.
        /// </summary>
        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            sb.Append("  \"GameFolder\": \"").Append(Escape(GameFolder)).Append("\",\r\n");
            sb.Append("  \"SiderFolder\": \"").Append(Escape(SiderFolder)).Append("\",\r\n");
            sb.Append("  \"LiveCpkFolder\": \"").Append(Escape(LiveCpkFolder)).Append("\",\r\n");
            sb.Append("  \"ModulesFolder\": \"").Append(Escape(ModulesFolder)).Append("\",\r\n");
            sb.Append("  \"ContentFolder\": \"").Append(Escape(ContentFolder)).Append("\",\r\n");
            sb.Append("  \"PesdbFolder\": \"").Append(Escape(PesdbFolder)).Append("\",\r\n");
            sb.Append("  \"EditFile\": \"").Append(Escape(EditFile)).Append("\",\r\n");
            sb.Append("  \"EnableUefaSwissFormat\": \"").Append(Escape(EnableUefaSwissFormat ?? "0")).Append("\",\r\n");
            sb.Append("  \"MinifaceFolder\": \"").Append(Escape(MinifaceFolder)).Append("\"\r\n");
            sb.Append("}");

            string tempPath = ConfigPath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(ConfigPath))
                {
                    File.Replace(tempPath, ConfigPath, null);
                }
                else
                {
                    File.Move(tempPath, ConfigPath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }
                throw;
            }
        }

        private static AppConfig Parse(string json)
        {
            AppConfig config = new AppConfig();
            if (string.IsNullOrWhiteSpace(json))
            {
                return config;
            }

            int pos = 0;
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != '{')
            {
                return config;
            }
            pos++;

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length)
                {
                    break;
                }
                if (json[pos] == '}')
                {
                    break;
                }
                if (json[pos] != '"')
                {
                    break;
                }

                string key = ParseString(json, ref pos);
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length || json[pos] != ':')
                {
                    break;
                }
                pos++;

                SkipWhitespace(json, ref pos);
                string value = null;
                if (pos < json.Length && json[pos] == '"')
                {
                    value = ParseString(json, ref pos);
                }
                else
                {
                    // Valor no string (número, booleano, null u otro):
                    // se ignora y se avanza hasta ',' o '}'.
                    while (pos < json.Length && json[pos] != ',' && json[pos] != '}')
                    {
                        pos++;
                    }
                }

                if (value != null)
                {
                    if (key == "GameFolder")
                    {
                        config.GameFolder = value;
                    }
                    else if (key == "SiderFolder")
                    {
                        config.SiderFolder = value;
                    }
                    else if (key == "LiveCpkFolder")
                    {
                        config.LiveCpkFolder = value;
                    }
                    else if (key == "ModulesFolder")
                    {
                        config.ModulesFolder = value;
                    }
                    else if (key == "ContentFolder")
                    {
                        config.ContentFolder = value;
                    }
                    else if (key == "PesdbFolder")
                    {
                        config.PesdbFolder = value;
                    }
                    else if (key == "EditFile")
                    {
                        config.EditFile = value;
                    }
                    else if (key == "EnableUefaSwissFormat")
                    {
                        config.EnableUefaSwissFormat = value;
                    }
                    else if (key == "MinifaceFolder")
                    {
                        config.MinifaceFolder = value;
                    }
                    // Claves desconocidas: se ignoran.
                }

                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                    continue;
                }
                break;
            }

            return config;
        }

        private static void SkipWhitespace(string json, ref int pos)
        {
            while (pos < json.Length && char.IsWhiteSpace(json[pos]))
            {
                pos++;
            }
        }

        // Parsea una cadena JSON entre comillas. Al entrar, json[pos] == '"'.
        // Al salir, pos queda justo después de la comilla de cierre.
        private static string ParseString(string json, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            pos++; // comilla de apertura
            while (pos < json.Length)
            {
                char c = json[pos];
                pos++;
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c == '\\' && pos < json.Length)
                {
                    char esc = json[pos];
                    pos++;
                    if (esc == '"')
                    {
                        sb.Append('"');
                    }
                    else if (esc == '\\')
                    {
                        sb.Append('\\');
                    }
                    else if (esc == '/')
                    {
                        sb.Append('/');
                    }
                    else if (esc == 'n')
                    {
                        sb.Append('\n');
                    }
                    else if (esc == 'r')
                    {
                        sb.Append('\r');
                    }
                    else if (esc == 't')
                    {
                        sb.Append('\t');
                    }
                    else if (esc == 'b')
                    {
                        sb.Append('\b');
                    }
                    else if (esc == 'f')
                    {
                        sb.Append('\f');
                    }
                    else if (esc == 'u' && pos + 4 <= json.Length)
                    {
                        int code = 0;
                        bool ok = true;
                        for (int i = 0; i < 4; i++)
                        {
                            int digit = HexDigit(json[pos + i]);
                            if (digit < 0)
                            {
                                ok = false;
                                break;
                            }
                            code = code * 16 + digit;
                        }
                        if (ok)
                        {
                            sb.Append((char)code);
                            pos += 4;
                        }
                    }
                    else
                    {
                        // Secuencia de escape desconocida: se conserva el carácter.
                        sb.Append(esc);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }
            return -1;
        }

        private static string Escape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    sb.Append("\\\\");
                }
                else if (c == '"')
                {
                    sb.Append("\\\"");
                }
                else if (c == '\n')
                {
                    sb.Append("\\n");
                }
                else if (c == '\r')
                {
                    sb.Append("\\r");
                }
                else if (c == '\t')
                {
                    sb.Append("\\t");
                }
                else if (c == '\b')
                {
                    sb.Append("\\b");
                }
                else if (c == '\f')
                {
                    sb.Append("\\f");
                }
                else if (c < 0x20)
                {
                    sb.Append("\\u");
                    sb.Append(((int)c).ToString("x4"));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
