using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EditorLigasPes
{
    public class LeaguePackageManifest
    {
        public string Version = "1.0";
        public string CreatedAt;
        public string Generator = "PES2021EditorPro";

        // Identificación y Nombres
        public byte OriginalCompId;
        public string NameAscii = "NUEVA_LIGA";
        public string NameSpanish = "Nueva Liga";
        public string NameLatam = "Nueva Liga";
        public string NameEnglish = "New League";
        public byte Continent = 4;
        public ushort CountryId = 144;
        public byte TotalTeams = 20;
        public byte CompType = 0; // 0=Liga, 1=Copa, 2=Supercopa
        public bool IsIndependent = true;
        public int Tier = 1;
        public byte ParentCompId = 0;
        public string ParentCompName = "";

        // Reglamentos Oficiales y Formatos
        public byte Changes = 5;
        public byte RegisteredPlayers = 23;
        public byte Rounds = 2;
        public byte TieBreaker = 1;
        public byte CupFormat = 0;
        public bool ExtraTime = false;
        public bool Penalties = false;
        public bool AwayGoals = false;
        public bool ThirdPlace = false;
        public bool SingleLeg = false;
        public byte NumGroups = 1;
        public ushort NumClassifiedTeams = 0;
        public bool FinalChampionship = false;
        public bool Championship = false;
        // Presencia real en el JSON: sin ella, Championship hereda FinalChampionship
        // (parametro nullable de CreateNewLeague) en vez de forzar false.
        public bool HasChampionship;

        // Reglas de Liga Máster
        public int Promote = 3;
        public int Demote = 3;
        public int Continental1 = 4;
        public int Continental2 = 2;
        public byte SeasonType = 0; // 0=Europa (Ago-May), 1=América (Feb-Dic)
        public ushort LinkedCupId = 0;
        public string LinkedCupName = "";
        public ushort LinkedSuperCupId = 0;
        public string LinkedSuperCupName = "";

        // Equipos Participantes
        public List<TeamPackageItem> Teams = new List<TeamPackageItem>();
    }

    public class TeamPackageItem
    {
        public uint OriginalTeamId;
        public bool IsCustom;
        public string NameSpanish;
        public string NameEnglish;
        public string NameDatabase;
        public string ShortName;
        public ushort CountryId;
        public ushort StadiumId;
        public uint CoachId;
        public bool IsNational;
        public string RawBytesBase64;
        // Presencia real en el JSON (no defaults): permite aplicar StadiumId/CoachId/
        // IsNational/NameDatabase solo cuando el manifiesto los trae.
        public bool HasStadiumId;
        public bool HasCoachId;
        public bool HasIsNational;
        public bool HasNameDatabase;
    }

    public class LeagueImportResult
    {
        public bool Success;
        public string ErrorMessage;
        public byte AssignedCompId;
        public ushort AssignedRegId;
        public int TeamsImported;
        public int TeamsRemapped;
        public int EmblemsInstalled;
        public Dictionary<uint, uint> TeamIdMap = new Dictionary<uint, uint>();
        public string SummaryReport;
        // Avisos no fatales (emblemas/bytes fallidos): antes se tragaban con catch { }.
        public List<string> Warnings = new List<string>();
    }

    public static class LeaguePackageManager
    {
        public static bool ExportPackage(DatabaseManager db, byte compId, string outputFilePath, out string message)
        {
            message = "";
            string tempOutputPath = null;
            try
            {
                var comp = db.Competitions.FirstOrDefault(c => c.Id == compId);
                if (comp == null)
                {
                    message = "No se encontró la competición con ID " + compId;
                    return false;
                }

                RegulationRecord reg;
                db.RegulationsByCompetition.TryGetValue(compId, out reg);

                var manifest = new LeaguePackageManifest
                {
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    OriginalCompId = comp.Id,
                    NameAscii = comp.Name ?? "LIGA_" + comp.Id,
                    NameSpanish = (reg != null && !string.IsNullOrEmpty(reg.NameSpanish)) ? reg.NameSpanish : comp.Name,
                    NameLatam = (reg != null && !string.IsNullOrEmpty(reg.NameLatamSpanish)) ? reg.NameLatamSpanish : comp.Name,
                    NameEnglish = (reg != null && !string.IsNullOrEmpty(reg.NameEnglish)) ? reg.NameEnglish : comp.Name,
                    Continent = comp.Continent,
                    CountryId = db.GetLeagueCountryId(comp.Id),
                    TotalTeams = (reg != null && reg.TotalTeams > 0) ? reg.TotalTeams : (byte)20,
                    // Mapeo CompType del manifiesto (0=Liga, 1=Copa, 2=Supercopa): el
                    // formato trae variantes no documentadas en PesDbModel (3=Copa;
                    // 5/7=Supercopa) que aqui se pliegan a 1/2. Ver PesDbModel.cs.
                    CompType = (reg != null && (reg.CompetitionType == 3 || reg.CompetitionType == 1)) ? (byte)1 :
                               ((reg != null && (reg.CompetitionType == 5 || reg.CompetitionType == 2 || reg.CompetitionType == 7)) ? (byte)2 : (byte)0),
                    IsIndependent = (reg == null || reg.IdDivision1 == 0),
                    Tier = db.GetLeagueTier(comp.Id),
                    ParentCompId = 0
                };

                if (!manifest.IsIndependent && reg != null)
                {
                    var parentReg = db.Regulations.FirstOrDefault(r => r.Id == reg.IdDivision1);
                    if (parentReg != null)
                    {
                        manifest.ParentCompId = parentReg.IdCompetition;
                        manifest.ParentCompName = parentReg.NameSpanish ?? parentReg.Name;
                    }
                }

                // Regulaciones
                if (reg != null)
                {
                    manifest.Changes = reg.Changes;
                    manifest.RegisteredPlayers = reg.RegisteredPlayers;
                    manifest.Rounds = reg.LeagueEachTeamMatches;
                    manifest.TieBreaker = reg.LeagueClassification;
                    manifest.CupFormat = reg.CupFormat;
                    manifest.ExtraTime = reg.ExtraTime;
                    manifest.Penalties = reg.PenaltyShootout;
                    manifest.AwayGoals = reg.AwayGoals;
                    manifest.ThirdPlace = reg.ThirdPlace;
                    manifest.SingleLeg = reg.SingleLeg;
                    manifest.NumGroups = reg.NumGroups;
                    manifest.NumClassifiedTeams = reg.NumClassifiedTeams;
                    manifest.FinalChampionship = reg.FinalChampionship;
                    manifest.Championship = reg.Championship;
                }

                // Liga Máster
                manifest.Promote = db.GetLeaguePromote(compId);
                manifest.Demote = db.GetLeagueDemote(compId);
                manifest.Continental1 = db.GetLeagueContinental1(compId);
                manifest.Continental2 = db.GetLeagueContinental2(compId);
                manifest.SeasonType = db.GetLeagueSeasonType(compId);
                manifest.LinkedCupId = db.GetLeagueLinkedCup(compId);
                manifest.LinkedSuperCupId = db.GetLeagueLinkedSuperCup(compId);

                // Equipos participantes
                List<uint> teamIds = new List<uint>();
                if (db.EntriesByCompetition.ContainsKey(compId))
                    teamIds = db.EntriesByCompetition[compId].Select(e => e.TeamId).ToList();

                foreach (uint tid in teamIds)
                {
                    TeamRecord t;
                    if (db.TeamsById.TryGetValue(tid, out t) && t != null)
                    {
                        var tItem = new TeamPackageItem
                        {
                            OriginalTeamId = tid,
                            IsCustom = (tid >= 1000 || t.IsNew),
                            NameSpanish = t.NameSpanish,
                            NameEnglish = t.NameEnglish,
                            NameDatabase = t.NameDatabase,
                            ShortName = t.ShortName,
                            CountryId = t.CountryId,
                            StadiumId = t.StadiumId,
                            CoachId = t.CoachId,
                            IsNational = t.IsNational,
                            RawBytesBase64 = (t.RawBytes != null && t.RawBytes.Length == TeamRecord.RecordSize) ? Convert.ToBase64String(t.RawBytes) : ""
                        };
                        manifest.Teams.Add(tItem);
                    }
                }

                // Crear archivo ZIP contenedor .pesleague en un temporal del mismo
                // directorio: antes se borraba el destino ANTES de crear el ZIP, asi que
                // un fallo a mitad dejaba sin paquete viejo ni nuevo. Solo al terminar
                // bien se reemplaza el destino (File.Replace si existe, Move si no).
                tempOutputPath = outputFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                int missingTeamLogos = 0;

                using (FileStream fs = new FileStream(tempOutputPath, FileMode.Create, FileAccess.Write))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // 1. Escribir manifest.json
                    var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
                    {
                        writer.Write(SerializeManifest(manifest));
                    }

                    // 2. Escribir logo oficial de la liga (si existe).
                    // GetCompetitionEmblem devuelve una imagen nueva en cada llamada,
                    // asi que liberarla con using no afecta a ninguna cache.
                    using (Image compLogo = db.GetCompetitionEmblem(compId))
                    {
                        if (compLogo != null)
                        {
                            var logoEntry = zip.CreateEntry("emblem.png", CompressionLevel.Optimal);
                            using (var entryStream = logoEntry.Open())
                            {
                                compLogo.Save(entryStream, ImageFormat.Png);
                            }
                        }
                    }

                    // 3. Escribir escudos de clubes
                    string liveRoot = db.GetLiveCpkRoot();
                    if (!string.IsNullOrEmpty(liveRoot) && Directory.Exists(liveRoot))
                    {
                        string flagDir = Path.Combine(liveRoot, "common", "render", "symbol", "flag");
                        foreach (var t in manifest.Teams)
                        {
                            string teamImgPath = Path.Combine(flagDir, string.Format("e_{0:D6}_r.png", t.OriginalTeamId));
                            if (!File.Exists(teamImgPath))
                                teamImgPath = Path.Combine(flagDir, string.Format("e_{0:D6}_f.png", t.OriginalTeamId));

                            if (File.Exists(teamImgPath))
                            {
                                var teamLogoEntry = zip.CreateEntry(string.Format("teams/{0}.png", t.OriginalTeamId), CompressionLevel.Optimal);
                                using (var entryStream = teamLogoEntry.Open())
                                using (var fileStream = File.OpenRead(teamImgPath))
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                            }
                            else
                            {
                                missingTeamLogos++;
                            }
                        }
                    }
                }

                if (File.Exists(outputFilePath))
                    File.Replace(tempOutputPath, outputFilePath, null);
                else
                    File.Move(tempOutputPath, outputFilePath);

                message = string.Format("Liga '{0}' exportada con éxito.\nTotal equipos: {1}\nEscudos de equipo no encontrados: {3}\nArchivo generado: {2}",
                    manifest.NameSpanish, manifest.Teams.Count, Path.GetFileName(outputFilePath), missingTeamLogos);
                return true;
            }
            catch (Exception ex)
            {
                // El temporal fallido no debe quedar tirado ni tocar el destino viejo.
                try { if (!string.IsNullOrEmpty(tempOutputPath) && File.Exists(tempOutputPath)) File.Delete(tempOutputPath); }
                catch { }
                message = "Error al exportar liga: " + ex.Message;
                return false;
            }
        }

        public static LeagueImportResult ImportPackage(DatabaseManager db, string packageFilePath)
        {
            var res = new LeagueImportResult();
            if (!File.Exists(packageFilePath))
            {
                res.Success = false;
                res.ErrorMessage = "El archivo especificado no existe: " + packageFilePath;
                return res;
            }

            try
            {
                LeaguePackageManifest manifest = null;
                byte[] compLogoBytes = null;
                var teamLogoBytes = new Dictionary<uint, byte[]>();

                using (FileStream fs = new FileStream(packageFilePath, FileMode.Open, FileAccess.Read))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    // Validación defensiva anti zip-bombs y desbordamiento de memoria
                    long totalUncompressedSize = 0;
                    int entryCount = 0;
                    foreach (var entry in zip.Entries)
                    {
                        entryCount++;
                        if (entryCount > 500)
                        {
                            res.Success = false;
                            res.ErrorMessage = "El paquete .pesleague contiene más de 500 archivos. Importación cancelada por seguridad.";
                            return res;
                        }
                        totalUncompressedSize += entry.Length;
                        if (totalUncompressedSize > 100 * 1024 * 1024)
                        {
                            res.Success = false;
                            res.ErrorMessage = "El tamaño descomprimido del paquete excede los 100 MB seguros. Posible archivo corrupto o zip-bomb.";
                            return res;
                        }
                    }

                    var manifestEntry = zip.GetEntry("manifest.json");
                    if (manifestEntry != null && manifestEntry.Length > 5 * 1024 * 1024)
                    {
                        res.Success = false;
                        res.ErrorMessage = "El archivo manifest.json excede el límite máximo seguro de 5 MB.";
                        return res;
                    }
                    if (manifestEntry == null)
                    {
                        res.Success = false;
                        res.ErrorMessage = "El paquete .pesleague no contiene el archivo manifest.json requerido.";
                        return res;
                    }

                    using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
                    {
                        manifest = DeserializeManifest(reader.ReadToEnd());
                    }

                    // Leer logo de la liga
                    var logoEntry = zip.GetEntry("emblem.png");
                    if (logoEntry != null)
                    {
                        if (logoEntry.Length > 5 * 1024 * 1024)
                        {
                            res.Success = false;
                            res.ErrorMessage = "El escudo de la liga (emblem.png) excede el límite máximo seguro de 5 MB. Importación cancelada por seguridad.";
                            return res;
                        }
                        using (var ms = new MemoryStream())
                        {
                            logoEntry.Open().CopyTo(ms);
                            compLogoBytes = ms.ToArray();
                        }
                    }

                    // Leer escudos de equipos
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.StartsWith("teams/") && entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(entry.FullName);
                            uint origTid;
                            if (uint.TryParse(fileName, out origTid))
                            {
                                if (entry.Length > 5 * 1024 * 1024)
                                {
                                    res.Success = false;
                                    res.ErrorMessage = "El escudo del equipo '" + entry.FullName + "' excede el límite máximo seguro de 5 MB. Importación cancelada por seguridad.";
                                    return res;
                                }
                                using (var ms = new MemoryStream())
                                {
                                    entry.Open().CopyTo(ms);
                                    teamLogoBytes[origTid] = ms.ToArray();
                                }
                            }
                        }
                    }
                }

                if (manifest == null)
                {
                    res.Success = false;
                    res.ErrorMessage = "No se pudieron procesar los datos del manifiesto de la liga.";
                    return res;
                }

                // 1. ASIGNACIÓN SEGURA DE ID DE COMPETICIÓN (Collision-Proof)
                byte assignedCompId = manifest.OriginalCompId;
                bool compCollision = db.Competitions.Any(c => c.Id == assignedCompId) || (assignedCompId < 50);
                if (compCollision)
                {
                    var freeIds = db.GetAvailableCompetitionIds();
                    if (freeIds.Count > 0) assignedCompId = freeIds[0];
                    else
                    {
                        bool foundSlot = false;
                        for (int b = 50; b <= 254; b++)
                        {
                            if (!db.Competitions.Any(c => c.Id == (byte)b))
                            {
                                assignedCompId = (byte)b;
                                foundSlot = true;
                                break;
                            }
                        }
                        if (!foundSlot)
                        {
                            res.Success = false;
                            res.ErrorMessage = "No hay ranuras (slots) libres disponibles para alojar la nueva competición en PES 2021.";
                            return res;
                        }
                    }
                }
                res.AssignedCompId = assignedCompId;

                // 2. TABLA DE TRADUCCIÓN Y REMAPEO DE EQUIPOS (ID Relational Remapping)
                var usedTeamIds = new HashSet<uint>(db.Teams.Select(t => t.Id));
                uint nextAvailableTeamId = 1000;
                while (usedTeamIds.Contains(nextAvailableTeamId)) nextAvailableTeamId++;

                var mappedTeamIds = new List<uint>();
                var seenOrigTeamIds = new HashSet<uint>();
                foreach (var tItem in manifest.Teams)
                {
                    uint origTid = tItem.OriginalTeamId;
                    // Equipos duplicados en el manifiesto: se procesa solo la primera
                    // aparicion (antes generaba entradas duplicadas en la liga).
                    if (!seenOrigTeamIds.Add(origTid)) continue;
                    uint newTid;

                    // Si no es personalizado y existe en la base destino con el mismo ID, se conserva
                    if (!tItem.IsCustom && db.TeamsById.ContainsKey(origTid))
                    {
                        newTid = origTid;
                    }
                    else if (!usedTeamIds.Contains(origTid) && origTid >= 500)
                    {
                        newTid = origTid;
                        usedTeamIds.Add(origTid);
                    }
                    else
                    {
                        newTid = nextAvailableTeamId++;
                        while (usedTeamIds.Contains(nextAvailableTeamId)) nextAvailableTeamId++;
                        usedTeamIds.Add(newTid);
                        res.TeamsRemapped++;
                    }

                    res.TeamIdMap[origTid] = newTid;
                    mappedTeamIds.Add(newTid);

                    // Insertar equipo en la base de datos si no existe
                    if (!db.TeamsById.ContainsKey(newTid))
                    {
                        TeamRecord newTeam = null;
                        if (!string.IsNullOrEmpty(tItem.RawBytesBase64))
                        {
                            try
                            {
                                byte[] raw = Convert.FromBase64String(tItem.RawBytesBase64);
                                if (raw.Length == TeamRecord.RecordSize)
                                {
                                    BitConverter.GetBytes(newTid).CopyTo(raw, 8); // Reasignar ID en offset 8
                                    newTeam = TeamRecord.FromBytes(raw, 0);
                                    newTeam.Id = newTid;
                                    newTeam.NameSpanish = tItem.NameSpanish;
                                    newTeam.NameEnglish = tItem.NameEnglish;
                                    newTeam.ShortName = tItem.ShortName;
                                    newTeam.CountryId = tItem.CountryId;
                                    newTeam.IsNew = true;
                                    newTeam.IsModified = true;
                                    newTeam.RawBytes = newTeam.ToBytes();
                                }
                            }
                            catch (Exception rawEx)
                            {
                                res.Warnings.Add("Equipo " + origTid + ": bytes crudos ilegibles o invalidos, se uso la plantilla (" + rawEx.Message + ").");
                            }
                        }

                        if (newTeam == null)
                        {
                            var template = db.Teams.FirstOrDefault(t => t.CountryId == tItem.CountryId) ?? db.Teams.FirstOrDefault();
                            if (template != null)
                            {
                                string name = !string.IsNullOrEmpty(tItem.NameSpanish) ? tItem.NameSpanish : (tItem.NameEnglish ?? ("Club " + newTid));
                                newTeam = template.CloneWithNewId(newTid, name, tItem.ShortName ?? "CLO");
                                newTeam.CountryId = tItem.CountryId;
                            }
                            else
                            {
                                newTeam = new TeamRecord();
                                newTeam.Id = newTid;
                                newTeam.NameSpanish = tItem.NameSpanish ?? ("Club " + newTid);
                                newTeam.NameEnglish = tItem.NameEnglish ?? newTeam.NameSpanish;
                                newTeam.ShortName = tItem.ShortName ?? "CLB";
                                newTeam.CountryId = tItem.CountryId;
                                newTeam.RawBytes = new byte[TeamRecord.RecordSize];
                                newTeam.RawBytes = newTeam.ToBytes();
                            }
                        }

                        // Campos del manifiesto que antes se ignoraban: solo se aplican
                        // si el equipo del manifiesto los trae (Has*); si faltan, se
                        // conservan los del clon/raw (no se pisan con los defaults 0).
                        if (tItem.HasStadiumId) newTeam.StadiumId = tItem.StadiumId;
                        if (tItem.HasCoachId) newTeam.CoachId = tItem.CoachId;
                        // El DT del paquete puede no existir en la base destino
                        // (o venir heredado de una plantilla): nunca dejar un
                        // CoachId colgado; el club recibe el suyo (Punto E).
                        if (newTeam.CoachId == 0 || !db.CoachesById.ContainsKey(newTeam.CoachId))
                            newTeam.CoachId = db.CreateNewCoach("DT " + newTeam.NameSpanish, newTeam.CountryId).Id;
                        if (tItem.HasIsNational) newTeam.IsNational = tItem.IsNational;
                        if (tItem.HasNameDatabase && !string.IsNullOrEmpty(tItem.NameDatabase)) newTeam.NameDatabase = tItem.NameDatabase;
                        newTeam.RawBytes = newTeam.ToBytes();

                        db.Teams.Add(newTeam);
                        db.TeamsById[newTid] = newTeam;
                        res.TeamsImported++;
                    }
                }

                // Sin equipos no hay liga que crear (antes se llegaba a CreateNewLeague
                // con 0 equipos: una liga que el juego oculta). Mas de 255 envolveria
                // el (byte) del conteo (p. ej. 300 -> 44) antes de validar.
                if (mappedTeamIds.Count == 0)
                {
                    res.Success = false;
                    res.ErrorMessage = "El paquete no contiene equipos: no se puede importar una liga de 0 equipos.";
                    return res;
                }
                if (mappedTeamIds.Count > 255)
                {
                    res.Success = false;
                    res.ErrorMessage = "El paquete contiene " + mappedTeamIds.Count + " equipos y supera el maximo de 255 por liga.";
                    return res;
                }

                // 3. CREAR LIGA VÍA DATABASE MANAGER (Crea Competition, Regulation, Entries y Reglas LM de forma atómica)
                db.CreateNewLeague(
                    assignedCompId,
                    manifest.NameAscii,
                    manifest.NameSpanish,
                    manifest.NameLatam,
                    manifest.NameEnglish,
                    manifest.Continent,
                    (byte)mappedTeamIds.Count,
                    manifest.CompType,
                    manifest.IsIndependent,
                    manifest.ParentCompId,
                    mappedTeamIds,
                    manifest.CountryId,
                    manifest.Changes,
                    manifest.RegisteredPlayers,
                    manifest.Rounds,
                    manifest.TieBreaker,
                    manifest.CupFormat,
                    manifest.ExtraTime,
                    manifest.Penalties,
                    manifest.AwayGoals,
                    manifest.ThirdPlace,
                    manifest.SingleLeg,
                    manifest.Promote,
                    manifest.Demote,
                    manifest.Continental1,
                    manifest.Continental2,
                    manifest.NumGroups,
                    manifest.NumClassifiedTeams,
                    manifest.FinalChampionship,
                    manifest.SeasonType,
                    manifest.LinkedCupId,
                    manifest.LinkedSuperCupId,
                    manifest.HasChampionship ? (bool?)manifest.Championship : null
                );

                RegulationRecord createdReg;
                if (db.RegulationsByCompetition.TryGetValue(assignedCompId, out createdReg) && createdReg != null)
                {
                    res.AssignedRegId = createdReg.Id;
                }

                // M149: Tier no tiene parametro (no es un campo almacenado: deriva de
                // la cadena IdDivision1 que CreateNewLeague arma desde ParentCompId).
                // Si el Tier del manifiesto no coincide con el derivado, la cadena
                // manda (es lo que usa el juego) y se avisa en vez de perder el dato.
                int derivedTier = db.GetLeagueTier(assignedCompId);
                if (derivedTier != manifest.Tier)
                    res.Warnings.Add("Tier del manifiesto (" + manifest.Tier + ") distinto del derivado de la cadena de divisiones (" + derivedTier + "): se usa el derivado.");

                // 4. EXTRAER Y REMAPEAR ASSETS (Escudos de Liga y Clubes)
                string liveRoot = db.GetLiveCpkRoot();
                if (!string.IsNullOrEmpty(liveRoot) && Directory.Exists(liveRoot))
                {
                    if (compLogoBytes != null && compLogoBytes.Length > 0)
                    {
                        try
                        {
                            using (var ms = new MemoryStream(compLogoBytes))
                            using (var img = Image.FromStream(ms))
                            {
                                db.SaveCompetitionEmblem(assignedCompId, img);
                                res.EmblemsInstalled++;
                            }
                        }
                        catch (Exception logoEx)
                        {
                            res.Warnings.Add("Escudo de la liga no instalado (" + logoEx.Message + ").");
                        }
                    }

                    foreach (var pair in teamLogoBytes)
                    {
                        uint origTid = pair.Key;
                        byte[] bytes = pair.Value;
                        if (res.TeamIdMap.ContainsKey(origTid))
                        {
                            uint newTid = res.TeamIdMap[origTid];
                            try
                            {
                                using (var ms = new MemoryStream(bytes))
                                using (var img = Image.FromStream(ms))
                                {
                                    ImageProcessor.SaveTeamCrests(img, liveRoot, newTid);
                                    res.EmblemsInstalled++;
                                }
                            }
                            catch (Exception teamLogoEx)
                            {
                                res.Warnings.Add("Escudo del equipo " + origTid + " no instalado (" + teamLogoEx.Message + ").");
                            }
                        }
                    }
                }

                // 5. GUARDAR REGLAS Y REGENERAR CONFIGURACIÓN DE SIDER
                db.SaveCustomLeagueRules();

                res.Success = true;
                var sb = new StringBuilder();
                sb.AppendLine("╔════════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("   PAQUETE MODULAR .PESLEAGUE INSTALADO CON ÉXITO");
                sb.AppendLine("╚════════════════════════════════════════════════════════════════════════════╝\n");
                sb.AppendLine(string.Format("🏆 Liga: [{0}] {1}", assignedCompId, manifest.NameSpanish));
                if (assignedCompId != manifest.OriginalCompId)
                    sb.AppendLine(string.Format("💡 ID Reasignado Automáticamente: #{0} ➔ #{1} (Para evitar colisiones)", manifest.OriginalCompId, assignedCompId));
                else
                    sb.AppendLine(string.Format("✔️ ID Asignado: #{0} (Slot libre)", assignedCompId));

                sb.AppendLine(string.Format("📋 Formato: {0} zonas, {1} clasificados a playoffs, {2} rondas", manifest.NumGroups, manifest.NumClassifiedTeams, manifest.Rounds));
                sb.AppendLine(string.Format("📅 Calendario Liga Máster: {0}", (manifest.SeasonType == 1) ? "Sudamericano / Anual (Feb-Dic)" : "Europeo (Ago-May)"));
                sb.AppendLine(string.Format("🛡️ Total Equipos Participantes: {0}", mappedTeamIds.Count));
                sb.AppendLine(string.Format("🔄 Equipos Remapeados a Nuevos IDs: {0}", res.TeamsRemapped));
                sb.AppendLine(string.Format("🖼️ Escudos y Gráficos Instalados en LiveCPK: {0}", res.EmblemsInstalled));
                sb.AppendLine("\n✔️ Base de datos, fl26world.txt y league_rules.json sincronizados automáticamente.");

                if (res.Warnings.Count > 0)
                {
                    sb.AppendLine("\nAvisos durante la importación (la liga se creó igual):");
                    foreach (var w in res.Warnings) sb.AppendLine(" - " + w);
                }

                res.SummaryReport = sb.ToString();
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.ErrorMessage = "Error crítico al importar paquete: " + ex.Message;
                return res;
            }
        }

        private static string SerializeManifest(LeaguePackageManifest m)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format("  \"version\": \"{0}\",", m.Version));
            sb.AppendLine(string.Format("  \"created_at\": \"{0}\",", m.CreatedAt));
            sb.AppendLine(string.Format("  \"generator\": \"{0}\",", m.Generator));
            sb.AppendLine(string.Format("  \"original_id\": {0},", m.OriginalCompId));
            sb.AppendLine(string.Format("  \"name_ascii\": \"{0}\",", EscapeJson(m.NameAscii)));
            sb.AppendLine(string.Format("  \"name_spanish\": \"{0}\",", EscapeJson(m.NameSpanish)));
            sb.AppendLine(string.Format("  \"name_latam\": \"{0}\",", EscapeJson(m.NameLatam)));
            sb.AppendLine(string.Format("  \"name_english\": \"{0}\",", EscapeJson(m.NameEnglish)));
            sb.AppendLine(string.Format("  \"continent\": {0},", m.Continent));
            sb.AppendLine(string.Format("  \"country_id\": {0},", m.CountryId));
            sb.AppendLine(string.Format("  \"total_teams\": {0},", m.TotalTeams));
            sb.AppendLine(string.Format("  \"comp_type\": {0},", m.CompType));
            sb.AppendLine(string.Format("  \"is_independent\": {0},", m.IsIndependent ? "true" : "false"));
            sb.AppendLine(string.Format("  \"tier\": {0},", m.Tier));
            sb.AppendLine(string.Format("  \"parent_comp_id\": {0},", m.ParentCompId));
            sb.AppendLine(string.Format("  \"parent_comp_name\": \"{0}\",", EscapeJson(m.ParentCompName)));

            // Regulation
            sb.AppendLine(string.Format("  \"changes\": {0},", m.Changes));
            sb.AppendLine(string.Format("  \"registered_players\": {0},", m.RegisteredPlayers));
            sb.AppendLine(string.Format("  \"rounds\": {0},", m.Rounds));
            sb.AppendLine(string.Format("  \"tie_breaker\": {0},", m.TieBreaker));
            sb.AppendLine(string.Format("  \"cup_format\": {0},", m.CupFormat));
            sb.AppendLine(string.Format("  \"extra_time\": {0},", m.ExtraTime ? "true" : "false"));
            sb.AppendLine(string.Format("  \"penalties\": {0},", m.Penalties ? "true" : "false"));
            sb.AppendLine(string.Format("  \"away_goals\": {0},", m.AwayGoals ? "true" : "false"));
            sb.AppendLine(string.Format("  \"third_place\": {0},", m.ThirdPlace ? "true" : "false"));
            sb.AppendLine(string.Format("  \"single_leg\": {0},", m.SingleLeg ? "true" : "false"));
            sb.AppendLine(string.Format("  \"num_groups\": {0},", m.NumGroups));
            sb.AppendLine(string.Format("  \"num_playoffs\": {0},", m.NumClassifiedTeams));
            sb.AppendLine(string.Format("  \"final_championship\": {0},", m.FinalChampionship ? "true" : "false"));
            sb.AppendLine(string.Format("  \"championship\": {0},", m.Championship ? "true" : "false"));

            // Master League
            sb.AppendLine(string.Format("  \"promote\": {0},", m.Promote));
            sb.AppendLine(string.Format("  \"demote\": {0},", m.Demote));
            sb.AppendLine(string.Format("  \"continental1\": {0},", m.Continental1));
            sb.AppendLine(string.Format("  \"continental2\": {0},", m.Continental2));
            sb.AppendLine(string.Format("  \"season_type\": {0},", m.SeasonType));
            sb.AppendLine(string.Format("  \"linked_cup_id\": {0},", m.LinkedCupId));
            sb.AppendLine(string.Format("  \"linked_supercup_id\": {0},", m.LinkedSuperCupId));

            // Teams Array
            sb.AppendLine("  \"teams\": [");
            for (int i = 0; i < m.Teams.Count; i++)
            {
                var t = m.Teams[i];
                sb.AppendLine("    {");
                sb.AppendLine(string.Format("      \"original_id\": {0},", t.OriginalTeamId));
                sb.AppendLine(string.Format("      \"is_custom\": {0},", t.IsCustom ? "true" : "false"));
                sb.AppendLine(string.Format("      \"name_spanish\": \"{0}\",", EscapeJson(t.NameSpanish)));
                sb.AppendLine(string.Format("      \"name_english\": \"{0}\",", EscapeJson(t.NameEnglish)));
                sb.AppendLine(string.Format("      \"name_database\": \"{0}\",", EscapeJson(t.NameDatabase)));
                sb.AppendLine(string.Format("      \"short_name\": \"{0}\",", EscapeJson(t.ShortName)));
                sb.AppendLine(string.Format("      \"country_id\": {0},", t.CountryId));
                sb.AppendLine(string.Format("      \"stadium_id\": {0},", t.StadiumId));
                sb.AppendLine(string.Format("      \"coach_id\": {0},", t.CoachId));
                sb.AppendLine(string.Format("      \"is_national\": {0},", t.IsNational ? "true" : "false"));
                sb.AppendLine(string.Format("      \"raw_bytes\": \"{0}\"", t.RawBytesBase64 ?? ""));
                sb.Append("    }");
                if (i < m.Teams.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static int GetJsonClampedInt(string json, string key, int min, int max, int defaultVal)
        {
            int val = GetJsonInt(json, key, defaultVal);
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private static LeaguePackageManifest DeserializeManifest(string json)
        {
            var m = new LeaguePackageManifest();
            // Sin original_id no se reclama el 50: 0 (<50) fuerza la ruta de slot libre.
            m.OriginalCompId = (byte)GetJsonClampedInt(json, "original_id", 0, 255, 0);
            m.NameAscii = GetJsonString(json, "name_ascii", "LIGA");
            m.NameSpanish = GetJsonString(json, "name_spanish", m.NameAscii);
            m.NameLatam = GetJsonString(json, "name_latam", m.NameSpanish);
            m.NameEnglish = GetJsonString(json, "name_english", m.NameAscii);
            m.Continent = (byte)GetJsonClampedInt(json, "continent", 1, 4, 4);
            m.CountryId = (ushort)GetJsonClampedInt(json, "country_id", 0, 65535, 144);
            m.TotalTeams = (byte)GetJsonClampedInt(json, "total_teams", 2, 60, 20);
            m.CompType = (byte)GetJsonClampedInt(json, "comp_type", 0, 10, 0);
            m.IsIndependent = GetJsonBool(json, "is_independent", true);
            m.Tier = GetJsonClampedInt(json, "tier", 1, 7, 1);
            m.ParentCompId = (byte)GetJsonClampedInt(json, "parent_comp_id", 0, 255, 0);
            m.ParentCompName = GetJsonString(json, "parent_comp_name", "");

            m.Changes = (byte)GetJsonClampedInt(json, "changes", 1, 10, 5);
            m.RegisteredPlayers = (byte)GetJsonClampedInt(json, "registered_players", 11, 35, 23);
            m.Rounds = (byte)GetJsonClampedInt(json, "rounds", 1, 4, 2);
            m.TieBreaker = (byte)GetJsonClampedInt(json, "tie_breaker", 0, 5, 1);
            m.CupFormat = (byte)GetJsonClampedInt(json, "cup_format", 0, 5, 0);
            m.ExtraTime = GetJsonBool(json, "extra_time", false);
            m.Penalties = GetJsonBool(json, "penalties", false);
            m.AwayGoals = GetJsonBool(json, "away_goals", false);
            m.ThirdPlace = GetJsonBool(json, "third_place", false);
            m.SingleLeg = GetJsonBool(json, "single_leg", false);
            m.NumGroups = (byte)GetJsonClampedInt(json, "num_groups", 1, 16, 1);
            m.NumClassifiedTeams = (ushort)GetJsonClampedInt(json, "num_playoffs", 0, 32, 0);
            m.FinalChampionship = GetJsonBool(json, "final_championship", false);
            m.Championship = GetJsonBool(json, "championship", false);
            m.HasChampionship = HasJsonKey(json, "championship");

            m.Promote = GetJsonClampedInt(json, "promote", 0, 10, 3);
            m.Demote = GetJsonClampedInt(json, "demote", 0, 10, 3);
            m.Continental1 = GetJsonClampedInt(json, "continental1", 0, 10, 4);
            m.Continental2 = GetJsonClampedInt(json, "continental2", 0, 10, 2);
            m.SeasonType = (byte)GetJsonClampedInt(json, "season_type", 0, 10, 0);
            m.LinkedCupId = (ushort)GetJsonClampedInt(json, "linked_cup_id", 0, 65535, 0);
            m.LinkedSuperCupId = (ushort)GetJsonClampedInt(json, "linked_supercup_id", 0, 65535, 0);

            // Parse teams array por escaneo con profundidad de llaves y respeto por
            // strings: el regex anterior \{([^{}]+)\} partia un equipo si su nombre
            // contenia { o } (y GetJsonString ya maneja comillas escapadas).
            int idxTeams = json.IndexOf("\"teams\"");
            if (idxTeams >= 0)
            {
                int arrStart = json.IndexOf('[', idxTeams);
                if (arrStart >= 0)
                {
                    var blocks = new List<string>();
                    int depth = 0;
                    bool inStr = false;
                    bool esc = false;
                    int objStart = -1;
                    for (int i = arrStart + 1; i < json.Length; i++)
                    {
                        char ch = json[i];
                        if (inStr)
                        {
                            if (esc) esc = false;
                            else if (ch == '\\') esc = true;
                            else if (ch == '"') inStr = false;
                        }
                        else
                        {
                            if (ch == '"') inStr = true;
                            else if (ch == '{')
                            {
                                if (depth == 0) objStart = i;
                                depth++;
                            }
                            else if (ch == '}')
                            {
                                depth--;
                                if (depth == 0 && objStart >= 0)
                                {
                                    blocks.Add(json.Substring(objStart, i - objStart + 1));
                                    objStart = -1;
                                }
                            }
                            else if (ch == ']' && depth == 0) break;
                        }
                    }
                    foreach (string block in blocks)
                    {
                    var t = new TeamPackageItem
                    {
                        OriginalTeamId = (uint)GetJsonClampedInt(block, "original_id", 0, int.MaxValue, 0),
                        IsCustom = GetJsonBool(block, "is_custom", false),
                        NameSpanish = GetJsonString(block, "name_spanish", ""),
                        NameEnglish = GetJsonString(block, "name_english", ""),
                        NameDatabase = GetJsonString(block, "name_database", ""),
                        ShortName = GetJsonString(block, "short_name", ""),
                        CountryId = (ushort)GetJsonClampedInt(block, "country_id", 0, 65535, 144),
                        StadiumId = (ushort)GetJsonClampedInt(block, "stadium_id", 0, 65535, 0),
                        CoachId = (uint)GetJsonClampedInt(block, "coach_id", 0, int.MaxValue, 0),
                        IsNational = GetJsonBool(block, "is_national", false),
                        RawBytesBase64 = GetJsonString(block, "raw_bytes", ""),
                        HasStadiumId = HasJsonKey(block, "stadium_id"),
                        HasCoachId = HasJsonKey(block, "coach_id"),
                        HasIsNational = HasJsonKey(block, "is_national"),
                        HasNameDatabase = HasJsonKey(block, "name_database")
                    };
                        if (t.OriginalTeamId > 0) m.Teams.Add(t);
                    }
                }
            }

            return m;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '"') sb.Append("\"");
                else if (ch == '\n') sb.Append("\n");
                else if (ch == '\r') sb.Append("\r");
                else if (ch == '\t') sb.Append("\t");
                else if (ch < 0x20) sb.Append(string.Format("\\u{0:x4}", (int)ch));
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static string GetJsonString(string json, string key, string def)
        {
            // Patron con escapes: (?:\\.|[^"\\])* consume \" sin cortar el valor.
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            if (m.Success) return UnescapeJson(m.Groups[1].Value);
            return def;
        }

        private static string UnescapeJson(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char e = s[i];
                    if (e == '"') sb.Append('"');
                    else if (e == '\\') sb.Append('\\');
                    else if (e == '/') sb.Append('/');
                    else if (e == 'n') sb.Append('\n');
                    else if (e == 'r') sb.Append('\r');
                    else if (e == 't') sb.Append('\t');
                    else if (e == 'b') sb.Append('\b');
                    else if (e == 'f') sb.Append('\f');
                    else if (e == 'u' && i + 4 < s.Length)
                    {
                        int code;
                        if (int.TryParse(s.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        else sb.Append('u');
                    }
                    else sb.Append(e);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static bool HasJsonKey(string json, string key)
        {
            return Regex.IsMatch(json, "\"" + key + "\"\\s*:");
        }

        private static int GetJsonInt(string json, string key, int def)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            if (m.Success)
            {
                int val;
                if (int.TryParse(m.Groups[1].Value, out val)) return val;
            }
            return def;
        }

        private static bool GetJsonBool(string json, string key, bool def)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            return def;
        }
    }
}
