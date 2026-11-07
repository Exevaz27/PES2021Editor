using System;

using System.Collections.Generic;

using System.Drawing;

using System.IO;

using System.Linq;

using System.Text;

using EditorLigasPes;

namespace PesEditorWeb

{

    // ========================================================================

    // EditorService: TODA la logica de la app actual, expuesta como acciones

    // para la interfaz web. Reutiliza sin cambios los archivos de logica

    // (DatabaseManager, PesDbModel, SiderLuaGenerator, LeaguePackageManager,

    // PesCrypt, CpkExtractor, SiderIniManager, AppConfig, PlayerBitHelper,

    // MinifaceProvider, GameEmblemProvider, ImageProcessor, WesysCompressor).

    //

    // Las operaciones que vivian mezcladas en MainForm (guardar con sync a

    // livecpk, asignar equipo a liga, desplegar Sider, competiciones custom)

    // estan portadas aca con la misma semantica.

    // ========================================================================

    public sealed class EditorService

    {

        private DatabaseManager _db;

        private AppConfig _cfg;

        private PesEditFile _edit;

        private bool _minifaceReady;

        public EditorService()

        {

            try { _cfg = AppConfig.Load(); } catch { _cfg = null; }

            if (_cfg == null) _cfg = new AppConfig();

            // Con solo la carpeta del juego, Sider se deriva solo (como en

            // el asistente del programa principal): si la config guardada

            // quedo sin Sider, se completa y se persiste al arrancar.

            try { if (AutoDeriveConfig()) _cfg.Save(); } catch { }

        }

        // ------------------------- utilidades -------------------------

        private static string GS(Dictionary<string, object> m, string k)

        {

            object v; if (m != null && m.TryGetValue(k, out v) && v != null) return v.ToString();

            return null;

        }

        private static string GS(Dictionary<string, object> m, string k, string def)

        {

            string v = GS(m, k);

            return string.IsNullOrWhiteSpace(v) ? def : v;

        }

        private static string GSNN(Dictionary<string, object> m, string k)

        {

            string v = GS(m, k);

            return string.IsNullOrWhiteSpace(v) ? null : v;

        }

        private static long GL(Dictionary<string, object> m, string k, long def)

        {

            object v; long r;

            if (m != null && m.TryGetValue(k, out v) && v != null && long.TryParse(v.ToString(), out r)) return r;

            return def;

        }

        private static int GI(Dictionary<string, object> m, string k, int def) { return (int)GL(m, k, def); }

        private static bool GB(Dictionary<string, object> m, string k, bool def)

        {

            object v; if (m != null && m.TryGetValue(k, out v) && v != null) { bool b; if (bool.TryParse(v.ToString(), out b)) return b; }

            return def;

        }

        private static Dictionary<string, object> GD(Dictionary<string, object> m, string k)

        {

            object v; if (m != null && m.TryGetValue(k, out v)) return v as Dictionary<string, object>;

            return null;

        }

        private static List<object> GA(Dictionary<string, object> m, string k)
        {
            // Robusto frente a cualquier representacion del array JSON que entregue
            // el deserializador (object[], List<object>, ArrayList, ...): se acepta
            // cualquier IEnumerable que no sea string. Antes solo se aceptaban
            // object[] y List<object>, y con ArrayList la lista volvia VACIA
            // (bug: Crear Liga recibia 0 equipos y el modelo rechazaba la lista).
            var r = new List<object>();
            object v;
            if (m != null && m.TryGetValue(k, out v) && v != null)
            {
                var en = v as System.Collections.IEnumerable;
                if (en != null && !(v is string))
                {
                    foreach (var o in en) r.Add(o);
                    return r;
                }
                r.Add(v);
            }
            return r;
        }

        private static List<uint> GUList(Dictionary<string, object> m, string k)

        {

            var r = new List<uint>();

            foreach (var o in GA(m, k)) { uint u; if (o != null && uint.TryParse(o.ToString(), out u)) r.Add(u); }

            return r;

        }

        private static Dictionary<string, object> Ok() { return new Dictionary<string, object> { { "ok", true } }; }

        private static Dictionary<string, object> Fail(string msg) { return new Dictionary<string, object> { { "ok", false }, { "error", msg } }; }

        private void RequireDb()

        {

            if (_db == null || _db.Competitions == null || _db.Competitions.Count == 0)

                throw new InvalidOperationException("No hay ninguna base de datos cargada. Cargá la pesdb desde Configuración o Inicio.");

        }

        private string CountryName(ushort id)

        {

            if (id == 0) return "Internacional";

            CountryRecord c;

            if (_db != null && _db.CountriesById != null && _db.CountriesById.TryGetValue(id, out c) && c != null)

            {

                string name = !string.IsNullOrWhiteSpace(c.NameSpanish) ? c.NameSpanish : c.NameEnglish;

                if (!string.IsNullOrWhiteSpace(name)) return name;

            }

            return "País " + id;

        }

        private RegulationRecord RegOf(byte compId)

        {

            RegulationRecord r;

            if (_db != null && _db.RegulationsByCompetition != null && _db.RegulationsByCompetition.TryGetValue(compId, out r)) return r;

            return null;

        }

        private bool IsCustomComp(CompetitionRecord c)

        {

            if (c == null) return false;

            if (c.IsNew) return true;

            var reg = RegOf(c.Id);

            return reg != null && reg.Id >= 179 && reg.Id < 1000;

        }

        private List<CompetitionRecord> GetCustomCompetitions()

        {

            // Portado 1:1 de MainForm.GetCustomCompetitions (M56).

            var customRegs = _db.Regulations.Where(r => r.Id >= 179 && r.Id < 1000).ToList();

            var customCompIds = new HashSet<byte>(customRegs.Select(r => r.IdCompetition));

            var customComps = _db.Competitions.Where(c => c.IsNew || customCompIds.Contains(c.Id)).OrderBy(c => c.Id).ToList();

            if (customComps.Count == 0)

            {

                customComps = _db.Competitions.Where(c => c.Id == 50 || c.Id == 51 || c.Id == 52 || c.Id == 53 || c.Id >= 150 || (c.Name ?? "").IndexOf("CLASSIC", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            }

            return customComps;

        }

        // ------------------------- DTOs -------------------------

        private Dictionary<string, object> CompDto(CompetitionRecord c)

        {

            var reg = RegOf(c.Id);

            int teamCount = 0; List<EntryRecord> ents;

            if (_db.EntriesByCompetition != null && _db.EntriesByCompetition.TryGetValue(c.Id, out ents) && ents != null) teamCount = ents.Count;

            ushort countryId = 0; try { countryId = _db.GetLeagueCountryId(c.Id); } catch { }

            int tier = 0; try { tier = _db.GetLeagueTier(c.Id); } catch { }

            byte type = reg != null ? reg.CompetitionType : (byte)0;

            return new Dictionary<string, object>

            {

                { "id", c.Id },

                { "regId", reg != null ? (int)reg.Id : 0 },

                { "name", reg != null && !string.IsNullOrEmpty(reg.NameSpanish) ? reg.NameSpanish : (c.Name ?? "") },

                { "nameSpanish", reg != null ? (reg.NameSpanish ?? "") : "" },

                { "nameEnglish", reg != null ? (reg.NameEnglish ?? "") : "" },

                { "nameLatam", reg != null ? (reg.NameLatamSpanish ?? "") : "" },

                { "nameAscii", c.Name ?? "" },

                { "type", type },

                { "typeName", CompTypeName(type) },

                { "isLeague", type == 0 || type == 4 },

                { "totalTeams", reg != null ? (int)reg.TotalTeams : teamCount },

                { "teamCount", teamCount },

                { "countryId", countryId },

                { "countryName", countryId != 0 ? CountryName(countryId) : "" },

                { "continent", c.Continent },

                { "tier", tier },

                { "isNew", c.IsNew },

                { "isCustom", IsCustomComp(c) },

                { "protected", DatabaseManager.IsProtectedVanillaCompetition(c.Id) }

            };

        }

        private static string CompTypeName(byte t)

        {

            switch (t) { case 0: return "Liga"; case 1: return "Copa"; case 2: return "Supercopa"; case 4: return "Liga"; case 3: return "Copa"; case 5: case 7: return "Supercopa"; default: return "Tipo " + t; }

        }

        private Dictionary<string, object> TeamDto(TeamRecord t)

        {

            byte compId; string compName;

            string leagueName = ""; int leagueId = 0;

            if (_db.IsTeamOccupied(t.Id, out compId, out compName)) { leagueId = compId; leagueName = compName ?? ""; }

            byte dCompId; string dCompName;

            string domesticLeagueName = ""; int domesticLeagueId = 0;

            if (_db.IsTeamInDomesticLeague(t.Id, 0, out dCompId, out dCompName)) { domesticLeagueId = dCompId; domesticLeagueName = dCompName ?? ""; }

            string dispName = t.DisplayName;

            string nameSp = !string.IsNullOrWhiteSpace(t.NameSpanish) ? t.NameSpanish : dispName;

            string nameEn = !string.IsNullOrWhiteSpace(t.NameEnglish) ? t.NameEnglish : dispName;

            string shortNm = !string.IsNullOrWhiteSpace(t.ShortName) ? t.ShortName : (dispName.Length >= 3 ? dispName.Substring(0, 3).ToUpper() : dispName);

            return new Dictionary<string, object>

            {

                { "id", t.Id },

                { "name", dispName },

                { "nameSpanish", nameSp },

                { "nameEnglish", nameEn },

                { "nameDatabase", t.NameDatabase ?? "" },

                { "shortName", shortNm },

                { "countryId", t.CountryId },

                { "countryName", CountryName(t.CountryId) },

                { "isNational", t.IsNational },

                { "isNew", t.IsNew },

                { "leagueId", leagueId },

                { "leagueName", leagueName },
                { "domesticLeagueId", domesticLeagueId },
                { "domesticLeagueName", domesticLeagueName },

                { "playerCount", _db.GetPlayerCount(t.Id) },

                { "stadiumId", t.StadiumId },

                { "coachId", t.CoachId },

                { "callnameId", t.CallnameId }

            };

        }

        private Dictionary<string, object> PlayerLiteDto(PlayerRecord p)

        {

            string clubName = "Agente Libre"; TeamRecord ct;

            if (p.ClubId != 0 && _db.TeamsById != null && _db.TeamsById.TryGetValue(p.ClubId, out ct) && ct != null) clubName = ct.NameSpanish ?? "";

            return new Dictionary<string, object>

            {

                { "id", p.Id },

                { "name", p.Name ?? "" },

                { "shirtName", p.ShirtName ?? "" },

                { "pos", p.PositionName ?? "" },

                { "posEs", p.PositionNameEs },

                { "posIndex", p.PositionIndex },

                { "ovr", p.Overall },

                { "age", p.Age },

                { "clubId", p.ClubId },

                { "clubName", clubName },

                { "natId", PlayerBitHelper.GetNationalityId(p.RawBytes) },

                { "natName", CountryName(PlayerBitHelper.GetNationalityId(p.RawBytes)) }

            };

        }

        // ------------------------- dispatch -------------------------

        public Dictionary<string, object> Handle(string action, Dictionary<string, object> msg)

        {

            switch (action)

            {

                // estado / config

                case "getState": return GetState();

                case "getHealth": return GetHealth();

                case "saveConfig": return SaveConfig(msg);

                case "deriveFromGame": return DeriveFromGame(msg);

                case "scanEditFiles": return ScanEditFiles();

                case "detectGames": return DetectGames();

                case "extractPesdb": return ExtractPesdb(msg);

                case "rescanMinifaces": return RescanMinifaces();

                case "openFolder": return OpenFolder(msg);

                // base de datos

                case "loadDb": return LoadDb(msg);

                case "saveDb": return SaveDb();

                // competiciones

                case "listCompetitions": return ListCompetitions(msg);

                case "getCompetition": return GetCompetition(msg);

                case "updateCompetition": return UpdateCompetition(msg);
                case "applyLeagueTemplate": return ApplyLeagueTemplate(msg);

                case "deleteCompetition": return DeleteCompetition(msg);

                case "createLeagueOptions": return CreateLeagueOptions();

                case "createLeague": return CreateLeague(msg);

                case "addTeamToCompetition": return AddTeamToCompetition(msg);

                case "removeTeamFromCompetition": return RemoveTeamFromCompetition(msg);

                case "exportLeaguePackage": return Fail("Usar exportLeaguePackageFile (con diálogo).");

                // equipos

                case "listTeams": return ListTeams(msg);

                case "getTeam": return GetTeam(msg);

                case "updateTeam": return UpdateTeam(msg);

                case "createTeam": return CreateTeam(msg);
                case "createTeamOptions": return CreateTeamOptions();

                case "cloneTeam": return CloneTeam(msg);

                case "deleteTeam": return DeleteTeam(msg);

                case "batchCreateTeams": return BatchCreateTeams(msg);

                case "generateTeams": return GenerateTeams(msg);

                case "assignTeamToLeague": return AssignTeamToLeague(msg);

                case "applyFormation": return ApplyFormation(msg);

                case "setTeamStrategy": return SetTeamStrategy(msg);

                case "setTacticalPosition": return SetTacticalPosition(msg);

                case "swapSquadPlayers": return SwapSquadPlayers(msg);

                case "setStarterRole": return SetStarterRole(msg);

                // plantilla

                case "getRoster": return GetRoster(msg);

                case "setShirtNumber": return SetShirtNumber(msg);

                case "transferPlayer": return TransferPlayer(msg);

                case "releasePlayer": return ReleasePlayer(msg);

                case "signFreeAgent": return SignFreeAgent(msg);

                case "autoAssignRoles": return AutoAssignRoles(msg);

                case "setSetPieceRoles": return SetSetPieceRoles(msg);

                case "freeAgents": return FreeAgents(msg);

                // jugadores

                case "listPlayers": return ListPlayers(msg);

                case "getPlayer": return GetPlayer(msg);

                case "updatePlayer": return UpdatePlayer(msg);

                case "createPlayer": return CreatePlayer(msg);
                case "createPlayerOptions": return CreatePlayerOptions();

                case "clonePlayer": return ClonePlayer(msg);

                case "deletePlayer": return DeletePlayer(msg);

                // piramide

                case "getPyramids": return GetPyramids();
                case "getPyramidFreeTeams": return GetPyramidFreeTeams(msg);
                case "setPyramidParent": return SetPyramidParent(msg);

                // sider

                case "listRestorePoints": return ListRestorePoints();

                case "createRestorePoint": return CreateRestorePoint(msg);

                case "restoreRestorePoint": return RestoreRestorePoint(msg);

                case "listProfiles": return ListProfiles();

                case "saveProfile": return SaveProfile(msg);

                case "applyProfile": return ApplyProfile(msg);

                case "deleteProfile": return DeleteProfile(msg);

                case "fixHealthItem": return FixHealthItem(msg);

                case "getWorld": return GetWorld();

                case "registerWorld": return RegisterWorld(msg);

                case "listWorlds": return ListWorlds();

                case "createWorld": return CreateWorld(msg);

                case "switchWorld": return SwitchWorld(msg);

                case "deleteWorld": return DeleteWorld(msg);

                case "getServerMap": return ServerMapPayload(msg);

                case "setServerMapEntry": return SetServerMapEntry(msg);

                case "removeServerMapEntry": return RemoveServerMapEntry(msg);

                case "listTeamOptions": return ListTeamOptions();

                case "renameWorld": return RenameWorld(msg);

                case "getCensus": return GetCensus();

                case "listPresets": return ListPresets();

                case "previewPreset": return PreviewPreset(msg);

                case "applyPreset": return ApplyPreset(msg);

                case "previewSquad": return PreviewSquad(msg);

                case "applySquad": return ApplySquad(msg);

                case "previewNewLife": return PreviewNewLife(msg);

                case "applyNewLife": return ApplyNewLife(msg);

                case "previewCsvPlayers": return PreviewCsvPlayers(msg);

                case "applyCsvPlayers": return ApplyCsvPlayers(msg);

                case "getSider": return GetSider();
                case "getIdScan": return GetIdScan();
                case "repairLiveCpkEmblems": return RepairLiveCpkEmblems();

                case "setSiderModule": return SetSiderModule(msg);

                case "deploySider": return DeploySider();

                case "setUefaSwissFormat": return SetUefaSwissFormat(msg);

                case "getLuaPreview": return GetLuaPreview();

                case "saveLua": return SaveLua();

                case "getWorldTxt": return GetWorldTxt();

                // emblemas e imagenes

                case "teamEmblem": return GetEmblem(msg, true);

                case "compEmblem": return GetEmblem(msg, false);

                case "setTeamEmblem": return SetEmblem(msg, true);

                case "setCompEmblem": return SetEmblem(msg, false);

                case "deleteCompEmblem": return DeleteCompEmblem(msg);

                case "playerMiniface": return PlayerMiniface(msg);

                case "setTeamEmblemFile": return SetEmblemFile(GS(msg, "path"), GL(msg, "id", 0), true);

                case "setCompEmblemFile": return SetEmblemFile(GS(msg, "path"), GL(msg, "id", 0), false);
                case "listGameEmblems": return ListGameEmblems();
                case "setCompEmblemFromGame": return SetCompEmblemFromGame(msg);

                // EDIT

                case "editLoad": return EditLoad(msg);

                case "editSave": return EditSave();

                case "editUnlock": return EditUnlock();

                case "editSetSid": return EditSetSid(msg);

                case "editSetDescription": return EditSetDescription(msg);

                case "editRestoreBackup": return EditRestoreBackup(msg);

                case "editInspectFl26": return EditInspectFl26(msg);

                default: return Fail("Acción no implementada: " + action);

            }

        }

        // ------------------------- estado / config -------------------------

        private Dictionary<string, object> ConfigDto()

        {

            return new Dictionary<string, object>

            {

                { "gameFolder", _cfg.GameFolder ?? "" },

                { "siderFolder", _cfg.SiderFolder ?? "" },

                { "liveCpkFolder", _cfg.LiveCpkFolder ?? "" },

                { "modulesFolder", _cfg.ModulesFolder ?? "" },

                { "contentFolder", _cfg.ContentFolder ?? "" },

                { "pesdbFolder", _cfg.PesdbFolder ?? "" },

                { "editFile", _cfg.EditFile ?? "" },

                { "minifaceFolder", (_cfg != null ? _cfg.GetOrDetectMinifaceFolder(true) : "") ?? "" },

                { "minifaceCount", MinifaceProvider.IndexedCount },

                { "minifaceFolderExists", !string.IsNullOrEmpty(_cfg != null ? _cfg.GetOrDetectMinifaceFolder(false) : null) && Directory.Exists(_cfg.GetOrDetectMinifaceFolder(false)) },

                { "isComplete", _cfg.IsComplete }

            };

        }

        private Dictionary<string, object> GetState()

        {

            var r = Ok();

            r["config"] = ConfigDto();

            bool loaded = _db != null && _db.Competitions != null && _db.Competitions.Count > 0;

            r["dbLoaded"] = loaded;

            if (loaded)

            {

                r["stats"] = new Dictionary<string, object>

                {

                    { "competitions", _db.Competitions.Count },

                    { "leagues", _db.Competitions.Count(c => { var reg = RegOf(c.Id); return reg != null && (reg.CompetitionType == 0 || reg.CompetitionType == 4); }) },

                    { "cups", _db.Competitions.Count(c => { var reg = RegOf(c.Id); return reg != null && !(reg.CompetitionType == 0 || reg.CompetitionType == 4); }) },

                    { "teams", _db.Teams.Count },

                    { "players", _db.Players.Count },

                    { "customLeagues", GetCustomCompetitions().Count },

                    { "hasUnsavedChanges", _db.HasUnsavedChanges }

                };

                r["dbPath"] = _db.DatabasePath ?? "";

            }

            r["editLoaded"] = _edit != null;

            return r;

        }

        // Fase 1 FL26: diagnostico de solo lectura (HealthCheck.cs).

        private Dictionary<string, object> GetHealth()

        {

            var rep = HealthChecker.Run(_cfg, _db);

            var items = new List<Dictionary<string, object>>();

            foreach (var it in rep.Items)

            {

                items.Add(new Dictionary<string, object>

                {

                    { "id", it.Id }, { "grupo", it.Grupo }, { "nivel", it.Nivel.ToString() },

                    { "titulo", it.Titulo }, { "detalle", it.Detalle ?? "" }, { "sugerencia", it.Sugerencia ?? "" }, { "fix", FixFor(it.Id) }

                });

            }

            var first = rep.PrimerProblema;

            var r = Ok();

            r["resumen"] = rep.Resumen;

            r["veredicto"] = rep.Veredicto.ToString();

            r["errores"] = rep.CountErrors;

            r["avisos"] = rep.CountAvisos;

            r["primerId"] = first == null ? "" : first.Id;

            r["primerTitulo"] = first == null ? "" : first.Titulo;

            r["items"] = items;

            r["texto"] = rep.ToText();

            return r;

        }

        private Dictionary<string, object> SaveConfig(Dictionary<string, object> msg)

        {

            _cfg.GameFolder = GSNN(msg, "gameFolder") ?? _cfg.GameFolder;

            _cfg.SiderFolder = GSNN(msg, "siderFolder") ?? _cfg.SiderFolder;

            _cfg.LiveCpkFolder = GSNN(msg, "liveCpkFolder") ?? _cfg.LiveCpkFolder;

            _cfg.ModulesFolder = GSNN(msg, "modulesFolder") ?? _cfg.ModulesFolder;

            _cfg.ContentFolder = GSNN(msg, "contentFolder") ?? _cfg.ContentFolder;

            _cfg.PesdbFolder = GSNN(msg, "pesdbFolder") ?? _cfg.PesdbFolder;

            _cfg.EditFile = GSNN(msg, "editFile") ?? _cfg.EditFile;

            _cfg.MinifaceFolder = GSNN(msg, "minifaceFolder") ?? _cfg.MinifaceFolder;

            AutoDeriveConfig();

            _cfg.Save();

            var r = Ok(); r["config"] = ConfigDto(); return r;

        }

        // Rellena los huecos de rutas derivandolos de la carpeta del

        // juego: Sider (sider o SiderAddons dentro del juego) y de el

        // livecpk / modules / content; y la pesdb si ya existe el

        // paquete PruebaLigas. Devuelve true si cambio algo.

        private bool AutoDeriveConfig()

        {

            bool changed = false;

            if (_cfg == null) return false;

            string game = _cfg.GameFolder;

            if (!string.IsNullOrWhiteSpace(game) && Directory.Exists(game)

                && (string.IsNullOrWhiteSpace(_cfg.SiderFolder) || !Directory.Exists(_cfg.SiderFolder)))

            {

                foreach (var cand in new[] { "sider", "SiderAddons" })

                {

                    string p = Path.Combine(game, cand);

                    if (Directory.Exists(p)) { _cfg.SiderFolder = p; changed = true; break; }

                }

            }

            if (!string.IsNullOrWhiteSpace(_cfg.SiderFolder) && Directory.Exists(_cfg.SiderFolder))

            {

                if (string.IsNullOrWhiteSpace(_cfg.LiveCpkFolder)) { _cfg.LiveCpkFolder = Path.Combine(_cfg.SiderFolder, "livecpk"); changed = true; }

                if (string.IsNullOrWhiteSpace(_cfg.ModulesFolder)) { _cfg.ModulesFolder = Path.Combine(_cfg.SiderFolder, "modules"); changed = true; }

                if (string.IsNullOrWhiteSpace(_cfg.ContentFolder)) { _cfg.ContentFolder = Path.Combine(_cfg.SiderFolder, "content"); changed = true; }

                if (string.IsNullOrWhiteSpace(_cfg.PesdbFolder))

                {

                    string pkg = Path.Combine(_cfg.SiderFolder, "livecpk", "PruebaLigas", "common", "etc", "pesdb");

                    if (File.Exists(Path.Combine(pkg, "Competition.bin"))) { _cfg.PesdbFolder = pkg; changed = true; }

                }

            }

            return changed;

        }

        private Dictionary<string, object> DeriveFromGame(Dictionary<string, object> msg)

        {

            string game = GS(msg, "gameFolder");

            if (string.IsNullOrEmpty(game) || !Directory.Exists(game)) return Fail("La carpeta del juego no existe.");

            string sider = null;

            foreach (var cand in new[] { "sider", "SiderAddons" })

            {

                string p = Path.Combine(game, cand);

                if (Directory.Exists(p)) { sider = p; break; }

            }

            var r = Ok();

            r["gameFolder"] = game;

            r["siderFolder"] = sider ?? "";

            r["liveCpkFolder"] = sider != null ? Path.Combine(sider, "livecpk") : "";

            r["modulesFolder"] = sider != null ? Path.Combine(sider, "modules") : "";

            r["contentFolder"] = sider != null ? Path.Combine(sider, "content") : "";

            string pkg = sider != null ? Path.Combine(sider, "livecpk", "PruebaLigas", "common", "etc", "pesdb") : "";

            r["pesdbPackage"] = pkg;

            r["pesdbPackageExists"] = !string.IsNullOrEmpty(pkg) && File.Exists(Path.Combine(pkg, "Competition.bin"));

            string cpk = null; try { cpk = CpkExtractor.FindBestDatabaseCpk(game); } catch { }

            r["bestCpk"] = cpk ?? "";

            return r;

        }

        private Dictionary<string, object> ScanEditFiles()

        {

            var files = PesCrypt.FindDefaultEditFiles() ?? new List<string>();

            var r = Ok();

            r["files"] = files.Cast<object>().ToList();

            return r;

        }

        private Dictionary<string, object> DetectGames()

        {

            var found = CpkExtractor.DetectGameInstallations() ?? new List<string>();

            var r = Ok(); r["games"] = found.Cast<object>().ToList(); return r;

        }

        private Dictionary<string, object> ExtractPesdb(Dictionary<string, object> msg)

        {

            string cpk = GS(msg, "cpkPath");

            string game = GS(msg, "gameFolder");

            string target = GS(msg, "targetFolder");

            if (string.IsNullOrEmpty(cpk) && !string.IsNullOrEmpty(game)) cpk = CpkExtractor.FindBestDatabaseCpk(game);

            if (string.IsNullOrEmpty(cpk) || !File.Exists(cpk)) return Fail("No se encontró el CPK de la base de datos.");

            if (string.IsNullOrEmpty(target)) return Fail("Falta la carpeta destino.");

            int n = CpkExtractor.ExtractPesDbFromCpk(cpk, target);

            var r = Ok(); r["files"] = n; r["cpk"] = cpk; r["target"] = target; return r;

        }

        // ------------------------- base de datos -------------------------

        private Dictionary<string, object> LoadDb(Dictionary<string, object> msg)

        {

            string folder = GS(msg, "folder");

            if (string.IsNullOrEmpty(folder)) folder = _cfg.PesdbFolder;

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Fail("La carpeta pesdb no existe: " + folder);

            _db = new DatabaseManager();

            _db.ConfirmExternalWrite = delegate { return true; };

            _db.Load(folder);

            var r = Ok();

            var warnings = new List<object>();

            try { foreach (var w in _db.LoadWarnings) warnings.Add(w); } catch { }

            r["warnings"] = warnings;

            var st = GetState(); r["stats"] = st["stats"]; r["dbPath"] = _db.DatabasePath ?? folder;

            return r;

        }

        private Dictionary<string, object> SaveDb()

        {

            RequireDb();

            string folder = _cfg.PesdbFolder;

            if (string.IsNullOrEmpty(folder)) folder = _db.DatabasePath;

            if (string.IsNullOrEmpty(folder)) return Fail("No hay carpeta pesdb configurada.");

            // Secuencia de la app actual (MainForm.SaveDatabase): guardar en

            // la pesdb de trabajo, reglas custom, tacticas y formaciones.

            _db.Save(folder);

            try { _db.SaveCustomLeagueRules(folder); } catch { }

            try { _db.SaveTactics(folder); } catch { }

            try { _db.SaveTacticsFormations(folder); } catch { }

            // Sync al paquete livecpk (Sider solo monta subcarpetas): con

            // .bak por archivo, igual que la app actual (fix v3-fix6).

            int copied = 0; string pkgDir = null;

            if (!string.IsNullOrEmpty(_cfg.LiveCpkFolder) && Directory.Exists(_cfg.LiveCpkFolder))

            {

                pkgDir = _cfg.LiveCpkFolder;

                string norm = pkgDir.Replace('/', '\\').TrimEnd('\\');

                if (!norm.EndsWith("\\PruebaLigas\\common\\etc\\pesdb", StringComparison.OrdinalIgnoreCase))

                    pkgDir = Path.Combine(_cfg.LiveCpkFolder, "PruebaLigas", "common", "etc", "pesdb");

                string srcFull = Path.GetFullPath(folder).TrimEnd('\\');

                string dstFull = Path.GetFullPath(pkgDir).TrimEnd('\\');

                if (!string.Equals(srcFull, dstFull, StringComparison.OrdinalIgnoreCase))

                {

                    Directory.CreateDirectory(pkgDir);

                    string[] files = { "Competition.bin", "CompetitionRegulation.bin", "CompetitionEntry.bin", "Team.bin", "Player.bin", "PlayerAssignment.bin", "Tactics.bin", "TacticsFormation.bin", "Coach.bin", "Stadium.bin", "Derby.bin" };

                    foreach (var f in files)

                    {

                        string src = Path.Combine(folder, f), dst = Path.Combine(pkgDir, f);

                        if (File.Exists(src))

                        {

                            if (File.Exists(dst)) { try { File.Copy(dst, dst + ".bak", true); } catch { } }

                            File.Copy(src, dst, true); copied++;

                        }

                    }

                }

            }

            // Regenerar league_manager.lua si ya existe en modules.

            bool luaSynced = false;

            if (!string.IsNullOrEmpty(_cfg.ModulesFolder))

            {

                string luaPath = Path.Combine(_cfg.ModulesFolder, "league_manager.lua");

                if (File.Exists(luaPath))

                {

                    SiderLuaGenerator.SaveLuaToFile(luaPath, GetCustomCompetitions(), _db.Regulations, _db);

                    luaSynced = true;

                }

            }

            _db.HasUnsavedChanges = false;

            var r = Ok();

            r["folder"] = folder; r["packageDir"] = pkgDir ?? ""; r["filesCopied"] = copied; r["luaSynced"] = luaSynced;

            return r;

        }

        // ------------------------- competiciones -------------------------

        private Dictionary<string, object> ListCompetitions(Dictionary<string, object> msg)

        {

            RequireDb();

            string filter = GS(msg, "filter") ?? "all";

            string search = (GS(msg, "search") ?? "").ToLowerInvariant();

            var list = new List<object>();

            foreach (var c in _db.Competitions.OrderBy(c => c.Id))

            {

                var dto = CompDto(c);

                bool isLeague = (bool)dto["isLeague"];

                if (filter == "leagues" && !isLeague) continue;

                if (filter == "cups" && isLeague) continue;

                if (search.Length > 0)

                {

                    string hay = ((string)dto["nameSpanish"] + " " + (string)dto["nameEnglish"] + " " + (string)dto["nameAscii"]).ToLowerInvariant();

                    if (!hay.Contains(search)) continue;

                }

                list.Add(dto);

            }

            var r = Ok(); r["competitions"] = list; r["total"] = list.Count; return r;

        }

        private Dictionary<string, object> GetCompetition(Dictionary<string, object> msg)

        {

            RequireDb();

            byte id = (byte)GI(msg, "id", -1);

            var comp = _db.Competitions.FirstOrDefault(c => c.Id == id);

            if (comp == null) return Fail("Competición no encontrada: " + id);

            var reg = RegOf(id);

            var dto = CompDto(comp);

            if (reg != null)

            {

                dto["changes"] = (int)reg.Changes;

                dto["registeredPlayers"] = (int)reg.RegisteredPlayers;

                dto["rounds"] = (int)reg.LeagueEachTeamMatches;

                dto["cupFormat"] = (int)reg.CupFormat;
            dto["tieBreaker"] = (int)reg.LeagueClassification;

                dto["numGroups"] = (int)reg.NumGroups;

                dto["numClassified"] = (int)reg.NumClassifiedTeams;

                dto["extraTime"] = reg.ExtraTime;

                dto["penalties"] = reg.PenaltyShootout;

                dto["awayGoals"] = reg.AwayGoals;

                dto["thirdPlace"] = reg.ThirdPlace;

                dto["singleLeg"] = reg.SingleLeg;

                dto["finalChampionship"] = reg.FinalChampionship;

            }

            dto["promote"] = _db.GetLeaguePromote(id);

            dto["demote"] = _db.GetLeagueDemote(id);

            dto["continental1"] = _db.GetLeagueContinental1(id);

            dto["continental2"] = _db.GetLeagueContinental2(id);

            dto["seasonType"] = (int)_db.GetLeagueSeasonType(id);

            dto["aperturaClausura"] = _db.GetLeagueAperturaClausura(id);

            dto["clausuraRegulation"] = (int)_db.GetLeagueClausuraRegulation(id);

            dto["linkedCup"] = (int)_db.GetLeagueLinkedCup(id);

            dto["linkedSuperCup"] = (int)_db.GetLeagueLinkedSuperCup(id);

            var teams = new List<object>();

            List<EntryRecord> ents;

            if (_db.EntriesByCompetition != null && _db.EntriesByCompetition.TryGetValue(id, out ents) && ents != null)

            {

                foreach (var e in ents.OrderBy(x => x.Position))

                {

                    TeamRecord t;

                    if (_db.TeamsById.TryGetValue((uint)e.TeamId, out t) && t != null)

                        teams.Add(new Dictionary<string, object> { { "id", t.Id }, { "name", t.NameSpanish ?? "" }, { "shortName", t.ShortName ?? "" }, { "position", (int)e.Position } });

                    else

                        teams.Add(new Dictionary<string, object> { { "id", e.TeamId }, { "name", "(equipo " + e.TeamId + ")" }, { "shortName", "" }, { "position", (int)e.Position } });

                }

            }

            dto["teams"] = teams;

            var cups = new List<object>();

            foreach (var c in _db.GetAvailableDomesticCups((ushort)GI(dto, "countryId", 0))) { var cr = RegOf(c.Id); cups.Add(new Dictionary<string, object> { { "compId", c.Id }, { "regId", cr != null ? (int)cr.Id : 0 }, { "name", cr != null ? (cr.NameSpanish ?? c.Name) : c.Name } }); }

            dto["availableCups"] = cups;

            var scups = new List<object>();

            foreach (var c in _db.GetAvailableSuperCups((ushort)GI(dto, "countryId", 0))) { var cr = RegOf(c.Id); scups.Add(new Dictionary<string, object> { { "compId", c.Id }, { "regId", cr != null ? (int)cr.Id : 0 }, { "name", cr != null ? (cr.NameSpanish ?? c.Name) : c.Name } }); }

            dto["availableSuperCups"] = scups;

            int parentCompId = 0;

            try { parentCompId = (int)_db.GetLeagueParentCompId(id); } catch { }

            dto["parentCompId"] = parentCompId;

            var parents = new List<object>();

            if (_db.Competitions != null)

            {

                foreach (var c in _db.Competitions.Where(x => x.Id != id).OrderBy(x => x.Name))

                {

                    var cd = CompDto(c);

                    if ((bool)cd["isLeague"]) parents.Add(cd);

                }

            }

            dto["parentLeagues"] = parents;

            var r = Ok(); r["competition"] = dto; return r;

        }

        private Dictionary<string, object> UpdateCompetition(Dictionary<string, object> msg)

        {

            RequireDb();

            byte id = (byte)GI(msg, "id", -1);

            var comp = _db.Competitions.FirstOrDefault(c => c.Id == id);

            var reg = RegOf(id);

            if (comp == null || reg == null) return Fail("Competición o reglamento no encontrado.");

            string v;

            if ((v = GS(msg, "nameSpanish")) != null) reg.NameSpanish = v;

            if ((v = GS(msg, "nameEnglish")) != null) reg.NameEnglish = v;

            if ((v = GS(msg, "nameLatam")) != null) reg.NameLatamSpanish = v;

            if ((v = GS(msg, "nameAscii")) != null) comp.Name = v;

            if (msg.ContainsKey("totalTeams")) reg.TotalTeams = (byte)GI(msg, "totalTeams", reg.TotalTeams);

            if (msg.ContainsKey("changes")) reg.Changes = (byte)GI(msg, "changes", reg.Changes);

            if (msg.ContainsKey("registeredPlayers")) reg.RegisteredPlayers = (byte)GI(msg, "registeredPlayers", reg.RegisteredPlayers);

            if (msg.ContainsKey("rounds")) reg.LeagueEachTeamMatches = (byte)GI(msg, "rounds", reg.LeagueEachTeamMatches);

            if (msg.ContainsKey("cupFormat")) reg.CupFormat = (byte)GI(msg, "cupFormat", reg.CupFormat);

            if (msg.ContainsKey("numGroups")) reg.NumGroups = (byte)GI(msg, "numGroups", reg.NumGroups);

            if (msg.ContainsKey("numClassified")) reg.NumClassifiedTeams = (ushort)GI(msg, "numClassified", reg.NumClassifiedTeams);

            if (msg.ContainsKey("extraTime")) reg.ExtraTime = GB(msg, "extraTime", reg.ExtraTime);

            if (msg.ContainsKey("penalties")) reg.PenaltyShootout = GB(msg, "penalties", reg.PenaltyShootout);

            if (msg.ContainsKey("awayGoals")) reg.AwayGoals = GB(msg, "awayGoals", reg.AwayGoals);

            if (msg.ContainsKey("thirdPlace")) reg.ThirdPlace = GB(msg, "thirdPlace", reg.ThirdPlace);

            if (msg.ContainsKey("singleLeg")) reg.SingleLeg = GB(msg, "singleLeg", reg.SingleLeg);

            if (msg.ContainsKey("finalChampionship")) reg.FinalChampionship = GB(msg, "finalChampionship", reg.FinalChampionship);

            if (msg.ContainsKey("countryId")) _db.SetLeagueCountry(id, (ushort)GI(msg, "countryId", 0));

            if (msg.ContainsKey("promote")) _db.SetLeaguePromote(id, GI(msg, "promote", 0));

            if (msg.ContainsKey("demote")) _db.SetLeagueDemote(id, GI(msg, "demote", 0));

            if (msg.ContainsKey("continental1")) _db.SetLeagueContinental1(id, GI(msg, "continental1", 0));

            if (msg.ContainsKey("continental2")) _db.SetLeagueContinental2(id, GI(msg, "continental2", 0));

            if (msg.ContainsKey("seasonType")) _db.SetLeagueSeasonType(id, (byte)GI(msg, "seasonType", 0));

            if (msg.ContainsKey("linkedCup")) _db.SetLeagueLinkedCup(id, (ushort)GI(msg, "linkedCup", 0));

            if (msg.ContainsKey("linkedSuperCup")) _db.SetLeagueLinkedSuperCup(id, (ushort)GI(msg, "linkedSuperCup", 0));

            if (msg.ContainsKey("parentCompId")) _db.SetLeagueParent(id, (byte)GI(msg, "parentCompId", 0));

            if (msg.ContainsKey("aperturaClausura"))

            {

                bool apClau = GB(msg, "aperturaClausura", false);

                ushort clReg = (ushort)GI(msg, "clausuraRegulation", 0);

                _db.SetLeagueAperturaClausura(id, apClau, clReg);

            }

            reg.IsModified = true; comp.IsModified = true; _db.HasUnsavedChanges = true;

            var r = Ok(); r["competition"] = CompDto(comp); return r;

        }

        private Dictionary<string, object> DeleteCompetition(Dictionary<string, object> msg)

        {

            RequireDb();

            byte id = (byte)GI(msg, "id", -1);

            if (DatabaseManager.IsProtectedVanillaCompetition(id))

                return Fail("Es una competición original protegida: no se puede eliminar.");

            bool okDel = _db.DeleteLeague(id);

            if (!okDel) return Fail("No se pudo eliminar la competición.");

            var r = Ok(); r["deleted"] = true; return r;

        }

        private Dictionary<string, object> CountriesDto()

        {

            var list = new List<object>();

            foreach (var c in _db.Countries.OrderBy(c => c.NameSpanish))

                list.Add(new Dictionary<string, object> { { "id", c.Id }, { "name", c.NameSpanish ?? c.NameEnglish ?? "" }, { "continent", c.Continent } });

            var r = new Dictionary<string, object>(); r["countries"] = list; return r;

        }

        private Dictionary<string, object> CreateLeagueOptions()

        {

            RequireDb();

            var r = Ok();

            var ids = new List<object>(); foreach (var b in _db.GetAvailableCompetitionIds()) ids.Add((int)b);

            r["availableIds"] = ids;

            r["countries"] = CountriesDto()["countries"];

            var parents = new List<object>();

            foreach (var c in _db.Competitions.OrderBy(c => c.Id)) { var d = CompDto(c); if ((bool)d["isLeague"]) parents.Add(d); }

            r["parentLeagues"] = parents;

            var cups = new List<object>(); var scups = new List<object>();
            foreach (var c in _db.Competitions.OrderBy(c => c.Id))
            {
                var d = CompDto(c); int t = Convert.ToInt32(d["type"]);
                if (t != 1 && t != 2) continue;
                var item = new Dictionary<string, object> { { "id", c.Id }, { "name", (string)d["name"] }, { "countryId", Convert.ToInt32(d["countryId"]) } };
                if (t == 1) cups.Add(item); else scups.Add(item);
            }
            r["cups"] = cups; r["superCups"] = scups;
            var free = new List<object>();

            foreach (var t in _db.GetFreePlayableTeams(0)) free.Add(TeamDto(t));

            r["freeTeams"] = free;

            var allTeams = new List<object>();

            foreach (var t in _db.Teams.OrderBy(t => t.DisplayName)) allTeams.Add(TeamDto(t));

            r["allTeams"] = allTeams;

            return r;

        }

        private Dictionary<string, object> CreateLeague(Dictionary<string, object> msg)

        {

            RequireDb();

            byte compId = (byte)GI(msg, "compId", -1);
            // Opcion B del escritorio: autogenerar equipos nuevos (Team.bin) para esta liga.
            var teamIdsDyn = GUList(msg, "teamIds");
            if (teamIdsDyn.Count == 0)
            {
                // Canal redundante: el front tambien manda los IDs como CSV.
                string csvIds = GS(msg, "teamIdsCsv");
                if (!string.IsNullOrEmpty(csvIds))
                {
                    foreach (var part in csvIds.Split(','))
                    {
                        uint uu; if (uint.TryParse(part.Trim(), out uu)) teamIdsDyn.Add(uu);
                    }
                }
            }
            int genCount = GI(msg, "genCount", 0);
            if (genCount > 0)
            {
                string prefix = GS(msg, "genPrefix");
                var created = _db.GenerateAndAddCustomTeams(genCount, string.IsNullOrEmpty(prefix) ? "Club" : prefix, (ushort)GI(msg, "countryId", 0));
                teamIdsDyn = new List<uint>();
                foreach (var t in created) teamIdsDyn.Add(t.Id);
            }
            // Guarda de ocupados: ningun equipo puede estar ya en otra liga domestica.
            var occupiedMsgs = new List<string>();
            foreach (var tid in teamIdsDyn)
            {
                byte ocId; string ocName;
                if (_db.IsTeamInDomesticLeague(tid, 0, out ocId, out ocName))
                {
                    var tt = _db.TeamsById != null && _db.TeamsById.ContainsKey(tid) ? _db.TeamsById[tid] : null;
                    occupiedMsgs.Add((tt != null ? tt.DisplayName : ("Equipo " + tid)) + " ya juega en " + ocName);
                }
            }
            if (occupiedMsgs.Count > 0)
                return Fail("No se creó la liga. " + string.Join("; ", occupiedMsgs.Take(6).ToArray()) + ". Quitá esos equipos de la lista o clonalos antes de crear.");

            // (teamIdsDyn ya trae la lista: elegidos o autogenerados)

            _db.CreateNewLeague(

                compId,

                GS(msg, "nameAscii") ?? "NUEVA_LIGA",

                GS(msg, "nameSpanish") ?? "Nueva Liga",

                GS(msg, "nameLatam") ?? GS(msg, "nameSpanish") ?? "Nueva Liga",

                GS(msg, "nameEnglish") ?? "New League",

                (byte)GI(msg, "continent", 4),

                (byte)GI(msg, "totalTeams", Math.Max(2, teamIdsDyn.Count)),

                (byte)GI(msg, "compType", 0),

                GB(msg, "isIndependent", true),

                (byte)GI(msg, "parentCompId", 0),

                teamIdsDyn,

                (ushort)GI(msg, "countryId", 0),

                (byte)GI(msg, "changes", 3),

                (byte)GI(msg, "registeredPlayers", 23),

                (byte)GI(msg, "rounds", 2),

                (byte)GI(msg, "tieBreaker", 1),

                (byte)GI(msg, "cupFormat", 0),

                GB(msg, "extraTime", false),

                GB(msg, "penalties", false),

                GB(msg, "awayGoals", false),

                GB(msg, "thirdPlace", false),

                GB(msg, "singleLeg", false),

                GI(msg, "promote", -1),

                GI(msg, "demote", -1),

                GI(msg, "continental1", 4),

                GI(msg, "continental2", 2),

                (byte)GI(msg, "numGroups", 1),

                (ushort)GI(msg, "numClassified", 0),

                GB(msg, "finalChampionship", false),

                (byte)GI(msg, "seasonType", 0),

                (ushort)GI(msg, "linkedCup", 0),

                (ushort)GI(msg, "linkedSuperCup", 0),

                GB(msg, "championship", false));

            if (msg.ContainsKey("aperturaClausura") && GB(msg, "aperturaClausura", false))

            {

                _db.SetLeagueAperturaClausura(compId, true);

            }

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["compId"] = (int)compId; return r;

        }

        private void AddEntry(byte compId, uint teamId)

        {

            var e = new EntryRecord { Id = _db.GetNextEntryId(), TeamId = teamId, CompetitionId = compId, IsNew = true, IsModified = true };

            List<EntryRecord> list;

            if (_db.EntriesByCompetition != null && _db.EntriesByCompetition.TryGetValue(compId, out list) && list != null)

            {

                // 1-indexada y contigua, como en DatabaseManager: antes list.Count duplicaba la ultima posicion.

                e.Position = (byte)(list.Count + 1);

                list.Add(e);

            }

            else

            {

                e.Position = 1;

                if (_db.EntriesByCompetition != null) _db.EntriesByCompetition[compId] = new List<EntryRecord> { e };

            }

            e.RawBytes = e.ToBytes();

            _db.Entries.Add(e);

        }

        private Dictionary<string, object> AddTeamToCompetition(Dictionary<string, object> msg)

        {

            RequireDb();

            byte compId = (byte)GI(msg, "compId", -1);

            uint teamId = (uint)GL(msg, "teamId", 0);

            List<EntryRecord> list;

            if (_db.EntriesByCompetition != null && _db.EntriesByCompetition.TryGetValue(compId, out list) && list != null && list.Any(e => e.TeamId == teamId))

                return Fail("El equipo ya está en esta competición.");
            var targetReg = RegOf(compId);
            if (targetReg != null && (targetReg.CompetitionType == 0 || targetReg.CompetitionType == 4))
            {
                byte ocId; string ocName;
                if (_db.IsTeamInDomesticLeague(teamId, compId, out ocId, out ocName))
                    return Fail("Ese equipo ya juega en " + ocName + ". Para moverlo, asignalo desde su ficha (ahí se desvincula de la otra liga); para tenerlo en las dos, clonalo.");
            }

            AddEntry(compId, teamId);

            _db.HasUnsavedChanges = true;

            return Ok();

        }

        private Dictionary<string, object> RemoveTeamFromCompetition(Dictionary<string, object> msg)

        {

            RequireDb();

            byte compId = (byte)GI(msg, "compId", -1);

            uint teamId = (uint)GL(msg, "teamId", 0);

            var e = _db.Entries.FirstOrDefault(x => x.CompetitionId == compId && x.TeamId == teamId);

            if (e != null)

            {

                _db.Entries.Remove(e);

                List<EntryRecord> list;

                if (_db.EntriesByCompetition != null && _db.EntriesByCompetition.TryGetValue(compId, out list) && list != null)

                {

                    list.Remove(e);

                    // Igual que la app actual: resecuenciar 1..N sin huecos

                    // y reconciliar TotalTeams con la lista real.

                    for (int i = 0; i < list.Count; i++)

                    {

                        list[i].Position = (byte)(i + 1);

                        list[i].IsModified = true;

                        list[i].RawBytes = list[i].ToBytes();

                    }

                    var reg = RegOf(compId);

                    if (reg != null) { reg.TotalTeams = (byte)list.Count; reg.IsModified = true; }

                }

                _db.HasUnsavedChanges = true;

            }

            return Ok();

        }

        // ------------------------- equipos -------------------------

        private Dictionary<string, object> ListTeams(Dictionary<string, object> msg)

        {

            RequireDb();

            string search = (GS(msg, "search") ?? "").ToLowerInvariant();

            string scope = GS(msg, "scope") ?? "all";

            int continent = GI(msg, "continent", 0);

            int leagueId = GI(msg, "leagueId", -1);

            int limit = GI(msg, "limit", 800);

            var list = new List<object>();

            foreach (var t in _db.Teams.OrderBy(t => t.DisplayName))

            {

                if (search.Length > 0)

                {

                    string hay = ((t.NameSpanish ?? "") + " " + (t.NameEnglish ?? "") + " " + (t.ShortName ?? "") + " " + t.Id).ToLowerInvariant();

                    if (!hay.Contains(search)) continue;

                }

                if (scope == "clubs" && t.IsNational) continue;

                if (scope == "nationals" && !t.IsNational) continue;

                byte occComp; string occName;

                bool occ = _db.IsTeamOccupied(t.Id, out occComp, out occName);

                if (scope == "withLeague" && !occ) continue;

                if (scope == "free" && occ) continue;

                if (continent != 0)

                {

                    CountryRecord cr;

                    byte teamCont = 0;

                    if (_db.CountriesById != null && _db.CountriesById.TryGetValue(t.CountryId, out cr) && cr != null) teamCont = cr.Continent;

                    if (teamCont != continent) continue;

                }

                if (leagueId >= 0 && !(occ && occComp == leagueId)) continue;

                list.Add(TeamDto(t));

                if (list.Count >= limit) break;

            }

            var r = Ok(); r["teams"] = list; r["total"] = list.Count; return r;

        }

        private Dictionary<string, object> GetTeam(Dictionary<string, object> msg)

        {

            RequireDb();

            uint id = (uint)GL(msg, "id", 0);

            TeamRecord t;

            if (!_db.TeamsById.TryGetValue(id, out t) || t == null) return Fail("Equipo no encontrado: " + id);

            var dto = TeamDto(t);

            StadiumRecord st; dto["stadiumName"] = (_db.StadiumsById != null && _db.StadiumsById.TryGetValue(t.StadiumId, out st) && st != null) ? (st.Name ?? "") : "";

            CoachRecord co; dto["coachName"] = (_db.CoachesById != null && _db.CoachesById.TryGetValue(t.CoachId, out co) && co != null) ? (co.Name ?? "") : "";

            dto["color1"] = ColorToHex(t.Color1);

            dto["color2"] = ColorToHex(t.Color2);

            dto["banners"] = new List<object> { t.Banner1 ?? "", t.Banner2 ?? "", t.Banner3 ?? "", t.Banner4 ?? "" };

            var rivals = new List<object>();
            var rids = new uint[] { t.Rival1, t.Rival2, t.Rival3 };
            foreach (var rid in rids)
            {
                TeamRecord rt = null; string rn = ""; string sn = "";
                uint cleanId = (rid == 262143 || rid == 0x3FFFF) ? 0 : rid;
                if (cleanId != 0 && _db.TeamsById.TryGetValue(cleanId, out rt) && rt != null)
                {
                    rn = rt.DisplayName;
                    sn = rt.ShortName ?? "";
                }
                rivals.Add(new Dictionary<string, object> { { "id", cleanId }, { "name", rn }, { "shortName", sn } });
            }
            dto["rivals"] = rivals;

            // Plantilla

            var roster = new List<object>();

            foreach (var a in _db.GetTeamRoster(id))

            {

                PlayerRecord p; string pn = "", pp = ""; int ovr = 0; int age = 0; ushort natId = 0; string natName = "";

                if (_db.PlayersById.TryGetValue(a.PlayerId, out p) && p != null)

                {

                    pn = p.Name ?? "";

                    pp = p.PositionName ?? "";

                    ovr = p.Overall;

                    age = p.Age;

                    natId = PlayerBitHelper.GetNationalityId(p.RawBytes);

                    natName = CountryName(natId);

                }

                roster.Add(new Dictionary<string, object>

                {

                    { "playerId", a.PlayerId }, { "name", pn }, { "pos", pp }, { "ovr", ovr },

                    { "age", age }, { "natId", natId }, { "natName", natName },

                    { "shirt", a.ShirtNumber }, { "squadOrder", a.SquadOrder },

                    { "captain", a.IsCaptain }, { "penalty", a.IsPenalty }, { "longFk", a.IsLongFK },

                    { "shortFk", a.IsShortFK }, { "leftCorner", a.IsLeftCorner }, { "rightCorner", a.IsRightCorner }

                });

            }

            dto["roster"] = roster;

            // Tactica y formacion: la app actual usa db.GetTeamFormation

            // (la formacion vigente) + el orden de la plantilla. Los registros

            // crudos de TacticsFormation incluyen las demas variantes y

            // duplicaban los titulares: no usarlos directo.

            var eleven = new List<object>();

            string formationName = "";

            var formation = _db.GetTeamFormation(id);

            if (formation != null && formation.Roles != null)

            {

                formationName = formation.Name ?? "";

                var rosterList = _db.GetTeamRoster(id);

                for (int i = 0; i < formation.Roles.Length; i++)

                {

                    PlayerAssignmentRecord a = i < rosterList.Count ? rosterList[i] : null;

                    PlayerRecord fp = null; string pn = ""; int shirt = 0;

                    if (a != null)

                    {

                        shirt = a.ShirtNumber;

                        if (_db.PlayersById.TryGetValue(a.PlayerId, out fp) && fp != null) pn = fp.Name ?? "";

                    }

                    eleven.Add(new Dictionary<string, object>

                    {

                        { "squadIndex", i }, { "role", formation.Roles[i] }, { "roleName", FormationPreset.GetRoleName(formation.Roles[i]) },

                        { "x", formation.Widths != null && i < formation.Widths.Length ? (int)formation.Widths[i] : 50 },

                        { "y", formation.Depths != null && i < formation.Depths.Length ? (int)formation.Depths[i] : 25 },

                        { "playerName", pn }, { "playerId", fp != null ? fp.Id : 0 }, { "shirt", shirt }

                    });

                }

            }

            dto["eleven"] = eleven;

            dto["formationName"] = formationName;

            var presets = new List<object>();

            foreach (var pr in FormationPreset.Presets) presets.Add(pr.Name);

            dto["formationPresets"] = presets;

            // Estrategia y tactica de juego

            int[] adv = _db.GetTeamAdvancedInstructions(id);

            TacticsRecord tr = (_db.TacticsByClub != null && _db.TacticsByClub.ContainsKey(id) && _db.TacticsByClub[id].Count > 0) ? _db.TacticsByClub[id][0] : null;

            uint stratVal = tr != null ? tr.Strategy : 0x5A936900;

            dto["strategy"] = new Dictionary<string, object>

            {

                { "attackStyle", (int)(stratVal & 0x01) },

                { "buildUp", (int)((stratVal >> 1) & 0x01) },

                { "attackArea", (int)((stratVal >> 2) & 0x01) },

                { "positioning", (int)((stratVal >> 3) & 0x01) },

                { "defensiveStyle", (int)((stratVal >> 4) & 0x01) },

                { "containmentArea", (int)((stratVal >> 5) & 0x01) },

                { "pressuring", (int)((stratVal >> 6) & 0x01) },

                { "defensiveLine", Math.Max(1, Math.Min(10, (int)((stratVal >> 8) & 0x0F) + 1)) },

                { "compactness", Math.Max(1, Math.Min(10, (int)((stratVal >> 12) & 0x0F) + 1)) },

                { "supportRange", Math.Max(1, Math.Min(10, (int)((stratVal >> 16) & 0x0F) + 1)) },

                { "advAtt1", adv[0] },

                { "advAtt2", adv[1] },

                { "advDef1", adv[2] },

                { "advDef2", adv[3] },

                { "raw", stratVal }

            };

            // Lookups para la ficha

            dto["countries"] = CountriesDto()["countries"];

            var stadiums = new List<object>();

            foreach (var s in _db.Stadiums.OrderBy(s => s.Name)) stadiums.Add(new Dictionary<string, object> { { "id", s.Id }, { "name", s.Name ?? "" } });

            dto["stadiums"] = stadiums;

            var coaches = new List<object>();

            foreach (var c in _db.Coaches.OrderBy(c => c.Name)) coaches.Add(new Dictionary<string, object> { { "id", c.Id }, { "name", c.Name ?? "" } });

            dto["coaches"] = coaches;

            var r = Ok(); r["team"] = dto; return r;

        }

        private static string ColorToHex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

        private static Color HexToColor(string hex, Color def)

        {

            try

            {

                if (string.IsNullOrEmpty(hex)) return def;

                hex = hex.TrimStart('#');

                int v = Convert.ToInt32(hex, 16);

                return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);

            }

            catch { return def; }

        }

        private Dictionary<string, object> UpdateTeam(Dictionary<string, object> msg)

        {

            RequireDb();

            uint id = (uint)GL(msg, "id", 0);

            TeamRecord t;

            if (!_db.TeamsById.TryGetValue(id, out t) || t == null) return Fail("Equipo no encontrado.");

            _db.UpdateTeamRecordFull(t,

                GS(msg, "nameSpanish") ?? t.NameSpanish,

                GS(msg, "nameEnglish") ?? t.NameEnglish,

                GS(msg, "shortName") ?? t.ShortName,

                (ushort)GI(msg, "countryId", t.CountryId),

                (ushort)GI(msg, "stadiumId", t.StadiumId),

                (uint)GL(msg, "coachId", t.CoachId),

                GB(msg, "isNational", t.IsNational),

                t.Rival1, t.Rival2, t.Rival3);

            if (msg.ContainsKey("color1")) t.Color1 = HexToColor(GS(msg, "color1"), t.Color1);

            if (msg.ContainsKey("color2")) t.Color2 = HexToColor(GS(msg, "color2"), t.Color2);

            var banners = GA(msg, "banners");

            if (banners.Count >= 4)

            {

                t.Banner1 = banners[0] != null ? banners[0].ToString() : "";

                t.Banner2 = banners[1] != null ? banners[1].ToString() : "";

                t.Banner3 = banners[2] != null ? banners[2].ToString() : "";

                t.Banner4 = banners[3] != null ? banners[3].ToString() : "";

            }

            var rivals = GUList(msg, "rivalIds");
            if (rivals == null || rivals.Count < 3) rivals = GUList(msg, "rivals");
            if (rivals != null && rivals.Count >= 3)
            {
                _db.SetTeamRivals(id, rivals[0], rivals[1], rivals[2]);
            }
            else if (msg.ContainsKey("rival1") || msg.ContainsKey("rival2") || msg.ContainsKey("rival3"))
            {
                uint r1 = (uint)GL(msg, "rival1", t.Rival1);
                uint r2 = (uint)GL(msg, "rival2", t.Rival2);
                uint r3 = (uint)GL(msg, "rival3", t.Rival3);
                _db.SetTeamRivals(id, r1, r2, r3);
            }

            if (msg.ContainsKey("callnameId")) t.CallnameId = (ushort)GI(msg, "callnameId", t.CallnameId);

            t.IsModified = true; _db.HasUnsavedChanges = true;

            var r = Ok(); r["team"] = TeamDto(t); return r;

        }

        private Dictionary<string, object> CreateTeam(Dictionary<string, object> msg)

        {

            RequireDb();

            var t = _db.CreateNewTeam(

                GS(msg, "nameSpanish") ?? "Nuevo Club",

                GS(msg, "nameEnglish") ?? "New Club",

                GS(msg, "shortName") ?? "NEW",

                (ushort)GI(msg, "countryId", 0),

                (ushort)GI(msg, "stadiumId", 0),

                (uint)GL(msg, "coachId", 0),

                (byte)GI(msg, "targetCompId", 0),

                (uint)GL(msg, "explicitId", 0));

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["team"] = TeamDto(t); return r;

        }

        private Dictionary<string, object> CloneTeam(Dictionary<string, object> msg)

        {

            RequireDb();

            uint id = (uint)GL(msg, "id", 0);

            var t = _db.CloneTeam(id, GS(msg, "newName"));

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["team"] = TeamDto(t); return r;

        }

        private Dictionary<string, object> DeleteTeam(Dictionary<string, object> msg)

        {

            RequireDb();

            uint id = (uint)GL(msg, "id", 0);

            bool okDel = _db.DeleteTeam(id);

            if (!okDel) return Fail("No se pudo eliminar el equipo.");

            var r = Ok(); r["deleted"] = true; return r;

        }

        private Dictionary<string, object> BatchCreateTeams(Dictionary<string, object> msg)

        {

            RequireDb();

            var created = _db.BatchCreateTeams(GI(msg, "count", 0), GS(msg, "prefix") ?? "Club", (ushort)GI(msg, "countryId", 0), (byte)GI(msg, "targetCompId", 0));

            int nCreated = created != null ? created.Count : 0;

            if (nCreated == 0) return Fail("No se creó ningún equipo: revisá la cantidad pedida.");

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["created"] = nCreated; return r;

        }

        private Dictionary<string, object> GenerateTeams(Dictionary<string, object> msg)

        {

            RequireDb();

            var created = _db.GenerateAndAddCustomTeams(GI(msg, "count", 0), GS(msg, "prefix") ?? "Club", (ushort)GI(msg, "countryId", 0));

            int nCreated = created != null ? created.Count : 0;

            if (nCreated == 0) return Fail("No se generó ningún equipo: revisá la cantidad pedida.");

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["created"] = nCreated; return r;

        }

        private Dictionary<string, object> AssignTeamToLeague(Dictionary<string, object> msg)

        {

            RequireDb();

            // Portado de MainForm.AssignSelectedTeamToLeague: el aforo se

            // cumple; "otros equipos" (compId 0) = desvincular de ligas.

            var teamIds = GUList(msg, "teamIds");

            byte compId = (byte)GI(msg, "compId", 0);

            if (compId == 0)

            {

                foreach (var tid in teamIds) _db.RemoveTeamFromDomesticLeagues(tid);

                _db.HasUnsavedChanges = true;

                var r0 = Ok(); r0["moved"] = teamIds.Count; return r0;

            }

            // Igual que la app actual (MainForm, asignacion individual):

            // tope duro de 32 equipos y la entrada la construye

            // DatabaseManager.AssignTeamToLeague (posicion 1-indexada,

            // RawBytes y TotalTeams reconciliados). Antes se armaba a mano

            // con AddEntry y la posicion quedaba duplicada.

            var existing = new HashSet<uint>();

            List<EntryRecord> current;

            if (_db.EntriesByCompetition != null && _db.EntriesByCompetition.TryGetValue(compId, out current) && current != null)

                foreach (var e in current) existing.Add(e.TeamId);

            int toAdd = teamIds.Count(tid => !existing.Contains(tid));

            if (existing.Count + toAdd > 32)

                return Fail("La competición ya cuenta con " + existing.Count + " equipos (límite máximo 32). La asignación fue bloqueada.");

            int moved = 0;

            foreach (var tid in teamIds)

            {

                if (existing.Contains(tid)) continue;

                _db.AssignTeamToLeague(tid, compId);

                moved++;

            }

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["moved"] = moved; return r;

        }

        private Dictionary<string, object> ApplyFormation(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            string presetName = GS(msg, "preset") ?? "";

            var preset = FormationPreset.Presets.FirstOrDefault(p => p.Name == presetName || presetName.StartsWith(p.Name) || p.Name.StartsWith(presetName));

            if (preset == null) return Fail("Formación no encontrada: " + presetName);

            _db.ApplyTeamFormation(teamId, preset);

            _db.HasUnsavedChanges = true;

            var r = Ok();

            r["formationName"] = preset.Name;

            var formation = _db.GetTeamFormation(teamId);

            var eleven = new List<object>();

            if (formation != null && formation.Roles != null)

            {

                var rosterList = _db.GetTeamRoster(teamId);

                for (int i = 0; i < formation.Roles.Length; i++)

                {

                    PlayerAssignmentRecord a = i < rosterList.Count ? rosterList[i] : null;

                    PlayerRecord fp = null; string pn = ""; int shirt = 0;

                    if (a != null)

                    {

                        shirt = a.ShirtNumber;

                        if (_db.PlayersById.TryGetValue(a.PlayerId, out fp) && fp != null) pn = fp.Name ?? "";

                    }

                    eleven.Add(new Dictionary<string, object>

                    {

                        { "squadIndex", i }, { "role", formation.Roles[i] }, { "roleName", FormationPreset.GetRoleName(formation.Roles[i]) },

                        { "x", formation.Widths != null && i < formation.Widths.Length ? (int)formation.Widths[i] : 50 },

                        { "y", formation.Depths != null && i < formation.Depths.Length ? (int)formation.Depths[i] : 25 },

                        { "playerName", pn }, { "playerId", fp != null ? fp.Id : 0 }, { "shirt", shirt }

                    });

                }

            }

            r["eleven"] = eleven;

            return r;

        }

        private Dictionary<string, object> SetTeamStrategy(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            int attackStyle = (int)GI(msg, "attackStyle", 0);

            int buildUp = (int)GI(msg, "buildUp", 1);

            int attackArea = (int)GI(msg, "attackArea", 0);

            int positioning = (int)GI(msg, "positioning", 0);

            int defensiveStyle = (int)GI(msg, "defensiveStyle", 0);

            int containmentArea = (int)GI(msg, "containmentArea", 0);

            int pressuring = (int)GI(msg, "pressuring", 0);

            int defLine = Math.Max(1, Math.Min(10, (int)GI(msg, "defensiveLine", 5))) - 1;

            int compactness = Math.Max(1, Math.Min(10, (int)GI(msg, "compactness", 5))) - 1;

            int supportRange = Math.Max(1, Math.Min(10, (int)GI(msg, "supportRange", 5))) - 1;

            uint newStrat = (uint)(

                (attackStyle & 0x01) |

                ((buildUp & 0x01) << 1) |

                ((attackArea & 0x01) << 2) |

                ((positioning & 0x01) << 3) |

                ((defensiveStyle & 0x01) << 4) |

                ((containmentArea & 0x01) << 5) |

                ((pressuring & 0x01) << 6) |

                ((defLine & 0x0F) << 8) |

                ((compactness & 0x0F) << 12) |

                ((supportRange & 0x0F) << 16)

            );

            int advAtt1 = (int)GI(msg, "advAtt1", 0);

            int advAtt2 = (int)GI(msg, "advAtt2", 0);

            int advDef1 = (int)GI(msg, "advDef1", 0);

            int advDef2 = (int)GI(msg, "advDef2", 0);

            _db.SetTeamAdvancedInstructions(teamId, advAtt1, advAtt2, advDef1, advDef2);

            _db.UpdateTeamStrategy(teamId, newStrat);

            _db.HasUnsavedChanges = true;

            return Ok();

        }

        private Dictionary<string, object> SetTacticalPosition(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            int squadIndex = (int)GL(msg, "squadIndex", -1);

            int role = (int)GL(msg, "role", -1);

            byte depth = (byte)GL(msg, "depth", 0);

            byte width = (byte)GL(msg, "width", 0);

            bool ok = _db.UpdatePlayerTacticalPosition(teamId, squadIndex, role, depth, width);

            if (!ok) return Fail("No se pudo actualizar la posición táctica.");

            var r = Ok();

            r["squadIndex"] = squadIndex;

            return r;

        }

        private Dictionary<string, object> SwapSquadPlayers(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            uint pidA = (uint)GL(msg, "playerIdA", 0);

            uint pidB = (uint)GL(msg, "playerIdB", 0);

            bool ok = _db.SwapSquadPlayers(teamId, pidA, pidB);

            if (!ok) return Fail("No se pudieron intercambiar los jugadores.");

            return Ok();

        }

        private Dictionary<string, object> SetStarterRole(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            int squadIndex = (int)GL(msg, "squadIndex", -1);

            int role = (int)GL(msg, "role", -1);

            bool ok = _db.SetStarterRole(teamId, squadIndex, role);

            if (!ok) return Fail("No se pudo cambiar el rol táctico.");

            return Ok();

        }

        // ------------------------- plantilla -------------------------

        private Dictionary<string, object> GetRoster(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            var list = new List<object>();

            foreach (var a in _db.GetTeamRoster(teamId))

            {

                PlayerRecord p; string pn = "", pp = ""; int ovr = 0; int age = 0; ushort natId = 0; string natName = "";

                if (_db.PlayersById.TryGetValue(a.PlayerId, out p) && p != null)

                {

                    pn = p.Name ?? "";

                    pp = p.PositionName ?? "";

                    ovr = p.Overall;

                    age = p.Age;

                    natId = PlayerBitHelper.GetNationalityId(p.RawBytes);

                    natName = CountryName(natId);

                }

                list.Add(new Dictionary<string, object>

                {

                    { "playerId", a.PlayerId }, { "name", pn }, { "pos", pp }, { "ovr", ovr },

                    { "age", age }, { "natId", natId }, { "natName", natName },

                    { "shirt", a.ShirtNumber }, { "squadOrder", a.SquadOrder }, { "captain", a.IsCaptain }

                });

            }

            var r = Ok(); r["roster"] = list; return r;

        }

        private Dictionary<string, object> SetShirtNumber(Dictionary<string, object> msg)

        {

            RequireDb();

            bool okSet = _db.ChangePlayerShirtNumber((uint)GL(msg, "teamId", 0), (uint)GL(msg, "playerId", 0), GI(msg, "shirt", 0));

            var r = Ok(); r["changed"] = okSet; if (!okSet) r["error"] = "Dorsal inválido u ocupado (1-99, sin repetir).";

            return r;

        }

        private Dictionary<string, object> TransferPlayer(Dictionary<string, object> msg)

        {

            RequireDb();

            bool okT = _db.TransferPlayer((uint)GL(msg, "playerId", 0), (uint)GL(msg, "fromTeamId", 0), (uint)GL(msg, "toTeamId", 0));

            if (!okT) return Fail("No se pudo traspasar al jugador (¿plantilla destino llena o jugador inexistente?).");

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["transferred"] = true; return r;

        }

        private Dictionary<string, object> ReleasePlayer(Dictionary<string, object> msg)

        {

            RequireDb();

            bool okR = _db.ReleasePlayer((uint)GL(msg, "playerId", 0), (uint)GL(msg, "teamId", 0));

            if (!okR) return Fail("No se pudo liberar al jugador.");

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["released"] = true; return r;

        }

        private Dictionary<string, object> SignFreeAgent(Dictionary<string, object> msg)

        {

            RequireDb();

            bool okS = _db.SignFreeAgent((uint)GL(msg, "playerId", 0), (uint)GL(msg, "teamId", 0), GI(msg, "shirt", 0));

            if (!okS) return Fail("No se pudo fichar al jugador (¿sigue libre y hay lugar en la plantilla?).");

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["signed"] = true; return r;

        }

        private Dictionary<string, object> AutoAssignRoles(Dictionary<string, object> msg)

        {

            RequireDb();

            bool okA = _db.AutoAssignRoles((uint)GL(msg, "teamId", 0));

            if (!okA) return Fail("No se pudieron auto-asignar los roles de este equipo.");

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["done"] = true; return r;

        }

        private Dictionary<string, object> SetSetPieceRoles(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            uint cap = (uint)GL(msg, "captainPid", 0);

            uint pen = (uint)GL(msg, "penaltyPid", 0);

            uint lfk = (uint)GL(msg, "longFkPid", 0);

            uint sfk = (uint)GL(msg, "shortFkPid", 0);

            uint lcn = (uint)GL(msg, "leftCornerPid", 0);

            uint rcn = (uint)GL(msg, "rightCornerPid", 0);

            bool ok = _db.SetTeamSetPieceRoles(teamId, cap, pen, lfk, sfk, lcn, rcn);

            if (!ok) return Fail("No se pudieron guardar los roles de balón parado.");

            return Ok();

        }

        private Dictionary<string, object> FreeAgents(Dictionary<string, object> msg)

        {

            RequireDb();

            var list = new List<object>();

            foreach (var p in _db.GetFreeAgents(GS(msg, "search") ?? "", GI(msg, "limit", 200))) list.Add(PlayerLiteDto(p));

            var r = Ok(); r["players"] = list; return r;

        }

        // ------------------------- jugadores -------------------------

        private Dictionary<string, object> ListPlayers(Dictionary<string, object> msg)

        {

            RequireDb();

            string search = (GS(msg, "search") ?? "").ToLowerInvariant();

            string pos = GS(msg, "pos") ?? "";

            if (pos.Length > 0) pos = PlayerRecord.NormalizePositionName(pos);

            int minOvr = GI(msg, "minOvr", 0);

            long clubId = GL(msg, "clubId", -1);

            bool freeOnly = GB(msg, "freeOnly", false);

            int limit = GI(msg, "limit", 300);

            int offset = GI(msg, "offset", 0);

            var filtered = new List<PlayerRecord>();

            foreach (var p in _db.Players)

            {

                if (search.Length > 0 && ((p.Name ?? "").ToLowerInvariant().Contains(search) || p.Id.ToString().Contains(search))) { /* sigue */ }

                else if (search.Length > 0) continue;

                if (pos.Length > 0 && (p.PositionName ?? "") != pos) continue;

                if (minOvr > 0 && p.Overall < minOvr) continue;

                if (clubId >= 0 && p.ClubId != (uint)clubId) continue;

                if (freeOnly && p.ClubId != 0) continue;

                filtered.Add(p);

            }

            filtered.Sort((a, b) => b.Overall.CompareTo(a.Overall));

            var list = new List<object>();

            foreach (var p in filtered.Skip(offset).Take(limit)) list.Add(PlayerLiteDto(p));

            var r = Ok(); r["players"] = list; r["total"] = filtered.Count; return r;

        }

        private Dictionary<string, object> GetPlayer(Dictionary<string, object> msg)

        {

            RequireDb();

            uint id = (uint)GL(msg, "id", 0);

            var p = _db.GetPlayer(id);

            if (p == null) return Fail("Jugador no encontrado: " + id);

            var dto = PlayerLiteDto(p);

            var d = p.RawBytes;

            dto["shirtName"] = p.ShirtName ?? "";

            dto["callnameId"] = p.CallnameId;

            dto["height"] = PlayerBitHelper.GetHeight(d);

            dto["weight"] = PlayerBitHelper.GetWeight(d);

            dto["leftFooted"] = PlayerBitHelper.IsLeftFooted(d);

            dto["weakFootUsage"] = PlayerBitHelper.GetWeakFootUsage(d);

            dto["weakFootAccuracy"] = PlayerBitHelper.GetWeakFootAccuracy(d);

            dto["form"] = PlayerBitHelper.GetForm(d);

            dto["injury"] = PlayerBitHelper.GetInjuryResistance(d);

            dto["style"] = PlayerBitHelper.GetPlayingStyle(d);

            dto["styleName"] = PlayerBitHelper.PlayingStyleNames.Length > PlayerBitHelper.GetPlayingStyle(d) ? PlayerBitHelper.PlayingStyleNames[PlayerBitHelper.GetPlayingStyle(d)] : "";

            var abilities = new Dictionary<string, object>();

            foreach (var key in PlayerBitHelper.DbAbilBits.Keys) abilities[key] = PlayerBitHelper.GetAbility(d, key);

            dto["abilities"] = abilities;

            var skills = new List<object>();

            foreach (var kv in PlayerBitHelper.SkillLabels.OrderBy(k => k.Key))

                skills.Add(new Dictionary<string, object> { { "id", kv.Key }, { "label", kv.Value }, { "on", PlayerBitHelper.HasSkill(d, kv.Key) } });

            dto["skills"] = skills;

            var comStyles = new List<object>();

            for (int i = 0; i < PlayerBitHelper.ComStyleNames.Length; i++)

                comStyles.Add(new Dictionary<string, object> { { "id", i }, { "label", PlayerBitHelper.ComStyleNames[i] }, { "on", PlayerBitHelper.HasComStyle(d, i) } });

            dto["comStyles"] = comStyles;

            var positions = new List<object>();

            foreach (var kv in PlayerBitHelper.DbPosBits.OrderBy(k => k.Key))

                positions.Add(new Dictionary<string, object> { { "index", kv.Key }, { "level", PlayerBitHelper.GetPositionCompatibility(d, kv.Key) } });

            dto["positions"] = positions;

            dto["positionNames"] = PlayerRecord.PositionNames;

            dto["positionNamesEs"] = PlayerRecord.PositionNamesEs;

            dto["styles"] = PlayerBitHelper.PlayingStyleNames;

            dto["countries"] = CountriesDto()["countries"];

            bool hasFace = false; try { hasFace = MinifaceProvider.HasRealFace(id); } catch { }

            dto["hasRealFace"] = hasFace;

            var r = Ok(); r["player"] = dto; return r;

        }

        private Dictionary<string, object> UpdatePlayer(Dictionary<string, object> msg)

        {

            RequireDb();

            uint id = (uint)GL(msg, "id", 0);

            var p = _db.GetPlayer(id);

            if (p == null) return Fail("Jugador no encontrado.");

            string v;

            if ((v = GS(msg, "name")) != null) p.Name = v;

            if ((v = GS(msg, "shirtName")) != null) p.ShirtName = v;

            var d = p.RawBytes;

            if (msg.ContainsKey("age")) PlayerBitHelper.SetAge(d, GI(msg, "age", p.Age));

            if (msg.ContainsKey("height")) PlayerBitHelper.SetHeight(d, GI(msg, "height", 180));

            if (msg.ContainsKey("weight")) PlayerBitHelper.SetWeight(d, GI(msg, "weight", 75));

            if (msg.ContainsKey("natId")) PlayerBitHelper.SetNationalityId(d, (ushort)GI(msg, "natId", 0));

            if (msg.ContainsKey("leftFooted")) PlayerBitHelper.SetFoot(d, GB(msg, "leftFooted", false));

            if (msg.ContainsKey("weakFootUsage")) PlayerBitHelper.SetWeakFootUsage(d, GI(msg, "weakFootUsage", 0));

            if (msg.ContainsKey("weakFootAccuracy")) PlayerBitHelper.SetWeakFootAccuracy(d, GI(msg, "weakFootAccuracy", 0));

            if (msg.ContainsKey("form")) PlayerBitHelper.SetForm(d, GI(msg, "form", 0));

            if (msg.ContainsKey("injury")) PlayerBitHelper.SetInjuryResistance(d, GI(msg, "injury", 0));

            if (msg.ContainsKey("posIndex")) PlayerBitHelper.SetPrimaryPosition(d, GI(msg, "posIndex", 0));

            if (msg.ContainsKey("style")) PlayerBitHelper.SetPlayingStyle(d, GI(msg, "style", 0));

            if (msg.ContainsKey("callnameId")) p.CallnameId = (uint)GL(msg, "callnameId", p.CallnameId);

            var abils = GD(msg, "abilities");

            if (abils != null) foreach (var kv in abils) { int val; if (int.TryParse(kv.Value != null ? kv.Value.ToString() : "0", out val)) PlayerBitHelper.SetAbility(d, kv.Key, val); }

            foreach (var s in GA(msg, "skills"))

            {

                var sd = s as Dictionary<string, object>;

                if (sd != null) PlayerBitHelper.SetSkill(d, GI(sd, "id", -1), GB(sd, "on", false));

            }

            foreach (var s in GA(msg, "comStyles"))

            {

                var sd = s as Dictionary<string, object>;

                if (sd != null) PlayerBitHelper.SetComStyle(d, GI(sd, "id", -1), GB(sd, "on", false));

            }

            foreach (var s in GA(msg, "positions"))

            {

                var sd = s as Dictionary<string, object>;

                if (sd != null) PlayerBitHelper.SetPositionCompatibility(d, GI(sd, "index", -1), GI(sd, "level", 0));

            }

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["player"] = PlayerLiteDto(p); return r;

        }

        private Dictionary<string, object> CreatePlayer(Dictionary<string, object> msg)

        {

            RequireDb();

            var p = _db.CreatePlayer(GS(msg, "name") ?? "Nuevo Jugador", PlayerRecord.NormalizePositionName(GS(msg, "posName") ?? "CF"), GI(msg, "ovr", 70), (ushort)GI(msg, "natId", 0), (uint)GL(msg, "clubId", 0));
            // Detalle completo (nivel editores de los repos): fisicos y habilidades individuales.
            string shn;
            if ((shn = GS(msg, "shirtName")) != null && shn.Length > 0) p.ShirtName = shn;
            var draw = p.RawBytes;
            if (msg.ContainsKey("age")) PlayerBitHelper.SetAge(draw, GI(msg, "age", p.Age));
            if (msg.ContainsKey("height")) PlayerBitHelper.SetHeight(draw, GI(msg, "height", 180));
            if (msg.ContainsKey("weight")) PlayerBitHelper.SetWeight(draw, GI(msg, "weight", 75));
            if (msg.ContainsKey("leftFooted")) PlayerBitHelper.SetFoot(draw, GB(msg, "leftFooted", false));
            if (msg.ContainsKey("weakFootUsage")) PlayerBitHelper.SetWeakFootUsage(draw, GI(msg, "weakFootUsage", 0));
            if (msg.ContainsKey("weakFootAccuracy")) PlayerBitHelper.SetWeakFootAccuracy(draw, GI(msg, "weakFootAccuracy", 0));
            if (msg.ContainsKey("form")) PlayerBitHelper.SetForm(draw, GI(msg, "form", 0));
            if (msg.ContainsKey("injury")) PlayerBitHelper.SetInjuryResistance(draw, GI(msg, "injury", 0));
            if (msg.ContainsKey("style")) PlayerBitHelper.SetPlayingStyle(draw, GI(msg, "style", 0));
            var abilsNew = GD(msg, "abilities");
            if (abilsNew != null) foreach (var kv in abilsNew) { int val; if (int.TryParse(kv.Value != null ? kv.Value.ToString() : "0", out val)) PlayerBitHelper.SetAbility(draw, kv.Key, val); }

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["player"] = PlayerLiteDto(p); return r;

        }

        private Dictionary<string, object> ClonePlayer(Dictionary<string, object> msg)

        {

            RequireDb();

            var src = _db.GetPlayer((uint)GL(msg, "id", 0));

            if (src == null) return Fail("Jugador no encontrado.");

            var p = _db.ClonePlayer(src);

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["player"] = PlayerLiteDto(p); return r;

        }

        private Dictionary<string, object> DeletePlayer(Dictionary<string, object> msg)

        {

            RequireDb();

            bool okD = _db.DeletePlayer((uint)GL(msg, "id", 0));

            if (!okD) return Fail("No se pudo eliminar al jugador.");

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["deleted"] = true; return r;

        }

        // ------------------------- piramide -------------------------

        private Dictionary<string, object> GetPyramids()

        {

            RequireDb();

            var list = new List<object>();

            foreach (var sys in _db.GetAllPyramidSystems())

            {

                var tierObjs = new List<Dictionary<string, object>>();

                foreach (var t in sys.Tiers.OrderBy(x => x.TierNumber))

                {

                    int compId = t.Competition != null ? (int)t.Competition.Id : 0;

                    var teams = new List<object>();

                    List<EntryRecord> ents;

                    if (compId > 0 && _db.EntriesByCompetition.TryGetValue((byte)compId, out ents) && ents != null)

                    {

                        foreach (var e in ents.OrderBy(x => x.TeamId))

                        {

                            TeamRecord tr;

                            string tn = _db.TeamsById.TryGetValue(e.TeamId, out tr) && tr != null ? (tr.NameSpanish ?? ("Equipo " + e.TeamId)) : ("Equipo " + e.TeamId);

                            teams.Add(new Dictionary<string, object> { { "id", (int)e.TeamId }, { "name", tn } });

                        }

                    }

                    tierObjs.Add(new Dictionary<string, object>

                    {

                        { "n", t.TierNumber }, { "title", t.TierTitle ?? "" },

                        { "compId", compId },

                        { "name", t.Regulation != null ? (t.Regulation.NameSpanish ?? "") : (t.Competition != null ? (t.Competition.Name ?? "") : "") },

                        { "teamCount", t.TeamCount },

                        { "promote", t.PromoteCount }, { "demote", t.DemoteCount },

                        { "continental1", t.Continental1Count }, { "continent", t.Competition != null ? (int)t.Competition.Continent : 0 },

                        { "parentCompId", compId > 0 ? (int)_db.GetLeagueParentCompId((byte)compId) : 0 },

                        { "teams", teams },

                        { "warnings", new List<object>() }

                    });

                }

                // avisos de cadena: madre inexistente y cortes

                foreach (var o in tierObjs)

                {

                    var warns = (List<object>)o["warnings"];

                    int compId = (int)o["compId"];

                    var reg = compId > 0 ? RegOf((byte)compId) : null;

                    if (reg != null && reg.IdDivision1 != 0 && (int)o["parentCompId"] == 0)

                        warns.Add("Su liga madre ya no existe en la base: quedo suelta.");

                    if ((int)o["parentCompId"] != 0 && (int)o["promote"] == 0)

                        warns.Add("Nadie asciende desde esta liga: la cadena esta cortada aca.");

                }

                // avisos de borde: descensos del nivel de arriba vs ascensos del de abajo

                var levels = tierObjs.Select(o => (int)o["n"]).Distinct().OrderBy(x => x).ToList();

                for (int i = 0; i + 1 < levels.Count; i++)

                {

                    int up = levels[i], down = levels[i + 1];

                    int dem = tierObjs.Where(o => (int)o["n"] == up).Sum(o => (int)o["demote"]);

                    int pro = tierObjs.Where(o => (int)o["n"] == down).Sum(o => (int)o["promote"]);

                    if (dem != pro && (dem > 0 || pro > 0))

                        foreach (var o in tierObjs.Where(o => (int)o["n"] == down))

                            ((List<object>)o["warnings"]).Add("Desde el nivel " + down + " ascienden " + pro + ", pero el nivel " + up + " desciende " + dem + ": la cadena no cierra.");

                }

                var cups = new List<object>();

                foreach (var c in sys.DomesticCups) { var cr = RegOf(c.Id); cups.Add(new Dictionary<string, object> { { "compId", (int)c.Id }, { "name", cr != null ? (cr.NameSpanish ?? c.Name) : c.Name } }); }

                var scups = new List<object>();

                foreach (var c in sys.SuperCups) { var cr = RegOf(c.Id); scups.Add(new Dictionary<string, object> { { "compId", (int)c.Id }, { "name", cr != null ? (cr.NameSpanish ?? c.Name) : c.Name } }); }

                list.Add(new Dictionary<string, object>

                {

                    { "countryId", (int)sys.CountryId }, { "countryName", sys.CountryName ?? "" },

                    { "totalClubs", sys.TotalClubs }, { "isBalanced", sys.IsBalanced }, { "balanceMessage", sys.BalanceMessage ?? "" },

                    { "tiers", tierObjs }, { "cups", cups }, { "superCups", scups }

                });

            }

            var r = Ok(); r["pyramids"] = list; return r;

        }

        private Dictionary<string, object> GetPyramidFreeTeams(Dictionary<string, object> msg)

        {

            RequireDb();

            ushort countryId = (ushort)GI(msg, "countryId", 0);

            byte continent = 0;

            CountryRecord cr;

            if (countryId != 0 && _db.CountriesById.TryGetValue(countryId, out cr) && cr != null) continent = cr.Continent;

            var teams = new List<object>();

            foreach (var t in _db.GetFreePlayableTeams(continent).Where(t => countryId == 0 || t.CountryId == countryId).OrderBy(t => t.NameSpanish ?? ""))

                teams.Add(new Dictionary<string, object> { { "id", (int)t.Id }, { "name", t.NameSpanish ?? ("Equipo " + t.Id) } });

            // libres pero NO jugables (plantel corto): el dato que le
            // falta al mensaje de la ventana cuando no hay jugables.
            var playableIds = new HashSet<uint>(teams.Select(t => (uint)(int)((Dictionary<string, object>)t)["id"]));
            var inLeague = new HashSet<uint>();
            foreach (var c in _db.Competitions)
            {
                var dto = CompDto(c);
                if (!(bool)dto["isLeague"]) continue;
                List<EntryRecord> el;
                if (_db.EntriesByCompetition.TryGetValue(c.Id, out el) && el != null)
                    foreach (var e in el) inLeague.Add(e.TeamId);
            }
            var shortTeams = new List<object>();
            foreach (var t in _db.Teams.Where(t => t.CountryId == countryId && !t.IsNational && t.Id != 2703 && t.Id != 2704 && !inLeague.Contains(t.Id) && !playableIds.Contains(t.Id)).OrderBy(t => t.NameSpanish ?? ""))
                shortTeams.Add(new Dictionary<string, object> { { "id", (int)t.Id }, { "name", t.NameSpanish ?? ("Equipo " + t.Id) } });
            var r = Ok(); r["teams"] = teams; r["shortTeams"] = shortTeams; r["countryId"] = (int)countryId; return r;

        }

        private Dictionary<string, object> SetPyramidParent(Dictionary<string, object> msg)

        {

            RequireDb();

            byte compId = (byte)GI(msg, "compId", -1);

            byte parentId = (byte)GI(msg, "parentCompId", 0);

            var comp = _db.Competitions.FirstOrDefault(c => c.Id == compId);

            if (comp == null) return Fail("Liga no encontrada.");

            if (parentId != 0)

            {

                if (parentId == compId) return Fail("Una liga no puede ser su propia madre.");

                var parent = _db.Competitions.FirstOrDefault(c => c.Id == parentId);

                if (parent == null) return Fail("La liga madre no existe.");

                var pd = CompDto(parent);

                if (!(bool)pd["isLeague"]) return Fail("La madre tiene que ser una liga, no una copa.");

                byte walk = parentId; int hops = 0;

                while (walk != 0 && hops++ < 32)

                {

                    if (walk == compId) return Fail("Esa liga ya esta debajo de la tuya: se formaria un circulo.");

                    walk = _db.GetLeagueParentCompId(walk);

                }

            }

            _db.SetLeagueParent(compId, parentId);

            _db.HasUnsavedChanges = true;

            var r = GetPyramids();

            r["message"] = parentId == 0 ? "Liga desvinculada: quedo independiente." : "Liga vinculada debajo de la elegida.";

            return r;

        }

        // ------------------------- sider -------------------------

        private Dictionary<string, object> GetSider()

        {

            var r = Ok();

            string sider = _cfg.SiderFolder ?? "";

            r["siderFolder"] = sider;

            var mods = new List<object>();

            if (!string.IsNullOrEmpty(sider) && Directory.Exists(sider))

            {

                foreach (var m in SiderIniManager.GetModules(sider))

                    mods.Add(new Dictionary<string, object> { { "name", m.Name }, { "active", m.IsActive } });

            }

            r["modules"] = mods;

            r["luaExists"] = !string.IsNullOrEmpty(_cfg.ModulesFolder) && File.Exists(Path.Combine(_cfg.ModulesFolder, "league_manager.lua"));

            r["enableUefaSwiss"] = (_cfg.EnableUefaSwissFormat == "1" || _cfg.EnableUefaSwissFormat == "true");

            if (_db != null && _db.Competitions != null && _db.Competitions.Count > 0)

                r["customLeagues"] = GetCustomCompetitions().Count;

            return r;

        }

        private Dictionary<string, object> SetSiderModule(Dictionary<string, object> msg)

        {

            if (string.IsNullOrEmpty(_cfg.SiderFolder)) return Fail("No hay carpeta de Sider configurada.");

            SiderIniManager.SetModuleActive(_cfg.SiderFolder, GS(msg, "name") ?? "", GB(msg, "active", false));

            return GetSider();

        }

        private Dictionary<string, object> SetUefaSwissFormat(Dictionary<string, object> msg)

        {

            bool enabled = GB(msg, "enabled", false);

            _cfg.EnableUefaSwissFormat = enabled ? "1" : "0";

            _cfg.Save();

            if (!string.IsNullOrEmpty(_cfg.SiderFolder) && Directory.Exists(_cfg.SiderFolder))

            {

                SiderIniManager.SetModuleActive(_cfg.SiderFolder, "fl26swiss.lua", enabled);

            }

            var r = Ok();

            r["enabled"] = enabled;

            return r;

        }

        private Dictionary<string, object> DeploySider()

        {

            RequireDb();

            if (string.IsNullOrEmpty(_cfg.SiderFolder) || !Directory.Exists(_cfg.SiderFolder)) return Fail("Configurá primero la carpeta de Sider.");

            AutoPoint("antes de desplegar");

            return DeploySiderCore();

        }

        private Dictionary<string, object> DeploySiderCore()

        {

            string resultMsg;

            bool enableSwiss = (_cfg.EnableUefaSwissFormat == "1" || _cfg.EnableUefaSwissFormat == "true");

            bool okD = SiderLuaGenerator.DeploySiderModules(_cfg.SiderFolder, GetCustomCompetitions(), _db.Regulations, _db, out resultMsg, enableSwiss);

            var r = Ok(); r["deployed"] = okD; r["message"] = resultMsg ?? ""; return r;

        }

        // ---------------- Fase 2: seguridad del Sider ----------------

        private static string FixFor(string id)

        {

            switch (id)

            {

                case "S2": case "S3": case "S5": case "D2": case "P2": case "P3": case "R3": return id;

                default: return null;

            }

        }

        private void AutoPoint(string motivo)

        {

            try

            {

                string veredicto = "";

                try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

                SiderBackup.Create(_cfg, motivo, true, veredicto);

            }

            catch { }

        }

        private Dictionary<string, object> ListRestorePoints()

        {

            var r = Ok();

            var list = new List<object>();

            foreach (var x in SiderBackup.List(_cfg))

            {

                list.Add(new Dictionary<string, object>

                {

                    { "id", x.Id }, { "fecha", x.Fecha.ToString("dd/MM HH:mm") },

                    { "motivo", x.Motivo }, { "automatico", x.Automatico },

                    { "veredicto", x.Veredicto }, { "tamanoMb", Math.Round(x.TamanoBytes / 1048576.0, 2) }

                });

            }

            r["points"] = list;

            return r;

        }

        private Dictionary<string, object> CreateRestorePoint(Dictionary<string, object> msg)

        {

            if (string.IsNullOrEmpty(_cfg.SiderFolder) || !Directory.Exists(_cfg.SiderFolder)) return Fail("Configurá primero la carpeta de Sider.");

            string veredicto = "";

            try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

            var info = SiderBackup.Create(_cfg, GS(msg, "motivo") ?? "manual", false, veredicto);

            if (info == null) return Fail("No se pudo crear el punto de restauración.");

            var r = ListRestorePoints(); r["message"] = "Punto de restauración creado."; return r;

        }

        private Dictionary<string, object> RestoreRestorePoint(Dictionary<string, object> msg)

        {

            AutoPoint("antes de restaurar");

            string err = SiderBackup.Restore(_cfg, GS(msg, "id"));

            if (err != null) return Fail(err);

            var r = Ok(); r["message"] = "Punto restaurado. Se creó un punto de seguridad con el estado previo.";

            return r;

        }

        private Dictionary<string, object> ProfilesPayload(string message)

        {

            SiderBackup.EnsureComoEstaba(_cfg);

            var r = Ok();

            var list = new List<object>();

            foreach (var x in SiderBackup.LoadProfiles(_cfg))

            {

                list.Add(new Dictionary<string, object>

                {

                    { "nombre", x.Nombre }, { "modulos", x.Modulos.Count },

                    { "activos", x.Modulos.Values.Count(v => v) }, { "esComoEstaba", x.EsComoEstaba }

                });

            }

            r["profiles"] = list;

            if (message != null) r["message"] = message;

            return r;

        }

        private Dictionary<string, object> ListProfiles() { return ProfilesPayload(null); }

        private Dictionary<string, object> SaveProfile(Dictionary<string, object> msg)

        {

            string nombre = (GS(msg, "nombre") ?? "").Trim();

            if (nombre.Length == 0) return Fail("Poné un nombre para el perfil.");

            if (nombre == SiderBackup.ComoEstabaNombre) return Fail("El perfil \u00abComo estaba\u00bb es protegido: no se sobrescribe.");

            var profiles = SiderBackup.LoadProfiles(_cfg);

            profiles.RemoveAll(x => x.Nombre == nombre);

            profiles.Add(SiderBackup.CaptureProfile(_cfg, nombre, false));

            SiderBackup.SaveProfiles(_cfg, profiles);

            return ProfilesPayload("Perfil \u00ab" + nombre + "\u00bb guardado con el estado actual de módulos.");

        }

        private Dictionary<string, object> ApplyProfile(Dictionary<string, object> msg)

        {

            string nombre = GS(msg, "nombre") ?? "";

            var profile = SiderBackup.LoadProfiles(_cfg).FirstOrDefault(x => x.Nombre == nombre);

            if (profile == null) return Fail("El perfil ya no existe.");

            AutoPoint("antes de aplicar perfil");

            string err = SiderBackup.ApplyProfile(_cfg, profile);

            if (err != null) return Fail(err);

            var r = GetSider(); r["message"] = "Perfil \u00ab" + nombre + "\u00bb aplicado.";

            return r;

        }

        private Dictionary<string, object> DeleteProfile(Dictionary<string, object> msg)

        {

            string nombre = GS(msg, "nombre") ?? "";

            var profiles = SiderBackup.LoadProfiles(_cfg);

            var target = profiles.FirstOrDefault(x => x.Nombre == nombre);

            if (target == null) return Fail("El perfil ya no existe.");

            if (target.EsComoEstaba) return Fail("El perfil \u00abComo estaba\u00bb no se puede borrar.");

            profiles.Remove(target);

            SiderBackup.SaveProfiles(_cfg, profiles);

            return ProfilesPayload("Perfil borrado.");

        }

        // ---------------- Fase 3: Mundo N.º 1 (solo registro) ----------------

        private Dictionary<string, object> WorldPayload(string message)

        {

            var r = Ok();

            var mundo = MundoRegistry.Load(_cfg).FirstOrDefault();

            r["registered"] = mundo != null;

            if (mundo != null)

            {

                r["world"] = new Dictionary<string, object>

                {

                    { "nombre", mundo.Nombre },

                    { "creado", mundo.Creado.ToString("dd/MM/yyyy HH:mm") },

                    { "actualizado", mundo.Actualizado.ToString("dd/MM/yyyy HH:mm") },

                    { "gameFolder", mundo.GameFolder }, { "siderFolder", mundo.SiderFolder },

                    { "pesdbFolder", mundo.PesdbFolder }, { "paqueteDir", mundo.PaqueteDir },

                    { "editFile", mundo.EditFile }, { "savesFolder", mundo.SavesFolder },

                    { "totalCompeticiones", mundo.TotalCompeticiones },

                    { "totalEquipos", mundo.TotalEquipos },

                    { "totalJugadores", mundo.TotalJugadores },

                    { "veredicto", mundo.Veredicto }, { "puntoAnclaId", mundo.PuntoAnclaId },

                    { "ligas", mundo.Ligas.Select(l => new Dictionary<string, object>

                        {

                            { "compId", l.CompId }, { "nombre", l.Nombre },

                            { "regId", l.RegId }, { "equipos", l.Equipos }

                        }).ToList<object>() }

                };

            }

            if (message != null) r["message"] = message;

            return r;

        }

        private Dictionary<string, object> GetWorld() { return WorldPayload(null); }

        private Dictionary<string, object> RegisterWorld(Dictionary<string, object> msg)

        {

            if (string.IsNullOrEmpty(_cfg.SiderFolder) || !Directory.Exists(_cfg.SiderFolder)) return Fail("Configurá primero la carpeta de Sider.");

            string veredicto = "";

            try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

            var ancla = SiderBackup.Create(_cfg, "registro de mundo", true, veredicto);

            var mundos = MundoRegistry.Load(_cfg);

            var mundo = mundos.FirstOrDefault();

            bool esNuevo = mundo == null;

            if (esNuevo)

            {

                mundo = new MundoInfo { Creado = DateTime.Now };

                mundos.Add(mundo);

            }

            string nombreMsg = (GS(msg, "nombre") ?? "").Trim();

            if (nombreMsg.Length > 0) mundo.Nombre = nombreMsg;

            mundo.Actualizado = DateTime.Now;

            mundo.Activo = true;

            mundo.GameFolder = _cfg.GameFolder ?? "";

            mundo.SiderFolder = _cfg.SiderFolder ?? "";

            mundo.PesdbFolder = _cfg.PesdbFolder ?? "";

            mundo.PaqueteDir = MundoRegistry.PackageDir(_cfg);

            mundo.EditFile = _cfg.EditFile ?? "";

            mundo.SavesFolder = MundoRegistry.DetectSavesFolder();

            mundo.Veredicto = veredicto;

            mundo.PuntoAnclaId = ancla != null ? ancla.Id : (mundo.PuntoAnclaId ?? "");

            mundo.Ligas = new List<MundoLiga>();

            if (_db != null)

            {

                mundo.TotalCompeticiones = _db.Competitions.Count;

                mundo.TotalEquipos = _db.Teams.Count;

                mundo.TotalJugadores = _db.Players.Count;

                foreach (var c in GetCustomCompetitions())

                {

                    int regId = 0;

                    var reg = _db.Regulations.FirstOrDefault(x => x.IdCompetition == c.Id);

                    if (reg != null) regId = reg.Id;

                    int equipos = _db.Entries.Where(e => e.CompetitionId == c.Id).Select(e => (int)e.TeamId).Distinct().Count();

                    mundo.Ligas.Add(new MundoLiga { CompId = c.Id, Nombre = c.Name ?? "", RegId = regId, Equipos = equipos });

                }

            }

            MundoRegistry.Save(_cfg, mundos);

            return WorldPayload(esNuevo

                ? "Mundo 1 registrado: tu instalación actual ya tiene nombre y foto. No se cambió ningún comportamiento."

                : "Registro del mundo actualizado con el estado actual.");

        }

        private Dictionary<string, object> RenameWorld(Dictionary<string, object> msg)

        {

            string nombre = (GS(msg, "nombre") ?? "").Trim();

            if (nombre.Length == 0) return Fail("Poné un nombre para el mundo.");

            var mundos = MundoRegistry.Load(_cfg);

            string id = GS(msg, "id");

            var mundo = !string.IsNullOrEmpty(id) ? mundos.FirstOrDefault(m => m.Id == id) : mundos.FirstOrDefault();

            if (mundo == null) return Fail("Todavía no hay un mundo registrado.");

            mundo.Nombre = nombre;

            MundoRegistry.Save(_cfg, mundos);

            return WorldsPayload("Mundo renombrado a \u00ab" + nombre + "\u00bb.");

        }

        // ---------------- Punto A FL26: intercambio de mundos ----------------

        // ---------------- Mapas de servidores (Punto B) ----------------

        private Dictionary<string, object> ListTeamOptions()

        {

            var r = Ok();

            var list = new List<object>();

            if (_db != null)

                foreach (var t in _db.Teams.OrderBy(t => t.NameSpanish ?? ""))

                    list.Add(new Dictionary<string, object> { { "id", t.Id }, { "name", t.NameSpanish ?? ("Equipo " + t.Id) } });

            r["teams"] = list;

            return r;

        }

        private Dictionary<string, object> ServerMapPayload(Dictionary<string, object> msg)

        {

            var r = Ok();

            var def = ServerDef.ById(GS(msg, "server", "stadium"));

            r["server"] = def.Id;

            r["serverName"] = def.Nombre;

            r["keyKind"] = def.KeyKind ?? "team";

            string dir = ServerMapDoc.ServerDir(_cfg, def);

            string mapPath = ServerMapDoc.MapPath(_cfg, def);

            bool installed = dir.Length > 0 && Directory.Exists(dir);

            r["installed"] = installed;

            r["dir"] = dir;

            r["mapPath"] = mapPath;

            var doc = ServerMapDoc.Load(mapPath);

            r["mapExists"] = doc.Exists;

            var targets = ServerMapDoc.ListTargets(dir);

            if (def.Id == "commentary" && targets.Count == 0)

            {

                targets = new List<string> {

                    "spa", "sam", "eng", "use", "bra", "por", "ita", "fra", "ger", "jpn", "ara", "kor", "tur", "rus", "dut"

                };

            }

            r["targets"] = targets;

            var targetSet = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);

            var dups = new HashSet<int>(doc.DuplicateTeamIds());

            var entries = new List<object>();

            bool isComp = (def.KeyKind == "comp");

            foreach (var e in doc.Entries())

            {

                string entityName = "";

                if (isComp)

                {

                    if (_db != null && _db.Competitions != null)

                    {

                        var c = _db.Competitions.FirstOrDefault(x => x.Id == e.TeamId);

                        if (c != null) entityName = c.Name ?? ("Competición " + c.Id);

                    }

                }

                else

                {

                    TeamRecord t;

                    if (_db != null && _db.TeamsById != null && _db.TeamsById.TryGetValue((uint)e.TeamId, out t) && t != null)

                        entityName = t.NameSpanish ?? ("Equipo " + t.Id);

                }

                entries.Add(new Dictionary<string, object>

                {

                    { "id", e.TeamId },

                    { "teamId", e.TeamId },

                    { "name", entityName },

                    { "teamName", entityName },

                    { "target", e.Target },

                    { "targetExists", def.Id == "commentary" || targetSet.Contains(e.Target) },

                    { "duplicate", dups.Contains(e.TeamId) }

                });

            }

            r["entries"] = entries;

            // Enviar opciones elegibles según keyKind (competiciones o equipos)

            var entityOptions = new List<object>();

            if (isComp)

            {

                if (_db != null && _db.Competitions != null)

                {

                    foreach (var c in _db.Competitions.OrderBy(x => x.Name ?? ""))

                    {

                        entityOptions.Add(new Dictionary<string, object> { { "id", c.Id }, { "name", (c.Name ?? ("Competición " + c.Id)) + " (#" + c.Id + ")" } });

                    }

                }

            }

            else

            {

                if (_db != null && _db.Teams != null)

                {

                    foreach (var t in _db.Teams.OrderBy(x => x.NameSpanish ?? ""))

                    {

                        entityOptions.Add(new Dictionary<string, object> { { "id", t.Id }, { "name", (t.NameSpanish ?? ("Equipo " + t.Id)) + " (#" + t.Id + ")" } });

                    }

                }

            }

            r["entityOptions"] = entityOptions;

            return r;

        }

        private Dictionary<string, object> SetServerMapEntry(Dictionary<string, object> msg)

        {

            var def = ServerDef.ById(GS(msg, "server", "stadium"));

            int id = GI(msg, "id", GI(msg, "teamId", 0));

            string target = GS(msg, "target", "").Trim();

            if (id <= 0) return Fail(def.KeyKind == "comp" ? "Elegí una competición." : "Elegí un equipo.");

            if (target.Length == 0) return Fail("Elegí una carpeta o valor destino.");

            string dir = ServerMapDoc.ServerDir(_cfg, def);

            if (dir.Length == 0 || !Directory.Exists(dir))

                return Fail("El servidor " + def.DirName + " no está en tu carpeta content.");

            string mapPath = ServerMapDoc.MapPath(_cfg, def);

            try { SiderBackup.Create(_cfg, "antes de editar el mapa de " + def.Nombre.ToLowerInvariant(), true, ""); } catch { }

            var doc = ServerMapDoc.Load(mapPath);

            if (!doc.Exists)

            {

                Directory.CreateDirectory(dir);

                File.WriteAllText(mapPath, "# " + def.MapFile + " — creado por PES2021EditorPro\r\n", new UTF8Encoding(false));

                doc = ServerMapDoc.Load(mapPath);

            }

            bool added = doc.Set(def, id, target);

            doc.Save();

            var r = ServerMapPayload(msg);

            r["message"] = (added ? "Asignación creada: " : "Asignación actualizada: ") + id + " → " + target + " (en " + def.MapFile + ").";

            return r;

        }

        private Dictionary<string, object> RemoveServerMapEntry(Dictionary<string, object> msg)

        {

            var def = ServerDef.ById(GS(msg, "server", "stadium"));

            int id = GI(msg, "id", GI(msg, "teamId", 0));

            if (id <= 0) return Fail(def.KeyKind == "comp" ? "Competición inválida." : "Equipo inválido.");

            string mapPath = ServerMapDoc.MapPath(_cfg, def);

            var doc = ServerMapDoc.Load(mapPath);

            if (!doc.Exists) return Fail("El mapa no existe todavía.");

            try { SiderBackup.Create(_cfg, "antes de editar el mapa de " + def.Nombre.ToLowerInvariant(), true, ""); } catch { }

            int n = doc.Remove(id);

            if (n == 0) return Fail(def.KeyKind == "comp" ? "Esa competición no tenía asignación en este mapa." : "Ese equipo no tenía asignación en este mapa.");

            doc.Save();

            var r = ServerMapPayload(msg);

            r["message"] = "Asignación quitada (" + n + (n == 1 ? " línea" : " líneas") + ").";

            return r;

        }

        private Dictionary<string, object> WorldDto(MundoInfo m)

        {

            return new Dictionary<string, object>

            {

                { "id", m.Id }, { "nombre", m.Nombre }, { "activo", m.Activo },

                { "creado", m.Creado.ToString("dd/MM/yyyy HH:mm") },

                { "actualizado", m.Actualizado.ToString("dd/MM/yyyy HH:mm") },

                { "savesFolder", m.SavesFolder }, { "veredicto", m.Veredicto },

                { "totalCompeticiones", m.TotalCompeticiones },

                { "totalEquipos", m.TotalEquipos }, { "totalJugadores", m.TotalJugadores },

                { "tieneStore", MundoSwitch.HasStore(_cfg, m.Id) },

                { "ligas", (m.Ligas ?? new List<MundoLiga>()).Select(l => new Dictionary<string, object>

                    {

                        { "compId", l.CompId }, { "nombre", l.Nombre },

                        { "regId", l.RegId }, { "equipos", l.Equipos }

                    }).ToList<object>() }

            };

        }

        private Dictionary<string, object> WorldsPayload(string message)

        {

            var r = Ok();

            var mundos = MundoRegistry.Load(_cfg);

            r["registered"] = mundos.Count > 0;

            r["worlds"] = mundos.Select(m => (object)WorldDto(m)).ToList();

            if (message != null) r["message"] = message;

            return r;

        }

        private Dictionary<string, object> ListWorlds() { return WorldsPayload(null); }

        private Dictionary<string, object> CreateWorld(Dictionary<string, object> msg)

        {

            if (string.IsNullOrEmpty(_cfg.SiderFolder) || !Directory.Exists(_cfg.SiderFolder)) return Fail("Configurá primero la carpeta de Sider.");

            string error;

            var mundo = MundoSwitch.CreateWorld(_cfg, _db, GS(msg, "nombre"), GS(msg, "modo") == "limpia", out error);

            if (mundo == null) return Fail(error);

            return WorldsPayload("Mundo «" + mundo.Nombre + "» creado. Activalo cuando quieras desde esta misma pantalla.");

        }

        private Dictionary<string, object> SwitchWorld(Dictionary<string, object> msg)

        {

            string id = GS(msg, "id");

            string veredicto = "";

            try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

            SiderBackup.Create(_cfg, "antes de cambiar de mundo", true, veredicto);

            string error;

            if (!MundoSwitch.SwitchTo(_cfg, _db, id, out error)) return Fail(error);

            // Recargar la base: el paquete ahora contiene la pesdb del mundo entrante

            string folder = _cfg.PesdbFolder;

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))

            {

                try

                {

                    _db = new DatabaseManager();

                    _db.ConfirmExternalWrite = delegate { return true; };

                    _db.Load(folder);

                }

                catch { }

            }

            var mundos = MundoRegistry.Load(_cfg);

            var activo = mundos.FirstOrDefault(m => m.Activo);

            return WorldsPayload("Mundo activo: «" + (activo != null ? activo.Nombre : id) + "». La base se recargó con sus ligas. Si el juego estaba abierto, cerralo y volvé a abrirlo.");

        }

        private Dictionary<string, object> DeleteWorld(Dictionary<string, object> msg)

        {

            string error;

            if (!MundoSwitch.DeleteWorld(_cfg, GS(msg, "id"), out error)) return Fail(error);

            return WorldsPayload("Mundo borrado (quedó en la papelera de editorpro-respaldos, recuperable a mano).");

        }

        // ---------------- Fase 4: presets de ligas reales ----------------

        private Dictionary<string, object> GetCensus()

        {

            RequireDb();

            var free = _db.GetFreePlayableTeams(0);

            var grupos = new Dictionary<ushort, List<TeamRecord>>();

            foreach (var tm in free)

            {

                List<TeamRecord> l;

                if (!grupos.TryGetValue(tm.CountryId, out l)) { l = new List<TeamRecord>(); grupos[tm.CountryId] = l; }

                l.Add(tm);

            }

            var paises = new List<object>();

            foreach (var g in grupos.OrderByDescending(x => x.Value.Count))

            {

                string nombrePais = g.Key == 0 ? "Sin país" : CountryName(g.Key);

                if (string.IsNullOrEmpty(nombrePais)) nombrePais = "País " + g.Key;

                paises.Add(new Dictionary<string, object>

                {

                    { "countryId", (int)g.Key }, { "nombre", nombrePais }, { "cantidad", g.Value.Count },

                    { "equipos", g.Value.OrderBy(x => x.NameSpanish).Select(x => new Dictionary<string, object>

                        {

                            { "id", (int)x.Id }, { "nombre", x.NameSpanish ?? x.NameEnglish ?? "" }

                        }).ToList<object>() }

                });

            }

            var r = Ok();

            r["total"] = free.Count;

            r["paises"] = paises;

            return r;

        }

        private Dictionary<string, object> ListPresets()

        {

            List<string> errores;

            var defs = PresetEngine.ListPresets(_cfg, out errores);

            var r = Ok();

            r["presetsDir"] = PresetEngine.PresetsDir(_cfg) ?? "";

            r["presets"] = defs.Select(d => new Dictionary<string, object>

            {

                { "nombre", d.Nombre }, { "archivo", d.Archivo }, { "pais", d.Pais },

                { "ligas", d.Ligas.Count }, { "equipos", d.Ligas.Sum(x => x.Equipos.Count) }

            }).ToList<object>();

            r["errores"] = errores;

            return r;

        }

        private static Dictionary<string, object> PreviewDto(PresetPreview pv)

        {

            var r = Ok();

            r["preset"] = new Dictionary<string, object>

            {

                { "nombre", pv.Preset.Nombre }, { "archivo", pv.Preset.Archivo }, { "pais", pv.Preset.Pais }

            };

            r["ligas"] = pv.Ligas.Select(l => new Dictionary<string, object>

            {

                { "nombre", l.Nombre }, { "tier", l.Tier }, { "compId", l.CompIdAsignado },

                { "viable", l.Viable }, { "motivo", l.MotivoSalto }, { "padre", l.PadreNombre },

                { "padreCompId", l.PadreCompId }, { "asignados", l.Asignados },

                { "equipos", l.Equipos.Select(e => new Dictionary<string, object>

                    {

                        { "pedido", e.Pedido }, { "estado", e.Estado }, { "teamId", (int)e.TeamId },

                        { "nombreReal", e.NombreReal }, { "detalle", e.Detalle }

                    }).ToList<object>() }

            }).ToList<object>();

            return r;

        }

        private Dictionary<string, object> PreviewPreset(Dictionary<string, object> msg)

        {

            RequireDb();

            var def = PresetEngine.LoadPreset(_cfg, GS(msg, "archivo"));

            if (def == null) return Fail("No se encontró el preset.");

            return PreviewDto(PresetEngine.Preview(_db, def));

        }

        private Dictionary<string, object> ApplyPreset(Dictionary<string, object> msg)

        {

            RequireDb();

            var def = PresetEngine.LoadPreset(_cfg, GS(msg, "archivo"));

            if (def == null) return Fail("No se encontró el preset.");

            string veredicto = "";

            try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

            SiderBackup.Create(_cfg, "antes de preset " + def.Nombre, true, veredicto);

            var pv = PresetEngine.Preview(_db, def);

            List<int> createdIds;

            var results = PresetEngine.Apply(_db, pv, out createdIds);

            if (createdIds.Count > 0)

            {

                _db.HasUnsavedChanges = true;

                try

                {

                    var mundos = MundoRegistry.Load(_cfg);

                    var mundo = mundos.FirstOrDefault();

                    if (mundo != null)

                    {

                        mundo.PresetCompIds.AddRange(createdIds.Where(x => !mundo.PresetCompIds.Contains(x)));

                        MundoRegistry.Save(_cfg, mundos);

                    }

                }

                catch { }

            }

            var r = Ok();

            r["results"] = results.Select(x => new Dictionary<string, object>

            {

                { "nombre", x.Nombre }, { "creada", x.Creada }, { "compId", x.CompId }, { "mensaje", x.Mensaje }

            }).ToList<object>();

            r["createdIds"] = createdIds;

            r["message"] = createdIds.Count > 0

                ? "Preset aplicado: " + createdIds.Count + " liga(s) creada(s). Acordate de Guardar (.bin) y redesplegar."

                : "El preset no creó ninguna liga: mirá la vista previa para ver los motivos.";

            return r;

        }

        // ---------------- Fase 5A: completar planteles ----------------

        private Dictionary<string, object> PreviewSquad(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            var pv = SquadGenerator.Preview(_db, teamId, GI(msg, "target", SquadGenerator.DefaultTarget));

            var r = Ok();

            r["teamId"] = (int)teamId;

            r["teamName"] = pv.TeamName;

            r["current"] = pv.CurrentCount;

            r["target"] = pv.Target;

            r["final"] = pv.FinalCount;

            r["hasTactics"] = pv.HasTactics;

            r["warnings"] = pv.Warnings;

            r["lineas"] = pv.Lineas.Select(l => new Dictionary<string, object>

            {

                { "linea", l.Linea }, { "actual", l.Actual }, { "cuota", l.Cuota },

                { "fichados", l.Fichados.Select(p => new Dictionary<string, object>

                    {

                        { "id", (int)p.Id }, { "nombre", p.Name ?? "" },

                        { "pos", p.PositionName ?? "" }, { "overall", p.Overall }

                    }).ToList<object>() }

            }).ToList<object>();

            return r;

        }

        private Dictionary<string, object> ApplySquad(Dictionary<string, object> msg)

        {

            RequireDb();

            uint teamId = (uint)GL(msg, "teamId", 0);

            TeamRecord team;

            string teamName = _db.TeamsById != null && _db.TeamsById.TryGetValue(teamId, out team) ? (team.NameSpanish ?? "") : ("equipo " + teamId);

            string veredicto = "";

            try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

            SiderBackup.Create(_cfg, "antes de completar plantel " + teamName, true, veredicto);

            var res = SquadGenerator.Apply(_db, teamId, GI(msg, "target", SquadGenerator.DefaultTarget));

            _db.HasUnsavedChanges = true;

            var r = Ok();

            r["signed"] = res.Signed;

            r["skipped"] = res.Skipped;

            r["final"] = res.FinalCount;

            r["formation"] = res.FormationApplied;

            r["message"] = "Plantel de " + teamName + ": " + res.Signed + " fichajes, plantel final de " + res.FinalCount +

                (res.FormationApplied ? ". Se aplicó la formación por defecto (4-2-3-1)" : "") +

                ". Acordate de Guardar (.bin).";

            return r;

        }

        // ---------------- Fase 5B: importador NewLife ----------------

        private Dictionary<string, object> PreviewNewLife(Dictionary<string, object> msg)

        {

            RequireDb();

            NlRelease rel;

            try { rel = NewLifeImporter.Load(GS(msg, "folder")); }

            catch (Exception ex) { return Fail(ex.Message); }

            var pv = NewLifeImporter.Preview(_db, rel);

            var r = Ok();

            r["name"] = rel.Name;

            r["version"] = rel.Version;

            r["warnings"] = rel.Warnings;

            r["clubs"] = pv.Clubs.Select(cp => new Dictionary<string, object>

            {

                { "nombre", cp.Club.Name }, { "liga", cp.Club.League }, { "pais", (int)cp.Club.Country },

                { "estado", cp.Estado }, { "entran", cp.Entran.Count }, { "nuevos", cp.Nuevos },

                { "reutilizados", cp.Reutilizados }, { "quedan", cp.QuedanEnSuClub },

                { "fuera", cp.FueraDelPlantel }, { "avisos", cp.Avisos }

            }).ToList<object>();

            return r;

        }

        private Dictionary<string, object> ApplyNewLife(Dictionary<string, object> msg)

        {

            RequireDb();

            NlRelease rel;

            try { rel = NewLifeImporter.Load(GS(msg, "folder")); }

            catch (Exception ex) { return Fail(ex.Message); }

            string veredicto = "";

            try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

            SiderBackup.Create(_cfg, "antes de importar clubes reales " + rel.Version, true, veredicto);

            var pv = NewLifeImporter.Preview(_db, rel);

            var results = NewLifeImporter.Apply(_db, pv);

            var r = Ok();

            r["results"] = results.Select(x => new Dictionary<string, object>

            {

                { "club", x.Club }, { "creado", x.Creado }, { "teamId", (int)x.TeamId }, { "mensaje", x.Mensaje }

            }).ToList<object>();

            int creados = results.Count(x => x.Creado);

            r["message"] = creados > 0

                ? "Importación completa: " + creados + " club(es) nuevo(s) con sus planteles. Acordate de Guardar (.bin)."

                : "La release no creó clubes nuevos (ya estaban en el juego o ya importados).";

            return r;

        }

        // ---------------- Importador CSV simple de jugadores ----------------

        private Dictionary<string, object> PreviewCsvPlayers(Dictionary<string, object> msg)

        {

            RequireDb();

            var warnings = new List<string>();

            List<NlClub> clubs;

            try { clubs = CsvImporter.Load(_db, GS(msg, "path"), (ushort)GI(msg, "countryId", 0), warnings, GS(msg, "format", "simple")); }

            catch (Exception ex) { return Fail(ex.Message); }

            var pv = CsvImporter.Preview(_db, clubs, warnings);

            var r = Ok();

            r["warnings"] = pv.Warnings;

            r["clubs"] = pv.Items.Select(cp => new Dictionary<string, object>

            {

                { "nombre", cp.Club.Name }, { "pais", (int)cp.Club.Country }, { "estado", cp.Estado },

                { "entran", cp.Entran.Count }, { "nuevos", cp.Nuevos }, { "reutilizados", cp.Reutilizados },

                { "quedan", cp.QuedanEnSuClub }, { "fuera", cp.FueraDelPlantel }, { "avisos", cp.Avisos }

            }).ToList<object>();

            return r;

        }

        private Dictionary<string, object> ApplyCsvPlayers(Dictionary<string, object> msg)

        {

            RequireDb();

            var warnings = new List<string>();

            List<NlClub> clubs;

            try { clubs = CsvImporter.Load(_db, GS(msg, "path"), (ushort)GI(msg, "countryId", 0), warnings, GS(msg, "format", "simple")); }

            catch (Exception ex) { return Fail(ex.Message); }

            string veredicto = "";

            try { veredicto = HealthChecker.Run(_cfg, _db).VeredictoTexto; } catch { }

            SiderBackup.Create(_cfg, "antes de importar CSV de jugadores", true, veredicto);

            var pv = CsvImporter.Preview(_db, clubs, warnings);

            var results = CsvImporter.Apply(_db, pv);

            var r = Ok();

            r["results"] = results.Select(x => new Dictionary<string, object>

            {

                { "club", x.Club }, { "creado", x.Creado }, { "teamId", (int)x.TeamId }, { "mensaje", x.Mensaje }

            }).ToList<object>();

            int creados = results.Count(x => x.Creado);

            r["message"] = creados > 0

                ? "Importación completa: " + creados + " club(es) con plantel cargado. Acordate de Guardar (.bin)."

                : "El CSV no cargó planteles nuevos (los equipos ya existían con plantel o los jugadores están en otros clubes).";

            return r;

        }

        private Dictionary<string, object> FixHealthItem(Dictionary<string, object> msg)

        {

            string id = GS(msg, "id") ?? "";

            if (string.IsNullOrEmpty(_cfg.SiderFolder) || !Directory.Exists(_cfg.SiderFolder)) return Fail("Configurá primero la carpeta de Sider.");

            string hecho;

            switch (id)

            {

                case "S2":

                    SiderBackup.Create(_cfg, "antes de arreglo S2", true, "");

                    hecho = SiderIniManager.StripBom(_cfg.SiderFolder) ? "BOM quitado del sider.ini." : "El sider.ini ya no tenía BOM.";

                    break;

                case "S3":

                    SiderBackup.Create(_cfg, "antes de arreglo S3", true, "");

                    hecho = SiderIniManager.EnsureLuajitEnabled(_cfg.SiderFolder);

                    break;

                case "S5":

                    SiderBackup.Create(_cfg, "antes de arreglo S5", true, "");

                    SiderIniManager.SetModuleActive(_cfg.SiderFolder, "league_manager.lua", true);

                    hecho = "league_manager.lua activado en sider.ini.";

                    break;

                case "D2":

                    RequireDb();

                    AutoPoint("antes de arreglo D2");

                    var rd = DeploySiderCore();

                    hecho = "Módulos redesplegados desde la base actual.";

                    break;

                case "P2":

                case "P3":

                    RequireDb();

                    AutoPoint("antes de arreglo " + id);

                    var rs = SaveDb();

                    if (rs.ContainsKey("ok") && !Convert.ToBoolean(rs["ok"])) return rs;

                    hecho = "Base guardada: el paquete quedó sincronizado.";

                    break;

                case "R3":

                    SiderBackup.Create(_cfg, "antes de arreglo R3", true, "");

                    foreach (string sub in new[] { "livecpk", "modules", "content" })

                        Directory.CreateDirectory(Path.Combine(_cfg.SiderFolder, sub));

                    hecho = "Subcarpetas de Sider creadas.";

                    break;

                default:

                    return Fail("Ese chequeo no tiene arreglo automático.");

            }

            var r = GetHealth();

            r["message"] = hecho;

            return r;

        }

        private Dictionary<string, object> GetLuaPreview()

        {

            RequireDb();

            var r = Ok();

            r["lua"] = SiderLuaGenerator.GenerateLuaScript(GetCustomCompetitions(), _db.Regulations, _db);

            return r;

        }

        private Dictionary<string, object> SaveLua()

        {

            RequireDb();

            if (string.IsNullOrEmpty(_cfg.ModulesFolder)) return Fail("No hay carpeta de módulos configurada.");

            Directory.CreateDirectory(_cfg.ModulesFolder);

            string path = Path.Combine(_cfg.ModulesFolder, "league_manager.lua");

            SiderLuaGenerator.SaveLuaToFile(path, GetCustomCompetitions(), _db.Regulations, _db);

            var r = Ok(); r["path"] = path; return r;

        }

        private Dictionary<string, object> GetWorldTxt()

        {

            RequireDb();

            var r = Ok();

            r["world"] = SiderLuaGenerator.GenerateWorldTxt(GetCustomCompetitions(), _db.Regulations, _db);

            return r;

        }

        // ------------------------- emblemas e imagenes -------------------------

        private static string ImageToBase64Png(Image img)

        {

            if (img == null) return null;

            using (var ms = new MemoryStream())

            {

                img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                return Convert.ToBase64String(ms.ToArray());

            }

        }

        private Dictionary<string, object> GetEmblem(Dictionary<string, object> msg, bool team)

        {

            RequireDb();

            var r = Ok();

            Image img = team ? _db.GetTeamEmblem((uint)GL(msg, "id", 0)) : _db.GetCompetitionEmblem(GI(msg, "id", 0));

            r["png"] = ImageToBase64Png(img);

            return r;

        }

        private Dictionary<string, object> SetEmblem(Dictionary<string, object> msg, bool team)

        {

            RequireDb();

            string b64 = GS(msg, "base64");

            if (string.IsNullOrEmpty(b64)) return Fail("No llegó la imagen.");

            byte[] bytes = Convert.FromBase64String(b64);

            using (var ms = new MemoryStream(bytes))

            using (var img = Image.FromStream(ms))

            {

                var copy = new Bitmap(img);

                if (team) _db.SaveTeamEmblem((uint)GL(msg, "id", 0), copy);

                else _db.SaveCompetitionEmblem(GI(msg, "id", 0), copy);

            }

            _db.HasUnsavedChanges = true;

            return Ok();

        }

        // ===== Completado 2026-11-06: galeria de logos del juego, opciones de alta, plantilla, escaneo =====
        private Dictionary<string, object> ListGameEmblems()
        {
            RequireDb();
            var r = Ok();
            string cpk = GameEmblemProvider.FindDt15CpkPath(_db.DatabasePath);
            r["cpkFound"] = !string.IsNullOrEmpty(cpk);
            var items = new List<object>();
            if (!string.IsNullOrEmpty(cpk))
            {
                foreach (var it in GameEmblemProvider.LoadGameCompetitionEmblems(cpk, _db))
                {
                    string png = null;
                    try { using (var img = GameEmblemProvider.ExtractEmblemImage(cpk, it.Entry)) { png = ImageToBase64Png(img); } } catch { }
                    items.Add(new Dictionary<string, object> { { "compId", it.CompetitionId }, { "name", it.Name ?? "" }, { "png", png } });
                }
            }
            r["items"] = items;
            return r;
        }
        private Dictionary<string, object> SetCompEmblemFromGame(Dictionary<string, object> msg)
        {
            RequireDb();
            int target = GI(msg, "id", 0);
            int gameCompId = GI(msg, "gameCompId", 0);
            string cpk = GameEmblemProvider.FindDt15CpkPath(_db.DatabasePath);
            if (string.IsNullOrEmpty(cpk)) return Fail("No se encontró dt15_x64.cpk subiendo desde la carpeta de tu base.");
            GameEmblemItem found = null;
            foreach (var it in GameEmblemProvider.LoadGameCompetitionEmblems(cpk, _db)) if (it.CompetitionId == gameCompId) { found = it; break; }
            if (found == null) return Fail("Ese logo no está en la galería del juego.");
            using (var img = GameEmblemProvider.ExtractEmblemImage(cpk, found.Entry))
            {
                if (img == null) return Fail("No se pudo extraer el logo del juego.");
                _db.SaveCompetitionEmblem(target, new System.Drawing.Bitmap(img));
            }
            _db.HasUnsavedChanges = true;
            return Ok();
        }
        private Dictionary<string, object> CreateTeamOptions()
        {
            RequireDb();
            var r = Ok();
            r["countries"] = CountriesDto()["countries"];
            var stadiums = new List<object>();
            foreach (var st in _db.Stadiums.OrderBy(x => x.Name)) stadiums.Add(new Dictionary<string, object> { { "id", st.Id }, { "name", st.Name ?? "" } });
            r["stadiums"] = stadiums;
            var coaches = new List<object>();
            foreach (var co in _db.Coaches.OrderBy(x => x.Name)) coaches.Add(new Dictionary<string, object> { { "id", co.Id }, { "name", co.Name ?? "" } });
            r["coaches"] = coaches;
            var leagues = new List<object>();
            foreach (var c in _db.Competitions.OrderBy(c => c.Id)) { var d = CompDto(c); if ((bool)d["isLeague"]) leagues.Add(new Dictionary<string, object> { { "id", c.Id }, { "name", (string)d["name"] } }); }
            r["leagues"] = leagues;
            r["nextTeamId"] = (long)_db.GetNextFreeTeamId();
            return r;
        }
        private Dictionary<string, object> CreatePlayerOptions()
        {
            RequireDb();
            var r = Ok();
            r["countries"] = CountriesDto()["countries"];
            r["styles"] = PlayerBitHelper.PlayingStyleNames;
            var teams = new List<object>();
            foreach (var t in _db.Teams.OrderBy(t => t.DisplayName)) teams.Add(new Dictionary<string, object> { { "id", (long)t.Id }, { "name", t.DisplayName }, { "playerCount", _db.GetPlayerCount(t.Id) } });
            r["teams"] = teams;
            return r;
        }
        private Dictionary<string, object> ApplyLeagueTemplate(Dictionary<string, object> msg)
        {
            RequireDb();
            byte targetId = (byte)GI(msg, "targetId", 0);
            byte baseId = (byte)GI(msg, "baseId", 0);
            var target = RegOf(targetId); var baseReg = RegOf(baseId);
            if (target == null || baseReg == null) return Fail("No se encontró el reglamento de la liga destino o de la liga base.");
            target.Changes = baseReg.Changes;
            target.RegisteredPlayers = baseReg.RegisteredPlayers;
            target.LeagueEachTeamMatches = baseReg.LeagueEachTeamMatches;
            target.LeagueClassification = baseReg.LeagueClassification;
            target.CupFormat = baseReg.CupFormat;
            target.NumGroups = baseReg.NumGroups;
            target.NumClassifiedTeams = baseReg.NumClassifiedTeams;
            target.ExtraTime = baseReg.ExtraTime;
            target.PenaltyShootout = baseReg.PenaltyShootout;
            target.AwayGoals = baseReg.AwayGoals;
            target.ThirdPlace = baseReg.ThirdPlace;
            target.SingleLeg = baseReg.SingleLeg;
            target.FinalChampionship = baseReg.FinalChampionship;
            _db.SetLeaguePromote(targetId, _db.GetLeaguePromote(baseId));
            _db.SetLeagueDemote(targetId, _db.GetLeagueDemote(baseId));
            _db.SetLeagueContinental1(targetId, _db.GetLeagueContinental1(baseId));
            _db.SetLeagueContinental2(targetId, _db.GetLeagueContinental2(baseId));
            _db.SetLeagueSeasonType(targetId, _db.GetLeagueSeasonType(baseId));
            _db.SetLeagueAperturaClausura(targetId, _db.GetLeagueAperturaClausura(baseId));
            _db.SetLeagueLinkedCup(targetId, _db.GetLeagueLinkedCup(baseId));
            _db.SetLeagueLinkedSuperCup(targetId, _db.GetLeagueLinkedSuperCup(baseId));
            _db.HasUnsavedChanges = true;
            return Ok();
        }
        private Dictionary<string, object> RepairLiveCpkEmblems()
        {
            RequireDb();
            var r = Ok(); r["repaired"] = _db.RepairLiveCpkEmblems(); return r;
        }
        private Dictionary<string, object> GetIdScan()
        {
            RequireDb();
            var rep = _db.RunCollisionAndIntegrityScan();
            var r = Ok();
            r["nextCompId"] = rep.NextFreeCompetitionId;
            r["nextRegId"] = rep.NextFreeRegulationId;
            r["nextSlot"] = rep.NextFreeSlot;
            return r;
        }
        private Dictionary<string, object> DeleteCompEmblem(Dictionary<string, object> msg)

        {

            RequireDb();

            var r = Ok(); r["deleted"] = _db.DeleteCompetitionEmblem(GI(msg, "id", 0)); return r;

        }

        private Dictionary<string, object> PlayerMiniface(Dictionary<string, object> msg)

        {

            var r = Ok(); r["png"] = null;

            try

            {

                if (!_minifaceReady)

                {

                    MinifaceProvider.Initialize(string.IsNullOrEmpty(_cfg.SiderFolder) ? null : _cfg.SiderFolder);

                    _minifaceReady = true;

                }

                uint pid = (uint)GL(msg, "id", 0);

                using (var bmp = MinifaceProvider.GetMiniface(pid, 128, GS(msg, "pos") ?? "CF", GS(msg, "name") ?? ""))

                {

                    if (bmp != null) r["png"] = ImageToBase64Png(bmp);

                }

            }

            catch { r["png"] = null; }

            return r;

        }

        private Dictionary<string, object> RescanMinifaces()
        {
            string folder = _cfg != null ? _cfg.GetOrDetectMinifaceFolder(true) : null;
            MinifaceProvider.Initialize(folder, true);
            _minifaceReady = true;
            var r = Ok();
            r["minifaceFolder"] = MinifaceProvider.PrimaryFolder ?? folder ?? "";
            r["minifaceCount"] = MinifaceProvider.IndexedCount;
            r["searchFolders"] = MinifaceProvider.SearchFolders;
            return r;
        }

        private Dictionary<string, object> OpenFolder(Dictionary<string, object> msg)
        {
            string path = GS(msg, "path");
            if (string.IsNullOrEmpty(path)) path = _cfg != null ? _cfg.GetOrDetectMinifaceFolder(true) : null;
            if (!string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
                return Ok();
            }
            return Fail("Ruta no válida.");
        }

        // Emblemas desde archivo (la UI elige la ruta con el dialogo nativo

        // y el nucleo lee la imagen: el JS no toca archivos del disco).

        public Dictionary<string, object> SetEmblemFile(string path, long id, bool team)

        {

            RequireDb();

            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Fail("No se encontró la imagen: " + path);

            using (var img = Image.FromFile(path))

            {

                var copy = new Bitmap(img);

                if (team) _db.SaveTeamEmblem((uint)id, copy);

                else _db.SaveCompetitionEmblem((int)id, copy);

            }

            _db.HasUnsavedChanges = true;

            return Ok();

        }

        // ------------------------- EDIT (PesCrypt) -------------------------

        private Dictionary<string, object> EditDto()

        {

            string desc = "";

            try { if (_edit.Desc != null) desc = Encoding.UTF8.GetString(_edit.Desc).Trim('\0', ' '); } catch { }

            string serial = "";

            try { if (_edit.Serial != null) serial = Encoding.Unicode.GetString(_edit.Serial).Trim('\0', ' '); } catch { }

            return new Dictionary<string, object>

            {

                { "path", _edit.FilePath ?? "" }, { "game", _edit.Game ?? "" },

                { "description", desc }, { "serial", serial },

                { "dataSize", _edit.Data != null ? _edit.Data.Length : 0 },

                { "logoPng", _edit.Logo != null ? Convert.ToBase64String(_edit.Logo) : null }

            };

        }

        private Dictionary<string, object> EditLoad(Dictionary<string, object> msg)

        {

            string path = GS(msg, "path");

            if (string.IsNullOrEmpty(path)) path = _cfg.EditFile;

            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Fail("Archivo EDIT no encontrado: " + path);

            _edit = PesCrypt.Load(path);

            var r = Ok(); r["edit"] = EditDto(); return r;

        }

        private void RequireEdit()

        {

            if (_edit == null) throw new InvalidOperationException("No hay ningún EDIT cargado.");

        }

        private Dictionary<string, object> EditSave()

        {

            RequireEdit();

            PesCrypt.Save(_edit, _edit.FilePath, true);

            return Ok();

        }

        private Dictionary<string, object> EditUnlock()

        {

            RequireEdit();

            PesCrypt.SetSteamId(_edit, PesCrypt.GetCurrentWindowsSid());

            var r = Ok(); r["edit"] = EditDto(); return r;

        }

        private Dictionary<string, object> EditSetSid(Dictionary<string, object> msg)

        {

            RequireEdit();

            PesCrypt.SetSteamId(_edit, GS(msg, "sid") ?? "");

            var r = Ok(); r["edit"] = EditDto(); return r;

        }

        private Dictionary<string, object> EditSetDescription(Dictionary<string, object> msg)

        {

            RequireEdit();

            PesCrypt.SetDescription(_edit, GS(msg, "text") ?? "");

            var r = Ok(); r["edit"] = EditDto(); return r;

        }

        private Dictionary<string, object> EditRestoreBackup(Dictionary<string, object> msg)

        {

            string path = GS(msg, "path"); if (string.IsNullOrEmpty(path) && _edit != null) path = _edit.FilePath;

            if (string.IsNullOrEmpty(path)) return Fail("Sin ruta de EDIT.");

            string m; bool okR = PesCrypt.RestoreFl26xBackup(path, out m);

            var r = Ok(); r["restored"] = okR; r["message"] = m ?? ""; return r;

        }

        private Dictionary<string, object> EditInspectFl26(Dictionary<string, object> msg)

        {

            string path = GS(msg, "path"); if (string.IsNullOrEmpty(path) && _edit != null) path = _edit.FilePath;

            if (string.IsNullOrEmpty(path)) return Fail("Sin ruta de EDIT.");

            var info = PesCrypt.InspectFl26xFile(path);

            var r = Ok();

            r["info"] = info == null ? null : new Dictionary<string, object>

            {

                { "exists", info.Exists }, { "isValid", info.IsValid },

                { "magic", info.Magic ?? "" }, { "version", info.Version },

                { "fileSize", info.FileSize }, { "status", info.StatusMessage ?? "" },

                { "teams", (long)info.TeamRecordsCount }, { "squads", (long)info.SquadRecordsCount },

                { "tactics", (long)info.TacticsRecordsCount }, { "managers", (long)info.ManagerRecordsCount },

                { "hasBackup", info.HasBackupPrev }, { "details", info.StatusMessage ?? "" }

            };

            return r;

        }

        // ------------------- archivos (paquetes y CSV) -------------------

        public Dictionary<string, object> ExportLeaguePackageFile(string path, byte compId)

        {

            RequireDb();

            string m; bool okE = LeaguePackageManager.ExportPackage(_db, compId, path, out m);

            if (!okE) return Fail(string.IsNullOrEmpty(m) ? "No se pudo exportar el paquete." : m);

            var r = Ok(); r["exported"] = true; r["message"] = m ?? ""; return r;

        }

        public Dictionary<string, object> ImportLeaguePackageFile(string path)

        {

            RequireDb();

            var res = LeaguePackageManager.ImportPackage(_db, path);

            if (res == null) return Fail("No se pudo importar el paquete de liga.");

            _db.HasUnsavedChanges = true;

            var r = Ok();

            r["warnings"] = (res != null && res.Warnings != null) ? res.Warnings.Cast<object>().ToList() : new List<object>();

            r["teamsMapped"] = res != null && res.TeamIdMap != null ? res.TeamIdMap.Count : 0;

            return r;

        }

        public Dictionary<string, object> ExportTeamsCsvFile(string path)

        {

            RequireDb();

            string res = _db.ExportTeamsToCsv(path);

            var r = Ok(); r["result"] = res ?? ""; return r;

        }

        public Dictionary<string, object> ImportTeamsCsvFile(string path)

        {

            RequireDb();

            int n = _db.ImportTeamsFromCsv(path);

            _db.HasUnsavedChanges = true;

            var r = Ok(); r["imported"] = n;

            r["skipped"] = (_db.LastCsvSkippedTeams ?? new List<string>()).Cast<object>().ToList();

            return r;

        }

        public Dictionary<string, object> EditExportDataFile(string path)

        {

            RequireEdit();

            File.WriteAllBytes(path, _edit.Data);

            return Ok();

        }

        public Dictionary<string, object> EditImportDataFile(string path)

        {

            RequireEdit();

            _edit.Data = File.ReadAllBytes(path);

            var r = Ok(); r["edit"] = EditDto(); return r;

        }

    }

}
