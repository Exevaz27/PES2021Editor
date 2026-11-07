using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EditorLigasPes
{
    public class CustomLeagueInfo
    {
        public int RegId { get; set; }
        public int CompId { get; set; }
        public int Slot { get; set; }
        public int CountryId { get; set; }
        public string CountryName { get; set; }
        public int KickoffAfter { get; set; }
        public int Tier { get; set; }
        public int Promote { get; set; }
        public int Demote { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Generador automatico de modulo Lua para Sider 7.x (eFootball PES 2021).
    /// Integra:
    /// 1. Ganchos Anti-Cuelgues (Null-Guards) para proteccion contra fallos de punteros nulos.
    /// 2. Expansion de Slots de Competicion (123 -> 250 Slots, accesores en 0x163400000).
    /// 3. Ranuras de Equipo a 8 bits (Slots 0..249 en 0x163C00000 sin colisiones).
    /// 4. Expansion de tabla interna de competiciones (0x1434FDF00).
    /// 5. Banderas de pais nativas (0x140C950D0).
    /// 6. Orden en menu Exhibicion/Kick Off (0x140EAD990).
    /// 7. Listas del Modo Editar (0x141EE68F0).
    /// Desarrollado por EXEVAZ27.
    /// </summary>
    public static class SiderLuaGenerator
    {
                public static string EscapeLuaString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\r", "")
                      .Replace("\n", " ")
                      .Replace("\t", " ");
        }

        // world.txt (fl26world.txt) no tiene sintaxis de escape: los parsers FL26
        // (fl26chain.lua, fl26comptab.lua, etc.) toman el resto de la linea tras
        // "name=" tal cual, hasta fin de linea. Escapar con backslash o comillas
        // como en lua dejaria esos caracteres literales en el nombre, asi que
        // aqui solo se neutraliza lo que romperia la linea: el retorno de carro
        // se quita y el salto de linea y el tabulador se sustituyen por espacio
        // (un salto de linea en el nombre inyectaria una linea nueva en el archivo).
        public static string EscapeWorldTxtName(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\r", "").Replace("\n", " ").Replace("\t", " ");
        }

        public static string EscapeLuaComment(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\r", "").Replace("\n", " ").Replace("--", "- -");
        }

        public static string GenerateLuaScript(List<CompetitionRecord> customLeagues, List<RegulationRecord> customRegulations, DatabaseManager dbManager = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("-- ============================================================================");
            sb.AppendLine("-- MODULO SIDER: LEAGUE MANAGER (PES 2021)");
            sb.AppendLine("-- eFootball PES 2021 Season Update (v1.07.02)");
            sb.AppendLine("-- Desarrollado por EXEVAZ27 | Edicion Portable");
            sb.AppendLine("-- Modulo todo-en-uno: Ganchos Anti-Cuelgues, 250 Slots, 8-Bit Slots y Ligas Mod");
            sb.AppendLine("-- ============================================================================\n");

            sb.AppendLine("local m = {}");
            sb.AppendLine("m.version = \"4.2.0\"");
            sb.AppendLine("m.author = \"EXEVAZ27\"\n");

            sb.AppendLine("-- ============================================================================");
            sb.AppendLine("-- 1. CONFIGURACION DE LIGAS PERSONALIZADAS");
            sb.AppendLine("-- ============================================================================");
            sb.AppendLine("local CUSTOM_LEAGUES = {");

            var leaguesToProcess = new List<CustomLeagueInfo>();

            if (customLeagues != null && customLeagues.Count > 0)
            {
                var customSlots = new Dictionary<byte, int>();
                int slotIndex = 84;

                // 1ra pasada: Asignar slots a cada liga custom (iniciando en 84 para no colisionar con Konami)
                foreach (var c in customLeagues.OrderBy(x => x.Id))
                {
                    if (c.Id < 30) continue;
                    if (slotIndex > 249)
                        throw new InvalidOperationException("Demasiadas ligas personalizadas: los slots 84..249 solo admiten 166 ligas.");
                    customSlots[c.Id] = slotIndex++;
                }

                // 2da pasada: Resolver pais, slot padre (kickoff_after) y jerarquia (tier)
                foreach (var c in customLeagues.OrderBy(x => x.Id))
                {
                    if (c.Id < 30) continue;
                    int compId = c.Id;
                    int slot = customSlots[c.Id];

                    var reg = customRegulations != null ? customRegulations.FirstOrDefault(r => r.IdCompetition == c.Id) : null;
                    int regId = reg != null ? reg.Id : 179;

                    // 1. Resolver pais automaticamente
                    int countryId = dbManager != null ? (int)dbManager.GetLeagueCountryId(c.Id) : 144;
                    string countryName = "Pais ID " + countryId;
                    if (dbManager != null && dbManager.CountriesById.ContainsKey((ushort)countryId))
                    {
                        countryName = dbManager.CountriesById[(ushort)countryId].NameSpanish;
                    }

                    // 2. Resolver Tier dinamicamente (sin dbManager, por cadena de divisiones).
                    int tier = dbManager != null ? dbManager.GetLeagueTier(c.Id) : ResolveTierFallback(reg, customRegulations);

                    // 3. Resolver Slot Padre para Kick Off (kickoff_after)
                    int kickoffAfter = 14; // Fallback por defecto: Argentina D1
                    byte parentCompId = 0;
                    if (reg != null && reg.IdDivision1 != 0)
                    {
                        var parentReg = customRegulations != null ? customRegulations.FirstOrDefault(r => r.Id == reg.IdDivision1) : null;
                        if (parentReg != null)
                        {
                            ushort parentCountry = dbManager != null ? dbManager.GetLeagueCountryId(parentReg.IdCompetition) : (ushort)0;
                            if (parentCountry == 0 || parentCountry == countryId)
                            {
                                parentCompId = parentReg.IdCompetition;
                            }
                        }

                        // Si no resolvio por Reg ID, verificar si se asigno directamente el Competition ID como Div1 (ej. 22 para Liga Argentina).
                        // Solo si cabe en un byte: el cast anterior truncaba RegIds > 255 y podia
                        // emparejar con una liga cualquiera por accidente.
                        if (parentCompId == 0 && reg.IdDivision1 <= 255)
                        {
                            byte candidateCompId = (byte)reg.IdDivision1;
                            if (customSlots.ContainsKey(candidateCompId) || DatabaseManager.VanillaCompSlots.ContainsKey(candidateCompId))
                            {
                                parentCompId = candidateCompId;
                            }
                        }

                        if (parentCompId != 0)
                        {
                            if (customSlots.ContainsKey(parentCompId))
                            {
                                kickoffAfter = customSlots[parentCompId];
                            }
                            else if (DatabaseManager.VanillaCompSlots.ContainsKey(parentCompId))
                            {
                                kickoffAfter = DatabaseManager.VanillaCompSlots[parentCompId];
                            }
                        }
                    }

                    // M137: c.Name puede ser null (la linea siguiente si protegia reg.NameSpanish).
                    // M143: se aceptan ambas ortografias en ambos campos (antes CLASSIC
                    // solo en c.Name y CLASIC solo en reg.NameSpanish).
                    bool isClassic = (c.Name != null && (c.Name.IndexOf("CLASSIC", StringComparison.OrdinalIgnoreCase) >= 0 || c.Name.IndexOf("CLASIC", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                        (reg != null && reg.NameSpanish != null && (reg.NameSpanish.IndexOf("CLASSIC", StringComparison.OrdinalIgnoreCase) >= 0 || reg.NameSpanish.IndexOf("CLASIC", StringComparison.OrdinalIgnoreCase) >= 0));
                    if (isClassic)
                    {
                        countryId = 0;
                        countryName = "Internacional";
                        kickoffAfter = 5; // Slot 5: Konami Native Classic Teams
                        tier = 1;
                    }
                    else if (parentCompId == 0)
                    {
                        // Liga sin padre resuelto: Ubicar por pais o continente
                        if (countryId == 144) kickoffAfter = 14; // Argentina D1
                        else if (countryId == 204) kickoffAfter = 7; // Inglaterra Premier
                        else if (countryId == 200) kickoffAfter = 8; // España La Liga
                        else if (countryId == 179) kickoffAfter = 9; // Italia Serie A
                        else if (countryId == 146) kickoffAfter = 15; // Brasil Brasileirao
                        else if (c.Continent == 2) kickoffAfter = 7; // Europa
                        else if (c.Continent == 4) kickoffAfter = 14; // Sudamerica
                        else kickoffAfter = 14;
                    }

                    string name = !string.IsNullOrEmpty(c.Name) ? c.Name : (reg != null && !string.IsNullOrEmpty(reg.NameSpanish) ? reg.NameSpanish : "LIGA MOD");
                    name = name.Replace("\r", "").Replace("\n", " ").Trim();

                    int promote = dbManager != null ? dbManager.GetLeaguePromote(c.Id) : (tier > 1 ? 3 : 0);
                    int demote = dbManager != null ? dbManager.GetLeagueDemote(c.Id) : (reg != null && reg.IdDivision2 != 0 ? 3 : 0);
                    // Una liga clasica no asciende ni desciende: se fuerzan a 0 aunque
                    // el dbManager devuelva otros valores (antes no se forzaban).
                    if (isClassic) { promote = 0; demote = 0; }

                    leaguesToProcess.Add(new CustomLeagueInfo
                    {
                        RegId = regId,
                        CompId = compId,
                        Slot = slot,
                        CountryId = countryId,
                        CountryName = countryName,
                        KickoffAfter = kickoffAfter,
                        Tier = tier,
                        Promote = promote,
                        Demote = demote,
                        Name = name
                    });
                }
            }

            if (leaguesToProcess.Count == 0)
            {
                // Liga por defecto: Primera B Nacional
                leaguesToProcess.Add(new CustomLeagueInfo
                {
                    RegId = 179,
                    CompId = 50,
                    Slot = 84,
                    CountryId = 144,
                    CountryName = "ARGENTINA",
                    KickoffAfter = 14,
                    Tier = 2,
                    Promote = 3,
                    Demote = 3,
                    Name = "PRIMERA B NACIONAL [MOD]"
                });
            }

            foreach (var l in leaguesToProcess)
            {
                sb.AppendLine("    {");
                sb.AppendLine(string.Format("        reg_id = {0},", l.RegId));
                sb.AppendLine(string.Format("        comp_id = {0},", l.CompId));
                sb.AppendLine(string.Format("        slot = {0},", l.Slot));
                sb.AppendLine(string.Format("        country_id = {0},        -- {1}", l.CountryId, EscapeLuaComment(l.CountryName)));
                sb.AppendLine(string.Format("        kickoff_after = {0},      -- Ubicada debajo de slot {0}", l.KickoffAfter));
                sb.AppendLine(string.Format("        tier = {0},", l.Tier));
                sb.AppendLine(string.Format("        promote = {0},", l.Promote));
                sb.AppendLine(string.Format("        demote = {0},", l.Demote));
                sb.AppendLine(string.Format("        name = \"{0}\"", EscapeLuaString(l.Name)));
                sb.AppendLine("    },");
            }

            sb.AppendLine("}\n");

            sb.Append(GetEngineLuaTemplate());

            // 3. Las directivas Split / Apertura-Clausura NO van en este
            // lua: son texto plano que fl26chain lee de modules\fl26world.txt
            // (ver WriteWorldFile). Escribirlas aca rompia la sintaxis del
            // modulo entero y ninguna liga custom cargaba.

            return sb.ToString();
        }

        public static void SaveLuaToFile(string filePath, List<CompetitionRecord> customLeagues, List<RegulationRecord> customRegulations, DatabaseManager dbManager = null)
        {
            string code = GenerateLuaScript(customLeagues, customRegulations, dbManager);
            File.WriteAllText(filePath, code, Encoding.UTF8);
        }

        // Tier sin dbManager: se recorre la cadena de divisiones dentro de las
        // regulaciones custom (D3 bajo D2 bajo D1 => tier 3). Si el padre no esta en
        // las customs (padre vanilla), no se puede saber la profundidad: se mantiene
        // el 2 historico como supuesto documentado, no como dato.
        private static int ResolveTierFallback(RegulationRecord reg, List<RegulationRecord> customRegulations)
        {
            if (reg == null || reg.IdDivision1 == 0) return 1;
            int tier = 1;
            var visited = new HashSet<ushort>();
            var current = reg;
            while (current != null && current.IdDivision1 != 0 && visited.Add(current.Id))
            {
                tier++;
                if (customRegulations == null) break;
                var parent = customRegulations.FirstOrDefault(r => r.Id == current.IdDivision1);
                if (parent == null) break; // Padre vanilla: profundidad desconocida, tier queda como esta (>= 2).
                current = parent;
            }
            return tier;
        }

        public static string GenerateWorldTxt(List<CompetitionRecord> customLeagues, List<RegulationRecord> customRegulations, DatabaseManager dbManager = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine("# ARCHIVO DE CONFIGURACION DE LIGA MASTER (fl26world.txt)");
            sb.AppendLine("# Generado automaticamente por PES 2021 Editor Pro (EXEVAZ27)");
            sb.AppendLine("# Alimenta: fl26joindll (Calendarios y Rollover Anual) y fl26chain (Ascensos/Descensos y Copas)");
            sb.AppendLine("# ==============================================================================\n");

            // 1. Directivas de Temporada Estacional (0=Agosto-Mayo / 1=Febrero-Diciembre)
            var seasonByRegion = new Dictionary<int, byte>();
            if (customLeagues != null)
            {
                foreach (var c in customLeagues)
                {
                    if (c.Id < 30) continue;
                    int region = (int)c.Group;
                    byte st = dbManager != null ? dbManager.GetLeagueSeasonType(c.Id) : (byte)0;
                    if (st > 0 && !seasonByRegion.ContainsKey(region))
                    {
                        // Dos ligas de la misma region con distinto tipo: gana la primera
                        // (antes ganaba la ultima en silencio, segun el orden de la lista).
                        seasonByRegion[region] = st;
                    }
                }
            }

            if (seasonByRegion.Count > 0)
            {
                sb.AppendLine("# DIRECTIVAS DE TEMPORADA (0=Europeo Ago-May / 1=Sudamericano Feb-Dic)");
                foreach (var kvp in seasonByRegion.OrderBy(x => x.Key))
                {
                    sb.AppendLine(string.Format("season {0} {1}", kvp.Key, kvp.Value));
                }
                sb.AppendLine();
            }

            int count = 0;
            if (customLeagues != null && customLeagues.Count > 0)
            {
                var customSlots = new Dictionary<byte, int>();
                int slotIndex = 84;
                foreach (var c in customLeagues.OrderBy(x => x.Id))
                {
                    if (c.Id < 30) continue;
                    if (slotIndex > 249)
                        throw new InvalidOperationException("Demasiadas ligas personalizadas: los slots 84..249 solo admiten 166 ligas.");
                    customSlots[c.Id] = slotIndex++;
                }

                foreach (var c in customLeagues.OrderBy(x => x.Id))
                {
                    if (c.Id < 30) continue;
                    var reg = customRegulations != null ? customRegulations.FirstOrDefault(r => r.IdCompetition == c.Id) : null;
                    int regId = reg != null ? reg.Id : 179;
                    int countryId = dbManager != null ? (int)dbManager.GetLeagueCountryId(c.Id) : 144;
                    int region = (int)c.Group;
                    int slot = customSlots.ContainsKey(c.Id) ? customSlots[c.Id] : 84;
                    int tier = dbManager != null ? dbManager.GetLeagueTier(c.Id) : ResolveTierFallback(reg, customRegulations);
                    int above = reg != null && reg.IdDivision1 != 0 ? reg.IdDivision1 : 0;
                    int promote = dbManager != null ? dbManager.GetLeaguePromote(c.Id) : (tier > 1 ? 3 : 0);
                    int demote = dbManager != null ? dbManager.GetLeagueDemote(c.Id) : (reg != null && reg.IdDivision2 != 0 ? 3 : 0);
                    // Cantidad real de equipos: entradas de la BD y, si no, TotalTeams
                    // de la regulacion. Solo si no hay ningun dato se usa 20, y se deja
                    // constancia en el propio world file (antes se inventaba en silencio).
                    int clubs = dbManager != null && dbManager.EntriesByCompetition.ContainsKey(c.Id) ? dbManager.EntriesByCompetition[c.Id].Count : 0;
                    if (clubs == 0 && reg != null && reg.TotalTeams > 0) clubs = reg.TotalTeams;
                    bool clubsAssumed = false;
                    if (clubs == 0) { clubs = 20; clubsAssumed = true; }
                    string name = !string.IsNullOrEmpty(c.Name) ? c.Name : (reg != null && !string.IsNullOrEmpty(reg.NameSpanish) ? reg.NameSpanish : "LIGA MOD");

                    ushort cupId = dbManager != null ? dbManager.GetLeagueLinkedCup(c.Id) : (ushort)0;
                    ushort scupId = dbManager != null ? dbManager.GetLeagueLinkedSuperCup(c.Id) : (ushort)0;

                    if (clubsAssumed)
                        sb.AppendLine("# NOTA: clubs=20 supuesto para la liga " + regId + " (sin entradas ni TotalTeams en la regulacion).");
                    sb.Append(string.Format("league {0} cid={1} region={2} country={3} slot={4} tier={5}",
                        regId, c.Id, region, countryId, slot, tier));
                    if (above > 0)
                    {
                        sb.Append(string.Format(" above={0}", above));
                    }
                    sb.Append(string.Format(" promote={0} demote={1} clubs={2} legs=2",
                        promote, demote, clubs));

                    if (cupId > 0)
                    {
                        sb.Append(string.Format(" cup={0}", cupId));
                        if (tier >= 2 && above > 0)
                        {
                            sb.Append(string.Format(" cuptop={0} cuplow={1} cupall=1", above, regId));
                        }
                        else
                        {
                            sb.Append(" cupall=1");
                        }
                    }

                    if (scupId > 0)
                    {
                        sb.Append(string.Format(" scup={0}", scupId));
                    }

                    sb.Append(string.Format(" name={0}", EscapeWorldTxtName(name)));
                    sb.AppendLine();
                    count++;
                }
            }

            if (count == 0)
            {
                // Fallback liga por defecto (sin customs): above/cup solo se emiten si
                // el dbManager los conoce. Antes se inventaban above=22 (un CompId en un
                // campo de RegIds: el above del world file va en espacio de RegIds, como
                // el primer token de cada linea) y cup=54 sin fuente.
                string fallbackAbove = "";
                string fallbackCup = "";
                if (dbManager != null)
                {
                    var parentReg = dbManager.Regulations.FirstOrDefault(r => r.IdCompetition == 22);
                    if (parentReg != null) fallbackAbove = " above=" + parentReg.Id;
                    ushort fc = dbManager.GetLeagueLinkedCup(50);
                    if (fc > 0) fallbackCup = " cup=" + fc + " cupall=1";
                }
                sb.AppendLine("league 179 cid=50 region=17 country=144 slot=84 tier=2" + fallbackAbove + " promote=3 demote=3 clubs=20 legs=2" + fallbackCup + " name=PRIMERA B NACIONAL [MOD]");
            }

            return sb.ToString();
        }

        // Directivas split vigentes (Apertura/Clausura) en el formato que
        // fl26chain parsea de fl26world.txt:
        //   split <reg> regular=<reg> groups=<regClausura> carry=0
        // managedRegIds: los RegId de TODAS las ligas custom del despliegue
        // (las gestiona el editor, tengan o no la funcion activa).
        public static List<string> BuildSplitDirectives(List<CompetitionRecord> customLeagues, List<RegulationRecord> customRegulations, DatabaseManager dbManager, out HashSet<int> managedRegIds)
        {
            var lines = new List<string>();
            managedRegIds = new HashSet<int>();
            if (customLeagues == null || dbManager == null) return lines;
            foreach (var c in customLeagues)
            {
                if (c.Id < 30) continue;
                var reg = customRegulations != null ? customRegulations.FirstOrDefault(r => r.IdCompetition == c.Id) : null;
                int regId = reg != null ? reg.Id : (int)c.Id;
                managedRegIds.Add(regId);
                if (dbManager.GetLeagueAperturaClausura(c.Id))
                {
                    ushort clauReg = dbManager.GetLeagueClausuraRegulation(c.Id);
                    int secondReg = clauReg > 0 ? (int)clauReg : regId;
                    lines.Add(string.Format("split {0} regular={0} groups={1} carry=0", regId, secondReg));
                }
            }
            return lines;
        }

        // Escribe modules\fl26world.txt: el contenido generado
        // (season/league de GenerateWorldTxt + las directivas split)
        // conservando las lineas preexistentes que el editor no gestiona:
        // directivas split de ligas ajenas, comentarios de terceros y
        // cualquier linea que no sea season/league/split ni encabezado
        // generado. Las split de ligas gestionadas se reescriben con el
        // estado actual (si la funcion se apago, desaparecen).
        public static void WriteWorldFile(string modulesDir, List<CompetitionRecord> customLeagues, List<RegulationRecord> customRegulations, DatabaseManager dbManager)
        {
            string path = Path.Combine(modulesDir, "fl26world.txt");
            HashSet<int> managed;
            var splits = BuildSplitDirectives(customLeagues, customRegulations, dbManager, out managed);
            string generated = GenerateWorldTxt(customLeagues, customRegulations, dbManager);
            if (splits.Count > 0)
            {
                var sbw = new StringBuilder();
                sbw.Append(generated);
                if (!generated.EndsWith("\n")) sbw.AppendLine();
                sbw.AppendLine("# DIRECTIVAS APERTURA / CLAUSURA (Tabla Anual Acumulada en Liga Master - carry=0)");
                foreach (var s in splits) sbw.AppendLine(s);
                generated = sbw.ToString();
            }

            var preserved = new List<string>();
            if (File.Exists(path))
            {
                foreach (var rawLine in File.ReadAllLines(path))
                {
                    string t = rawLine.Trim();
                    if (t.Length == 0) continue;
                    var m = Regex.Match(t, @"^split\s+(\d+)");
                    if (m.Success)
                    {
                        int sid;
                        if (int.TryParse(m.Groups[1].Value, out sid) && !managed.Contains(sid)) preserved.Add(rawLine);
                        continue;
                    }
                    if (t.StartsWith("season ") || t.StartsWith("league ")) continue;
                    if (t.StartsWith("#"))
                    {
                        if (t.StartsWith("# ==") || t.StartsWith("# NOTA: clubs=20 supuesto") ||
                            t.Contains("ARCHIVO DE CONFIGURACION DE LIGA MASTER") ||
                            t.Contains("Generado automaticamente por PES 2021 Editor Pro") ||
                            t.Contains("Alimenta: fl26joindll") ||
                            t.Contains("DIRECTIVAS DE TEMPORADA") ||
                            t.Contains("DIRECTIVAS APERTURA / CLAUSURA") ||
                            t.Contains("Contenido preexistente conservado")) continue;
                        preserved.Add(rawLine);
                        continue;
                    }
                    preserved.Add(rawLine);
                }
            }
            if (preserved.Count > 0)
            {
                var sbp = new StringBuilder();
                sbp.Append(generated);
                if (!generated.EndsWith("\n")) sbp.AppendLine();
                sbp.AppendLine("# --- Contenido preexistente conservado (no lo gestiona el editor) ---");
                foreach (var pl in preserved) sbp.AppendLine(pl);
                generated = sbp.ToString();
            }
            File.WriteAllText(path, generated, Encoding.UTF8);
        }

        public static bool DeploySiderModules(string siderDir, List<CompetitionRecord> customLeagues, List<RegulationRecord> customRegulations, DatabaseManager dbManager, out string resultMessage, bool enableSwiss = false)
        {
            try
            {
                if (string.IsNullOrEmpty(siderDir) || !Directory.Exists(siderDir))
                {
                    resultMessage = "El directorio de Sider no existe o no es accesible:\n" + siderDir;
                    return false;
                }

                string modulesDir = Path.Combine(siderDir, "modules");
                if (!Directory.Exists(modulesDir))
                {
                    Directory.CreateDirectory(modulesDir);
                }

                // Desplegar modulos auxiliares y DLLs nativas de Liga Master y ascensos multidivision
                string[] filesToCopy = new[] {
                    "fl26edit.dll",
                    "fl26edit.lua",
                    "fl26editlist.lua",
                    "fl26caps.lua",
                    "fl26appcap.lua",
                    "fl26regs600.lua",
                    "fl26slots250.lua",
                    "fl26slot7.lua",
                    "fl26guards.lua",
                    "fl26nullguard12.lua",
                    "fl26nullguard13.lua",
                    "fl26boardguard.lua",
                    "fl26ctlguard.lua",
                    "fl26kitguard.lua",
                    "fl26resultsguard.lua",
                    "fl26superguard.lua",
                    "fl26seasonend.lua",
                    "fl26augseason.lua",
                    "fl26regen.dll",
                    "fl26regen.lua",
                    "fl26regfast.lua",
                    "fl26walk.dll",
                    "fl26walk.lua",
                    "fl26compcache.dll",
                    "fl26compcache.lua",
                    "fl26join.dll",
                    "fl26joindll.lua",
                    "fl26chain.dll",
                    "fl26chain.lua",
                    "fl26swiss.dll",
                    "fl26swiss.lua",
                    "fl26rank.lua",
                    "fl26deeprank.lua",
                    "fl26reg128.lua",
                    "fl26catlist.lua",
                    "fl26slotnames.lua",
                    "fl26clubs.dll",
                    "fl26clubs.lua"
                };
                var deployedFiles = new List<string>();
                var missingFiles = new List<string>();

                foreach (var fname in filesToCopy)
                {
                    string target = Path.Combine(modulesDir, fname);
                    string src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules", fname);
                    if (!File.Exists(src))
                    {
                        src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fname);
                    }
                    if (!File.Exists(src))
                    {
                        src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", fname);
                    }
                    if (File.Exists(src))
                    {
                        try
                        {
                            if (File.Exists(target))
                            {
                                File.Copy(target, target + ".bak", true);
                            }
                            File.Copy(src, target, true);
                            deployedFiles.Add(fname);
                        }
                        catch (Exception ex)
                        {
                            missingFiles.Add(string.Format("{0} (Error al copiar: {1})", fname, ex.Message));
                        }
                    }
                    else
                    {
                        missingFiles.Add(fname);
                    }
                }

                // 4. Desplegar league_manager.lua con respaldo previo
                string lmPath = Path.Combine(modulesDir, "league_manager.lua");
                if (File.Exists(lmPath))
                {
                    try { File.Copy(lmPath, lmPath + ".bak", true); } catch { }
                }
                string lmContent = GenerateLuaScript(customLeagues, customRegulations, dbManager);
                File.WriteAllText(lmPath, lmContent, Encoding.UTF8);
                deployedFiles.Add("league_manager.lua");

                // 4.1. Desplegar descriptor fl26world.txt con respaldo previo
                string worldPath = Path.Combine(modulesDir, "fl26world.txt");
                if (File.Exists(worldPath))
                {
                    try { File.Copy(worldPath, worldPath + ".bak", true); } catch { }
                }
                WriteWorldFile(modulesDir, customLeagues, customRegulations, dbManager);
                deployedFiles.Add("fl26world.txt");

                // 5. Configurar sider.ini
                string iniPath = Path.Combine(siderDir, "sider.ini");
                bool siderIniExists = File.Exists(iniPath);
                bool siderIniWritten = false;
                if (siderIniExists)
                {
                    string iniContent = File.ReadAllText(iniPath, Encoding.UTF8);
                    bool iniModified = false;

                    // Conservar los finales de linea originales (LF o CRLF):
                    // el despliegue anterior convertia todo a CRLF y, al
                    // escribir con Encoding.UTF8, agregaba un BOM que puede
                    // hacer que Sider no reconozca la seccion [sider].
                    string iniNewline = iniContent.Contains("\r\n") ? "\r\n" : "\n";

                    // Asegurar luajit.ext.enabled = 1
                    if (!Regex.IsMatch(iniContent, @"luajit\.ext\.enabled\s*=\s*1"))
                    {
                        if (Regex.IsMatch(iniContent, @"luajit\.ext\.enabled\s*=\s*\d+"))
                        {
                            iniContent = Regex.Replace(iniContent, @"luajit\.ext\.enabled\s*=\s*\d+", "luajit.ext.enabled = 1");
                            iniModified = true;
                        }
                        else if (iniContent.Contains("[sider]"))
                        {
                            iniContent = iniContent.Replace("[sider]", "[sider]" + iniNewline + "luajit.ext.enabled = 1");
                            iniModified = true;
                        }
                        else
                        {
                            iniContent = "luajit.ext.enabled = 1" + iniNewline + iniContent;
                            iniModified = true;
                        }
                    }

                    // Lista de modulos requeridos en el orden exacto del motor
                    var reqList = new List<string> {
                        "fl26edit.lua",
                        "fl26caps.lua",
                        "fl26appcap.lua",
                        "fl26regs600.lua",
                        "fl26slots250.lua",
                        "fl26slot7.lua",
                        "fl26guards.lua",
                        "fl26nullguard12.lua",
                        "fl26nullguard13.lua",
                        "fl26boardguard.lua",
                        "fl26ctlguard.lua",
                        "fl26kitguard.lua",
                        "fl26resultsguard.lua",
                        "fl26superguard.lua",
                        "fl26seasonend.lua",
                        "fl26augseason.lua",
                        "fl26regen.lua",
                        "fl26regfast.lua",
                        "fl26walk.lua",
                        "fl26compcache.lua",
                        "fl26joindll.lua",
                        "fl26chain.lua"
                    };
                    if (enableSwiss)
                    {
                        reqList.Add("fl26swiss.lua");
                    }
                    reqList.AddRange(new[] {
                        "fl26rank.lua",
                        "fl26deeprank.lua",
                        "fl26reg128.lua",
                        "fl26catlist.lua",
                        "fl26slotnames.lua",
                        "fl26clubs.lua",
                        "league_manager.lua",
                        "fl26editlist.lua"
                    });
                    var requiredModules = reqList.ToArray();
                    var lines = iniContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

                    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int lastModuleIndex = -1;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var m = Regex.Match(lines[i], @"^\s*lua\.module\s*=\s*""([^""]+)""");
                        if (m.Success)
                        {
                            existing.Add(m.Groups[1].Value);
                            lastModuleIndex = i;
                        }
                    }

                    var toAdd = new List<string>();
                    foreach (var mod in requiredModules)
                    {
                        if (!existing.Contains(mod))
                        {
                            toAdd.Add(string.Format("lua.module = \"{0}\"", mod));
                        }
                    }

                    if (toAdd.Count > 0)
                    {
                        if (lastModuleIndex >= 0)
                        {
                            lines.InsertRange(lastModuleIndex + 1, toAdd);
                        }
                        else
                        {
                            lines.AddRange(toAdd);
                        }
                        iniModified = true;
                    }

                    if (iniModified)
                    {
                        try { File.Copy(iniPath, iniPath + ".bak", true); } catch { }
                        // Sin BOM (UTF8Encoding(false)) y con los finales originales.
                        File.WriteAllText(iniPath, string.Join(iniNewline, lines.ToArray()), new UTF8Encoding(false));
                        siderIniWritten = true;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine(missingFiles.Count > 0 ? "Despliegue de Sider incompleto en:" : "Ecosistema de Sider configurado en:");
                sb.AppendLine(siderDir);
                sb.AppendLine(string.Format("\nArchivos y módulos desplegados en sider\\modules ({0}):", deployedFiles.Count));
                foreach (var f in deployedFiles)
                {
                    sb.AppendLine("  ✔ " + f);
                }
                if (missingFiles.Count > 0)
                {
                    sb.AppendLine(string.Format("\n⚠️ Archivos omitidos o no encontrados en la distribución ({0}):", missingFiles.Count));
                    foreach (var f in missingFiles)
                    {
                        sb.AppendLine("  ✖ " + f);
                    }
                }
                sb.AppendLine("\nDetalle de funciones nativas:");
                if (deployedFiles.Contains("fl26edit.dll") && deployedFiles.Contains("fl26edit.lua")) sb.AppendLine("  • fl26edit.dll & fl26edit.lua (Persistencia Modo Editar: expansión a 2.048 clubes y .fl26x)");
                if (deployedFiles.Contains("fl26editlist.lua")) sb.AppendLine("  ✔ fl26editlist.lua (Inyección de ligas personalizadas en menús del Modo Editar)");
                if (deployedFiles.Contains("fl26caps.lua")) sb.AppendLine("  ✔ fl26caps.lua (Expansion de memoria: 2.400 clubes, 8.000 fixtures, fechas)");
                if (deployedFiles.Contains("fl26appcap.lua")) sb.AppendLine("  ✔ fl26appcap.lua (Expansión de tabla de apariencias de 30.024 a 65.000 registros)");
                if (deployedFiles.Contains("fl26regen.lua") && deployedFiles.Contains("fl26regen.dll")) sb.AppendLine("  ✔ fl26regen.dll & fl26regen.lua (Regens auténticos con nuevos nombres, rostros genéricos y ratings equilibrados)");
                if (deployedFiles.Contains("fl26clubs.lua") && deployedFiles.Contains("fl26clubs.dll")) sb.AppendLine("  ✔ fl26clubs.dll & fl26clubs.lua (Aislamiento de slots corruptos en Select Team y blindaje de CPU transfer AI)");
                if (deployedFiles.Contains("fl26joindll.lua") && deployedFiles.Contains("fl26join.dll")) sb.AppendLine("  ✔ fl26joindll.lua & fl26join.dll (Asignacion de 38 fechas y rollover anual en Liga Master)");
                if (deployedFiles.Contains("fl26chain.lua") && deployedFiles.Contains("fl26chain.dll")) sb.AppendLine("  ✔ fl26chain.lua & fl26chain.dll (Ascensos y descensos multidivision automaticos D1 <-> D2 <-> D3, hasta 32 cadenas)");
                if (deployedFiles.Contains("fl26swiss.lua") && deployedFiles.Contains("fl26swiss.dll")) sb.AppendLine("  ✔ fl26swiss.dll & fl26swiss.lua (Formato Suizo UEFA: 36 clubes, 8 fechas y fase de liga única)");
                if (deployedFiles.Contains("fl26rank.lua") && deployedFiles.Contains("fl26deeprank.lua")) sb.AppendLine("  ✔ fl26rank.lua & fl26deeprank.lua (Desbloqueo de 7 divisiones y compuertas de descenso)");
                if (deployedFiles.Contains("fl26world.txt")) sb.AppendLine("  ✔ fl26world.txt (Descriptor de cadenas y fixtures de Liga Master)");
                if (deployedFiles.Contains("fl26guards.lua")) sb.AppendLine("  ✔ fl26guards.lua (11 Ganchos Anti-Cuelgues Null-Guards)");
                if (deployedFiles.Contains("fl26nullguard12.lua")) sb.AppendLine("  ✔ fl26nullguard12.lua (Null-Guard 12: Anti-crash al actualizar fatiga/moral en Liga Master)");
                if (deployedFiles.Contains("fl26nullguard13.lua")) sb.AppendLine("  ✔ fl26nullguard13.lua (Null-Guard 13: Anti-crash en selección de copa amistosa de pretemporada)");
                if (deployedFiles.Contains("fl26boardguard.lua")) sb.AppendLine("  ✔ fl26boardguard.lua (Board-Guard: Anti-crash en objetivos de directiva para clubes nuevos en Liga Master)");
                if (deployedFiles.Contains("fl26ctlguard.lua")) sb.AppendLine("  ✔ fl26ctlguard.lua (Controller-Guard: Restablece club anterior al ser despedido o cambiar de equipo en ML/BAL)");
                if (deployedFiles.Contains("fl26kitguard.lua")) sb.AppendLine("  ✔ fl26kitguard.lua (Kit-Guard: Fallback neutral gris en Kit Server para evitar crash en selección de arquero)");
                if (deployedFiles.Contains("fl26resultsguard.lua")) sb.AppendLine("  ✔ fl26resultsguard.lua (Results-Guard: Previene crash por lista vacía de resultados de jornada al simular partidos)");
                if (deployedFiles.Contains("fl26superguard.lua")) sb.AppendLine("  ✔ fl26superguard.lua (Super-Guard: Previene crash en Supercopa cuando falta un clasificado continental)");
                if (deployedFiles.Contains("fl26slots250.lua")) sb.AppendLine("  ✔ fl26slots250.lua (Expansion a 250 Slots de Competicion)");
                if (deployedFiles.Contains("fl26slot7.lua")) sb.AppendLine("  ✔ fl26slot7.lua (Ranuras de Club a 8 bits 0..249)");
                if (deployedFiles.Contains("fl26reg128.lua")) sb.AppendLine("  ✔ fl26reg128.lua (Expansion de Paises / Regiones a 128)");
                if (deployedFiles.Contains("fl26regfast.lua")) sb.AppendLine("  ✔ fl26regfast.lua (Acelerador Liga Master: -65% tiempo de carga)");
                if (deployedFiles.Contains("fl26regs600.lua")) sb.AppendLine("  ✔ fl26regs600.lua (Expansion del recuento de regulaciones a 600)");
                if (deployedFiles.Contains("fl26compcache.dll") && deployedFiles.Contains("fl26compcache.lua")) sb.AppendLine("  ✔ fl26compcache.dll & fl26compcache.lua (Anti-congelamiento del 31 de enero)");
                if (deployedFiles.Contains("league_manager.lua")) sb.AppendLine(string.Format("  ✔ league_manager.lua ({0} ligas registradas con ascensos y banderas)", customLeagues != null ? customLeagues.Count : 0));
                if (siderIniWritten)
                {
                    sb.AppendLine("\nConfiguracion actualizada en sider.ini:");
                    sb.AppendLine("  ✔ luajit.ext.enabled = 1 (habilitado para asignacion FFI)");
                    sb.AppendLine("  ✔ Módulos FL26 configurados en orden exacto en la lista [modules]");
                }
                else if (siderIniExists)
                {
                    sb.AppendLine("\nConfiguracion en sider.ini: ya contenia la configuracion requerida; no fue necesario modificarla.");
                }
                else
                {
                    sb.AppendLine("\nAVISO: sider.ini no encontrado en la carpeta de Sider: no se modifico ninguna configuracion en sider.ini.");
                }

                resultMessage = sb.ToString();
                return missingFiles.Count == 0;
            }
            catch (Exception ex)
            {
                resultMessage = "Error al desplegar módulos en Sider:\n" + ex.Message;
                return false;
            }
        }

private static string GetEngineLuaTemplate()
        {
            return @"-- NULLGUARDS_PATCHES
local NULLGUARDS_PATCHES = {
    { va = 0x14252e800, old = ""0000000000000000000000000000000000000000000000"", new = ""4885c9740f48895c2418574883ec40e9a6bdabff31c0c3"", why = ""null-guard trampoline: test rcx,rcx; jz null_ret; <relocated prologue>; jmp 0x141fea5ba; null_ret: xor eax,eax; ret"" },
    { va = 0x141fea5b0, old = ""48895c2418574883ec40"", new = ""e94b4254009090909090"", why = ""redirect fn 0x141fea5b0 prologue (10 bytes) to trampoline at 0x14252e68c (jmp rel32 + 5 nop)"" },
    { va = 0x14252e880, old = ""0000000000000000000000000000000000000000"", new = ""4885c00f843daba9fe39a8c0030000e9aaa9a9fe"", why = ""null-guard trampoline: test rax,rax; jz 0x140fc93c6; cmp [rax+0x3c0],ebp; jmp 0x140fc923e"" },
    { va = 0x140fc9238, old = ""39a8c0030000"", new = ""e94356560190"", why = ""redirect the unchecked deref at 0x140fc9238 to the trampoline (jmp rel32 + nop)"" },
    { va = 0x14252e840, old = ""000000000000000000000000000000000000"", new = ""4885d2740a48895c2420e9863804ff31c0c3"", why = ""null-guard trampoline: test rdx,rdx; jz null_ret; mov [rsp+0x20],rbx; jmp 0x1415720d5; null_ret: xor eax,eax; ret"" },
    { va = 0x1415720d0, old = ""48895c2420"", new = ""e96bc7fb00"", why = ""redirect fn 0x1415720d0 prologue (5 bytes) to the trampoline at 0x14252e840 (jmp rel32)"" },
    { va = 0x14252e860, old = ""0000000000000000000000000000000000000000000000"", new = ""4885c90f846f84f9fe83fa6b0f876684f9fee9527ef9fe"", why = ""null-guard trampoline: test rcx,rcx; je 0x1414c6cd8; cmp edx,0x6b; ja 0x1414c6cd8; jmp 0x1414c66c9"" },
    { va = 0x1414c66c0, old = ""83fa6b0f870f060000"", new = ""e99b81060190909090"", why = ""redirect fn 0x1414c66c0 entry (9 bytes: cmp+ja) to the trampoline at 0x14252e860 (jmp rel32 + 4 nop)"" },
    { va = 0x14252e8a0, old = ""000000000000000000000000000000000000000000000000000000000000"", new = ""488b45b0448bc64885c07409448b4804e9f9bad5fe8b5c2440e953bbd5fe"", why = ""squad-table null-guard trampoline: mov rax,[rbp-0x50]; mov r8d,esi; test rax,rax;  jz empty; mov r9d,[rax+4]; jmp 0x14128a3ae; empty: mov ebx,[rsp+0x40]; jmp 0x14128a411"" },
    { va = 0x14128a3a3, old = ""488b45b0448bc6448b4804"", new = ""e9f8442a01909090909090"", why = ""redirect the unchecked first-element load at 0x14128a3a3 to the trampoline (jmp rel32 + 6 nop)"" },
    { va = 0x14252e8c0, old = ""00000000000000000000000000000000"", new = ""8b457785c07c04412b0c80e9184edffe"", why = ""standings-position guard trampoline: mov eax,[rbp+0x77]; test eax,eax;  jl skip; sub ecx,[r8+rax*4]; skip: jmp 0x1413236e8"" },
    { va = 0x1413236e1, old = ""8b4577412b0c80"", new = ""e9dab120019090"", why = ""redirect the unchecked position index at 0x1413236e1 to the trampoline (jmp rel32 + 2 nop)"" },
    { va = 0x14252e8e0, old = ""0000000000000000000000000000000000000000000000000000000000000000"", new = ""8b542440488b4c24484885c974090fb71491e92e817afee9b1857afe00000000"", why = ""empty-schedule guard trampoline: mov edx,[rsp+0x40]; mov rcx,[rsp+0x48];  test rcx,rcx; je out; movzx edx,word [rcx+rdx*4]; jmp 0x140cd6a25;  out: jmp 0x140cd6ead"" },
    { va = 0x140cd6a18, old = ""8b542440488b4c24480fb71491"", new = ""e9c37e85019090909090909090"", why = ""redirect the unchecked schedule read at 0x140cd6a18 to the trampoline  (jmp rel32 + 8 nop)"" },
    { va = 0x140cafc35, old = ""e986feffff"", new = ""e929000000"", why = ""league-link walk, plain step: jmp 0x140cafac0 -> jmp 0x140cafc63 (0xffff ends the walk)"" },
    { va = 0x140cafdac, old = ""e914fdffff"", new = ""e9b7feffff"", why = ""league-link walk, dated step: jmp 0x140cafac5 -> jmp 0x140cafc68 (0xffff ends the walk)"" },
    { va = 0x140f3258e, old = ""0f10442420660f73d80c660f7ec1f7c1d0000000760a81884801000000000200f6c150760a81884801000000000400f6c120760ff6c108760a81884801000000000400"", new = ""8b4c242c4885c0743af6c1d0740a81884801000000000200f6c150740a81884801000000000400f6c1207417f6c1087412818848010000000004009090909090909090"", why = ""new Master League squad flags: skip a record whose player lookup returned 0 (was: or dword [rax+0x148] with rax=0)"" },
}

-- SLOTS250_PATCHES
local SLOTS250_PATCHES = {
    { va = 0x163400800, old = ""00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"", new = ""00000000ee020000dc050000ca080000b80b0000a60e000094110000d81200001c14000060150000a4160000e81700002c190000701a0000b41b0000f81c00003c1e0000801f0000c4200000082200004c23000090240000d4250000182700005c280000a0290000e42a0000282c00006c2d0000b02e0000f42f0000383100007c320000c033000004350000483600008c370000d0380000143a0000583b00009c3c0000e03d0000243f000068400000ac410000f04200003444000078450000bc4600000048000044490000884a0000cc4b0000104d0000544e0000984f0000dc5000002052000064530000a8540000ec5500003057000074580000b8590000fc5a0000405c0000845d0000c85e00000c600000506100003e6400002c6700001a6a0000086d0000f66f0000e4720000d2750000c0780000ae7b00009c7e0000c4880000ec9200003094000074950000b8960000fc97000040990000849a0000c89b00000c9d0000509e0000949f0000d8a000001ca2000060a30000a4a40000e8a500002ca7000070a80000b4a90000f8aa00003cac000080ad0000c4ae000008b000004cb1000090b20000d4b3000018b500005cb60000a0b70000e4b8000028ba00006cbb0000b0bc0000f4bd000038bf00007cc00000c0c1000004c3000048c400008cc50000d0c6000014c8000014c8000058c900009cca0000e0cb000024cd000068ce0000accf0000f0d0000034d2000078d30000bcd4000000d6000044d7000088d80000ccd9000010db000054dc000098dd0000dcde000020e0000064e10000a8e20000ece3000030e5000074e60000b8e70000fce8000040ea000084eb0000c8ec00000cee000050ef000094f00000d8f100001cf3000060f40000a4f50000e8f600002cf8000070f90000b4fa0000f8fb00003cfd000080fe0000c4ff0000080101004c02010090030100d4040100180601005c070100a0080100e4090100280b01006c0c0100b00d0100f40e0100381001007c110100c012010004140100481501008c160100d017010014190100581a01009c1b0100e01c0100241e0100681f0100ac200100f02101003423010078240100bc250100002701004428010088290100cc2a0100102c0100542d0100982e0100dc2f01002031010064320100a8330100ec3401003036010074370100b8380100fc390100403b0100843c0100c83d01000c3f01005040010094410100d84201001c44010060450100a4460100e84701002c490100704a0100b44b0100f84c01003c4e0100804f0100c4500100085201004c53010090540100d4550100185701005c580100a0590100e45a0100285c01006c5d0100b05e0100f45f0100386101007c620100c06301000465010048660100"", why = ""first entry of each slot's list (dword index into the region)"" },
    { va = 0x163400c00, old = ""00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"", new = ""ee020000ee020000ee020000ee020000ee020000ee020000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000ee020000ee020000ee020000ee020000ee020000ee020000ee020000ee020000ee020000ee020000280a0000280a000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000000000000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000440100004401000044010000"", why = ""capacity of each slot (slot 123 = 0)"" },
    { va = 0x163400000, old = ""000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"", new = ""89d03dfa000000732f3c7b742b0fb79441749f0500443bc2731e488d15df0b0000443b04827311488d15d20700008b14824401c28b0491c383c8ffc3"", why = ""get accessor body, table driven"" },
    { va = 0x163400040, old = ""00000000000000000000000000000000000000000000000000000000000000000000000000000000000000"", new = ""31c081fafa000000732083fa7b741b5289d0488d15a70b00008b14820fb78441749f050039d00f47c25ac3"", why = ""count accessor body, table driven"" },
    { va = 0x163400070, old = ""0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"", new = ""89d03dfa00000073223c7b741e488d157c0b0000443b04827311488d156f0700008b14824401c244890c91c3"", why = ""set accessor body, table driven"" },
    { va = 0x1634000a0, old = ""00000000000000000000000000000000000000000000000000000000000000000000000000000000000000"", new = ""81fafa000000732283fa7b741d5289d0488d15490b00008b1482664139d077096644898441749f05005ac3"", why = ""setcount accessor body, table driven"" },
    { va = 0x1634000d0, old = ""00000000000000000000000000000000000000000000000000000000"", new = ""4183f87b0f845eb10cde4181f8fa0000000f8d51b10cdee90eb00cde"", why = ""stub for 0x1414cb0f0 -- guard: cmp r8d,123 -> rejected; cmp r8d,250; jge 0x1414cb238; jmp 0x1414cb0fa  [cid2slot: only real slots of the rows a competition id has]"" },
    { va = 0x1634000f0, old = ""0000000000000000000000000000000000000000000000000000"", new = ""83fe7b0f8408750fde81fefa0000000f8dfc740fdee94d740fde"", why = ""stub for 0x1414f754e -- guard: cmp esi,123 -> rejected; cmp esi,250; jge 0x1414f7601; jmp 0x1414f7557  [slot-list builder: slot of a competition entry (first test)]"" },
    { va = 0x163400110, old = ""0000000000000000000000000000000000000000000000000000"", new = ""83fe7b0f84eb740fde81fefa0000000f8ddf740fdee94d740fde"", why = ""stub for 0x1414f756e -- guard: cmp esi,123 -> rejected; cmp esi,250; jge 0x1414f7604; jmp 0x1414f7577  [slot-list builder: slot of a competition entry (second test)]"" },
    { va = 0x163400130, old = ""000000000000000000000000000000000000"", new = ""4181fefa0000000f83b3760fdee99e760fde"", why = ""stub for 0x1414f77da -- loop: cmp r14d,250; jae 0x1414f77f0; jmp 0x1414f77e0  [slot-list builder: write every slot vector into the region]"" },
    { va = 0x163400150, old = ""0000000000000000000000000000000000000000000000000000"", new = ""83f87b0f846488e6dd81f8fa0000000f8d5888e6dde97f87e6dd"", why = ""stub for 0x1412688e0 -- guard: cmp eax,123 -> rejected; cmp eax,250; jge 0x1412689bd; jmp 0x1412688e9  [regulation fill: slot of the competition]"" },
    { va = 0x163400170, old = ""0000000000000000000000000000000000000000000000000000"", new = ""83fd7b0f8415abefdd81fdfa0000000f824cabefdde904abefdd"", why = ""stub for 0x1412fac89 -- guard: cmp ebp,123 -> rejected; cmp ebp,250; jb 0x1412facd1; jmp 0x1412fac8e  [Select Team list: a real slot, else the special list at +0xf0]"" },
    { va = 0x163400190, old = ""00000000000000000000000000000000000000000000000000000000"", new = ""4183fc7b0f84bab0efdd4181fcfa0000000f82c1b0efdde9a8b0efdd"", why = ""stub for 0x1412fb24e -- guard: cmp r12d,123 -> rejected; cmp r12d,250; jb 0x1412fb268; jmp 0x1412fb254  [Select Team list: a real slot, else the special list]"" },
    { va = 0x1634001b0, old = ""0000000000000000000000000000000000"", new = ""81fefa0000000f8295b3efdde9edb4efdd"", why = ""stub for 0x1412fb6a5 -- loop: cmp esi,250; jb 0x1412fb551; jmp 0x1412fb6ae  [Select Team list: walk every slot]"" },
    { va = 0x1634001d0, old = ""000000000000000000000000000000000000"", new = ""4181fffa0000000f83813fe6dde9203fe6dd"", why = ""stub for 0x1412640fc -- loop: cmp r15d,250; jae 0x14126415e; jmp 0x141264102  [user club categories: walk every slot looking for the club]"" },
    { va = 0x1634001f0, old = ""0000000000000000000000000000000000000000000000000000"", new = ""b90700000083f87b74093d800000007d0289c189c8e9efaa6ce1"", why = ""team slot: slot < 128 and != 123 kept, otherwise 7 (7-bit field +0x41c)"" },
    { va = 0x1414d6290, old = ""83fa7b7320"", new = ""e96b9df221"", why = ""get accessor 0x1414d6290 -> its new body at 0x163400000 (entry address kept)"" },
    { va = 0x1414d62c0, old = ""33c083fa7b"", new = ""e97b9df221"", why = ""count accessor 0x1414d62c0 -> its new body at 0x163400040 (entry address kept)"" },
    { va = 0x1414d6330, old = ""83fa7b731c"", new = ""e93b9df221"", why = ""set accessor 0x1414d6330 -> its new body at 0x163400070 (entry address kept)"" },
    { va = 0x1414d6360, old = ""83fa7b7316"", new = ""e93b9df221"", why = ""setcount accessor 0x1414d6360 -> its new body at 0x1634000a0 (entry address kept)"" },
    { va = 0x1414cb0f0, old = ""4183f87b0f8d3e010000"", new = ""e9db4ff3219090909090"", why = ""cmp r8d, 0x7b / jge 0x1414cb238 -> stub 0x1634000d0  [cid2slot: only real slots of the rows a competition id has]"" },
    { va = 0x1414f754e, old = ""83fe7b0f8daa000000"", new = ""e99d8bf02190909090"", why = ""cmp esi, 0x7b / jge 0x1414f7601 -> stub 0x1634000f0  [slot-list builder: slot of a competition entry (first test)]"" },
    { va = 0x1414f756e, old = ""83fe7b0f8d8d000000"", new = ""e99d8bf02190909090"", why = ""cmp esi, 0x7b / jge 0x1414f7604 -> stub 0x163400110  [slot-list builder: slot of a competition entry (second test)]"" },
    { va = 0x1414f77da, old = ""4183fe7b7310"", new = ""e95189f02190"", why = ""cmp r14d, 0x7b / jae 0x1414f77f0 -> stub 0x163400130  [slot-list builder: write every slot vector into the region]"" },
    { va = 0x1412688e0, old = ""83f87b0f8dd4000000"", new = ""e96b78192290909090"", why = ""cmp eax, 0x7b / jge 0x1412689bd -> stub 0x163400150  [regulation fill: slot of the competition]"" },
    { va = 0x1412fac89, old = ""83fd7b7243"", new = ""e9e2541022"", why = ""cmp ebp, 0x7b / jb 0x1412facd1 -> stub 0x163400170  [Select Team list: a real slot, else the special list at +0xf0]"" },
    { va = 0x1412fb24e, old = ""4183fc7b7214"", new = ""e93d4f102290"", why = ""cmp r12d, 0x7b / jb 0x1412fb268 -> stub 0x163400190  [Select Team list: a real slot, else the special list]"" },
    { va = 0x1412fb6a5, old = ""83fe7b0f82a3feffff"", new = ""e9064b102290909090"", why = ""cmp esi, 0x7b / jb 0x1412fb551 -> stub 0x1634001b0  [Select Team list: walk every slot]"" },
    { va = 0x1412640fc, old = ""4183ff7b735c"", new = ""e9cfc0192290"", why = ""cmp r15d, 0x7b / jae 0x14126415e -> stub 0x1634001d0  [user club categories: walk every slot looking for the club]"" },
    { va = 0x144acacec, old = ""83f87bb9070000000f4cc889c8"", new = ""e9ff54931e9090909090909090"", why = "".impdata 0x144acace0: cmp eax,0x7b / mov ecx,7 / cmovl / mov eax,ecx -> stub 0x1634001f0"" },
    { va = 0x1414d62e5, old = ""7b000000"", new = ""01000000"", why = ""clear: one pass over all lists instead of 123 x 750"" },
    { va = 0x1414d62f2, old = ""ee020000"", new = ""dd670100"", why = ""clear: 92125 list dwords"" },
    { va = 0x1414d631e, old = ""68a10500"", new = ""749f0500"", why = ""clear: counts at +0x59f74"" },
    { va = 0x1414d6326, old = ""f6000000"", new = ""f4010000"", why = ""clear: 250 counts"" },
    { va = 0x1414f6e49, old = ""7b000000"", new = ""fa000000"", why = ""slot-list builder 0x1414f6e20: 250 slot vectors (mov edx,0x7b)"" },
}

-- SLOT7_PATCHES
local SLOT7_PATCHES = {
    { va = 0x163c00000, old = ""000000000000000000000000000000000000000000000000000000000000000000000000000000"", new = ""4883ec280fb709e8d4b28cdd83f87b740c3dfa00000073054883c428c3b8070000004883c428c3"", why = ""league slot of a competition id: id2slot, 7 only for 123 or >= 250 (0x141509c00 caps at 128)"" },
    { va = 0x140b08f74, old = ""7f"", new = ""ff"", why = ""slot reader 0x140b08f6b: and cl, 0x7f -> 0xff"" },
    { va = 0x140c6c0c0, old = ""7f"", new = ""ff"", why = ""slot reader 0x140c6c0b7: and cl, 0x7f -> 0xff"" },
    { va = 0x140fa3077, old = ""7f"", new = ""ff"", why = ""slot reader 0x140fa306f: and al, 0x7f -> 0xff"" },
    { va = 0x140fd2a7f, old = ""7f"", new = ""ff"", why = ""slot reader 0x140fd2a77: and al, 0x7f -> 0xff"" },
    { va = 0x140fd8eae, old = ""7f"", new = ""ff"", why = ""slot reader 0x140fd8ea6: and al, 0x7f -> 0xff"" },
    { va = 0x141262384, old = ""7f"", new = ""ff"", why = ""slot reader 0x141262379: and r15d, 0x7f -> 0xff"" },
    { va = 0x1412628f4, old = ""7f"", new = ""ff"", why = ""slot reader 0x1412628ec: and al, 0x7f -> 0xff"" },
    { va = 0x141262b84, old = ""7f"", new = ""ff"", why = ""slot reader 0x141262b7c: and al, 0x7f -> 0xff"" },
    { va = 0x141324df5, old = ""7f"", new = ""ff"", why = ""slot reader 0x141324ded: and al, 0x7f -> 0xff"" },
    { va = 0x14132a045, old = ""7f"", new = ""ff"", why = ""slot reader 0x14132a03d: and al, 0x7f -> 0xff"" },
    { va = 0x14133396d, old = ""7f"", new = ""ff"", why = ""slot reader 0x141333964: and eax, 0x7f -> 0xff"" },
    { va = 0x141354dd1, old = ""7f"", new = ""ff"", why = ""slot reader 0x141354dc9: and al, 0x7f -> 0xff"" },
    { va = 0x1413558d6, old = ""7f"", new = ""ff"", why = ""slot reader 0x1413558cd: and eax, 0x7f -> 0xff"" },
    { va = 0x141358f7d, old = ""7f"", new = ""ff"", why = ""slot reader 0x141358f74: and cl, 0x7f -> 0xff"" },
    { va = 0x14135a8cd, old = ""7f"", new = ""ff"", why = ""slot reader 0x14135a8c5: and al, 0x7f -> 0xff"" },
    { va = 0x14135ae11, old = ""7f"", new = ""ff"", why = ""slot reader 0x14135ae09: and al, 0x7f -> 0xff"" },
    { va = 0x14135b17d, old = ""7f"", new = ""ff"", why = ""slot reader 0x14135b175: and al, 0x7f -> 0xff"" },
    { va = 0x14135d790, old = ""7f"", new = ""ff"", why = ""slot reader 0x14135d787: and ecx, 0x7f -> 0xff"" },
    { va = 0x14136037d, old = ""7f"", new = ""ff"", why = ""slot reader 0x141360372: and r15b, 0x7f -> 0xff"" },
    { va = 0x1413603f4, old = ""7f"", new = ""ff"", why = ""slot reader 0x1413603eb: and cl, 0x7f -> 0xff"" },
    { va = 0x141361121, old = ""7f"", new = ""ff"", why = ""slot reader 0x141361119: and al, 0x7f -> 0xff"" },
    { va = 0x141365911, old = ""7f"", new = ""ff"", why = ""slot reader 0x141365906: and r15b, 0x7f -> 0xff"" },
    { va = 0x14136593a, old = ""7f"", new = ""ff"", why = ""slot reader 0x141365930: and dil, 0x7f -> 0xff"" },
    { va = 0x141509575, old = ""7f"", new = ""ff"", why = ""slot reader 0x14150956c: and ebp, 0x7f -> 0xff"" },
    { va = 0x14150d4ee, old = ""7f"", new = ""ff"", why = ""slot reader 0x14150d4e5: and edx, 0x7f -> 0xff"" },
    { va = 0x14150d528, old = ""7f"", new = ""ff"", why = ""slot reader 0x14150d51f: and edx, 0x7f -> 0xff"" },
    { va = 0x141511253, old = ""7f"", new = ""ff"", why = ""slot reader 0x141511243: and ecx, 0x7f -> 0xff"" },
    { va = 0x1415191dc, old = ""7f"", new = ""ff"", why = ""slot reader 0x1415191d3: and eax, 0x7f -> 0xff"" },
    { va = 0x141527ebf, old = ""7f"", new = ""ff"", why = ""slot reader 0x141527eb7: and al, 0x7f -> 0xff"" },
    { va = 0x141541f6b, old = ""7f"", new = ""ff"", why = ""slot reader 0x141541f62: and ecx, 0x7f -> 0xff"" },
    { va = 0x141546a12, old = ""7f"", new = ""ff"", why = ""slot reader 0x141546a09: and ecx, 0x7f -> 0xff"" },
    { va = 0x141547ff2, old = ""7f"", new = ""ff"", why = ""slot reader 0x141547fe9: and ecx, 0x7f -> 0xff"" },
    { va = 0x141262760, old = ""4183a51c040000800fb6c083e07f4109851c040000"", new = ""4188851c0400009090909090909090909090909090"", why = ""user's club: slot from 0x141509c00 -> byte +0x41c (flag in +0x41a untouched, as and 0xffffff80 kept bit 7)"" },
    { va = 0x1412627df, old = ""4183a4241c040000800fb645ef83e07f410984241c040000"", new = ""0fb645ef418884241c040000909090909090909090909090"", why = ""user's club path: other club gets the saved slot [rbp-0x11] -> byte +0x41c"" },
    { va = 0x1412628f5, old = ""4183a51c040000800fb6c04109851c040000"", new = ""4188851c0400009090909090909090909090"", why = ""user's club takes another club's slot (al) -> byte +0x41c"" },
    { va = 0x141262930, old = ""418b8704030000c1e807a80175060fbaf207eb040fbaea074189951c040000"", new = ""418b8704030000c1e80724014188851a040000909090909090909090909090"", why = ""flag of the regulation (+0x304 bit 7) -> +0x41a instead of bit 7 of +0x41c"" },
    { va = 0x14126294f, old = ""83a11c040000800fb645ef83e07f09811c040000"", new = ""0fb645ef88811c04000090909090909090909090"", why = ""other club gets the saved slot [rbp-0x11] -> byte +0x41c"" },
    { va = 0x141262b85, old = ""4183a51c040000800fb6c04109851c040000"", new = ""4188851c0400009090909090909090909090"", why = ""user's club takes another club's slot (al) -> byte +0x41c"" },
    { va = 0x141262bc0, old = ""8b8604030000c1e807a80175060fbaf207eb040fbaea074189951c040000"", new = ""8b8604030000c1e80724014188851a040000909090909090909090909090"", why = ""flag of the regulation (+0x304 bit 7) -> +0x41a instead of bit 7 of +0x41c"" },
    { va = 0x141262bde, old = ""83a11c04000080410fb6c783e07f09811c040000"", new = ""410fb6c788811c04000090909090909090909090"", why = ""other club gets slot r15b -> byte +0x41c"" },
    { va = 0x141262e90, old = ""0fb6c841338d1c04000083e17f41338d1c04000041898d1c040000418b8704030000c1e807a80175060fbaf107eb040fbae90741898d1c040000"", new = ""4188851c040000418b8704030000c1e80724014188851a0400009090909090909090909090909090909090909090909090909090909090909090"", why = ""user's club: slot (al) -> byte +0x41c, regulation flag -> +0x41a"" },
    { va = 0x141262f77, old = ""83a01c040000800fb645ef83e07f09811c040000"", new = ""0fb645ef88811c04000090909090909090909090"", why = ""other club (rax == rcx) gets the saved slot [rbp-0x11] -> byte +0x41c"" },
    { va = 0x14136595f, old = ""83a01c04000080400fb6cf09881c040000"", new = ""4088b81c04000090909090909090909090"", why = ""season mover: club takes slot dil -> byte +0x41c"" },
    { va = 0x14136599f, old = ""83a01c04000080410fb6cf09881c040000"", new = ""4488b81c04000090909090909090909090"", why = ""season mover: club takes slot r15b -> byte +0x41c"" },
    { va = 0x14126655e, old = ""8b856c03000083e0fb83c87b"", new = ""8b856c030000b07b90909090"", why = ""staged world: no slot -> byte = 123 (was low 7 bits = 123, bit 7 kept)"" },
    { va = 0x1412665cb, old = ""0fb6c88b856c03000033c883e17f33c1"", new = ""8b8d6c03000088c189c8909090909090"", why = ""staged world: slot from 0x141263b40 -> whole byte +0x41c of the staged copy"" },
    { va = 0x1414c5b41, old = ""818b1c04000080000000"", new = ""c6831a04000001909090"", why = ""team init: flag = 1 now in +0x41a (was or [+0x41c],0x80)"" },
    { va = 0x141f12004, old = ""81a61c0400007fffffff098e1c040000"", new = ""c1e907888e1a04000090909090909090"", why = ""edit field setter: flag (ecx = 0x80 or 0) -> +0x41a bit 0"" },
    { va = 0x141eddb4f, old = ""8b801c040000c1e8072401"", new = ""0fb6801a04000024019090"", why = ""flag reader: +0x41a bit 0"" },
    { va = 0x141f116af, old = ""8b851c040000458bc6c1e8072401"", new = ""0fb6851a040000458bc624019090"", why = ""edit field getter: flag from +0x41a bit 0"" },
    { va = 0x140b15aa9, old = ""0fb78718040000"", new = ""8b871804000090"", why = ""team copy: country word +0x418 copied as a dword, so +0x41a (flag) travels with it"" },
    { va = 0x140b15ab0, old = ""66898318040000"", new = ""89831804000090"", why = ""team copy: store the dword +0x418..+0x41b"" },
    { va = 0x140ae3de8, old = ""e8135ea200"", new = ""e813c21123"", why = ""call 0x141509c00 -> our slot function 0x163c00000"" },
    { va = 0x140ae44d3, old = ""e82857a200"", new = ""e828bb1123"", why = ""call 0x141509c00 -> our slot function 0x163c00000"" },
    { va = 0x14126275b, old = ""e8a0742a00"", new = ""e8a0d89922"", why = ""call 0x141509c00 -> our slot function 0x163c00000"" },
    { va = 0x141262e8b, old = ""e8706d2a00"", new = ""e870d19922"", why = ""call 0x141509c00 -> our slot function 0x163c00000"" },
    { va = 0x14135964b, old = ""e8b0051b00"", new = ""e8b0698a22"", why = ""call 0x141509c00 -> our slot function 0x163c00000"" },
    { va = 0x14135a50b, old = ""e8f0f61a00"", new = ""e8f05a8a22"", why = ""call 0x141509c00 -> our slot function 0x163c00000"" },
}

-- REG128_PATCHES (44 parches: expansion de paises/regiones a 128)
local REG128_PATCHES = {
    { va = 0x140acce1f, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): LeagueSelectCategory list (0x140accce0) -> 0x1414cf160"" },
    { va = 0x140acd68f, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): personalDataSelect (0x140acd560) -> 0x1414cf160"" },
    { va = 0x140b6a856, old = ""8b830c030000c1e80783e03f"", new = ""0fb683fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): menu: region == r12d"" },
    { va = 0x140b81ba3, old = ""8b800c030000c1e80783e03f"", new = ""0fb680fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): menu object +0x64c (later 0x140ead9c0 lookup / == +0x5ac)"" },
    { va = 0x140b82750, old = ""8b800c030000c1e80783e03f"", new = ""0fb680fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): menu object +0x5ac (0x140ead9c0 lookup / == +0x64c)"" },
    { va = 0x140c6bdac, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): Select Team headings (0x140c6bd60) -> 0x1414cf160"" },
    { va = 0x140c6cd88, old = ""8b900c030000c1ea0783e23f"", new = ""0fb690fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): League Info icons 0x140c6ccb0: the league's own region (edx, read by fl26catlist's stub)"" },
    { va = 0x140c6ce00, old = ""8b830c030000c1e80783e03f"", new = ""0fb683fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): League Info icons 0x140c6ccb0: a regulation's region == the league's"" },
    { va = 0x140eada89, old = ""8b890c030000c1e90783e13f"", new = ""0fb689fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): Competition Info list 0x140ead9f0: region == the list entry"" },
    { va = 0x140eadb73, old = ""448b8e0c03000033d2448b970c03000041c1e90741c1ea074183e13f4183e23f"", new = ""440fb68efe02000033d2440fb697fe0200000f1f840000000000660f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): Competition Info sort helper 0x140eadae0: both regions, the xor edx,edx between them kept"" },
    { va = 0x140f1f641, old = ""8b800c030000c1e80783e03f"", new = ""0fb680fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): menu: region == r15d"" },
    { va = 0x140fa1bb1, old = ""8b8b0c030000c1e90783e13f"", new = ""0fb68bfe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): menu: region looked up in a list"" },
    { va = 0x14126464c, old = ""8b870c03000025801f0000"", new = ""0fb687fe02000085c06690"", why = ""career list 0x1412645b0: region 0 test reads the full region (movzx + test eax,eax, je skip unchanged)"" },
    { va = 0x141264659, old = ""3d800e0000"", new = ""3dffffffff"", alt = ""3d01000000"", why = ""career list 0x1412645b0: the region-29 skip never fires (a byte never equals -1); fl26reg64 had cmp eax,1, which with the full region would now skip region 1"" },
    { va = 0x141264a5f, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): career list 0x141264880 -> 0x1414ce270"" },
    { va = 0x141264b51, old = ""41bc01000026"", new = ""41bc01000006"", alt = ""41bc01000006"", why = ""career list 0x141264880, other branch: the skip mask loses bit 29 (regions 0, 25, 26 stay; as fl26reg64)"" },
    { va = 0x141264b7d, old = ""8b860c030000c1e80783e03f"", new = ""0fb686fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): career list 0x141264880, other branch (cmp 0x1d / bt mask after it)"" },
    { va = 0x1414c94af, old = ""c683fe020000ff66898bff020000"", new = ""c783fd020000ff0000ff0f1f4000"", why = ""record constructor 0x1414c93b0: +0x2fe and +0x2ff = 0 instead of 0xffff (one dword store to +0x2fd..+0x300 = ff 00 00 ff keeps +0x2fd and +0x300 at 0xff as before)"" },
    { va = 0x1414c99c8, old = ""c783fc020000ffffffff"", new = ""c783fc020000ffff0000"", why = ""record re-init 0x1414c98f0: +0x2fc = 0xffff, +0x2fe = 0 (region 0, as the memset left +0x30c)"" },
    { va = 0x1414c9c10, old = ""81a10c0300007fe0ffff83e23fc1e20709910c030000c3cccccccccccccccccc"", new = ""488d8100030000668950fe80fa3f7602b23fc1e2076681600c7fe06609500cc3"", why = ""region setter (one caller, the parser): full region -> byte +0x2fe (word store, +0x2ff = 0 until the parser marks it), min(region,63) -> +0x30c bits 7..12 (63 = 'not a 6-bit region'); rewritten in its own 32 bytes (body + int3 padding), no cave"" },
    { va = 0x1414cce96, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' -> 0x1414ce270"" },
    { va = 0x1414cdb5d, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' -> 0x1414ce270"" },
    { va = 0x1414ce2d4, old = ""8b810c030000c1e80783e03f"", new = ""0fb681fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): 0x1414ce270 (region -> the competitions of that region): region == wanted"" },
    { va = 0x1414ced48, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' -> 0x1414ce270"" },
    { va = 0x1414cee9d, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' -> 0x1414ce270"" },
    { va = 0x1414f8390, old = ""8b08c1e91b0fb6c1241f3c1d7304440fb6f1"", new = ""8b088bc1c1e91bc1e81383e0600bc8448bf1"", alt = ""8b088bc1c1e91bc1e81383e0200bc8448bf1"", why = ""parser: region = Competition.bin +3 bits 3..7 (low five) | bit 0 -> 5 | bit 1 -> 6, 0..127; every row with bits 0..1 clear decodes as before"" },
    { va = 0x1414f83d1, old = ""740b0fb7d1488bcfe8521bfdff"", new = ""c687ff0200005a660f1f440000"", why = ""parser: the call that copied Competition.bin d0 bits 18..26 into +0x2fe (0x1414c9f30) becomes mov byte [rdi+0x2ff],0x5a -- the mark that says +0x2fe holds a region (old careers lack it)"" },
    { va = 0x14150c9d2, old = ""8b860c03000025801f00003d80000000"", new = ""0fb686fe02000083f801660f1f440000"", why = ""region == 1 (interClub) test: full region compared with 1 (was and 0x1f80 / cmp 0x80)"" },
    { va = 0x14151c95e, old = ""8b860c03000081e3ff0f000025801f00003d80000000"", new = ""0fb686fe02000081e3ff0f000083f801660f1f440000"", why = ""region == 1 (interClub) test: full region compared with 1 (and ebx,0xfff kept)"" },
    { va = 0x141541288, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' -> 0x1414ce270"" },
    { va = 0x141541639, old = ""8b800c030000c1e80783e03f"", new = ""0fb680fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): the getter 0x1415415f0(comp) -> region, 63 callers"" },
    { va = 0x141542dd8, old = ""448bb00c03000041c1ee074183e63f"", new = ""440fb6b0fe0200000f1f8000000000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141542e6a, old = ""8bb80c030000c1ef0783e73f"", new = ""0fb6b8fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x1415430a8, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x1415432a8, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141543498, old = ""448ba00c03000041c1ec074183e43f"", new = ""440fb6a0fe0200000f1f8000000000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141543898, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141543e65, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141544ff8, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141545088, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141545528, old = ""8b880c030000c1e90783e13f"", new = ""0fb688fe0200000f1f440000"", why = ""full region (movzx byte +0x2fe, was +0x30c shr 7 / and 0x3f): inline 'region of competition' (0x14154xxxx block)"" },
    { va = 0x141575f1d, old = ""83f81d"", new = ""83f800"", why = ""0x141575ea0: region 29 is a region like any other (cmp eax,0x1d -> cmp eax,0; the test eax,eax / je after it already sends region 0 to the same place)"" },
    { va = 0x1415773af, old = ""83f81d"", new = ""83f800"", why = ""0x141577380: region 29 is a region like any other (cmp eax,0x1d -> cmp eax,0; the test eax,eax / je after it already sends region 0 to the same place)"" },
    { va = 0x141577414, old = ""83f81d"", new = ""83f800"", why = ""0x1415773e0: region 29 is a region like any other (cmp eax,0x1d -> cmp eax,0; the test eax,eax / je after it already sends region 0 to the same place)"" },
}


-- ============================================================================
-- CONSTANTES DE MEMORIA Y FIRMAS DE PES2021.EXE
-- ============================================================================
local BASE = 0x1434fdf00         -- Tabla estática de competiciones (.data, 165 filas x 0x108)
local STRIDE = 0x108             -- 264 bytes por fila
local NROWS = 165                -- 165 filas de fábrica (IDs hasta 177)
local TEMPLATE_ID = 61           -- ID 61 es una plantilla de liga limpia en el juego

local OFF_ID = 0x00
local OFF_SLOT = 0x04
local OFF_PROMOTE = 0x30
local OFF_DEMOTE = 0x34
local OFF_NAME = 0x38
local OFF_SHORT = 0x3c
local OFF_SPLIT_TEXT = 0x44
local OFF_INFO_TEXT = 0x48
local NO_LABEL = ""\255\255\255\255""

-- Sitios LEA a la tabla de competiciones
local LEAS = {
    { 0x1414fdbda, ""4c8d251f030002"" },   -- lea r12,[table]  (búsqueda por ID)
    { 0x1414fdd71, ""488d0588010002"" },   -- lea rax,[table]  (búsqueda por índice)
    { 0x1414fde07, ""4c8d35f2000002"" },   -- lea r14,[table]  (verificación de rango)
}

-- Contadores de cantidad de filas (165 = 0xA5)
local COUNT_SITES = {
    { 0x1414fdc4a, ""81fea5000000"", 2 },  -- cmp esi, 0xa5
    { 0x1414fdd60, ""81f9a5000000"", 2 },  -- cmp ecx, 0xa5
    { 0x1414fde1b, ""41bca5000000"", 2 },  -- mov r12d, 0xa5
    { 0x1414fe1e0, ""b8a5000000"",   1 },  -- mov eax, 0xa5  (función getter de cantidad)
}

-- Hook de bandera de país por slot
local SLOT_FN = 0x140c950d0
local SLOT_FN_PROLOGUE = ""48895c240855488d6c24a94881ecd0000000""   -- 18 bytes
local SLOT_FN_RESUME = 0x12
local NSLOTS = 250

-- Orden en menú Exhibición / Kick Off
local ORDER_FN = 0x140ead990
local ORDER_TBL = 0x1427d5920
local ORDER_N = 89
local ORDER_UNLISTED = 0x1427d5754
local ORDER_FN_BYTES = ""33c0488d15877f92010f1f8000000000390a7410ffc04883c20483f85972f1b859000000c3""
local ORDER_SITES = {
    { ORDER_FN, ORDER_FN_BYTES },
    { ORDER_UNLISTED, ""59000000"" },
    { 0x140c9359e, ""3b05b021b401"" }
}

-- Hook para listas del Modo Editar (Editar > Equipos / Jugadores)
local EDIT_HOOK = 0x141ee7086         -- mov rbx, [rsp+0x38] (epílogo de 0x141ee68f0)
local EDIT_HOOK_OLD = ""488b5c2438""
local EDIT_LIST0 = 0x141ee690d        -- mov dword [rbp+0x10], 7
local EDIT_LIST0_OLD = ""c7451007000000""
local EDIT_PUSH = 0x14047be40         -- push_back(vector*, const uint32_t*)
local EDIT_BACK = 0x141ee708b

-- ============================================================================
-- UTILIDADES DE CODIFICACION BINARIA Y MEMORIA
-- ============================================================================
local function u32le(v)
    v = v % 0x100000000
    return string.char(v % 256, math.floor(v / 256) % 256, math.floor(v / 65536) % 256, math.floor(v / 16777216) % 256)
end

local function u64le(v)
    return u32le(v % 0x100000000) .. u32le(math.floor(v / 0x100000000))
end

local function bin2hex(s)
    if s == nil then return """" end
    return (s:gsub(""."", function(c) return string.format(""%02x"", c:byte()) end))
end

local function hex2bin(h)
    return (h:gsub(""%x%x"", function(b) return string.char(tonumber(b, 16)) end))
end

local function rel32(from_next, to)
    local d = to - from_next
    if d >= 0x80000000 or d < -0x80000000 then return nil end
    if d < 0 then d = d + 0x100000000 end
    return u32le(d)
end

local function row_u32(row, off)
    return memory.unpack(""u32"", row:sub(off + 1, off + 4))
end

local function row_set(row, off, bytes)
    return row:sub(1, off) .. bytes .. row:sub(off + #bytes + 1)
end

local function check_sites(list)
    for _, s in ipairs(list) do
        local n = #s[2] / 2
        local got = bin2hex(memory.read(s[1], n))
        if got ~= s[2] then
            log(string.format(""[league_manager] Error: bytes en 0x%x son %s, se esperaba %s"", s[1], got, s[2]))
            return false
        end
    end
    return true
end

-- ============================================================================
-- GESTOR UNIVERSAL DE PARCHES Y RESERVA DE PAGINAS
-- ============================================================================
local function reserve_page(page, desc)
    if ffi == nil then
        log(""[league_manager] Error: FFI global es nil. Requiere luajit.ext.enabled = 1 en sider.ini"")
        return false
    end
    if not m.cdef_virtualalloc then
        ffi.cdef([[ void* fl26_VirtualAlloc(void*, size_t, uint32_t, uint32_t) asm(""VirtualAlloc""); ]])
        m.cdef_virtualalloc = true
    end
    local function try_alloc(kind)
        local p = ffi.C.fl26_VirtualAlloc(ffi.cast(""void*"", page), 0x1000, kind, 0x40) -- PAGE_EXECUTE_READWRITE
        return p ~= nil and tonumber(ffi.cast(""uint64_t"", p)) or 0
    end
    local got = try_alloc(0x3000)
    if got ~= page then got = try_alloc(0x1000) end
    if got ~= page then
        log(string.format(""[league_manager] Error: no se pudo reservar pagina 0x%x para %s (obtenido 0x%x)"", page, desc, got))
        return false
    end
    return true
end

local function apply_patches_safely(name, patch_list)
    local n = #patch_list
    local bad = 0
    for i = 1, n do
        local p = patch_list[i]
        local want = hex2bin(p.old)
        local cur = memory.read(p.va, #want)
        if cur == hex2bin(p.new) then
            -- Ya aplicado previamente (por modulo previo o recarga)
        elseif p.alt and cur == hex2bin(p.alt) then
            -- Variante alternativa ya presente
        elseif cur ~= want then
            log(string.format(""[league_manager] [%s] Mismatch en 0x%x: esperado %s, actual %s (%s)"",
                name, p.va, p.old, bin2hex(cur), p.why or """"))
            bad = bad + 1
        end
    end

    if bad > 0 then
        log(string.format(""[league_manager] [%s] %d fallos de verificacion. Omitiendo aplicacion por seguridad."", name, bad))
        return false
    end

    local written = 0
    for i = 1, n do
        local p = patch_list[i]
        local target = hex2bin(p.new)
        local cur = memory.read(p.va, #target)
        if cur ~= target then
            memory.write(p.va, target)
            written = written + 1
        end
    end
    log(string.format(""[league_manager] [%s] %d parches verificados y activos."", name, n))
    return true
end

-- ============================================================================
-- 1. INSTALACION DE GANCHOS ANTI-CUELGUES (NULL-GUARDS)
-- ============================================================================
local function install_nullguards()
    return apply_patches_safely(""Null-Guards"", NULLGUARDS_PATCHES)
end

-- ============================================================================
-- 2. INSTALACION DE EXPANSION DE SLOTS A 250
-- ============================================================================
local function install_slots250()
    if not reserve_page(0x163400000, ""Slots 250 Table & Accessors"") then
        return false
    end
    return apply_patches_safely(""Slots 250"", SLOTS250_PATCHES)
end

-- ============================================================================
-- 3. INSTALACION DE RANURAS DE CLUB A 8 BITS (SLOT 7 -> 8)
-- ============================================================================
local function install_slot7()
    if not reserve_page(0x163c00000, ""Slot 8-Bit Trampoline"") then
        return false
    end
    return apply_patches_safely(""Slot 8-Bit"", SLOT7_PATCHES)
end

-- ============================================================================
-- 3b. INSTALACION DE EXPANSION DE PAISES / REGIONES A 128 (fl26reg128)
-- ============================================================================
local function install_reg128()
    return apply_patches_safely(""Regiones 128"", REG128_PATCHES)
end

-- ============================================================================
-- 4. INSTALACION DE BANDERAS DE PAIS (HOOK 0x140C950D0)
-- ============================================================================
local function install_flags(slot_country, count)
    local got = bin2hex(memory.read(SLOT_FN, #SLOT_FN_PROLOGUE / 2))
    if got ~= SLOT_FN_PROLOGUE then
        log(string.format(""[league_manager] Prologo de banderas en 0x%x no coincide -- sin banderas"", SLOT_FN))
        return false
    end

    local stub = nil
    for _, pref in ipairs({ 0x15e200000, 0x15f200000, 0x161200000, 0x171200000 }) do
        local p = ffi.C.fl26_VirtualAlloc(ffi.cast(""void*"", pref), 0x1000, 0x3000, 0x40) -- PAGE_EXECUTE_READWRITE
        if p ~= nil then stub = tonumber(ffi.cast(""uint64_t"", p)); break end
    end
    if not stub then
        log(""[league_manager] VirtualAlloc fallo para el stub de banderas"")
        return false
    end

    local back = rel32(stub + 0x3b, SLOT_FN + SLOT_FN_RESUME)
    local into = rel32(SLOT_FN + 5, stub)
    if not (back and into) then
        log(""[league_manager] Stub de banderas fuera de rango de salto"")
        return false
    end

    local code = ""\129\249\249\0\0\0"" .. ""\15\135\24\0\0\0"" .. ""\137\200"" .. ""\72\141\21\38\0\0\0""
              .. ""\15\183\4\66"" .. ""\102\61\255\255"" .. ""\15\132\1\0\0\0"" .. ""\195""
              .. hex2bin(SLOT_FN_PROLOGUE) .. ""\233"" .. back
    assert(#code == 0x3b, #code)

    local tbl = {}
    for sl = 0, NSLOTS - 1 do
        local c = slot_country[sl] or 0xffff
        tbl[#tbl + 1] = string.char(c % 256, math.floor(c / 256) % 256)
    end

    ffi.copy(ffi.cast(""void*"", stub), code .. table.concat(tbl))
    memory.write(SLOT_FN, ""\233"" .. into)
    log(string.format(""[league_manager] Banderas instaladas en stub 0x%x (%d slots con pais)"", stub, count))
    return true
end

-- ============================================================================
-- 5. ORDEN EN MENU EXHIBICION / KICK OFF (HOOK 0x140EAD990)
-- ============================================================================
local function install_kickoff_order(slot_after, order)
    for _, s in ipairs(ORDER_SITES) do
        local got = bin2hex(memory.read(s[1], #s[2] / 2))
        if got ~= s[2] then
            log(string.format(""[league_manager] Sitios de orden no coinciden en 0x%x -- orden sin modificar"", s[1]))
            return false
        end
    end

    local list = {}
    for i = 0, ORDER_N - 1 do
        local sl = memory.unpack(""u32"", memory.read(ORDER_TBL + 4 * i, 4))
        if not slot_after[sl] then list[#list + 1] = sl end
    end

    local function follows(s, a)
        for _ = 1, 64 do
            s = slot_after[s]
            if s == nil then return false end
            if s == a then return true end
        end
        return false
    end

    local pending = {}
    for _, sl in ipairs(order) do pending[#pending + 1] = sl end
    repeat
        local progress, rest = false, {}
        for _, sl in ipairs(pending) do
            local a, at = slot_after[sl], nil
            for i, v in ipairs(list) do if v == a then at = i; break end end
            if at then
                while list[at + 1] and follows(list[at + 1], a) do at = at + 1 end
                table.insert(list, at + 1, sl)
                progress = true
            else
                rest[#rest + 1] = sl
            end
        end
        pending = rest
    until not progress or #pending == 0
    for _, sl in ipairs(pending) do list[#list + 1] = sl end

    local n = #list
    local buf = ffi.C.fl26_VirtualAlloc(nil, 4 * n, 0x3000, 0x04) -- PAGE_READWRITE
    if buf == nil then
        log(""[league_manager] VirtualAlloc fallo para el buffer de orden"")
        return false
    end

    local t = ffi.cast(""uint32_t*"", buf)
    for i, v in ipairs(list) do t[i - 1] = v end

    local addr = tonumber(ffi.cast(""uint64_t"", buf))
    local lo, hi = addr % 0x100000000, math.floor(addr / 0x100000000)
    local code = ""\72\186"" .. u32le(lo) .. u32le(hi) .. ""\49\192"" .. ""\57\10"" .. ""\116\18"" .. ""\255\192""
              .. ""\72\131\194\4"" .. ""\61"" .. u32le(n) .. ""\114\239"" .. ""\184"" .. u32le(n) .. ""\195""
    assert(#code == 35, #code)

    memory.write(ORDER_UNLISTED, u32le(n))
    memory.write(ORDER_FN, code)
    log(string.format(""[league_manager] Orden de Kick Off actualizado: %d slots configurados"", n))
    return true
end

-- ============================================================================
-- 6. HOOK DEL MODO EDITAR (0x141EE68F0)
-- ============================================================================
local function install_edit_list(slots_to_add)
    local got_hook = bin2hex(memory.read(EDIT_HOOK, #EDIT_HOOK_OLD / 2))
    local got_l0 = bin2hex(memory.read(EDIT_LIST0, #EDIT_LIST0_OLD / 2))
    if got_hook ~= EDIT_HOOK_OLD or got_l0 ~= EDIT_LIST0_OLD then
        log(""[league_manager] Firmas del Modo Editar no coinciden -- Modo Editar intacto"")
        return false
    end

    local buf = nil
    for _, pref in ipairs({ 0x15ee00000, 0x15fe00000, 0x161e00000, 0x171e00000 }) do
        local p = ffi.C.fl26_VirtualAlloc(ffi.cast(""void*"", pref), 0x1000, 0x3000, 0x40) -- PAGE_EXECUTE_READWRITE
        if p ~= nil then buf = tonumber(ffi.cast(""uint64_t"", p)); break end
    end
    if not buf then
        log(""[league_manager] VirtualAlloc fallo para el stub del Modo Editar"")
        return false
    end

    local stub_addr = buf + 0x20
    local slots_addr = buf + 0x100

    -- Escribir lista de slots (terminada en 0xFFFFFFFF)
    local t = ffi.cast(""uint32_t*"", ffi.cast(""void*"", slots_addr))
    for i, sl in ipairs(slots_to_add) do t[i - 1] = sl end
    t[#slots_to_add] = 0xffffffff

    -- Codigo del stub
    local s = string.char(0x48, 0x8d, 0x3d) .. u32le(slots_addr - (stub_addr + 7))  -- lea rdi, [rip->slots]
           .. string.char(0x83, 0x3f, 0xff)                                         -- loop: cmp dword [rdi], -1
           .. string.char(0x74, 0x18)                                               -- je done (+0x18)
           .. string.char(0x48, 0x89, 0xfa)                                         -- mov rdx, rdi
           .. string.char(0x48, 0x89, 0xd9)                                         -- mov rcx, rbx
           .. string.char(0x48, 0xb8) .. u64le(EDIT_PUSH)                           -- mov rax, 0x14047be40
           .. string.char(0xff, 0xd0)                                               -- call rax
           .. string.char(0x48, 0x83, 0xc7, 0x04)                                   -- add rdi, 4
           .. string.char(0xeb, 0xe3)                                               -- jmp loop (-0x1d)
           .. hex2bin(EDIT_HOOK_OLD)                                                -- instruccion reemplazada (5 bytes: offset 36..40)
           .. string.char(0xe9) .. rel32(stub_addr + 41 + 5, EDIT_BACK)             -- jmp back (opcode en 41, disp32 en 42..45, RIP=46)

    assert(#s == 46, string.format(""[league_manager] Tamano inesperado de stub Modo Editar: esperado 46, obtenido %d"", #s))
    ffi.copy(ffi.cast(""void*"", stub_addr), s, #s)

    local jump_to_stub = string.char(0xe9) .. rel32(EDIT_HOOK + 5, stub_addr)
    memory.write(EDIT_HOOK, jump_to_stub)
    log(string.format(""[league_manager] Modo Editar enlazado en stub 0x%x (%d slots agregados)"", stub_addr, #slots_to_add))
    return true
end

-- ============================================================================
-- 7. INICIALIZACION PRINCIPAL DEL MODULO SIDER
-- ============================================================================
function m.init(ctx)
    log(string.format(""[league_manager] Iniciando League Manager v%s por %s"", m.version, m.author))
    
    if ffi == nil then
        log(""[league_manager] Error: FFI global no disponible. Asegurate de tener luajit.ext.enabled = 1 en sider.ini"")
        return
    end

    if not m.cdef_virtualalloc then
        ffi.cdef([[ void* fl26_VirtualAlloc(void*, size_t, uint32_t, uint32_t) asm(""VirtualAlloc""); ]])
        m.cdef_virtualalloc = true
    end

    -- 1. Verificar firmas de codigo para la tabla de competiciones ANTES de aplicar cualquier gancho o parche
    if not (check_sites(LEAS) and check_sites(COUNT_SITES)) then
        log(""[league_manager] Error critico: firmas de la tabla de competiciones no coinciden con este ejecutable. Abortando inicializacion de forma segura."")
        return
    end

    -- 2. Instalar Ganchos Anti-Cuelgues (Null-Guards)
    local failed_installs = {}
    if not install_nullguards() then failed_installs[#failed_installs + 1] = ""Null-Guards"" end

    -- 3. Instalar Expansion a 250 Slots
    if not install_slots250() then failed_installs[#failed_installs + 1] = ""Slots 250"" end

    -- 4. Instalar Expansion de Slot de Club a 8 Bits
    if not install_slot7() then failed_installs[#failed_installs + 1] = ""Slot 8-Bit"" end

    -- 5. Instalar Expansion de Paises / Regiones a 128
    if not install_reg128() then failed_installs[#failed_installs + 1] = ""Regiones 128"" end

    -- Si un parche base fallo, abortar antes de expandir la tabla: continuar dejaria
    -- ganchos instalados sobre una base inconsistente (ver M139). El doble sistema
    -- de parches (lua generado + fl26*.lua) sigue siendo una decision pendiente:
    -- que modulo es dueno de cada direccion debe auditarse antes de endurecer mas.
    if #failed_installs > 0 then
        log(string.format(""[league_manager] Error: instalacion base incompleta (%s). Abortando antes de expandir la tabla."", table.concat(failed_installs, "", "")))
        return
    end

    -- 5. Leer las 165 filas de fabrica
    local rows, byid = {}, {}
    for i = 0, NROWS - 1 do
        local r = memory.read(BASE + i * STRIDE, STRIDE)
        rows[#rows + 1] = r
        local id = row_u32(r, OFF_ID)
        if not byid[id] then byid[id] = i + 1 end
    end

    if not byid[TEMPLATE_ID] then
        log(string.format(""[league_manager] Error: no se encontro la fila plantilla ID %d"", TEMPLATE_ID))
        return
    end

    local tmpl = rows[byid[TEMPLATE_ID]]

    -- 6. Agregar filas para nuestras ligas personalizadas
    local added_slots = {}
    local slot_country = {}
    local slot_after = {}
    local order_list = {}

    for _, l in ipairs(CUSTOM_LEAGUES) do
        local new_row = row_set(row_set(tmpl, OFF_ID, u32le(l.reg_id)), OFF_SLOT, u32le(l.slot))
        
        -- Limpiar nombres para que lea automaticamente el nombre real de CompetitionRegulation.bin
        new_row = row_set(row_set(new_row, OFF_NAME, NO_LABEL), OFF_SHORT, NO_LABEL)
        new_row = row_set(row_set(new_row, OFF_SPLIT_TEXT, NO_LABEL), OFF_INFO_TEXT, NO_LABEL)

        -- Activar premios de temporada (+0x29 = 1)
        new_row = row_set(new_row, 0x29, ""\1"")

        -- Configurar ascensos / descensos
        new_row = row_set(row_set(new_row, OFF_PROMOTE, u32le(math.max(0, l.promote or 0))), OFF_DEMOTE, u32le(math.max(0, l.demote or 0)))

        rows[#rows + 1] = new_row
        byid[l.reg_id] = #rows

        added_slots[#added_slots + 1] = l.slot
        slot_country[l.slot] = l.country_id

        if l.kickoff_after then
            slot_after[l.slot] = l.kickoff_after
            order_list[#order_list + 1] = l.slot
        end

        log(string.format(""[league_manager] Fila creada: RegID %d -> Fila %d, Slot %d, Pais %d (%s)"",
            l.reg_id, #rows - 1, l.slot, l.country_id, l.name))
    end

    local total_rows = #rows
    local body = table.concat(rows)

    -- 7. Asignar memoria para la nueva tabla expandida
    local newbase = nil
    for _, pref in ipairs({ 0x15c000000, 0x15d000000, 0x160000000, 0x170000000, 0x180000000 }) do
        local p = ffi.C.fl26_VirtualAlloc(ffi.cast(""void*"", pref), #body, 0x3000, 0x04) -- PAGE_READWRITE
        if p ~= nil then newbase = tonumber(ffi.cast(""uint64_t"", p)); break end
    end

    if not newbase then
        log(""[league_manager] VirtualAlloc fallo para la tabla de competiciones"")
        return
    end

    ffi.copy(ffi.cast(""void*"", newbase), body, #body)

    -- 8. Repuntar las instrucciones LEA a la nueva tabla
    for _, s in ipairs(LEAS) do
        local disp = newbase - (s[1] + 7)
        if disp < 0 then disp = disp + 0x100000000 end
        memory.write(s[1] + 3, u32le(disp))
    end

    -- 9. Actualizar los 4 contadores de filas en el motor
    for _, s in ipairs(COUNT_SITES) do
        memory.write(s[1] + s[3], u32le(total_rows))
    end

    log(string.format(""[league_manager] Tabla de competiciones expandida en 0x%x (%d filas totales)"", newbase, total_rows))

    -- 10. Instalar gancho de banderas
    if not install_flags(slot_country, #CUSTOM_LEAGUES) then failed_installs[#failed_installs + 1] = ""Banderas"" end

    -- 11. Instalar orden de exhibicion / Kick Off
    if #order_list > 0 then
        if not install_kickoff_order(slot_after, order_list) then failed_installs[#failed_installs + 1] = ""Orden Kick Off"" end
    end

    -- 12. Instalar soporte para Modo Editar
    if not install_edit_list(added_slots) then failed_installs[#failed_installs + 1] = ""Modo Editar"" end

    if #failed_installs == 0 then
        log(""[league_manager] Todos los sistemas enlazados con exito. 100% Nativo para PES 2021."")
    else
        log(string.format(""[league_manager] Error: no se instalaron: %s"", table.concat(failed_installs, "", "")))
    end
end

-- ============================================================================
-- 8. INTERFAZ OVERLAY (SIDER HUD - TECLA ESPACIO)
-- ============================================================================
function m.overlay(ctx, state)
    local text = string.format(""EXEVAZ27 League Manager v%s (PES 2021)\n"", m.version)
    text = text .. ""--------------------------------------------------\n""
    text = text .. string.format(""Ligas Personalizadas Activas: %d\n"", #CUSTOM_LEAGUES)
    for _, l in ipairs(CUSTOM_LEAGUES) do
        text = text .. string.format("" - %s\n   RegID: %d | CompID: %d | Slot: %d | Pais: %d\n   Posicion: Debajo de Slot %d\n"",
            l.name, l.reg_id, l.comp_id, l.slot, l.country_id, l.kickoff_after or 14)
    end
    text = text .. ""Estado de Sistemas:\n""
    text = text .. "" [X] Null-Guards (11 Ganchos Anti-Cuelgues)\n""
    text = text .. "" [X] Expansion de Capacidad: 250 Slots (0x163400000)\n""
    text = text .. "" [X] Ranuras de Equipo de 8 Bits: Slots 0..249 (0x163C00000)\n""
    text = text .. "" [X] Expansion de Paises / Regiones a 128 (fl26reg128)\n""
    text = text .. "" [X] Menu Seleccion de Equipo (Exhibicion / Amistoso)\n""
    text = text .. "" [X] Modo Editar (Equipos y Jugadores)\n""
    text = text .. "" [X] Banderas y Nombres Nativos\n""
    return text
end

return m
";
        }
    }
}
