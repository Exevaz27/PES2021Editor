using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace EditorLigasPes
{
    /// <summary>
    /// Modelos de datos binarios forenses para PES 2021 / SP Football Life.
    /// Desarrollado por EXEVAZ27.
    /// </summary>
    public static class BinaryHelper
    {
        public static string ReadNullTerminatedString(byte[] buffer, int offset, int maxLength, Encoding encoding)
        {
            int len = 0;
            while (len < maxLength && offset + len < buffer.Length && buffer[offset + len] != 0)
                len++;
            return encoding.GetString(buffer, offset, len);
        }

        public static void WriteNullTerminatedString(byte[] buffer, int offset, int maxLength, string text, Encoding encoding)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || maxLength <= 0 || (long)offset + maxLength > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "WriteNullTerminatedString fuera del buffer: offset=" + offset + ", maxLength=" + maxLength + ", buffer=" + buffer.Length + ".");
            byte[] encoded = encoding.GetBytes(text ?? string.Empty);
            int copyLen = Math.Min(encoded.Length, maxLength - 1);
            Buffer.BlockCopy(encoded, 0, buffer, offset, copyLen);
            // Rellenar resto con ceros
            for (int i = copyLen; i < maxLength; i++)
            {
                buffer[offset + i] = 0;
            }
        }
    }

    public class CompetitionRecord
    {
        public const int RecordSize = 36;

        public bool IsModified { get; set; }
        public bool IsNew { get; set; }

        public short Value1;
        public byte Group;
        public byte IdParentCompetition;
        public byte Id;
        public byte Continent; // 2=Europa, 4=América Sur, 3=Asia, 1=Otros/Mundial
        public bool Value2;
        public bool Licenced;
        public bool Fake;
        public bool Value3;
        public string Name; // ASCII 28 bytes
        public byte[] RawBytes;

        public static CompetitionRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var c = new CompetitionRecord();
            c.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, c.RawBytes, 0, RecordSize);
            c.IsModified = false;
            c.IsNew = false;

            c.Value1 = BitConverter.ToInt16(buffer, offset + 0);
            // Decodificación de 7 bits para Región/País (0..127) según estándar fl26reg128:
            // bits 3..7 = bits 0..4, bit 0 = bit 5, bit 1 = bit 6 (100% compatible con Konami para 0..28)
            byte b3 = buffer[offset + 3];
            c.Group = (byte)(((b3 >> 3) & 0x1F) | ((b3 & 0x01) << 5) | (((b3 >> 1) & 0x01) << 6));
            c.IdParentCompetition = buffer[offset + 4];
            c.Id = buffer[offset + 5];

            byte b6 = buffer[offset + 6];
            c.Continent = (byte)(b6 & 0x0F);
            c.Value2 = (b6 & (1 << 4)) != 0;
            c.Licenced = (b6 & (1 << 5)) != 0;
            c.Fake = (b6 & (1 << 6)) != 0;
            c.Value3 = (b6 & (1 << 7)) != 0;

            c.Name = BinaryHelper.ReadNullTerminatedString(buffer, offset + 8, 28, Encoding.ASCII);
            return c;
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", Id, Name);
        }

        public byte[] ToBytes()
        {
            byte[] buf = (RawBytes != null && RawBytes.Length >= 36) ? (byte[])RawBytes.Clone() : new byte[36];
            if (Group > 127)
                throw new ArgumentOutOfRangeException("Group", "Group=" + Group + " no cabe en 7 bits (maximo 127): antes se truncaba en silencio (200 -> 72).");
            BitConverter.GetBytes(Value1).CopyTo(buf, 0);

            // Codificación de 7 bits para Región/País (0..127) según estándar fl26reg128:
            // bits 3..7 = bits 0..4, bit 0 = bit 5, bit 1 = bit 6 (preserva bit 2 de Konami)
            buf[3] = (byte)((buf[3] & 0x04) | ((Group & 0x1F) << 3) | ((Group >> 5) & 0x01) | (((Group >> 6) & 0x01) << 1));
            buf[4] = IdParentCompetition;
            buf[5] = Id;

            // Byte 6
            byte b6 = (byte)(Continent & 0x0F);
            if (Value2) b6 |= (1 << 4);
            if (Licenced) b6 |= (1 << 5);
            if (Fake) b6 |= (1 << 6);
            if (Value3) b6 |= (1 << 7);
            buf[6] = b6;

            BinaryHelper.WriteNullTerminatedString(buf, 8, 28, Name, Encoding.ASCII);
            return buf;
        }
    }

    public class RegulationRecord
    {
        public const int RecordSize = 2352;

        public bool IsModified { get; set; }
        public bool IsNew { get; set; }

        public ushort IdDivision2;
        public ushort Id;
        public ushort IdDivision1;
        public short Value2020_1;
        public byte IdCompetition;
        public byte CompetitionType; // 0=Liga, 1=Copa, 2=Supercopa, 4=Liga estándar. El importador de paquetes ademas trata 3 como Copa y 5/7 como Supercopa (ver LeaguePackageManager.ExportPackage): valores observados en el codigo, no en la documentacion del formato.
        public byte Group;
        public byte TotalTeams; // Max 63 (6 bits)
        public byte RegisteredPlayers;
        public byte Changes; // Cambios permitidos
        public ushort NumClassifiedTeams;
        public bool FinalChampionship;
        public bool Championship;
        public byte NumGroups;
        public byte LeagueClassification;
        public byte CupFormat;
        public byte LeagueEachTeamMatches;
        public bool IsDivision1; // Byte 17 bit 7
        public bool IsDivision2; // Byte 18 bit 0
        public byte SanctionType;
        public bool AwayGoals;            // Byte 19 bit 0 (Regla de Gol de Visitante en desempates globales)
        public bool ExtraTime;            // Byte 19 bit 1 (Prórroga / Tiempo Extra de 30 min)
        public bool PenaltyShootout;      // Byte 19 bit 3 (Definición por Penales)
        public bool ThirdPlace;           // Byte 19 bit 4 (Partido por el 3er Puesto)
        public bool SingleLeg;            // Byte 19 bit 5 (1 = Partido Único en eliminatorias / 0 = Ida y Vuelta)
        public bool GroupLabelsNumbered;  // Byte 19 bit 6 (1 = Números Grupo 1..12 / 0 = Letras Grupo A..L)

        public string NameLatamSpanish; // 115 bytes UTF-8 (offset 250)
        public string NameEnglish;      // 115 bytes UTF-8 (offset 480)
        public string NameSpanish;      // 115 bytes UTF-8 (offset 825)
        public string Name;             // 32 bytes ASCII (offset 1055)

        public byte[] RawBytes;

        public static RegulationRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var r = new RegulationRecord();
            r.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, r.RawBytes, 0, RecordSize);
            r.IsModified = false;
            r.IsNew = false;

            r.IdDivision2 = BitConverter.ToUInt16(buffer, offset + 0);
            r.Id = BitConverter.ToUInt16(buffer, offset + 2);
            r.IdDivision1 = BitConverter.ToUInt16(buffer, offset + 4);
            r.Value2020_1 = BitConverter.ToInt16(buffer, offset + 6);
            r.IdCompetition = buffer[offset + 8];
            r.CompetitionType = buffer[offset + 9];
            r.Group = buffer[offset + 10];

            r.TotalTeams = (byte)(buffer[offset + 11] & 0x3F);

            ushort w12 = BitConverter.ToUInt16(buffer, offset + 12);
            r.RegisteredPlayers = (byte)(w12 & 0x3F);
            r.Changes = (byte)((w12 >> 6) & 0x3F);

            r.NumClassifiedTeams = (ushort)((BitConverter.ToUInt16(buffer, offset + 14) >> 2) & 0x3F);
            r.FinalChampionship = (buffer[offset + 15] & (1 << 6)) != 0;
            r.Championship = (buffer[offset + 15] & (1 << 7)) != 0;

            r.NumGroups = (byte)(buffer[offset + 16] & 0x1F);
            r.LeagueClassification = (byte)((buffer[offset + 16] >> 5) & 0x07);

            r.CupFormat = (byte)((buffer[offset + 17] >> 1) & 0x07);
            r.LeagueEachTeamMatches = (byte)((buffer[offset + 17] >> 4) & 0x07);
            r.IsDivision1 = (buffer[offset + 17] & (1 << 7)) != 0;

            r.IsDivision2 = (buffer[offset + 18] & (1 << 0)) != 0;
            r.SanctionType = (byte)((buffer[offset + 18] >> 5) & 0x07);

            r.AwayGoals = (buffer[offset + 19] & (1 << 0)) != 0;
            r.ExtraTime = (buffer[offset + 19] & (1 << 1)) != 0;
            r.PenaltyShootout = (buffer[offset + 19] & (1 << 3)) != 0;
            r.ThirdPlace = (buffer[offset + 19] & (1 << 4)) != 0;
            r.SingleLeg = (buffer[offset + 19] & (1 << 5)) != 0;
            r.GroupLabelsNumbered = (buffer[offset + 19] & (1 << 6)) != 0;

            r.NameLatamSpanish = BinaryHelper.ReadNullTerminatedString(buffer, offset + 250, 115, Encoding.UTF8);
            r.NameEnglish = BinaryHelper.ReadNullTerminatedString(buffer, offset + 480, 115, Encoding.UTF8);
            r.NameSpanish = BinaryHelper.ReadNullTerminatedString(buffer, offset + 825, 115, Encoding.UTF8);
            r.Name = BinaryHelper.ReadNullTerminatedString(buffer, offset + 1055, 32, Encoding.ASCII);

            return r;
        }

        public byte[] ToBytes()
        {
            byte[] buf = (RawBytes != null && RawBytes.Length >= RecordSize) ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            if (TotalTeams > 63)
                throw new ArgumentOutOfRangeException("TotalTeams", "TotalTeams=" + TotalTeams + " no cabe en 6 bits (maximo 63): antes se truncaba en silencio.");
            if (RegisteredPlayers > 63)
                throw new ArgumentOutOfRangeException("RegisteredPlayers", "RegisteredPlayers=" + RegisteredPlayers + " no cabe en 6 bits (maximo 63): antes se truncaba en silencio.");
            if (Changes > 63)
                throw new ArgumentOutOfRangeException("Changes", "Changes=" + Changes + " no cabe en 6 bits (maximo 63): antes se truncaba en silencio.");
            if (NumClassifiedTeams > 63)
                throw new ArgumentOutOfRangeException("NumClassifiedTeams", "NumClassifiedTeams=" + NumClassifiedTeams + " no cabe en 6 bits (maximo 63): antes se truncaba en silencio.");

            BitConverter.GetBytes(IdDivision2).CopyTo(buf, 0);
            BitConverter.GetBytes(Id).CopyTo(buf, 2);
            BitConverter.GetBytes(IdDivision1).CopyTo(buf, 4);
            BitConverter.GetBytes(Value2020_1).CopyTo(buf, 6);
            buf[8] = IdCompetition;
            buf[9] = CompetitionType;
            buf[10] = Group;

            // TotalTeams en byte 11 (bits 0..5)
            buf[11] = (byte)((buf[11] & 0xC0) | (TotalTeams & 0x3F));

            // Substitutions / Changes en byte 12-13 PRESERVANDO BITS 12..15 (high nybble de byte 13)
            ushort existingW12 = BitConverter.ToUInt16(buf, 12);
            ushort highBits = (ushort)(existingW12 & 0xF000);
            // Si es un registro nuevo y es tipo Liga (CompetitionType == 4), asignar por defecto 0x1000 (byte 13 = 0x10)
            if (IsNew && highBits == 0 && CompetitionType == 4)
                highBits = 0x1000;

            ushort newW12 = (ushort)(highBits | (RegisteredPlayers & 0x3F) | ((Changes & 0x3F) << 6));
            BitConverter.GetBytes(newW12).CopyTo(buf, 12);

            // Bytes 14-15: Preservar bits 0..1 de byte 14 y bits 0..5 de byte 15
            // NumClassifiedTeams (bits 2..7 de byte 14), FinalChampionship (bit 14), Championship (bit 15)
            ushort w14 = BitConverter.ToUInt16(buf, 14);
            w14 = (ushort)((w14 & 0x3F03) | ((NumClassifiedTeams & 0x3F) << 2));
            if (FinalChampionship) w14 |= (1 << 14); else w14 &= unchecked((ushort)~(1 << 14));
            if (Championship) w14 |= (1 << 15); else w14 &= unchecked((ushort)~(1 << 15));
            BitConverter.GetBytes(w14).CopyTo(buf, 14);

            // Byte 16: NumGroups (bits 0..4) y LeagueClassification (bits 5..7)
            buf[16] = (byte)((NumGroups & 0x1F) | ((LeagueClassification & 0x07) << 5));

            // Byte 17: Bit 0 preservado, CupFormat (bits 1..3), LeagueEachTeamMatches (bits 4..6), IsDivision1 (bit 7)
            byte b17 = (byte)(buf[17] & 0x01);
            b17 |= (byte)((CupFormat & 0x07) << 1);
            b17 |= (byte)((LeagueEachTeamMatches & 0x07) << 4);
            if (IsDivision1)
            {
                b17 |= (1 << 7);
            }
            else if (IsDivision2)
            {
                // En 2das divisiones de PES 2021, cada equipo juega ida y vuelta por defecto (bits 4..6 = 2)
                if (LeagueEachTeamMatches == 0)
                    b17 |= (2 << 4);
            }
            buf[17] = b17;

            // Byte 18: Division2 (bit 0) y SanctionType (bits 5..7), bits 1..4 preservados
            buf[18] = (byte)((buf[18] & 0x1E) | ((SanctionType & 0x07) << 5) | (IsDivision2 ? 1 : 0));

            // Byte 19: Gol Visitante (bit 0), Prórroga (bit 1), bit 2 preservado, Penales (bit 3), 3er Puesto (bit 4), Partido Único (bit 5), Grupos Numéricos (bit 6), bit 7 preservado
            byte b19 = (byte)(buf[19] & 0x84);
            if (AwayGoals) b19 |= (1 << 0);
            if (ExtraTime) b19 |= (1 << 1);
            if (PenaltyShootout) b19 |= (1 << 3);
            if (ThirdPlace) b19 |= (1 << 4);
            if (SingleLeg) b19 |= (1 << 5);
            if (GroupLabelsNumbered) b19 |= (1 << 6);
            buf[19] = b19;

            // Nombres en CompetitionRegulation.bin:
            // Estructura forense de 20 ranuras de idioma (115 bytes UTF-8 cada una):
            // Ranuras 0..8: offsets 20, 135, 250, 365, 480, 595, 710, 825, 940
            // Ranuras 9..19: offsets 1087, 1202, 1317, 1432, 1547, 1662, 1777, 1892, 2007, 2122, 2237
            // En offset 1055: 32 bytes ASCII (Nombre de base de datos)
            int[] allLangOffsets = new int[]
            {
                20, 135, 250, 365, 480, 595, 710, 825, 940,
                1087, 1202, 1317, 1432, 1547, 1662, 1777, 1892, 2007, 2122, 2237
            };

            string primaryName = !string.IsNullOrEmpty(NameSpanish) ? NameSpanish :
                                 (!string.IsNullOrEmpty(NameLatamSpanish) ? NameLatamSpanish : NameEnglish);

            // Alto 52: Solo sobrescribir todas las 20 ranuras de idioma para registros NUEVOS (IsNew).
            // Para competiciones existentes modificadas (IsModified), se preservan intactas las traducciones
            // nativas de Konami en los otros 17 idiomas (japonés, alemán, italiano, francés, etc.).
            if (IsNew && !string.IsNullOrEmpty(primaryName))
            {
                foreach (int off in allLangOffsets)
                {
                    BinaryHelper.WriteNullTerminatedString(buf, off, 115, primaryName, Encoding.UTF8);
                }
            }

            // Ranuras específicas según propiedades editadas
            if (!string.IsNullOrEmpty(NameLatamSpanish))
                BinaryHelper.WriteNullTerminatedString(buf, 250, 115, NameLatamSpanish, Encoding.UTF8);
            if (!string.IsNullOrEmpty(NameEnglish))
            {
                BinaryHelper.WriteNullTerminatedString(buf, 480, 115, NameEnglish, Encoding.UTF8);
                BinaryHelper.WriteNullTerminatedString(buf, 2237, 115, NameEnglish, Encoding.UTF8); // Slot 19 = US English
            }
            if (!string.IsNullOrEmpty(NameSpanish))
                BinaryHelper.WriteNullTerminatedString(buf, 825, 115, NameSpanish, Encoding.UTF8);

            if (!string.IsNullOrEmpty(Name))
                BinaryHelper.WriteNullTerminatedString(buf, 1055, 32, Name, Encoding.ASCII);

            return buf;
        }
    }

    public class EntryRecord
    {
        public const int RecordSize = 12;

        public bool IsModified { get; set; }
        public bool IsNew { get; set; }

        public uint TeamId;
        public ushort Id;
        public short Value1;
        public byte CompetitionId;
        public byte Position;
        public byte Value2;
        public byte Padding;

        public byte[] RawBytes;

        public static EntryRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var e = new EntryRecord();
            e.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, e.RawBytes, 0, RecordSize);
            e.IsModified = false;
            e.IsNew = false;

            e.TeamId = BitConverter.ToUInt32(buffer, offset + 0);
            e.Id = BitConverter.ToUInt16(buffer, offset + 4);
            e.Value1 = BitConverter.ToInt16(buffer, offset + 6);
            e.CompetitionId = buffer[offset + 8];
            e.Position = buffer[offset + 9];
            e.Value2 = buffer[offset + 10];
            e.Padding = buffer[offset + 11];

            return e;
        }

        public byte[] ToBytes()
        {
            byte[] buf = (RawBytes != null && RawBytes.Length >= RecordSize) ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            BitConverter.GetBytes(TeamId).CopyTo(buf, 0);
            BitConverter.GetBytes(Id).CopyTo(buf, 4);
            BitConverter.GetBytes(Value1).CopyTo(buf, 6);
            buf[8] = CompetitionId;
            buf[9] = Position;
            buf[10] = Value2;
            buf[11] = Padding;
            return buf;
        }
    }

    public class TeamRecord
    {
        public const int RecordSize = 1532;

        public bool IsModified { get; set; }
        public bool IsNew { get; set; }

        public uint CoachId;
        public uint Id;
        public ushort StadiumId;
        public ushort CountryId;
        public ushort CallnameId;
        public bool IsNational;
        public uint Rival1;
        public uint Rival2;
        public uint Rival3;
        public string NameSpanish;
        public string NameEnglish;
        public string NameDatabase;
        public string ShortName;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NameSpanish)) return NameSpanish;
                if (!string.IsNullOrWhiteSpace(NameEnglish)) return NameEnglish;
                if (!string.IsNullOrWhiteSpace(NameDatabase)) return NameDatabase;
                if (!string.IsNullOrWhiteSpace(ShortName)) return ShortName;
                return "Equipo " + Id;
            }
        }

        // M145: el bloque BANC solo se escribe si el registro original lo traia
        // (_hasBancBlock) o si el usuario fijo colores/banderas explicitamente
        // (MainForm). Sin esto, ToBytes inventaba el bloque con colores
        // pseudoaleatorios derivados del Id. Las propiedades marcan el flag al
        // asignar; FromBytes asigna los campos internos sin marcarlo.
        private bool _hasBancBlock;
        private bool _identityExplicit;
        private Color _color1;
        private Color _color2;
        private string _banner1;
        private string _banner2;
        private string _banner3;
        private string _banner4;
        public Color Color1 { get { return _color1; } set { _color1 = value; _identityExplicit = true; } }
        public Color Color2 { get { return _color2; } set { _color2 = value; _identityExplicit = true; } }
        public string Banner1 { get { return _banner1; } set { _banner1 = value; _identityExplicit = true; } }
        public string Banner2 { get { return _banner2; } set { _banner2 = value; _identityExplicit = true; } }
        public string Banner3 { get { return _banner3; } set { _banner3 = value; _identityExplicit = true; } }
        public string Banner4 { get { return _banner4; } set { _banner4 = value; _identityExplicit = true; } }

        public byte[] RawBytes;

        public static TeamRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var t = new TeamRecord();
            t.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, t.RawBytes, 0, RecordSize);
            t.IsModified = false;
            t.IsNew = false;

            t.CoachId = BitConverter.ToUInt32(buffer, offset + 0);
            t.Id = BitConverter.ToUInt32(buffer, offset + 8);
            t.Rival1 = BitConverter.ToUInt32(buffer, offset + 28);
            t.Rival2 = BitConverter.ToUInt32(buffer, offset + 40);
            t.Rival3 = BitConverter.ToUInt32(buffer, offset + 48);
            t.StadiumId = BitConverter.ToUInt16(buffer, offset + 60);

            t.CallnameId = BitConverter.ToUInt16(buffer, offset + 66);

            ushort countryWord = BitConverter.ToUInt16(buffer, offset + 70);
            t.CountryId = (ushort)((countryWord >> 2) & 0x1FF);

            t.IsNational = (buffer[offset + 83] & (1 << 7)) != 0;

            t.NameSpanish = BinaryHelper.ReadNullTerminatedString(buffer, offset + 158, 70, Encoding.UTF8);
            t.NameEnglish = BinaryHelper.ReadNullTerminatedString(buffer, offset + 368, 70, Encoding.UTF8);
            t.NameDatabase = BinaryHelper.ReadNullTerminatedString(buffer, offset + 788, 24, Encoding.ASCII);
            t.ShortName = BinaryHelper.ReadNullTerminatedString(buffer, offset + 882, 10, Encoding.ASCII);

            // Lectura forense de Colores de Radar y Pancartas/Banderas (Opción A)
            if (buffer.Length >= offset + 0x5BC &&
                buffer[offset + 0x56C] == 0x42 && buffer[offset + 0x56D] == 0x41 &&
                buffer[offset + 0x56E] == 0x4E && buffer[offset + 0x56F] == 0x43) // "BANC"
            {
                t._hasBancBlock = true;
                t._color1 = Color.FromArgb(buffer[offset + 0x570], buffer[offset + 0x571], buffer[offset + 0x572]);
                t._color2 = Color.FromArgb(buffer[offset + 0x574], buffer[offset + 0x575], buffer[offset + 0x576]);
                t._banner1 = BinaryHelper.ReadNullTerminatedString(buffer, offset + 0x578, 16, Encoding.ASCII);
                t._banner2 = BinaryHelper.ReadNullTerminatedString(buffer, offset + 0x588, 16, Encoding.ASCII);
                t._banner3 = BinaryHelper.ReadNullTerminatedString(buffer, offset + 0x598, 16, Encoding.ASCII);
                t._banner4 = BinaryHelper.ReadNullTerminatedString(buffer, offset + 0x5A8, 16, Encoding.ASCII);
            }
            else
            {
                // Paleta de radar representativa por defecto (solo para mostrar: no
                // debe persistirse como bloque BANC si el vanilla no lo traia).
                t._hasBancBlock = false;
                int r1 = (int)((t.Id * 47) % 200 + 30);
                int g1 = (int)((t.Id * 89) % 200 + 30);
                int b1 = (int)((t.Id * 131) % 200 + 30);
                t._color1 = Color.FromArgb(r1, g1, b1);
                t._color2 = Color.FromArgb((r1 + 100) % 255, (g1 + 80) % 255, (b1 + 120) % 255);
                t._banner1 = "";
                t._banner2 = "";
                t._banner3 = "";
                t._banner4 = "";
            }

            return t;
        }

        public byte[] ToBytes()
        {
            byte[] buf = (byte[])RawBytes.Clone();
            BitConverter.GetBytes(CoachId).CopyTo(buf, 0);
            BitConverter.GetBytes(Id).CopyTo(buf, 8);
            BitConverter.GetBytes(Rival1).CopyTo(buf, 28);
            BitConverter.GetBytes(Rival2).CopyTo(buf, 40);
            BitConverter.GetBytes(Rival3).CopyTo(buf, 48);
            BitConverter.GetBytes(StadiumId).CopyTo(buf, 60);

            BitConverter.GetBytes(CallnameId).CopyTo(buf, 66);

            ushort countryWord = BitConverter.ToUInt16(buf, 70);
            countryWord = (ushort)((countryWord & ~(0x1FF << 2)) | ((CountryId & 0x1FF) << 2));
            BitConverter.GetBytes(countryWord).CopyTo(buf, 70);

            if (IsNational) buf[83] |= (1 << 7);
            else buf[83] &= unchecked((byte)~(1 << 7));

            BinaryHelper.WriteNullTerminatedString(buf, 158, 70, NameSpanish, Encoding.UTF8);
            BinaryHelper.WriteNullTerminatedString(buf, 368, 70, NameEnglish, Encoding.UTF8);
            BinaryHelper.WriteNullTerminatedString(buf, 788, 24, NameDatabase, Encoding.ASCII);
            BinaryHelper.WriteNullTerminatedString(buf, 882, 10, ShortName, Encoding.ASCII);

            // Guardado de Colores de Radar y Pancartas/Banderas: solo si el bloque
            // existia en el registro original o el usuario fijo la identidad (M145).
            if (buf.Length >= 0x5BC && (_hasBancBlock || _identityExplicit))
            {
                buf[0x56C] = 0x42; buf[0x56D] = 0x41; buf[0x56E] = 0x4E; buf[0x56F] = 0x43; // Tag "BANC"
                buf[0x570] = Color1.R; buf[0x571] = Color1.G; buf[0x572] = Color1.B; buf[0x573] = 0xFF;
                buf[0x574] = Color2.R; buf[0x575] = Color2.G; buf[0x576] = Color2.B; buf[0x577] = 0xFF;
                BinaryHelper.WriteNullTerminatedString(buf, 0x578, 16, Banner1 ?? "", Encoding.ASCII);
                BinaryHelper.WriteNullTerminatedString(buf, 0x588, 16, Banner2 ?? "", Encoding.ASCII);
                BinaryHelper.WriteNullTerminatedString(buf, 0x598, 16, Banner3 ?? "", Encoding.ASCII);
                BinaryHelper.WriteNullTerminatedString(buf, 0x5A8, 16, Banner4 ?? "", Encoding.ASCII);
            }

            return buf;
        }

        public TeamRecord CloneWithNewId(uint newId, string newName, string newShortName)
        {
            byte[] clonedBytes = ToBytes();
            var t = TeamRecord.FromBytes(clonedBytes, 0);
            t.Id = newId;
            t.IsNew = true;
            t.NameSpanish = newName;
            t.NameEnglish = newName;
            t.NameDatabase = ("TEAM_" + newId).PadRight(24, '\0').Substring(0, Math.Min(23, ("TEAM_" + newId).Length));
            t.ShortName = !string.IsNullOrEmpty(newShortName) ? newShortName : "CLO";
            t.RawBytes = t.ToBytes();
            return t;
        }

        public override string ToString()
        {
            string displayName = !string.IsNullOrEmpty(NameSpanish) ? NameSpanish : NameEnglish;
            return string.Format("[{0}] {1} ({2})", Id, displayName, ShortName);
        }
    }

    public class CountryRecord
    {
        public const int RecordSize = 1420;

        public ushort Id;
        public byte Continent;
        public string NameSpanish;
        public string NameEnglish;
        public byte[] RawBytes;

        public static CountryRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var c = new CountryRecord();
            c.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, c.RawBytes, 0, RecordSize);
            uint dw0 = BitConverter.ToUInt32(buffer, offset);
            c.Id = (ushort)((dw0 >> 10) & 0x1FF);
            c.Continent = (byte)((dw0 >> 6) & 0x0F);
            c.NameSpanish = BinaryHelper.ReadNullTerminatedString(buffer, offset + 148, 70, Encoding.UTF8);
            c.NameEnglish = BinaryHelper.ReadNullTerminatedString(buffer, offset + 288, 70, Encoding.UTF8);
            if (string.IsNullOrEmpty(c.NameSpanish)) c.NameSpanish = c.NameEnglish;
            return c;
        }

        // M81: sin ToBytes, escribir Country.bin desde DatabaseManager destruiria
        // los campos no modelados. Se clona RawBytes y solo se reescriben Id,
        // Continent y los dos nombres; el resto se preserva tal cual.
        // Nota: si el espanol venia vacio, FromBytes lo cubre con el ingles y
        // ToBytes persiste ese valor (comportamiento del modelo, no del formato).
        public byte[] ToBytes()
        {
            byte[] buf = (RawBytes != null && RawBytes.Length >= RecordSize) ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            // dw0: Id en bits 10..18, Continent en bits 6..9; el resto se preserva.
            uint dw0 = BitConverter.ToUInt32(buf, 0);
            dw0 = (dw0 & ~((0x1FFu << 10) | (0x0Fu << 6))) | (((uint)(Id & 0x1FF) << 10) | ((uint)(Continent & 0x0F) << 6));
            BitConverter.GetBytes(dw0).CopyTo(buf, 0);
            BinaryHelper.WriteNullTerminatedString(buf, 148, 70, NameSpanish, Encoding.UTF8);
            BinaryHelper.WriteNullTerminatedString(buf, 288, 70, NameEnglish, Encoding.UTF8);
            return buf;
        }

        public override string ToString()
        {
            string name = !string.IsNullOrEmpty(NameSpanish) ? NameSpanish : NameEnglish;
            return string.Format("[{0}] {1}", Id, name);
        }
    }

    public class CoachRecord
    {
        public const int RecordSize = 100;

        public uint Id;
        public ushort CountryId;
        public string Name;
        public string NameJapanese;
        public byte[] RawBytes;

        public static CoachRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var c = new CoachRecord();
            c.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, c.RawBytes, 0, RecordSize);
            c.Id = BitConverter.ToUInt32(buffer, offset + 0);
            uint dw4 = BitConverter.ToUInt32(buffer, offset + 4);
            c.CountryId = (ushort)(dw4 & 0x1FF);
            c.NameJapanese = BinaryHelper.ReadNullTerminatedString(buffer, offset + 8, 46, Encoding.UTF8);
            c.Name = BinaryHelper.ReadNullTerminatedString(buffer, offset + 54, 46, Encoding.UTF8);
            if (string.IsNullOrEmpty(c.Name)) c.Name = c.NameJapanese;
            return c;
        }

        public byte[] ToBytes()
        {
            byte[] buf = RawBytes != null ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            Array.Copy(BitConverter.GetBytes(Id), 0, buf, 0, 4);
            uint dw4 = BitConverter.ToUInt32(buf, 4);
            dw4 = (dw4 & ~0x1FFu) | (CountryId & 0x1FFu);
            Array.Copy(BitConverter.GetBytes(dw4), 0, buf, 4, 4);
            BinaryHelper.WriteNullTerminatedString(buf, 8, 46, NameJapanese ?? Name ?? "", Encoding.UTF8);
            BinaryHelper.WriteNullTerminatedString(buf, 54, 46, Name ?? "", Encoding.UTF8);
            return buf;
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", Id, Name);
        }
    }

    public class StadiumRecord
    {
        public const int RecordSize = 392;

        public ushort Id;
        public string Name;
        public string NameJapanese;
        public byte[] RawBytes;

        public static StadiumRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var s = new StadiumRecord();
            s.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, s.RawBytes, 0, RecordSize);
            s.Id = BitConverter.ToUInt16(buffer, offset + 4);
            s.NameJapanese = BinaryHelper.ReadNullTerminatedString(buffer, offset + 8, 62, Encoding.UTF8);
            s.Name = BinaryHelper.ReadNullTerminatedString(buffer, offset + 189, 70, Encoding.UTF8);
            if (string.IsNullOrEmpty(s.Name)) s.Name = s.NameJapanese;
            return s;
        }

        public byte[] ToBytes()
        {
            byte[] buf = RawBytes != null ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            Array.Copy(BitConverter.GetBytes(Id), 0, buf, 4, 2);
            BinaryHelper.WriteNullTerminatedString(buf, 8, 62, NameJapanese ?? Name ?? "", Encoding.UTF8);
            BinaryHelper.WriteNullTerminatedString(buf, 189, 70, Name ?? "", Encoding.UTF8);
            return buf;
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", Id, Name);
        }
    }

    public class DerbyRecord
    {
        public const int RecordSize = 8;

        public uint TeamId1;
        public uint TeamId2;

        public static DerbyRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var d = new DerbyRecord();
            d.TeamId1 = BitConverter.ToUInt32(buffer, offset);
            d.TeamId2 = BitConverter.ToUInt32(buffer, offset + 4);
            return d;
        }

        public byte[] ToBytes()
        {
            byte[] buf = new byte[RecordSize];
            Array.Copy(BitConverter.GetBytes(TeamId1), 0, buf, 0, 4);
            Array.Copy(BitConverter.GetBytes(TeamId2), 0, buf, 4, 4);
            return buf;
        }

        public override string ToString()
        {
            return string.Format("Derby: {0} vs {1}", TeamId1, TeamId2);
        }
    }

    public class PlayerRecord
    {
        public const int RecordSize = 312;

        public uint ClubId;
        public uint Id;

        public uint CallnameId;

        public string Name;
        public string ShirtName;
        public string NameJapanese;
        public byte[] RawBytes;

        public int PositionIndex;
        public string PositionName;
        public int Overall;
        public int Age;

        public static readonly string[] PositionNames = new string[] {
            "GK", "CB", "LB", "RB", "DMF", "CMF", "LMF", "RMF", "AMF", "LWF", "RWF", "SS", "CF"
        };

        public static readonly string[] PositionNamesEs = new string[] {
            "PT", "CT", "LI", "LD", "MCD", "MC", "II", "ID", "MP", "EI", "ED", "SD", "DC"
        };

        public string PositionNameEs
        {
            get
            {
                int idx = PositionIndex;
                if (idx >= 0 && idx < PositionNamesEs.Length) return PositionNamesEs[idx];
                return "DC";
            }
        }

        public static int GetPositionIndexByName(string posName)
        {
            if (string.IsNullOrEmpty(posName)) return 12; // CF / DC
            for (int i = 0; i < PositionNames.Length; i++)
            {
                if (string.Equals(PositionNames[i], posName, StringComparison.OrdinalIgnoreCase)) return i;
            }
            for (int i = 0; i < PositionNamesEs.Length; i++)
            {
                if (string.Equals(PositionNamesEs[i], posName, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return 12;
        }

        public static string NormalizePositionName(string posName)
        {
            int idx = GetPositionIndexByName(posName);
            return PositionNames[idx];
        }

        private static int ReadAbility(byte[] data, int offset, int shift, int bits, int add)
        {
            int window = BitConverter.ToUInt16(data, offset);
            return ((window >> shift) & ((1 << bits) - 1)) + add;
        }

        public int ComputeOverall()
        {
            if (RawBytes == null || RawBytes.Length < RecordSize) return 70;
            try
            {
                int pos = PositionIndex;
                if (pos < 0 || pos >= PositionNames.Length) pos = 12;

                if (pos == 0) // GK
                {
                    int gkAw = ReadAbility(RawBytes, 0x28, 6, 6, 40);
                    int gkCatch = ReadAbility(RawBytes, 0x2D, 4, 6, 40);
                    int gkClear = ReadAbility(RawBytes, 0x21, 5, 6, 40);
                    int gkReflex = ReadAbility(RawBytes, 0x28, 0, 6, 40);
                    int gkReach = ReadAbility(RawBytes, 0x25, 4, 6, 40);
                    int jump = ReadAbility(RawBytes, 0x24, 6, 6, 40);
                    int phys = ReadAbility(RawBytes, 0x30, 6, 6, 40);
                    double score = (gkAw * 0.25) + (gkReflex * 0.25) + (gkReach * 0.20) + (gkCatch * 0.15) + (gkClear * 0.05) + (jump * 0.05) + (phys * 0.05);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 1) // CB
                {
                    int defAw = ReadAbility(RawBytes, 0x22, 3, 6, 40);
                    int ballWin = ReadAbility(RawBytes, 0x27, 0, 6, 40);
                    int phys = ReadAbility(RawBytes, 0x30, 6, 6, 40);
                    int head = ReadAbility(RawBytes, 0x24, 0, 6, 40);
                    int jump = ReadAbility(RawBytes, 0x24, 6, 6, 40);
                    int spd = ReadAbility(RawBytes, 0x26, 2, 6, 40);
                    double score = (defAw * 0.25) + (ballWin * 0.25) + (phys * 0.18) + (head * 0.12) + (jump * 0.10) + (spd * 0.10);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 2 || pos == 3) // LB, RB
                {
                    int spd = ReadAbility(RawBytes, 0x26, 2, 6, 40);
                    int acc = ReadAbility(RawBytes, 0x2B, 0, 6, 40);
                    int sta = ReadAbility(RawBytes, 0x2A, 2, 6, 40);
                    int defAw = ReadAbility(RawBytes, 0x22, 3, 6, 40);
                    int ballWin = ReadAbility(RawBytes, 0x27, 0, 6, 40);
                    int loftPass = ReadAbility(RawBytes, 0x32, 2, 6, 40);
                    double score = (spd * 0.20) + (acc * 0.18) + (sta * 0.15) + (defAw * 0.18) + (ballWin * 0.15) + (loftPass * 0.14);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 4) // DMF
                {
                    int defAw = ReadAbility(RawBytes, 0x22, 3, 6, 40);
                    int ballWin = ReadAbility(RawBytes, 0x27, 0, 6, 40);
                    int lowPass = ReadAbility(RawBytes, 0x20, 7, 6, 40);
                    int sta = ReadAbility(RawBytes, 0x2A, 2, 6, 40);
                    int phys = ReadAbility(RawBytes, 0x30, 6, 6, 40);
                    double score = (defAw * 0.22) + (ballWin * 0.22) + (lowPass * 0.20) + (sta * 0.20) + (phys * 0.16);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 5) // CMF
                {
                    int lowPass = ReadAbility(RawBytes, 0x20, 7, 6, 40);
                    int loftPass = ReadAbility(RawBytes, 0x32, 2, 6, 40);
                    int ballCtrl = ReadAbility(RawBytes, 0x23, 1, 6, 40);
                    int sta = ReadAbility(RawBytes, 0x2A, 2, 6, 40);
                    int drib = ReadAbility(RawBytes, 0x2C, 0, 6, 40);
                    double score = (lowPass * 0.24) + (loftPass * 0.20) + (ballCtrl * 0.20) + (sta * 0.18) + (drib * 0.18);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 6 || pos == 7) // LMF, RMF
                {
                    int spd = ReadAbility(RawBytes, 0x26, 2, 6, 40);
                    int drib = ReadAbility(RawBytes, 0x2C, 0, 6, 40);
                    int ballCtrl = ReadAbility(RawBytes, 0x23, 1, 6, 40);
                    int loftPass = ReadAbility(RawBytes, 0x32, 2, 6, 40);
                    int sta = ReadAbility(RawBytes, 0x2A, 2, 6, 40);
                    double score = (spd * 0.24) + (drib * 0.22) + (ballCtrl * 0.20) + (loftPass * 0.18) + (sta * 0.16);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 8) // AMF
                {
                    int offAw = ReadAbility(RawBytes, 0x2E, 2, 6, 40);
                    int ballCtrl = ReadAbility(RawBytes, 0x23, 1, 6, 40);
                    int drib = ReadAbility(RawBytes, 0x2C, 0, 6, 40);
                    int lowPass = ReadAbility(RawBytes, 0x20, 7, 6, 40);
                    int finish = ReadAbility(RawBytes, 0x31, 4, 6, 40);
                    double score = (offAw * 0.22) + (ballCtrl * 0.22) + (drib * 0.20) + (lowPass * 0.20) + (finish * 0.16);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 9 || pos == 10) // LWF, RWF
                {
                    int spd = ReadAbility(RawBytes, 0x26, 2, 6, 40);
                    int acc = ReadAbility(RawBytes, 0x2B, 0, 6, 40);
                    int drib = ReadAbility(RawBytes, 0x2C, 0, 6, 40);
                    int finish = ReadAbility(RawBytes, 0x31, 4, 6, 40);
                    int ballCtrl = ReadAbility(RawBytes, 0x23, 1, 6, 40);
                    double score = (spd * 0.24) + (acc * 0.22) + (drib * 0.22) + (finish * 0.18) + (ballCtrl * 0.14);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else if (pos == 11) // SS
                {
                    int offAw = ReadAbility(RawBytes, 0x2E, 2, 6, 40);
                    int finish = ReadAbility(RawBytes, 0x31, 4, 6, 40);
                    int drib = ReadAbility(RawBytes, 0x2C, 0, 6, 40);
                    int ballCtrl = ReadAbility(RawBytes, 0x23, 1, 6, 40);
                    int lowPass = ReadAbility(RawBytes, 0x20, 7, 6, 40);
                    double score = (offAw * 0.24) + (finish * 0.24) + (drib * 0.20) + (ballCtrl * 0.18) + (lowPass * 0.14);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
                else // CF
                {
                    int offAw = ReadAbility(RawBytes, 0x2E, 2, 6, 40);
                    int finish = ReadAbility(RawBytes, 0x31, 4, 6, 40);
                    int phys = ReadAbility(RawBytes, 0x30, 6, 6, 40);
                    int head = ReadAbility(RawBytes, 0x24, 0, 6, 40);
                    int spd = ReadAbility(RawBytes, 0x26, 2, 6, 40);
                    double score = (offAw * 0.28) + (finish * 0.28) + (phys * 0.18) + (head * 0.14) + (spd * 0.12);
                    return Math.Max(40, Math.Min(99, (int)Math.Round(score)));
                }
            }
            catch { return 70; }
        }

        public static PlayerRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var p = new PlayerRecord();
            p.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, p.RawBytes, 0, RecordSize);
            p.ClubId = BitConverter.ToUInt32(buffer, offset + 0);
            p.Id = BitConverter.ToUInt32(buffer, offset + 8);

            p.CallnameId = BitConverter.ToUInt32(buffer, offset + 4);
            p.NameJapanese = BinaryHelper.ReadNullTerminatedString(buffer, offset + 129, 61, Encoding.UTF8);
            p.ShirtName = BinaryHelper.ReadNullTerminatedString(buffer, offset + 190, 61, Encoding.UTF8);
            p.Name = BinaryHelper.ReadNullTerminatedString(buffer, offset + 251, 61, Encoding.UTF8);
            if (string.IsNullOrEmpty(p.Name)) p.Name = p.ShirtName ?? p.NameJapanese;

            p.PositionIndex = (buffer[offset + 0x36] >> 2) & 0x0F;
            p.PositionName = (p.PositionIndex >= 0 && p.PositionIndex < PositionNames.Length) ? PositionNames[p.PositionIndex] : "CF";
            p.Age = (buffer[offset + 0x33] & 0x3F) + 15;
            p.Overall = p.ComputeOverall();

            return p;
        }

        public byte[] ToBytes()
        {
            byte[] buf = RawBytes != null ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            Array.Copy(BitConverter.GetBytes(ClubId), 0, buf, 0, 4);
            Array.Copy(BitConverter.GetBytes(Id), 0, buf, 8, 4);

            Array.Copy(BitConverter.GetBytes(CallnameId), 0, buf, 4, 4);
            // Persistir posicion y edad desde los campos (antes solo se clonaba RawBytes:
            // cambiar PositionIndex/Age sin pasar por PlayerBitHelper se perdia al guardar).
            // Overall no se escribe: es calculado (ComputeOverall), no un byte almacenado.
            if (buf.Length > 0x36)
            {
                int posIdx = Math.Max(0, Math.Min(PositionNames.Length - 1, PositionIndex));
                buf[0x36] = (byte)((buf[0x36] & ~(0x0F << 2)) | ((posIdx & 0x0F) << 2));
                int ageVal = Math.Max(15, Math.Min(78, Age));
                buf[0x33] = (byte)((buf[0x33] & ~0x3F) | ((ageVal - 15) & 0x3F));
            }
            BinaryHelper.WriteNullTerminatedString(buf, 129, 61, NameJapanese ?? Name ?? "", Encoding.UTF8);
            BinaryHelper.WriteNullTerminatedString(buf, 190, 61, ShirtName ?? Name ?? "", Encoding.UTF8);
            BinaryHelper.WriteNullTerminatedString(buf, 251, 61, Name ?? "", Encoding.UTF8);
            return buf;
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1} ({2})", Id, Name, ShirtName);
        }
    }

    public class PlayerAssignmentRecord
    {
        public const int RecordSize = 16;

        public uint EntryId;
        public uint PlayerId;
        public uint TeamId;
        public uint Pack;
        public byte[] RawBytes;

        public int ShirtNumber
        {
            get { return (int)(Pack & 0x3FF) + 1; }
            set { Pack = (Pack & ~0x3FFu) | (uint)((Math.Max(1, Math.Min(99, value)) - 1) & 0x3FF); }
        }

        public int SquadOrder
        {
            get { return (int)((Pack >> 10) & 0x3F); }
            set { Pack = (Pack & ~(0x3Fu << 10)) | (uint)((value & 0x3F) << 10); }
        }

        public bool IsRightCorner { get { return (Pack & (1u << 16)) != 0; } set { if (value) Pack |= (1u << 16); else Pack &= ~(1u << 16); } }
        public bool IsShortFK { get { return (Pack & (1u << 17)) != 0; } set { if (value) Pack |= (1u << 17); else Pack &= ~(1u << 17); } }
        public bool IsLeftCorner { get { return (Pack & (1u << 18)) != 0; } set { if (value) Pack |= (1u << 18); else Pack &= ~(1u << 18); } }
        public bool IsLongFK { get { return (Pack & (1u << 19)) != 0; } set { if (value) Pack |= (1u << 19); else Pack &= ~(1u << 19); } }
        public bool IsPenalty { get { return (Pack & (1u << 20)) != 0; } set { if (value) Pack |= (1u << 20); else Pack &= ~(1u << 20); } }
        public bool IsCaptain { get { return (Pack & (1u << 21)) != 0; } set { if (value) Pack |= (1u << 21); else Pack &= ~(1u << 21); } }

        public static PlayerAssignmentRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var a = new PlayerAssignmentRecord();
            a.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, a.RawBytes, 0, RecordSize);
            a.EntryId = BitConverter.ToUInt32(buffer, offset + 0);
            a.PlayerId = BitConverter.ToUInt32(buffer, offset + 4);
            a.TeamId = BitConverter.ToUInt32(buffer, offset + 8);
            a.Pack = BitConverter.ToUInt32(buffer, offset + 12);
            return a;
        }

        public byte[] ToBytes()
        {
            byte[] buf = RawBytes != null ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            Array.Copy(BitConverter.GetBytes(EntryId), 0, buf, 0, 4);
            Array.Copy(BitConverter.GetBytes(PlayerId), 0, buf, 4, 4);
            Array.Copy(BitConverter.GetBytes(TeamId), 0, buf, 8, 4);
            Array.Copy(BitConverter.GetBytes(Pack), 0, buf, 12, 4);
            return buf;
        }

        public override string ToString()
        {
            return string.Format("Team {0} -> Player {1} (#{2}, pos {3})", TeamId, PlayerId, ShirtNumber, SquadOrder);
        }
    }

    public class TacticsRecord
    {
        public const int RecordSize = 12;

        public uint Key;
        public uint ClubId;
        public uint Strategy;
        public byte[] RawBytes;

        public static TacticsRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var t = new TacticsRecord();
            t.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, t.RawBytes, 0, RecordSize);
            t.Key = BitConverter.ToUInt32(buffer, offset + 0);
            t.ClubId = BitConverter.ToUInt32(buffer, offset + 4);
            t.Strategy = BitConverter.ToUInt32(buffer, offset + 8);
            return t;
        }

        public byte[] ToBytes()
        {
            byte[] buf = RawBytes != null ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            Array.Copy(BitConverter.GetBytes(Key), 0, buf, 0, 4);
            Array.Copy(BitConverter.GetBytes(ClubId), 0, buf, 4, 4);
            Array.Copy(BitConverter.GetBytes(Strategy), 0, buf, 8, 4);
            return buf;
        }

        public override string ToString()
        {
            return string.Format("Tactics Key {0} -> Club {1} (Strat: 0x{2:X8})", Key, ClubId, Strategy);
        }
    }

    public class TacticsFormationRecord
    {
        public const int RecordSize = 12;

        public uint TacticsKey;
        public uint Role;
        public byte Depth; // Y: 3..46
        public byte Width; // X: 0..100
        public byte Place; // (variant << 4) | squad_order
        public byte Pad;
        public byte[] RawBytes;

        public int Variant
        {
            get { return (Place >> 4) & 0x0F; }
            set { Place = (byte)((Place & 0x0F) | ((value & 0x0F) << 4)); }
        }

        public int SquadIndex
        {
            get { return Place & 0x0F; }
            set { Place = (byte)((Place & 0xF0) | (value & 0x0F)); }
        }

        public static TacticsFormationRecord FromBytes(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || (long)offset + RecordSize > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", "FromBytes fuera del buffer: se necesitan " + RecordSize + " bytes en offset=" + offset + ".");
            var f = new TacticsFormationRecord();
            f.RawBytes = new byte[RecordSize];
            Buffer.BlockCopy(buffer, offset, f.RawBytes, 0, RecordSize);
            f.TacticsKey = BitConverter.ToUInt32(buffer, offset + 0);
            f.Role = BitConverter.ToUInt32(buffer, offset + 4);
            f.Depth = buffer[offset + 8];
            f.Width = buffer[offset + 9];
            f.Place = buffer[offset + 10];
            f.Pad = buffer[offset + 11];
            return f;
        }

        public byte[] ToBytes()
        {
            byte[] buf = RawBytes != null ? (byte[])RawBytes.Clone() : new byte[RecordSize];
            Array.Copy(BitConverter.GetBytes(TacticsKey), 0, buf, 0, 4);
            Array.Copy(BitConverter.GetBytes(Role), 0, buf, 4, 4);
            buf[8] = Depth;
            buf[9] = Width;
            buf[10] = Place;
            buf[11] = Pad;
            return buf;
        }

        public override string ToString()
        {
            return string.Format("Key {0} [Var {1}, Pos {2}]: Role {3} @ ({4}, {5})",
                TacticsKey, Variant, SquadIndex, Role, Width, Depth);
        }
    }

    public class FormationPreset
    {
        public string Name { get; set; }
        public int[] Roles { get; set; }
        public byte[] Depths { get; set; }
        public byte[] Widths { get; set; }

        public override string ToString()
        {
            return Name;
        }

        public static string GetRoleName(int role)
        {
            switch (role)
            {
                case 0: return "GK";
                case 1: return "CB";
                case 2: return "LB";
                case 3: return "RB";
                case 4: return "DMF";
                case 5: return "CMF";
                case 6: return "LMF";
                case 7: return "RMF";
                case 8: return "AMF";
                case 9: return "LWF";
                case 10: return "RWF";
                case 11: return "SS";
                case 12: return "CF";
                default: return "JUG";
            }
        }

        public static readonly List<FormationPreset> Presets = new List<FormationPreset>
        {
            // Línea de 4
            new FormationPreset { Name = "4-4-2", Roles = new int[] { 0, 1, 1, 3, 2, 5, 5, 7, 6, 12, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 22, 22, 25, 25, 42, 42 }, Widths = new byte[] { 52, 63, 41, 89, 15, 63, 41, 86, 18, 62, 42 } },
            new FormationPreset { Name = "4-3-3", Roles = new int[] { 0, 1, 1, 3, 2, 4, 5, 5, 10, 9, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 18, 24, 24, 38, 38, 43 }, Widths = new byte[] { 52, 63, 41, 89, 15, 52, 68, 36, 86, 18, 52 } },
            new FormationPreset { Name = "4-2-3-1", Roles = new int[] { 0, 1, 1, 3, 2, 4, 4, 8, 10, 9, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 18, 18, 32, 42, 42, 43 }, Widths = new byte[] { 52, 63, 41, 89, 15, 64, 40, 52, 85, 19, 52 } },
            new FormationPreset { Name = "4-1-2-1-2", Roles = new int[] { 0, 1, 1, 3, 2, 4, 5, 5, 8, 12, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 18, 24, 24, 32, 42, 42 }, Widths = new byte[] { 52, 63, 41, 89, 15, 52, 68, 36, 52, 62, 42 } },
            new FormationPreset { Name = "4-1-4-1", Roles = new int[] { 0, 1, 1, 3, 2, 4, 5, 5, 7, 6, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 18, 26, 26, 28, 28, 43 }, Widths = new byte[] { 52, 63, 41, 89, 15, 52, 64, 40, 86, 18, 52 } },
            new FormationPreset { Name = "4-2-1-3", Roles = new int[] { 0, 1, 1, 3, 2, 4, 4, 8, 10, 9, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 19, 19, 30, 39, 39, 43 }, Widths = new byte[] { 52, 63, 41, 89, 15, 64, 40, 52, 86, 18, 52 } },
            new FormationPreset { Name = "4-2-2-2", Roles = new int[] { 0, 1, 1, 3, 2, 4, 4, 8, 8, 12, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 19, 19, 31, 31, 42, 42 }, Widths = new byte[] { 52, 63, 41, 89, 15, 64, 40, 68, 36, 62, 42 } },
            new FormationPreset { Name = "4-3-1-2", Roles = new int[] { 0, 1, 1, 3, 2, 4, 5, 5, 8, 12, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 17, 22, 22, 31, 42, 42 }, Widths = new byte[] { 52, 63, 41, 89, 15, 52, 70, 34, 52, 62, 42 } },
            new FormationPreset { Name = "4-1-3-2", Roles = new int[] { 0, 1, 1, 3, 2, 4, 7, 6, 8, 12, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 18, 27, 27, 31, 42, 42 }, Widths = new byte[] { 52, 63, 41, 89, 15, 52, 86, 18, 52, 62, 42 } },
            new FormationPreset { Name = "4-3-2-1", Roles = new int[] { 0, 1, 1, 3, 2, 4, 5, 5, 8, 8, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 18, 22, 22, 32, 32, 43 }, Widths = new byte[] { 52, 63, 41, 89, 15, 52, 70, 34, 65, 39, 52 } },
            new FormationPreset { Name = "4-2-4", Roles = new int[] { 0, 1, 1, 3, 2, 5, 5, 10, 9, 12, 12 }, Depths = new byte[] { 3, 10, 10, 12, 12, 22, 22, 39, 39, 43, 43 }, Widths = new byte[] { 52, 63, 41, 89, 15, 63, 41, 86, 18, 62, 42 } },

            // Línea de 3
            new FormationPreset { Name = "3-5-2", Roles = new int[] { 0, 1, 1, 1, 4, 4, 7, 6, 8, 12, 12 }, Depths = new byte[] { 3, 10, 10, 10, 19, 19, 25, 25, 33, 43, 43 }, Widths = new byte[] { 52, 52, 73, 31, 64, 40, 86, 18, 52, 64, 40 } },
            new FormationPreset { Name = "3-4-3", Roles = new int[] { 0, 1, 1, 1, 5, 5, 7, 6, 10, 9, 12 }, Depths = new byte[] { 3, 10, 10, 10, 22, 22, 24, 24, 38, 38, 43 }, Widths = new byte[] { 52, 52, 73, 31, 63, 41, 88, 16, 84, 20, 52 } },
            new FormationPreset { Name = "3-4-1-2", Roles = new int[] { 0, 1, 1, 1, 5, 5, 7, 6, 8, 12, 12 }, Depths = new byte[] { 3, 10, 10, 10, 21, 21, 25, 25, 32, 42, 42 }, Widths = new byte[] { 52, 52, 73, 31, 64, 40, 86, 18, 52, 62, 42 } },
            new FormationPreset { Name = "3-4-2-1", Roles = new int[] { 0, 1, 1, 1, 5, 5, 7, 6, 8, 8, 12 }, Depths = new byte[] { 3, 10, 10, 10, 21, 21, 25, 25, 32, 32, 43 }, Widths = new byte[] { 52, 52, 73, 31, 64, 40, 86, 18, 65, 39, 52 } },
            new FormationPreset { Name = "3-2-4-1", Roles = new int[] { 0, 1, 1, 1, 4, 4, 7, 6, 8, 8, 12 }, Depths = new byte[] { 3, 10, 10, 10, 18, 18, 28, 28, 32, 32, 43 }, Widths = new byte[] { 52, 52, 73, 31, 63, 41, 86, 18, 65, 39, 52 } },
            new FormationPreset { Name = "3-3-3-1", Roles = new int[] { 0, 1, 1, 1, 4, 7, 6, 8, 8, 8, 12 }, Depths = new byte[] { 3, 10, 10, 10, 18, 24, 24, 32, 32, 32, 43 }, Widths = new byte[] { 52, 52, 73, 31, 52, 86, 18, 68, 52, 36, 52 } },

            // Línea de 5
            new FormationPreset { Name = "5-3-2", Roles = new int[] { 0, 1, 1, 1, 3, 2, 4, 5, 5, 12, 12 }, Depths = new byte[] { 3, 10, 10, 10, 15, 15, 20, 24, 24, 42, 42 }, Widths = new byte[] { 52, 52, 73, 31, 89, 15, 52, 70, 34, 62, 42 } },
            new FormationPreset { Name = "5-4-1", Roles = new int[] { 0, 1, 1, 1, 3, 2, 5, 5, 7, 6, 12 }, Depths = new byte[] { 3, 10, 10, 10, 14, 14, 22, 22, 27, 27, 42 }, Widths = new byte[] { 52, 52, 73, 31, 89, 15, 63, 41, 86, 18, 52 } },
            new FormationPreset { Name = "5-2-1-2", Roles = new int[] { 0, 1, 1, 1, 3, 2, 5, 5, 8, 12, 12 }, Depths = new byte[] { 3, 10, 10, 10, 14, 14, 22, 22, 31, 42, 42 }, Widths = new byte[] { 52, 52, 73, 31, 89, 15, 64, 40, 52, 62, 42 } },
            new FormationPreset { Name = "5-2-2-1", Roles = new int[] { 0, 1, 1, 1, 3, 2, 5, 5, 8, 8, 12 }, Depths = new byte[] { 3, 10, 10, 10, 14, 14, 21, 21, 32, 32, 43 }, Widths = new byte[] { 52, 52, 73, 31, 89, 15, 64, 40, 65, 39, 52 } },
            new FormationPreset { Name = "5-2-3", Roles = new int[] { 0, 1, 1, 1, 3, 2, 5, 5, 10, 9, 12 }, Depths = new byte[] { 3, 10, 10, 10, 14, 14, 21, 21, 39, 39, 43 }, Widths = new byte[] { 52, 52, 73, 31, 89, 15, 64, 40, 86, 18, 52 } }
        };
    }
}
