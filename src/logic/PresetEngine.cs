// ============================================================================
//  PresetEngine.cs  -  Fase 4: presets de ligas reales
// ----------------------------------------------------------------------------
//  Un preset es una receta JSON (editorpro-respaldos\presets\*.json) de
//  una piramide real: ligas con su tier y sus equipos POR NOMBRE. El
//  motor la resuelve contra la base cargada (vista previa sin tocar
//  nada) y la aplica creando cada liga con DatabaseManager.
//  CreateNewLeague, el mismo camino auditado de Crear Liga.
//
//  Reglas duras:
//   - Un equipo ocupado NUNCA se mueve ni se reemplaza: se omite y se
//     reporta (la regla inviolable del editor).
//   - Solo entran equipos libres jugables (GetFreePlayableTeams).
//   - Liga con menos de MinEquipos asignados: se salta y se reporta.
//   - Un equipo puede forzarse por ID con la sintaxis "Nombre#123".
//
//  Idea de presets/plantillas tomada de FL26 Mod Studio (Matozanato,
//  MIT) y de los presets de Pes-Editor-Rebuilt (Agustinioquino,
//  BSD-3), reimplementada para PES 2021.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EditorLigasPes
{
    public class PresetLigaDef
    {
        public string Nombre = "";
        public int Tier = 1;
        public string Padre = "";
        public int Ascienden = -1;
        public int Descienden = -1;
        public List<string> Equipos = new List<string>();
    }

    public class PresetDef
    {
        public string Nombre = "";
        public string Archivo = "";
        public int Pais;
        public int Continente; // 0 = el del pais
        public List<PresetLigaDef> Ligas = new List<PresetLigaDef>();
    }

    public class PreviewTeam
    {
        public string Pedido = "";
        public string Estado = ""; // ok | ocupado | no-existe | ambiguo | no-jugable | duplicado
        public uint TeamId;
        public string NombreReal = "";
        public string Detalle = "";
    }

    public class PreviewLiga
    {
        public string Nombre = "";
        public int Tier = 1;
        public int CompIdAsignado; // 0 = ninguno
        public bool Viable;
        public string MotivoSalto = "";
        public int PadreCompId;
        public string PadreNombre = "";
        public List<PreviewTeam> Equipos = new List<PreviewTeam>();
        public int Asignados { get { return Equipos.Count(e => e.Estado == "ok"); } }
    }

    public class PresetPreview
    {
        public PresetDef Preset;
        public List<PreviewLiga> Ligas = new List<PreviewLiga>();
    }

    public class PresetApplyResult
    {
        public string Nombre = "";
        public bool Creada;
        public int CompId;
        public string Mensaje = "";
    }

    public static class PresetEngine
    {
        public const int MinEquipos = 8;

        public static string PresetsDir(AppConfig cfg)
        {
            string root = SiderBackup.RootDir(cfg);
            return root == null ? null : Path.Combine(root, "presets");
        }

        // ---------------- Carga de recetas ----------------

        public static List<PresetDef> ListPresets(AppConfig cfg, out List<string> errores)
        {
            errores = new List<string>();
            var list = new List<PresetDef>();
            string dir = PresetsDir(cfg);
            if (dir == null || !Directory.Exists(dir)) return list;
            foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
            {
                try
                {
                    var def = ParsePreset(File.ReadAllText(file));
                    def.Archivo = Path.GetFileName(file);
                    list.Add(def);
                }
                catch (Exception ex) { errores.Add(Path.GetFileName(file) + ": " + ex.Message); }
            }
            return list;
        }

        public static PresetDef LoadPreset(AppConfig cfg, string archivo)
        {
            string dir = PresetsDir(cfg);
            if (dir == null || string.IsNullOrWhiteSpace(archivo)) return null;
            string path = Path.Combine(dir, Path.GetFileName(archivo));
            if (!File.Exists(path)) return null;
            var def = ParsePreset(File.ReadAllText(path));
            def.Archivo = Path.GetFileName(path);
            return def;
        }

        private static PresetDef ParsePreset(string json)
        {
            var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            var def = new PresetDef
            {
                Nombre = GS(d, "nombre"),
                Pais = GI(d, "pais"),
                Continente = GI(d, "continente")
            };
            if (d.ContainsKey("ligas") && d["ligas"] is System.Collections.IEnumerable)
            {
                foreach (var o in (System.Collections.IEnumerable)d["ligas"])
                {
                    var ld = o as Dictionary<string, object>;
                    if (ld == null) continue;
                    var liga = new PresetLigaDef
                    {
                        Nombre = GS(ld, "nombre"),
                        Tier = Math.Max(1, GI(ld, "tier", 1)),
                        Padre = GS(ld, "padre"),
                        Ascienden = GI(ld, "ascienden", -1),
                        Descienden = GI(ld, "descienden", -1)
                    };
                    if (ld.ContainsKey("equipos") && ld["equipos"] is System.Collections.IEnumerable)
                    {
                        foreach (var e in (System.Collections.IEnumerable)ld["equipos"])
                        {
                            string s = Convert.ToString(e);
                            if (!string.IsNullOrWhiteSpace(s)) liga.Equipos.Add(s.Trim());
                        }
                    }
                    def.Ligas.Add(liga);
                }
            }
            return def;
        }

        // ---------------- Vista previa (solo lectura) ----------------

        public static string NormalizeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string decomposed = s.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            string up = sb.ToString();
            var parts = up.Split(new[] { ' ', '\t', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        public static PresetPreview Preview(DatabaseManager db, PresetDef preset)
        {
            var preview = new PresetPreview { Preset = preset };
            var freeIds = new Queue<byte>(db.GetAvailableCompetitionIds());
            var freePlayable = new HashSet<uint>(db.GetFreePlayableTeams(0).Select(t => t.Id));

            // ocupados: entradas de competiciones de tipo liga (0 o 4)
            var leagueCompIds = new HashSet<byte>();
            var compNames = new Dictionary<byte, string>();
            foreach (var c in db.Competitions)
            {
                var reg = db.Regulations.FirstOrDefault(r => r.IdCompetition == c.Id);
                string nombre = reg != null && !string.IsNullOrEmpty(reg.NameSpanish) ? reg.NameSpanish : (c.Name ?? "");
                compNames[c.Id] = nombre;
                if (reg != null && (reg.CompetitionType == 0 || reg.CompetitionType == 4)) leagueCompIds.Add(c.Id);
            }
            var ocupadoEn = new Dictionary<uint, string>();
            foreach (var e in db.Entries)
            {
                if (leagueCompIds.Contains(e.CompetitionId) && !ocupadoEn.ContainsKey(e.TeamId))
                    ocupadoEn[e.TeamId] = compNames.ContainsKey(e.CompetitionId) ? compNames[e.CompetitionId] : ("CompID " + e.CompetitionId);
            }

            // indice por nombre normalizado (es/en/base)
            var porNombre = new Dictionary<string, List<TeamRecord>>();
            foreach (var t in db.Teams)
            {
                foreach (string n in new[] { t.NameSpanish, t.NameEnglish, t.NameDatabase })
                {
                    string key = NormalizeName(n);
                    if (key.Length == 0) continue;
                    List<TeamRecord> l;
                    if (!porNombre.TryGetValue(key, out l)) { l = new List<TeamRecord>(); porNombre[key] = l; }
                    if (!l.Contains(t)) l.Add(t);
                }
            }

            var yaUsados = new HashSet<uint>();
            var compPorNombre = new Dictionary<string, byte>();
            foreach (var c in db.Competitions)
            {
                string key = NormalizeName(compNames.ContainsKey(c.Id) ? compNames[c.Id] : "");
                if (key.Length > 0 && !compPorNombre.ContainsKey(key)) compPorNombre[key] = c.Id;
                string ka = NormalizeName(c.Name);
                if (ka.Length > 0 && !compPorNombre.ContainsKey(ka)) compPorNombre[ka] = c.Id;
            }

            foreach (var ligaDef in preset.Ligas)
            {
                var pl = new PreviewLiga { Nombre = ligaDef.Nombre, Tier = ligaDef.Tier };
                foreach (string pedidoRaw in ligaDef.Equipos)
                {
                    var pt = new PreviewTeam { Pedido = pedidoRaw };
                    string pedido = pedidoRaw;
                    uint forcedId = 0;
                    int hash = pedido.LastIndexOf('#');
                    if (hash > 0 && uint.TryParse(pedido.Substring(hash + 1).Trim(), out forcedId))
                        pedido = pedido.Substring(0, hash).Trim();

                    TeamRecord team = null;
                    if (forcedId > 0)
                    {
                        team = db.Teams.FirstOrDefault(t => t.Id == forcedId);
                        if (team == null) { pt.Estado = "no-existe"; pt.Detalle = "No hay equipo con ID " + forcedId + "."; }
                    }
                    else
                    {
                        List<TeamRecord> cands;
                        if (porNombre.TryGetValue(NormalizeName(pedido), out cands) && cands.Count > 0)
                        {
                            if (cands.Count > 1)
                            {
                                pt.Estado = "ambiguo";
                                pt.Detalle = "Hay " + cands.Count + " equipos con ese nombre (IDs " + string.Join(", ", cands.Select(x => x.Id.ToString())) + "). Desambiguá con Nombre#ID.";
                            }
                            else team = cands[0];
                        }
                        else { pt.Estado = "no-existe"; pt.Detalle = "Ningún equipo de la base se llama así."; }
                    }

                    if (team != null)
                    {
                        pt.TeamId = team.Id;
                        pt.NombreReal = team.NameSpanish ?? team.NameEnglish ?? "";
                        if (yaUsados.Contains(team.Id))
                        {
                            pt.Estado = "duplicado";
                            pt.Detalle = "Ya está asignado a otra liga de este preset.";
                        }
                        else if (freePlayable.Contains(team.Id))
                        {
                            pt.Estado = "ok";
                            yaUsados.Add(team.Id);
                        }
                        else if (ocupadoEn.ContainsKey(team.Id))
                        {
                            pt.Estado = "ocupado";
                            pt.Detalle = "Ya juega en " + ocupadoEn[team.Id] + ": no se mueve ni se reemplaza.";
                        }
                        else
                        {
                            pt.Estado = "no-jugable";
                            pt.Detalle = "No figura como libre jugable (plantel con menos de 11 jugadores, selección o equipo clásico).";
                        }
                    }
                    pl.Equipos.Add(pt);
                }

                // padre: existente o una liga viable anterior de este preset
                if (!string.IsNullOrWhiteSpace(ligaDef.Padre))
                {
                    pl.PadreNombre = ligaDef.Padre;
                    string pk = NormalizeName(ligaDef.Padre);
                    byte pid;
                    var previa = preview.Ligas.FirstOrDefault(x => NormalizeName(x.Nombre) == pk && x.Viable);
                    if (previa != null) pl.PadreCompId = previa.CompIdAsignado;
                    else if (compPorNombre.TryGetValue(pk, out pid)) pl.PadreCompId = pid;
                }

                if (pl.Asignados < MinEquipos)
                {
                    pl.Viable = false;
                    pl.MotivoSalto = "Solo " + pl.Asignados + " equipos asignables (mínimo " + MinEquipos + ").";
                }
                else if (freeIds.Count == 0)
                {
                    pl.Viable = false;
                    pl.MotivoSalto = "No quedan CompID libres en la base.";
                }
                else
                {
                    pl.Viable = true;
                    pl.CompIdAsignado = freeIds.Dequeue();
                }
                preview.Ligas.Add(pl);
            }
            return preview;
        }

        // ---------------- Aplicacion ----------------

        public static List<PresetApplyResult> Apply(DatabaseManager db, PresetPreview preview, out List<int> createdIds)
        {
            createdIds = new List<int>();
            var results = new List<PresetApplyResult>();
            var preset = preview.Preset;
            byte continent = (byte)preset.Continente;
            if (continent == 0)
            {
                CountryRecord cr;
                if (preset.Pais > 0 && db.CountriesById.TryGetValue((ushort)preset.Pais, out cr)) continent = cr.Continent;
                if (continent == 0) continent = 4;
            }

            // padres creados en esta misma aplicacion, por nombre normalizado
            var creadosPorNombre = new Dictionary<string, byte>();
            foreach (var pl in preview.Ligas)
            {
                var res = new PresetApplyResult { Nombre = pl.Nombre };
                if (!pl.Viable)
                {
                    res.Creada = false;
                    res.Mensaje = "Saltada: " + pl.MotivoSalto;
                    results.Add(res);
                    continue;
                }
                byte padre = (byte)pl.PadreCompId;
                byte creadaAntes;
                if (padre == 0 && !string.IsNullOrWhiteSpace(pl.PadreNombre) &&
                    creadosPorNombre.TryGetValue(NormalizeName(pl.PadreNombre), out creadaAntes))
                    padre = creadaAntes;

                var teamIds = pl.Equipos.Where(e => e.Estado == "ok").Select(e => e.TeamId).ToList();
                var ligaDef = preset.Ligas.FirstOrDefault(l => l.Nombre == pl.Nombre);
                try
                {
                    db.CreateNewLeague(
                        (byte)pl.CompIdAsignado,
                        pl.Nombre, pl.Nombre, pl.Nombre, pl.Nombre,
                        continent,
                        (byte)teamIds.Count,
                        0,
                        padre == 0,
                        padre,
                        teamIds,
                        (ushort)preset.Pais,
                        3, 23, 2, 1, 0,
                        false, false, false, false, false,
                        ligaDef != null ? ligaDef.Ascienden : -1,
                        ligaDef != null ? ligaDef.Descienden : -1);
                    res.Creada = true;
                    res.CompId = pl.CompIdAsignado;
                    res.Mensaje = "Creada con CompID " + pl.CompIdAsignado + " y " + teamIds.Count + " equipos.";
                    createdIds.Add(pl.CompIdAsignado);
                    creadosPorNombre[NormalizeName(pl.Nombre)] = (byte)pl.CompIdAsignado;
                }
                catch (Exception ex)
                {
                    res.Creada = false;
                    res.Mensaje = "Error al crear: " + ex.Message;
                }
                results.Add(res);
            }
            return results;
        }

        private static string GS(Dictionary<string, object> d, string key)
        {
            return d != null && d.ContainsKey(key) && d[key] != null ? Convert.ToString(d[key]) : "";
        }

        private static int GI(Dictionary<string, object> d, string key) { return GI(d, key, 0); }

        private static int GI(Dictionary<string, object> d, string key, int def)
        {
            try { return d != null && d.ContainsKey(key) && d[key] != null ? Convert.ToInt32(d[key]) : def; }
            catch { return def; }
        }
    }
}
