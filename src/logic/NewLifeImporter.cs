// ============================================================================
//  NewLifeImporter.cs  -  Fase 5B: importador de equipos reales (NewLife)
// ----------------------------------------------------------------------------
//  Lee una release en el formato NewLife documentado por FL26 Mod
//  Studio (docs/developers/fork/newlife-format.md, MIT): una carpeta
//  (o partes NewLife-*.zip dentro de ella) con newlife.json +
//  clubs.csv + players.csv, y crea en la base los clubes nuevos con
//  sus planteles reales.
//
//  Reglas de la casa (las de siempre):
//   - Un club que el juego ya tiene (in_game o game_id) NO se toca.
//   - Un jugador que ya existe (game_id, mismo nombre) y esta libre
//     se ficha; si juega en otro club, NO se mueve: se reporta.
//   - Plantel por club: los mejores 30, maximo 3 arqueros (regla de
//     la propia release). El resto no se crea y se reporta.
//   - Jugadores nuevos: id libre menor a 262144 (el maximo del juego)
//     y campos escritos con PlayerBitHelper, el mismo camino que
//     CreatePlayer. Las 25 habilidades y las posiciones calzan bit a
//     bit con tools/playeredit.py de FL26; skills y estilos COM se
//     aplican solo cuando el bit coincide exactamente.
//   - Cada club creado recibe formacion por defecto y roles con la
//     maquinaria de la Fase 5A. Si un plantel queda corto (<11), la
//     previa lo avisa y la ficha del equipo ofrece completarlo (5A).
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EditorLigasPes
{
    public class NlPlayer
    {
        public uint NewlifeId;
        public uint GameId;
        public string Name = "";
        public uint ClubNewlifeId;
        public Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Get(string key) { string v; return Fields.TryGetValue(key, out v) ? v : ""; }
        public int RegPosIndex
        {
            get
            {
                string s = Get("Registered Position").Trim();
                for (int i = 0; i < PlayerRecord.PositionNames.Length; i++)
                    if (string.Equals(PlayerRecord.PositionNames[i], s, StringComparison.OrdinalIgnoreCase)) return i;
                return 12;
            }
        }
        public double RankScore
        {
            get
            {
                double sum = 0; int n = 0;
                foreach (var ab in AbilityMap.Keys)
                {
                    int v;
                    if (int.TryParse(Get(ab), out v)) { sum += v; n++; }
                }
                return n > 0 ? sum / n : 60;
            }
        }
        // Nombre de habilidad en el CSV -> clave de DbAbilBits (bits identicos, verificado)
        public static readonly Dictionary<string, string> AbilityMap = new Dictionary<string, string>
        {
            { "Offensive Awareness", "atk" }, { "Ball Control", "ball_ctrl" }, { "Dribbling", "drib" },
            { "Tight Possession", "tight_pos" }, { "Low Pass", "lowpass" }, { "Lofted Pass", "loftpass" },
            { "Finishing", "finish" }, { "Heading", "header" }, { "Place Kicking", "place_kick" },
            { "Curl", "swerve" }, { "Speed", "speed" }, { "Acceleration", "exp_pwr" },
            { "Kicking Power", "kick_pwr" }, { "Jump", "jump" }, { "Physical Contact", "phys_cont" },
            { "Balance", "body_ctrl" }, { "Stamina", "stamina" }, { "Defensive Awareness", "def" },
            { "Ball Winning", "ball_win" }, { "Aggression", "aggres" }, { "GK Awareness", "gk" },
            { "GK Catching", "catching" }, { "GK Clearing", "clearing" }, { "GK Reflexes", "reflex" },
            { "GK Reach", "cover" }
        };
        // Nombre de skill en el CSV -> bit de playeredit.py (se cruza por bit con el editor)
        public static readonly Dictionary<string, int> SkillBits = new Dictionary<string, int>
        {
            { "Scissors Feint", 519 }, { "Double Touch", 523 }, { "Flip Flap", 485 }, { "Marseille Turn", 497 },
            { "Sombrero", 482 }, { "Cut Behind & Turn", 528 }, { "Scotch Move", 525 },
            { "Step On Skill control", 500 }, { "Heading (skill)", 495 }, { "Long Range Drive", 527 },
            { "Chip shot control", 506 }, { "Long Range Shooting", 518 }, { "Knuckle Shot", 513 },
            { "Dipping Shot", 494 }, { "Rising Shots", 499 }, { "Acrobatic Finishing", 524 },
            { "Heel Trick", 505 }, { "First-time Shot", 511 }, { "One-touch Pass", 507 },
            { "Through Passing", 487 }, { "Weighted Pass", 484 }, { "Pinpoint Crossing", 483 },
            { "Outside Curler", 493 }, { "Rabona", 515 }, { "No Look Pass", 512 },
            { "Low Lofted Pass", 488 }, { "GK Low Punt", 490 }, { "GK High Punt", 496 },
            { "Long Throw", 521 }, { "GK Long Throw", 522 }, { "Penalty Specialist", 501 },
            { "GK Penalty Saver", 502 }, { "Gamesmanship", 491 }, { "Man Marking", 504 },
            { "Track Back", 517 }, { "Interception", 503 }, { "Acrobatic Clear", 530 },
            { "Captaincy", 492 }, { "Super-Sub", 516 }, { "Fighting Spirit", 486 },
            { "Trickster", 489 }, { "Mazing Run", 531 }, { "Speeding Bullet", 526 },
            { "Incisive Run", 510 }, { "Long Ball Expert", 529 }, { "Early Cross", 447 }, { "Long Ranger", 520 }
        };
    }

    public class NlClub
    {
        public uint NewlifeId;
        public bool InGame;
        public uint GameId;
        public string Name = "";
        public ushort Country;
        public string League = "";
        public List<NlPlayer> Players = new List<NlPlayer>();
    }

    public class NlRelease
    {
        public string Name = "";
        public string Version = "";
        public List<NlClub> Clubs = new List<NlClub>();
        public List<string> Warnings = new List<string>();
    }

    public class NlClubPreview
    {
        public NlClub Club;
        public string Estado = ""; // nuevo | existente | ya-importado
        public List<NlPlayer> Entran = new List<NlPlayer>();       // en orden de plantel
        public int Nuevos;          // jugadores a crear dentro de Entran
        public int Reutilizados;    // existentes libres dentro de Entran
        public List<string> QuedanEnSuClub = new List<string>();
        public int FueraDelPlantel;
        public List<string> Avisos = new List<string>();
    }

    public class NlPreview
    {
        public NlRelease Release;
        public List<NlClubPreview> Clubs = new List<NlClubPreview>();
    }

    public class NlApplyResult
    {
        public string Club = "";
        public bool Creado;
        public uint TeamId;
        public int JugadoresCreados;
        public int JugadoresFichados;
        public string Mensaje = "";
    }

    public static class NewLifeImporter
    {
        public const int SquadCap = 30;
        public const int MaxGoalkeepers = 3;
        public const uint MaxPlayerId = 262144;

        // ---------------- Lectura de la release ----------------

        public static NlRelease Load(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                throw new InvalidOperationException("La carpeta de la release no existe.");
            string dir = FindReleaseDir(folder);
            var sources = new List<string[]>(); // [json, clubs, players] por fuente

            string jsonPath = Path.Combine(dir, "newlife.json");
            if (File.Exists(jsonPath))
            {
                sources.Add(new[]
                {
                    File.ReadAllText(jsonPath),
                    ReadText(Path.Combine(dir, "clubs.csv")),
                    ReadText(Path.Combine(dir, "players.csv"))
                });
            }
            foreach (string zip in Directory.GetFiles(dir, "NewLife-*.zip").OrderBy(z => z))
            {
                using (var za = ZipFile.OpenRead(zip))
                {
                    Func<string, string> readEntry = name =>
                    {
                        var e = za.Entries.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (e == null) return null;
                        using (var sr = new StreamReader(e.Open(), Encoding.UTF8)) return sr.ReadToEnd();
                    };
                    string j = readEntry("newlife.json");
                    if (j == null) continue;
                    sources.Add(new[] { j, readEntry("clubs.csv") ?? "", readEntry("players.csv") ?? "" });
                }
            }
            if (sources.Count == 0)
                throw new InvalidOperationException("No se encontró una release NewLife en esa carpeta (falta newlife.json).");

            var release = new NlRelease();
            var clubsById = new Dictionary<uint, NlClub>();
            string version = null;
            foreach (var src in sources)
            {
                var meta = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(src[0]);
                string v = meta.ContainsKey("version") && meta["version"] != null ? Convert.ToString(meta["version"]) : "";
                if (version == null)
                {
                    version = v;
                    release.Version = v;
                    release.Name = meta.ContainsKey("name") && meta["name"] != null ? Convert.ToString(meta["name"]) : "NewLife Database";
                }
                else if (v != version)
                    throw new InvalidOperationException("La carpeta mezcla versiones de NewLife (" + version + " y " + v + "): la release se niega, como hace Mod Studio.");

                foreach (var row in ParseCsv(src[1]))
                {
                    uint nlId = ParseUint(GetCol(row, "newlife_id"));
                    if (nlId == 0 || clubsById.ContainsKey(nlId)) continue;
                    var club = new NlClub
                    {
                        NewlifeId = nlId,
                        InGame = GetCol(row, "in_game").Trim() == "1",
                        GameId = ParseUint(GetCol(row, "game_id")),
                        Name = GetCol(row, "name"),
                        League = GetCol(row, "league")
                    };
                    ushort ctry;
                    if (ushort.TryParse(GetCol(row, "country"), out ctry)) club.Country = ctry;
                    clubsById[nlId] = club;
                    release.Clubs.Add(club);
                }
                foreach (var row in ParseCsv(src[2]))
                {
                    var p = new NlPlayer
                    {
                        NewlifeId = ParseUint(GetCol(row, "newlife_id")),
                        GameId = ParseUint(GetCol(row, "game_id")),
                        Name = GetCol(row, "name"),
                        ClubNewlifeId = ParseUint(GetCol(row, "club_id"))
                    };
                    foreach (var kv in row) p.Fields[kv.Key] = kv.Value;
                    NlClub club;
                    if (clubsById.TryGetValue(p.ClubNewlifeId, out club)) club.Players.Add(p);
                }
            }
            int sinClub = 0;
            foreach (var c in release.Clubs) if (c.Players.Count == 0) sinClub++;
            if (sinClub > 0) release.Warnings.Add(sinClub + " clubes de la release no tienen jugadores en players.csv.");
            return release;
        }

        private static string FindReleaseDir(string folder)
        {
            if (File.Exists(Path.Combine(folder, "newlife.json"))) return folder;
            foreach (string sub in Directory.GetDirectories(folder))
                if (File.Exists(Path.Combine(sub, "newlife.json"))) return sub;
            return folder;
        }

        private static string ReadText(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        private static uint ParseUint(string s)
        {
            uint v; return uint.TryParse((s ?? "").Trim(), out v) ? v : 0;
        }

        private static string GetCol(Dictionary<string, string> row, string key)
        {
            string v; return row != null && row.TryGetValue(key, out v) ? v : "";
        }

        // CSV con comillas (RFC 4180 simplificado)
        public static List<Dictionary<string, string>> ParseCsv(string text)
        {
            var rows = new List<Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(text)) return rows;
            var records = new List<List<string>>();
            var field = new StringBuilder();
            var record = new List<string>();
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { record.Add(field.ToString()); field.Length = 0; }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    record.Add(field.ToString()); field.Length = 0;
                    records.Add(record); record = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
            if (records.Count == 0) return rows;
            var header = records[0];
            for (int r = 1; r < records.Count; r++)
            {
                if (records[r].Count == 1 && records[r][0].Trim().Length == 0) continue;
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < header.Count; c++)
                    d[header[c].Trim()] = c < records[r].Count ? records[r][c] : "";
                rows.Add(d);
            }
            return rows;
        }

        // ---------------- Vista previa (solo lectura) ----------------

        public static NlPreview Preview(DatabaseManager db, NlRelease release)
        {
            var pv = new NlPreview { Release = release };
            foreach (var club in release.Clubs)
            {
                var cp = new NlClubPreview { Club = club };
                uint existingId = 0;
                if (club.InGame) existingId = club.NewlifeId;
                else if (club.GameId > 0 && db.TeamsById.ContainsKey(club.GameId)) existingId = club.GameId;

                if (existingId > 0 && db.TeamsById.ContainsKey(existingId))
                {
                    cp.Estado = "existente";
                    pv.Clubs.Add(cp);
                    continue;
                }
                bool yaImportado = db.Teams.Any(t => t.IsNew && t.CountryId == club.Country &&
                    PresetEngine.NormalizeName(t.NameSpanish) == PresetEngine.NormalizeName(club.Name));
                if (yaImportado)
                {
                    cp.Estado = "ya-importado";
                    pv.Clubs.Add(cp);
                    continue;
                }
                cp.Estado = "nuevo";

                // Clasificar jugadores del club
                var candidatos = new List<KeyValuePair<NlPlayer, double>>();
                foreach (var p in club.Players)
                {
                    PlayerRecord existente = null;
                    if (p.GameId > 0) db.PlayersById.TryGetValue(p.GameId, out existente);
                    if (existente != null && PresetEngine.NormalizeName(existente.Name) != PresetEngine.NormalizeName(p.Name))
                        existente = null; // mismo id pero otra persona: se trata como nuevo
                    if (existente != null)
                    {
                        if (existente.ClubId == 0) candidatos.Add(new KeyValuePair<NlPlayer, double>(p, existente.Overall));
                        else cp.QuedanEnSuClub.Add(p.Name + " (juega en otro club de tu base)");
                    }
                    else candidatos.Add(new KeyValuePair<NlPlayer, double>(p, p.RankScore));
                }
                candidatos = candidatos.OrderByDescending(kv => kv.Value).ToList();

                int gk = 0;
                var tomados = new HashSet<NlPlayer>();
                foreach (var kv in candidatos)
                {
                    if (cp.Entran.Count >= SquadCap) break;
                    bool esGk = kv.Key.RegPosIndex == 0;
                    if (esGk && gk >= MaxGoalkeepers) continue;
                    if (esGk) gk++;
                    cp.Entran.Add(kv.Key);
                    tomados.Add(kv.Key);
                }
                cp.FueraDelPlantel = candidatos.Count - tomados.Count;
                foreach (var p in cp.Entran)
                {
                    PlayerRecord ex = null;
                    if (p.GameId > 0) db.PlayersById.TryGetValue(p.GameId, out ex);
                    if (ex != null && ex.ClubId == 0 && PresetEngine.NormalizeName(ex.Name) == PresetEngine.NormalizeName(p.Name)) cp.Reutilizados++;
                    else cp.Nuevos++;
                }
                if (cp.Entran.Count < 11)
                    cp.Avisos.Add("Plantel corto (" + cp.Entran.Count + "): tras importar, completalo desde la ficha del equipo (Fase 5A).");
                if (cp.QuedanEnSuClub.Count > 0)
                    cp.Avisos.Add(cp.QuedanEnSuClub.Count + " jugadores quedan en su club actual: no se mueven.");
                pv.Clubs.Add(cp);
            }
            return pv;
        }

        // ---------------- Aplicacion ----------------

        public static List<NlApplyResult> Apply(DatabaseManager db, NlPreview pv)
        {
            var results = new List<NlApplyResult>();
            var usedTeamIds = new HashSet<uint>(db.Teams.Select(t => t.Id));
            uint nextTeamId = 6001;

            foreach (var cp in pv.Clubs)
            {
                var res = new NlApplyResult { Club = cp.Club.Name };
                if (cp.Estado != "nuevo")
                {
                    res.Creado = false;
                    res.Mensaje = cp.Estado == "existente" ? "Ya está en el juego: no se toca." : "Ya fue importado antes: no se duplica.";
                    results.Add(res);
                    continue;
                }
                var club = cp.Club;
                while (nextTeamId <= 65530 && usedTeamIds.Contains(nextTeamId)) nextTeamId++;
                if (nextTeamId > 65530) { res.Mensaje = "Sin IDs de equipo libres."; results.Add(res); continue; }
                uint teamId = nextTeamId++;
                usedTeamIds.Add(teamId);

                var template = db.Teams.FirstOrDefault(t => t.CountryId == club.Country) ?? db.Teams.FirstOrDefault();
                if (template == null) { res.Mensaje = "No hay equipo plantilla para clonar."; results.Add(res); continue; }
                string shortName = ShortNameOf(club.Name);
                var team = template.CloneWithNewId(teamId, club.Name, shortName);
                team.CountryId = club.Country;
                // DT propio (Punto E): el club no hereda el tecnico de la plantilla.
                team.CoachId = db.CreateNewCoach("DT " + club.Name, club.Country).Id;
                db.Teams.Add(team);
                db.TeamsById[teamId] = team;
                res.TeamId = teamId;

                foreach (var p in cp.Entran)
                {
                    PlayerRecord existente = null;
                    if (p.GameId > 0) db.PlayersById.TryGetValue(p.GameId, out existente);
                    if (existente != null && existente.ClubId == 0 && PresetEngine.NormalizeName(existente.Name) == PresetEngine.NormalizeName(p.Name))
                    {
                        if (db.SignFreeAgent(existente.Id, teamId)) res.JugadoresFichados++;
                    }
                    else
                    {
                        var creado = CreateImportedPlayer(db, p);
                        if (creado != null)
                        {
                            res.JugadoresCreados++;
                            if (db.SignFreeAgent(creado.Id, teamId)) res.JugadoresFichados++;
                        }
                    }
                }
                db.ApplyTeamFormation(teamId, db.GetTeamFormation(teamId));
                db.AutoAssignRoles(teamId);
                res.Creado = true;
                res.Mensaje = "Creado con ID " + teamId + ": " + res.JugadoresFichados + " jugadores en el plantel (" + res.JugadoresCreados + " nuevos).";
                results.Add(res);
            }
            db.HasUnsavedChanges = true;
            return results;
        }

        private static string ShortNameOf(string name)
        {
            var letters = new string((name ?? "").Where(char.IsLetter).ToArray());
            string s = letters.Length >= 3 ? letters.Substring(0, 3) : (letters + "FC").Substring(0, 3);
            return s.ToUpperInvariant();
        }

        // Crea el registro de un jugador nuevo con los campos de la release.
        public static PlayerRecord CreateImportedPlayer(DatabaseManager db, NlPlayer p)
        {
            uint newId = db.SuggestNextFreePlayerId();
            if (newId >= MaxPlayerId)
            {
                uint scan = 1;
                while (scan < MaxPlayerId && db.PlayersById.ContainsKey(scan)) scan++;
                if (scan >= MaxPlayerId) return null;
                newId = scan;
            }
            var raw = new byte[PlayerRecord.RecordSize];
            PlayerBitHelper.SetForm(raw, 5);
            PlayerBitHelper.SetInjuryResistance(raw, 1);
            PlayerBitHelper.SetWeakFootUsage(raw, 2);
            PlayerBitHelper.SetWeakFootAccuracy(raw, 2);

            int iv;
            ushort uv;
            if (ushort.TryParse(p.Get("Nationality"), out uv)) PlayerBitHelper.SetNationalityId(raw, uv);
            int age = 24;
            if (int.TryParse(p.Get("Age"), out iv)) { age = Math.Max(15, Math.Min(60, iv)); }
            PlayerBitHelper.SetAge(raw, age);
            if (int.TryParse(p.Get("Height (cm)"), out iv)) PlayerBitHelper.SetHeight(raw, Math.Max(100, Math.Min(227, iv)));
            else PlayerBitHelper.SetHeight(raw, 180);
            if (int.TryParse(p.Get("Weight (kg)"), out iv)) PlayerBitHelper.SetWeight(raw, Math.Max(30, Math.Min(157, iv)));
            else PlayerBitHelper.SetWeight(raw, 75);
            PlayerBitHelper.SetFoot(raw, string.Equals(p.Get("Stronger Foot").Trim(), "Left", StringComparison.OrdinalIgnoreCase));
            if (int.TryParse(p.Get("Weak Foot Usage"), out iv)) PlayerBitHelper.SetWeakFootUsage(raw, Math.Max(0, Math.Min(3, iv)));
            if (int.TryParse(p.Get("Weak Foot Accuracy"), out iv)) PlayerBitHelper.SetWeakFootAccuracy(raw, Math.Max(0, Math.Min(3, iv)));
            if (int.TryParse(p.Get("Form"), out iv)) PlayerBitHelper.SetForm(raw, Math.Max(0, Math.Min(7, iv)));
            if (int.TryParse(p.Get("Injury Resistance"), out iv)) PlayerBitHelper.SetInjuryResistance(raw, Math.Max(0, Math.Min(2, iv)));
            if (int.TryParse(p.Get("Playing Style"), out iv)) PlayerBitHelper.SetPlayingStyle(raw, Math.Max(0, Math.Min(31, iv)));

            int regPos = p.RegPosIndex;
            PlayerBitHelper.SetPrimaryPosition(raw, regPos);
            for (int i = 0; i < PlayerRecord.PositionNames.Length; i++)
            {
                int compat;
                if (int.TryParse(p.Get(PlayerRecord.PositionNames[i]), out compat))
                    PlayerBitHelper.SetPositionCompatibility(raw, i, Math.Max(0, Math.Min(2, compat)));
            }
            PlayerBitHelper.SetPositionCompatibility(raw, regPos, 2);

            foreach (var kv in NlPlayer.AbilityMap)
            {
                int val;
                if (int.TryParse(p.Get(kv.Key), out val))
                    PlayerBitHelper.SetAbility(raw, kv.Value, Math.Max(40, Math.Min(99, val)));
            }
            // Skills y estilos COM: solo si el bit coincide con el del editor.
            var bitToSkill = new Dictionary<int, int>();
            foreach (var kv in PlayerBitHelper.DbSkillBits) bitToSkill[kv.Value] = kv.Key;
            foreach (var kv in NlPlayer.SkillBits)
            {
                if (p.Get(kv.Key).Trim() != "1") continue;
                int skillId;
                if (bitToSkill.TryGetValue(kv.Value, out skillId)) { PlayerBitHelper.SetSkill(raw, skillId, true); continue; }
                int comIdx = Array.IndexOf(PlayerBitHelper.DbComBits, kv.Value);
                if (comIdx >= 0) PlayerBitHelper.SetComStyle(raw, comIdx, true);
            }

            var rec = PlayerRecord.FromBytes(raw, 0);
            rec.Id = newId;
            rec.Name = p.Name ?? "";
            rec.ShirtName = (p.Name ?? "").ToUpperInvariant();
            rec.ClubId = 0;
            rec.PositionIndex = regPos;
            rec.PositionName = PlayerRecord.PositionNames[regPos];
            rec.Age = age;
            rec.Overall = rec.ComputeOverall();
            rec.RawBytes = rec.ToBytes();
            db.Players.Add(rec);
            db.PlayersById[newId] = rec;
            return rec;
        }
    }
}
