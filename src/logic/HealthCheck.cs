using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// =====================================================================
//  HealthCheck.cs — Fase 1 FL26: Diagnostico "¿Esta todo listo?"
//  SOLO LECTURA: este archivo no escribe, no crea ni modifica ningun
//  archivo. Lee rutas (AppConfig), sider.ini (texto + SiderIniManager),
//  el lua desplegado, el paquete livecpk, sider.log y la base cargada,
//  y devuelve un informe con niveles Ok / Info / Aviso / Error.
//
//  Inspiracion: checks.py + diagnostics/overview de FL26 Mod Studio
//  (Matozanato, MIT), reimplementado para PES 2021 con chequeos propios
//  (BOM en sider.ini, marcas [league_manager] del log, hash del paquete).
//  Los IDs de chequeo (R1..E2) son estables: la Fase 2 enganchara de
//  ellos los arreglos con un clic.
// =====================================================================
namespace EditorLigasPes
{
    public enum HealthLevel
    {
        Ok = 0,
        Info = 1,
        Aviso = 2,
        Error = 3
    }

    public class HealthItem
    {
        public string Id;
        public string Grupo;
        public HealthLevel Nivel;
        public string Titulo;
        public string Detalle;
        public string Sugerencia;
    }

    public class HealthReport
    {
        public List<HealthItem> Items = new List<HealthItem>();
        public DateTime Fecha = DateTime.Now;

        public int CountErrors { get { return Items.Count(i => i.Nivel == HealthLevel.Error); } }
        public int CountAvisos { get { return Items.Count(i => i.Nivel == HealthLevel.Aviso); } }

        public HealthLevel Veredicto
        {
            get
            {
                if (CountErrors > 0) return HealthLevel.Error;
                if (CountAvisos > 0) return HealthLevel.Aviso;
                return HealthLevel.Ok;
            }
        }

        public string VeredictoTexto
        {
            get
            {
                if (Veredicto == HealthLevel.Error) return "No listo";
                if (Veredicto == HealthLevel.Aviso) return "Listo con avisos";
                return "Listo";
            }
        }

        public HealthItem PrimerProblema
        {
            get
            {
                var e = Items.FirstOrDefault(i => i.Nivel == HealthLevel.Error);
                if (e != null) return e;
                return Items.FirstOrDefault(i => i.Nivel == HealthLevel.Aviso);
            }
        }

        public string Resumen
        {
            get
            {
                return string.Format("{0} ({1} errores, {2} avisos)", VeredictoTexto, CountErrors, CountAvisos);
            }
        }

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("DIAGNOSTICO PES 2021 EDITOR PRO — " + Fecha.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("Veredicto: " + Resumen);
            var p = PrimerProblema;
            if (p != null) sb.AppendLine("Primer problema: " + p.Id + " · " + p.Titulo);
            string grupo = null;
            foreach (var it in Items)
            {
                if (it.Grupo != grupo)
                {
                    grupo = it.Grupo;
                    sb.AppendLine();
                    sb.AppendLine("== " + grupo.ToUpperInvariant() + " ==");
                }
                string tag = it.Nivel == HealthLevel.Ok ? "OK" : it.Nivel == HealthLevel.Info ? "INFO" : it.Nivel == HealthLevel.Aviso ? "AVISO" : "ERROR";
                sb.AppendLine(string.Format("[{0}] {1} {2}", tag, it.Id, it.Titulo));
                if (!string.IsNullOrEmpty(it.Detalle)) sb.AppendLine("      " + it.Detalle);
                if (!string.IsNullOrEmpty(it.Sugerencia)) sb.AppendLine("      Sugerencia: " + it.Sugerencia);
            }
            return sb.ToString();
        }
    }

    public static class HealthChecker
    {
        private static readonly string[] SyncFiles = new string[]
        {
            "Competition.bin", "CompetitionRegulation.bin", "CompetitionEntry.bin",
            "Team.bin", "Player.bin", "PlayerAssignment.bin",
            "Tactics.bin", "TacticsFormation.bin", "Coach.bin", "Stadium.bin", "Derby.bin"
        };

        private static readonly string[] RequiredPesdb = new string[]
        {
            "Competition.bin", "CompetitionRegulation.bin", "CompetitionEntry.bin", "Team.bin"
        };

        public static HealthReport Run(AppConfig cfg, DatabaseManager db)
        {
            var rep = new HealthReport();
            if (cfg == null) cfg = new AppConfig();
            Safe(rep, "Rutas", () => CheckRutas(rep, cfg));
            Safe(rep, "Sider.ini", () => CheckSiderIni(rep, cfg));
            List<CompetitionRecord> customs = null;
            if (db != null)
            {
                try { customs = GetCustomCompetitions(db); } catch { customs = null; }
            }
            Safe(rep, "Despliegue", () => CheckDespliegue(rep, cfg, db, customs));
            Safe(rep, "Paquete livecpk", () => CheckPaquete(rep, cfg));
            Safe(rep, "Log de Sider", () => CheckLog(rep, cfg, customs));
            Safe(rep, "Base", () => CheckBase(rep, db, customs));
            Safe(rep, "Forense", () => CheckForensics(rep, cfg, db));
            Safe(rep, "EDIT", () => CheckEdit(rep, cfg));
            return rep;
        }

        private static void Safe(HealthReport rep, string grupo, Action body)
        {
            try { body(); }
            catch (Exception ex)
            {
                rep.Items.Add(new HealthItem
                {
                    Id = "—",
                    Grupo = grupo,
                    Nivel = HealthLevel.Info,
                    Titulo = "El grupo " + grupo + " no se pudo completar",
                    Detalle = ex.Message
                });
            }
        }

        private static void Add(HealthReport rep, string grupo, string id, HealthLevel nivel, string titulo, string detalle, string sugerencia)
        {
            rep.Items.Add(new HealthItem { Id = id, Grupo = grupo, Nivel = nivel, Titulo = titulo, Detalle = detalle, Sugerencia = sugerencia });
        }

        // Mismo criterio que MainForm.GetCustomCompetitions (M56).
        public static List<CompetitionRecord> GetCustomCompetitions(DatabaseManager db)
        {
            var customRegs = db.Regulations.Where(r => r.Id >= 179 && r.Id < 1000).ToList();
            var customCompIds = new HashSet<byte>(customRegs.Select(r => r.IdCompetition));
            var customComps = db.Competitions.Where(c => c.IsNew || customCompIds.Contains(c.Id)).OrderBy(c => c.Id).ToList();
            if (customComps.Count == 0)
            {
                customComps = db.Competitions.Where(c => c.Id == 50 || c.Id == 51 || c.Id == 52 || c.Id == 53 || c.Id >= 150 || (c.Name ?? "").IndexOf("CLASSIC", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            return customComps;
        }

        private static string ModulesDir(AppConfig cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.ModulesFolder)) return cfg.ModulesFolder;
            if (!string.IsNullOrWhiteSpace(cfg.SiderFolder)) return Path.Combine(cfg.SiderFolder, "modules");
            return null;
        }

        private static string PackageDir(AppConfig cfg)
        {
            string live = cfg.LiveCpkFolder;
            if (string.IsNullOrWhiteSpace(live) && !string.IsNullOrWhiteSpace(cfg.SiderFolder))
                live = Path.Combine(cfg.SiderFolder, "livecpk");
            if (string.IsNullOrWhiteSpace(live)) return null;
            return Path.Combine(live, "PruebaLigas", "common", "etc", "pesdb");
        }

        // ---------------- Grupo Rutas ----------------
        private static void CheckRutas(HealthReport rep, AppConfig cfg)
        {
            const string G = "Rutas";
            // R1 juego + exe
            if (!string.IsNullOrWhiteSpace(cfg.GameFolder) && Directory.Exists(cfg.GameFolder) && File.Exists(Path.Combine(cfg.GameFolder, "PES2021.exe")))
                Add(rep, G, "R1", HealthLevel.Ok, "Carpeta del juego y PES2021.exe", cfg.GameFolder, null);
            else
                Add(rep, G, "R1", HealthLevel.Error, "No se encuentra PES2021.exe en la carpeta del juego",
                    string.IsNullOrWhiteSpace(cfg.GameFolder) ? "La configuracion no tiene carpeta del juego." : "Carpeta: " + cfg.GameFolder,
                    "Revisar la carpeta del juego en Configuracion.");

            // R2 Sider
            if (!string.IsNullOrWhiteSpace(cfg.SiderFolder) && Directory.Exists(cfg.SiderFolder))
                Add(rep, G, "R2", HealthLevel.Ok, "Carpeta de Sider", cfg.SiderFolder, null);
            else
                Add(rep, G, "R2", HealthLevel.Error, "La carpeta de Sider no existe",
                    string.IsNullOrWhiteSpace(cfg.SiderFolder) ? "Sin carpeta configurada." : "Carpeta: " + cfg.SiderFolder,
                    "Revisar la derivacion de Sider en Configuracion.");

            // R3 subcarpetas del Sider
            var faltan = new List<string>();
            if (string.IsNullOrWhiteSpace(cfg.LiveCpkFolder) || !Directory.Exists(cfg.LiveCpkFolder)) faltan.Add("livecpk");
            if (ModulesDir(cfg) == null || !Directory.Exists(ModulesDir(cfg))) faltan.Add("modules");
            if (string.IsNullOrWhiteSpace(cfg.ContentFolder) || !Directory.Exists(cfg.ContentFolder)) faltan.Add("content");
            if (faltan.Count == 0)
                Add(rep, G, "R3", HealthLevel.Ok, "Subcarpetas de Sider: livecpk, modules y content", null, null);
            else
                Add(rep, G, "R3", HealthLevel.Aviso, "Faltan subcarpetas de Sider: " + string.Join(", ", faltan.ToArray()),
                    "Deben colgar de la carpeta de Sider.", "Crearlas o corregir las rutas en Configuracion.");

            // R4 pesdb de trabajo
            var sinBin = new List<string>();
            bool pesdbOk = !string.IsNullOrWhiteSpace(cfg.PesdbFolder) && Directory.Exists(cfg.PesdbFolder);
            if (pesdbOk)
            {
                foreach (var f in RequiredPesdb)
                    if (!File.Exists(Path.Combine(cfg.PesdbFolder, f))) sinBin.Add(f);
            }
            if (pesdbOk && sinBin.Count == 0)
                Add(rep, G, "R4", HealthLevel.Ok, "pesdb de trabajo con los 4 .bin obligatorios", cfg.PesdbFolder, null);
            else
                Add(rep, G, "R4", HealthLevel.Error, "La pesdb de trabajo no esta completa",
                    !pesdbOk ? "La carpeta no existe: " + (cfg.PesdbFolder ?? "(sin configurar)")
                              : "Faltan: " + string.Join(", ", sinBin.ToArray()),
                    "Elegir la pesdb existente, extraerla del CPK o crear una limpia desde Configuracion.");

            // R5 configuracion completa
            if (cfg.IsComplete)
                Add(rep, G, "R5", HealthLevel.Ok, "Configuracion completa", null, null);
            else
                Add(rep, G, "R5", HealthLevel.Aviso, "Configuracion incompleta (falta juego, Sider o pesdb)", null,
                    "Completar el asistente de Configuracion.");

            // R6 EDIT
            if (string.IsNullOrWhiteSpace(cfg.EditFile))
                Add(rep, G, "R6", HealthLevel.Info, "Sin archivo EDIT configurado (es opcional)", null, null);
            else if (File.Exists(cfg.EditFile))
                Add(rep, G, "R6", HealthLevel.Ok, "Archivo EDIT configurado y presente", cfg.EditFile, null);
            else
                Add(rep, G, "R6", HealthLevel.Aviso, "El EDIT configurado no esta en disco", cfg.EditFile,
                    "Elegir el EDIT vigente en Configuracion.");
        }

        // ---------------- Grupo Sider.ini ----------------
        private static void CheckSiderIni(HealthReport rep, AppConfig cfg)
        {
            const string G = "Sider.ini";
            if (string.IsNullOrWhiteSpace(cfg.SiderFolder))
            {
                Add(rep, G, "S1", HealthLevel.Error, "Sin carpeta de Sider: no se puede leer sider.ini", null, null);
                return;
            }
            string ini = Path.Combine(cfg.SiderFolder, "sider.ini");
            if (!File.Exists(ini))
            {
                Add(rep, G, "S1", HealthLevel.Error, "No existe sider.ini en la carpeta de Sider", ini, null);
                return;
            }
            Add(rep, G, "S1", HealthLevel.Ok, "sider.ini presente", ini, null);

            // S2 BOM
            bool bom = false;
            try
            {
                using (var fs = new FileStream(ini, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int b0 = fs.ReadByte(); int b1 = fs.ReadByte(); int b2 = fs.ReadByte();
                    bom = (b0 == 0xEF && b1 == 0xBB && b2 == 0xBF);
                }
            }
            catch { }
            if (bom)
                Add(rep, G, "S2", HealthLevel.Error, "sider.ini empieza con BOM UTF-8: Sider puede no reconocer la seccion [sider] e ignorar todo el archivo",
                    "Fue la causa del incidente en que Sider dejo de detectarse (corregido en el despliegue, v3-fix7).",
                    "Reescribir el archivo sin BOM (la Fase 2 lo podra hacer con un clic).");
            else
                Add(rep, G, "S2", HealthLevel.Ok, "sider.ini sin BOM", null, null);

            string[] lines = new string[0];
            try { lines = File.ReadAllLines(ini, Encoding.UTF8); } catch { }

            // S3 luajit
            bool luaJitOk = false; string luaJitVista = null;
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (t.StartsWith(";")) continue;
                if (t.StartsWith("luajit.ext.enabled", StringComparison.OrdinalIgnoreCase))
                {
                    luaJitVista = t;
                    var parts = t.Split('=');
                    if (parts.Length == 2 && parts[1].Trim() == "1") luaJitOk = true;
                }
            }
            if (luaJitOk)
                Add(rep, G, "S3", HealthLevel.Ok, "luajit.ext.enabled = 1 (FFI disponible)", null, null);
            else
                Add(rep, G, "S3", HealthLevel.Error, "luajit.ext.enabled no esta en 1: league_manager.lua aborta sin FFI",
                    luaJitVista == null ? "La linea no esta en sider.ini." : "Linea encontrada: " + luaJitVista,
                    "Poner luajit.ext.enabled = 1 en sider.ini.");

            // Modulos via el gestor existente
            var modules = new List<SiderModuleEntry>();
            try { modules = SiderIniManager.GetModules(cfg.SiderFolder); } catch { }

            // S4 / S5 league_manager
            var lm = modules.FirstOrDefault(m => string.Equals(m.Name, "league_manager.lua", StringComparison.OrdinalIgnoreCase));
            if (lm == null)
                Add(rep, G, "S4", HealthLevel.Error, "sider.ini no tiene la linea de league_manager.lua", null,
                    "Desplegar los modulos desde la pestaña Sider.");
            else
            {
                Add(rep, G, "S4", HealthLevel.Ok, "Linea de league_manager.lua presente en sider.ini", null, null);
                if (lm.IsActive)
                    Add(rep, G, "S5", HealthLevel.Ok, "league_manager.lua activo", null, null);
                else
                    Add(rep, G, "S5", HealthLevel.Error, "league_manager.lua esta COMENTADO en sider.ini: Sider no lo carga y ninguna liga nueva aparece en el juego",
                        "La linea empieza con ';'.", "Activarlo en el gestor de modulos de Configuracion.");
            }

            // S6 lineas colgadas
            string modDir = ModulesDir(cfg);
            int colgadas = 0;
            foreach (var m in modules)
            {
                if (!m.IsActive) continue;
                if (modDir == null || !File.Exists(Path.Combine(modDir, m.Name)))
                {
                    colgadas++;
                    Add(rep, G, "S6", HealthLevel.Aviso, "Modulo activo sin archivo en modules: " + m.Name, null,
                        "Instalar el archivo o desactivar la linea.");
                }
            }
            if (colgadas == 0 && modules.Count > 0)
                Add(rep, G, "S6", HealthLevel.Ok, "Todos los modulos activos tienen su archivo en modules", null, null);

            // S7 duplicados (lectura propia: GetModules deduplica)
            var conteo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (t.StartsWith(";")) continue; // comentada no cuenta como duplicado
                if (!t.StartsWith("lua.module", StringComparison.OrdinalIgnoreCase)) continue;
                int q1 = t.IndexOf('"'); int q2 = t.LastIndexOf('"');
                if (q1 >= 0 && q2 > q1)
                {
                    string name = t.Substring(q1 + 1, q2 - q1 - 1);
                    int n; conteo.TryGetValue(name, out n); conteo[name] = n + 1;
                }
            }
            var dups = conteo.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
            if (dups.Count > 0)
                Add(rep, G, "S7", HealthLevel.Aviso, "Modulos repetidos en sider.ini: " + string.Join(", ", dups.ToArray()), null,
                    "Dejar una sola linea por modulo.");
            else
                Add(rep, G, "S7", HealthLevel.Ok, "Sin modulos repetidos en sider.ini", null, null);
            // S8 orden: league_manager expande la tabla maestra al cargar;
            // un modulo fl26 activo ANTES que el puede intentar expandirla
            // por su cuenta (en el log se ve como ABORTED de los fl26).
            var orden = new List<string>();
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (t.StartsWith(";")) continue;
                if (!t.StartsWith("lua.module", StringComparison.OrdinalIgnoreCase)) continue;
                int q1 = t.IndexOf('"'); int q2 = t.LastIndexOf('"');
                if (q1 >= 0 && q2 > q1) orden.Add(t.Substring(q1 + 1, q2 - q1 - 1));
            }
            int lmIdx = orden.FindIndex(n => string.Equals(n, "league_manager.lua", StringComparison.OrdinalIgnoreCase));
            if (lmIdx >= 0)
            {
                var antes = orden.Take(lmIdx).Where(n => n.StartsWith("fl26", StringComparison.OrdinalIgnoreCase)).ToList();
                if (antes.Count > 0)
                    Add(rep, G, "S8", HealthLevel.Aviso,
                        "Orden de modulos: " + string.Join(", ", antes.ToArray()) + " corre antes que league_manager.lua",
                        "league_manager expande la tabla maestra al cargar; un fl26 anterior puede intentar expandirla por su cuenta y abortar (los ABORTED del log).",
                        "En sider.ini, dejar league_manager.lua por encima de los modulos fl26.");
                else
                    Add(rep, G, "S8", HealthLevel.Ok, "Orden de modulos: league_manager.lua corre antes que los fl26", null, null);
            }
        }

        // ---------------- Grupo Despliegue ----------------
        private static void CheckDespliegue(HealthReport rep, AppConfig cfg, DatabaseManager db, List<CompetitionRecord> customs)
        {
            const string G = "Despliegue";
            string modDir = ModulesDir(cfg);
            string luaPath = modDir == null ? null : Path.Combine(modDir, "league_manager.lua");

            if (luaPath != null && File.Exists(luaPath))
                Add(rep, G, "D1", HealthLevel.Ok, "league_manager.lua desplegado en modules", luaPath, null);
            else
            {
                Add(rep, G, "D1", HealthLevel.Error, "league_manager.lua no esta en la carpeta modules",
                    luaPath ?? "(sin carpeta modules)", "Desplegar desde la pestaña Sider.");
                return;
            }

            if (db == null || customs == null)
            {
                Add(rep, G, "D2", HealthLevel.Info, "Sin base cargada: no se puede comparar el lua desplegado con la base", null, null);
                return;
            }

            // D2 comparacion con el generado en memoria
            try
            {
                string generado = SiderLuaGenerator.GenerateLuaScript(customs, db.Regulations, db);
                string enDisco = File.ReadAllText(luaPath);
                if (Normalize(generado) == Normalize(enDisco))
                    Add(rep, G, "D2", HealthLevel.Ok, "El lua desplegado coincide con la base actual", null, null);
                else
                    Add(rep, G, "D2", HealthLevel.Aviso, "El lua desplegado NO coincide con la base actual: el juego corre un lua viejo",
                        "Difiere del que se genera desde las ligas custom cargadas.", "Redesplegar desde la pestaña Sider.");
            }
            catch (Exception ex)
            {
                Add(rep, G, "D2", HealthLevel.Info, "No se pudo generar el lua para comparar", ex.Message, null);
            }

            // D3 cada liga custom nombrada en el lua
            try
            {
                string enDisco = File.ReadAllText(luaPath);
                var ausentes = new List<string>();
                foreach (var c in customs)
                {
                    string nombre = c.Name ?? "";
                    if (nombre.Length == 0) continue;
                    if (enDisco.IndexOf(nombre, StringComparison.OrdinalIgnoreCase) < 0 &&
                        enDisco.IndexOf(SiderLuaGenerator.EscapeLuaString(nombre), StringComparison.OrdinalIgnoreCase) < 0)
                        ausentes.Add(nombre);
                }
                if (ausentes.Count == 0)
                    Add(rep, G, "D3", HealthLevel.Ok, "El lua nombra a todas las ligas custom de la base (" + customs.Count + ")", null, null);
                else
                    Add(rep, G, "D3", HealthLevel.Error, "Ligas custom ausentes en el lua desplegado: " + string.Join(", ", ausentes.ToArray()), null,
                        "Redesplegar desde la pestaña Sider.");
            }
            catch (Exception ex)
            {
                Add(rep, G, "D3", HealthLevel.Info, "No se pudo cruzar el lua con las ligas", ex.Message, null);
            }
        }

        private static string Normalize(string s)
        {
            if (s == null) return "";
            return s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
        }

        // ---------------- Grupo Paquete livecpk ----------------
        private static void CheckPaquete(HealthReport rep, AppConfig cfg)
        {
            const string G = "Paquete livecpk";
            string pkg = PackageDir(cfg);
            if (pkg == null || !Directory.Exists(pkg))
            {
                Add(rep, G, "P1", HealthLevel.Error, "No existe el paquete livecpk\\PruebaLigas\\common\\etc\\pesdb",
                    pkg ?? "(sin livecpk configurado)", "Guardar la base para que se cree y sincronice el paquete.");
                return;
            }
            Add(rep, G, "P1", HealthLevel.Ok, "Paquete del juego presente", pkg, null);

            var faltan = SyncFiles.Where(f => !File.Exists(Path.Combine(pkg, f))).ToList();
            if (faltan.Count == 0)
                Add(rep, G, "P2", HealthLevel.Ok, "Los 11 archivos del paquete estan completos", null, null);
            else
                Add(rep, G, "P2", HealthLevel.Error, "Faltan archivos en el paquete: " + string.Join(", ", faltan.ToArray()), null,
                    "Guardar la base para resincronizar el paquete.");

            if (string.IsNullOrWhiteSpace(cfg.PesdbFolder) || !Directory.Exists(cfg.PesdbFolder))
            {
                Add(rep, G, "P3", HealthLevel.Info, "Sin pesdb de trabajo: no se puede comparar el paquete", null, null);
                return;
            }
            var difieren = new List<string>();
            foreach (var f in SyncFiles)
            {
                string a = Path.Combine(cfg.PesdbFolder, f);
                string b = Path.Combine(pkg, f);
                if (!File.Exists(a) || !File.Exists(b)) continue;
                if (!HashEquals(a, b)) difieren.Add(f);
            }
            if (difieren.Count == 0)
                Add(rep, G, "P3", HealthLevel.Ok, "El paquete es identico a la pesdb de trabajo", null, null);
            else
                Add(rep, G, "P3", HealthLevel.Aviso, "El paquete difiere de la pesdb de trabajo en: " + string.Join(", ", difieren.ToArray()),
                    "El juego ve el paquete, no la carpeta de trabajo.", "Guardar la base para sincronizar.");
        }

        private static bool HashEquals(string a, string b)
        {
            try
            {
                if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
                using (var sha = SHA256.Create())
                {
                    byte[] ha, hb;
                    using (var fs = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) ha = sha.ComputeHash(fs);
                    using (var fs = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) hb = sha.ComputeHash(fs);
                    return ha.SequenceEqual(hb);
                }
            }
            catch { return false; }
        }

        // ---------------- Grupo Log de Sider ----------------
        private static void CheckLog(HealthReport rep, AppConfig cfg, List<CompetitionRecord> customs)
        {
            const string G = "Log de Sider";
            if (string.IsNullOrWhiteSpace(cfg.SiderFolder))
            {
                Add(rep, G, "L1", HealthLevel.Info, "Sin carpeta de Sider: no hay log que leer", null, null);
                return;
            }
            string log = Path.Combine(cfg.SiderFolder, "sider.log");
            if (!File.Exists(log))
            {
                Add(rep, G, "L1", HealthLevel.Info, "Todavia no hay sider.log: el juego no se corrio con Sider desde esta instalacion", log, null);
                return;
            }
            Add(rep, G, "L1", HealthLevel.Ok, "sider.log presente", log, null);

            // El bloque de inicio del league_manager vive al PRINCIPIO del
            // log (Sider carga los modulos al arrancar) y una sesion larga
            // lo empuja fuera de cualquier ventana de cola: se recorre el
            // archivo entero quedandose solo con las lineas relevantes.
            List<string> lmLines, abortedLines;
            CollectLogLines(log, out lmLines, out abortedLines);
            DateTime logTime = File.GetLastWriteTime(log);

            // Alcance: la ultima corrida del league_manager
            int inicio = 0;
            for (int i = lmLines.Count - 1; i >= 0; i--)
            {
                if (lmLines[i].Contains("Iniciando League Manager")) { inicio = i; break; }
            }
            var run = lmLines.Skip(inicio).ToList();

            // L2 frescura del log contra el paquete
            string pkg = PackageDir(cfg);
            if (pkg != null && Directory.Exists(pkg))
            {
                DateTime maxPkg = DateTime.MinValue;
                foreach (var f in SyncFiles)
                {
                    string p = Path.Combine(pkg, f);
                    if (File.Exists(p)) { var t = File.GetLastWriteTime(p); if (t > maxPkg) maxPkg = t; }
                }
                if (maxPkg != DateTime.MinValue && logTime < maxPkg)
                    Add(rep, G, "L2", HealthLevel.Info, "El log es anterior al ultimo guardado del paquete: sus marcas no prueban el estado actual",
                        "Log: " + logTime.ToString("dd/MM HH:mm") + " · Paquete: " + maxPkg.ToString("dd/MM HH:mm"),
                        "Entrar al juego una vez y volver a chequear.");
                else
                    Add(rep, G, "L2", HealthLevel.Ok, "El log es posterior al ultimo guardado del paquete", "Ultima corrida registrada: " + logTime.ToString("dd/MM HH:mm"), null);
            }

            // L3 FFI
            var ffi = run.FirstOrDefault(l => l.Contains("[league_manager]") && (l.Contains("FFI global") || l.Contains("luajit.ext.enabled")));
            if (ffi != null && ffi.Contains("Error"))
                Add(rep, G, "L3", HealthLevel.Error, "La ultima corrida aborto por FFI (luajit)", Trunc(ffi), "Poner luajit.ext.enabled = 1 en sider.ini.");
            else
                Add(rep, G, "L3", HealthLevel.Ok, "Sin errores de FFI en la ultima corrida", null, null);

            // L4 abortos del parche
            var abort = run.FirstOrDefault(l => l.Contains("firmas de la tabla de competiciones no coinciden")
                || l.Contains("instalacion base incompleta")
                || l.Contains("fallos de verificacion")
                || l.Contains("[VERIFY-FAIL]")
                || l.Contains("VirtualAlloc fallo")
                || l.Contains("no se encontro la fila plantilla")
                || l.Contains("no se instalaron"));
            if (abort != null)
                Add(rep, G, "L4", HealthLevel.Error, "La ultima corrida aborto un parche del league_manager", Trunc(abort), null);
            else
                Add(rep, G, "L4", HealthLevel.Ok, "Sin abortos de parche en la ultima corrida", null, null);

            // L5 filas expandidas
            int? filas = null;
            foreach (var l in run)
            {
                var m = Regex.Match(l, @"Tabla de competiciones expandida.*\((\d+) filas totales\)");
                if (m.Success) filas = int.Parse(m.Groups[1].Value);
            }
            if (filas.HasValue)
            {
                if (customs != null)
                {
                    int esperado = 165 + customs.Count;
                    if (filas.Value == esperado)
                        Add(rep, G, "L5", HealthLevel.Ok, "Tabla expandida a " + filas.Value + " filas (165 + " + customs.Count + " ligas custom)", null, null);
                    else
                        Add(rep, G, "L5", HealthLevel.Aviso, "La ultima corrida expandio la tabla a " + filas.Value + " filas, pero la base actual espera " + esperado + " (165 + " + customs.Count + ")",
                            "El juego cargo otro lua u otra base.", "Guardar, redesplegar y volver a entrar al juego.");
                }
                else
                    Add(rep, G, "L5", HealthLevel.Ok, "Tabla expandida a " + filas.Value + " filas en la ultima corrida", null, null);
            }
            else if (customs != null && customs.Count > 0)
                Add(rep, G, "L5", HealthLevel.Aviso, "La ultima corrida no registro la expansion de la tabla de competiciones", null,
                    "Revisar L3/L4 y que el lua desplegado sea el actual.");

            // L6 enlace completo
            bool enlazado = run.Any(l => l.Contains("Todos los sistemas enlazados con exito"));
            if (enlazado)
                Add(rep, G, "L6", HealthLevel.Ok, "La ultima corrida enlazo todos los sistemas del league_manager", null, null);
            else if (ffi == null && abort == null)
                Add(rep, G, "L6", HealthLevel.Aviso, "La ultima corrida no confirmo el enlace completo de sistemas", null,
                    "Entrar al juego con Sider y volver a chequear.");

            // L7 ABORTED genericos (de todo el archivo, sin duplicados)
            var aborted = abortedLines.Distinct().Take(3).ToList();
            foreach (var a in aborted)
                Add(rep, G, "L7", HealthLevel.Aviso, "Linea ABORTED en el log", Trunc(a), null);
        }

        private static string Trunc(string s)
        {
            if (s == null) return null;
            s = s.Trim();
            return s.Length <= 220 ? s : s.Substring(0, 220) + "…";
        }

        // Recorre el log completo una sola vez y conserva las lineas del
        // league_manager y las ABORTED (con tope, quedandose con las
        // ultimas). Un sider.log de varios MB se procesa sin cargarlo
        // entero en memoria.
        private static void CollectLogLines(string path, out List<string> lmLines, out List<string> abortedLines)
        {
            lmLines = new List<string>();
            abortedLines = new List<string>();
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains("[league_manager]"))
                        {
                            lmLines.Add(line);
                            if (lmLines.Count > 800) lmLines.RemoveAt(0);
                        }
                        else if (line.Contains("ABORTED"))
                        {
                            abortedLines.Add(line);
                            if (abortedLines.Count > 60) abortedLines.RemoveAt(0);
                        }
                    }
                }
            }
            catch { }
        }

        // ---------------- Grupo Base ----------------
        private static void CheckBase(HealthReport rep, DatabaseManager db, List<CompetitionRecord> customs)
        {
            const string G = "Base";
            if (db == null)
            {
                Add(rep, G, "B1", HealthLevel.Info, "Sin base cargada: se omiten los chequeos de base", null, null);
                return;
            }
            if (customs == null) customs = new List<CompetitionRecord>();

            Add(rep, G, "B1", HealthLevel.Info,
                string.Format("Base: {0} competiciones · {1} equipos · {2} jugadores · {3} ligas custom",
                    db.Competitions.Count, db.Teams.Count, db.Players.Count, customs.Count), null, null);

            var regByComp = new Dictionary<byte, RegulationRecord>();
            foreach (var r in db.Regulations) regByComp[r.IdCompetition] = r;

            // B2 equipos cargados vs reglamento
            foreach (var c in customs)
            {
                RegulationRecord reg;
                if (!regByComp.TryGetValue(c.Id, out reg)) continue;
                int cargados = db.Entries.Count(e => e.CompetitionId == c.Id);
                if (cargados != reg.TotalTeams)
                    Add(rep, G, "B2", HealthLevel.Aviso,
                        string.Format("Liga custom {0}: tiene {1} equipos cargados pero su reglamento dice {2}", Nombre(c), cargados, reg.TotalTeams), null,
                        "Revisar los participantes de la liga.");
            }

            // B3 posiciones contiguas 1..N
            foreach (var c in customs)
            {
                var pos = db.Entries.Where(e => e.CompetitionId == c.Id).Select(e => (int)e.Position).OrderBy(p => p).ToList();
                bool okPos = true;
                for (int i = 0; i < pos.Count; i++) if (pos[i] != i + 1) { okPos = false; break; }
                if (!okPos)
                    Add(rep, G, "B3", HealthLevel.Aviso,
                        "Liga custom " + Nombre(c) + ": las posiciones de participantes no son contiguas 1.." + pos.Count,
                        "Posiciones: " + string.Join(",", pos.Select(p => p.ToString()).ToArray()), null);
            }

            // B4 equipos con menos de 11 jugadores distintos
            if (db.PlayerAssignments != null && db.PlayerAssignments.Count > 0)
            {
                var conteo = new Dictionary<uint, HashSet<uint>>();
                foreach (var a in db.PlayerAssignments)
                {
                    HashSet<uint> set;
                    if (!conteo.TryGetValue(a.TeamId, out set)) { set = new HashSet<uint>(); conteo[a.TeamId] = set; }
                    set.Add(a.PlayerId);
                }
                var teamById = new Dictionary<uint, TeamRecord>();
                foreach (var t in db.Teams) teamById[t.Id] = t;
                foreach (var c in customs)
                {
                    foreach (var e in db.Entries.Where(x => x.CompetitionId == c.Id))
                    {
                        HashSet<uint> set;
                        int n = conteo.TryGetValue(e.TeamId, out set) ? set.Count : 0;
                        if (n < 11)
                        {
                            TeamRecord tr;
                            string tn = ("ID " + e.TeamId);
                            if (teamById.TryGetValue(e.TeamId, out tr))
                                tn = !string.IsNullOrEmpty(tr.NameSpanish) ? tr.NameSpanish : (!string.IsNullOrEmpty(tr.NameEnglish) ? tr.NameEnglish : tn);
                            Add(rep, G, "B4", HealthLevel.Error,
                                string.Format("Equipo {0} (liga {1}) tiene {2} jugadores: con menos de 11 el juego lo esconde", tn, Nombre(c), n), null,
                                "Completar la plantilla del equipo.");
                        }
                    }
                }
            }

            // B5 sin pais (el pais de la liga vive en su reglamento:
            // DatabaseManager.GetLeagueCountryId, el mismo que usa el lua)
            foreach (var c in customs)
            {
                ushort pais = 0;
                try { pais = db.GetLeagueCountryId(c.Id); } catch { }
                if (pais == 0)
                    Add(rep, G, "B5", HealthLevel.Aviso, "Liga custom sin pais asignado: " + Nombre(c),
                        "En el juego se ve sin bandera.", "Asignarle el pais a la liga.");
            }

            // B6 piramide con hueco: nivel 3 sin nivel 2
            try
            {
                foreach (var sys in db.GetAllPyramidSystems())
                {
                    var tiers = new HashSet<int>(sys.Tiers.Select(t => t.TierNumber));
                    if (tiers.Contains(3) && !tiers.Contains(2))
                        Add(rep, G, "B6", HealthLevel.Aviso,
                            "Piramide de " + sys.CountryName + ": tiene nivel 3 pero no nivel 2",
                            "Los ascensos/descensos entre pisos quedan cortados.", null);
                }
            }
            catch (Exception ex)
            {
                Add(rep, G, "B6", HealthLevel.Info, "No se pudieron revisar las piramides", ex.Message, null);
            }

            // B7 RegID duplicado entre regulaciones custom
            var customRegs = db.Regulations.Where(r => r.Id >= 179 && r.Id < 1000).ToList();
            var dupRegs = customRegs.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var d in dupRegs)
                Add(rep, G, "B7", HealthLevel.Error, "RegID duplicado entre ligas custom: " + d, null, null);
            if (dupRegs.Count == 0 && customRegs.Count > 0)
                Add(rep, G, "B7", HealthLevel.Ok, "Sin RegID duplicados entre ligas custom", null, null);
        }

        private static string Nombre(CompetitionRecord c)
        {
            return string.IsNullOrEmpty(c.Name) ? ("ID " + c.Id) : c.Name;
        }

        // ---------------- Grupo Forense (checks.py de Matozanato) ----------------
        private static void CheckForensics(HealthReport rep, AppConfig cfg, DatabaseManager db)
        {
            const string G = "Diagnóstico Forense";
            if (db == null)
            {
                Add(rep, G, "F1", HealthLevel.Info, "Sin base cargada: omitiendo auditoría forense profunda", null, null);
                return;
            }

            // F1: Dorsales duplicados en una misma plantilla
            if (db.PlayerAssignments != null && db.PlayerAssignments.Count > 0)
            {
                var dupShirts = new List<string>();
                var byTeam = db.PlayerAssignments.GroupBy(a => a.TeamId);
                foreach (var tg in byTeam)
                {
                    var shirtMap = new Dictionary<int, List<uint>>();
                    foreach (var a in tg)
                    {
                        int shirt = (int)(a.Pack & 0x3FF) + 1;
                        if (shirt > 0 && shirt <= 99)
                        {
                            List<uint> pids;
                            if (!shirtMap.TryGetValue(shirt, out pids)) { pids = new List<uint>(); shirtMap[shirt] = pids; }
                            pids.Add(a.PlayerId);
                        }
                    }
                    foreach (var pair in shirtMap)
                    {
                        if (pair.Value.Count > 1)
                        {
                            TeamRecord tr;
                            string tName = db.TeamsById != null && db.TeamsById.TryGetValue(tg.Key, out tr) && tr != null
                                ? (tr.NameSpanish ?? tr.NameEnglish ?? ("Equipo " + tg.Key))
                                : ("Equipo " + tg.Key);
                            dupShirts.Add(string.Format("{0}: dorsal #{1} repetido ({2} jugadores)", tName, pair.Key, pair.Value.Count));
                            if (dupShirts.Count >= 6) break;
                        }
                    }
                    if (dupShirts.Count >= 6) break;
                }
                if (dupShirts.Count > 0)
                    Add(rep, G, "F1", HealthLevel.Aviso, "Dorsales repetidos en plantillas de club: " + string.Join("; ", dupShirts.ToArray()),
                        "Dos jugadores en un mismo equipo con la misma camiseta pueden generar confusión o fallos de render.",
                        "Reasignar números de dorsal en la pestaña Plantilla.");
                else
                    Add(rep, G, "F1", HealthLevel.Ok, "Sin dorsales duplicados en las plantillas analizadas", null, null);
            }

            // F2: Entradas huérfanas en CompetitionEntry.bin
            if (db.Entries != null && db.Entries.Count > 0 && db.TeamsById != null)
            {
                var missingEntries = db.Entries.Where(e => !db.TeamsById.ContainsKey(e.TeamId)).ToList();
                if (missingEntries.Count > 0)
                {
                    var sample = missingEntries.Take(4).Select(e => string.Format("Torneo {0} -> Equipo {1}", e.CompetitionId, e.TeamId));
                    Add(rep, G, "F2", HealthLevel.Error,
                        string.Format("Se encontraron {0} entradas en CompetitionEntry.bin que apuntan a equipos inexistentes", missingEntries.Count),
                        string.Join(", ", sample.ToArray()),
                        "Revisar o limpiar los equipos asignados a las competiciones.");
                }
                else
                {
                    Add(rep, G, "F2", HealthLevel.Ok, "Todas las entradas de torneos apuntan a equipos válidos", null, null);
                }
            }

            // F3: Asignaciones huérfanas en PlayerAssignment.bin
            if (db.PlayerAssignments != null && db.PlayerAssignments.Count > 0)
            {
                int orphanPlayers = 0;
                int orphanTeams = 0;
                foreach (var a in db.PlayerAssignments)
                {
                    if (db.PlayersById != null && !db.PlayersById.ContainsKey(a.PlayerId)) orphanPlayers++;
                    if (db.TeamsById != null && !db.TeamsById.ContainsKey(a.TeamId)) orphanTeams++;
                }
                if (orphanPlayers > 0 || orphanTeams > 0)
                {
                    Add(rep, G, "F3", HealthLevel.Aviso,
                        string.Format("Asignaciones huérfanas: {0} futbolistas sin ficha y {1} asignados a clubes inexistentes", orphanPlayers, orphanTeams),
                        "Pueden generar registros fantasma en la base.",
                        "Limpiar transferencias o sincronizar jugadores.");
                }
                else
                {
                    Add(rep, G, "F3", HealthLevel.Ok, "Integridad referencial de asignaciones de jugadores y clubes 100% limpia", null, null);
                }
            }

            // F4: Límite de recuento de regulaciones (recount_rows / fl26regs600)
            if (db.Regulations != null && db.Regulations.Count > 300)
            {
                bool hasRegs600 = false;
                string iniPath = !string.IsNullOrWhiteSpace(cfg.SiderFolder) ? Path.Combine(cfg.SiderFolder, "sider.ini") : null;
                if (iniPath != null && File.Exists(iniPath))
                {
                    string txt = File.ReadAllText(iniPath);
                    hasRegs600 = Regex.IsMatch(txt, @"^\s*lua\.module\s*=\s*""fl26regs600\.lua""", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                }
                if (!hasRegs600)
                {
                    Add(rep, G, "F4", HealthLevel.Error,
                        string.Format("La base tiene {0} regulaciones (>300) pero fl26regs600.lua no está activo en sider.ini", db.Regulations.Count),
                        "Konami solo cuenta las primeras 300 filas: los partidos de eliminatorias y fases finales de torneos continentales se perderán.",
                        "Habilitar fl26regs600.lua desplegando con Sider en la pestaña Sider y Lua.");
                }
                else
                {
                    Add(rep, G, "F4", HealthLevel.Ok, string.Format("Recuento de regulaciones cubierto: {0} regulaciones con fl26regs600.lua activo", db.Regulations.Count), null, null);
                }
            }

            // F5: Permisos de escritura de Windows en carpetas críticas (write_problems)
            if (!string.IsNullOrWhiteSpace(cfg.GameFolder) && Directory.Exists(cfg.GameFolder))
            {
                bool writeOk = true;
                string testFile = Path.Combine(cfg.GameFolder, ".test_write_access.tmp");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch
                {
                    writeOk = false;
                }
                if (!writeOk)
                {
                    Add(rep, G, "F5", HealthLevel.Error,
                        "Windows no permite escritura en la carpeta del juego (" + cfg.GameFolder + ")",
                        "El sistema de archivos de Windows o UAC está bloqueando la carpeta (típico bajo 'C:\\Program Files').",
                        "Ejecutar la app como Administrador o mover la carpeta del juego fuera de Archivos de programa.");
                }
                else
                {
                    Add(rep, G, "F5", HealthLevel.Ok, "Permisos de escritura verificados en la carpeta del juego", null, null);
                }
            }

            // F6: Módulos en conflicto en sider.ini (fl26cuphook vs fl26chain)
            if (!string.IsNullOrWhiteSpace(cfg.SiderFolder))
            {
                string iniPath = Path.Combine(cfg.SiderFolder, "sider.ini");
                if (File.Exists(iniPath))
                {
                    string txt = File.ReadAllText(iniPath);
                    if (Regex.IsMatch(txt, @"fl26cuphook", RegexOptions.IgnoreCase) && Regex.IsMatch(txt, @"fl26chain", RegexOptions.IgnoreCase))
                    {
                        Add(rep, G, "F6", HealthLevel.Error,
                            "Conflicto de módulos en sider.ini: fl26cuphook y fl26chain no pueden coexistir",
                            "fl26cuphook es una versión antigua obsoleta reemplazada por fl26chain.",
                            "Desactivar o borrar la línea de fl26cuphook en sider.ini.");
                    }
                    else
                    {
                        Add(rep, G, "F6", HealthLevel.Ok, "Sin conflictos de módulos obsoletos en sider.ini", null, null);
                    }
                }
            }
        }

        // ---------------- Grupo EDIT ----------------
        private static void CheckEdit(HealthReport rep, AppConfig cfg)
        {
            const string G = "EDIT";
            if (string.IsNullOrWhiteSpace(cfg.EditFile)) return; // R6 ya lo informa
            if (!File.Exists(cfg.EditFile))
            {
                Add(rep, G, "E1", HealthLevel.Aviso, "El EDIT configurado no esta en disco", cfg.EditFile, null);
                return;
            }
            Add(rep, G, "E1", HealthLevel.Ok, "EDIT presente", cfg.EditFile, null);
            if (File.Exists(cfg.EditFile + ".bak"))
                Add(rep, G, "E2", HealthLevel.Ok, "El EDIT tiene su copia .bak al lado", null, null);
            else
                Add(rep, G, "E2", HealthLevel.Info, "El EDIT no tiene copia .bak al lado todavia", null, null);
        }
    }
}
