// ============================================================
// ServerMap.cs — Editor de mapas de servidores de contenido de
// Sider (stadium-server map_teams.txt, kit-server map.txt).
// Punto B del cierre FL26, reimplementado en C# (licencia MIT,
// crédito a Matozanato / FL26 Mod Studio por el mecanismo de
// edición declarativa de mapas).
//
// Disciplina (la lección del sider.ini): el archivo se edita
// quirúrgicamente. Cada línea conserva su texto y su final
// (\r\n o \n); comentarios, líneas en blanco y líneas que no se
// entienden quedan byte a byte. El BOM se detecta y se conserva.
// Solo cambian las líneas de las asignaciones tocadas, y antes de
// escribir queda una copia .bak junto al mapa.
// ============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EditorLigasPes
{
    public class ServerDef
    {
        public string Id;          // "stadium" | "kit" | "ball" | "scoreboard" | "badge" | "referee" | "commentary"
        public string Nombre;      // "Estadios" | "Kits" | "Balones" | "Marcadores" | "Badges" | "Árbitros" | "Relatos"
        public string DirName;     // carpeta dentro de content
        public string MapFile;     // nombre del archivo de mapa
        public bool QuoteTargets;  // kit-server / badge-server escriben el destino entre comillas
        public string KeyKind;     // "team" | "comp"

        public static ServerDef Stadium()
        {
            return new ServerDef { Id = "stadium", Nombre = "Estadios", DirName = "stadium-server", MapFile = "map_teams.txt", QuoteTargets = false, KeyKind = "team" };
        }
        public static ServerDef Kit()
        {
            return new ServerDef { Id = "kit", Nombre = "Kits", DirName = "kit-server", MapFile = "map.txt", QuoteTargets = true, KeyKind = "team" };
        }
        public static ServerDef Ball()
        {
            return new ServerDef { Id = "ball", Nombre = "Balones", DirName = "ball-server", MapFile = "map_competitions.txt", QuoteTargets = false, KeyKind = "comp" };
        }
        public static ServerDef Scoreboard()
        {
            return new ServerDef { Id = "scoreboard", Nombre = "Marcadores", DirName = "scoreboard-server", MapFile = "map_competitions.txt", QuoteTargets = false, KeyKind = "comp" };
        }
        public static ServerDef Badge()
        {
            return new ServerDef { Id = "badge", Nombre = "Badges", DirName = "badge-server", MapFile = "map.txt", QuoteTargets = true, KeyKind = "comp" };
        }
        public static ServerDef RefereeKit()
        {
            return new ServerDef { Id = "referee", Nombre = "Árbitros", DirName = "referee_kit-server", MapFile = "map_competitions.txt", QuoteTargets = false, KeyKind = "comp" };
        }
        public static ServerDef Commentary()
        {
            return new ServerDef { Id = "commentary", Nombre = "Relatos", DirName = "commentary-server", MapFile = "map_competitions.txt", QuoteTargets = false, KeyKind = "comp" };
        }

        public static ServerDef ById(string id)
        {
            switch (id != null ? id.ToLowerInvariant() : "")
            {
                case "kit": return Kit();
                case "ball": return Ball();
                case "scoreboard": return Scoreboard();
                case "badge": return Badge();
                case "referee": return RefereeKit();
                case "commentary": return Commentary();
                default: return Stadium();
            }
        }
    }

    public class MapEntry
    {
        public int TeamId;
        public int EntityId { get { return TeamId; } set { TeamId = value; } }
        public string Target;
        public int LineIndex; // índice de la línea en el documento
    }

    public class ServerMapDoc
    {
        public string Path;
        public bool Exists;
        public bool HadBom;
        public string NewLine = "\r\n"; // dominante, para líneas nuevas
        private readonly List<string> _lines = new List<string>();   // contenido sin terminador
        private readonly List<string> _terms = new List<string>();   // terminador de cada línea

        private static readonly Regex AssignRx = new Regex(@"^\s*(\d+)\s*,\s*(.+?)\s*$");

        public static string ServerDir(AppConfig cfg, ServerDef def)
        {
            string content = cfg == null ? "" : (cfg.ContentFolder ?? "");
            if (string.IsNullOrWhiteSpace(content) && cfg != null && !string.IsNullOrWhiteSpace(cfg.SiderFolder))
                content = System.IO.Path.Combine(cfg.SiderFolder, "content");
            if (string.IsNullOrWhiteSpace(content)) return "";
            return System.IO.Path.Combine(content, def.DirName);
        }

        public static string MapPath(AppConfig cfg, ServerDef def)
        {
            string dir = ServerDir(cfg, def);
            return dir.Length == 0 ? "" : System.IO.Path.Combine(dir, def.MapFile);
        }

        public static ServerMapDoc Load(string path)
        {
            var doc = new ServerMapDoc { Path = path };
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { doc.Exists = false; return doc; }
            doc.Exists = true;
            byte[] bytes = File.ReadAllBytes(path);
            int offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { doc.HadBom = true; offset = 3; }
            string text = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
            int crlf = 0, lf = 0;
            int i = 0;
            while (i < text.Length)
            {
                int nl = text.IndexOf('\n', i);
                if (nl < 0)
                {
                    doc._lines.Add(text.Substring(i));
                    doc._terms.Add("");
                    break;
                }
                string term = (nl > i && text[nl - 1] == '\r') ? "\r\n" : "\n";
                int len = nl - i - (term == "\r\n" ? 1 : 0);
                doc._lines.Add(text.Substring(i, len));
                doc._terms.Add(term);
                if (term == "\r\n") crlf++; else lf++;
                i = nl + 1;
            }
            doc.NewLine = lf > crlf ? "\n" : "\r\n";
            return doc;
        }

        public List<MapEntry> Entries()
        {
            var list = new List<MapEntry>();
            for (int idx = 0; idx < _lines.Count; idx++)
            {
                var m = AssignRx.Match(_lines[idx]);
                if (!m.Success) continue;
                string raw = m.Groups[2].Value.Trim();
                if (raw.StartsWith("#") || raw.StartsWith(";")) continue;
                string target = raw.Trim().Trim('"');
                int id;
                if (!int.TryParse(m.Groups[1].Value, out id)) continue;
                list.Add(new MapEntry { TeamId = id, Target = target, LineIndex = idx });
            }
            return list;
        }

        public List<int> DuplicateTeamIds()
        {
            return Entries().GroupBy(e => e.TeamId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        }

        private string FormatLine(ServerDef def, int teamId, string target)
        {
            string t = def.QuoteTargets ? "\"" + target + "\"" : target;
            return teamId + ", " + t;
        }

        /// <summary>Crea o actualiza la asignación de un equipo. Devuelve true si agregó una línea nueva.</summary>
        public bool Set(ServerDef def, int teamId, string target)
        {
            var existing = Entries().FirstOrDefault(e => e.TeamId == teamId);
            if (existing != null)
            {
                _lines[existing.LineIndex] = FormatLine(def, teamId, target);
                return false;
            }
            // Agregar al final: la última línea debe terminar antes.
            if (_lines.Count > 0 && _terms[_terms.Count - 1] == "")
                _terms[_terms.Count - 1] = NewLine;
            _lines.Add(FormatLine(def, teamId, target));
            _terms.Add(NewLine);
            return true;
        }

        /// <summary>Quita todas las asignaciones de un equipo. Devuelve cuántas líneas sacó.</summary>
        public int Remove(int teamId)
        {
            var idxs = Entries().Where(e => e.TeamId == teamId).Select(e => e.LineIndex).OrderByDescending(x => x).ToList();
            foreach (int idx in idxs)
            {
                _lines.RemoveAt(idx);
                _terms.RemoveAt(idx);
            }
            return idxs.Count;
        }

        public string BuildText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _lines.Count; i++)
            {
                sb.Append(_lines[i]);
                sb.Append(_terms[i]);
            }
            return sb.ToString();
        }

        /// <summary>Escribe el documento: .bak previo y BOM conservado.</summary>
        public void Save()
        {
            if (File.Exists(Path))
                File.Copy(Path, Path + ".bak", true);
            string text = BuildText();
            byte[] body = new UTF8Encoding(false).GetBytes(text);
            using (var fs = new FileStream(Path, FileMode.Create, FileAccess.Write))
            {
                if (HadBom)
                {
                    byte[] bom = { 0xEF, 0xBB, 0xBF };
                    fs.Write(bom, 0, bom.Length);
                }
                fs.Write(body, 0, body.Length);
            }
            Exists = true;
        }

        /// <summary>Carpetas destino disponibles en el directorio del servidor (escaneo recursivo hasta 3 niveles).</summary>
        public static List<string> ListTargets(string serverDir)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(serverDir) || !Directory.Exists(serverDir)) return list;
            CollectDirs(serverDir, serverDir, 1, 3, list);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static void CollectDirs(string root, string current, int depth, int maxDepth, List<string> list)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(current))
                {
                    string name = System.IO.Path.GetFileName(d);
                    if (name.StartsWith(".") || name.Equals("common", StringComparison.OrdinalIgnoreCase) || name.Equals("asset", StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = d.Substring(root.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                    list.Add(rel);
                    if (depth < maxDepth)
                    {
                        CollectDirs(root, d, depth + 1, maxDepth, list);
                    }
                }
            }
            catch { }
        }
    }
}
