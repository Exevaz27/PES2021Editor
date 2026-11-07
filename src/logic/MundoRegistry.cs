// ============================================================================
//  MundoRegistry.cs  -  Fase 3: registro de mundos PES 2021
// ----------------------------------------------------------------------------
//  Un "mundo" es una instalacion completa como unidad con nombre: su
//  pesdb (ligas/equipos/jugadores), su paquete livecpk, su Sider, su
//  EDIT y su carpeta de saves. Esta fase SOLO registra el mundo actual
//  (Mundo 1): no intercambia mundos ni cambia ningun comportamiento.
//
//  El registro vive en editorpro-respaldos\mundos.json (la carpeta de
//  la Fase 2, que sobrevive a los reemplazos de la app). El punto de
//  restauracion "ancla" del mundo lo crea SiderBackup.
//
//  Idea de mundos tomada de FL26 Mod Studio (Matozanato, MIT),
//  reimplementada para PES 2021.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace EditorLigasPes
{
    public class MundoLiga
    {
        public int CompId;
        public string Nombre;
        public int RegId;
        public int Equipos;
    }

    public class MundoInfo
    {
        public string Id = "mundo-1";
        public string Nombre = "Mundo 1";
        public DateTime Creado;
        public DateTime Actualizado;
        public string GameFolder = "";
        public string SiderFolder = "";
        public string PesdbFolder = "";
        public string PaqueteDir = "";
        public string EditFile = "";
        public string SavesFolder = "";
        public List<MundoLiga> Ligas = new List<MundoLiga>();
        public int TotalCompeticiones;
        public int TotalEquipos;
        public int TotalJugadores;
        public string Veredicto = "";
        public string PuntoAnclaId = "";
        public List<int> PresetCompIds = new List<int>();
        public bool Activo = true;
    }

    public static class MundoRegistry
    {
        public static string RegistryPath(AppConfig cfg)
        {
            string root = SiderBackup.RootDir(cfg);
            return root == null ? null : Path.Combine(root, "mundos.json");
        }

        public static string PackageDir(AppConfig cfg)
        {
            if (cfg == null) return "";
            if (!string.IsNullOrWhiteSpace(cfg.LiveCpkFolder))
            {
                string p = Path.Combine(cfg.LiveCpkFolder, "PruebaLigas", "common", "etc", "pesdb");
                if (Directory.Exists(p)) return p;
            }
            return cfg.PesdbFolder ?? "";
        }

        public static List<MundoInfo> Load(AppConfig cfg)
        {
            var list = new List<MundoInfo>();
            string path = RegistryPath(cfg);
            if (path == null || !File.Exists(path)) return list;
            try
            {
                var arr = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(path));
                foreach (var d in arr)
                {
                    var m = new MundoInfo
                    {
                        Id = GS(d, "id"),
                        Nombre = GS(d, "nombre"),
                        GameFolder = GS(d, "gameFolder"),
                        SiderFolder = GS(d, "siderFolder"),
                        PesdbFolder = GS(d, "pesdbFolder"),
                        PaqueteDir = GS(d, "paqueteDir"),
                        EditFile = GS(d, "editFile"),
                        SavesFolder = GS(d, "savesFolder"),
                        Veredicto = GS(d, "veredicto"),
                        PuntoAnclaId = GS(d, "puntoAnclaId"),
                        TotalCompeticiones = GI(d, "totalCompeticiones"),
                        TotalEquipos = GI(d, "totalEquipos"),
                        TotalJugadores = GI(d, "totalJugadores"),
                        Activo = d.ContainsKey("activo") && Convert.ToBoolean(d["activo"])
                    };
                    DateTime dt;
                    if (DateTime.TryParse(GS(d, "creado"), out dt)) m.Creado = dt;
                    if (DateTime.TryParse(GS(d, "actualizado"), out dt)) m.Actualizado = dt;
                    if (m.Id.Length == 0) m.Id = "mundo-1";
                    if (m.Nombre.Length == 0) m.Nombre = "Mundo 1";
                    if (d.ContainsKey("presetCompIds") && d["presetCompIds"] is System.Collections.IEnumerable)
                    {
                        foreach (var o in (System.Collections.IEnumerable)d["presetCompIds"])
                        {
                            try { m.PresetCompIds.Add(Convert.ToInt32(o)); } catch { }
                        }
                    }
                    if (d.ContainsKey("ligas") && d["ligas"] is System.Collections.IEnumerable)
                    {
                        foreach (var o in (System.Collections.IEnumerable)d["ligas"])
                        {
                            var ld = o as Dictionary<string, object>;
                            if (ld == null) continue;
                            m.Ligas.Add(new MundoLiga
                            {
                                CompId = GI(ld, "compId"),
                                Nombre = GS(ld, "nombre"),
                                RegId = GI(ld, "regId"),
                                Equipos = GI(ld, "equipos")
                            });
                        }
                    }
                    list.Add(m);
                }
            }
            catch { }
            return list;
        }

        public static void Save(AppConfig cfg, List<MundoInfo> mundos)
        {
            string path = RegistryPath(cfg);
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var arr = mundos.Select(m => new Dictionary<string, object>
            {
                { "id", m.Id }, { "nombre", m.Nombre },
                { "creado", m.Creado.ToString("o") }, { "actualizado", m.Actualizado.ToString("o") },
                { "gameFolder", m.GameFolder }, { "siderFolder", m.SiderFolder },
                { "pesdbFolder", m.PesdbFolder }, { "paqueteDir", m.PaqueteDir },
                { "editFile", m.EditFile }, { "savesFolder", m.SavesFolder },
                { "totalCompeticiones", m.TotalCompeticiones }, { "totalEquipos", m.TotalEquipos },
                { "totalJugadores", m.TotalJugadores },
                { "veredicto", m.Veredicto }, { "puntoAnclaId", m.PuntoAnclaId }, { "activo", m.Activo },
                { "presetCompIds", m.PresetCompIds.Select(x => (object)x).ToList() },
                { "ligas", m.Ligas.Select(l => new Dictionary<string, object>
                    {
                        { "compId", l.CompId }, { "nombre", l.Nombre },
                        { "regId", l.RegId }, { "equipos", l.Equipos }
                    }).ToList<object>() }
            }).ToList();
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(arr));
        }

        // Busca Documentos\KONAMI\*\*\save de PES 2021. Solo lectura.
        public static string DetectSavesFolder()
        {
            try { return DetectSavesFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); }
            catch { return ""; }
        }

        public static string DetectSavesFolder(string documentsDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(documentsDir)) return "";
                string konami = Path.Combine(documentsDir, "KONAMI");
                if (!Directory.Exists(konami)) return "";
                foreach (string versionDir in Directory.GetDirectories(konami))
                {
                    if (Path.GetFileName(versionDir).IndexOf("PES 2021", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    foreach (string userDir in Directory.GetDirectories(versionDir))
                    {
                        string save = Path.Combine(userDir, "save");
                        if (Directory.Exists(save)) return save;
                    }
                    string direct = Path.Combine(versionDir, "save");
                    if (Directory.Exists(direct)) return direct;
                }
            }
            catch { }
            return "";
        }

        private static string GS(Dictionary<string, object> d, string key)
        {
            return d != null && d.ContainsKey(key) && d[key] != null ? Convert.ToString(d[key]) : "";
        }

        private static int GI(Dictionary<string, object> d, string key)
        {
            try { return d != null && d.ContainsKey(key) && d[key] != null ? Convert.ToInt32(d[key]) : 0; }
            catch { return 0; }
        }
    }
}
