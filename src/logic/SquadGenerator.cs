// ============================================================================
//  SquadGenerator.cs  -  Fase 5A: generador de plantillas jugables
// ----------------------------------------------------------------------------
//  Completa el plantel de un equipo con plantel corto (una cascara
//  generada, un club importado o uno creado a mano) fichando agentes
//  libres de la propia base, con equilibrio por lineas, y le da
//  tacticas y roles con la maquinaria existente del editor:
//
//   - Fichajes: DatabaseManager.SignFreeAgent (el camino auditado de
//     los traspasos: crea la asignacion con EntryId, dorsal libre y
//     orden de plantel, y actualiza todos los indices).
//   - Tacticas: DatabaseManager.ApplyTeamFormation con la formacion
//     por defecto (4-2-3-1) si el equipo no tiene ninguna.
//   - Roles: DatabaseManager.AutoAssignRoles.
//
//  Nunca se le saca un jugador a otro equipo: solo libres (ClubId 0).
//  El orden de fichaje es el orden de plantel: arquero titular,
//  titulares por linea y despues el banco, porque la formacion
//  referencia a los jugadores por su orden (SquadOrder).
//
//  Idea (generacion completa para clubes nuevos) tomada de FL26 Mod
//  Studio (Matozanato, MIT), reimplementada para PES 2021.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace EditorLigasPes
{
    public class SquadLinePreview
    {
        public string Linea = ""; // ARQ | DEF | MED | DEL
        public int Actual;
        public int Cuota;
        public List<PlayerRecord> Fichados = new List<PlayerRecord>();
    }

    public class SquadPreview
    {
        public uint TeamId;
        public string TeamName = "";
        public int CurrentCount;
        public int Target;
        public List<SquadLinePreview> Lineas = new List<SquadLinePreview>();
        public List<PlayerRecord> SigningOrder = new List<PlayerRecord>();
        public List<string> Warnings = new List<string>();
        public bool HasTactics;
        public int FinalCount { get { return CurrentCount + SigningOrder.Count; } }
    }

    public class SquadApplyResult
    {
        public int Signed;
        public int Skipped;
        public int FinalCount;
        public bool FormationApplied;
        public bool RolesAssigned;
    }

    public static class SquadGenerator
    {
        public const int DefaultTarget = 23;
        public const int HardCap = 32;

        public static string LineOf(PlayerRecord p)
        {
            int pos = p != null ? p.PositionIndex : 5;
            if (pos <= 0) return "ARQ";
            if (pos <= 3) return "DEF";
            if (pos <= 8) return "MED";
            return "DEL";
        }

        private static void Quotas(int target, out int gk, out int def, out int mid, out int fwd)
        {
            gk = target >= 18 ? 3 : (target >= 12 ? 2 : 1);
            int rest = Math.Max(0, target - gk);
            def = (int)Math.Round(rest * 0.40);
            mid = (int)Math.Round(rest * 0.35);
            fwd = Math.Max(0, rest - def - mid);
        }

        public static SquadPreview Preview(DatabaseManager db, uint teamId, int target)
        {
            if (target < 11) target = 11;
            if (target > HardCap) target = HardCap;
            var pv = new SquadPreview { TeamId = teamId, Target = target };
            TeamRecord team;
            if (db.TeamsById != null && db.TeamsById.TryGetValue(teamId, out team))
                pv.TeamName = team.NameSpanish ?? team.NameEnglish ?? "";
            List<TacticsRecord> tlist;
            pv.HasTactics = db.TacticsByClub != null && db.TacticsByClub.TryGetValue(teamId, out tlist) && tlist != null && tlist.Count > 0;

            // Plantel actual por linea
            var roster = db.GetTeamRoster(teamId);
            pv.CurrentCount = roster.Count;
            var currentByLine = new Dictionary<string, int> { { "ARQ", 0 }, { "DEF", 0 }, { "MED", 0 }, { "DEL", 0 } };
            foreach (var a in roster)
            {
                PlayerRecord p;
                if (db.PlayersById != null && db.PlayersById.TryGetValue(a.PlayerId, out p) && p != null)
                    currentByLine[LineOf(p)]++;
            }

            int qGk, qDef, qMid, qFwd;
            Quotas(target, out qGk, out qDef, out qMid, out qFwd);
            var quota = new Dictionary<string, int> { { "ARQ", qGk }, { "DEF", qDef }, { "MED", qMid }, { "DEL", qFwd } };

            // Agentes libres por linea, mejores primero
            var pool = new Dictionary<string, List<PlayerRecord>>();
            foreach (var key in quota.Keys) pool[key] = new List<PlayerRecord>();
            foreach (var p in db.Players.Where(x => x.ClubId == 0))
                pool[LineOf(p)].Add(p);
            foreach (var key in quota.Keys)
                pool[key] = pool[key].OrderByDescending(x => x.Overall).ThenBy(x => x.Id).ToList();

            int need = Math.Max(0, target - pv.CurrentCount);
            var picked = new Dictionary<string, List<PlayerRecord>>();
            foreach (var key in quota.Keys)
            {
                int deficit = Math.Max(0, quota[key] - currentByLine[key]);
                var take = pool[key].Take(deficit).ToList();
                if (take.Count < deficit)
                    pv.Warnings.Add("Quedan solo " + pool[key].Count + " agentes libres de " + key + " y hacían falta " + deficit + ".");
                picked[key] = take;
            }

            int totalPicked = picked.Values.Sum(l => l.Count);
            if (totalPicked < need)
            {
                // Completar con lo que quede, campo primero y arqueros al final
                var usados = new HashSet<uint>(picked.Values.SelectMany(l => l).Select(x => x.Id));
                var restoCampo = pool["DEF"].Concat(pool["MED"]).Concat(pool["DEL"])
                    .Where(x => !usados.Contains(x.Id))
                    .OrderByDescending(x => x.Overall).ThenBy(x => x.Id).ToList();
                var restoArq = pool["ARQ"]
                    .Where(x => !usados.Contains(x.Id))
                    .OrderByDescending(x => x.Overall).ThenBy(x => x.Id).ToList();
                var resto = restoCampo.Concat(restoArq).ToList();
                foreach (var p in resto)
                {
                    if (totalPicked >= need) break;
                    picked[LineOf(p)].Add(p);
                    totalPicked++;
                }
                if (totalPicked < need)
                    pv.Warnings.Add("No hay suficientes agentes libres en la base: el plantel quedaría en " + (pv.CurrentCount + totalPicked) + " de " + target + ".");
            }

            // Orden de fichaje = orden de plantel: ARQ titular, titulares
            // por linea (DEF, MED, DEL) y despues el resto del banco.
            var gks = picked["ARQ"].OrderByDescending(x => x.Overall).ToList();
            var campo = picked["DEF"].Concat(picked["MED"]).Concat(picked["DEL"])
                .OrderBy(x => x.KeyOfLine()).ThenByDescending(x => x.Overall).ToList();
            var orden = new List<PlayerRecord>();
            if (gks.Count > 0) orden.Add(gks[0]);
            orden.AddRange(campo.Take(10));
            orden.AddRange(gks.Skip(1));
            orden.AddRange(campo.Skip(10));
            pv.SigningOrder = orden;

            foreach (var key in new[] { "ARQ", "DEF", "MED", "DEL" })
            {
                pv.Lineas.Add(new SquadLinePreview
                {
                    Linea = key,
                    Actual = currentByLine[key],
                    Cuota = quota[key],
                    Fichados = picked[key]
                });
            }

            int finalGk = currentByLine["ARQ"] + picked["ARQ"].Count;
            if (finalGk == 0) pv.Warnings.Add("ATENCIÓN: el equipo quedaría SIN arquero (no hay libres en la base). Así no puede jugar.");
            if (!pv.HasTactics) pv.Warnings.Add("El equipo no tiene tácticas: se le aplica la formación por defecto (4-2-3-1).");
            return pv;
        }

        public static SquadApplyResult Apply(DatabaseManager db, uint teamId, int target)
        {
            var pv = Preview(db, teamId, target);
            var res = new SquadApplyResult();
            foreach (var p in pv.SigningOrder)
            {
                if (db.SignFreeAgent(p.Id, teamId)) res.Signed++;
                else res.Skipped++;
            }
            if (!pv.HasTactics)
            {
                db.ApplyTeamFormation(teamId, db.GetTeamFormation(teamId));
                res.FormationApplied = true;
            }
            res.RolesAssigned = db.AutoAssignRoles(teamId);
            res.FinalCount = db.GetTeamRoster(teamId).Count;
            return res;
        }
    }

    internal static class SquadPlayerOrderExt
    {
        public static int KeyOfLine(this PlayerRecord p)
        {
            string l = SquadGenerator.LineOf(p);
            return l == "DEF" ? 0 : (l == "MED" ? 1 : (l == "DEL" ? 2 : 3));
        }
    }
}
