// ============================================================================
//  CsvImporter.cs  -  Importador CSV simple de jugadores
// ----------------------------------------------------------------------------
//  Un solo archivo CSV, una fila por jugador, que se puede armar en
//  cualquier planilla (o exportar de Football Manager / una web):
//
//    Equipo, Jugador, Posicion           (obligatorias)
//    Pais                                pais del club (nombre o id)
//    Media                               media general: si no hay
//                                        habilidades sueltas, siembra
//                                        todas con este valor
//    IdJuego                             si el jugador ya existe en el
//                                        juego, su id: se reutiliza
//    Edad, Altura, Peso, Pie, Nacionalidad
//    + las 25 habilidades por su nombre (Offensive Awareness, etc.)
//
//  Reutiliza el motor de la Fase 5B (NewLifeImporter.CreateImportedPlayer
//  y el parser CSV): los clubes se crean con su plantel real, los
//  jugadores que ya existen y estan libres se fichan sin duplicar, y
//  los que juegan en otro club NO se mueven: se reportan. Un equipo
//  que ya existe y tiene plantel (11+) no se toca; si existe con el
//  plantel corto, se le carga el del CSV.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EditorLigasPes
{
    public class CsvClubPreview
    {
        public NlClub Club;
        public string Estado = ""; // nuevo | existente-libre | ya-tiene-plantel
        public uint ExistingTeamId;
        public List<NlPlayer> Entran = new List<NlPlayer>();
        public int Nuevos;
        public int Reutilizados;
        public List<string> QuedanEnSuClub = new List<string>();
        public int FueraDelPlantel;
        public List<string> Avisos = new List<string>();
    }

    public class CsvPreview
    {
        public List<NlClub> Clubs = new List<NlClub>();
        public List<CsvClubPreview> Items = new List<CsvClubPreview>();
        public List<string> Warnings = new List<string>();
    }

    public static class CsvImporter
    {
        private static string Col(Dictionary<string, string> row, params string[] names)
        {
            foreach (var n in names)
            {
                foreach (var kv in row)
                    if (PresetEngine.NormalizeName(kv.Key) == PresetEngine.NormalizeName(n))
                        return kv.Value != null ? kv.Value.Trim() : "";
            }
            return "";
        }

        private static ushort ResolveCountry(DatabaseManager db, string text, ushort fallback)
        {
            ushort id;
            if (ushort.TryParse((text ?? "").Trim(), out id) && id > 0) return id;
            if (!string.IsNullOrWhiteSpace(text))
            {
                string want = PresetEngine.NormalizeName(text);
                var c = db.Countries.FirstOrDefault(x =>
                    PresetEngine.NormalizeName(x.NameSpanish) == want ||
                    PresetEngine.NormalizeName(x.NameEnglish) == want);
                if (c != null) return c.Id;
            }
            return fallback;
        }

        public static List<NlClub> Load(DatabaseManager db, string path, ushort defaultCountryId, List<string> warnings)
        {
            return Load(db, path, defaultCountryId, warnings, "simple");
        }

        // Formato: "simple" (escala del juego) | "fm" (Football Manager
        // 1-20, se convierte) | "eafc" (EA FC 0-99, nombres SoFIFA).
        // Los modos fm/eafc normalizan cada fila a la forma simple y el
        // resto del pipeline es el mismo.
        public static List<NlClub> Load(DatabaseManager db, string path, ushort defaultCountryId, List<string> warnings, string format)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("No se encontró el archivo CSV.");
            string text = File.ReadAllText(path, Encoding.UTF8);
            var rawRows = NewLifeImporter.ParseCsv(text);
            if (rawRows.Count == 0) throw new InvalidOperationException("El CSV está vacío o no tiene filas de jugadores.");
            var rows = new List<Dictionary<string, string>>();
            foreach (var r in rawRows)
            {
                if (format == "fm") rows.Add(NormalizeFmRow(r));
                else if (format == "eafc") rows.Add(NormalizeEaRow(r));
                else rows.Add(r);
            }

            var clubs = new List<NlClub>();
            var byName = new Dictionary<string, NlClub>();
            uint seq = 1;
            int sinEquipo = 0, sinPosicion = 0;
            foreach (var row in rows)
            {
                string equipo = Col(row, "Equipo", "Team", "Club");
                string jugador = Col(row, "Jugador", "Player", "Nombre", "Name");
                string posicion = Col(row, "Posicion", "Position", "Registered Position");
                if (string.IsNullOrWhiteSpace(equipo) || string.IsNullOrWhiteSpace(jugador)) { sinEquipo++; continue; }
                if (string.IsNullOrWhiteSpace(posicion)) { sinPosicion++; continue; }

                string key = PresetEngine.NormalizeName(equipo);
                NlClub club;
                if (!byName.TryGetValue(key, out club))
                {
                    club = new NlClub
                    {
                        NewlifeId = 900000 + seq++,
                        Name = equipo,
                        Country = ResolveCountry(db, Col(row, "Pais", "Country"), defaultCountryId)
                    };
                    byName[key] = club;
                    clubs.Add(club);
                }

                var p = new NlPlayer
                {
                    Name = jugador,
                    ClubNewlifeId = club.NewlifeId,
                    GameId = 0
                };
                uint gid;
                if (uint.TryParse(Col(row, "IdJuego", "GameId", "Game Id"), out gid)) p.GameId = gid;
                p.NewlifeId = 500000 + (uint)club.Players.Count + club.NewlifeId;

                // Campos con los nombres que entiende el motor (playeredit)
                p.Fields["Registered Position"] = posicion.ToUpperInvariant();
                p.Fields[posicion.ToUpperInvariant()] = "2";
                string media = Col(row, "Media", "Overall", "OVR", "Rating");
                foreach (var ab in NlPlayer.AbilityMap.Keys)
                {
                    string v = Col(row, ab);
                    if (!string.IsNullOrWhiteSpace(v)) p.Fields[ab] = v;
                    else if (!string.IsNullOrWhiteSpace(media)) p.Fields[ab] = media;
                }
                SetIf(p, "Age", Col(row, "Edad", "Age"));
                SetIf(p, "Height (cm)", Col(row, "Altura", "Height", "Height (cm)"));
                SetIf(p, "Weight (kg)", Col(row, "Peso", "Weight", "Weight (kg)"));
                string pie = Col(row, "Pie", "Foot", "Stronger Foot");
                if (!string.IsNullOrWhiteSpace(pie))
                    p.Fields["Stronger Foot"] = (pie.StartsWith("I", StringComparison.OrdinalIgnoreCase) ||
                        pie.StartsWith("L", StringComparison.OrdinalIgnoreCase)) ? "Left" : "Right";
                string nac = Col(row, "Nacionalidad", "Nationality");
                if (!string.IsNullOrWhiteSpace(nac))
                {
                    ushort nid = ResolveCountry(db, nac, 0);
                    if (nid > 0) p.Fields["Nationality"] = nid.ToString();
                }
                club.Players.Add(p);
            }
            if (sinEquipo > 0) warnings.Add(sinEquipo + " filas salteadas: les falta Equipo o Jugador.");
            if (sinPosicion > 0) warnings.Add(sinPosicion + " filas salteadas: les falta Posición.");
            if (clubs.Count == 0) throw new InvalidOperationException("El CSV no trajo ningún club válido (revisá las columnas Equipo, Jugador y Posicion).");
            return clubs;
        }

        private static void SetIf(NlPlayer p, string field, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) p.Fields[field] = value;
        }

        // ---------------- Formatos FM / EA FC (Punto C) ----------------

        // Football Manager 1-20 -> escala del juego 40-99 (lineal, como FL26).
        public static int ConvertFmValue(int v)
        {
            if (v < 1) v = 1;
            if (v > 20) v = 20;
            int r = 40 + (int)Math.Round((v - 1) * 59.0 / 19.0);
            return Math.Max(40, Math.Min(99, r));
        }

        // Columna FM -> habilidad del juego (nombre playeredit).
        private static readonly Dictionary<string, string> FmAbilityMap = new Dictionary<string, string>
        {
            { "Finishing", "Finishing" }, { "Heading", "Heading" }, { "Passing", "Low Pass" },
            { "Crossing", "Lofted Pass" }, { "Dribbling", "Dribbling" }, { "Technique", "Ball Control" },
            { "First Touch", "Tight Possession" }, { "Pace", "Speed" }, { "Acceleration", "Acceleration" },
            { "Stamina", "Stamina" }, { "Strength", "Physical Contact" }, { "Agility", "Balance" },
            { "Jumping Reach", "Jump" }, { "Marking", "Defensive Awareness" }, { "Tackling", "Ball Winning" },
            { "Long Shots", "Kicking Power" }, { "Free Kick Taking", "Place Kicking" },
            { "Penalty Taking", "Place Kicking" }, { "Reflexes", "GK Reflexes" }, { "Handling", "GK Catching" },
            { "One On Ones", "GK Awareness" }, { "Kicking", "GK Clearing" }, { "Aerial Ability", "GK Reach" }
        };

        // Columna EA FC / SoFIFA -> habilidad del juego.
        private static readonly Dictionary<string, string> EaAbilityMap = new Dictionary<string, string>
        {
            { "Sprint Speed", "Speed" }, { "SprintSpeed", "Speed" }, { "Acceleration", "Acceleration" },
            { "Finishing", "Finishing" }, { "Heading Accuracy", "Heading" }, { "HeadingAccuracy", "Heading" },
            { "Short Passing", "Low Pass" }, { "ShortPassing", "Low Pass" },
            { "Long Passing", "Lofted Pass" }, { "LongPassing", "Lofted Pass" },
            { "Ball Control", "Ball Control" }, { "BallControl", "Ball Control" },
            { "Dribbling", "Dribbling" }, { "Curve", "Curl" },
            { "FK Accuracy", "Place Kicking" }, { "FKAccuracy", "Place Kicking" },
            { "Shot Power", "Kicking Power" }, { "ShotPower", "Kicking Power" },
            { "Jumping", "Jump" }, { "Strength", "Physical Contact" }, { "Balance", "Balance" },
            { "Stamina", "Stamina" }, { "Defensive Awareness", "Defensive Awareness" },
            { "DefensiveAwareness", "Defensive Awareness" }, { "Marking", "Defensive Awareness" },
            { "Standing Tackle", "Ball Winning" }, { "StandingTackle", "Ball Winning" },
            { "Aggression", "Aggression" },
            { "GK Reflexes", "GK Reflexes" }, { "GKReflexes", "GK Reflexes" },
            { "GK Handling", "GK Catching" }, { "GKHandling", "GK Catching" },
            { "GK Kicking", "GK Clearing" }, { "GKKicking", "GK Clearing" },
            { "GK Positioning", "GK Awareness" }, { "GKPositioning", "GK Awareness" },
            { "GK Diving", "GK Reach" }, { "GKDiving", "GK Reach" }
        };

        private static Dictionary<string, string> BaseRow(Dictionary<string, string> row, string posicion)
        {
            var o = new Dictionary<string, string>();
            o["Equipo"] = Col(row, "Equipo", "Team", "Club");
            o["Jugador"] = Col(row, "Jugador", "Player", "Nombre", "Name");
            o["Posicion"] = posicion;
            o["Pais"] = Col(row, "Pais", "Country");
            o["IdJuego"] = Col(row, "IdJuego", "GameId", "Game Id");
            o["Edad"] = Col(row, "Edad", "Age");
            return o;
        }

        private static Dictionary<string, string> NormalizeFmRow(Dictionary<string, string> row)
        {
            var o = BaseRow(row, FmPosition(Col(row, "Posicion", "Position", "Registered Position")));
            var converted = new List<int>();
            foreach (var kv in FmAbilityMap)
            {
                string raw = Col(row, kv.Key);
                int v;
                if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw.Trim(), out v))
                {
                    int c = ConvertFmValue(v);
                    o[kv.Value] = c.ToString();
                    converted.Add(c);
                }
            }
            if (converted.Count > 0)
                o["Media"] = ((int)Math.Round(converted.Average())).ToString();
            o["Altura"] = Col(row, "Altura", "Height");
            o["Peso"] = Col(row, "Peso", "Weight");
            o["Pie"] = Col(row, "Pie", "Preferred Foot", "Foot");
            o["Nacionalidad"] = Col(row, "Nacionalidad", "Nationality", "Nation");
            return o;
        }

        private static Dictionary<string, string> NormalizeEaRow(Dictionary<string, string> row)
        {
            string posRaw = Col(row, "Posicion", "Positions", "Position", "Registered Position");
            var o = BaseRow(row, EaPosition(posRaw));
            foreach (var kv in EaAbilityMap)
            {
                string raw = Col(row, kv.Key);
                int v;
                if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw.Trim(), out v))
                    o[kv.Value] = Math.Max(1, Math.Min(99, v)).ToString();
            }
            string overall = Col(row, "Media", "Overall", "OVR", "Rating");
            if (!string.IsNullOrWhiteSpace(overall)) o["Media"] = overall;
            o["Altura"] = EaHeightCm(Col(row, "Altura", "Height"));
            o["Peso"] = EaWeightKg(Col(row, "Peso", "Weight"));
            o["Pie"] = Col(row, "Pie", "Preferred Foot", "Foot");
            o["Nacionalidad"] = Col(row, "Nacionalidad", "Nationality", "Nation");
            return o;
        }

        // SoFIFA viejo trae altura como 5'11" y peso en libras.
        private static string EaHeightCm(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            raw = raw.Trim();
            int ap = raw.IndexOf('\'');
            if (ap > 0)
            {
                int feet, inches;
                string rest = raw.Substring(ap + 1).Replace("\"", "");
                if (int.TryParse(raw.Substring(0, ap), out feet) && int.TryParse(rest, out inches))
                    return ((int)Math.Round((feet * 12 + inches) * 2.54)).ToString();
                return "";
            }
            int v; return int.TryParse(raw, out v) ? v.ToString() : "";
        }

        private static string EaWeightKg(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            raw = raw.Trim();
            bool lbs = raw.IndexOf("lbs", StringComparison.OrdinalIgnoreCase) >= 0;
            var digits = new string(raw.Where(c => char.IsDigit(c)).ToArray());
            int v;
            if (!int.TryParse(digits, out v)) return "";
            return lbs ? ((int)Math.Round(v * 0.4536)).ToString() : v.ToString();
        }

        // "ST (C)", "D (RL)", "AM (L)", "WB (R)", "DM" -> codigo del juego.
        public static string FmPosition(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim().ToUpperInvariant();
            foreach (var pn in PlayerRecord.PositionNames)
                if (s == pn) return pn;
            string side = "";
            int par = s.IndexOf('(');
            string baseCode = (par >= 0 ? s.Substring(0, par) : s).Trim();
            if (par >= 0)
            {
                int close = s.IndexOf(')', par);
                string inside = close > par ? s.Substring(par + 1, close - par - 1) : "";
                if (inside.Contains("L") && !inside.Contains("R")) side = "L";
                else if (inside.Contains("R") && !inside.Contains("L")) side = "R";
            }
            switch (baseCode)
            {
                case "GK": return "GK";
                case "D": case "DC": return side == "L" ? "LB" : side == "R" ? "RB" : "CB";
                case "WB": return side == "L" ? "LMF" : side == "R" ? "RMF" : "LMF";
                case "DM": case "DMF": return "DMF";
                case "M": case "MC": return side == "L" ? "LMF" : side == "R" ? "RMF" : "CMF";
                case "AM": case "AMC": return side == "L" ? "LWF" : side == "R" ? "RWF" : "AMF";
                case "ST": case "S": return "CF";
                default: return s;
            }
        }

        // "ST, CAM" / "CDM" / "CF" -> codigo del juego.
        public static string EaPosition(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string first = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToUpperInvariant();
            // Primero el dialecto EA (su CF es el SS del juego, su ST es el CF);
            // lo que no sea dialecto pasa tal cual si es un codigo valido.
            switch (first)
            {
                case "ST": return "CF";
                case "CF": return "SS";
                case "CAM": return "AMF";
                case "CDM": return "DMF";
                case "CM": return "CMF";
                case "LM": return "LMF";
                case "RM": return "RMF";
                case "LW": return "LWF";
                case "RW": return "RWF";
                case "LWB": return "LB";
                case "RWB": return "RB";
                default: return first;
            }
        }

        public static CsvPreview Preview(DatabaseManager db, List<NlClub> clubs, List<string> warnings)
        {
            var pv = new CsvPreview { Clubs = clubs };
            if (warnings != null) pv.Warnings.AddRange(warnings);
            foreach (var club in clubs)
            {
                var cp = new CsvClubPreview { Club = club };
                var existente = db.Teams.FirstOrDefault(t =>
                    PresetEngine.NormalizeName(t.NameSpanish) == PresetEngine.NormalizeName(club.Name) ||
                    PresetEngine.NormalizeName(t.NameEnglish) == PresetEngine.NormalizeName(club.Name));
                int plantelActual = 0;
                if (existente != null)
                {
                    cp.ExistingTeamId = existente.Id;
                    plantelActual = db.GetTeamRoster(existente.Id).Count;
                    if (plantelActual >= 11)
                    {
                        cp.Estado = "ya-tiene-plantel";
                        pv.Items.Add(cp);
                        continue;
                    }
                    cp.Estado = "existente-libre";
                }
                else cp.Estado = "nuevo";

                var candidatos = new List<KeyValuePair<NlPlayer, double>>();
                foreach (var p in club.Players)
                {
                    PlayerRecord ex = null;
                    if (p.GameId > 0) db.PlayersById.TryGetValue(p.GameId, out ex);
                    if (ex != null && PresetEngine.NormalizeName(ex.Name) != PresetEngine.NormalizeName(p.Name)) ex = null;
                    if (ex != null)
                    {
                        if (ex.ClubId == 0) candidatos.Add(new KeyValuePair<NlPlayer, double>(p, ex.Overall));
                        else if (existente != null && ex.ClubId == existente.Id) { /* ya esta en este equipo */ }
                        else cp.QuedanEnSuClub.Add(p.Name + " (juega en otro club de tu base)");
                    }
                    else candidatos.Add(new KeyValuePair<NlPlayer, double>(p, p.RankScore));
                }
                candidatos = candidatos.OrderByDescending(kv => kv.Value).ToList();

                int cupo = NewLifeImporter.SquadCap - plantelActual;
                int gkActual = 0;
                if (existente != null)
                    gkActual = db.GetTeamRoster(existente.Id).Count(a =>
                    {
                        PlayerRecord pr; return db.PlayersById.TryGetValue(a.PlayerId, out pr) && pr.PositionIndex == 0;
                    });
                int gk = gkActual;
                var tomados = new HashSet<NlPlayer>();
                foreach (var kv in candidatos)
                {
                    if (cp.Entran.Count >= cupo) break;
                    bool esGk = kv.Key.RegPosIndex == 0;
                    if (esGk && gk >= NewLifeImporter.MaxGoalkeepers) continue;
                    if (esGk) gk++;
                    cp.Entran.Add(kv.Key);
                    tomados.Add(kv.Key);
                }
                cp.FueraDelPlantel = candidatos.Count - tomados.Count;
                foreach (var p in cp.Entran)
                {
                    PlayerRecord ex = null;
                    if (p.GameId > 0) db.PlayersById.TryGetValue(p.GameId, out ex);
                    if (ex != null && ex.ClubId == 0) cp.Reutilizados++; else cp.Nuevos++;
                }
                if (plantelActual + cp.Entran.Count < 11)
                    cp.Avisos.Add("Plantel corto (" + (plantelActual + cp.Entran.Count) + "): tras importar, completalo desde la ficha del equipo (Fase 5A).");
                if (cp.QuedanEnSuClub.Count > 0)
                    cp.Avisos.Add(cp.QuedanEnSuClub.Count + " jugadores quedan en su club actual: no se mueven.");
                pv.Items.Add(cp);
            }
            return pv;
        }

        public static List<NlApplyResult> Apply(DatabaseManager db, CsvPreview pv)
        {
            var results = new List<NlApplyResult>();
            var usedTeamIds = new HashSet<uint>(db.Teams.Select(t => t.Id));
            uint nextTeamId = 6001;

            foreach (var cp in pv.Items)
            {
                var res = new NlApplyResult { Club = cp.Club.Name };
                if (cp.Estado == "ya-tiene-plantel")
                {
                    res.Creado = false;
                    res.Mensaje = "Ya existe con plantel completo: no se toca.";
                    results.Add(res);
                    continue;
                }
                var club = cp.Club;
                uint teamId;
                if (cp.Estado == "existente-libre")
                {
                    teamId = cp.ExistingTeamId;
                }
                else
                {
                    while (nextTeamId <= 65530 && usedTeamIds.Contains(nextTeamId)) nextTeamId++;
                    if (nextTeamId > 65530) { res.Mensaje = "Sin IDs de equipo libres."; results.Add(res); continue; }
                    teamId = nextTeamId++;
                    usedTeamIds.Add(teamId);
                    var template = db.Teams.FirstOrDefault(t => t.CountryId == club.Country) ?? db.Teams.FirstOrDefault();
                    if (template == null) { res.Mensaje = "No hay equipo plantilla para clonar."; results.Add(res); continue; }
                    var team = template.CloneWithNewId(teamId, club.Name, ShortNameOf(club.Name));
                    team.CountryId = club.Country;
                    // DT propio (Punto E): el clon no hereda el tecnico de la
                    // plantilla; el club nace con el suyo, como en CreateNewTeam.
                    team.CoachId = db.CreateNewCoach("DT " + club.Name, club.Country).Id;
                    db.Teams.Add(team);
                    db.TeamsById[teamId] = team;
                }
                res.TeamId = teamId;

                foreach (var p in cp.Entran)
                {
                    PlayerRecord ex = null;
                    if (p.GameId > 0) db.PlayersById.TryGetValue(p.GameId, out ex);
                    if (ex != null && ex.ClubId == 0 && PresetEngine.NormalizeName(ex.Name) == PresetEngine.NormalizeName(p.Name))
                    {
                        if (db.SignFreeAgent(ex.Id, teamId)) res.JugadoresFichados++;
                    }
                    else
                    {
                        var creado = NewLifeImporter.CreateImportedPlayer(db, p);
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
                res.Mensaje = (cp.Estado == "nuevo" ? "Creado con ID " : "Plantel cargado en el equipo existente (ID ") + teamId + "): " +
                    res.JugadoresFichados + " jugadores (" + res.JugadoresCreados + " nuevos).";
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
    }
}
