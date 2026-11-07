using System;







using System.Collections.Generic;







using System.Drawing;







using System.IO;







using System.Linq;







using System.Text;







namespace EditorLigasPes







{







    /// <summary>







    /// Administrador de Base de Datos PES (pesdb) con soporte forense de copias de seguridad y expansión de ligas.







    /// Desarrollado por EXEVAZ27.







    /// </summary>







    public class DatabaseManager







    {







        public string DatabasePath { get; private set; }







        public List<CompetitionRecord> Competitions { get; private set; }







        public List<RegulationRecord> Regulations { get; private set; }







        public List<EntryRecord> Entries { get; private set; }



        public List<TeamRecord> Teams { get; private set; }



        public List<CountryRecord> Countries { get; private set; }



        public List<CoachRecord> Coaches { get; private set; }



        public List<StadiumRecord> Stadiums { get; private set; }



        public List<DerbyRecord> Derbies { get; private set; }



        public List<PlayerRecord> Players { get; private set; }



        public List<PlayerAssignmentRecord> PlayerAssignments { get; private set; }



        public List<TacticsRecord> Tactics { get; private set; }



        public List<TacticsFormationRecord> TacticsFormations { get; private set; }







        public Dictionary<uint, TeamRecord> TeamsById { get; private set; }



        public Dictionary<ushort, CountryRecord> CountriesById { get; private set; }



        public Dictionary<uint, CoachRecord> CoachesById { get; private set; }



        public Dictionary<ushort, StadiumRecord> StadiumsById { get; private set; }



        public Dictionary<uint, PlayerRecord> PlayersById { get; private set; }



        public Dictionary<uint, List<PlayerAssignmentRecord>> PlayerAssignmentsByTeam { get; private set; }



        public Dictionary<uint, List<TacticsRecord>> TacticsByClub { get; private set; }



        public Dictionary<uint, List<TacticsFormationRecord>> FormationsByTacticsKey { get; private set; }



        public Dictionary<byte, List<EntryRecord>> EntriesByCompetition { get; private set; }



        public Dictionary<byte, RegulationRecord> RegulationsByCompetition { get; private set; }



        public Dictionary<uint, int> PlayersPerTeam { get; private set; }



        public Dictionary<byte, ushort> CustomLeagueCountries { get; private set; }



        public Dictionary<byte, byte> LeagueSeasonType { get; private set; }



        public Dictionary<byte, ushort> LeagueLinkedCup { get; private set; }



        public Dictionary<byte, ushort> LeagueLinkedSuperCup { get; private set; }



        public Dictionary<byte, bool> LeagueAperturaClausura { get; private set; }



        public Dictionary<byte, ushort> LeagueClausuraRegulation { get; private set; }



        public Dictionary<byte, bool> LeagueCupAllDivisions { get; private set; }







        public bool HasUnsavedChanges { get; set; }



        private List<string> _loadWarnings = new List<string>();



        public List<string> LoadWarnings { get { return _loadWarnings; } }







        // Alto 13 (mitad DB): equipos preexistentes omitidos por la ultima



        // importacion CSV (ver ImportTeamsFromCsv). La UI los muestra.



        public List<string> LastCsvSkippedTeams { get; private set; }







        







        public DatabaseManager()



        {



            Competitions = new List<CompetitionRecord>();



            Regulations = new List<RegulationRecord>();



            Entries = new List<EntryRecord>();



            Teams = new List<TeamRecord>();



            Countries = new List<CountryRecord>();



            Coaches = new List<CoachRecord>();



            Stadiums = new List<StadiumRecord>();



            Derbies = new List<DerbyRecord>();



            Players = new List<PlayerRecord>();



            PlayerAssignments = new List<PlayerAssignmentRecord>();



            Tactics = new List<TacticsRecord>();



            TacticsFormations = new List<TacticsFormationRecord>();







            TeamsById = new Dictionary<uint, TeamRecord>();



            CountriesById = new Dictionary<ushort, CountryRecord>();



            CoachesById = new Dictionary<uint, CoachRecord>();



            StadiumsById = new Dictionary<ushort, StadiumRecord>();



            PlayersById = new Dictionary<uint, PlayerRecord>();



            PlayerAssignmentsByTeam = new Dictionary<uint, List<PlayerAssignmentRecord>>();



            TacticsByClub = new Dictionary<uint, List<TacticsRecord>>();



            FormationsByTacticsKey = new Dictionary<uint, List<TacticsFormationRecord>>();



            EntriesByCompetition = new Dictionary<byte, List<EntryRecord>>();



            RegulationsByCompetition = new Dictionary<byte, RegulationRecord>();



            PlayersPerTeam = new Dictionary<uint, int>();



            CustomLeagueCountries = new Dictionary<byte, ushort>();



            LeagueSeasonType = new Dictionary<byte, byte>();



            LeagueLinkedCup = new Dictionary<byte, ushort>();



            LeagueLinkedSuperCup = new Dictionary<byte, ushort>();



            LeagueCupAllDivisions = new Dictionary<byte, bool>();



            LeagueAperturaClausura = new Dictionary<byte, bool>();



            LeagueClausuraRegulation = new Dictionary<byte, ushort>();







        }







        // M92: punto de confirmacion para escrituras fuera de la carpeta



        // de la DB (emblemas livecpk, borrado de emblemas). Si es null,



        // se mantiene el comportamiento actual; si la UI lo asigna y



        // devuelve false, la escritura externa no se realiza. El cableado



        // en MainForm queda pendiente de decision de Exe.



        public Func<string, bool> ConfirmExternalWrite { get; set; }







        private bool ExternalWriteAllowed(string what)



        {



            if (ConfirmExternalWrite == null) return true;



            try { return ConfirmExternalWrite(what); }



            catch { return false; }



        }







        public void SetLeagueCountry(byte compId, ushort countryId)







        {







            // Alto 25: no mutar ligas oficiales (rango vanilla 1..149). Se



            // eximen las competiciones personalizadas: creadas en esta sesion



            // (IsNew) o con reglamento personalizado (Id >= 179, mismo criterio



            // que MainForm.isCustom). Sin esta exencion, EnsureClassicTeamsCompetition



            // (comp. de clasicos, ID 53) y las ligas creadas por la app quedarian



            // bloqueadas. Los 10 IDs estructurales de IsProtectedVanillaCompetition



            // quedan cubiertos por este rango. Si la comp. no existe en memoria,



            // no se bloquea (se preserva el comportamiento previo para IDs fantasma).



            if (compId >= 1 && compId <= 149 && Competitions != null)



            {



                var existing = Competitions.FirstOrDefault(c => c.Id == compId);



                RegulationRecord existingReg = null;



                bool hasCustomReg = existing != null && RegulationsByCompetition != null



                    && RegulationsByCompetition.TryGetValue(compId, out existingReg)



                    && existingReg != null && existingReg.Id >= 179;



                if (existing != null && !existing.IsNew && !hasCustomReg)



                    throw new InvalidOperationException("La competicion " + compId + " es una competicion oficial base del juego (rango vanilla 1..149) y su pais no puede modificarse con SetLeagueCountry.");



            }







            if (CustomLeagueCountries == null)







                CustomLeagueCountries = new Dictionary<byte, ushort>();







            CustomLeagueCountries[compId] = countryId;







            // Sincronizar region (0..127) en el registro de competicion







            var comp = Competitions != null ? Competitions.FirstOrDefault(c => c.Id == compId) : null;







            if (comp != null)







            {







                comp.Group = GetDefaultRegionForCountry(countryId);







                comp.IsModified = true;







                // Alto 25: re-sellar RawBytes al mutar (IsModified ya hace que



                // Save use ToBytes, pero el re-sellado explicito es lo pedido).



                comp.RawBytes = comp.ToBytes();







            }







            HasUnsavedChanges = true;







        }







        /// <summary>







        /// Mapea el ID de pais a la region interna de PES 2021 (0..127).







        /// Con el expansor fl26reg128, se desbloquean las regiones 29..127.







        /// </summary>







        public static byte GetDefaultRegionForCountry(ushort countryId)







        {







            switch (countryId)







            {







                case 0: return 0;   // Internacional / Otros







                case 204: return 1; // Inglaterra







                case 208: return 2; // Francia







                case 215: return 3; // Italia







                case 210: return 4; // Alemania







                case 236: return 5; // Espana







                case 222: return 6; // Paises Bajos







                case 227: return 7; // Portugal







                case 241: return 8; // Turquia







                case 229: return 9; // Rusia







                case 195: return 10;// Belgica







                case 202: return 11;// Dinamarca







                case 238: return 12;// Suiza







                case 232: return 13;// Escocia







                case 192: return 14;// Austria







                case 211: return 15;// Grecia







                case 146: return 16;// Brasil







                case 144: return 17;// Argentina







                case 148: return 18;// Chile







                case 149: return 19;// Colombia







                case 262: return 20;// Japon







                case 261: return 25;// China







                case 288: return 26;// Tailandia







                case 155: return 27;// Peru







                case 157: return 28;// Uruguay







                // Regiones expandidas por fl26reg128 (29..127):







                case 145: return 29;// Bolivia







                case 151: return 30;// Ecuador







                case 154: return 31;// Paraguay







                case 158: return 32;// Venezuela







                case 89: return 33; // Mexico







                case 95: return 34; // Estados Unidos







                case 77: return 35; // Canada







                case 79: return 36; // Costa Rica







                case 198: return 37;// Croacia







                case 233: return 38;// Serbia







                case 242: return 39;// Ucrania







                case 226: return 40;// Polonia







                case 237: return 41;// Suecia







                case 224: return 42;// Noruega







                case 201: return 43;// Rep. Checa







                case 280: return 44;// Arabia Saudita







                case 26: return 45; // Egipto







                case 41: return 46; // Marruecos







                default:







                    return (byte)(countryId > 0 ? (countryId % 128) : 0);







            }







        }







        public string GetLiveCpkRoot()







        {







            if (!string.IsNullOrEmpty(DatabasePath))







            {







                // Si la ruta contiene "livecpk", buscar el directorio directo dentro de livecpk







                int idx = DatabasePath.IndexOf("livecpk", StringComparison.OrdinalIgnoreCase);







                if (idx >= 0)







                {







                    string after = DatabasePath.Substring(idx + 7).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);







                    string[] parts = after.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);







                    if (parts.Length > 0)







                    {







                        string root = Path.Combine(DatabasePath.Substring(0, idx + 7), parts[0]);







                        if (Directory.Exists(root)) return root;







                    }







                }







            }







            // Sin raiz derivable de la base cargada: no hay fallback a ninguna
            // carpeta escrita a mano (principio: nada de rutas de una PC puntual).
            return null;







        }







        public Image GetCompetitionEmblem(int compId)







        {







            // B41: fuera del rango de IDs de competicion no hay emblema



            // posible (antes solo generaba un nombre inexistente).



            if (compId < 0 || compId > 255) return null;







            // 1. Prioridad: Sider livecpk







            string liveRoot = GetLiveCpkRoot();







            if (!string.IsNullOrEmpty(liveRoot))







            {







                string embPath = Path.Combine(liveRoot, "common", "render", "symbol", "emblemLc", string.Format("emb_{0:D4}_l.png", compId));







                if (File.Exists(embPath))







                {







                    return ImageProcessor.LoadSafe(embPath);







                }







            }







            // 2. Fallback: Archivo original del juego dt15_x64.cpk







            string dt15 = GameEmblemProvider.FindDt15CpkPath(DatabasePath);







            if (!string.IsNullOrEmpty(dt15) && File.Exists(dt15))







            {







                var entries = CpkExtractor.GetTocEntries(dt15, (dir, name) =>







                    dir.IndexOf("emblemLc", StringComparison.OrdinalIgnoreCase) >= 0 &&







                    string.Equals(name, string.Format("emb_{0:D4}_l.png", compId), StringComparison.OrdinalIgnoreCase));







                if (entries.Count > 0)







                {







                    return GameEmblemProvider.ExtractEmblemImage(dt15, entries[0]);







                }







            }







            return null;







        }







        public void SaveCompetitionEmblem(int compId, Image sourceImage)







        {







            // M72/B54: misma guarda que SaveTeamEmblem (antes el null



            // terminaba en ArgumentNullException en ImageProcessor).



            if (sourceImage == null) return;







            string liveRoot = GetLiveCpkRoot();







            if (string.IsNullOrEmpty(liveRoot))
            {
                // Sin livecpk derivable de la base: se exporta junto a la base.
                // (Antes caia a la carpeta del juego del autor, escrita a mano.)
                liveRoot = Path.Combine(DatabasePath ?? ".", "livecpk_export");
                Directory.CreateDirectory(liveRoot);
            }







            ImageProcessor.SaveCompetitionEmblems(sourceImage, liveRoot, compId);







        }







        public bool DeleteCompetitionEmblem(int compId)







        {







            string liveRoot = GetLiveCpkRoot();







            if (string.IsNullOrEmpty(liveRoot)) return false;







            if (!ExternalWriteAllowed("delete-emblem")) return false;







            return ImageProcessor.DeleteCompetitionEmblems(liveRoot, compId);







        }







        public Image GetTeamEmblem(uint teamId)



        {



            // B41: el ID 0 no es un equipo valido.



            if (teamId == 0) return null;



            // 1. Prioridad: Sider LiveCPK



            string liveRoot = GetLiveCpkRoot();



            if (!string.IsNullOrEmpty(liveRoot))



            {



                string flagDir = Path.Combine(liveRoot, "common", "render", "symbol", "flag");



                string[] candidates = new string[] {



                    string.Format("e_{0:D6}_r_l.png", teamId),



                    string.Format("e_{0:D6}_r.png", teamId),



                    string.Format("e_{0:D6}_f_l.png", teamId),



                    string.Format("e_{0:D6}_f.png", teamId),



                    string.Format("e_{0:D6}_r_ll.png", teamId)



                };



                foreach (var c in candidates)



                {



                    string p = Path.Combine(flagDir, c);



                    if (File.Exists(p))



                    {



                        return ImageProcessor.LoadSafe(p);



                    }



                }



            }







            // 2. Fallback: Archivo original del juego dt15_x64.cpk



            string dt15 = GameEmblemProvider.FindDt15CpkPath(DatabasePath);



            if (!string.IsNullOrEmpty(dt15) && File.Exists(dt15))



            {



                // B40: mismas 5 variantes que el fallback livecpk



                // (antes solo _r y _f).



                string[] targetNames = new string[] {



                    string.Format("e_{0:D6}_r_l.png", teamId),



                    string.Format("e_{0:D6}_r.png", teamId),



                    string.Format("e_{0:D6}_f_l.png", teamId),



                    string.Format("e_{0:D6}_f.png", teamId),



                    string.Format("e_{0:D6}_r_ll.png", teamId)



                };



                var entries = CpkExtractor.GetTocEntries(dt15, (dir, name) =>



                    dir.IndexOf("flag", StringComparison.OrdinalIgnoreCase) >= 0 &&



                    targetNames.Any(tn => string.Equals(name, tn, StringComparison.OrdinalIgnoreCase)));







                if (entries.Count > 0)



                {



                    return GameEmblemProvider.ExtractEmblemImage(dt15, entries[0]);



                }



            }







            return null;



        }







        public void SaveTeamEmblem(uint teamId, Image emblemImage)



        {



            if (emblemImage == null) return;



            string liveRoot = GetLiveCpkRoot();



            if (string.IsNullOrEmpty(liveRoot))
            {
                // Sin livecpk derivable de la base: se exporta junto a la base.
                // (Antes caia a la carpeta del juego del autor, escrita a mano.)
                liveRoot = Path.Combine(DatabasePath ?? ".", "livecpk_export");
                Directory.CreateDirectory(liveRoot);
            }







            ImageProcessor.SaveTeamCrests(emblemImage, liveRoot, teamId);



            HasUnsavedChanges = true;



        }







        public void Load(string folderPath)







        {







            if (!Directory.Exists(folderPath))







                throw new DirectoryNotFoundException("El directorio de base de datos no existe: " + folderPath);







            string compFile = Path.Combine(folderPath, "Competition.bin");







            string regFile = Path.Combine(folderPath, "CompetitionRegulation.bin");







            string entryFile = Path.Combine(folderPath, "CompetitionEntry.bin");







            string teamFile = Path.Combine(folderPath, "Team.bin");







            if (!File.Exists(compFile) || !File.Exists(regFile) || !File.Exists(entryFile) || !File.Exists(teamFile))







            {







                throw new FileNotFoundException("Faltan archivos requeridos en la carpeta pesdb. Se necesitan: Competition.bin, CompetitionRegulation.bin, CompetitionEntry.bin y Team.bin.");







            }







            // Realizar copia de seguridad automática inicial (.bak) si no existe







            CreateInitialBackups(folderPath);







            // 1. Cargar y descomprimir







            byte[] compBytes = WesysCompressor.Decompress(compFile);







            byte[] regBytes = WesysCompressor.Decompress(regFile);







            byte[] entryBytes = WesysCompressor.Decompress(entryFile);







            byte[] teamBytes = WesysCompressor.Decompress(teamFile);







            // M62: DatabasePath solo se fija cuando los 4 archivos



            // obligatorios ya descomprimieron (un fallo antes no deja



            // la ruta apuntando a una carpeta no cargada).



            DatabasePath = folderPath;







            LoadWarnings.Clear();







            // 2. Parsear Competitions (36 bytes) - Alto 31: guarda de resto final



            Competitions.Clear();



            for (int i = 0; i + CompetitionRecord.RecordSize <= compBytes.Length; i += CompetitionRecord.RecordSize)



            {



                Competitions.Add(CompetitionRecord.FromBytes(compBytes, i));



            }







            // 3. Parsear Regulations (2352 bytes) - Alto 31: guarda de resto final



            Regulations.Clear();



            RegulationsByCompetition.Clear();



            // M61: se avisa de IDs de reglamento repetidos (filas



            // corruptas/duplicadas). La lista conserva todas las filas



            // para no perder datos al guardar; el diccionario por



            // competicion sigue siendo "ultima fila gana" porque una



            // competicion puede tener varias filas de reglamento (fases).



            var seenRegIds = new HashSet<ushort>();



            for (int i = 0; i + RegulationRecord.RecordSize <= regBytes.Length; i += RegulationRecord.RecordSize)



            {



                var r = RegulationRecord.FromBytes(regBytes, i);



                if (!seenRegIds.Add(r.Id))



                {



                    LoadWarnings.Add(string.Format("Reglamento duplicado en el archivo: ID {0} (competicion {1})", r.Id, r.IdCompetition));



                }



                Regulations.Add(r);



                RegulationsByCompetition[r.IdCompetition] = r;



            }







            // 4. Parsear Teams (1532 bytes) - Alto 31: guarda de resto final



            Teams.Clear();



            TeamsById.Clear();



            for (int i = 0; i + TeamRecord.RecordSize <= teamBytes.Length; i += TeamRecord.RecordSize)



            {



                var t = TeamRecord.FromBytes(teamBytes, i);



                Teams.Add(t);



                // M61: primer equipo gana en el indice (igual que



                // paises/entrenadores/estadios/jugadores); la lista



                // conserva todas las filas para no perder datos al guardar.



                if (TeamsById.ContainsKey(t.Id))



                {



                    LoadWarnings.Add(string.Format("Equipo duplicado omitido del indice: ID {0} ({1})", t.Id, t.NameSpanish));



                }



                else



                {



                    TeamsById[t.Id] = t;



                }



            }







            // 5. Parsear Entries (12 bytes) - Alto 31: guarda de resto final



            Entries.Clear();



            EntriesByCompetition.Clear();



            for (int i = 0; i + EntryRecord.RecordSize <= entryBytes.Length; i += EntryRecord.RecordSize)



            {



                var e = EntryRecord.FromBytes(entryBytes, i);



                Entries.Add(e);



                if (!EntriesByCompetition.ContainsKey(e.CompetitionId))



                    EntriesByCompetition[e.CompetitionId] = new List<EntryRecord>();



                EntriesByCompetition[e.CompetitionId].Add(e);



            }







            // 6. Parsear Country.bin (stride 1420 bytes)



            Countries.Clear();



            CountriesById.Clear();



            string countryFile = Path.Combine(folderPath, "Country.bin");
            if (!File.Exists(countryFile))
                LoadWarnings.Add("Advertencia: esta base no trae Country.bin; la lista de paises quedo vacia. No se cargo ningun archivo de otra carpeta.");
            if (File.Exists(countryFile))



            {



                try



                {



                    byte[] countryBytes = WesysCompressor.Decompress(countryFile);



                    for (int i = 0; i + CountryRecord.RecordSize <= countryBytes.Length; i += CountryRecord.RecordSize)



                    {



                        var c = CountryRecord.FromBytes(countryBytes, i);



                        if (c.Id > 0 && !CountriesById.ContainsKey(c.Id))



                        {



                            Countries.Add(c);



                            CountriesById[c.Id] = c;



                        }



                        else if (c.Id > 0)



                        {



                            LoadWarnings.Add(string.Format("Pais duplicado omitido: ID {0}", c.Id));



                        }



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar Country.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando Country.bin: " + ex.Message);



                }



            }



            EnsureFallbackCountries();







            // 7. Parsear Coach.bin (stride 100 bytes)



            Coaches.Clear();



            CoachesById.Clear();



            string coachFile = Path.Combine(folderPath, "Coach.bin");



            if (File.Exists(coachFile))



            {



                try



                {



                    byte[] coachBytes = WesysCompressor.Decompress(coachFile);



                    for (int i = 0; i + CoachRecord.RecordSize <= coachBytes.Length; i += CoachRecord.RecordSize)



                    {



                        var coach = CoachRecord.FromBytes(coachBytes, i);



                        if (!CoachesById.ContainsKey(coach.Id))



                        {



                            Coaches.Add(coach);



                            CoachesById[coach.Id] = coach;



                        }



                        else



                        {



                            LoadWarnings.Add(string.Format("Entrenador duplicado omitido: ID {0}", coach.Id));



                        }



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar Coach.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando Coach.bin: " + ex.Message);



                }



            }



            EnsureFallbackCoaches();







            // 8. Parsear Stadium.bin (stride 392 bytes)



            Stadiums.Clear();



            StadiumsById.Clear();



            string stadiumFile = Path.Combine(folderPath, "Stadium.bin");



            if (File.Exists(stadiumFile))



            {



                try



                {



                    byte[] stadiumBytes = WesysCompressor.Decompress(stadiumFile);



                    for (int i = 0; i + StadiumRecord.RecordSize <= stadiumBytes.Length; i += StadiumRecord.RecordSize)



                    {



                        var s = StadiumRecord.FromBytes(stadiumBytes, i);



                        if (!StadiumsById.ContainsKey(s.Id))



                        {



                            Stadiums.Add(s);



                            StadiumsById[s.Id] = s;



                        }



                        else



                        {



                            LoadWarnings.Add(string.Format("Estadio duplicado omitido: ID {0}", s.Id));



                        }



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar Stadium.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando Stadium.bin: " + ex.Message);



                }



            }



            EnsureFallbackStadiums();







            // 9. Parsear Derby.bin (stride 8 bytes)



            Derbies.Clear();



            string derbyFile = Path.Combine(folderPath, "Derby.bin");



            if (File.Exists(derbyFile))



            {



                try



                {



                    byte[] derbyBytes = WesysCompressor.Decompress(derbyFile);



                    for (int i = 0; i + DerbyRecord.RecordSize <= derbyBytes.Length; i += DerbyRecord.RecordSize)



                    {



                        Derbies.Add(DerbyRecord.FromBytes(derbyBytes, i));



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar Derby.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando Derby.bin: " + ex.Message);



                }



            }







            // 10. Parsear PlayerAssignment.bin (stride 16 bytes)



            PlayerAssignments.Clear();



            PlayerAssignmentsByTeam.Clear();



            PlayersPerTeam.Clear();



            string paFile = Path.Combine(folderPath, "PlayerAssignment.bin");



            if (File.Exists(paFile))



            {



                try



                {



                    byte[] paBytes = WesysCompressor.Decompress(paFile);



                    for (int i = 0; i + PlayerAssignmentRecord.RecordSize <= paBytes.Length; i += PlayerAssignmentRecord.RecordSize)



                    {



                        var pa = PlayerAssignmentRecord.FromBytes(paBytes, i);



                        PlayerAssignments.Add(pa);



                        if (!PlayerAssignmentsByTeam.ContainsKey(pa.TeamId))



                            PlayerAssignmentsByTeam[pa.TeamId] = new List<PlayerAssignmentRecord>();



                        PlayerAssignmentsByTeam[pa.TeamId].Add(pa);







                        if (PlayersPerTeam.ContainsKey(pa.TeamId))



                            PlayersPerTeam[pa.TeamId]++;



                        else



                            PlayersPerTeam[pa.TeamId] = 1;



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar PlayerAssignment.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando PlayerAssignment.bin: " + ex.Message);



                }



            }







            // 11. Parsear Player.bin (stride 312 bytes)



            Players.Clear();



            PlayersById.Clear();



            string playerFile = Path.Combine(folderPath, "Player.bin");



            if (File.Exists(playerFile))



            {



                try



                {



                    byte[] playerBytes = WesysCompressor.Decompress(playerFile);



                    for (int i = 0; i + PlayerRecord.RecordSize <= playerBytes.Length; i += PlayerRecord.RecordSize)



                    {



                        var p = PlayerRecord.FromBytes(playerBytes, i);



                        if (!PlayersById.ContainsKey(p.Id))



                        {



                            Players.Add(p);



                            PlayersById[p.Id] = p;



                        }



                        else



                        {



                            LoadWarnings.Add(string.Format("Jugador duplicado omitido: ID {0}", p.Id));



                        }



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar Player.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando Player.bin: " + ex.Message);



                }



            }







            // 12. Parsear Tactics.bin (stride 12 bytes)



            Tactics.Clear();



            TacticsByClub.Clear();



            string tacticsFile = Path.Combine(folderPath, "Tactics.bin");



            if (File.Exists(tacticsFile))



            {



                try



                {



                    byte[] tBytes = WesysCompressor.Decompress(tacticsFile);



                    for (int i = 0; i + TacticsRecord.RecordSize <= tBytes.Length; i += TacticsRecord.RecordSize)



                    {



                        var t = TacticsRecord.FromBytes(tBytes, i);



                        Tactics.Add(t);



                        List<TacticsRecord> list;



                        if (!TacticsByClub.TryGetValue(t.ClubId, out list))



                        {



                            list = new List<TacticsRecord>();



                            TacticsByClub[t.ClubId] = list;



                        }



                        list.Add(t);



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar Tactics.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando Tactics.bin: " + ex.Message);



                }



            }







            // 13. Parsear TacticsFormation.bin (stride 12 bytes)



            TacticsFormations.Clear();



            FormationsByTacticsKey.Clear();



            string tfFile = Path.Combine(folderPath, "TacticsFormation.bin");



            if (File.Exists(tfFile))



            {



                try



                {



                    byte[] tfBytes = WesysCompressor.Decompress(tfFile);



                    for (int i = 0; i + TacticsFormationRecord.RecordSize <= tfBytes.Length; i += TacticsFormationRecord.RecordSize)



                    {



                        var tf = TacticsFormationRecord.FromBytes(tfBytes, i);



                        TacticsFormations.Add(tf);



                        List<TacticsFormationRecord> list;



                        if (!FormationsByTacticsKey.TryGetValue(tf.TacticsKey, out list))



                        {



                            list = new List<TacticsFormationRecord>();



                            FormationsByTacticsKey[tf.TacticsKey] = list;



                        }



                        list.Add(tf);



                    }



                }



                catch (Exception ex)



                {



                    LoadWarnings.Add(string.Format("Advertencia al cargar TacticsFormation.bin: {0}", ex.Message));



                    Console.WriteLine("[WARN] Error cargando TacticsFormation.bin: " + ex.Message);



                }



            }







            // 14. Cargar reglas personalizadas de ligas (ascensos, descensos, plazas continentales)







            LoadCustomLeagueRules(folderPath);







            LoadTeamTacticsExtra(folderPath);







            // 15. Garantizar la existencia y asignación de la competición de Equipos Clásicos







            EnsureClassicTeamsCompetition();







            // M62: una carga recien completada no deja cambios pendientes



            // (las derivaciones de la carga, como la competicion de



            // clasicos, se recrean identicas en la proxima carga).



            HasUnsavedChanges = false;







        }







                public static readonly string[] DatabaseFilesToBackup = new string[]



        {



            "Competition.bin", "CompetitionRegulation.bin", "CompetitionEntry.bin",



            "Team.bin", "Coach.bin", "Stadium.bin", "Derby.bin",



            "PlayerAssignment.bin", "Player.bin", "Tactics.bin", "TacticsFormation.bin",



            "Country.bin", "Ball.bin", "Boots.bin", "Glove.bin", "league_rules.json"



        };







        private void CreateInitialBackups(string folderPath)



        {



            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;



            string[] binFiles = Directory.GetFiles(folderPath, "*.bin");



            foreach (var binPath in binFiles)



            {



                try



                {



                    string bak = binPath + ".bak";



                    if (File.Exists(bak))



                    {



                        // Alto 34: rotar el .bak previo con timestamp en vez de



                        // conservar solo la primera copia (envenenable).



                        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");



                        string rotated = bak + "." + timeStr;



                        int suffix = 1;



                        while (File.Exists(rotated))



                        {



                            rotated = bak + "." + timeStr + "_" + suffix;



                            suffix++;



                        }



                        File.Move(bak, rotated);



                    }



                    File.Copy(binPath, bak, false);



                }



                catch { }



            }



            string rulesPath = Path.Combine(folderPath, "league_rules.json");



            if (File.Exists(rulesPath))



            {



                try



                {



                    string bak = rulesPath + ".bak";



                    if (File.Exists(bak))



                    {



                        // Alto 34: rotar el .bak previo con timestamp en vez de



                        // conservar solo la primera copia (envenenable).



                        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");



                        string rotated = bak + "." + timeStr;



                        int suffix = 1;



                        while (File.Exists(rotated))



                        {



                            rotated = bak + "." + timeStr + "_" + suffix;



                            suffix++;



                        }



                        File.Move(bak, rotated);



                    }



                    File.Copy(rulesPath, bak, false);



                }



                catch { }



            }



        }







        public string CreateTimestampedBackup(string folderPath)



        {



            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return null;



            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");



            string backupDir = Path.Combine(folderPath, "Backups_" + timeStr);



            int counter = 1;



            while (Directory.Exists(backupDir))



            {



                backupDir = Path.Combine(folderPath, "Backups_" + timeStr + "_" + counter);



                counter++;



            }



            Directory.CreateDirectory(backupDir);







            string[] binFiles = Directory.GetFiles(folderPath, "*.bin");



            foreach (var binPath in binFiles)



            {



                try



                {



                    string fileName = Path.GetFileName(binPath);



                    File.Copy(binPath, Path.Combine(backupDir, fileName), true);



                }



                catch { }



            }







            string rulesPath = Path.Combine(folderPath, "league_rules.json");



            if (File.Exists(rulesPath))



            {



                try



                {



                    File.Copy(rulesPath, Path.Combine(backupDir, "league_rules.json"), true);



                }



                catch { }



            }







            return backupDir;



        }







        public static void AtomicWriteFile(string targetPath, byte[] data)



        {



            if (string.IsNullOrEmpty(targetPath) || data == null) return;



            string dir = Path.GetDirectoryName(targetPath);



            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))



                Directory.CreateDirectory(dir);







            string sessionId = Guid.NewGuid().ToString("N");



            string tmpPath = targetPath + ".tmp_" + sessionId;



            File.WriteAllBytes(tmpPath, data);







            if (File.Exists(targetPath))



            {



                string bakPath = targetPath + ".savebak_" + sessionId;



                try



                {



                    if (File.Exists(bakPath)) File.Delete(bakPath);



                    File.Replace(tmpPath, targetPath, bakPath);



                    if (File.Exists(bakPath)) File.Delete(bakPath);



                }



                catch



                {



                    File.Copy(tmpPath, targetPath, true);



                    if (File.Exists(tmpPath)) File.Delete(tmpPath);



                }



            }



            else



            {



                File.Move(tmpPath, targetPath);



            }



        }







        public static void AtomicWriteText(string targetPath, string content, Encoding encoding)



        {



            if (string.IsNullOrEmpty(targetPath) || content == null) return;



            byte[] bytes = encoding.GetBytes(content);



            AtomicWriteFile(targetPath, bytes);



        }







        public List<byte> GetAvailableCompetitionIds()







        {







            var used = new HashSet<byte>(Competitions.Select(c => c.Id));







            var available = new List<byte>();







            // 1. Huecos libres nativos de Konami en el rango 1..145 (PRIORITARIOS)







            // Estos slots son 100% nativos del motor y reconocidos por las tablas fijas del juego







            int[][] nativeGaps = new int[][]







            {







                new int[] { 50, 55 },







                new int[] { 57, 65 },







                new int[] { 91, 96 },







                new int[] { 130, 136 },







                new int[] { 142, 144 }







            };







            foreach (var gap in nativeGaps)







            {







                for (int id = gap[0]; id <= gap[1]; id++)







                {







                    if (!used.Contains((byte)id))







                        available.Add((byte)id);







                }







            }







            // 2. Rango extendido (150 a 240)







            for (int id = 150; id <= 240; id++)







            {







                if (!used.Contains((byte)id))







                    available.Add((byte)id);







            }







            return available;







        }







        public ushort GetNextRegulationId()







        {







            var usedIds = new HashSet<ushort>(Regulations.Select(r => r.Id));







            // Buscar en el rango de ligas base (179 a 999) para evitar colisión con offsets de fase de grupos (>= 1024)







            for (ushort id = 179; id < 1000; id++)







            {







                if (!usedIds.Contains(id))







                    return id;







            }







            // M64: sin hueco en 179..999 no se devuelve maxId + 1 (podia



            // superar el limite de 1024 o envolver a 0 con maxId == 65535):



            // se informa en vez de entregar un ID invalido.



            throw new InvalidOperationException("No hay IDs de reglamento libres en el rango 179..999.");







        }







        public ushort GetNextEntryId()







        {







            // M64: primer hueco libre en vez de maxId + 1 (que envolvia



            // a 0 con maxId == 65535).



            var usedIds = new HashSet<ushort>(Entries.Select(e => e.Id));







            for (int id = 1; id <= 65535; id++)







            {







                if (!usedIds.Contains((ushort)id))







                    return (ushort)id;







            }







            throw new InvalidOperationException("No hay IDs de entrada libres en el rango 1..65535.");







        }







        public int GetPlayerCount(uint teamId)







        {







            int count;







            if (PlayersPerTeam.TryGetValue(teamId, out count))







                return count;







            return 0;







        }







        public uint GetNextFreeTeamId()







        {







            var usedIds = new HashSet<uint>(Teams.Select(t => t.Id));







            // Buscar en rango seguro de 16 bits (6001 a 65530) para no desbordar en el motor del juego







            for (uint candidate = 6001; candidate <= 65530; candidate++)







            {







                if (!usedIds.Contains(candidate))







                    return candidate;







            }







            throw new InvalidOperationException("No se encontraron IDs de equipo libres en el rango seguro de 16 bits (< 65535).");







        }







        // I30: IDs de equipos/competicion/reglamento clasicos con nombre



        // (antes literales 2703/2704/53/182 dispersos por el archivo).



        public const uint ClassicEuropeTeamId = 2703;



        public const uint ClassicWorldTeamId = 2704;



        public const byte ClassicTeamsCompetitionId = 53;



        public const ushort ClassicTeamsRegulationId = 182;







        // B45: equipos que juegan en ligas domesticas (copas y



        // supercopas no ocupan: un equipo de copa puede entrar en liga).



        // Solo decide el tipo de reglamento, sin fallback por nombre



        // (un "Superliga" no es una supercopa).



        private HashSet<uint> GetDomesticLeagueTeamIds()



        {



            var domestic = new HashSet<uint>();



            if (Entries == null) return domestic;



            foreach (var e in Entries)



            {



                var comp = Competitions != null ? Competitions.FirstOrDefault(c => c.Id == e.CompetitionId) : null;



                if (comp == null) continue;



                RegulationRecord reg = null;



                if (RegulationsByCompetition != null) RegulationsByCompetition.TryGetValue(comp.Id, out reg);



                if (IsCupOrSuperCupCompetition(reg)) continue;



                domestic.Add(e.TeamId);



            }



            return domestic;



        }







        private int GetDistinctPlayerCount(uint teamId)



        {



            if (PlayerAssignmentsByTeam == null) return 0;



            List<PlayerAssignmentRecord> roster;



            if (!PlayerAssignmentsByTeam.TryGetValue(teamId, out roster) || roster == null) return 0;



            return roster.Select(a => a.PlayerId).Distinct().Count();



        }







        public List<TeamRecord> GetFreeTeams()







        {







            var occupied = GetDomesticLeagueTeamIds();







            return Teams.Where(t => !occupied.Contains(t.Id)).ToList();







        }







        public List<TeamRecord> GetFreePlayableTeams(byte continent = 0)







        {







            // B45: ocupado = solo ligas domesticas (copas no ocupan).



            var occupied = GetDomesticLeagueTeamIds();







            // Equipos clasicos: van en su propia liga.



            var list = Teams.Where(t => !occupied.Contains(t.Id) && !t.IsNational && t.Id != ClassicEuropeTeamId && t.Id != ClassicWorldTeamId).ToList();







            // Si hay datos de PlayerAssignment, filtrar/priorizar equipos con al menos 11 jugadores







            if (PlayersPerTeam.Count > 0)







            {







                // B45: jugadores distintos, no filas de asignacion (un



                // jugador con filas duplicadas inflaba el conteo).



                var withSquad = list.Where(t => GetDistinctPlayerCount(t.Id) >= 11).ToList();







                if (withSquad.Count > 0)







                    list = withSquad;







            }







            // Ordenar dando prioridad al continente correspondiente si se especifica







            if (continent == 4) // Sudamérica (países 144 a 153)







            {







                list = list.OrderByDescending(t => (t.CountryId >= 144 && t.CountryId <= 153) ? 1 : 0)







                           .ThenByDescending(t => GetPlayerCount(t.Id))







                           .ToList();







            }







            else if (continent == 2) // Europa







            {







                list = list.OrderByDescending(t => (t.CountryId < 100 || (t.CountryId >= 200 && t.CountryId <= 240)) ? 1 : 0)







                           .ThenByDescending(t => GetPlayerCount(t.Id))







                           .ToList();







            }







            else







            {







                list = list.OrderByDescending(t => GetPlayerCount(t.Id)).ToList();







            }







            return list;







        }







        public bool IsTeamOccupied(uint teamId, out byte compId, out string compName)







        {







            compId = 0;







            compName = null;







            var entry = Entries.FirstOrDefault(e => e.TeamId == teamId);







            if (entry != null)







            {







                byte foundCompId = entry.CompetitionId;







                compId = foundCompId;







                var comp = Competitions.FirstOrDefault(c => c.Id == foundCompId);







                compName = comp != null ? comp.Name : ("Competición " + foundCompId);







                return true;







            }







            return false;







        }







        // Completado 2026-11-06: ocupacion en ligas domesticas (misma definicion que
        // GetFreePlayableTeams: entradas en competiciones que no son copa ni supercopa).
        public bool IsTeamInDomesticLeague(uint teamId, byte exceptCompId, out byte compId, out string compName)
        {
            compId = 0;
            compName = null;
            var domestic = GetDomesticLeagueTeamIds();
            if (!domestic.Contains(teamId)) return false;
            if (Entries != null)
            {
                foreach (var e in Entries)
                {
                    if (e.TeamId != teamId || e.CompetitionId == exceptCompId) continue;
                    RegulationRecord reg = null;
                    if (RegulationsByCompetition != null) RegulationsByCompetition.TryGetValue(e.CompetitionId, out reg);
                    if (IsCupOrSuperCupCompetition(reg)) continue;
                    compId = e.CompetitionId;
                    string nm = null;
                    if (reg != null && !string.IsNullOrEmpty(reg.NameSpanish)) nm = reg.NameSpanish;
                    if (string.IsNullOrEmpty(nm))
                    {
                        var comp = Competitions != null ? Competitions.FirstOrDefault(c => c.Id == e.CompetitionId) : null;
                        nm = comp != null ? comp.Name : null;
                    }
                    compName = string.IsNullOrEmpty(nm) ? ("Competición " + e.CompetitionId) : nm;
                    return true;
                }
            }
            return false;
        }

        public List<TeamRecord> GenerateAndAddCustomTeams(int count, string namePrefix, ushort countryId)







        {







            // M66: sin equipos no hay plantilla que clonar (antes



            // Teams[0] lanzaba IndexOutOfRangeException).



            if (Teams == null || Teams.Count == 0)



                throw new InvalidOperationException("No hay equipos cargados para usar como plantilla de equipos personalizados.");







            var newTeams = new List<TeamRecord>();







            var templateTeam = Teams.FirstOrDefault(t => t.CountryId == countryId) ?? Teams[0];







            string[] themedNames = GetThemedClubNames(countryId, count);







            // M66: IDs precalculados en una pasada (antes un



            // GetNextFreeTeamId por iteracion, O(n^2)) y rollback si el



            // rango se agota a mitad de la generacion.



            var usedTeamIds = new HashSet<uint>(Teams.Select(t => t.Id));



            uint nextTeamId = 6001;



            try



            {



            for (int i = 1; i <= count; i++)







            {







                while (nextTeamId <= 65530 && usedTeamIds.Contains(nextTeamId))



                    nextTeamId++;



                if (nextTeamId > 65530)



                    throw new InvalidOperationException("No se encontraron IDs de equipo libres en el rango seguro de 16 bits (< 65535).");



                uint newId = nextTeamId;



                usedTeamIds.Add(newId);



                nextTeamId++;







                string teamName;







                if (!string.IsNullOrEmpty(namePrefix) && namePrefix != "Club")







                {







                    teamName = string.Format("{0} {1}", namePrefix, i);







                }







                else







                {







                    teamName = (i - 1 < themedNames.Length) ? themedNames[i - 1] : string.Format("Club {0}", i);







                }







                string shortName = GenerateShortName(teamName, i);







                TeamRecord t = templateTeam.CloneWithNewId(newId, teamName, shortName);







                t.CountryId = countryId;







                t.IsNational = false;







                Teams.Add(t);







                TeamsById[newId] = t;







                newTeams.Add(t);







            }



            }



            catch



            {



                foreach (var added in newTeams)



                {



                    Teams.Remove(added);



                    TeamsById.Remove(added.Id);



                }



                throw;



            }







            HasUnsavedChanges = true;







            return newTeams;







        }







        public TeamRecord CloneTeam(uint originalTeamId, string customName)







        {







            if (!TeamsById.ContainsKey(originalTeamId))







                throw new ArgumentException("El equipo especificado no existe: " + originalTeamId);







            var original = TeamsById[originalTeamId];







            uint newId = GetNextFreeTeamId();







            string name = !string.IsNullOrEmpty(customName) ? customName : (original.NameSpanish + " (Clon)");







            var clone = original.CloneWithNewId(newId, name, original.ShortName);







            Teams.Add(clone);







            TeamsById[newId] = clone;







            HasUnsavedChanges = true;







            return clone;







        }







        public TeamRecord CreateNewTeam(string nameSpanish, string nameEnglish, string shortName, ushort countryId, ushort stadiumId = 0, uint coachId = 0, byte targetCompId = 0, uint explicitId = 0)



        {



            // B42: un ID explicito libre se respeta (0 = asignar uno nuevo).



            uint newId = explicitId;



            if (newId == 0)



            {



                newId = GetNextFreeTeamId();



            }



            else if (TeamsById != null && TeamsById.ContainsKey(newId))



            {



                throw new ArgumentException("El ID de equipo " + newId + " ya está en uso.");



            }



            var templateTeam = Teams.FirstOrDefault(x => x.CountryId == countryId) ?? (Teams.Count > 0 ? Teams[0] : null);







            TeamRecord newTeam;



            if (templateTeam != null)



            {



                newTeam = templateTeam.CloneWithNewId(newId, nameSpanish, shortName);



            }



            else



            {



                newTeam = new TeamRecord();



                newTeam.RawBytes = new byte[TeamRecord.RecordSize];



                newTeam.Id = newId;



            }







            newTeam.NameSpanish = !string.IsNullOrEmpty(nameSpanish) ? nameSpanish : ("Club " + newId);



            newTeam.NameEnglish = !string.IsNullOrEmpty(nameEnglish) ? nameEnglish : newTeam.NameSpanish;



            newTeam.ShortName = !string.IsNullOrEmpty(shortName) ? shortName : "CLB";



            newTeam.CountryId = countryId;







            // M67: estadio por defecto determinista: el generico KONAMI



            // Stadium (29) cuando existe, en vez del primero de la lista.



            if (stadiumId == 0)



            {



                var genericStadium = (Stadiums != null) ? Stadiums.FirstOrDefault(s => s.Id == 29) : null;



                stadiumId = (genericStadium != null) ? genericStadium.Id : ((Stadiums != null && Stadiums.Count > 0) ? Stadiums[0].Id : (ushort)29);



            }



            newTeam.StadiumId = stadiumId;







            // Solución definitiva Bug Jorge Jesus: asignación de entrenador válido y único



            if (coachId == 0 || (CoachesById != null && !CoachesById.ContainsKey(coachId)))



            {



                var newCoach = CreateNewCoach("DT " + newTeam.NameSpanish, countryId);



                newTeam.CoachId = newCoach.Id;



            }



            else



            {



                newTeam.CoachId = coachId;



            }







            newTeam.IsNational = false;



            newTeam.IsNew = true;



            newTeam.IsModified = true;



            // M67: el PadRight era codigo muerto (ToBytes escribe el



            // nombre con terminador en 24 bytes); queda el valor directo.



            newTeam.NameDatabase = "TEAM_" + newId;



            newTeam.RawBytes = newTeam.ToBytes();







            Teams.Add(newTeam);



            TeamsById[newId] = newTeam;







            if (targetCompId > 0)



            {



                AssignTeamToLeague(newId, targetCompId);



            }







            HasUnsavedChanges = true;



            return newTeam;



        }







        public bool DeleteTeam(uint teamId)



        {



            if (!TeamsById.ContainsKey(teamId)) return false;







            // M73: no vaciar una competicion estructural protegida



            // borrando un equipo que juega en ella.



            if (EntriesByCompetition != null)



            {



                foreach (var kvp in EntriesByCompetition)



                {



                    if (IsProtectedVanillaCompetition(kvp.Key) && kvp.Value.Any(e => e.TeamId == teamId))



                        throw new InvalidOperationException("El equipo " + teamId + " juega en la competicion estructural protegida " + kvp.Key + " y no puede eliminarse.");



                }



            }







            // 1. Quitar de competiciones



            RemoveTeamFromDomesticLeagues(teamId);







            // M68: quitar tambien plantillas, tacticas, derbis y



            // referencias del equipo (antes quedaban huerfanos).



            if (PlayerAssignmentsByTeam != null && PlayerAssignmentsByTeam.ContainsKey(teamId))



            {



                var roster = PlayerAssignmentsByTeam[teamId].ToList();



                if (PlayerAssignments != null)



                {



                    foreach (var pa in roster) PlayerAssignments.Remove(pa);



                }



                PlayerAssignmentsByTeam.Remove(teamId);



            }



            if (PlayersPerTeam != null && PlayersPerTeam.ContainsKey(teamId))



                PlayersPerTeam.Remove(teamId);



            if (TacticsByClub != null && TacticsByClub.ContainsKey(teamId))



            {



                var tacticsList = TacticsByClub[teamId].ToList();



                var tacticsKeys = tacticsList.Select(tr => tr.Key).ToList();



                if (Tactics != null)



                {



                    foreach (var tr in tacticsList) Tactics.Remove(tr);



                }



                if (TacticsFormations != null)



                {



                    foreach (var key in tacticsKeys) TacticsFormations.RemoveAll(f => f.TacticsKey == key);



                }



                if (FormationsByTacticsKey != null)



                {



                    foreach (var key in tacticsKeys) FormationsByTacticsKey.Remove(key);



                }



                TacticsByClub.Remove(teamId);



            }



            if (Derbies != null)



                Derbies.RemoveAll(d => d.TeamId1 == teamId || d.TeamId2 == teamId);



            if (Players != null)



            {



                foreach (var p in Players)



                {



                    if (p.ClubId == teamId)



                    {



                        p.ClubId = 0;



                        p.RawBytes = p.ToBytes();



                    }



                }



            }



            if (Teams != null)



            {



                foreach (var other in Teams)



                {



                    if (other.Id == teamId) continue;



                    bool touched = false;



                    if (other.Rival1 == teamId) { other.Rival1 = 0; touched = true; }



                    if (other.Rival2 == teamId) { other.Rival2 = 0; touched = true; }



                    if (other.Rival3 == teamId) { other.Rival3 = 0; touched = true; }



                    if (touched)



                    {



                        other.IsModified = true;



                        other.RawBytes = other.ToBytes();



                    }



                }



            }







            // 2. Quitar de listas maestras



            var t = TeamsById[teamId];



            Teams.Remove(t);



            TeamsById.Remove(teamId);







            HasUnsavedChanges = true;



            return true;



        }











        public void UpdateTeamRecord(TeamRecord t, string nameSpanish, string nameEnglish, string shortName, ushort countryId, ushort stadiumId, uint coachId, bool isNational)



        {



            if (t == null) return;



            t.NameSpanish = nameSpanish;



            t.NameEnglish = nameEnglish;



            t.ShortName = shortName;



            t.CountryId = countryId;



            t.StadiumId = stadiumId;



            t.CoachId = coachId;



            t.IsNational = isNational;



            t.IsModified = true;



            t.RawBytes = t.ToBytes();







            HasUnsavedChanges = true;



        }







        public void UpdateTeamRecordFull(TeamRecord t, string nameSpanish, string nameEnglish, string shortName, ushort countryId, ushort stadiumId, uint coachId, bool isNational, uint r1, uint r2, uint r3)



        {



            if (t == null) return;



            t.NameSpanish = nameSpanish;



            t.NameEnglish = nameEnglish;



            t.ShortName = shortName;



            t.CountryId = countryId;



            t.StadiumId = stadiumId;



            t.CoachId = coachId;



            t.IsNational = isNational;



            t.Rival1 = r1;



            t.Rival2 = r2;



            t.Rival3 = r3;



            t.IsModified = true;



            t.RawBytes = t.ToBytes();







            SetTeamRivals(t.Id, r1, r2, r3);



            HasUnsavedChanges = true;



        }







        // B44: centinela "sin rival" con nombre (0x3FFFF == 262143;



        // antes se comprobaban ambas formas literales).



        private const uint NoRivalSentinel = 0x3FFFF;







        public List<uint> GetTeamRivals(uint teamId)



        {



            var list = new HashSet<uint>();



            TeamRecord t;



            if (TeamsById.TryGetValue(teamId, out t) && t != null)



            {



                if (t.Rival1 > 0 && t.Rival1 != NoRivalSentinel && t.Rival1 != teamId) list.Add(t.Rival1);



                if (t.Rival2 > 0 && t.Rival2 != NoRivalSentinel && t.Rival2 != teamId) list.Add(t.Rival2);



                if (t.Rival3 > 0 && t.Rival3 != NoRivalSentinel && t.Rival3 != teamId) list.Add(t.Rival3);



            }







            if (Derbies != null)



            {



                foreach (var d in Derbies)



                {



                    // B44: el rival del derbi debe existir (antes se



                    // agregaban derbis huerfanos de equipos inexistentes).



                    if (d.TeamId1 == teamId && d.TeamId2 > 0 && d.TeamId2 != teamId && (TeamsById == null || TeamsById.ContainsKey(d.TeamId2))) list.Add(d.TeamId2);



                    else if (d.TeamId2 == teamId && d.TeamId1 > 0 && d.TeamId1 != teamId && (TeamsById == null || TeamsById.ContainsKey(d.TeamId1))) list.Add(d.TeamId1);



                }



            }







            return list.ToList();



        }







        public void SetTeamRivals(uint teamId, uint r1, uint r2, uint r3)



        {



            TeamRecord t;



            if (TeamsById.TryGetValue(teamId, out t) && t != null)



            {



                t.Rival1 = r1;



                t.Rival2 = r2;



                t.Rival3 = r3;



                t.IsModified = true;



                t.RawBytes = t.ToBytes();



            }







            if (Derbies != null)



            {



                Derbies.RemoveAll(d => d.TeamId1 == teamId || d.TeamId2 == teamId);



                uint[] rivals = new uint[] { r1, r2, r3 };



                foreach (var r in rivals)



                {



                    // B44: sin derbi para rivales inexistentes.



                    if (r > 0 && r != NoRivalSentinel && r != teamId && (TeamsById == null || TeamsById.ContainsKey(r)))



                    {



                        if (!Derbies.Any(d => (d.TeamId1 == teamId && d.TeamId2 == r) || (d.TeamId1 == r && d.TeamId2 == teamId)))



                        {



                            Derbies.Add(new DerbyRecord { TeamId1 = teamId, TeamId2 = r });



                        }



                    }



                }



            }







            HasUnsavedChanges = true;



        }







        public List<PlayerAssignmentRecord> GetTeamRoster(uint teamId)



        {



            List<PlayerAssignmentRecord> list;



            if (PlayerAssignmentsByTeam != null && PlayerAssignmentsByTeam.TryGetValue(teamId, out list))



            {



                return list.OrderBy(a => a.SquadOrder).ThenBy(a => a.ShirtNumber).ToList();



            }



            return new List<PlayerAssignmentRecord>();



        }







        public PlayerRecord GetPlayer(uint playerId)



        {



            PlayerRecord p;



            if (PlayersById != null && PlayersById.TryGetValue(playerId, out p))



                return p;



            return null;



        }







        public bool ChangePlayerShirtNumber(uint teamId, uint playerId, int newShirtNumber)



        {



            var roster = GetTeamRoster(teamId);



            var target = roster.FirstOrDefault(a => a.PlayerId == playerId);



            if (target == null) return false;







            // M69: el setter de ShirtNumber recorta en silencio a 1..99,



            // asi que el rango se valida aqui; y el dorsal debe ser unico



            // en la plantilla (antes se podia duplicar).



            if (newShirtNumber < 1 || newShirtNumber > 99) return false;



            if (roster.Any(a => a.PlayerId != playerId && a.ShirtNumber == newShirtNumber)) return false;







            target.ShirtNumber = newShirtNumber;



            target.RawBytes = target.ToBytes();



            HasUnsavedChanges = true;



            return true;



        }











        public bool TransferPlayer(uint playerId, uint fromTeamId, uint toTeamId)



        {



            if (fromTeamId == toTeamId) return false;



            if (PlayerAssignmentsByTeam == null || !PlayerAssignmentsByTeam.ContainsKey(fromTeamId)) return false;







            var fromList = PlayerAssignmentsByTeam[fromTeamId];



            var pa = fromList.FirstOrDefault(a => a.PlayerId == playerId);



            if (pa == null) return false;







            if (!PlayerAssignmentsByTeam.ContainsKey(toTeamId))



                PlayerAssignmentsByTeam[toTeamId] = new List<PlayerAssignmentRecord>();







            var toList = PlayerAssignmentsByTeam[toTeamId];



            if (toList.Count >= 32) return false; // Límite reglamentario de 32 futbolistas por plantilla







            // M69: el dorsal se resuelve ANTES de quitar al jugador del



            // origen, para poder rechazar sin dejarlo en ningun club.



            var usedShirts = new HashSet<int>(toList.Select(a => a.ShirtNumber));



            int newShirt = pa.ShirtNumber;



            if (usedShirts.Contains(newShirt))



            {



                newShirt = 1;



                while (usedShirts.Contains(newShirt) && newShirt < 99)



                    newShirt++;



                // Si 1..99 estan todos usados, no asignar un 99 duplicado.



                if (usedShirts.Contains(newShirt)) return false;



            }







            fromList.Remove(pa);



            if (PlayersPerTeam.ContainsKey(fromTeamId))



                PlayersPerTeam[fromTeamId] = Math.Max(0, PlayersPerTeam[fromTeamId] - 1);







            pa.TeamId = toTeamId;



            pa.ShirtNumber = newShirt;



            // M69: orden segun el maximo existente + 1 (antes Count, que



            // colisionaba si las ordenes no eran densas), con tope 31.



            pa.SquadOrder = Math.Min(31, toList.Count > 0 ? toList.Max(a => a.SquadOrder) + 1 : 0);



            // Limpiar roles especiales en nuevo club



            pa.IsCaptain = false;



            pa.IsPenalty = false;



            pa.IsShortFK = false;



            pa.IsLongFK = false;



            pa.IsLeftCorner = false;



            pa.IsRightCorner = false;



            pa.RawBytes = pa.ToBytes();







            toList.Add(pa);



            if (PlayersPerTeam.ContainsKey(toTeamId)) PlayersPerTeam[toTeamId]++;



            else PlayersPerTeam[toTeamId] = 1;







            // Sincronizar lista maestra de asignaciones para evitar divergencias



            if (PlayerAssignments != null && !PlayerAssignments.Contains(pa))



            {



                PlayerAssignments.Add(pa);



            }







            if (PlayersById != null && PlayersById.ContainsKey(playerId))



            {



                PlayersById[playerId].ClubId = toTeamId;



                PlayersById[playerId].RawBytes = PlayersById[playerId].ToBytes();



            }







            HasUnsavedChanges = true;



            return true;



        }







        // B43: punto de partida para el proximo ID libre (evita



        // re-escanear desde 150000 en cada creacion en bucle, O(n^2)).



        // Solo lo avanzan las creaciones, no las consultas.



        private uint _nextFreePlayerIdHint = 150000;







        public uint SuggestNextFreePlayerId()



        {



            uint candidate = _nextFreePlayerIdHint < 150000 ? 150000 : _nextFreePlayerIdHint;



            while (PlayersById != null && PlayersById.ContainsKey(candidate))



            {



                candidate++;



            }



            return candidate;



        }







        public PlayerRecord CreatePlayer(string name, string posName, int ovr, ushort natId, uint clubId = 0)



        {



            uint newId = SuggestNextFreePlayerId();



            byte[] raw = new byte[PlayerRecord.RecordSize];







            PlayerBitHelper.SetNationalityId(raw, natId);



            PlayerBitHelper.SetAge(raw, 24);



            PlayerBitHelper.SetHeight(raw, 180);



            PlayerBitHelper.SetWeight(raw, 75);



            PlayerBitHelper.SetFoot(raw, false);



            PlayerBitHelper.SetWeakFootUsage(raw, 2);



            PlayerBitHelper.SetWeakFootAccuracy(raw, 2);



            PlayerBitHelper.SetForm(raw, 5);



            PlayerBitHelper.SetInjuryResistance(raw, 1);







            int posIdx = PlayerRecord.GetPositionIndexByName(posName);



            PlayerBitHelper.SetPrimaryPosition(raw, posIdx);



            PlayerBitHelper.SetPositionCompatibility(raw, posIdx, 2);







            int clampedOvr = Math.Max(50, Math.Min(95, ovr));



            foreach (var k in PlayerBitHelper.DbAbilBits.Keys)



            {



                PlayerBitHelper.SetAbility(raw, k, clampedOvr);



            }







            var p = PlayerRecord.FromBytes(raw, 0);



            p.Id = newId;



            // B43/B54: nombre null y ToUpper dependiente de cultura.



            p.Name = name ?? "";



            p.ShirtName = (name ?? "").ToUpperInvariant();



            p.ClubId = clubId;



            p.PositionIndex = posIdx;



            p.PositionName = PlayerRecord.PositionNames[posIdx];



            p.Age = 24;



            p.Overall = p.ComputeOverall();



            p.RawBytes = p.ToBytes();







            Players.Add(p);



            if (PlayersById == null) PlayersById = new Dictionary<uint, PlayerRecord>();



            PlayersById[newId] = p;



            if (newId >= _nextFreePlayerIdHint) _nextFreePlayerIdHint = newId + 1;







            if (clubId > 0)



            {



                SignFreeAgent(newId, clubId);



            }







            HasUnsavedChanges = true;



            return p;



        }







        public PlayerRecord ClonePlayer(PlayerRecord source)



        {



            if (source == null || source.RawBytes == null) return null;



            uint newId = SuggestNextFreePlayerId();







            byte[] newRaw = (byte[])source.RawBytes.Clone();



            BitConverter.GetBytes(newId).CopyTo(newRaw, 8);







            string copyName = source.Name + " (Copia)";



            string copyShirt = source.ShirtName != null ? source.ShirtName : (source.Name ?? "").ToUpperInvariant();







            var p = PlayerRecord.FromBytes(newRaw, 0);



            p.Id = newId;



            p.Name = copyName;



            p.ShirtName = copyShirt;



            p.ClubId = 0; // Se crea como Agente Libre



            p.Age = source.Age;



            p.PositionIndex = source.PositionIndex;



            p.PositionName = source.PositionName;



            p.Overall = p.ComputeOverall();



            p.RawBytes = p.ToBytes();







            Players.Add(p);



            if (PlayersById == null) PlayersById = new Dictionary<uint, PlayerRecord>();



            PlayersById[newId] = p;



            if (newId >= _nextFreePlayerIdHint) _nextFreePlayerIdHint = newId + 1;



            HasUnsavedChanges = true;



            return p;



        }







        public bool DeletePlayer(uint playerId)



        {



            if (PlayersById == null || !PlayersById.ContainsKey(playerId)) return false;



            var p = PlayersById[playerId];







            if (p.ClubId > 0 && PlayerAssignmentsByTeam != null && PlayerAssignmentsByTeam.ContainsKey(p.ClubId))



            {



                ReleasePlayer(playerId, p.ClubId);



            }







            Players.Remove(p);



            PlayersById.Remove(playerId);



            HasUnsavedChanges = true;



            return true;



        }











        public bool ReleasePlayer(uint playerId, uint currentTeamId)



        {



            if (PlayerAssignmentsByTeam == null || !PlayerAssignmentsByTeam.ContainsKey(currentTeamId)) return false;







            var list = PlayerAssignmentsByTeam[currentTeamId];



            var pa = list.FirstOrDefault(a => a.PlayerId == playerId);



            if (pa == null) return false;







            list.Remove(pa);



            if (PlayerAssignments != null) PlayerAssignments.Remove(pa);



            if (PlayersPerTeam.ContainsKey(currentTeamId))



                PlayersPerTeam[currentTeamId] = Math.Max(0, PlayersPerTeam[currentTeamId] - 1);







            if (PlayersById != null && PlayersById.ContainsKey(playerId))



            {



                PlayersById[playerId].ClubId = 0;



                PlayersById[playerId].RawBytes = PlayersById[playerId].ToBytes();



            }







            HasUnsavedChanges = true;



            return true;



        }











        public bool SignFreeAgent(uint playerId, uint toTeamId, int shirtNumber = 0)



        {



            if (PlayersById == null || !PlayersById.ContainsKey(playerId)) return false;



            if (TeamsById == null || !TeamsById.ContainsKey(toTeamId)) return false;







            var p = PlayersById[playerId];



            if (p.ClubId != 0)



            {



                if (p.ClubId == toTeamId) return false; // Ya está registrado en este club



                return TransferPlayer(playerId, p.ClubId, toTeamId);



            }







            // Si por inconsistencia previa figurara en PlayerAssignments con otro club, transferir formalmente



            if (PlayerAssignments != null)



            {



                var existing = PlayerAssignments.FirstOrDefault(a => a.PlayerId == playerId);



                if (existing != null)



                {



                    if (existing.TeamId == toTeamId) return false;



                    return TransferPlayer(playerId, existing.TeamId, toTeamId);



                }



            }







            if (!PlayerAssignmentsByTeam.ContainsKey(toTeamId))



                PlayerAssignmentsByTeam[toTeamId] = new List<PlayerAssignmentRecord>();







            var toList = PlayerAssignmentsByTeam[toTeamId];



            if (toList.Any(a => a.PlayerId == playerId)) return false;



            if (toList.Count >= 32) return false; // Límite reglamentario de 32 futbolistas por plantilla







            var usedShirts = new HashSet<int>(toList.Select(a => a.ShirtNumber));



            if (shirtNumber <= 0 || usedShirts.Contains(shirtNumber))



            {



                shirtNumber = 1;



                while (usedShirts.Contains(shirtNumber) && shirtNumber < 99)



                    shirtNumber++;



                // M69: si 1..99 estan todos usados, no asignar un 99 duplicado.



                if (usedShirts.Contains(shirtNumber)) return false;



            }







            uint nextEid = (PlayerAssignments != null && PlayerAssignments.Count > 0) ? (PlayerAssignments.Max(a => a.EntryId) + 1) : 3400001;







            var pa = new PlayerAssignmentRecord



            {



                EntryId = nextEid,



                PlayerId = playerId,



                TeamId = toTeamId,



                RawBytes = new byte[PlayerAssignmentRecord.RecordSize],



                ShirtNumber = shirtNumber,



                // M69: orden segun el maximo existente + 1, con tope 31.



                SquadOrder = Math.Min(31, toList.Count > 0 ? toList.Max(a => a.SquadOrder) + 1 : 0)



            };



            pa.RawBytes = pa.ToBytes();







            if (PlayerAssignments != null) PlayerAssignments.Add(pa);



            toList.Add(pa);







            if (PlayersPerTeam.ContainsKey(toTeamId)) PlayersPerTeam[toTeamId]++;



            else PlayersPerTeam[toTeamId] = 1;







            p.ClubId = toTeamId;



            p.RawBytes = p.ToBytes();







            HasUnsavedChanges = true;



            return true;



        }











        public List<PlayerRecord> GetFreeAgents(string searchFilter = "", int limit = 200)



        {



            if (Players == null || Players.Count == 0) return new List<PlayerRecord>();







            var query = Players.Where(p => p.ClubId == 0);



            if (!string.IsNullOrEmpty(searchFilter))



            {



                query = query.Where(p => (p.Name != null && p.Name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||



                                         (p.ShirtName != null && p.ShirtName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||



                                         p.Id.ToString().Contains(searchFilter));



            }







            return query.Take(limit).ToList();



        }







        public bool SetTeamSetPieceRoles(uint teamId, uint captainPid, uint penaltyPid, uint longFkPid, uint shortFkPid, uint leftCornerPid, uint rightCornerPid)



        {



            var roster = GetTeamRoster(teamId);



            if (roster == null || roster.Count == 0) return false;







            foreach (var pa in roster)



            {



                pa.IsCaptain = (pa.PlayerId == captainPid);



                pa.IsPenalty = (pa.PlayerId == penaltyPid);



                pa.IsLongFK = (pa.PlayerId == longFkPid);



                pa.IsShortFK = (pa.PlayerId == shortFkPid);



                pa.IsLeftCorner = (pa.PlayerId == leftCornerPid);



                pa.IsRightCorner = (pa.PlayerId == rightCornerPid);



                pa.RawBytes = pa.ToBytes();



            }



            HasUnsavedChanges = true;



            return true;



        }







        public bool AutoAssignRoles(uint teamId)



        {



            var roster = GetTeamRoster(teamId);



            if (roster == null || roster.Count == 0) return false;







            var starters = roster.Where(a => a.SquadOrder < 11).ToList();



            if (starters.Count == 0) starters = roster.Take(11).ToList();







            Func<PlayerAssignmentRecord, string, int> getAbil = (pa, abilName) =>



            {



                PlayerRecord p;



                if (PlayersById != null && PlayersById.TryGetValue(pa.PlayerId, out p) && p != null && p.RawBytes != null)



                {



                    int bitPos;



                    if (PlayerBitHelper.DbAbilBits.TryGetValue(abilName, out bitPos))



                    {



                        return PlayerBitHelper.GetBits(p.RawBytes, bitPos, 6) + 40;



                    }



                    return p.Overall;



                }



                return 70;



            };







            Func<PlayerAssignmentRecord, int> getAge = pa =>



            {



                PlayerRecord p;



                if (PlayersById != null && PlayersById.TryGetValue(pa.PlayerId, out p) && p != null) return p.Age;



                return 25;



            };







            Func<PlayerAssignmentRecord, int> getOvr = pa =>



            {



                PlayerRecord p;



                if (PlayersById != null && PlayersById.TryGetValue(pa.PlayerId, out p) && p != null) return p.Overall;



                return 70;



            };







            var outfield = starters.Where(pa =>



            {



                PlayerRecord p;



                if (PlayersById != null && PlayersById.TryGetValue(pa.PlayerId, out p) && p != null)



                {



                    return p.PositionIndex != 0;



                }



                return pa.SquadOrder != 0;



            }).ToList();



            if (outfield.Count == 0) outfield = starters;







            var bestCaptain = starters.OrderByDescending(pa => getOvr(pa) * 2 + Math.Min(35, getAge(pa))).FirstOrDefault() ?? starters[0];



            var bestLongFk = outfield.OrderByDescending(pa => getAbil(pa, "place_kick") * 2 + getAbil(pa, "kick_pwr") * 2 + getAbil(pa, "swerve")).FirstOrDefault() ?? outfield[0];



            var bestShortFk = outfield.OrderByDescending(pa => getAbil(pa, "place_kick") * 2 + getAbil(pa, "swerve") * 2 + getAbil(pa, "finish")).FirstOrDefault() ?? outfield[0];



            var bestPenalty = outfield.OrderByDescending(pa => getAbil(pa, "finish") * 2 + getAbil(pa, "place_kick") + getAbil(pa, "kick_pwr")).FirstOrDefault() ?? outfield[0];



            var bestLeftCorner = outfield.OrderByDescending(pa => getAbil(pa, "swerve") * 2 + getAbil(pa, "loftpass") * 2 + getAbil(pa, "place_kick")).FirstOrDefault() ?? outfield[0];



            var bestRightCorner = outfield.OrderByDescending(pa => getAbil(pa, "swerve") * 2 + getAbil(pa, "loftpass") * 2 + getAbil(pa, "place_kick")).FirstOrDefault() ?? outfield[0];







            foreach (var pa in roster)



            {



                pa.IsCaptain = (pa.PlayerId == bestCaptain.PlayerId);



                pa.IsPenalty = (pa.PlayerId == bestPenalty.PlayerId);



                pa.IsLongFK = (pa.PlayerId == bestLongFk.PlayerId);



                pa.IsShortFK = (pa.PlayerId == bestShortFk.PlayerId);



                pa.IsLeftCorner = (pa.PlayerId == bestLeftCorner.PlayerId);



                pa.IsRightCorner = (pa.PlayerId == bestRightCorner.PlayerId);



                pa.RawBytes = pa.ToBytes();



            }







            HasUnsavedChanges = true;



            return true;



        }







        public string ExportTeamsToCsv(string outputPath)



        {



            // I22: ruta de salida explicita (antes File.WriteAllText



            // lanzaba ArgumentNullException sin contexto).



            if (string.IsNullOrEmpty(outputPath))



                throw new ArgumentException("La ruta de salida del CSV no puede estar vacia.");



            var sb = new StringBuilder();



            // Cabecera estándar PES2020Editor / Pes-Editor-Rebuilt (148 columnas)



            string header = "Id;Name;Country;Stadium;Coach;National;NameJapanese;NameSpanish;NameLatamSpanish;NameEnglish;NameUSEnglish;NameItalian;NamePortuguese;NameBrazilian;NameFrench;NameGerman;NameDutch;NameSwedish;NameRussian;NameGreek;NameTurkish;NameSimplifiedChinese;NameDatabase;NameEmpty1;NameEmpty2;NameEmpty3;NameEmpty4;ShortNameLicensed;ShortNameFake;Abbreviation;Commentary;StadiumFile;StadiumName;StadiumFileName;EmblemFile;EmblemFileName;Rival1;Rival2;Rival3;Banner1;Banner2;Banner3;Banner4;Kit1;Kit2;Kit3;Kit4;Kit5;Kit6;Kit7;Kit8;Kit9;Kit10;TeamColor1R;TeamColor1G;TeamColor1B;TeamColor2R;TeamColor2G;TeamColor2B;TurfPattern;SidelineColour;SeatColour;GoalStyle;NetPattern;GoalNettingDesign;GoalNettingColor1R;GoalNettingColor1G;GoalNettingColor1B;GoalNettingColor2R;GoalNettingColor2G;GoalNettingColor2B;Sponsor1;Sponsor2;Sponsor3;SponsorFile1;SponsorFile2;SponsorFile3;SponsorColorR;SponsorColorG;SponsorColorB;EditName;EditEmblem;EditStadium;EditStadiumName;EditStadiumFile;EditStadiumDetails;EditStadiumDetails2;EditRivals;EditBanners;EditTeamColors;EditCoach;EditSponsorFile;EditSponsorColors;EditSponsor;Edit1;Fake;LicencedPlayers;LicencedKits;LicencedCoach;LicencedCoach2;FeederTeam;ParentTeam;NonPlayableLeague;HasAnthem;AnthemStandingStyle;AnthemPlayersSinging;AnthemStandingAngle;Value1;Value2;Value3;Value4;Value5;Value6;Value7;ValueFF;Team2020_1;Team2020_2;Team2020_3;Team2020_4;Team2020_5;Team2020_6;Team2020_7;Team2020_8;Team2020_9;Team2020_10;Team2020_11;Country2020_1;Country2020_2;Country2020_3;Country2020_4;Country2020_5;Value2020_1;Value2020_2;Value2020_3;Value2020_4;Value2020_5;Value2020_6;Value2020_7;Value2020_8;Value2020_9;Value2020_10;Value2020_11;Value2020_12;Value2020_13;Value2020_14;Value2020_15;Value2020_16;Value2020_17";



            sb.AppendLine(header);







            int colCount = header.Split(';').Length;







            foreach (var t in Teams.OrderBy(x => x.Id))



            {



                string[] row = new string[colCount];



                for (int i = 0; i < colCount; i++) row[i] = "0";







                row[0] = t.Id.ToString();



                row[1] = !string.IsNullOrEmpty(t.NameSpanish) ? t.NameSpanish : t.NameEnglish;



                row[2] = t.CountryId.ToString();



                row[3] = t.StadiumId.ToString();



                row[4] = t.CoachId.ToString();



                row[5] = t.IsNational ? "True" : "False";



                row[7] = t.NameSpanish ?? "";



                row[9] = t.NameEnglish ?? "";



                row[22] = t.NameDatabase ?? "";



                row[27] = t.ShortName ?? "";



                row[29] = t.ShortName ?? "";



                row[36] = t.Rival1.ToString();



                row[37] = t.Rival2.ToString();



                row[38] = t.Rival3.ToString();







                sb.AppendLine(string.Join(";", row));



            }







            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);



            return outputPath;



        }







        // B42: Split con comillas (antes Split(';') partia nombres con ';').



        private static string[] SplitCsvLine(string line)



        {



            var fields = new List<string>();



            var current = new StringBuilder();



            bool inQuotes = false;



            for (int i = 0; i < line.Length; i++)



            {



                char ch = line[i];



                if (inQuotes)



                {



                    if (ch == '"')



                    {



                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }



                        else inQuotes = false;



                    }



                    else current.Append(ch);



                }



                else if (ch == '"') inQuotes = true;



                else if (ch == ';') { fields.Add(current.ToString()); current.Clear(); }



                else current.Append(ch);



            }



            fields.Add(current.ToString());



            return fields.ToArray();



        }







        public int ImportTeamsFromCsv(string csvPath)



        {



            LastCsvSkippedTeams = new List<string>();



            if (!File.Exists(csvPath)) return 0;







            string[] lines = File.ReadAllLines(csvPath, Encoding.UTF8);



            if (lines.Length <= 1) return 0;







            string[] header = SplitCsvLine(lines[0]);



            int idIdx = Array.IndexOf(header, "Id");



            int spNameIdx = Array.IndexOf(header, "NameSpanish");



            int enNameIdx = Array.IndexOf(header, "NameEnglish");



            int shortIdx = Array.IndexOf(header, "Abbreviation");



            int countryIdx = Array.IndexOf(header, "Country");



            int stadiumIdx = Array.IndexOf(header, "Stadium");



            int coachIdx = Array.IndexOf(header, "Coach");



            int nationalIdx = Array.IndexOf(header, "National");







            if (idIdx < 0) idIdx = 0;



            if (spNameIdx < 0) spNameIdx = 1;



            if (enNameIdx < 0) enNameIdx = 1;



            if (shortIdx < 0) shortIdx = 27;







            int count = 0;



            for (int i = 1; i < lines.Length; i++)



            {



                string line = lines[i].Trim();



                if (string.IsNullOrEmpty(line)) continue;



                string[] parts = SplitCsvLine(line);



                if (parts.Length <= idIdx) continue;







                uint tid;



                if (!uint.TryParse(parts[idIdx], out tid)) continue;







                string sp = (spNameIdx >= 0 && parts.Length > spNameIdx) ? parts[spNameIdx] : "";



                string en = (enNameIdx >= 0 && parts.Length > enNameIdx) ? parts[enNameIdx] : sp;



                string sh = (shortIdx >= 0 && parts.Length > shortIdx) ? parts[shortIdx] : "CLB";







                // B42: una celda no vacia que no parsea omite la fila



                // con aviso (antes se importaba con el fallback 144/29/0



                // en silencio).



                ushort cid = 144;



                if (countryIdx >= 0 && parts.Length > countryIdx && !string.IsNullOrEmpty(parts[countryIdx]) && !ushort.TryParse(parts[countryIdx], out cid))



                {



                    LastCsvSkippedTeams.Add(string.Format("ID {0}: pais no numerico '{1}'", tid, parts[countryIdx]));



                    continue;



                }







                ushort sid = 29;



                if (stadiumIdx >= 0 && parts.Length > stadiumIdx && !string.IsNullOrEmpty(parts[stadiumIdx]) && !ushort.TryParse(parts[stadiumIdx], out sid))



                {



                    LastCsvSkippedTeams.Add(string.Format("ID {0}: estadio no numerico '{1}'", tid, parts[stadiumIdx]));



                    continue;



                }







                uint coach = 0;



                if (coachIdx >= 0 && parts.Length > coachIdx && !string.IsNullOrEmpty(parts[coachIdx]) && !uint.TryParse(parts[coachIdx], out coach))



                {



                    LastCsvSkippedTeams.Add(string.Format("ID {0}: entrenador no numerico '{1}'", tid, parts[coachIdx]));



                    continue;



                }







                bool nat = false;



                if (nationalIdx >= 0 && parts.Length > nationalIdx && !string.IsNullOrEmpty(parts[nationalIdx]) && !bool.TryParse(parts[nationalIdx], out nat))



                {



                    LastCsvSkippedTeams.Add(string.Format("ID {0}: nacional no booleano '{1}'", tid, parts[nationalIdx]));



                    continue;



                }







                TeamRecord t;



                if (TeamsById.TryGetValue(tid, out t))



                {



                    // Alto 13 (mitad DB): no sobrescribir equipos del juego base



                    // sin aviso explicito. Criterio: equipo preexistente fuera del



                    // rango de creacion de la app (GetNextFreeTeamId: 6001..65530).



                    // Se omite y se reporta en LastCsvSkippedTeams; la confirmacion



                    // previa, el respaldo y el try/catch de UI los pone MainForm.



                    if (t.Id < 6001)



                    {



                        LastCsvSkippedTeams.Add(string.Format("ID {0} ({1})", t.Id, !string.IsNullOrEmpty(t.NameSpanish) ? t.NameSpanish : t.NameEnglish));



                        continue;



                    }



                    UpdateTeamRecord(t, sp, en, sh, cid, sid, coach, nat);



                }



                else



                {



                    // B42: el ID del CSV se respeta si esta libre (la



                    // reimportacion ya no crea un equipo nuevo cada vez).



                    CreateNewTeam(sp, en, sh, cid, sid, coach, 0, tid);



                }



                count++;



            }







            if (count > 0) HasUnsavedChanges = true;



            return count;



        }







        public List<TeamRecord> BatchCreateTeams(int count, string namePrefix, ushort countryId, byte targetCompId)



        {



            var list = new List<TeamRecord>();



            for (int i = 1; i <= count; i++)



            {



                string name = string.Format("{0} {1}", namePrefix, i);



                string sh = GenerateShortName(name, i);



                var t = CreateNewTeam(name, name, sh, countryId, 0, 0, targetCompId);



                list.Add(t);



            }



            HasUnsavedChanges = true;



            return list;



        }







        public static readonly Dictionary<byte, int> VanillaCompSlots = new Dictionary<byte, int>







        {







            { 9, 7 },     // England D1 (Premier League) -> Slot 7







            { 66, 50 },   // England D2 (Championship) -> Slot 50







            { 11, 8 },    // Spain D1 (La Liga) -> Slot 8







            { 67, 52 },   // Spain D2 (La Liga 2) -> Slot 52







            { 10, 9 },    // Italy D1 (Serie A) -> Slot 9







            { 69, 53 },   // Italy D2 (Serie B) -> Slot 53







            { 12, 10 },   // France D1 (Ligue 1) -> Slot 10







            { 68, 51 },   // France D2 (Ligue 2) -> Slot 51







            { 13, 11 },   // Netherlands D1 (Eredivisie) -> Slot 11







            { 14, 12 },   // Portugal D1 (Liga NOS) -> Slot 12







            { 21, 15 },   // Brazil D1 (Brasileirão Série A) -> Slot 15







            { 90, 99 },   // Brazil D2 (Brasileirão Série B) -> Slot 99







            { 22, 14 },   // Argentina D1 (Liga Profesional) -> Slot 14







            { 23, 16 },   // Chile D1 -> Slot 16







            { 122, 69 },  // Colombia D1 -> Slot 69







            { 111, 88 },  // Belgium D1 -> Slot 88







            { 114, 91 },  // Russia D1 -> Slot 91







            { 117, 96 },  // Switzerland D1 -> Slot 96







            { 119, 94 },  // Turkey D1 -> Slot 94







            { 128, 114 }, // Denmark D1 -> Slot 114







            { 137, 105 }, // Scotland D1 -> Slot 105







            { 125, 102 }, // China D1 -> Slot 102







            { 139, 119 }, // Thailand D1 -> Slot 119







            { 39, 22 }    // Liga PEU / Alemania -> Slot 22







        };







        public static readonly Dictionary<byte, ushort> VanillaCompCountries = new Dictionary<byte, ushort>







        {







            { 9, 204 },   // England D1 (204 = Inglaterra)







            { 66, 204 },  // England D2







            { 11, 236 },  // Spain D1 (236 = España)







            { 67, 236 },  // Spain D2







            { 10, 215 },  // Italy D1 (215 = Italia)







            { 69, 215 },  // Italy D2







            { 12, 208 },  // France D1 (208 = Francia)







            { 68, 208 },  // France D2







            { 13, 224 },  // Netherlands D1 (224 = Holanda)







            { 14, 228 },  // Portugal D1 (228 = Portugal)







            { 21, 146 },  // Brazil D1 (146 = Brasil)







            { 90, 146 },  // Brazil D2







            { 22, 144 },  // Argentina D1 (144 = Argentina)







            { 23, 147 },  // Chile D1 (147 = Chile)







            { 122, 148 }, // Colombia D1 (148 = Colombia)







            { 111, 197 }, // Belgium D1 (197 = Bélgica)







            { 114, 230 }, // Russia D1 (230 = Rusia)







            { 117, 238 }, // Switzerland D1 (238 = Suiza)







            { 119, 240 }, // Turkey D1 (240 = Turquía)







            { 128, 203 }, // Denmark D1 (203 = Dinamarca)







            { 137, 232 }, // Scotland D1 (232 = Escocia)







            { 125, 7 },   // China D1 (7 = China)







            { 139, 36 },  // Thailand D1 (36 = Tailandia)







            { 39, 210 }   // Alemania (210 = Alemania)







        };







        public ushort GetLeagueCountryId(byte compId)







        {







            return GetLeagueCountryId(compId, new HashSet<byte>());







        }







        // M70: sobrecarga con visitados para cortar ciclos A->B->A



        // (antes la recursion solo impedia el auto-bucle).



        private ushort GetLeagueCountryId(byte compId, HashSet<byte> visited)







        {







            if (!visited.Add(compId)) return 144;







            // 0. Revisar si tiene país asignado explícitamente en el editor







            if (CustomLeagueCountries != null && CustomLeagueCountries.ContainsKey(compId))







                return CustomLeagueCountries[compId];







            // 1. Diccionario de competiciones vanilla estándar de PES 2021







            ushort vanillaCountry;







            if (VanillaCompCountries.TryGetValue(compId, out vanillaCountry))







                return vanillaCountry;







            // 2. Si es una subdivisión ligada a un padre, hereda estrictamente el país del sistema doméstico padre







            RegulationRecord reg;







            if (RegulationsByCompetition.TryGetValue(compId, out reg) && reg.IdDivision1 != 0)







            {







                var parentReg = Regulations.FirstOrDefault(r => r.Id == reg.IdDivision1);







                if (parentReg != null && parentReg.IdCompetition != compId)







                {







                    return GetLeagueCountryId(parentReg.IdCompetition, visited);







                }







            }







            // 3. Si es una liga independiente, obtener el país predominante de sus clubes







            if (EntriesByCompetition.ContainsKey(compId) && EntriesByCompetition[compId].Count > 0)







            {







                var countryCounts = new Dictionary<ushort, int>();







                foreach (var entry in EntriesByCompetition[compId])







                {







                    TeamRecord t;







                    if (TeamsById.TryGetValue(entry.TeamId, out t) && t.CountryId > 0)







                    {







                        countryCounts[t.CountryId] = countryCounts.ContainsKey(t.CountryId) ? countryCounts[t.CountryId] + 1 : 1;







                    }







                }







                if (countryCounts.Count > 0)







                {







                    return countryCounts.OrderByDescending(kv => kv.Value).First().Key;







                }







            }







            // 4. Fallback por continente







            var comp = Competitions.FirstOrDefault(c => c.Id == compId);







            if (comp != null)







            {







                if (comp.Continent == 4) return 144; // Argentina / Sudamérica







                if (comp.Continent == 2) return 204; // Inglaterra / Europa







            }







            return 144;







        }







        public byte GetLeagueParentCompId(byte compId)



        {



            RegulationRecord reg;



            if (!RegulationsByCompetition.TryGetValue(compId, out reg) || reg == null || reg.IdDivision1 == 0)



                return 0;



            var parentReg = Regulations.FirstOrDefault(r => r.Id == reg.IdDivision1);



            return parentReg != null ? parentReg.IdCompetition : (byte)0;



        }







        public void SetLeagueParent(byte compId, byte parentCompId)



        {



            RegulationRecord reg;



            if (!RegulationsByCompetition.TryGetValue(compId, out reg) || reg == null)



                return;







            if (parentCompId == 0 || parentCompId == compId)



            {



                reg.IdDivision1 = 0;



                reg.IsDivision1 = true;



                reg.IsDivision2 = false;



            }



            else



            {



                RegulationRecord parentReg;



                if (RegulationsByCompetition.TryGetValue(parentCompId, out parentReg) && parentReg != null)



                {



                    reg.IdDivision1 = parentReg.Id;



                    reg.IsDivision1 = false;



                    reg.IsDivision2 = true;



                    // Enlazar division inferior en el padre



                    if (parentReg.IdDivision2 == 0)



                    {



                        parentReg.IdDivision2 = reg.Id;



                        parentReg.IsModified = true;



                        parentReg.RawBytes = parentReg.ToBytes();



                    }



                }



                else



                {



                    reg.IdDivision1 = 0;



                    reg.IsDivision1 = true;



                    reg.IsDivision2 = false;



                }



            }



            reg.IsModified = true;



            reg.RawBytes = reg.ToBytes();



            HasUnsavedChanges = true;



        }







        public int GetLeagueTier(byte compId)







        {







            RegulationRecord reg;







            if (!RegulationsByCompetition.TryGetValue(compId, out reg))







                return 1;







            if (reg.IdDivision1 == 0)







                return 1; // 1ra División / Independiente







            // Rastrear la cadena hacia arriba







            int tier = 1;







            ushort currentParentRegId = reg.IdDivision1;







            var visited = new HashSet<ushort>();







            while (currentParentRegId != 0 && !visited.Contains(currentParentRegId))







            {







                visited.Add(currentParentRegId);







                // M71: no contar el nivel si el padre no existe (antes



                // tier++ corria antes de comprobar parentReg == null).



                var parentReg = Regulations.FirstOrDefault(r => r.Id == currentParentRegId);







                if (parentReg != null)







                {







                    tier++;







                    currentParentRegId = parentReg.IdDivision1;







                }







                else







                {







                    break;







                }







            }







            return tier;







        }







        public Dictionary<byte, int> LeaguePromoteCount = new Dictionary<byte, int>();







        public Dictionary<byte, int> LeagueDemoteCount = new Dictionary<byte, int>();







        public Dictionary<byte, int> LeagueContinental1Count = new Dictionary<byte, int>();







        public Dictionary<byte, int> LeagueContinental2Count = new Dictionary<byte, int>();







        public int GetLeaguePromote(byte compId)







        {







            if (LeaguePromoteCount.ContainsKey(compId)) return LeaguePromoteCount[compId];







            int tier = GetLeagueTier(compId);







            return (tier > 1) ? 3 : 0;







        }







        public void SetLeaguePromote(byte compId, int count)







        {







            LeaguePromoteCount[compId] = Math.Max(0, Math.Min(8, count));







            HasUnsavedChanges = true;







        }







        public int GetLeagueDemote(byte compId)







        {







            if (LeagueDemoteCount.ContainsKey(compId)) return LeagueDemoteCount[compId];







            if (RegulationsByCompetition.ContainsKey(compId))







            {







                var reg = RegulationsByCompetition[compId];







                if (reg.IdDivision2 != 0) return 3;







            }







            int tier = GetLeagueTier(compId);







            return (tier == 1) ? 3 : 0;







        }







        public void SetLeagueDemote(byte compId, int count)







        {







            LeagueDemoteCount[compId] = Math.Max(0, Math.Min(8, count));







            HasUnsavedChanges = true;







        }







        public int GetLeagueContinental1(byte compId)







        {







            if (LeagueContinental1Count.ContainsKey(compId)) return LeagueContinental1Count[compId];







            if (RegulationsByCompetition.ContainsKey(compId))







            {







                // B47: si hay reglamento, su valor es el dato (incluso 0



                // plazas); el defecto 4 solo aplica sin reglamento (antes



                // un 0 explicito se leia como "sin dato" y daba 4).



                var reg = RegulationsByCompetition[compId];







                return reg.NumClassifiedTeams;







            }







            return 4; // Default Champions / Libertadores







        }







        public void SetLeagueContinental1(byte compId, int count)







        {







            // M65: el valor recortado es el que se guarda en ambos lados



            // (antes el diccionario recortaba a 8 pero el reglamento



            // recibia el valor sin recortar) y se re-sella RawBytes.



            int clamped = Math.Max(0, Math.Min(8, count));







            LeagueContinental1Count[compId] = clamped;







            if (RegulationsByCompetition.ContainsKey(compId))







            {







                var reg = RegulationsByCompetition[compId];







                reg.NumClassifiedTeams = (ushort)clamped;







                reg.IsModified = true;







                reg.RawBytes = reg.ToBytes();







            }







            HasUnsavedChanges = true;







        }







        public int GetLeagueContinental2(byte compId)







        {







            if (LeagueContinental2Count.ContainsKey(compId)) return LeagueContinental2Count[compId];







            return 2; // Default Europa League / Sudamericana







        }







        public void SetLeagueContinental2(byte compId, int count)







        {







            LeagueContinental2Count[compId] = Math.Max(0, Math.Min(8, count));







            HasUnsavedChanges = true;







        }







        public byte GetLeagueSeasonType(byte compId)







        {







            byte st;







            if (LeagueSeasonType != null && LeagueSeasonType.TryGetValue(compId, out st)) return st;







            ushort countryId = GetLeagueCountryId(compId);







            return IsCalendarYearCountry(countryId) ? (byte)1 : (byte)0;







        }







        public void SetLeagueSeasonType(byte compId, byte seasonType)







        {







            if (LeagueSeasonType == null) LeagueSeasonType = new Dictionary<byte, byte>();







            LeagueSeasonType[compId] = seasonType;







            HasUnsavedChanges = true;







        }







        public ushort GetLeagueLinkedCup(byte compId)







        {







            ushort cupId;







            if (LeagueLinkedCup != null && LeagueLinkedCup.TryGetValue(compId, out cupId)) return cupId;







            ushort countryId = GetLeagueCountryId(compId);







            var cups = GetAvailableDomesticCups(countryId);







            if (cups.Count > 0)







            {







                var reg = Regulations.FirstOrDefault(r => r.IdCompetition == cups[0].Id);







                if (reg != null) return (ushort)reg.Id;







            }







            return 0;







        }







        public void SetLeagueLinkedCup(byte compId, ushort cupRegId)







        {







            if (LeagueLinkedCup == null) LeagueLinkedCup = new Dictionary<byte, ushort>();







            LeagueLinkedCup[compId] = cupRegId;







            HasUnsavedChanges = true;







        }







        public ushort GetLeagueLinkedSuperCup(byte compId)







        {







            ushort scupId;







            if (LeagueLinkedSuperCup != null && LeagueLinkedSuperCup.TryGetValue(compId, out scupId)) return scupId;







            ushort countryId = GetLeagueCountryId(compId);







            var scups = GetAvailableSuperCups(countryId);







            if (scups.Count > 0)







            {







                var reg = Regulations.FirstOrDefault(r => r.IdCompetition == scups[0].Id);







                if (reg != null) return (ushort)reg.Id;







            }







            return 0;







        }







        public bool GetLeagueAperturaClausura(byte compId)



        {



            bool val;



            if (LeagueAperturaClausura != null && LeagueAperturaClausura.TryGetValue(compId, out val)) return val;



            return false;



        }







        public void SetLeagueAperturaClausura(byte compId, bool enabled, ushort clausuraRegId = 0)



        {



            if (LeagueAperturaClausura == null) LeagueAperturaClausura = new Dictionary<byte, bool>();



            if (LeagueClausuraRegulation == null) LeagueClausuraRegulation = new Dictionary<byte, ushort>();



            LeagueAperturaClausura[compId] = enabled;



            if (clausuraRegId > 0)



                LeagueClausuraRegulation[compId] = clausuraRegId;



            else if (!enabled)



                LeagueClausuraRegulation.Remove(compId);



            HasUnsavedChanges = true;



        }







        public ushort GetLeagueClausuraRegulation(byte compId)



        {



            ushort regId;



            if (LeagueClausuraRegulation != null && LeagueClausuraRegulation.TryGetValue(compId, out regId)) return regId;



            return 0;



        }







        public void SetLeagueLinkedSuperCup(byte compId, ushort scupRegId)







        {







            if (LeagueLinkedSuperCup == null) LeagueLinkedSuperCup = new Dictionary<byte, ushort>();



                if (LeagueAperturaClausura == null) LeagueAperturaClausura = new Dictionary<byte, bool>();



                if (LeagueClausuraRegulation == null) LeagueClausuraRegulation = new Dictionary<byte, ushort>();







            LeagueLinkedSuperCup[compId] = scupRegId;







            HasUnsavedChanges = true;







        }







        public static bool IsCalendarYearCountry(ushort countryId)







        {







            switch (countryId)







            {







                // Sudamérica (calendario Feb-Dic / anual)



                case 144: // Argentina







                case 145: // Bolivia







                case 146: // Brasil







                case 148: // Chile







                case 149: // Colombia







                case 151: // Ecuador







                case 154: // Paraguay







                case 155: // Peru







                case 157: // Uruguay







                case 158: // Venezuela







                // Asia (calendario alternativo)



                case 261: // China







                case 262: // Japon







                // CONCACAF (calendario alternativo)



                case 95:  // USA (MLS)







                case 89:  // Mexico







                    return true;







                default:







                    return false;







            }







        }







        // M74/M85: definicion unica de copa/supercopa por tipo de



        // reglamento (antes tres conjuntos distintos: {3,1}/{7,2,5} en



        // GetAvailableDomesticCups, {3}/{7,8} en piramide y {1,2,3,8} en



        // IsClubInDomesticLeague; el tipo 5, supercopa creada por



        // CreateNewLeague, caia como liga en piramide e IsClub).



        private static bool IsCupCompetitionType(byte competitionType)



        {



            return competitionType == 1 || competitionType == 3;



        }







        private static bool IsSuperCupCompetitionType(byte competitionType)



        {



            return competitionType == 2 || competitionType == 5 || competitionType == 7 || competitionType == 8;



        }







        private static bool IsCupOrSuperCupCompetition(RegulationRecord reg)



        {



            return reg != null && (IsCupCompetitionType(reg.CompetitionType) || IsSuperCupCompetitionType(reg.CompetitionType));



        }







        public List<CompetitionRecord> GetAvailableDomesticCups(ushort countryId = 0)







        {







            var list = new List<CompetitionRecord>();







            foreach (var c in Competitions)







            {







                string nameUpper = (c.Name ?? "").ToUpperInvariant();







                RegulationRecord reg;







                RegulationsByCompetition.TryGetValue(c.Id, out reg);







                bool isSuperCup = (reg != null && IsSuperCupCompetitionType(reg.CompetitionType)) ||







                                  nameUpper.Contains("SUPER CUP") || nameUpper.Contains("SUPERCOPA") || nameUpper.Contains("SHIELD") || nameUpper.Contains("SUPERTAÇA");







                if (isSuperCup) continue;







                bool isCup = (reg != null && IsCupCompetitionType(reg.CompetitionType)) ||







                             nameUpper.Contains("CUP") || nameUpper.Contains("COPA") || nameUpper.Contains("POKAL") || nameUpper.Contains("COUPE") || nameUpper.Contains("TAÇA");







                if (isCup)







                {







                    if (countryId == 0 || GetLeagueCountryId(c.Id) == countryId)







                    {







                        list.Add(c);







                    }







                }







            }







            return list;







        }







        public List<CompetitionRecord> GetAvailableSuperCups(ushort countryId = 0)







        {







            var list = new List<CompetitionRecord>();







            foreach (var c in Competitions)







            {







                string nameUpper = (c.Name ?? "").ToUpperInvariant();







                RegulationRecord reg;







                RegulationsByCompetition.TryGetValue(c.Id, out reg);







                bool isSuperCup = (reg != null && IsSuperCupCompetitionType(reg.CompetitionType)) ||







                                  nameUpper.Contains("SUPER CUP") || nameUpper.Contains("SUPERCOPA") || nameUpper.Contains("SHIELD") || nameUpper.Contains("SUPERTAÇA");







                if (isSuperCup)







                {







                    if (countryId == 0 || GetLeagueCountryId(c.Id) == countryId)







                    {







                        list.Add(c);







                    }







                }







            }







            return list;







        }







        private static List<string> CombineThemedPool(string[] part1, string[] part2, bool reverseOrder, int count)



        {



            var pool = new List<string>(part1.Length * part2.Length);



            for (int j = 0; j < part2.Length; j++)



            {



                for (int i = 0; i < part1.Length; i++)



                {



                    string name = reverseOrder 



                        ? string.Format("{0} {1}", part2[j], part1[i])



                        : string.Format("{0} {1}", part1[i], part2[j]);



                    pool.Add(name);



                }



            }







            var result = new List<string>();



            var used = new HashSet<string>();



            int step = 7;



            int idx = 0;







            for (int k = 0; k < pool.Count && result.Count < count; k++)



            {



                string candidate = pool[idx % pool.Count];



                if (!used.Contains(candidate))



                {



                    used.Add(candidate);



                    result.Add(candidate);



                }



                idx += step;



            }







            // Fallback seguro si count excede la cantidad total de combinaciones unicas



            int cycle = 2;



            while (result.Count < count)



            {



                for (int i = 0; i < pool.Count && result.Count < count; i++)



                {



                    string candidate = string.Format("{0} {1}", pool[i], cycle);



                    if (!used.Contains(candidate))



                    {



                        used.Add(candidate);



                        result.Add(candidate);



                    }



                }



                cycle++;



            }







            return result;



        }







        public static string[] GetThemedClubNames(ushort countryId, int count)



        {



            if (count <= 0) return new string[0];







            if (countryId == 144 || countryId == 145 || countryId == 148 || countryId == 149 || countryId == 151 || countryId == 154 || countryId == 155 || countryId == 157 || countryId == 158)



            {



                // Sudamerica / Hispano (20 prefijos x 34 ciudades = 680 combinaciones)



                string[] prefixes = new[] { "C.A.", "Club", "Deportivo", "Sportivo", "Atlético", "Unión", "Defensores de", "Estudiantes de", "Racing de", "Ferroviario", "San Martín de", "Huracán de", "Independiente de", "Juventud de", "Almirante", "Talleres de", "Central", "Gimnasia de", "Nueva", "Villa" };



                string[] cities = new[] { "Buenos Aires", "Rosario", "Córdoba", "Mendoza", "La Plata", "Santa Fe", "Tucumán", "Salta", "Mar del Plata", "Quilmes", "Avellaneda", "Lanús", "Banfield", "Tigre", "Morón", "Caseros", "Campana", "Junín", "Rafaela", "Resistencia", "Posadas", "Neuquén", "Bahía Blanca", "Paraná", "Santiago", "San Juan", "Jujuy", "Chaco", "Corrientes", "Formosa", "Trelew", "Comodoro", "Olavarría", "Pergamino" };



                return CombineThemedPool(prefixes, cities, false, count).ToArray();



            }



            else if (countryId == 204) // Inglaterra / UK (24 ciudades x 8 sufijos = 192 combinaciones)



            {



                string[] cities = new[] { "Bristol", "Sheffield", "Nottingham", "Derby", "Preston", "Blackburn", "Reading", "Plymouth", "Coventry", "Luton", "Oxford", "Cambridge", "Exeter", "Lincoln", "York", "Chester", "Carlisle", "Grimsby", "Mansfield", "Swindon", "Bradford", "Stockport", "Rotherham", "Shrewsbury" };



                string[] suffixes = new[] { "City", "United", "Town", "Rovers", "Athletic", "Wanderers", "Albion", "County" };



                return CombineThemedPool(cities, suffixes, false, count).ToArray();



            }



            else if (countryId == 236) // España (10 prefijos x 22 ciudades = 220 combinaciones)



            {



                string[] prefixes = new[] { "Real", "Deportivo", "U.D.", "C.D.", "Atlético", "Racing", "Sociedad Deportiva", "Unión Deportiva", "Agrupación Deportiva", "Club de Fútbol" };



                string[] cities = new[] { "Toledo", "Burgos", "Salamanca", "Badajoz", "Logroño", "Ourense", "Jaén", "Pontevedra", "Zamora", "Palencia", "Segovia", "Cuenca", "Ávila", "Teruel", "Mérida", "Guadalajara", "Alcoyano", "Algeciras", "Linares", "Sabadell", "Castellón", "Tarragona" };



                return CombineThemedPool(prefixes, cities, false, count).ToArray();



            }



            else if (countryId == 146) // Brasil (10 prefijos x 20 regiones = 200 combinaciones)



            {



                string[] prefixes = new[] { "Grêmio", "Esporte Clube", "Atlético", "Sociedade Esportiva", "Clube Atlético", "Associação Atlética", "América", "Nacional", "União", "Botafogo" };



                string[] regions = new[] { "Paulista", "Carioca", "Mineiro", "Gaúcho", "Paranaense", "Catarinense", "Baiano", "Pernambucano", "Cearense", "Goiano", "Paulistano", "Amazonense", "Campinense", "Santista", "Cuiabano", "Potiguar", "Alagoano", "Sergipano", "Paraense", "Maranhense" };



                return CombineThemedPool(prefixes, regions, false, count).ToArray();



            }



            else



            {



                // Generico profesional



                var result = new List<string>();



                for (int i = 1; i <= count; i++)



                {



                    result.Add(string.Format("Club Deportivo {0}", i));



                }



                return result.ToArray();



            }



        }







        public static string GenerateShortName(string fullName, int index)







        {







            if (string.IsNullOrEmpty(fullName)) return FallbackShortName(index);







            var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);







            if (parts.Length >= 2)







            {







                string mainPart = parts[parts.Length - 1];







                if (mainPart.Length >= 3)







                    return mainPart.Substring(0, 3).ToUpperInvariant();







                // I32: con ultima palabra corta se usa LA ULTIMA palabra



                // (antes parts[1]) y se completa a 3 caracteres con



                // letras de la primera palabra (antes podia dar 2).



                string candidate = (parts[0].Substring(0, 1) + mainPart.Substring(0, Math.Min(2, mainPart.Length))).ToUpperInvariant();



                int padIdx = 1;



                while (candidate.Length < 3 && padIdx < parts[0].Length)



                {



                    candidate += parts[0].Substring(padIdx, 1).ToUpperInvariant();



                    padIdx++;



                }



                if (candidate.Length >= 3) return candidate;



                return FallbackShortName(index);







            }







            if (fullName.Length >= 3)







                return fullName.Substring(0, 3).ToUpperInvariant();







            return FallbackShortName(index);







        }







        // I33: indice normalizado (negativo -> 0; >= 100 con su numero



        // completo para no colisionar con 0..99).



        private static string FallbackShortName(int index)



        {



            if (index < 0) index = 0;



            if (index < 100) return string.Format("T{0:D2}", index);



            return string.Format("T{0}", index);



        }







        public LeaguePyramidSystem GetPyramidForCountry(ushort countryId)







        {







            // B50: las competiciones del pais se calculan una vez en



            // GetAllPyramidSystems y se reutilizan (antes cada pais



            // re-recorria todas las competiciones).



            var countryComps = Competitions.Where(c => GetLeagueCountryId(c.Id) == countryId).ToList();







            return GetPyramidForCountryCore(countryId, countryComps);







        }







        private LeaguePyramidSystem GetPyramidForCountryCore(ushort countryId, List<CompetitionRecord> countryComps)







        {







            var system = new LeaguePyramidSystem();







            system.CountryId = countryId;







            CountryRecord cr;







            if (CountriesById.TryGetValue(countryId, out cr))







            {







                system.CountryName = !string.IsNullOrEmpty(cr.NameSpanish) ? cr.NameSpanish : cr.NameEnglish;







            }







            else if (countryId == 0)







            {







                system.CountryName = "Internacional";







            }







            else







            {







                system.CountryName = "País ID " + countryId;







            }







            var candidateLeagues = new List<CompetitionRecord>();







            foreach (var c in countryComps)







            {







                RegulationRecord reg;







                RegulationsByCompetition.TryGetValue(c.Id, out reg);







                string nameUpper = (c.Name ?? "").ToUpperInvariant();







                // M75: el filtro de torneos genericos ya no depende de countryId != 0 (antes el sistema internacional



                // incluia pruebas/amistosos).







                bool isGeneric = nameUpper.Contains("FAKE") || nameUpper.Contains("MATCHDAY") || 







                                 nameUpper.Contains("PRACTICE") || nameUpper.Contains("FRIENDLY") || 







                                 nameUpper.Contains("RETIREMENT") || nameUpper.Contains("KONAMI_LEAGUE");







                if (isGeneric) continue;







                bool isCup = (reg != null && IsCupCompetitionType(reg.CompetitionType)) || nameUpper.Contains("CUP") || nameUpper.Contains("COPA") || nameUpper.Contains("TAÇA") || nameUpper.Contains("POKAL");







                bool isSuperCup = (reg != null && IsSuperCupCompetitionType(reg.CompetitionType)) || nameUpper.Contains("SUPER");







                if (isSuperCup)







                {







                    system.SuperCups.Add(c);







                }







                else if (isCup)







                {







                    system.DomesticCups.Add(c);







                }







                else







                {







                    candidateLeagues.Add(c);







                }







            }







            int totalClubs = 0;







            foreach (var c in candidateLeagues)







            {







                RegulationRecord reg;







                RegulationsByCompetition.TryGetValue(c.Id, out reg);







                int tierNum = GetLeagueTier(c.Id);







                string tierTitle = (tierNum == 1) ? "Tier 1 • Primera División" : (tierNum == 2 ? "Tier 2 • Segunda División" : string.Format("Tier {0} • División Inferior", tierNum));







                // B49: sin entradas ni reglamento no se inventa 20.



                int tCount = EntriesByCompetition.ContainsKey(c.Id) ? EntriesByCompetition[c.Id].Count : (reg != null ? reg.TotalTeams : 0);







                totalClubs += tCount;







                system.Tiers.Add(new LeaguePyramidTier







                {







                    TierNumber = tierNum,







                    TierTitle = tierTitle,







                    Competition = c,







                    Regulation = reg,







                    TeamCount = tCount,







                    IsParityEven = (tCount % 2 == 0),







                    PromoteCount = GetLeaguePromote(c.Id),







                    DemoteCount = GetLeagueDemote(c.Id),







                    // B49: sin reglamento no se inventan 5 cambios ni 12



                    // suplentes; con reglamento se usa su valor real.



                    SubsCount = (reg != null) ? reg.Changes : 0,







                    BenchCount = (reg != null) ? Math.Max(0, reg.RegisteredPlayers - 11) : 0,







                    Continental1Count = GetLeagueContinental1(c.Id),







                    Continental2Count = GetLeagueContinental2(c.Id)







                });







            }







            system.Tiers = system.Tiers.OrderBy(t => t.TierNumber).ThenBy(t => t.Competition.Id).ToList();







            system.TotalClubs = totalClubs;







            // Validar balance de ascensos y descensos







            // M75: se agrupa por numero de tier (antes se comparaban



            // ligas del mismo tier entre si) y se informan TODOS los



            // desbalances (antes solo el primero); el mensaje de



            // equilibrio ya no usa solo el primer tier.



            if (system.Tiers.Count >= 2)







            {







                var tierGroups = system.Tiers.GroupBy(t => t.TierNumber).OrderBy(g => g.Key).ToList();







                bool allBalanced = true;







                var imbalanceList = new List<string>();







                for (int i = 0; i < tierGroups.Count - 1; i++)







                {







                    int demotes = tierGroups[i].Sum(t => t.DemoteCount);







                    int promotes = tierGroups[i + 1].Sum(t => t.PromoteCount);







                    if (demotes != promotes)







                    {







                        allBalanced = false;







                        imbalanceList.Add(string.Format("Desbalance entre Tier {0} ({1} descensos) y Tier {2} ({3} ascensos)",







                            tierGroups[i].Key, demotes, tierGroups[i + 1].Key, promotes));







                    }







                }







                system.IsBalanced = allBalanced;







                if (allBalanced)







                {







                    system.BalanceMessage = "Equilibrado (descensos y ascensos coinciden entre divisiones)";







                }







                else







                {







                    system.BalanceMessage = string.Join("; ", imbalanceList.ToArray());







                }







            }







            else







            {







                system.IsBalanced = true;







                system.BalanceMessage = "División única sin descensos enlazados";







            }







            return system;







        }







        public List<LeaguePyramidSystem> GetAllPyramidSystems()







        {







            // B50: el pais 0 (internacional) ya no se excluye, y el pais



            // de cada competicion se calcula una sola vez.



            var compsByCountry = new Dictionary<ushort, List<CompetitionRecord>>();



            if (Competitions != null)



            {



                foreach (var c in Competitions)



                {



                    ushort cid = GetLeagueCountryId(c.Id);



                    if (!compsByCountry.ContainsKey(cid)) compsByCountry[cid] = new List<CompetitionRecord>();



                    compsByCountry[cid].Add(c);



                }



            }







            var list = new List<LeaguePyramidSystem>();







            foreach (var kvp in compsByCountry)







            {







                var p = GetPyramidForCountryCore(kvp.Key, kvp.Value);







                if (p.Tiers.Count > 0)







                {







                    list.Add(p);







                }







            }







            // Ordenar: primero países con multi-tier (>=2 divisiones), luego alfabéticamente







            return list.OrderByDescending(p => p.Tiers.Count > 1).ThenBy(p => p.CountryName).ToList();







        }







        public int RepairLiveCpkEmblems()







        {







            string liveRoot = GetLiveCpkRoot();







            if (string.IsNullOrEmpty(liveRoot)) return 0;







            if (!ExternalWriteAllowed("repair-emblems")) return 0;







            return ImageProcessor.RepairLiveCpkEmblems(liveRoot);







        }







                public byte[] SerializeCustomLeagueRulesBytes()



        {



            try



            {



                var sb = new StringBuilder();



                sb.AppendLine("{");



                sb.AppendLine("  \"rules\": [");



                var allCompIds = new HashSet<byte>();



                if (LeaguePromoteCount != null) foreach (var k in LeaguePromoteCount.Keys) allCompIds.Add(k);



                if (LeagueDemoteCount != null) foreach (var k in LeagueDemoteCount.Keys) allCompIds.Add(k);



                if (LeagueContinental1Count != null) foreach (var k in LeagueContinental1Count.Keys) allCompIds.Add(k);



                if (LeagueContinental2Count != null) foreach (var k in LeagueContinental2Count.Keys) allCompIds.Add(k);



                if (CustomLeagueCountries != null) foreach (var k in CustomLeagueCountries.Keys) allCompIds.Add(k);



                if (LeagueSeasonType != null) foreach (var k in LeagueSeasonType.Keys) allCompIds.Add(k);



                if (LeagueLinkedCup != null) foreach (var k in LeagueLinkedCup.Keys) allCompIds.Add(k);



                if (LeagueLinkedSuperCup != null) foreach (var k in LeagueLinkedSuperCup.Keys) allCompIds.Add(k);



                if (LeagueAperturaClausura != null) foreach (var k in LeagueAperturaClausura.Keys) allCompIds.Add(k);







                int idx = 0;



                foreach (var id in allCompIds.OrderBy(x => x))



                {



                    // M80/B51: solo se escriben los campos presentes de



                    // forma explicita en los diccionarios (antes se



                    // emitia la regla completa con valores por defecto



                    // que la carga luego fijaba como si fueran datos).



                    var fields = new List<string>();



                    fields.Add(string.Format("\"id\": {0}", id));



                    if (CustomLeagueCountries != null && CustomLeagueCountries.ContainsKey(id))



                        fields.Add(string.Format("\"country\": {0}", CustomLeagueCountries[id]));



                    if (LeaguePromoteCount != null && LeaguePromoteCount.ContainsKey(id))



                        fields.Add(string.Format("\"promote\": {0}", LeaguePromoteCount[id]));



                    if (LeagueDemoteCount != null && LeagueDemoteCount.ContainsKey(id))



                        fields.Add(string.Format("\"demote\": {0}", LeagueDemoteCount[id]));



                    if (LeagueContinental1Count != null && LeagueContinental1Count.ContainsKey(id))



                        fields.Add(string.Format("\"continental1\": {0}", LeagueContinental1Count[id]));



                    if (LeagueContinental2Count != null && LeagueContinental2Count.ContainsKey(id))



                        fields.Add(string.Format("\"continental2\": {0}", LeagueContinental2Count[id]));



                    if (LeagueSeasonType != null && LeagueSeasonType.ContainsKey(id))



                        fields.Add(string.Format("\"season\": {0}", LeagueSeasonType[id]));



                    if (LeagueLinkedCup != null && LeagueLinkedCup.ContainsKey(id))



                        fields.Add(string.Format("\"cup\": {0}", LeagueLinkedCup[id]));



                    if (LeagueLinkedSuperCup != null && LeagueLinkedSuperCup.ContainsKey(id))



                        fields.Add(string.Format("\"scup\": {0}", LeagueLinkedSuperCup[id]));



                    if (LeagueAperturaClausura != null && LeagueAperturaClausura.ContainsKey(id) && LeagueAperturaClausura[id])



                    {



                        fields.Add("\"aperturaClausura\": true");



                        if (LeagueClausuraRegulation != null && LeagueClausuraRegulation.ContainsKey(id) && LeagueClausuraRegulation[id] > 0)



                            fields.Add(string.Format("\"clausuraRegulation\": {0}", LeagueClausuraRegulation[id]));



                    }



                    sb.Append("    {" + string.Join(", ", fields.ToArray()) + "}");



                    if (++idx < allCompIds.Count) sb.AppendLine(",");



                    else sb.AppendLine();



                }



                sb.AppendLine("  ]");



                sb.AppendLine("}");



                return Encoding.UTF8.GetBytes(sb.ToString());



            }



            catch



            {



                return null;



            }



        }







        public void SaveCustomLeagueRules(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            // M76/I22: sin carpeta no hay guardado silencioso.



            if (string.IsNullOrEmpty(folderPath))



                throw new InvalidOperationException("No se puede guardar league_rules.json: no hay carpeta de base de datos.");



            try



            {



                byte[] bytes = SerializeCustomLeagueRulesBytes();



                if (bytes == null)



                    throw new InvalidOperationException("No se pudieron serializar las reglas personalizadas de ligas.");



                AtomicWriteFile(Path.Combine(folderPath, "league_rules.json"), bytes);



            }



            catch (InvalidOperationException)



            {



                throw;



            }



            catch (Exception ex)



            {



                throw new InvalidOperationException("Fallo al guardar league_rules.json: " + ex.Message, ex);



            }



        }







        private void LoadCustomLeagueRules(string folderPath)







        {







            try







            {







                // M62/M77: recargar limpia las reglas previas y



                // garantiza diccionarios no nulos (antes sobrevivian a



                // la recarga y una referencia nula lanzaba NRE).



                if (CustomLeagueCountries == null) CustomLeagueCountries = new Dictionary<byte, ushort>();



                if (LeaguePromoteCount == null) LeaguePromoteCount = new Dictionary<byte, int>();



                if (LeagueDemoteCount == null) LeagueDemoteCount = new Dictionary<byte, int>();



                if (LeagueContinental1Count == null) LeagueContinental1Count = new Dictionary<byte, int>();



                if (LeagueContinental2Count == null) LeagueContinental2Count = new Dictionary<byte, int>();



                if (LeagueSeasonType == null) LeagueSeasonType = new Dictionary<byte, byte>();



                if (LeagueLinkedCup == null) LeagueLinkedCup = new Dictionary<byte, ushort>();



                if (LeagueLinkedSuperCup == null) LeagueLinkedSuperCup = new Dictionary<byte, ushort>();



                if (LeagueAperturaClausura == null) LeagueAperturaClausura = new Dictionary<byte, bool>();



                if (LeagueClausuraRegulation == null) LeagueClausuraRegulation = new Dictionary<byte, ushort>();



                CustomLeagueCountries.Clear();



                LeaguePromoteCount.Clear();



                LeagueDemoteCount.Clear();



                LeagueContinental1Count.Clear();



                LeagueContinental2Count.Clear();



                LeagueSeasonType.Clear();



                LeagueLinkedCup.Clear();



                LeagueLinkedSuperCup.Clear();



                LeagueAperturaClausura.Clear();



                LeagueClausuraRegulation.Clear();



                if (LeagueCupAllDivisions != null) LeagueCupAllDivisions.Clear();







                string path = Path.Combine(folderPath, "league_rules.json");







                if (!File.Exists(path)) return;







                string content = File.ReadAllText(path, Encoding.UTF8);







                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"\{\s*""id""\s*:\s*(\d+)(.*?)\}");







                foreach (System.Text.RegularExpressions.Match m in matches)







                {







                    // M78: un ID corrupto se ignora con aviso sin



                    // abortar la carga de las reglas restantes.



                    byte id;



                    if (!byte.TryParse(m.Groups[1].Value, out id))



                    {



                        LoadWarnings.Add(string.Format("Regla con ID corrupto '{0}', se ignora.", m.Groups[1].Value));



                        continue;



                    }







                    string rest = m.Groups[2].Value;







                    var mCountry = System.Text.RegularExpressions.Regex.Match(rest, @"""country""\s*:\s*(\d+)");







                    if (mCountry.Success)



                    {



                        ushort cId;



                        if (!ushort.TryParse(mCountry.Groups[1].Value, out cId))



                        {



                            LoadWarnings.Add(string.Format("Regla {0}: pais corrupto '{1}', se ignora ese campo.", id, mCountry.Groups[1].Value));



                        }



                        else if (cId == 0)



                        {



                            // M79: el 0 (internacional) se guarda



                            // explicito (antes la carga lo descartaba).



                            CustomLeagueCountries[id] = 0;



                        }



                        else



                        {



                            // Alto 25: una regla guardada por v3 sobre una liga oficial



                            // se omite con aviso en vez de romper la carga.



                            try { SetLeagueCountry(id, cId); }



                            catch (InvalidOperationException ex) { LoadWarnings.Add(string.Format("Regla de pais omitida para la competicion oficial {0}: {1}", id, ex.Message)); }



                        }



                    }







                    var mPromote = System.Text.RegularExpressions.Regex.Match(rest, @"""promote""\s*:\s*(\d+)");







                    if (mPromote.Success)



                    {



                        int v;



                        if (int.TryParse(mPromote.Groups[1].Value, out v)) LeaguePromoteCount[id] = v;



                        else LoadWarnings.Add(string.Format("Regla {0}: campo 'promote' corrupto '{1}', se ignora.", id, mPromote.Groups[1].Value));



                    }







                    var mDemote = System.Text.RegularExpressions.Regex.Match(rest, @"""demote""\s*:\s*(\d+)");







                    if (mDemote.Success)



                    {



                        int v;



                        if (int.TryParse(mDemote.Groups[1].Value, out v)) LeagueDemoteCount[id] = v;



                        else LoadWarnings.Add(string.Format("Regla {0}: campo 'demote' corrupto '{1}', se ignora.", id, mDemote.Groups[1].Value));



                    }







                    var mCont1 = System.Text.RegularExpressions.Regex.Match(rest, @"""continental1""\s*:\s*(\d+)");







                    if (mCont1.Success)



                    {



                        int v;



                        if (int.TryParse(mCont1.Groups[1].Value, out v)) LeagueContinental1Count[id] = v;



                        else LoadWarnings.Add(string.Format("Regla {0}: campo 'continental1' corrupto '{1}', se ignora.", id, mCont1.Groups[1].Value));



                    }







                    var mCont2 = System.Text.RegularExpressions.Regex.Match(rest, @"""continental2""\s*:\s*(\d+)");







                    if (mCont2.Success)



                    {



                        int v;



                        if (int.TryParse(mCont2.Groups[1].Value, out v)) LeagueContinental2Count[id] = v;



                        else LoadWarnings.Add(string.Format("Regla {0}: campo 'continental2' corrupto '{1}', se ignora.", id, mCont2.Groups[1].Value));



                    }







                    var mApClau = System.Text.RegularExpressions.Regex.Match(rest, @"""aperturaClausura""\s*:\s*(true|false|1|0)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);



                    if (mApClau.Success)



                    {



                        string val = mApClau.Groups[1].Value.ToLowerInvariant();



                        LeagueAperturaClausura[id] = (val == "true" || val == "1");



                    }



                    var mClauReg = System.Text.RegularExpressions.Regex.Match(rest, @"""clausuraRegulation""\s*:\s*(\d+)");



                    if (mClauReg.Success)



                    {



                        ushort clVal;



                        if (ushort.TryParse(mClauReg.Groups[1].Value, out clVal)) LeagueClausuraRegulation[id] = clVal;



                    }







                    var mSeason = System.Text.RegularExpressions.Regex.Match(rest, @"""season""\s*:\s*(\d+)");







                    if (mSeason.Success)



                    {



                        byte v;



                        if (byte.TryParse(mSeason.Groups[1].Value, out v)) LeagueSeasonType[id] = v;



                        else LoadWarnings.Add(string.Format("Regla {0}: campo 'season' corrupto '{1}', se ignora.", id, mSeason.Groups[1].Value));



                    }







                    var mCup = System.Text.RegularExpressions.Regex.Match(rest, @"""cup""\s*:\s*(\d+)");







                    if (mCup.Success)



                    {



                        ushort v;



                        if (ushort.TryParse(mCup.Groups[1].Value, out v)) LeagueLinkedCup[id] = v;



                        else LoadWarnings.Add(string.Format("Regla {0}: campo 'cup' corrupto '{1}', se ignora.", id, mCup.Groups[1].Value));



                    }







                    var mScup = System.Text.RegularExpressions.Regex.Match(rest, @"""scup""\s*:\s*(\d+)");







                    if (mScup.Success)



                    {



                        ushort v;



                        if (ushort.TryParse(mScup.Groups[1].Value, out v)) LeagueLinkedSuperCup[id] = v;



                        else LoadWarnings.Add(string.Format("Regla {0}: campo 'scup' corrupto '{1}', se ignora.", id, mScup.Groups[1].Value));



                    }







                }







            }







            catch (Exception ex)



            {



                // M78: el error de lectura se avisa en vez de tragarse.



                LoadWarnings.Add(string.Format("Error al cargar league_rules.json: {0}", ex.Message));



            }







        }







        public void EnsureFallbackCountries()
        {
            var defaults = new[]
            {
                new CountryRecord { Id = 0, Continent = 0, NameSpanish = "Internacional", NameEnglish = "International" },
                new CountryRecord { Id = 1, Continent = 2, NameSpanish = "AFGANISTÁN", NameEnglish = "AFGHANISTAN" },
                new CountryRecord { Id = 2, Continent = 2, NameSpanish = "BAHRÉIN", NameEnglish = "BAHRAIN" },
                new CountryRecord { Id = 3, Continent = 2, NameSpanish = "BANGLADESH", NameEnglish = "BANGLADESH" },
                new CountryRecord { Id = 4, Continent = 2, NameSpanish = "BUTÁN", NameEnglish = "BHUTAN" },
                new CountryRecord { Id = 5, Continent = 2, NameSpanish = "BRUNÉI", NameEnglish = "BRUNEI DARUSSALAM" },
                new CountryRecord { Id = 6, Continent = 3, NameSpanish = "CAMBOYA", NameEnglish = "CAMBODIA" },
                new CountryRecord { Id = 7, Continent = 3, NameSpanish = "CHINA", NameEnglish = "CHINA" },
                new CountryRecord { Id = 8, Continent = 3, NameSpanish = "HONG KONG", NameEnglish = "HONG KONG" },
                new CountryRecord { Id = 9, Continent = 3, NameSpanish = "INDIA", NameEnglish = "INDIA" },
                new CountryRecord { Id = 10, Continent = 3, NameSpanish = "INDONESIA", NameEnglish = "INDONESIA" },
                new CountryRecord { Id = 11, Continent = 3, NameSpanish = "IRÁN", NameEnglish = "IRAN" },
                new CountryRecord { Id = 12, Continent = 3, NameSpanish = "IRAK", NameEnglish = "IRAQ" },
                new CountryRecord { Id = 13, Continent = 3, NameSpanish = "JAPÓN", NameEnglish = "JAPAN" },
                new CountryRecord { Id = 14, Continent = 3, NameSpanish = "JORDANIA", NameEnglish = "JORDAN" },
                new CountryRecord { Id = 15, Continent = 3, NameSpanish = "COREA DEL NORTE", NameEnglish = "KOREA DPR" },
                new CountryRecord { Id = 16, Continent = 3, NameSpanish = "COREA DEL SUR", NameEnglish = "REPUBLIC OF KOREA" },
                new CountryRecord { Id = 17, Continent = 3, NameSpanish = "KUWAIT", NameEnglish = "KUWAIT" },
                new CountryRecord { Id = 18, Continent = 3, NameSpanish = "LAOS", NameEnglish = "LAOS" },
                new CountryRecord { Id = 19, Continent = 3, NameSpanish = "LÍBANO", NameEnglish = "LEBANON" },
                new CountryRecord { Id = 20, Continent = 3, NameSpanish = "MACAO", NameEnglish = "MACAU" },
                new CountryRecord { Id = 21, Continent = 3, NameSpanish = "MALASIA", NameEnglish = "MALAYSIA" },
                new CountryRecord { Id = 22, Continent = 3, NameSpanish = "MALDIVAS", NameEnglish = "MALDIVES" },
                new CountryRecord { Id = 23, Continent = 3, NameSpanish = "MONGOLIA", NameEnglish = "MONGOLIA" },
                new CountryRecord { Id = 24, Continent = 3, NameSpanish = "BIRMANIA", NameEnglish = "MYANMAR" },
                new CountryRecord { Id = 25, Continent = 3, NameSpanish = "NEPAL", NameEnglish = "NEPAL" },
                new CountryRecord { Id = 26, Continent = 3, NameSpanish = "OMÁN", NameEnglish = "OMAN" },
                new CountryRecord { Id = 27, Continent = 3, NameSpanish = "PAKISTÁN", NameEnglish = "PAKISTAN" },
                new CountryRecord { Id = 28, Continent = 3, NameSpanish = "PALESTINA", NameEnglish = "PALESTINE" },
                new CountryRecord { Id = 29, Continent = 3, NameSpanish = "FILIPINAS", NameEnglish = "PHILIPPINES" },
                new CountryRecord { Id = 30, Continent = 3, NameSpanish = "QATAR", NameEnglish = "QATAR" },
                new CountryRecord { Id = 31, Continent = 3, NameSpanish = "ARABIA SAUDITA", NameEnglish = "SAUDI ARABIA" },
                new CountryRecord { Id = 32, Continent = 3, NameSpanish = "SINGAPUR", NameEnglish = "SINGAPORE" },
                new CountryRecord { Id = 33, Continent = 3, NameSpanish = "SRI LANKA", NameEnglish = "SRI LANKA" },
                new CountryRecord { Id = 34, Continent = 3, NameSpanish = "SIRIA", NameEnglish = "SYRIA" },
                new CountryRecord { Id = 36, Continent = 3, NameSpanish = "TAILANDIA", NameEnglish = "THAILAND" },
                new CountryRecord { Id = 37, Continent = 3, NameSpanish = "EMIRATOS ÁRABES UNIDOS", NameEnglish = "UNITED ARAB EMIRATES" },
                new CountryRecord { Id = 38, Continent = 3, NameSpanish = "VIETNAM", NameEnglish = "VIETNAM" },
                new CountryRecord { Id = 39, Continent = 3, NameSpanish = "YEMEN", NameEnglish = "YEMEN" },
                new CountryRecord { Id = 40, Continent = 3, NameSpanish = "KIRGUISTÁN", NameEnglish = "KYRGYZSTAN" },
                new CountryRecord { Id = 41, Continent = 3, NameSpanish = "TAYIKISTÁN", NameEnglish = "TAJIKISTAN" },
                new CountryRecord { Id = 42, Continent = 3, NameSpanish = "TURKMENISTÁN", NameEnglish = "TURKMENISTAN" },
                new CountryRecord { Id = 43, Continent = 3, NameSpanish = "TIMOR ORIENTAL", NameEnglish = "TIMOR-LESTE" },
                new CountryRecord { Id = 44, Continent = 1, NameSpanish = "ARGELIA", NameEnglish = "ALGERIA" },
                new CountryRecord { Id = 45, Continent = 1, NameSpanish = "ANGOLA", NameEnglish = "ANGOLA" },
                new CountryRecord { Id = 46, Continent = 1, NameSpanish = "BENÍN", NameEnglish = "BENIN" },
                new CountryRecord { Id = 47, Continent = 1, NameSpanish = "BOTSUANA", NameEnglish = "BOTSWANA" },
                new CountryRecord { Id = 48, Continent = 1, NameSpanish = "BURKINA FASO", NameEnglish = "BURKINA FASO" },
                new CountryRecord { Id = 49, Continent = 1, NameSpanish = "BURUNDI", NameEnglish = "BURUNDI" },
                new CountryRecord { Id = 50, Continent = 1, NameSpanish = "CAMERÚN", NameEnglish = "CAMEROON" },
                new CountryRecord { Id = 51, Continent = 1, NameSpanish = "CABO VERDE", NameEnglish = "CABO VERDE" },
                new CountryRecord { Id = 52, Continent = 1, NameSpanish = "REP. CENTROAFRICANA", NameEnglish = "CENTRAL AFRICAN REP." },
                new CountryRecord { Id = 53, Continent = 1, NameSpanish = "CHAD", NameEnglish = "CHAD" },
                new CountryRecord { Id = 54, Continent = 1, NameSpanish = "COMORAS", NameEnglish = "COMOROS" },
                new CountryRecord { Id = 55, Continent = 1, NameSpanish = "REP. DEMOC. DE CONGO", NameEnglish = "CONGO DR" },
                new CountryRecord { Id = 56, Continent = 1, NameSpanish = "COSTA DE MARFIL", NameEnglish = "CÔTE D'IVOIRE" },
                new CountryRecord { Id = 57, Continent = 1, NameSpanish = "YIBUTI", NameEnglish = "DJIBOUTI" },
                new CountryRecord { Id = 58, Continent = 1, NameSpanish = "EGIPTO", NameEnglish = "EGYPT" },
                new CountryRecord { Id = 59, Continent = 1, NameSpanish = "GUINEA ECUATORIAL", NameEnglish = "EQUATORIAL GUINEA" },
                new CountryRecord { Id = 60, Continent = 1, NameSpanish = "ERITREA", NameEnglish = "ERITREA" },
                new CountryRecord { Id = 61, Continent = 1, NameSpanish = "ETIOPÍA", NameEnglish = "ETHIOPIA" },
                new CountryRecord { Id = 62, Continent = 1, NameSpanish = "GABÓN", NameEnglish = "GABON" },
                new CountryRecord { Id = 63, Continent = 1, NameSpanish = "GAMBIA", NameEnglish = "GAMBIA" },
                new CountryRecord { Id = 64, Continent = 1, NameSpanish = "GHANA", NameEnglish = "GHANA" },
                new CountryRecord { Id = 65, Continent = 1, NameSpanish = "GUINEA", NameEnglish = "GUINEA" },
                new CountryRecord { Id = 66, Continent = 1, NameSpanish = "GUINEA-BISSAU", NameEnglish = "GUINEA-BISSAU" },
                new CountryRecord { Id = 67, Continent = 1, NameSpanish = "KENIA", NameEnglish = "KENYA" },
                new CountryRecord { Id = 68, Continent = 1, NameSpanish = "LESOTO", NameEnglish = "LESOTHO" },
                new CountryRecord { Id = 69, Continent = 1, NameSpanish = "LIBERIA", NameEnglish = "LIBERIA" },
                new CountryRecord { Id = 70, Continent = 1, NameSpanish = "LIBIA", NameEnglish = "LIBYA" },
                new CountryRecord { Id = 71, Continent = 1, NameSpanish = "MADAGASCAR", NameEnglish = "MADAGASCAR" },
                new CountryRecord { Id = 72, Continent = 1, NameSpanish = "MALAUI", NameEnglish = "MALAWI" },
                new CountryRecord { Id = 73, Continent = 1, NameSpanish = "MALI", NameEnglish = "MALI" },
                new CountryRecord { Id = 74, Continent = 1, NameSpanish = "MAURITANIA", NameEnglish = "MAURITANIA" },
                new CountryRecord { Id = 75, Continent = 1, NameSpanish = "MAURICIO", NameEnglish = "MAURITIUS" },
                new CountryRecord { Id = 76, Continent = 1, NameSpanish = "MARRUECOS", NameEnglish = "MOROCCO" },
                new CountryRecord { Id = 77, Continent = 1, NameSpanish = "MOZAMBIQUE", NameEnglish = "MOZAMBIQUE" },
                new CountryRecord { Id = 78, Continent = 1, NameSpanish = "NAMIBIA", NameEnglish = "NAMIBIA" },
                new CountryRecord { Id = 79, Continent = 1, NameSpanish = "NÍGER", NameEnglish = "NIGER" },
                new CountryRecord { Id = 80, Continent = 1, NameSpanish = "NIGERIA", NameEnglish = "NIGERIA" },
                new CountryRecord { Id = 81, Continent = 1, NameSpanish = "RUANDA", NameEnglish = "RWANDA" },
                new CountryRecord { Id = 82, Continent = 1, NameSpanish = "SANTO TOMÉ Y PRÍNCIPE", NameEnglish = "SAO TOME AND PRINCIPE" },
                new CountryRecord { Id = 83, Continent = 1, NameSpanish = "SENEGAL", NameEnglish = "SENEGAL" },
                new CountryRecord { Id = 84, Continent = 1, NameSpanish = "SEYCHELLES", NameEnglish = "SEYCHELLES" },
                new CountryRecord { Id = 85, Continent = 1, NameSpanish = "SIERRA LEONA", NameEnglish = "SIERRA LEONE" },
                new CountryRecord { Id = 86, Continent = 1, NameSpanish = "SOMALIA", NameEnglish = "SOMALIA" },
                new CountryRecord { Id = 87, Continent = 1, NameSpanish = "SUDÁFRICA", NameEnglish = "SOUTH AFRICA" },
                new CountryRecord { Id = 88, Continent = 1, NameSpanish = "SUDÁN", NameEnglish = "SUDAN" },
                new CountryRecord { Id = 89, Continent = 1, NameSpanish = "ESUATINI", NameEnglish = "ESWATINI" },
                new CountryRecord { Id = 90, Continent = 1, NameSpanish = "TANZANIA", NameEnglish = "TANZANIA" },
                new CountryRecord { Id = 91, Continent = 1, NameSpanish = "TOGO", NameEnglish = "TOGO" },
                new CountryRecord { Id = 92, Continent = 1, NameSpanish = "TÚNEZ", NameEnglish = "TUNISIA" },
                new CountryRecord { Id = 93, Continent = 1, NameSpanish = "UGANDA", NameEnglish = "UGANDA" },
                new CountryRecord { Id = 94, Continent = 1, NameSpanish = "ZAMBIA", NameEnglish = "ZAMBIA" },
                new CountryRecord { Id = 95, Continent = 1, NameSpanish = "ZIMBABUE", NameEnglish = "ZIMBABWE" },
                new CountryRecord { Id = 98, Continent = 1, NameSpanish = "CONGO", NameEnglish = "CONGO" },
                new CountryRecord { Id = 103, Continent = 1, NameSpanish = "ANGUILA", NameEnglish = "ANGUILLA" },
                new CountryRecord { Id = 104, Continent = 1, NameSpanish = "ANTIGUA Y BARBUDA", NameEnglish = "ANTIGUA AND BARBUDA" },
                new CountryRecord { Id = 105, Continent = 2, NameSpanish = "ARUBA", NameEnglish = "ARUBA" },
                new CountryRecord { Id = 106, Continent = 2, NameSpanish = "BAHAMAS", NameEnglish = "BAHAMAS" },
                new CountryRecord { Id = 107, Continent = 2, NameSpanish = "BARBADOS", NameEnglish = "BARBADOS" },
                new CountryRecord { Id = 108, Continent = 2, NameSpanish = "BELICE", NameEnglish = "BELIZE" },
                new CountryRecord { Id = 109, Continent = 2, NameSpanish = "BERMUDAS", NameEnglish = "BERMUDA" },
                new CountryRecord { Id = 110, Continent = 2, NameSpanish = "CANADÁ", NameEnglish = "CANADA" },
                new CountryRecord { Id = 111, Continent = 2, NameSpanish = "ISLAS CAIMÁN", NameEnglish = "CAYMAN ISLANDS" },
                new CountryRecord { Id = 112, Continent = 2, NameSpanish = "COSTA RICA", NameEnglish = "COSTA RICA" },
                new CountryRecord { Id = 113, Continent = 2, NameSpanish = "CUBA", NameEnglish = "CUBA" },
                new CountryRecord { Id = 114, Continent = 2, NameSpanish = "DOMINICA", NameEnglish = "DOMINICA" },
                new CountryRecord { Id = 115, Continent = 2, NameSpanish = "REPÚBLICA DOMINICANA", NameEnglish = "DOMINICAN REPUBLIC" },
                new CountryRecord { Id = 116, Continent = 2, NameSpanish = "EL SALVADOR", NameEnglish = "EL SALVADOR" },
                new CountryRecord { Id = 117, Continent = 2, NameSpanish = "GRANADA", NameEnglish = "GRENADA" },
                new CountryRecord { Id = 119, Continent = 2, NameSpanish = "GUATEMALA", NameEnglish = "GUATEMALA" },
                new CountryRecord { Id = 120, Continent = 2, NameSpanish = "HAITÍ", NameEnglish = "HAITI" },
                new CountryRecord { Id = 121, Continent = 2, NameSpanish = "HONDURAS", NameEnglish = "HONDURAS" },
                new CountryRecord { Id = 122, Continent = 2, NameSpanish = "JAMAICA", NameEnglish = "JAMAICA" },
                new CountryRecord { Id = 124, Continent = 2, NameSpanish = "MÉXICO", NameEnglish = "MEXICO" },
                new CountryRecord { Id = 125, Continent = 2, NameSpanish = "MONTSERRAT", NameEnglish = "MONTSERRAT" },
                new CountryRecord { Id = 127, Continent = 2, NameSpanish = "NICARAGUA", NameEnglish = "NICARAGUA" },
                new CountryRecord { Id = 128, Continent = 2, NameSpanish = "PANAMÁ", NameEnglish = "PANAMA" },
                new CountryRecord { Id = 129, Continent = 2, NameSpanish = "PUERTO RICO", NameEnglish = "PUERTO RICO" },
                new CountryRecord { Id = 130, Continent = 2, NameSpanish = "SAN CRISTÓBAL Y NIEVES", NameEnglish = "SAINT KITTS AND NEVIS" },
                new CountryRecord { Id = 131, Continent = 2, NameSpanish = "SANTA LUCÍA", NameEnglish = "SAINT LUCIA" },
                new CountryRecord { Id = 132, Continent = 2, NameSpanish = "S. VICENTE Y GRANADINAS", NameEnglish = "ST VINCENT & GRENADINES" },
                new CountryRecord { Id = 133, Continent = 2, NameSpanish = "TRINIDAD Y TOBAGO", NameEnglish = "TRINIDAD AND TOBAGO" },
                new CountryRecord { Id = 134, Continent = 2, NameSpanish = "ISLAS TURCAS Y CAICOS", NameEnglish = "TURKS AND CAICOS IS." },
                new CountryRecord { Id = 135, Continent = 2, NameSpanish = "ESTADOS UNIDOS", NameEnglish = "USA" },
                new CountryRecord { Id = 136, Continent = 2, NameSpanish = "ISLAS VÍRGENES BRIT.", NameEnglish = "BRITISH VIRGIN ISLANDS" },
                new CountryRecord { Id = 137, Continent = 2, NameSpanish = "ISLAS VÍRGENES EE. UU.", NameEnglish = "US VIRGIN ISLANDS" },
                new CountryRecord { Id = 139, Continent = 2, NameSpanish = "SURINAM", NameEnglish = "SURINAME" },
                new CountryRecord { Id = 140, Continent = 2, NameSpanish = "CURAZAO", NameEnglish = "CURAÇAO" },
                new CountryRecord { Id = 144, Continent = 2, NameSpanish = "ARGENTINA", NameEnglish = "ARGENTINA" },
                new CountryRecord { Id = 145, Continent = 2, NameSpanish = "BOLIVIA", NameEnglish = "BOLIVIA" },
                new CountryRecord { Id = 146, Continent = 2, NameSpanish = "BRASIL", NameEnglish = "BRAZIL" },
                new CountryRecord { Id = 147, Continent = 2, NameSpanish = "CHILE", NameEnglish = "CHILE" },
                new CountryRecord { Id = 148, Continent = 2, NameSpanish = "COLOMBIA", NameEnglish = "COLOMBIA" },
                new CountryRecord { Id = 149, Continent = 2, NameSpanish = "ECUADOR", NameEnglish = "ECUADOR" },
                new CountryRecord { Id = 150, Continent = 2, NameSpanish = "PARAGUAY", NameEnglish = "PARAGUAY" },
                new CountryRecord { Id = 151, Continent = 2, NameSpanish = "PERÚ", NameEnglish = "PERU" },
                new CountryRecord { Id = 152, Continent = 2, NameSpanish = "URUGUAY", NameEnglish = "URUGUAY" },
                new CountryRecord { Id = 153, Continent = 2, NameSpanish = "VENEZUELA", NameEnglish = "VENEZUELA" },
                new CountryRecord { Id = 159, Continent = 2, NameSpanish = "GUYANA", NameEnglish = "GUYANA" },
                new CountryRecord { Id = 161, Continent = 3, NameSpanish = "SAMOA AMERICANA", NameEnglish = "AMERICAN SAMOA" },
                new CountryRecord { Id = 162, Continent = 2, NameSpanish = "AUSTRALIA", NameEnglish = "AUSTRALIA" },
                new CountryRecord { Id = 163, Continent = 3, NameSpanish = "ISLAS COOK", NameEnglish = "COOK ISLANDS" },
                new CountryRecord { Id = 164, Continent = 3, NameSpanish = "FIYI", NameEnglish = "FIJI" },
                new CountryRecord { Id = 165, Continent = 3, NameSpanish = "NUEVA CALEDONIA", NameEnglish = "NEW CALEDONIA" },
                new CountryRecord { Id = 166, Continent = 3, NameSpanish = "NUEVA ZELANDA", NameEnglish = "NEW ZEALAND" },
                new CountryRecord { Id = 167, Continent = 3, NameSpanish = "PAPÚA NUEVA GUINEA", NameEnglish = "PAPUA NEW GUINEA" },
                new CountryRecord { Id = 168, Continent = 3, NameSpanish = "SAMOA", NameEnglish = "SAMOA" },
                new CountryRecord { Id = 169, Continent = 3, NameSpanish = "ISLAS SALOMÓN", NameEnglish = "SOLOMON ISLANDS" },
                new CountryRecord { Id = 170, Continent = 3, NameSpanish = "TAHITÍ", NameEnglish = "TAHITI" },
                new CountryRecord { Id = 171, Continent = 3, NameSpanish = "TONGA", NameEnglish = "TONGA" },
                new CountryRecord { Id = 172, Continent = 4, NameSpanish = "VANUATU", NameEnglish = "VANUATU" },
                new CountryRecord { Id = 176, Continent = 3, NameSpanish = "GUAM", NameEnglish = "GUAM" },
                new CountryRecord { Id = 184, Continent = 3, NameSpanish = "PALAOS", NameEnglish = "PALAU" },
                new CountryRecord { Id = 189, Continent = 0, NameSpanish = "ISRAEL", NameEnglish = "ISRAEL" },
                new CountryRecord { Id = 190, Continent = 0, NameSpanish = "TURQUÍA", NameEnglish = "TURKEY" },
                new CountryRecord { Id = 191, Continent = 0, NameSpanish = "ALBANIA", NameEnglish = "ALBANIA" },
                new CountryRecord { Id = 192, Continent = 0, NameSpanish = "ANDORRA", NameEnglish = "ANDORRA" },
                new CountryRecord { Id = 193, Continent = 0, NameSpanish = "ARMENIA", NameEnglish = "ARMENIA" },
                new CountryRecord { Id = 194, Continent = 0, NameSpanish = "AUSTRIA", NameEnglish = "AUSTRIA" },
                new CountryRecord { Id = 195, Continent = 0, NameSpanish = "AZERBAIYÁN", NameEnglish = "AZERBAIJAN" },
                new CountryRecord { Id = 196, Continent = 0, NameSpanish = "BIELORRUSIA", NameEnglish = "BELARUS" },
                new CountryRecord { Id = 197, Continent = 0, NameSpanish = "BÉLGICA", NameEnglish = "BELGIUM" },
                new CountryRecord { Id = 198, Continent = 0, NameSpanish = "BOSNIA-HERZEGOVINA", NameEnglish = "BOSNIA AND HERZEGOVINA" },
                new CountryRecord { Id = 199, Continent = 0, NameSpanish = "BULGARIA", NameEnglish = "BULGARIA" },
                new CountryRecord { Id = 200, Continent = 0, NameSpanish = "CROACIA", NameEnglish = "CROATIA" },
                new CountryRecord { Id = 201, Continent = 0, NameSpanish = "CHIPRE", NameEnglish = "CYPRUS" },
                new CountryRecord { Id = 202, Continent = 0, NameSpanish = "REPÚBLICA CHECA", NameEnglish = "CZECH REPUBLIC" },
                new CountryRecord { Id = 203, Continent = 0, NameSpanish = "DINAMARCA", NameEnglish = "DENMARK" },
                new CountryRecord { Id = 204, Continent = 0, NameSpanish = "INGLATERRA", NameEnglish = "ENGLAND" },
                new CountryRecord { Id = 205, Continent = 0, NameSpanish = "ESTONIA", NameEnglish = "ESTONIA" },
                new CountryRecord { Id = 206, Continent = 0, NameSpanish = "ISLAS FEROE", NameEnglish = "FAROE ISLANDS" },
                new CountryRecord { Id = 207, Continent = 0, NameSpanish = "FINLANDIA", NameEnglish = "FINLAND" },
                new CountryRecord { Id = 208, Continent = 0, NameSpanish = "FRANCIA", NameEnglish = "FRANCE" },
                new CountryRecord { Id = 209, Continent = 0, NameSpanish = "GEORGIA", NameEnglish = "GEORGIA" },
                new CountryRecord { Id = 210, Continent = 0, NameSpanish = "ALEMANIA", NameEnglish = "GERMANY" },
                new CountryRecord { Id = 211, Continent = 0, NameSpanish = "GRECIA", NameEnglish = "GREECE" },
                new CountryRecord { Id = 212, Continent = 0, NameSpanish = "HUNGRÍA", NameEnglish = "HUNGARY" },
                new CountryRecord { Id = 213, Continent = 0, NameSpanish = "ISLANDIA", NameEnglish = "ICELAND" },
                new CountryRecord { Id = 214, Continent = 0, NameSpanish = "IRLANDA", NameEnglish = "IRELAND" },
                new CountryRecord { Id = 215, Continent = 0, NameSpanish = "ITALIA", NameEnglish = "ITALY" },
                new CountryRecord { Id = 216, Continent = 0, NameSpanish = "KAZAJISTÁN", NameEnglish = "KAZAKHSTAN" },
                new CountryRecord { Id = 217, Continent = 0, NameSpanish = "LETONIA", NameEnglish = "LATVIA" },
                new CountryRecord { Id = 218, Continent = 0, NameSpanish = "LIECHTENSTEIN", NameEnglish = "LIECHTENSTEIN" },
                new CountryRecord { Id = 219, Continent = 0, NameSpanish = "LITUANIA", NameEnglish = "LITHUANIA" },
                new CountryRecord { Id = 220, Continent = 0, NameSpanish = "LUXEMBURGO", NameEnglish = "LUXEMBOURG" },
                new CountryRecord { Id = 221, Continent = 0, NameSpanish = "MACEDONIA DEL NORTE", NameEnglish = "NORTH MACEDONIA" },
                new CountryRecord { Id = 222, Continent = 0, NameSpanish = "MALTA", NameEnglish = "MALTA" },
                new CountryRecord { Id = 223, Continent = 0, NameSpanish = "MOLDAVIA", NameEnglish = "MOLDOVA" },
                new CountryRecord { Id = 224, Continent = 0, NameSpanish = "HOLANDA", NameEnglish = "NETHERLANDS" },
                new CountryRecord { Id = 225, Continent = 0, NameSpanish = "IRLANDA DEL NORTE", NameEnglish = "NORTHERN IRELAND" },
                new CountryRecord { Id = 226, Continent = 0, NameSpanish = "NORUEGA", NameEnglish = "NORWAY" },
                new CountryRecord { Id = 227, Continent = 0, NameSpanish = "POLONIA", NameEnglish = "POLAND" },
                new CountryRecord { Id = 228, Continent = 0, NameSpanish = "PORTUGAL", NameEnglish = "PORTUGAL" },
                new CountryRecord { Id = 229, Continent = 0, NameSpanish = "RUMANÍA", NameEnglish = "ROMANIA" },
                new CountryRecord { Id = 230, Continent = 0, NameSpanish = "RUSIA", NameEnglish = "RUSSIA" },
                new CountryRecord { Id = 231, Continent = 0, NameSpanish = "SAN MARINO", NameEnglish = "SAN MARINO" },
                new CountryRecord { Id = 232, Continent = 0, NameSpanish = "ESCOCIA", NameEnglish = "SCOTLAND" },
                new CountryRecord { Id = 234, Continent = 0, NameSpanish = "ESLOVAQUIA", NameEnglish = "SLOVAKIA" },
                new CountryRecord { Id = 235, Continent = 0, NameSpanish = "ESLOVENIA", NameEnglish = "SLOVENIA" },
                new CountryRecord { Id = 236, Continent = 0, NameSpanish = "ESPAÑA", NameEnglish = "SPAIN" },
                new CountryRecord { Id = 237, Continent = 0, NameSpanish = "SUECIA", NameEnglish = "SWEDEN" },
                new CountryRecord { Id = 238, Continent = 0, NameSpanish = "SUIZA", NameEnglish = "SWITZERLAND" },
                new CountryRecord { Id = 239, Continent = 0, NameSpanish = "UCRANIA", NameEnglish = "UKRAINE" },
                new CountryRecord { Id = 240, Continent = 3, NameSpanish = "UZBEKISTÁN", NameEnglish = "UZBEKISTAN" },
                new CountryRecord { Id = 241, Continent = 1, NameSpanish = "GALES", NameEnglish = "WALES" },
                new CountryRecord { Id = 245, Continent = 0, NameSpanish = "GIBRALTAR", NameEnglish = "GIBRALTAR" },
                new CountryRecord { Id = 250, Continent = 0, NameSpanish = "MÓNACO", NameEnglish = "MONACO" },
                new CountryRecord { Id = 260, Continent = 4, NameSpanish = "Otros", NameEnglish = "Others" },
                new CountryRecord { Id = 298, Continent = 3, NameSpanish = "CHINA TAIPÉI", NameEnglish = "CHINESE TAIPEI" },
                new CountryRecord { Id = 303, Continent = 0, NameSpanish = "SERBIA", NameEnglish = "SERBIA" },
                new CountryRecord { Id = 304, Continent = 0, NameSpanish = "MONTENEGRO", NameEnglish = "MONTENEGRO" },
                new CountryRecord { Id = 311, Continent = 0, NameSpanish = "KOSOVO", NameEnglish = "KOSOVO" },
                new CountryRecord { Id = 312, Continent = 1, NameSpanish = "SUDÁN DEL SUR", NameEnglish = "SOUTH SUDAN" },
            };
            foreach (var c in defaults)
            {
                if (!CountriesById.ContainsKey(c.Id))
                {
                    Countries.Add(c);
                    CountriesById[c.Id] = c;
                }
            }
        }







        public void EnsureFallbackStadiums()



        {



            if (Stadiums.Count == 0)



            {



                var defaults = new[]



                {



                    new StadiumRecord { Id = 2, Name = "Camp Nou", NameJapanese = "Camp Nou" },



                    new StadiumRecord { Id = 5, Name = "Wembley Stadium connected by EE", NameJapanese = "Wembley Stadium" },



                    new StadiumRecord { Id = 6, Name = "Stadio Olimpico", NameJapanese = "Stadio Olimpico" },



                    new StadiumRecord { Id = 7, Name = "Old Trafford", NameJapanese = "Old Trafford" },



                    new StadiumRecord { Id = 10, Name = "Estadio Jose Alvalade", NameJapanese = "Estadio Jose Alvalade" },



                    new StadiumRecord { Id = 11, Name = "Allianz Arena", NameJapanese = "Allianz Arena" },



                    new StadiumRecord { Id = 13, Name = "Metropole Arena", NameJapanese = "Metropole Arena" },



                    new StadiumRecord { Id = 14, Name = "Estadio do Morumbi", NameJapanese = "Estadio do Morumbi" },



                    new StadiumRecord { Id = 15, Name = "Estadio Urbano Caldeira", NameJapanese = "Estadio Urbano Caldeira" },



                    new StadiumRecord { Id = 16, Name = "Allianz Parque", NameJapanese = "Allianz Parque" },



                    new StadiumRecord { Id = 17, Name = "El Monumental", NameJapanese = "El Monumental" },



                    new StadiumRecord { Id = 18, Name = "Alberto J. Armando (La Bombonera)", NameJapanese = "La Bombonera" },



                    new StadiumRecord { Id = 29, Name = "KONAMI Stadium", NameJapanese = "KONAMI Stadium" },



                    new StadiumRecord { Id = 30, Name = "Neu Sonne Arena", NameJapanese = "Neu Sonne Arena" },



                    new StadiumRecord { Id = 31, Name = "Metropolitano Stadium", NameJapanese = "Metropolitano" },



                    new StadiumRecord { Id = 32, Name = "Estadio Campeon del Siglo", NameJapanese = "Campeon del Siglo" },



                    new StadiumRecord { Id = 40, Name = "Saitama Stadium 2002", NameJapanese = "Saitama Stadium" },



                    new StadiumRecord { Id = 47, Name = "San Siro / Giuseppe Meazza", NameJapanese = "San Siro" },



                    new StadiumRecord { Id = 50, Name = "St. Jakob-Park", NameJapanese = "St. Jakob-Park" },



                    new StadiumRecord { Id = 53, Name = "Emirates Stadium", NameJapanese = "Emirates Stadium" }



                };



                foreach (var s in defaults)



                {



                    s.RawBytes = s.ToBytes();



                    Stadiums.Add(s);



                    StadiumsById[s.Id] = s;



                }



            }



        }







        public void EnsureFallbackCoaches()



        {



            if (Coaches.Count == 0)



            {



                var defaults = new[]



                {



                    new CoachRecord { Id = 2, Name = "JORGE JESUS", CountryId = 228 },



                    new CoachRecord { Id = 3, Name = "H. GOMEZ", CountryId = 148 },



                    new CoachRecord { Id = 232056, Name = "G. SOUTHGATE", CountryId = 204 },



                    new CoachRecord { Id = 231532, Name = "S. CLARKE", CountryId = 232 },



                    new CoachRecord { Id = 233532, Name = "S. KENNY", CountryId = 214 },



                    new CoachRecord { Id = 132400, Name = "T. AZRIA", CountryId = 208 }



                };



                foreach (var c in defaults)



                {



                    c.RawBytes = c.ToBytes();



                    Coaches.Add(c);



                    CoachesById[c.Id] = c;



                }



            }



        }







        public CoachRecord CreateNewCoach(string name, ushort countryId)



        {



            uint newCoachId = Coaches.Count > 0 ? (Coaches.Max(c => c.Id) + 1) : 300001;



            if (newCoachId < 300000) newCoachId = 300001;



            while (CoachesById.ContainsKey(newCoachId))



                newCoachId++;







            var coach = new CoachRecord();



            coach.Id = newCoachId;



            coach.CountryId = countryId;



            coach.Name = !string.IsNullOrEmpty(name) ? name : ("Coach " + newCoachId);



            coach.NameJapanese = coach.Name;



            coach.RawBytes = coach.ToBytes();







            Coaches.Add(coach);



            CoachesById[newCoachId] = coach;



            HasUnsavedChanges = true;



            return coach;



        }







        public StadiumRecord CreateNewStadium(string name, ushort stadiumId = 0)



        {



            if (stadiumId != 0)



            {



                if (StadiumsById.ContainsKey(stadiumId) || Stadiums.Any(s => s.Id == stadiumId))



                    throw new InvalidOperationException("El ID de estadio " + stadiumId + " ya está en uso.");



            }



            else



            {



                var usedStadiumIds = new HashSet<ushort>(Stadiums.Select(s => s.Id));



                ushort nextId = 1;



                while (nextId < 65535 && usedStadiumIds.Contains(nextId))



                    nextId++;



                if (usedStadiumIds.Contains(nextId))



                    throw new InvalidOperationException("No hay IDs libres para nuevos estadios (límite de 65535 alcanzado).");



                stadiumId = nextId;



            }







            var stad = new StadiumRecord();



            stad.Id = stadiumId;



            stad.Name = !string.IsNullOrEmpty(name) ? name : ("Estadio " + stadiumId);



            stad.NameJapanese = stad.Name;



            stad.RawBytes = stad.ToBytes();







            Stadiums.Add(stad);



            StadiumsById[stadiumId] = stad;



            HasUnsavedChanges = true;



            return stad;



        }







        public void EnsureClassicTeamsCompetition()







        {







            // 1. Buscar si ya existe la competición de equipos clásicos







            var classicComp = Competitions.FirstOrDefault(c =>







                c.Name.IndexOf("CLASSIC", StringComparison.OrdinalIgnoreCase) >= 0 ||







                (RegulationsByCompetition.ContainsKey(c.Id) &&







                 RegulationsByCompetition[c.Id].NameSpanish != null &&







                 RegulationsByCompetition[c.Id].NameSpanish.IndexOf("CLÁSIC", StringComparison.OrdinalIgnoreCase) >= 0));







            byte compId;







            if (classicComp == null)







            {







                // Asignar el ID clasico si está libre, o el siguiente hueco libre







                compId = ClassicTeamsCompetitionId;







                if (Competitions.Any(c => c.Id == compId))







                {







                    var freeIds = GetAvailableCompetitionIds();







                    compId = (freeIds.Count > 0) ? freeIds[0] : (byte)(Competitions.Max(c => c.Id) + 1);







                }







                ushort regId = ClassicTeamsRegulationId;







                while (Regulations.Any(r => r.Id == regId)) regId++;







                // Buscar template comp







                var templateComp = Competitions.FirstOrDefault(c => c.Continent == 1) ?? Competitions[0];







                classicComp = CompetitionRecord.FromBytes(templateComp.RawBytes, 0);







                classicComp.Id = compId;







                classicComp.Name = "CLASSIC_TEAMS";







                classicComp.Continent = 1; // Internacional / Mundial







                classicComp.Group = 0;







                classicComp.Licenced = true;







                classicComp.Fake = false;







                classicComp.IdParentCompetition = 0;







                classicComp.IsNew = true;







                classicComp.RawBytes = classicComp.ToBytes();







                Competitions.Add(classicComp);







                // I31: ordenar en su sitio para no reemplazar la



                // instancia de la lista (los bindings quedaban obsoletos).



                Competitions.Sort((a, b) => a.Id.CompareTo(b.Id));







                // Buscar template reg (Tipo Liga)



                var templateReg = Regulations.FirstOrDefault(r => r.CompetitionType == 4) ?? Regulations.FirstOrDefault();



                var reg = (templateReg != null && templateReg.RawBytes != null && templateReg.RawBytes.Length >= 2352)



                    ? RegulationRecord.FromBytes(templateReg.RawBytes, 0)



                    : new RegulationRecord { RawBytes = new byte[2352] };







                reg.Id = regId;







                reg.IdCompetition = compId;







                reg.CompetitionType = 4; // Tipo Liga







                reg.TotalTeams = 2;







                reg.RegisteredPlayers = 32;







                reg.Changes = 5;







                reg.NumGroups = 1;







                reg.LeagueClassification = 1;







                reg.CupFormat = 0;







                reg.LeagueEachTeamMatches = 2;







                reg.IsDivision1 = true;







                reg.IsDivision2 = false;







                reg.IdDivision1 = 0;







                reg.IdDivision2 = 0;







                reg.NameSpanish = "Equipos Clásicos";







                reg.NameLatamSpanish = "Equipos Clásicos";







                reg.NameEnglish = "Classic Teams";







                reg.Name = "CLASSIC_TEAMS";







                reg.IsNew = true;







                reg.RawBytes = reg.ToBytes();







                Regulations.Add(reg);







                RegulationsByCompetition[compId] = reg;







            }







            else







            {







                compId = classicComp.Id;







            }







            // 2. Gestionar asignación de equipos clásicos (2703 European Classics y 2704 World Classics)







            if (!EntriesByCompetition.ContainsKey(compId))







                EntriesByCompetition[compId] = new List<EntryRecord>();







            uint[] classicTids = new uint[] { ClassicEuropeTeamId, ClassicWorldTeamId };







            foreach (uint tid in classicTids)







            {







                // Si ya está asignado en la competición de clásicos, no hacer nada



                if (Entries.Any(e => e.TeamId == tid && e.CompetitionId == compId))



                    continue;







                // Alto 35: No extraer clásicos de ligas existentes del usuario ni sustituir arbitrariamente.



                // Solo asignar a clásicos si el equipo no está asignado en ninguna competición.



                if (!Entries.Any(e => e.TeamId == tid))



                {



                    byte pos = (byte)(EntriesByCompetition[compId].Count + 1);



                    var entry = new EntryRecord



                    {



                        TeamId = tid,



                        Id = GetNextEntryId(),



                        CompetitionId = compId,



                        Position = pos,



                        Value1 = 0,



                        Value2 = 0,



                        Padding = 0,



                        RawBytes = new byte[EntryRecord.RecordSize],



                        IsNew = true



                    };



                    entry.RawBytes = entry.ToBytes();



                    Entries.Add(entry);



                    EntriesByCompetition[compId].Add(entry);



                    HasUnsavedChanges = true;



                }







            }







            // Actualizar TotalTeams en RegulationRecord según la cantidad real de equipos asignados







            if (RegulationsByCompetition.ContainsKey(compId))







            {







                var r = RegulationsByCompetition[compId];







                byte actualCount = (byte)EntriesByCompetition[compId].Count;







                if (actualCount >= 2 && r.TotalTeams != actualCount)







                {







                    r.TotalTeams = actualCount;







                    r.IsModified = true;







                    r.RawBytes = r.ToBytes();







                }







            }







            // País Internacional (0)







            // Alto 25: si la guarda vanilla rechaza esta comp. (p. ej. una liga



            // del usuario cuyo nombre contiene "CLASSIC"), no romper la carga:



            // se omite y se registra. La comp. de clasicos real queda exenta



            // por IsNew / reglamento >= 179.



            try { SetLeagueCountry(compId, 0); }



            catch (InvalidOperationException ex) { LoadWarnings.Add(string.Format("Pais de la competicion de clasicos omitido por la guarda vanilla: {0}", ex.Message)); }







            // 3. Garantizar emblemas en LiveCPK







            EnsureClassicEmblems(compId);







        }







        public void EnsureClassicEmblems(int compId)







        {







            string liveRoot = GetLiveCpkRoot();







            if (string.IsNullOrEmpty(liveRoot)) return;







            if (!ExternalWriteAllowed("classic-emblem")) return;







            string embPath = Path.Combine(liveRoot, "common", "render", "symbol", "emblemLc", string.Format("emb_{0:D4}_l.png", compId));







            if (!File.Exists(embPath))







            {







                try







                {







                    using (var bmp = ImageProcessor.CreateClassicEmblem())







                    {







                        ImageProcessor.SaveCompetitionEmblems(bmp, liveRoot, compId);







                    }







                }







                catch { }







            }







        }







        public void CreateNewLeague(







            byte compId,







            string nameAscii,







            string nameSpanish,







            string nameLatamSpanish,







            string nameEnglish,







            byte continent,







            byte totalTeams,







            byte compType,







            bool isIndependent,







            byte parentCompId,







            List<uint> teamIds,







            ushort countryId = 0,







            byte changes = 3,







            byte registeredPlayers = 18,







            byte rounds = 2,







            byte tieBreaker = 1,







            byte cupFormat = 0,







            bool extraTime = false,







            bool penalties = false,







            bool awayGoals = false,







            bool thirdPlace = false,







            bool singleLeg = false,







            int promote = -1,







            int demote = -1,







            int continental1 = 4,







            int continental2 = 2,







            byte numGroups = 1,







            ushort numClassifiedTeams = 0,







            bool finalChampionship = false,







            byte seasonType = 0,







            ushort linkedCup = 0,







            ushort linkedSuperCup = 0,







            // M149: Championship es un bit distinto de FinalChampionship



            // (ToBytes: bit 15 vs bit 14). Al final de la firma para no



            // desplazar a los llamadores posicionales actuales; nullable:



            // sin valor, hereda FinalChampionship.



            bool? championship = null)







        {







            // Validar que el ID no exista



            if (Competitions.Any(c => c.Id == compId))



                throw new InvalidOperationException("El ID de competición " + compId + " ya está en uso.");







            // Validar cantidad de equipos (rango soportado en PES 2021: 2 a 60 equipos)
            // Excepcion Piramide: totalTeams 0 con lista vacia crea la
            // liga-estructura (nace sin equipos y se llena desde su ficha).



            if ((totalTeams < 2 && totalTeams != 0) || totalTeams > 60)



                throw new ArgumentException("La cantidad de equipos debe estar entre 2 y 60.");







            // Alto 37: Validaciones exhaustivas de teamIds



            if (teamIds == null || (teamIds.Count == 0 && totalTeams != 0))



                throw new ArgumentException("La lista de equipos asignados no puede estar vacía.");







            if (teamIds.Count != totalTeams)



                throw new ArgumentException(string.Format("La cantidad de equipos asignados ({0}) no coincide con totalTeams ({1}).", teamIds.Count, totalTeams));







            var uniqueTeamIds = new HashSet<uint>();



            foreach (var tid in teamIds)



            {



                if (!uniqueTeamIds.Add(tid))



                    throw new ArgumentException(string.Format("El equipo ID {0} está duplicado en la lista de asignación.", tid));



                if (!TeamsById.ContainsKey(tid))



                    throw new ArgumentException(string.Format("El equipo ID {0} no existe en Team.bin.", tid));



                // Alto 37: mismo criterio que GetFreePlayableTeams: ni



                // selecciones nacionales ni equipos clasicos 2703/2704.



                TeamRecord teamRec = TeamsById[tid];



                if (teamRec.IsNational)



                    throw new ArgumentException("El equipo ID " + tid + " es una seleccion nacional y no puede entrar como equipo de liga.");



                if (tid == ClassicEuropeTeamId || tid == ClassicWorldTeamId)



                    throw new ArgumentException("El equipo ID " + tid + " es un equipo clasico y no puede entrar como equipo de liga.");



            }







            // Alto 36: el padre debe existir y ser una liga (CompetitionType 4).



            // Sin esto, una D2 bajo una copa/seleccion (o un padre inexistente)



            // quedaba a medio enlazar en silencio en la rama else de abajo.



            // R4: si el padre ya tiene D2, fallar aqui (antes de quitar equipos



            // de sus ligas) en vez de dejar la nueva D2 con IsDivision2=true



            // pero sin enlace y sin aviso.



            if (!isIndependent && parentCompId != 0)



            {



                var parentComp = Competitions.FirstOrDefault(c => c.Id == parentCompId);



                if (parentComp == null)



                    throw new InvalidOperationException("La liga padre (ID " + parentCompId + ") no existe.");



                RegulationRecord parentRegCheck = null;



                RegulationsByCompetition.TryGetValue(parentCompId, out parentRegCheck);



                if (parentRegCheck == null)



                    throw new InvalidOperationException("La liga padre (ID " + parentCompId + ") no tiene reglamento.");



                if (parentRegCheck.CompetitionType != 4)



                    throw new InvalidOperationException("La liga padre (ID " + parentCompId + ") no es una liga (tipo " + parentRegCheck.CompetitionType + "); no puede tener divisiones inferiores.");



                if (parentRegCheck.IdDivision2 != 0)



                    throw new InvalidOperationException("La liga padre (ID " + parentCompId + ") ya tiene una segunda division (reglamento " + parentRegCheck.IdDivision2 + "); no se puede crear otra D2 bajo el mismo padre.");



                List<EntryRecord> parentEntries = null;



                if (EntriesByCompetition != null && EntriesByCompetition.TryGetValue(parentCompId, out parentEntries) && parentEntries != null && parentEntries.Count > 0)



                {



                    bool allNational = true;



                    foreach (var pe in parentEntries)



                    {



                        TeamRecord pt;



                        if (!TeamsById.TryGetValue(pe.TeamId, out pt) || !pt.IsNational) { allNational = false; break; }



                    }



                    if (allNational)



                        throw new InvalidOperationException("La liga padre (ID " + parentCompId + ") es una competicion de selecciones; no puede tener divisiones inferiores.");



                }



            }







            // Remover los clubes de ligas domésticas previas para prevenir duplicidad multilíga



            foreach (var tid in teamIds)



            {



                RemoveTeamFromDomesticLeagues(tid);



            }







            // 1. Crear CompetitionRecord



            CompetitionRecord templateComp = null;



            if (!isIndependent && parentCompId != 0)



            {



                templateComp = Competitions.FirstOrDefault(c => c.Id == parentCompId);



            }



            if (templateComp == null)



            {



                templateComp = Competitions.FirstOrDefault(c => c.Continent == continent) ?? Competitions.FirstOrDefault();



            }







            CompetitionRecord newComp;



            if (templateComp != null && templateComp.RawBytes != null && templateComp.RawBytes.Length >= 36)



            {



                newComp = CompetitionRecord.FromBytes(templateComp.RawBytes, 0);



            }



            else



            {



                newComp = new CompetitionRecord();



                newComp.RawBytes = new byte[36];



            }



            newComp.IsNew = true;







            newComp.Id = compId;



            newComp.IdParentCompetition = 0; // REGLA FORENSE MOTOR KONAMI: Todas las ligas de clubes deben tener IdParentCompetition == 0



            newComp.Continent = (!isIndependent && templateComp != null) ? templateComp.Continent : continent;



            newComp.Group = (!isIndependent && templateComp != null) ? templateComp.Group : ((continent == 4) ? (byte)17 : ((continent == 3) ? (byte)21 : (byte)22));



            newComp.Value1 = unchecked((short)0xC864);



            newComp.Licenced = true;



            newComp.Fake = false;



            newComp.Value2 = false; // Debe ser false para ligas domésticas de clubes en PES 2021



            newComp.Value3 = false;



            newComp.Name = nameAscii.ToUpperInvariant();



            newComp.RawBytes = newComp.ToBytes();







            // 2. Crear RegulationRecord



            RegulationRecord parentReg = null;



            if (!isIndependent && parentCompId != 0)



            {



                RegulationsByCompetition.TryGetValue(parentCompId, out parentReg);



            }







            var templateReg = parentReg 



                              ?? Regulations.FirstOrDefault(r => r.CompetitionType == 4 && r.RawBytes != null && r.RawBytes.Length >= 14 && (r.RawBytes[13] & 0xF0) == 0x10) 



                              ?? Regulations.FirstOrDefault(r => r.CompetitionType == 4 && r.RawBytes != null && r.RawBytes.Length >= 2352) 



                              ?? Regulations.FirstOrDefault(r => r.RawBytes != null && r.RawBytes.Length >= 2352);







            RegulationRecord newReg;



            if (templateReg != null && templateReg.RawBytes != null && templateReg.RawBytes.Length >= 2352)



            {



                newReg = RegulationRecord.FromBytes(templateReg.RawBytes, 0);



            }



            else



            {



                newReg = new RegulationRecord();



                newReg.RawBytes = new byte[2352];



                newReg.RawBytes[13] = 0x10;



            }



            newReg.IsNew = true;







            ushort newRegId = GetNextRegulationId();



            newReg.Id = newRegId;



            newReg.IdCompetition = compId;



            newReg.TotalTeams = totalTeams;



            newReg.CompetitionType = (byte)(compType == 0 ? 4 : (compType == 1 ? 3 : 5)); // 4=Liga estándar, 3=Copa, 5=Supercopa



            newReg.Name = nameAscii.ToUpperInvariant();



            newReg.NameSpanish = !string.IsNullOrEmpty(nameSpanish) ? nameSpanish : nameAscii;



            newReg.NameLatamSpanish = !string.IsNullOrEmpty(nameLatamSpanish) ? nameLatamSpanish : newReg.NameSpanish;



            newReg.NameEnglish = !string.IsNullOrEmpty(nameEnglish) ? nameEnglish : nameAscii;







            // Aplicar reglamentos oficiales configurados



            newReg.Changes = changes;



            newReg.RegisteredPlayers = registeredPlayers;



            newReg.LeagueEachTeamMatches = rounds;



            newReg.LeagueClassification = tieBreaker;



            newReg.CupFormat = cupFormat;



            newReg.ExtraTime = extraTime;



            newReg.PenaltyShootout = penalties;



            newReg.AwayGoals = awayGoals;



            newReg.ThirdPlace = thirdPlace;



            newReg.SingleLeg = singleLeg;



            newReg.NumGroups = numGroups;



            newReg.NumClassifiedTeams = numClassifiedTeams;



            newReg.FinalChampionship = finalChampionship;



            newReg.Championship = championship ?? finalChampionship;







            if (isIndependent)



            {



                newReg.IdDivision1 = 0;



                newReg.IdDivision2 = 0;



                newReg.IsDivision1 = true;



                newReg.IsDivision2 = false;



                newReg.Group = 255;



                newReg.SanctionType = (templateReg != null) ? templateReg.SanctionType : (byte)1;



            }



            else



            {



                if (parentReg != null)



                {



                    newReg.IdDivision1 = parentReg.Id;



                    newReg.IdDivision2 = 0;



                    newReg.IsDivision1 = false;



                    newReg.IsDivision2 = true;



                    newReg.LeagueEachTeamMatches = 2; // En PES 2021 todas las 2das divisiones juegan 2 rondas



                    newReg.Group = 255;



                    newReg.SanctionType = parentReg.SanctionType;







                    // Alto 36: Enlazar la división padre con esta nueva división subordinada



                    // Solo enlazar si el padre no tenía ya otra D2 configurada



                    if (parentReg.IdDivision2 == 0)



                    {



                        parentReg.IdDivision2 = newReg.Id;



                        if (parentReg.IdDivision1 == 0)



                        {



                            parentReg.IsDivision1 = true;



                        }



                        parentReg.IsModified = true;



                        parentReg.RawBytes = parentReg.ToBytes(); // Re-sellar búfer del padre (Alto 10 & 36)



                    }



                }



                else



                {



                    newReg.IdDivision1 = 0;



                    newReg.IdDivision2 = 0;



                    newReg.IsDivision1 = false;



                    newReg.IsDivision2 = true;



                    newReg.LeagueEachTeamMatches = 2;



                    newReg.Group = 255;



                    newReg.SanctionType = (templateReg != null) ? templateReg.SanctionType : (byte)1;



                }



            }







            newReg.RawBytes = newReg.ToBytes();







            // 3. Crear CompetitionEntry records (1-indexed en Position y estrictamente contiguos por competición)



            int lastIndex = -1;



            if (!isIndependent && parentCompId != 0)



            {



                lastIndex = Entries.FindLastIndex(e => e.CompetitionId == 79 || e.CompetitionId == 49 || e.CompetitionId == parentCompId);



            }



            if (lastIndex == -1)



            {



                lastIndex = Entries.FindLastIndex(e => e.CompetitionId != 8);



            }



            if (lastIndex == -1)



            {



                lastIndex = Entries.Count - 1;



            }







            // Alto 38: Buscar un bloque contiguo libre de entry IDs sin colisiones globales ni overflow ushort



            var usedEntryIds = new HashSet<ushort>(Entries.Select(e => e.Id));



            ushort startEntryId = 0;



            ushort maxCurrentId = Entries.Count > 0 ? Entries.Max(e => e.Id) : (ushort)0;







            if ((int)maxCurrentId + teamIds.Count < 65535)



            {



                startEntryId = (ushort)(maxCurrentId + 1);



            }



            else



            {



                // Buscar hueco contiguo desde 1



                ushort candidate = 1;



                while (candidate <= 65535 - teamIds.Count)



                {



                    bool fits = true;



                    for (int k = 0; k < teamIds.Count; k++)



                    {



                        if (usedEntryIds.Contains((ushort)(candidate + k)))



                        {



                            fits = false;



                            candidate = (ushort)(candidate + k + 1);



                            break;



                        }



                    }



                    if (fits)



                    {



                        startEntryId = candidate;



                        break;



                    }



                }



                if (startEntryId == 0)



                    throw new InvalidOperationException("No se encontró un bloque de IDs de entrada contiguos disponibles en CompetitionEntry.bin.");



            }







            var newEntries = new List<EntryRecord>();



            for (int i = 0; i < teamIds.Count; i++)



            {



                var entry = new EntryRecord();



                entry.RawBytes = new byte[EntryRecord.RecordSize];



                entry.TeamId = teamIds[i];



                entry.Id = (ushort)(startEntryId + i);



                entry.CompetitionId = compId;



                entry.Position = (byte)(i + 1); // 1-INDEXED (1..TotalTeams) en PES 2021



                entry.Value1 = 0;



                entry.Value2 = 0;



                entry.Padding = 0;



                entry.IsNew = true;



                entry.RawBytes = entry.ToBytes();



                newEntries.Add(entry);



            }







            // Insertar de forma contigua en la colección general Entries



            Entries.InsertRange(lastIndex + 1, newEntries);







            // Agregar a colecciones de memoria



            Competitions.Add(newComp);



            Regulations.Add(newReg);



            RegulationsByCompetition[compId] = newReg;



            EntriesByCompetition[compId] = newEntries;







            if (countryId != 0)



            {



                SetLeagueCountry(compId, countryId);



            }







            if (promote >= 0) SetLeaguePromote(compId, promote);



            if (demote >= 0) SetLeagueDemote(compId, demote);



            SetLeagueContinental1(compId, continental1);



            SetLeagueContinental2(compId, continental2);







            if (seasonType > 0 || IsCalendarYearCountry(countryId)) SetLeagueSeasonType(compId, seasonType > 0 ? seasonType : (byte)1);



            if (linkedCup > 0) SetLeagueLinkedCup(compId, linkedCup);



            if (linkedSuperCup > 0) SetLeagueLinkedSuperCup(compId, linkedSuperCup);







            HasUnsavedChanges = true;







        }







        public void ApplyDiagnosticCanary(string canaryName = "LIGA ARGENTINA [MOD]")







        {







            foreach (var comp in Competitions.Where(c => c.Id == 22))







            {







                comp.IsModified = true;







            }







            foreach (var reg in Regulations.Where(r => r.IdCompetition == 22))







            {







                reg.IsModified = true;







                reg.NameSpanish = canaryName;







                reg.NameLatamSpanish = canaryName;







                reg.NameEnglish = canaryName;







                reg.RawBytes = reg.ToBytes();







            }







        }







        public static bool IsProtectedVanillaCompetition(byte compId)



        {



            // Ligas y divisiones estructurales vanilla que NUNCA deben eliminarse del motor:



            // - ID 21: Brasileirao Serie A (ancla de primera division sudamericana)



            // - ID 90: Brasileirao Serie B (ancla de segunda division sudamericana; slot Sider 99)



            // - ID 17 / 88: Premier League / Championship (acoplamiento ingles D1/D2)



            // - ID 18 / 89: Ligue 1 / Ligue 2 (acoplamiento frances D1/D2)



            // - ID 19 / 92: Serie A TIM / Serie BKT (acoplamiento italiano D1/D2)



            // - ID 20 / 93: LaLiga Santander / LaLiga SmartBank (acoplamiento espanol D1/D2)



            byte[] protectedIds = { 21, 90, 17, 88, 18, 89, 19, 92, 20, 93 };



            return Array.IndexOf(protectedIds, compId) >= 0;



        }







        public bool DeleteLeague(byte compId)



        {



            var comp = Competitions.FirstOrDefault(c => c.Id == compId);



            if (comp == null) return false;







            if (IsProtectedVanillaCompetition(compId))



                throw new InvalidOperationException(string.Format("La competicion '{0}' (ID {1}) es una competicion estructural protegida del motor de PES 2021 y no puede ser eliminada.", comp.Name, compId));







            // 1. Quitar CompetitionRecord



            Competitions.Remove(comp);







            // 2. Quitar RegulationRecord







            RegulationRecord reg = null;







            if (RegulationsByCompetition.TryGetValue(compId, out reg))







            {







                Regulations.Remove(reg);







                RegulationsByCompetition.Remove(compId);







            }







            else







            {







                reg = Regulations.FirstOrDefault(r => r.IdCompetition == compId);







                if (reg != null) Regulations.Remove(reg);







            }







            // 3. Desvincular ligas hijas o padres que apuntaran a este reglamento







            if (reg != null)







            {







                foreach (var r in Regulations)







                {







                    if (r.IdDivision1 == reg.Id)







                    {







                        r.IdDivision1 = 0;







                        r.IsDivision2 = false;







                        r.IsModified = true;







                        r.RawBytes = r.ToBytes();







                    }







                    if (r.IdDivision2 == reg.Id)







                    {







                        r.IdDivision2 = 0;







                        r.IsModified = true;







                        BitConverter.GetBytes((ushort)0).CopyTo(r.RawBytes, 0);







                    }







                }







            }







            // 4. Liberar equipos (remover entradas de CompetitionEntry.bin)







            if (EntriesByCompetition.ContainsKey(compId))







            {







                var compEntries = EntriesByCompetition[compId].ToList();







                foreach (var e in compEntries)







                {







                    Entries.Remove(e);







                }







                EntriesByCompetition.Remove(compId);







            }







            // 5. Limpiar reglas personalizadas







            if (CustomLeagueCountries != null && CustomLeagueCountries.ContainsKey(compId))







                CustomLeagueCountries.Remove(compId);







            if (LeaguePromoteCount != null && LeaguePromoteCount.ContainsKey(compId))







                LeaguePromoteCount.Remove(compId);







            if (LeagueDemoteCount != null && LeagueDemoteCount.ContainsKey(compId))







                LeagueDemoteCount.Remove(compId);







            if (LeagueContinental1Count != null && LeagueContinental1Count.ContainsKey(compId))







                LeagueContinental1Count.Remove(compId);







            if (LeagueContinental2Count != null && LeagueContinental2Count.ContainsKey(compId))







                LeagueContinental2Count.Remove(compId);







            if (LeagueSeasonType != null && LeagueSeasonType.ContainsKey(compId))







                LeagueSeasonType.Remove(compId);







            if (LeagueLinkedCup != null && LeagueLinkedCup.ContainsKey(compId))







                LeagueLinkedCup.Remove(compId);







            if (LeagueLinkedSuperCup != null && LeagueLinkedSuperCup.ContainsKey(compId))







                LeagueLinkedSuperCup.Remove(compId);







            // 6. Eliminar emblema en Sider LiveCPK si existe







            DeleteCompetitionEmblem(compId);







            HasUnsavedChanges = true;







            return true;







        }







        public bool IsLeagueParityValid(byte compId)







        {







            if (!EntriesByCompetition.ContainsKey(compId)) return true;







            int count = EntriesByCompetition[compId].Count;







            return (count == 0 || count % 2 == 0);







        }







        public byte GetTeamContinent(TeamRecord t)







        {







            if (t == null) return 1;







            // IDs de países CONCACAF conocidos (México, USA, Canadá, Costa Rica, etc.)



            // PES los clasifica con Continent=3 (mismo que Asia) pero son Norteamérica.



            ushort cid = t.CountryId;



            if (cid == 89 || cid == 95 || cid == 77 || cid == 79 ||  // Mexico, USA, Canada, Costa Rica



                cid == 78 || cid == 81 || cid == 82 || cid == 83 ||  // Cuba, El Salvador, Guatemala, Haiti



                cid == 84 || cid == 86 || cid == 88)                  // Honduras, Jamaica, Panama



                return 5; // CONCACAF / Norteamérica







            CountryRecord cr;







            if (CountriesById.TryGetValue(cid, out cr))







            {







                if (cr.Continent == 0) return 2; // Europa







                if (cr.Continent == 2) return 4; // Sudamérica







                if (cr.Continent == 3) return 3; // Asia / Oceanía







                if (cr.Continent == 1) return 1; // África / Otros







            }







            // Fallback por rangos de IDs (cuando CountriesById no tiene el registro)



            // Alto 39: Japon (262), China (261), Arabia Saudita (280) y



            // Tailandia (288) son Asia segun GetDefaultRegionForCountry, pero



            // caian en el rango europeo 190..290 de abajo. Se comprueban antes.



            if (cid == 262 || cid == 261 || cid == 280 || cid == 288) return 3; // Asia / Oceania



            if (cid >= 190 && cid <= 290) return 2; // Europa (IDs altos: 192=Austria, 204=Inglaterra...)







            if (cid >= 144 && cid <= 158) return 4; // Sudamérica (Argentina 144 .. Venezuela 158)







            if (cid >= 157 && cid <= 165) return 4; // Sudamérica extendida (Uruguay, Venezuela...)







            return 1; // Otros / desconocido







        }







        public bool IsClubInDomesticLeague(uint teamId)







        {







            if (!Entries.Any(e => e.TeamId == teamId)) return false;







            foreach (var e in Entries.Where(en => en.TeamId == teamId))







            {







                var comp = Competitions.FirstOrDefault(c => c.Id == e.CompetitionId);







                if (comp == null) continue;







                RegulationRecord reg;







                RegulationsByCompetition.TryGetValue(comp.Id, out reg);







                // M85: (comp.Name ?? "") en vez de comp.Name (NRE si



                // el nombre era null) y conjunto unico de copa (M74).



                bool isCup = IsCupOrSuperCupCompetition(reg)







                             || (comp.Name ?? "").IndexOf("CUP", StringComparison.OrdinalIgnoreCase) >= 0







                             || (comp.Name ?? "").IndexOf("CHAMPIONS", StringComparison.OrdinalIgnoreCase) >= 0







                             || (comp.Name ?? "").IndexOf("SUPER", StringComparison.OrdinalIgnoreCase) >= 0;







                if (!isCup) return true;







            }







            return false;







        }







        public List<TeamRecord> GetOtherTeamsByContinent(byte continent)







        {







            // M86: equipos en ligas domesticas calculados en una pasada



            // (antes un IsClubInDomesticLeague por equipo, cuadratico).



            var domesticTeamIds = GetDomesticLeagueTeamIds();







            var list = new List<TeamRecord>();







            foreach (var t in Teams)







            {







                if (t.IsNational) continue;







                if (t.Id == ClassicEuropeTeamId || t.Id == ClassicWorldTeamId) continue; // Equipos clásicos van en su propia liga







                if (domesticTeamIds.Contains(t.Id)) continue; // Ya pertenece a una liga doméstica







                byte teamCont = GetTeamContinent(t);







                // M86: el continente 1 tiene su rama propia (antes el



                // "resto" se mezclaba en Europa).



                if (continent == 1 && teamCont == 1) // Otros / resto







                {







                    list.Add(t);







                }







                else if (continent == 2 && teamCont == 2) // Otros Europa







                {







                    list.Add(t);







                }







                else if (continent == 4 && teamCont == 4) // Otros América Latina







                {







                    list.Add(t);







                }







                else if (continent == 3 && teamCont == 3) // Otros Asia







                {







                    list.Add(t);







                }







            }







            return list.OrderBy(t => !string.IsNullOrEmpty(t.NameSpanish) ? t.NameSpanish : t.NameEnglish).ToList();







        }







                public void RemoveTeamFromDomesticLeagues(uint teamId)



        {



            var toRemove = new List<EntryRecord>();



            foreach (var e in Entries.Where(en => en.TeamId == teamId))



            {



                var comp = Competitions.FirstOrDefault(c => c.Id == e.CompetitionId);



                if (comp == null) continue;



                RegulationRecord reg;



                RegulationsByCompetition.TryGetValue(comp.Id, out reg);



                // M85: (comp.Name ?? "") en vez de comp.Name y



                // conjunto unico de copa (M74).



                bool isCup = IsCupOrSuperCupCompetition(reg)



                             || (comp.Name ?? "").IndexOf("CUP", StringComparison.OrdinalIgnoreCase) >= 0



                             || (comp.Name ?? "").IndexOf("CHAMPIONS", StringComparison.OrdinalIgnoreCase) >= 0



                             || (comp.Name ?? "").IndexOf("SUPER", StringComparison.OrdinalIgnoreCase) >= 0;



                if (!isCup)



                {



                    toRemove.Add(e);



                }



            }



            var affectedComps = new HashSet<byte>();



            foreach (var e in toRemove)



            {



                Entries.Remove(e);



                affectedComps.Add(e.CompetitionId);



                if (EntriesByCompetition.ContainsKey(e.CompetitionId))



                {



                    EntriesByCompetition[e.CompetitionId].Remove(e);



                    for (int i = 0; i < EntriesByCompetition[e.CompetitionId].Count; i++)



                    {



                        var entry = EntriesByCompetition[e.CompetitionId][i];



                        entry.Position = (byte)(i + 1);



                        entry.IsModified = true;



                        entry.RawBytes = entry.ToBytes();



                    }



                }



            }



            // M84: reconciliar TotalTeams de las ligas origen (antes



            // solo se reconciliaba el destino en AssignTeamToLeague).



            if (RegulationsByCompetition != null)



            {



                foreach (var compId in affectedComps)



                {



                    RegulationRecord affectedReg;



                    if (RegulationsByCompetition.TryGetValue(compId, out affectedReg) && affectedReg != null && EntriesByCompetition.ContainsKey(compId))



                    {



                        byte actualCount = (byte)EntriesByCompetition[compId].Count;



                        if (actualCount >= 2 && affectedReg.TotalTeams != actualCount)



                        {



                            affectedReg.TotalTeams = actualCount;



                            affectedReg.IsModified = true;



                            affectedReg.RawBytes = affectedReg.ToBytes();



                        }



                    }



                }



            }



            if (toRemove.Count > 0)



            {



                HasUnsavedChanges = true;



            }



        }







        public void AssignTeamToLeague(uint teamId, byte targetCompId)



        {



            // M84: validar equipo y destino (antes un destino fantasma



            // creaba entradas huerfanas y un equipo inexistente tambien).



            if (TeamsById == null || !TeamsById.ContainsKey(teamId))



                throw new ArgumentException("El equipo especificado no existe: " + teamId);



            if (Competitions == null || !Competitions.Any(c => c.Id == targetCompId))



                throw new ArgumentException("La competicion destino no existe: " + targetCompId);



            // M73: no engordar una competicion estructural protegida.



            if (IsProtectedVanillaCompetition(targetCompId))



                throw new InvalidOperationException("La competicion " + targetCompId + " es una competicion estructural protegida y no puede recibir equipos con AssignTeamToLeague.");



            if (EntriesByCompetition == null) EntriesByCompetition = new Dictionary<byte, List<EntryRecord>>();



            RemoveTeamFromDomesticLeagues(teamId);



            if (!EntriesByCompetition.ContainsKey(targetCompId))



                EntriesByCompetition[targetCompId] = new List<EntryRecord>();







            // Prevenir insercion duplicada del mismo equipo



            if (EntriesByCompetition[targetCompId].Any(e => e.TeamId == teamId))



                return;







            // M84: sin hueco de posicion no se envuelve a 0 a los 255.



            if (EntriesByCompetition[targetCompId].Count >= 255)



                throw new InvalidOperationException("La competicion " + targetCompId + " ya tiene 255 equipos y no admite mas posiciones.");



            byte nextPos = (byte)(EntriesByCompetition[targetCompId].Count + 1);



            var entry = new EntryRecord



            {



                TeamId = teamId,



                Id = GetNextEntryId(),



                CompetitionId = targetCompId,



                Position = nextPos,



                Value1 = 0,



                Value2 = 0,



                Padding = 0,



                RawBytes = new byte[EntryRecord.RecordSize],



                IsNew = true



            };



            entry.RawBytes = entry.ToBytes();



            Entries.Add(entry);



            EntriesByCompetition[targetCompId].Add(entry);







            // Alto 12: reconciliar TotalTeams de la liga destino (mismo patron



            // que EnsureClassicTeamsCompetition). La creacion masiva



            // (BatchCreateTeams) y la asignacion llegan por aqui y antes



            // dejaban TotalTeams desfasado.



            if (RegulationsByCompetition != null && RegulationsByCompetition.ContainsKey(targetCompId))



            {



                var targetReg = RegulationsByCompetition[targetCompId];



                byte actualCount = (byte)EntriesByCompetition[targetCompId].Count;



                if (actualCount >= 2 && targetReg.TotalTeams != actualCount)



                {



                    targetReg.TotalTeams = actualCount;



                    targetReg.IsModified = true;



                    targetReg.RawBytes = targetReg.ToBytes();



                }



            }



            HasUnsavedChanges = true;



        }







                public byte[] SerializeCompetitionBytes()



        {



            var orderedComps = Competitions.OrderBy(c => c.Id).ToList();



            using (var msComp = new MemoryStream())



            {



                foreach (var c in orderedComps)



                {



                    byte[] rec = (!c.IsModified && !c.IsNew && c.RawBytes != null) ? c.RawBytes : c.ToBytes();



                    msComp.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(msComp.ToArray());



            }



        }







        public byte[] SerializeRegulationBytes()



        {



            using (var msReg = new MemoryStream())



            {



                foreach (var r in Regulations)



                {



                    byte[] rec = (!r.IsModified && !r.IsNew && r.RawBytes != null) ? r.RawBytes : r.ToBytes();



                    msReg.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(msReg.ToArray());



            }



        }







        public byte[] SerializeEntryBytes()



        {



            using (var msEntry = new MemoryStream())



            {



                foreach (var e in Entries)



                {



                    byte[] rec = (!e.IsModified && !e.IsNew && e.RawBytes != null) ? e.RawBytes : e.ToBytes();



                    msEntry.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(msEntry.ToArray());



            }



        }







        public byte[] SerializeTacticsBytes()



        {



            if (Tactics == null || Tactics.Count == 0) return null;



            var ordered = Tactics.OrderBy(t => t.Key).ToList();



            using (var ms = new MemoryStream())



            {



                foreach (var t in ordered)



                {



                    byte[] rec = t.ToBytes();



                    ms.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(ms.ToArray());



            }



        }







        public void SaveTactics(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (Tactics == null || Tactics.Count == 0) return;



            byte[] packed = SerializeTacticsBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "Tactics.bin"), packed);



        }







        public byte[] SerializeTacticsFormationBytes()



        {



            if (TacticsFormations == null || TacticsFormations.Count == 0) return null;



            var ordered = TacticsFormations.OrderBy(f => f.TacticsKey).ThenBy(f => f.Place).ToList();



            using (var ms = new MemoryStream())



            {



                foreach (var f in ordered)



                {



                    byte[] rec = f.ToBytes();



                    ms.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(ms.ToArray());



            }



        }







        public void SaveTacticsFormations(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (TacticsFormations == null || TacticsFormations.Count == 0) return;



            byte[] packed = SerializeTacticsFormationBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "TacticsFormation.bin"), packed);



        }







        public void Save(string folderPath)



        {



            if (string.IsNullOrEmpty(folderPath))



                folderPath = DatabasePath;







            // M87: sin carpeta valida no se guarda (antes un null



            // lanzaba en Path.Combine y un "" escribia en el



            // directorio de trabajo del proceso).



            if (string.IsNullOrEmpty(folderPath))



                throw new InvalidOperationException("No se puede guardar: no hay carpeta de base de datos.");



            if (!Directory.Exists(folderPath))



                throw new DirectoryNotFoundException("La carpeta de base de datos no existe: " + folderPath);







            // 1. Crear backup integral con timestamp y milisegundos de todos los archivos



            CreateTimestampedBackup(folderPath);







            // 2. FASE 1: Serialización y compresión en memoria de TODOS los archivos (sin tocar el disco)



            var staged = new Dictionary<string, byte[]>();







            if (Competitions != null && Competitions.Count > 0)



                staged["Competition.bin"] = SerializeCompetitionBytes();







            if (Regulations != null && Regulations.Count > 0)



                staged["CompetitionRegulation.bin"] = SerializeRegulationBytes();







            if (Entries != null && Entries.Count > 0)



                staged["CompetitionEntry.bin"] = SerializeEntryBytes();







            if (Teams != null && Teams.Count > 0)



                staged["Team.bin"] = SerializeTeamBytes();







            if (Coaches != null && Coaches.Count > 0)



                staged["Coach.bin"] = SerializeCoachBytes();







            if (Stadiums != null && Stadiums.Count > 0)



                staged["Stadium.bin"] = SerializeStadiumBytes();







            if (Derbies != null && Derbies.Count > 0)



                staged["Derby.bin"] = SerializeDerbyBytes();







            if (PlayerAssignments != null && PlayerAssignments.Count > 0)



                staged["PlayerAssignment.bin"] = SerializePlayerAssignmentBytes();







            if (Players != null && Players.Count > 0)



                staged["Player.bin"] = SerializePlayerBytes();







            if (Tactics != null && Tactics.Count > 0)



                staged["Tactics.bin"] = SerializeTacticsBytes();







            if (TacticsFormations != null && TacticsFormations.Count > 0)



                staged["TacticsFormation.bin"] = SerializeTacticsFormationBytes();







            byte[] rulesBytes = SerializeCustomLeagueRulesBytes();



            // M76: si la serializacion fallo, el guardado no continua



            // en silencio sin las reglas.



            if (rulesBytes == null)



                throw new InvalidOperationException("No se pudieron serializar las reglas personalizadas de ligas.");



            if (rulesBytes.Length > 0)



                staged["league_rules.json"] = rulesBytes;







            staged["team_tactics_extra.json"] = SerializeTeamTacticsExtraBytes();







            // 3. FASE 2: Escritura en archivos temporales (.tmp_...)



            string sessionId = Guid.NewGuid().ToString("N");



            var writtenTmpFiles = new List<KeyValuePair<string, string>>(); // targetPath, tmpPath



            var committedBackups = new List<KeyValuePair<string, string>>(); // targetPath, bakPath (.savebak_ conservado hasta el commit completo)



            var createdFiles = new List<string>(); // targetPath creados por File.Move (sin version previa)







            try



            {



                foreach (var kvp in staged)



                {



                    string targetPath = Path.Combine(folderPath, kvp.Key);



                    string tmpPath = targetPath + ".tmp_" + sessionId;



                    File.WriteAllBytes(tmpPath, kvp.Value);



                    writtenTmpFiles.Add(new KeyValuePair<string, string>(targetPath, tmpPath));



                }







                // 4. FASE 3: Commit atómico de todos los archivos preparados



                foreach (var pair in writtenTmpFiles)



                {



                    string targetPath = pair.Key;



                    string tmpPath = pair.Value;







                    if (File.Exists(targetPath))



                    {



                        string bakPath = targetPath + ".savebak_" + sessionId;



                        try



                        {



                            if (File.Exists(bakPath)) File.Delete(bakPath);



                            File.Replace(tmpPath, targetPath, bakPath);



                            committedBackups.Add(new KeyValuePair<string, string>(targetPath, bakPath));



                        }



                        catch



                        {



                            // Sin respaldo previo no habria forma de revertir:



                            // copiar el original a .savebak_ antes de sobrescribir.



                            try



                            {



                                if (File.Exists(bakPath)) File.Delete(bakPath);



                                File.Copy(targetPath, bakPath, true);



                            }



                            catch { }



                            File.Copy(tmpPath, targetPath, true);



                            if (File.Exists(tmpPath)) File.Delete(tmpPath);



                            if (File.Exists(bakPath)) committedBackups.Add(new KeyValuePair<string, string>(targetPath, bakPath));



                            else committedBackups.Add(new KeyValuePair<string, string>(targetPath, null));



                        }



                    }



                    else



                    {



                        File.Move(tmpPath, targetPath);



                        createdFiles.Add(targetPath);



                    }



                }







                // Solo con el commit completo se eliminan los respaldos.



                foreach (var cb in committedBackups)



                {



                    try { if (cb.Value != null && File.Exists(cb.Value)) File.Delete(cb.Value); } catch { }



                }







                HasUnsavedChanges = false;



            }



            catch (Exception ex)



            {



                // Alto 41: rollback minimo seguro. Restaurar cada archivo ya



                // reemplazado desde su .savebak_ y borrar los archivos creados



                // en este intento (sin version previa que restaurar). Los



                // .savebak_ se conservan tras un rollback parcial para



                // recuperacion manual.



                var restoreErrors = new List<string>();



                foreach (var cb in committedBackups)



                {



                    try



                    {



                        if (cb.Value != null && File.Exists(cb.Value))



                            File.Copy(cb.Value, cb.Key, true);



                        else



                            restoreErrors.Add(cb.Key + " (sin respaldo .savebak_)");



                    }



                    catch (Exception rex)



                    {



                        restoreErrors.Add(cb.Key + " (" + rex.Message + ")");



                    }



                }



                foreach (var created in createdFiles)



                {



                    try { if (File.Exists(created)) File.Delete(created); } catch (Exception rex) { restoreErrors.Add(created + " (" + rex.Message + ")"); }



                }



                // Limpieza de emergencia de archivos temporales en caso de fallo antes del commit



                foreach (var pair in writtenTmpFiles)



                {



                    try



                    {



                        if (File.Exists(pair.Value))



                            File.Delete(pair.Value);



                    }



                    catch { }



                }



                string rollbackNote;



                if (committedBackups.Count == 0 && createdFiles.Count == 0)



                    rollbackNote = "No se habia reemplazado ningun archivo todavia.";



                else if (restoreErrors.Count == 0)



                    rollbackNote = "Se restauraron los archivos ya reemplazados desde sus respaldos .savebak_.";



                else



                    rollbackNote = "ATENCION: no se pudieron restaurar todos los archivos: " + string.Join("; ", restoreErrors.ToArray()) + ". Los respaldos .savebak_ se conservan para recuperacion manual.";



                throw new InvalidOperationException("Fallo crítico durante el guardado atómico de la base de datos: " + ex.Message + " " + rollbackNote, ex);



            }



        }







        public FormationPreset GetTeamFormation(uint teamId)



        {



            List<TacticsRecord> trList;



            if (TacticsByClub != null && TacticsByClub.TryGetValue(teamId, out trList) && trList.Count > 0)



            {



                uint key = trList[0].Key;



                List<TacticsFormationRecord> formList;



                if (FormationsByTacticsKey != null && FormationsByTacticsKey.TryGetValue(key, out formList) && formList.Count >= 11)



                {



                    // Obtener los 11 titulares de la variante 0



                    var starters = formList.Where(f => f.Variant == 0).OrderBy(f => f.SquadIndex).Take(11).ToList();



                    if (starters.Count == 11)



                    {



                        int[] r = new int[11];



                        byte[] d = new byte[11];



                        byte[] w = new byte[11];



                        for (int i = 0; i < 11; i++)



                        {



                            r[i] = (int)starters[i].Role;



                            d[i] = starters[i].Depth;



                            w[i] = starters[i].Width;



                        }







                        // Identificar nombre de formacion segun roles o lineas



                        string matchName = null;



                        foreach (var preset in FormationPreset.Presets)



                        {



                            bool match = true;



                            for (int i = 0; i < 11; i++)



                            {



                                if (preset.Roles[i] != r[i]) { match = false; break; }



                            }



                            if (match) { matchName = preset.Name; break; }



                        }







                        if (string.IsNullOrEmpty(matchName))



                        {



                            int defCount = 0, midCount = 0, fwdCount = 0;



                            for (int i = 1; i < 11; i++)



                            {



                                if (d[i] <= 16) defCount++;



                                else if (d[i] <= 34) midCount++;



                                else fwdCount++;



                            }



                            matchName = string.Format("{0}-{1}-{2}", defCount, midCount, fwdCount);



                        }







                        var current = new FormationPreset();



                        current.Name = matchName;



                        current.Roles = r;



                        current.Depths = d;



                        current.Widths = w;



                        return current;



                    }



                }



            }







            // Fallback por defecto 4-2-3-1



            return FormationPreset.Presets.FirstOrDefault(p => p.Name.Contains("4-2-3-1")) ?? FormationPreset.Presets[0];



        }







        public void ApplyTeamFormation(uint teamId, FormationPreset preset)



        {



            if (preset == null) return;







            // M90: validar TODO antes de borrar nada (antes el preset



            // se indexaba despues de RemoveAll y un preset corto o un



            // diccionario nulo rompia a mitad de la destruccion).



            if (preset.Roles == null || preset.Roles.Length < 11



                || preset.Depths == null || preset.Depths.Length < 11



                || preset.Widths == null || preset.Widths.Length < 11) return;



            if (TacticsByClub == null) TacticsByClub = new Dictionary<uint, List<TacticsRecord>>();



            if (Tactics == null) Tactics = new List<TacticsRecord>();



            if (TacticsFormations == null) TacticsFormations = new List<TacticsFormationRecord>();



            if (FormationsByTacticsKey == null) FormationsByTacticsKey = new Dictionary<uint, List<TacticsFormationRecord>>();







            List<TacticsRecord> trList;



            if (!TacticsByClub.TryGetValue(teamId, out trList) || trList == null || trList.Count == 0)



            {



                // Crear 2 llaves para el club en Tactics.bin



                uint maxKey = Tactics.Count > 0 ? Tactics.Max(t => t.Key) : 7000;



                uint key1 = maxKey + 1;



                uint key2 = maxKey + 2;







                var t1 = new TacticsRecord();



                t1.Key = key1;



                t1.ClubId = teamId;



                t1.Strategy = 0x5A936900;







                var t2 = new TacticsRecord();



                t2.Key = key2;



                t2.ClubId = teamId;



                t2.Strategy = 0x5B936811;







                Tactics.Add(t1);



                Tactics.Add(t2);







                trList = new List<TacticsRecord> { t1, t2 };



                TacticsByClub[teamId] = trList;



            }







            // Para cada llave táctica del equipo, aplicar las 33 filas (3 variantes x 11 jugadores)



            foreach (var tRec in trList)



            {



                uint key = tRec.Key;



                TacticsFormations.RemoveAll(f => f.TacticsKey == key);







                var newForms = new List<TacticsFormationRecord>();



                for (int variant = 0; variant < 3; variant++)



                {



                    for (int i = 0; i < 11; i++)



                    {



                        var f = new TacticsFormationRecord();



                        f.TacticsKey = key;



                        f.Role = (uint)preset.Roles[i];



                        f.Depth = preset.Depths[i];



                        f.Width = preset.Widths[i];



                        f.Place = (byte)((variant << 4) | (i & 0x0F));



                        f.Pad = 0;



                        newForms.Add(f);



                        TacticsFormations.Add(f);



                    }



                }



                FormationsByTacticsKey[key] = newForms;



            }







            HasUnsavedChanges = true;



        }







                public Dictionary<uint, int[]> TeamAdvancedInstructions = new Dictionary<uint, int[]>();







        public int[] GetTeamAdvancedInstructions(uint teamId)



        {



            int[] adv;



            if (TeamAdvancedInstructions != null && TeamAdvancedInstructions.TryGetValue(teamId, out adv) && adv != null)



                return adv;



            return new int[] { 0, 0, 0, 0 };



        }







        public void SetTeamAdvancedInstructions(uint teamId, int att1, int att2, int def1, int def2)



        {



            if (TeamAdvancedInstructions == null) TeamAdvancedInstructions = new Dictionary<uint, int[]>();



            TeamAdvancedInstructions[teamId] = new int[] { att1, att2, def1, def2 };



            HasUnsavedChanges = true;



        }







        private void LoadTeamTacticsExtra(string folderPath)



        {



            try



            {



                if (TeamAdvancedInstructions == null) TeamAdvancedInstructions = new Dictionary<uint, int[]>();



                else TeamAdvancedInstructions.Clear();



                string path = Path.Combine(folderPath, "team_tactics_extra.json");



                if (!File.Exists(path)) return;



                string content = File.ReadAllText(path, Encoding.UTF8);



                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""(\d+)""\s*:\s*\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]");



                foreach (System.Text.RegularExpressions.Match m in matches)



                {



                    uint tid;



                    if (uint.TryParse(m.Groups[1].Value, out tid))



                    {



                        int a1 = int.Parse(m.Groups[2].Value);



                        int a2 = int.Parse(m.Groups[3].Value);



                        int d1 = int.Parse(m.Groups[4].Value);



                        int d2 = int.Parse(m.Groups[5].Value);



                        TeamAdvancedInstructions[tid] = new int[] { a1, a2, d1, d2 };



                    }



                }



            }



            catch (Exception ex)



            {



                LoadWarnings.Add(string.Format("Aviso al cargar team_tactics_extra.json: {0}", ex.Message));



            }



        }







        public byte[] SerializeTeamTacticsExtraBytes()



        {



            var sb = new StringBuilder();



            sb.Append("{\n");



            bool first = true;



            if (TeamAdvancedInstructions != null)



            {



                foreach (var kvp in TeamAdvancedInstructions.OrderBy(k => k.Key))



                {



                    if (kvp.Value == null || kvp.Value.Length < 4) continue;



                    if (kvp.Value[0] == 0 && kvp.Value[1] == 0 && kvp.Value[2] == 0 && kvp.Value[3] == 0) continue;



                    if (!first) sb.Append(",\n");



                    sb.AppendFormat("  \"{0}\": [{1}, {2}, {3}, {4}]", kvp.Key, kvp.Value[0], kvp.Value[1], kvp.Value[2], kvp.Value[3]);



                    first = false;



                }



            }



            sb.Append("\n}\n");



            return Encoding.UTF8.GetBytes(sb.ToString());



        }







        public void UpdateTeamStrategy(uint teamId, uint newStrategy)



        {



            if (TacticsByClub != null && TacticsByClub.ContainsKey(teamId))



            {



                foreach (var tr in TacticsByClub[teamId])



                {



                    tr.Strategy = newStrategy;



                    tr.RawBytes = tr.ToBytes();



                }



            }



            HasUnsavedChanges = true;



        }







public bool UpdatePlayerTacticalPosition(uint teamId, int squadIndex, int role, byte depth, byte width)



        {



            if (squadIndex < 0 || squadIndex >= 11) return false;



            if (TacticsByClub == null || !TacticsByClub.ContainsKey(teamId)) return false;



            var trList = TacticsByClub[teamId];



            if (trList == null || trList.Count == 0) return false;







            bool updated = false;



            foreach (var tr in trList)



            {



                uint key = tr.Key;



                if (FormationsByTacticsKey != null && FormationsByTacticsKey.ContainsKey(key))



                {



                    var fList = FormationsByTacticsKey[key];



                    foreach (var rec in fList)



                    {



                        if (rec.SquadIndex == squadIndex)



                        {



                            rec.Depth = depth;



                            rec.Width = width;



                            if (role >= 0) rec.Role = (uint)role;



                            rec.RawBytes = rec.ToBytes();



                            updated = true;



                        }



                    }



                }



            }



            if (updated) HasUnsavedChanges = true;



            return updated;



        }







        public bool SwapSquadPlayers(uint teamId, uint playerIdA, uint playerIdB)



        {



            if (playerIdA == playerIdB) return false;



            if (PlayerAssignmentsByTeam == null || !PlayerAssignmentsByTeam.ContainsKey(teamId)) return false;



            var list = PlayerAssignmentsByTeam[teamId];



            var paA = list.FirstOrDefault(a => a.PlayerId == playerIdA);



            var paB = list.FirstOrDefault(a => a.PlayerId == playerIdB);



            if (paA == null || paB == null) return false;







            int orderA = paA.SquadOrder;



            int orderB = paB.SquadOrder;



            paA.SquadOrder = orderB;



            paB.SquadOrder = orderA;



            paA.RawBytes = paA.ToBytes();



            paB.RawBytes = paB.ToBytes();



            HasUnsavedChanges = true;



            return true;



        }







        public bool SetStarterRole(uint teamId, int squadIndex, int role)



        {



            if (squadIndex < 0 || squadIndex >= 11 || role < 0) return false;



            if (TacticsByClub == null || !TacticsByClub.ContainsKey(teamId)) return false;



            var trList = TacticsByClub[teamId];



            if (trList == null || trList.Count == 0) return false;







            bool updated = false;



            foreach (var tr in trList)



            {



                uint key = tr.Key;



                if (FormationsByTacticsKey != null && FormationsByTacticsKey.ContainsKey(key))



                {



                    var fList = FormationsByTacticsKey[key];



                    foreach (var rec in fList)



                    {



                        if (rec.SquadIndex == squadIndex)



                        {



                            rec.Role = (uint)role;



                            rec.RawBytes = rec.ToBytes();



                            updated = true;



                        }



                    }



                }



            }



            if (updated) HasUnsavedChanges = true;



            return updated;



        }







        public void GetTeamMetrics(uint teamId, out int attack, out int midfield, out int defense, out float stars, out int squadCount)



        {



            var roster = GetTeamRoster(teamId);



            squadCount = roster != null ? roster.Count : 0;







            if (roster == null || roster.Count == 0)



            {



                attack = 65; midfield = 65; defense = 65; stars = 2.0f;



                return;



            }







            var attList = new List<int>();



            var midList = new List<int>();



            var defList = new List<int>();







            foreach (var a in roster)



            {



                PlayerRecord p = GetPlayer(a.PlayerId);



                if (p == null) continue;







                int pos = p.PositionIndex;



                // B52: un indice negativo es dato invalido y no se



                // clasifica como ataque (antes caia en el else).



                if (pos < 0) continue;



                if (pos >= 0 && pos <= 3) // GK, CB, LB, RB



                    defList.Add(p.Overall);



                else if (pos >= 4 && pos <= 8) // DMF, CMF, LMF, RMF, AMF



                    midList.Add(p.Overall);



                else // LWF, RWF, SS, CF



                    attList.Add(p.Overall);



            }







            // Tomar los mejores de cada línea para un cálculo fidedigno del 11 titular



            attack = attList.Count > 0 ? (int)Math.Round(attList.OrderByDescending(x => x).Take(3).Average()) : 65;



            midfield = midList.Count > 0 ? (int)Math.Round(midList.OrderByDescending(x => x).Take(4).Average()) : 65;



            defense = defList.Count > 0 ? (int)Math.Round(defList.OrderByDescending(x => x).Take(4).Average()) : 65;







            double mean = (attack * 0.35) + (midfield * 0.35) + (defense * 0.30);



            if (mean >= 85) stars = 5.0f;



            else if (mean >= 82) stars = 4.5f;



            else if (mean >= 78) stars = 4.0f;



            else if (mean >= 74) stars = 3.5f;



            else if (mean >= 70) stars = 3.0f;



            else if (mean >= 65) stars = 2.5f;



            else if (mean >= 60) stars = 2.0f;



            else stars = 1.5f;



        }







                public byte[] SerializeTeamBytes()



        {



            if (Teams == null || Teams.Count == 0) return null;







            // M89: guardar ya no crea entrenadores (antes el guardado



            // mutaba la base en memoria asignando DT nuevos; la



            // asignacion de DT valido ocurre al crear el equipo).







            var orderedTeams = Teams.OrderBy(t => t.Id).ToList();



            using (var msTeam = new MemoryStream())



            {



                foreach (var t in orderedTeams)



                {



                    byte[] rec = (!t.IsModified && !t.IsNew && t.RawBytes != null) ? t.RawBytes : t.ToBytes();



                    msTeam.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(msTeam.ToArray());



            }



        }







        public void SaveTeams(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (Teams == null || Teams.Count == 0) return;



            byte[] packed = SerializeTeamBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "Team.bin"), packed);



        }







        public byte[] SerializeCoachBytes()



        {



            if (Coaches == null || Coaches.Count == 0) return null;



            var ordered = Coaches.OrderBy(c => c.Id).ToList();



            using (var ms = new MemoryStream())



            {



                foreach (var c in ordered)



                {



                    byte[] rec = c.ToBytes();



                    ms.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(ms.ToArray());



            }



        }







        public void SaveCoaches(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (Coaches == null || Coaches.Count == 0) return;



            byte[] packed = SerializeCoachBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "Coach.bin"), packed);



        }







        public byte[] SerializeStadiumBytes()



        {



            if (Stadiums == null || Stadiums.Count == 0) return null;



            var ordered = Stadiums.OrderBy(s => s.Id).ToList();



            using (var ms = new MemoryStream())



            {



                foreach (var s in ordered)



                {



                    byte[] rec = s.ToBytes();



                    ms.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(ms.ToArray());



            }



        }







        public void SaveStadiums(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (Stadiums == null || Stadiums.Count == 0) return;



            byte[] packed = SerializeStadiumBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "Stadium.bin"), packed);



        }







        public byte[] SerializeDerbyBytes()



        {



            if (Derbies == null || Derbies.Count == 0) return null;



            using (var ms = new MemoryStream())



            {



                foreach (var d in Derbies)



                {



                    byte[] rec = d.ToBytes();



                    ms.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(ms.ToArray());



            }



        }







        public void SaveDerbies(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (Derbies == null || Derbies.Count == 0) return;



            byte[] packed = SerializeDerbyBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "Derby.bin"), packed);



        }







        public byte[] SerializePlayerAssignmentBytes()



        {



            if (PlayerAssignments == null || PlayerAssignments.Count == 0) return null;



            using (var ms = new MemoryStream())



            {



                foreach (var pa in PlayerAssignments)



                {



                    byte[] rec = pa.ToBytes();



                    ms.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(ms.ToArray());



            }



        }







        public void SavePlayerAssignments(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (PlayerAssignments == null || PlayerAssignments.Count == 0) return;



            byte[] packed = SerializePlayerAssignmentBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "PlayerAssignment.bin"), packed);



        }







        public byte[] SerializePlayerBytes()



        {



            if (Players == null || Players.Count == 0) return null;



            var ordered = Players.OrderBy(p => p.Id).ToList();



            using (var ms = new MemoryStream())



            {



                foreach (var p in ordered)



                {



                    byte[] rec = p.ToBytes();



                    ms.Write(rec, 0, rec.Length);



                }



                return WesysCompressor.Compress(ms.ToArray());



            }



        }







        public void SavePlayers(string folderPath = null)



        {



            if (string.IsNullOrEmpty(folderPath)) folderPath = DatabasePath;



            if (Players == null || Players.Count == 0) return;



            byte[] packed = SerializePlayerBytes();



            if (packed != null)



                AtomicWriteFile(Path.Combine(folderPath, "Player.bin"), packed);



        }







        public DatabaseDiagnosticReport RunCollisionAndIntegrityScan()







        {







            var rep = new DatabaseDiagnosticReport();







            rep.TotalCompetitions = Competitions != null ? Competitions.Count : 0;







            // M91: "custom" ya no es Id >= 40 (la 90 vanilla contaba



            // como custom): es creada en sesion o con reglamento >= 179.



            rep.CustomCompetitions = 0;



            if (Competitions != null)



            {



                foreach (var c in Competitions)



                {



                    RegulationRecord customReg;



                    bool hasCustomReg = RegulationsByCompetition != null



                        && RegulationsByCompetition.TryGetValue(c.Id, out customReg)



                        && customReg != null && customReg.Id >= 179;



                    if (c.IsNew || hasCustomReg) rep.CustomCompetitions++;



                }



            }







            rep.TotalRegulations = Regulations != null ? Regulations.Count : 0;







            rep.CustomRegulations = Regulations != null ? Regulations.Count(r => r.Id >= 179) : 0;







            rep.TotalTeams = Teams != null ? Teams.Count : 0;







            // 1. Proximo Competition ID libre (>= 50)







            int nextComp = 50;







            if (Competitions != null)







            {







                var existingComps = new HashSet<byte>(Competitions.Select(c => c.Id));







                while (nextComp < 255 && existingComps.Contains((byte)nextComp)) nextComp++;







            }







            rep.NextFreeCompetitionId = nextComp;







            // 2. Proximo Regulation ID libre (>= 180)







            int nextReg = 180;







            if (Regulations != null)







            {







                var existingRegs = new HashSet<ushort>(Regulations.Select(r => r.Id));







                while (nextReg < 600 && existingRegs.Contains((ushort)nextReg)) nextReg++;







            }







            rep.NextFreeRegulationId = nextReg;







            // 3. Proximo Slot Sider libre (>= 84)







            int nextSlot = 84;







            var usedSlots = new HashSet<int>(VanillaCompSlots.Values);







            while (nextSlot < 250 && usedSlots.Contains(nextSlot)) nextSlot++;







            rep.NextFreeSlot = nextSlot;







            // 4. Chequeo de colisiones de IDs de Competicion







            if (Competitions != null)







            {







                var dupComps = Competitions.GroupBy(c => c.Id).Where(g => g.Count() > 1);







                foreach (var g in dupComps)







                {







                    rep.CompetitionCollisions.Add(string.Format("Conflicto Critico: Competition ID {0} duplicado ({1} veces: {2})",







                        g.Key, g.Count(), string.Join(", ", g.Select(x => x.Name).ToArray())));







                }







            }







            // 5. Chequeo de colisiones de IDs de Regulacion







            if (Regulations != null)







            {







                var dupRegs = Regulations.GroupBy(r => r.Id).Where(g => g.Count() > 1);







                foreach (var g in dupRegs)







                {







                    rep.RegulationCollisions.Add(string.Format("Conflicto Critico: Regulation ID {0} duplicado ({1} veces)", g.Key, g.Count()));







                }







            }







            // 6. Deteccion de Equipos asignados a multiples ligas domesticas







            if (EntriesByCompetition != null)







            {







                var teamComps = new Dictionary<uint, List<byte>>();







                foreach (var kvp in EntriesByCompetition)







                {







                    byte compId = kvp.Key;







                    var c = Competitions != null ? Competitions.FirstOrDefault(x => x.Id == compId) : null;







                    if (c == null) continue;







                    // M91: solo ligas domesticas (una liga + copa no es



                    // colision) y sin punto ciego en continente 0; el



                    // tipo de reglamento decide (sin fallback por nombre,



                    // que clasificaria mal una "Superliga" como supercopa).



                    RegulationRecord multiReg = null;



                    if (RegulationsByCompetition != null) RegulationsByCompetition.TryGetValue(compId, out multiReg);



                    if (IsCupOrSuperCupCompetition(multiReg)) continue;







                    foreach (var entry in kvp.Value)







                    {







                        if (!teamComps.ContainsKey(entry.TeamId))







                            teamComps[entry.TeamId] = new List<byte>();







                        // M91: cada liga cuenta una vez por equipo



                        // (antes un duplicado en la misma liga contaba doble).



                        if (!teamComps[entry.TeamId].Contains(compId))



                            teamComps[entry.TeamId].Add(compId);







                    }







                }







                foreach (var kvp in teamComps)







                {







                    if (kvp.Value.Count > 1)







                    {







                        string teamName = "Equipo " + kvp.Key;







                        TeamRecord tr;







                        if (TeamsById != null && TeamsById.TryGetValue(kvp.Key, out tr))







                        {







                            teamName = !string.IsNullOrEmpty(tr.NameSpanish) ? tr.NameSpanish : tr.NameEnglish;







                        }







                        var compNameList = new List<string>();







                        foreach (byte cid in kvp.Value)







                        {







                            var comp = Competitions != null ? Competitions.FirstOrDefault(x => x.Id == cid) : null;







                            compNameList.Add(comp != null ? string.Format("[{0}] {1}", cid, comp.Name) : cid.ToString());







                        }







                        rep.TeamMultiLeagueCollisions.Add(string.Format("Colision de Club: '{0}' (ID {1}) asignado a {2} ligas simultaneas: {3}",







                            teamName, kvp.Key, kvp.Value.Count, string.Join(" | ", compNameList.ToArray())));







                    }







                }







            }







            // 7. Advertencias de paridad en ligas







            if (Competitions != null && EntriesByCompetition != null)







            {







                foreach (var c in Competitions)







                {







                    if (EntriesByCompetition.ContainsKey(c.Id))







                    {







                        // M91: la paridad es requisito de ligas, no de copas.



                        RegulationRecord parityReg = null;



                        if (RegulationsByCompetition != null) RegulationsByCompetition.TryGetValue(c.Id, out parityReg);



                        if (IsCupOrSuperCupCompetition(parityReg)) continue;







                        int count = EntriesByCompetition[c.Id].Count;







                        if (count > 0 && count % 2 != 0)







                        {







                            rep.ParityWarnings.Add(string.Format("Aviso de Paridad: Liga [{0}] '{1}' tiene {2} clubes (numero impar). En PES 2021 los calendarios requieren paridad.",







                                c.Id, c.Name, count));







                        }







                    }







                }







            }







            // 8. Integridad de Region y Pais (128 Regiones)







            if (Competitions != null)







            {







                foreach (var c in Competitions)







                {







                    if (c.Group >= 128)







                    {







                        rep.CountryIntegrityWarnings.Add(string.Format("Aviso de Region: Liga [{0}] '{1}' tiene Region {2} (excede el limite ampliado de 128 regiones 0..127).",







                            c.Id, c.Name, c.Group));







                    }







                }







            }







            // B53-9. Coherencia Entries <-> EntriesByCompetition.



            if (Entries != null && EntriesByCompetition != null)



            {



                var inDict = new HashSet<EntryRecord>();



                foreach (var kvp in EntriesByCompetition)



                {



                    bool compExists = Competitions != null && Competitions.Any(c => c.Id == kvp.Key);



                    if (!compExists)



                    {



                        rep.EntryIntegrityWarnings.Add(string.Format("Integridad de entradas: la competicion {0} tiene {1} entradas en el indice pero no existe como competicion.", kvp.Key, kvp.Value.Count));



                    }



                    foreach (var e in kvp.Value)



                    {



                        inDict.Add(e);



                        if (!Entries.Contains(e))



                        {



                            rep.EntryIntegrityWarnings.Add(string.Format("Integridad de entradas: la entrada de equipo {0} (competicion {1}) esta en el indice pero no en la lista maestra.", e.TeamId, kvp.Key));



                        }



                    }



                }



                foreach (var e in Entries)



                {



                    if (!inDict.Contains(e))



                    {



                        rep.EntryIntegrityWarnings.Add(string.Format("Integridad de entradas: la entrada de equipo {0} (competicion {1}) esta en la lista maestra pero no en el indice.", e.TeamId, e.CompetitionId));



                    }



                }



            }







            // B53-10. IDs de entrada duplicados.



            if (Entries != null)



            {



                var dupEntryIds = Entries.GroupBy(e => e.Id).Where(g => g.Count() > 1);



                foreach (var g in dupEntryIds)



                {



                    rep.EntryIntegrityWarnings.Add(string.Format("Integridad de entradas: Entry ID {0} duplicado ({1} veces).", g.Key, g.Count()));



                }



            }







            // B53-11. Posiciones duplicadas o con huecos por competicion



            // (solo ligas: en copas la posicion puede no ser contigua).



            if (EntriesByCompetition != null)



            {



                foreach (var kvp in EntriesByCompetition)



                {



                    RegulationRecord posReg = null;



                    if (RegulationsByCompetition != null) RegulationsByCompetition.TryGetValue(kvp.Key, out posReg);



                    if (IsCupOrSuperCupCompetition(posReg)) continue;



                    var positions = kvp.Value.Select(e => (int)e.Position).OrderBy(p => p).ToList();



                    bool contiguous = true;



                    for (int i = 0; i < positions.Count; i++)



                    {



                        if (positions[i] != i + 1) { contiguous = false; break; }



                    }



                    if (!contiguous)



                    {



                        rep.EntryIntegrityWarnings.Add(string.Format("Integridad de entradas: la competicion {0} tiene posiciones no contiguas o duplicadas ({1}).", kvp.Key, string.Join(", ", positions.Select(p => p.ToString()).ToArray())));



                    }



                }



            }







            // B53-12. TotalTeams del reglamento vs entradas reales (solo



            // ligas) y Group del reglamento fuera de 0..127 (255 es el



            // centinela de liga independiente/division y no se informa).



            if (Regulations != null && EntriesByCompetition != null)



            {



                foreach (var r in Regulations)



                {



                    if (r.CompetitionType == 4)



                    {



                        int realCount = EntriesByCompetition.ContainsKey(r.IdCompetition) ? EntriesByCompetition[r.IdCompetition].Count : 0;



                        if (r.TotalTeams != realCount)



                        {



                            rep.EntryIntegrityWarnings.Add(string.Format("Integridad de reglamento: la competicion {0} declara TotalTeams {1} pero tiene {2} entradas.", r.IdCompetition, r.TotalTeams, realCount));



                        }



                    }



                    if (r.Group >= 128 && r.Group != 255)



                    {



                        rep.CountryIntegrityWarnings.Add(string.Format("Aviso de Region: el reglamento {0} (competicion {1}) tiene Region {2} (excede el limite ampliado de 128 regiones 0..127).", r.Id, r.IdCompetition, r.Group));



                    }



                }



            }







            return rep;







        }







    }







    public class LeaguePyramidTier







    {







        public int TierNumber { get; set; }







        public string TierTitle { get; set; }







        public CompetitionRecord Competition { get; set; }







        public RegulationRecord Regulation { get; set; }







        public int TeamCount { get; set; }







        public bool IsParityEven { get; set; }







        public int PromoteCount { get; set; }







        public int DemoteCount { get; set; }







        public int SubsCount { get; set; }







        public int BenchCount { get; set; }







        public int Continental1Count { get; set; }







        public int Continental2Count { get; set; }







    }







    public class LeaguePyramidSystem







    {







        public ushort CountryId { get; set; }







        public string CountryName { get; set; }







        public List<LeaguePyramidTier> Tiers { get; set; }







        public List<CompetitionRecord> DomesticCups { get; set; }







        public List<CompetitionRecord> SuperCups { get; set; }







        public int TotalClubs { get; set; }







        public bool IsBalanced { get; set; }







        public string BalanceMessage { get; set; }







        public LeaguePyramidSystem()







        {







            Tiers = new List<LeaguePyramidTier>();







            DomesticCups = new List<CompetitionRecord>();







            SuperCups = new List<CompetitionRecord>();







        }







        public override string ToString()







        {







            return string.Format("{0} ({1} divisiones, {2} clubes)", CountryName, Tiers.Count, TotalClubs);







        }







    }







    public class DatabaseDiagnosticReport







    {







        public int TotalCompetitions { get; set; }







        public int CustomCompetitions { get; set; }







        public int TotalRegulations { get; set; }







        public int CustomRegulations { get; set; }







        public int TotalTeams { get; set; }







        public int NextFreeCompetitionId { get; set; }







        public int NextFreeRegulationId { get; set; }







        public int NextFreeSlot { get; set; }







        public List<string> CompetitionCollisions { get; set; }







        public List<string> RegulationCollisions { get; set; }







        public List<string> TeamMultiLeagueCollisions { get; set; }







        public List<string> ParityWarnings { get; set; }







        public List<string> CountryIntegrityWarnings { get; set; }







        public List<string> EntryIntegrityWarnings { get; set; }







        public DatabaseDiagnosticReport()







        {







            CompetitionCollisions = new List<string>();







            RegulationCollisions = new List<string>();







            TeamMultiLeagueCollisions = new List<string>();







            ParityWarnings = new List<string>();







            CountryIntegrityWarnings = new List<string>();







            EntryIntegrityWarnings = new List<string>();







        }







        public bool HasErrors { get { return CompetitionCollisions.Count > 0 || RegulationCollisions.Count > 0 || TeamMultiLeagueCollisions.Count > 0; } }







        public bool HasWarnings { get { return ParityWarnings.Count > 0 || CountryIntegrityWarnings.Count > 0 || EntryIntegrityWarnings.Count > 0; } }







    }







}







