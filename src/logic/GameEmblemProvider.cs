using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

namespace EditorLigasPes
{
    public class GameEmblemItem
    {
        public int CompetitionId { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
        public CpkFileEntry Entry { get; set; }
        public Image CachedImage { get; set; }

        public override string ToString()
        {
            return string.Format("[ID: {0:D2}] {1}", CompetitionId, Name);
        }
    }

    public static class GameEmblemProvider
    {
        private static readonly Regex CompIdRegex = new Regex(@"^emb_(\d{4})_l\.png$", RegexOptions.IgnoreCase);

        public static string FindDt15CpkPath(string dbPath)
        {
            // 1. Si la base de datos esta en Sider livecpk
            if (!string.IsNullOrEmpty(dbPath))
            {
                // Si dbPath es un archivo (p. ej. la ruta de la BD), se parte de
                // su carpeta: tratarlo como carpeta hacía subir desde un punto
                // inexistente y podía resolver en otra instalación (M96).
                string dir = File.Exists(dbPath) ? Path.GetDirectoryName(dbPath) : dbPath;
                while (!string.IsNullOrEmpty(dir))
                {
                    string candidate = Path.Combine(dir, "Data", "dt15_x64.cpk");
                    if (File.Exists(candidate)) return candidate;

                    string candidateUpper = Path.Combine(dir, "data", "dt15_x64.cpk");
                    if (File.Exists(candidateUpper)) return candidateUpper;

                    var parent = Directory.GetParent(dir);
                    if (parent == null || parent.FullName == dir) break;
                    dir = parent.FullName;
                }
            }

            // (Las rutas absolutas "conocidas" se eliminaron: el dt15 se busca
            // solo subiendo desde la base configurada, paso 1.)
            return null;
        }

        public static List<GameEmblemItem> LoadGameCompetitionEmblems(string cpkPath, DatabaseManager db)
        {
            var list = new List<GameEmblemItem>();
            if (string.IsNullOrEmpty(cpkPath) || !File.Exists(cpkPath))
                return list;

            var entries = CpkExtractor.GetTocEntries(cpkPath, (dir, name) =>
                dir.IndexOf("emblemLc", StringComparison.OrdinalIgnoreCase) >= 0 &&
                name.EndsWith("_l.png", StringComparison.OrdinalIgnoreCase));

            var seenIds = new HashSet<int>();
            foreach (var entry in entries)
            {
                var match = CompIdRegex.Match(entry.FileName);
                if (match.Success)
                {
                    int cid = int.Parse(match.Groups[1].Value);
                    if (!seenIds.Add(cid)) continue; // sin duplicados (M97)
                    string name = null;

                    if (db != null)
                    {
                        var reg = db.Regulations != null ? db.Regulations.Find(r => r.IdCompetition == cid) : null;
                        if (reg != null)
                        {
                            if (!string.IsNullOrEmpty(reg.NameSpanish)) name = reg.NameSpanish;
                            else if (!string.IsNullOrEmpty(reg.NameLatamSpanish)) name = reg.NameLatamSpanish;
                            else if (!string.IsNullOrEmpty(reg.NameEnglish)) name = reg.NameEnglish;
                            else if (!string.IsNullOrEmpty(reg.Name)) name = reg.Name;
                        }

                        if (string.IsNullOrEmpty(name) && db.Competitions != null)
                        {
                            var comp = db.Competitions.Find(c => c.Id == cid);
                            if (comp != null && !string.IsNullOrEmpty(comp.Name)) name = comp.Name;
                        }
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        name = GetFallbackCompetitionName(cid);
                    }

                    list.Add(new GameEmblemItem
                    {
                        CompetitionId = cid,
                        Name = name,
                        FileName = entry.FileName,
                        Entry = entry
                    });
                }
            }

            // Ordenar por ID ascendente
            list.Sort((a, b) => a.CompetitionId.CompareTo(b.CompetitionId));
            return list;
        }

        public static Image ExtractEmblemImage(string cpkPath, CpkFileEntry entry)
        {
            if (string.IsNullOrEmpty(cpkPath) || entry == null) return null;
            byte[] bytes = CpkExtractor.ExtractEntryBytes(cpkPath, entry);
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    using (var temp = Image.FromStream(ms))
                    {
                        return new Bitmap(temp);
                    }
                }
            }
            catch
            {
                // PNG corrupto o ilegible: null, no excepción (M97).
                return null;
            }
        }

        private static string GetFallbackCompetitionName(int id)
        {
            switch (id)
            {
                case 1: return "Copa Internacional";
                case 2: return "UEFA Champions League";
                case 3: return "UEFA Europa League";
                case 4: return "UEFA Super Cup";
                case 5: return "Copa Libertadores";
                case 6: return "AFC Champions League";
                case 17: return "English League (Premier League)";
                case 18: return "English 2nd Division (Championship)";
                case 19: return "Spanish League (La Liga)";
                case 20: return "Spanish 2nd Division (Segunda)";
                case 21: return "Italian League (Serie A)";
                case 22: return "Argentine League (Liga Profesional)";
                case 23: return "English FA Cup";
                case 24: return "French League (Ligue 1)";
                case 25: return "French 2nd Division (Ligue 2)";
                case 26: return "Dutch League (Eredivisie)";
                case 27: return "Portuguese League (Primeira Liga)";
                case 28: return "Brazilian League (Brasileirão Série A)";
                case 29: return "Brazilian 2nd Division (Série B)";
                case 30: return "Russian League (Premier Liga)";
                case 31: return "Turkish League (Süper Lig)";
                case 32: return "Belgian League (Pro League)";
                case 33: return "Danish League (Superliga)";
                case 34: return "Swiss League (Super League)";
                case 35: return "Scottish League (Premiership)";
                case 36: return "Argentine Cup (Copa Argentina)";
                case 37: return "Spanish Cup (Copa del Rey)";
                case 38: return "Italian Cup (Coppa Italia)";
                case 39: return "French Cup (Coupe de France)";
                case 40: return "German Cup (DFB-Pokal)";
                case 45: return "Copa Sudamericana";
                case 46: return "Recopa Sudamericana";
                default: return string.Format("Competición Oficial #{0:D2}", id);
            }
        }
    }
}
