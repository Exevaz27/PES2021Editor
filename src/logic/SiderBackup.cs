// ============================================================================
//  SiderBackup.cs  -  Fase 2: seguridad del ecosistema Sider
// ----------------------------------------------------------------------------
//  Puntos de restauracion (sider.ini exacto + carpeta modules completa +
//  los 11 .bin del paquete livecpk, con indice y retencion) y perfiles de
//  modulos de sider.ini (con el perfil protegido "Como estaba").
//
//  Idea y modelo tomados de backups.py / factory.py / profiles.py de
//  FL26 Mod Studio (Matozanato, licencia MIT), reimplementados en C#
//  para PES 2021 sobre SiderIniManager y AppConfig.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace EditorLigasPes
{
    public class RestorePointInfo
    {
        public string Id;
        public DateTime Fecha;
        public string Motivo;
        public bool Automatico;
        public string Veredicto;
        public long TamanoBytes;
    }

    public class SiderProfile
    {
        public string Nombre;
        public Dictionary<string, bool> Modulos = new Dictionary<string, bool>();
        public bool EsComoEstaba;
    }

    public static class SiderBackup
    {
        public const string RootName = "editorpro-respaldos";
        public const string ComoEstabaNombre = "Como estaba";
        private const int MaxAutomaticos = 15;

        // Los 11 .bin del paquete (misma lista que el chequeo P2).
        private static readonly string[] SyncBins = new[]
        {
            "Competition.bin", "CompetitionRegulation.bin", "CompetitionEntry.bin",
            "Team.bin", "Player.bin", "PlayerAssignment.bin", "Tactics.bin",
            "TacticsFormation.bin", "Coach.bin", "Stadium.bin", "Derby.bin"
        };

        public static string RootDir(AppConfig cfg)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SiderFolder)) return null;
            return Path.Combine(cfg.SiderFolder, RootName);
        }

        private static string PackageDir(AppConfig cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.LiveCpkFolder))
            {
                string p = Path.Combine(cfg.LiveCpkFolder, "PruebaLigas", "common", "etc", "pesdb");
                if (Directory.Exists(p)) return p;
            }
            return cfg.PesdbFolder;
        }

        private static string Slug(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "punto";
            var chars = s.ToLowerInvariant().Select(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '-').ToArray();
            string slug = new string(chars).Trim('-');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            if (slug.Length > 28) slug = slug.Substring(0, 28).Trim('-');
            return slug.Length == 0 ? "punto" : slug;
        }

        // ---------------- Puntos de restauracion ----------------

        public static RestorePointInfo Create(AppConfig cfg, string motivo, bool automatico, string veredicto)
        {
            string root = RootDir(cfg);
            if (root == null || !Directory.Exists(cfg.SiderFolder)) return null;
            Directory.CreateDirectory(root);

            string id = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Slug(motivo);
            string dir = Path.Combine(root, id);
            int n = 2;
            while (Directory.Exists(dir)) { dir = Path.Combine(root, id + "-" + n); n++; }
            id = Path.GetFileName(dir);
            Directory.CreateDirectory(dir);

            // sider.ini exacto
            string ini = Path.Combine(cfg.SiderFolder, "sider.ini");
            if (File.Exists(ini)) File.Copy(ini, Path.Combine(dir, "sider.ini"), true);

            // modules completo
            if (!string.IsNullOrWhiteSpace(cfg.ModulesFolder) && Directory.Exists(cfg.ModulesFolder))
                CopyDir(cfg.ModulesFolder, Path.Combine(dir, "modules"));

           // paquete (los 11 .bin)
            string pkg = PackageDir(cfg);
            if (!string.IsNullOrWhiteSpace(pkg) && Directory.Exists(pkg))
            {
                string pd = Path.Combine(dir, "paquete");
                Directory.CreateDirectory(pd);
                foreach (string b in SyncBins)
                {
                    string src = Path.Combine(pkg, b);
                    if (File.Exists(src)) File.Copy(src, Path.Combine(pd, b), true);
                }
            }

            // mapas de servidores de contenido (Punto E): entran en la foto,
            // los siete servidores que conoce ServerDef en esta version.
            try
            {
                foreach (var def in new[] { ServerDef.Stadium(), ServerDef.Kit(), ServerDef.Ball(), ServerDef.Scoreboard(), ServerDef.Badge(), ServerDef.RefereeKit(), ServerDef.Commentary() })
                {
                    string srcMap = ServerMapDoc.MapPath(cfg, def);
                    if (!string.IsNullOrWhiteSpace(srcMap) && File.Exists(srcMap))
                    {
                        string md = Path.Combine(dir, "mapas", def.DirName);
                        Directory.CreateDirectory(md);
                        File.Copy(srcMap, Path.Combine(md, def.MapFile), true);
                    }
                }
            }
            catch { }
            var info = new RestorePointInfo
            {
                Id = id,
                Fecha = DateTime.Now,
                Motivo = motivo ?? "",
                Automatico = automatico,
                Veredicto = veredicto ?? "",
                TamanoBytes = DirSize(dir)
            };
            WritePointMeta(dir, info);
            WriteIndex(cfg);
            if (automatico) ApplyRetention(cfg);
            return info;
        }

        public static List<RestorePointInfo> List(AppConfig cfg)
        {
            var list = new List<RestorePointInfo>();
            string root = RootDir(cfg);
            if (root == null || !Directory.Exists(root)) return list;
            foreach (string dir in Directory.GetDirectories(root))
            {
                var info = ReadPointMeta(dir);
                if (info == null)
                {
                    info = new RestorePointInfo { Id = Path.GetFileName(dir), Fecha = Directory.GetCreationTime(dir), Motivo = "", Veredicto = "" };
                }
                info.TamanoBytes = DirSize(dir);
                list.Add(info);
            }
            return list.OrderByDescending(x => x.Fecha).ToList();
        }

        // Devuelve null si restauro bien; mensaje de error si no.
        public static string Restore(AppConfig cfg, string id)
        {
            string root = RootDir(cfg);
            if (root == null) return "Sin carpeta de Sider configurada.";
            string dir = Path.Combine(root, id ?? "");
            if (!Directory.Exists(dir)) return "El punto de restauracion ya no existe.";

            // sider.ini: bytes exactos del punto
            string savedIni = Path.Combine(dir, "sider.ini");
            if (File.Exists(savedIni))
                File.Copy(savedIni, Path.Combine(cfg.SiderFolder, "sider.ini"), true);

            // modules: los .lua vuelven a ser los del punto (los agregados
            // despues se borran); otros archivos (dll y demas) no se tocan.
            string savedMods = Path.Combine(dir, "modules");
            if (Directory.Exists(savedMods) && !string.IsNullOrWhiteSpace(cfg.ModulesFolder))
            {
                Directory.CreateDirectory(cfg.ModulesFolder);
                var savedNames = new HashSet<string>(
                    Directory.GetFiles(savedMods, "*", SearchOption.AllDirectories)
                        .Select(f => f.Substring(savedMods.Length).TrimStart('\\', '/').ToLowerInvariant()));
                foreach (string cur in Directory.GetFiles(cfg.ModulesFolder, "*.lua", SearchOption.AllDirectories))
                {
                    string rel = cur.Substring(cfg.ModulesFolder.Length).TrimStart('\\', '/').ToLowerInvariant();
                    if (!savedNames.Contains(rel)) { try { File.Delete(cur); } catch { } }
                }
                CopyDir(savedMods, cfg.ModulesFolder);
            }

            // mapas de servidores: vuelven los del punto (Punto E)
            try
            {
                string savedMaps = Path.Combine(dir, "mapas");
                if (Directory.Exists(savedMaps))
                {
                    foreach (var def in new[] { ServerDef.Stadium(), ServerDef.Kit(), ServerDef.Ball(), ServerDef.Scoreboard(), ServerDef.Badge(), ServerDef.RefereeKit(), ServerDef.Commentary() })
                    {
                        string srcMap = Path.Combine(savedMaps, def.DirName, def.MapFile);
                        string dstMap = ServerMapDoc.MapPath(cfg, def);
                        if (File.Exists(srcMap) && !string.IsNullOrWhiteSpace(dstMap))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dstMap));
                            File.Copy(srcMap, dstMap, true);
                        }
                    }
                }
            }
            catch { }
             // paquete
            string savedPkg = Path.Combine(dir, "paquete");
            string pkg = PackageDir(cfg);
            if (Directory.Exists(savedPkg) && !string.IsNullOrWhiteSpace(pkg))
            {
                Directory.CreateDirectory(pkg);
                foreach (string f in Directory.GetFiles(savedPkg, "*.bin"))
                    File.Copy(f, Path.Combine(pkg, Path.GetFileName(f)), true);
            }
            return null;
        }

        private static void ApplyRetention(AppConfig cfg)
        {
            try
            {
                var autos = List(cfg).Where(x => x.Automatico).OrderByDescending(x => x.Fecha).ToList();
                string root = RootDir(cfg);
                for (int i = MaxAutomaticos; i < autos.Count; i++)
                {
                    string dir = Path.Combine(root, autos[i].Id);
                    try { Directory.Delete(dir, true); } catch { }
                }
                WriteIndex(cfg);
            }
            catch { }
        }

        private static void WritePointMeta(string dir, RestorePointInfo info)
        {
            try
            {
                var d = new Dictionary<string, object>
                {
                    { "id", info.Id }, { "fecha", info.Fecha.ToString("o") },
                    { "motivo", info.Motivo }, { "automatico", info.Automatico },
                    { "veredicto", info.Veredicto }
                };
                File.WriteAllText(Path.Combine(dir, "punto.json"), new JavaScriptSerializer().Serialize(d));
            }
            catch { }
        }

        private static RestorePointInfo ReadPointMeta(string dir)
        {
            try
            {
                string meta = Path.Combine(dir, "punto.json");
                if (!File.Exists(meta)) return null;
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(meta));
                DateTime fecha;
                if (!DateTime.TryParse(Convert.ToString(d.ContainsKey("fecha") ? d["fecha"] : ""), out fecha)) fecha = Directory.GetCreationTime(dir);
                return new RestorePointInfo
                {
                    Id = Path.GetFileName(dir),
                    Fecha = fecha,
                    Motivo = d.ContainsKey("motivo") ? Convert.ToString(d["motivo"]) : "",
                    Automatico = d.ContainsKey("automatico") && Convert.ToBoolean(d["automatico"]),
                    Veredicto = d.ContainsKey("veredicto") ? Convert.ToString(d["veredicto"]) : ""
                };
            }
            catch { return null; }
        }

        private static void WriteIndex(AppConfig cfg)
        {
            string root = RootDir(cfg);
            if (root == null) return;
            try
            {
                Directory.CreateDirectory(root);
                var items = List(cfg).Select(x => new Dictionary<string, object>
                {
                    { "id", x.Id }, { "fecha", x.Fecha.ToString("o") }, { "motivo", x.Motivo },
                    { "automatico", x.Automatico }, { "veredicto", x.Veredicto }, { "tamano", x.TamanoBytes }
                }).ToList();
                File.WriteAllText(Path.Combine(root, "indice.json"), new JavaScriptSerializer().Serialize(items));
            }
            catch { }
        }

        // ---------------- Perfiles ----------------

        private static string ProfilesPath(AppConfig cfg)
        {
            string root = RootDir(cfg);
            return root == null ? null : Path.Combine(root, "perfiles.json");
        }

        public static List<SiderProfile> LoadProfiles(AppConfig cfg)
        {
            var list = new List<SiderProfile>();
            string path = ProfilesPath(cfg);
            if (path == null || !File.Exists(path)) return list;
            try
            {
                var arr = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(path));
                foreach (var d in arr)
                {
                    var p = new SiderProfile
                    {
                        Nombre = d.ContainsKey("nombre") ? Convert.ToString(d["nombre"]) : "",
                        EsComoEstaba = d.ContainsKey("esComoEstaba") && Convert.ToBoolean(d["esComoEstaba"])
                    };
                    if (d.ContainsKey("modulos") && d["modulos"] is Dictionary<string, object>)
                    {
                        foreach (var kv in (Dictionary<string, object>)d["modulos"])
                            p.Modulos[kv.Key] = Convert.ToBoolean(kv.Value);
                    }
                    list.Add(p);
                }
            }
            catch { }
            return list;
        }

        public static void SaveProfiles(AppConfig cfg, List<SiderProfile> profiles)
        {
            string path = ProfilesPath(cfg);
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var arr = profiles.Select(p => new Dictionary<string, object>
            {
                { "nombre", p.Nombre }, { "esComoEstaba", p.EsComoEstaba },
                { "modulos", p.Modulos.ToDictionary(kv => kv.Key, kv => (object)kv.Value) }
            }).ToList();
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(arr));
        }

        public static SiderProfile CaptureProfile(AppConfig cfg, string nombre, bool esComoEstaba)
        {
            var p = new SiderProfile { Nombre = nombre, EsComoEstaba = esComoEstaba };
            foreach (var m in SiderIniManager.GetModules(cfg.SiderFolder))
                p.Modulos[m.Name] = m.IsActive;
            return p;
        }

        // Captura el "Como estaba" la primera vez; no lo toca despues.
        public static void EnsureComoEstaba(AppConfig cfg)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cfg.SiderFolder) || !Directory.Exists(cfg.SiderFolder)) return;
                var profiles = LoadProfiles(cfg);
                if (profiles.Any(x => x.EsComoEstaba)) return;
                profiles.Insert(0, CaptureProfile(cfg, ComoEstabaNombre, true));
                SaveProfiles(cfg, profiles);
            }
            catch { }
        }

        // Devuelve null si aplico bien; mensaje de error si no.
        public static string ApplyProfile(AppConfig cfg, SiderProfile profile)
        {
            if (profile == null) return "Perfil inexistente.";
            foreach (var kv in profile.Modulos)
            {
                try { SiderIniManager.SetModuleActive(cfg.SiderFolder, kv.Key, kv.Value); }
                catch (Exception ex) { return "No se pudo ajustar " + kv.Key + ": " + ex.Message; }
            }
            return null;
        }

        // ---------------- Utilidades ----------------

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(src.Length).TrimStart('\\', '/');
                string target = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(f, target, true);
            }
        }

        private static long DirSize(string dir)
        {
            long total = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total;
        }
    }
}
