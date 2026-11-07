// ============================================================================
//  MundoSwitch.cs  -  Punto A FL26: intercambio de mundos
// ----------------------------------------------------------------------------
//  Segunda mitad de la Fase 3 (la primera fue el registro). Todos los
//  mundos comparten la instalacion: lo que se intercambia es el
//  CONTENIDO de la pesdb del paquete PruebaLigas y la carpeta de
//  guardado de KONAMI (carreras + EDIT viajan juntos, renombrando
//  save <-> save_<mundo>, sin copiar). Cada mundo guarda su pesdb en
//  editorpro-respaldos\mundos\<id>\pesdb.
//
//  Cambiar de mundo nunca pierde trabajo: antes de salir, la pesdb
//  viva se copia al almacen del mundo saliente (foto de salida) y se
//  refresca su foto. Borrar manda todo a una papelera fechada.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EditorLigasPes
{
    public static class MundoSwitch
    {
        public static string WorldsRoot(AppConfig cfg)
        {
            return Path.Combine(SiderBackup.RootDir(cfg), "mundos");
        }

        public static string StoreDir(AppConfig cfg, string worldId)
        {
            return Path.Combine(WorldsRoot(cfg), worldId, "pesdb");
        }

        public static bool HasStore(AppConfig cfg, string worldId)
        {
            string d = StoreDir(cfg, worldId);
            return Directory.Exists(d) && File.Exists(Path.Combine(d, "Competition.bin"));
        }

        // Foto de contenido desde una base cargada (mismo criterio que el registro).
        public static void TakePhoto(DatabaseManager db, MundoInfo mundo)
        {
            if (db == null || mundo == null) return;
            mundo.TotalCompeticiones = db.Competitions.Count;
            mundo.TotalEquipos = db.Teams.Count;
            mundo.TotalJugadores = db.Players.Count;
            var customRegs = db.Regulations.Where(r => r.Id >= 179 && r.Id < 1000).ToList();
            var customIds = new HashSet<byte>(customRegs.Select(r => r.IdCompetition));
            var customs = db.Competitions.Where(c => c.IsNew || customIds.Contains(c.Id)).OrderBy(c => c.Id).ToList();
            if (customs.Count == 0)
                customs = db.Competitions.Where(c => c.Id == 50 || c.Id == 51 || c.Id == 52 || c.Id == 53 || c.Id >= 150 ||
                    (c.Name ?? "").IndexOf("CLASSIC", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            mundo.Ligas = new List<MundoLiga>();
            foreach (var c in customs)
            {
                var reg = db.Regulations.FirstOrDefault(x => x.IdCompetition == c.Id);
                int equipos = db.Entries.Where(e => e.CompetitionId == c.Id).Select(e => (int)e.TeamId).Distinct().Count();
                mundo.Ligas.Add(new MundoLiga { CompId = c.Id, Nombre = c.Name ?? "", RegId = reg != null ? reg.Id : 0, Equipos = equipos });
            }
        }

        // Copia espejo de una carpeta plana (la pesdb): copia todo y
        // borra en destino lo que ya no esta en origen.
        public static void MirrorCopy(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            var srcFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in Directory.GetFiles(src))
            {
                string name = Path.GetFileName(f);
                srcFiles.Add(name);
                File.Copy(f, Path.Combine(dst, name), true);
            }
            foreach (string f in Directory.GetFiles(dst))
                if (!srcFiles.Contains(Path.GetFileName(f))) File.Delete(f);
        }

        // Foto de salida: la pesdb viva queda guardada en el almacen del mundo.
        public static void Snapshot(AppConfig cfg, DatabaseManager db, MundoInfo mundo)
        {
            string pkg = MundoRegistry.PackageDir(cfg);
            if (string.IsNullOrEmpty(pkg) || !Directory.Exists(pkg)) return;
            MirrorCopy(pkg, StoreDir(cfg, mundo.Id));
            TakePhoto(db, mundo);
            mundo.Actualizado = DateTime.Now;
        }

        public static MundoInfo CreateWorld(AppConfig cfg, DatabaseManager db, string nombre, bool desdeBaseLimpia, out string error)
        {
            error = "";
            var mundos = MundoRegistry.Load(cfg);
            if (mundos.Count == 0) { error = "Primero registrá el Mundo 1."; return null; }
            int maxNum = 0;
            foreach (var m in mundos)
            {
                int n;
                if (m.Id != null && m.Id.StartsWith("mundo-") && int.TryParse(m.Id.Substring(6), out n) && n > maxNum) maxNum = n;
            }
            var mundo = new MundoInfo
            {
                Id = "mundo-" + (maxNum + 1),
                Nombre = string.IsNullOrWhiteSpace(nombre) ? "Mundo " + (maxNum + 1) : nombre.Trim(),
                Creado = DateTime.Now,
                Actualizado = DateTime.Now,
                GameFolder = cfg.GameFolder ?? "",
                SiderFolder = cfg.SiderFolder ?? "",
                PesdbFolder = cfg.PesdbFolder ?? "",
                PaqueteDir = MundoRegistry.PackageDir(cfg),
                EditFile = cfg.EditFile ?? "",
                SavesFolder = "",
                Activo = false
            };
            string store = StoreDir(cfg, mundo.Id);
            Directory.CreateDirectory(store);
            if (desdeBaseLimpia)
            {
                string cpk = CpkExtractor.FindBestDatabaseCpk(cfg.GameFolder);
                if (string.IsNullOrEmpty(cpk) || !File.Exists(cpk))
                {
                    error = "No se encontró el CPK de la base en la carpeta del juego.";
                    try { Directory.Delete(store, true); } catch { }
                    return null;
                }
                CpkExtractor.ExtractPesDbFromCpk(cpk, store);
                try
                {
                    var tmp = new DatabaseManager();
                    tmp.ConfirmExternalWrite = delegate { return true; };
                    tmp.Load(store);
                    TakePhoto(tmp, mundo);
                }
                catch { /* la foto queda en cero: el mundo igual se crea */ }
            }
            else
            {
                string pkg = MundoRegistry.PackageDir(cfg);
                if (string.IsNullOrEmpty(pkg) || !Directory.Exists(pkg))
                {
                    error = "No se encontró la pesdb del paquete para clonar.";
                    try { Directory.Delete(store, true); } catch { }
                    return null;
                }
                MirrorCopy(pkg, store);
                var activo = mundos.FirstOrDefault(m => m.Activo) ?? mundos[0];
                mundo.TotalCompeticiones = activo.TotalCompeticiones;
                mundo.TotalEquipos = activo.TotalEquipos;
                mundo.TotalJugadores = activo.TotalJugadores;
                mundo.Ligas = activo.Ligas != null
                    ? activo.Ligas.Select(l => new MundoLiga { CompId = l.CompId, Nombre = l.Nombre, RegId = l.RegId, Equipos = l.Equipos }).ToList()
                    : new List<MundoLiga>();
            }
            mundos.Add(mundo);
            MundoRegistry.Save(cfg, mundos);
            return mundo;
        }

        public static bool SwitchTo(AppConfig cfg, DatabaseManager db, string targetId, out string error)
        {
            error = "";
            var mundos = MundoRegistry.Load(cfg);
            var target = mundos.FirstOrDefault(m => m.Id == targetId);
            if (target == null) { error = "Ese mundo no existe en el registro."; return false; }
            if (target.Activo) { error = "Ese mundo ya es el activo."; return false; }
            var activo = mundos.FirstOrDefault(m => m.Activo);
            if (activo == null) { error = "No hay un mundo activo desde el cual cambiar."; return false; }
            if (!string.IsNullOrEmpty(target.GameFolder) && !string.IsNullOrEmpty(cfg.GameFolder) &&
                !string.Equals(Path.GetFullPath(target.GameFolder).TrimEnd('\\'), Path.GetFullPath(cfg.GameFolder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            { error = "Ese mundo pertenece a otra instalación del juego."; return false; }
            if (!HasStore(cfg, target.Id)) { error = "Ese mundo no tiene una pesdb guardada todavía."; return false; }

            // 1) Foto de salida del mundo actual (nunca se pierde trabajo)
            Snapshot(cfg, db, activo);

            // 2) Intercambio de carreras: save <-> save_<mundo>
            string liveSave = !string.IsNullOrEmpty(activo.SavesFolder) ? activo.SavesFolder : MundoRegistry.DetectSavesFolder();
            if (!string.IsNullOrEmpty(liveSave))
            {
                string parent = Path.GetDirectoryName(liveSave);
                string slotOut = Path.Combine(parent, "save_" + activo.Id);
                string slotIn = Path.Combine(parent, "save_" + target.Id);
                if (Directory.Exists(liveSave) && Directory.Exists(slotOut))
                { error = "Ya existe la carpeta de carreras del mundo saliente (" + slotOut + "): revisala antes de cambiar."; return false; }
                if (Directory.Exists(liveSave)) Directory.Move(liveSave, slotOut);
                if (Directory.Exists(slotIn)) Directory.Move(slotIn, liveSave);
                else Directory.CreateDirectory(liveSave);
                activo.SavesFolder = slotOut;
                target.SavesFolder = liveSave;
            }

            // 3) La pesdb del entrante va al paquete
            MirrorCopy(StoreDir(cfg, target.Id), MundoRegistry.PackageDir(cfg));

            activo.Activo = false;
            target.Activo = true;
            target.Actualizado = DateTime.Now;
            MundoRegistry.Save(cfg, mundos);
            return true;
        }

        public static bool DeleteWorld(AppConfig cfg, string worldId, out string error)
        {
            error = "";
            var mundos = MundoRegistry.Load(cfg);
            var w = mundos.FirstOrDefault(m => m.Id == worldId);
            if (w == null) { error = "Ese mundo no existe en el registro."; return false; }
            if (w.Activo) { error = "El mundo activo no se puede borrar."; return false; }
            string trash = Path.Combine(SiderBackup.RootDir(cfg), "papelera", w.Id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            string store = Path.Combine(WorldsRoot(cfg), w.Id);
            if (Directory.Exists(store))
            {
                Directory.CreateDirectory(trash);
                Directory.Move(store, Path.Combine(trash, "mundo"));
            }
            if (!string.IsNullOrEmpty(w.SavesFolder) && Directory.Exists(w.SavesFolder) &&
                Path.GetFileName(w.SavesFolder).StartsWith("save_"))
            {
                Directory.CreateDirectory(trash);
                Directory.Move(w.SavesFolder, Path.Combine(trash, "save"));
            }
            mundos.Remove(w);
            MundoRegistry.Save(cfg, mundos);
            return true;
        }
    }
}
