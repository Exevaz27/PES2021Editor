# ESPECIFICACIÓN TÉCNICA FORENSE DE ESTRUCTURA BINARIA (PES 2021 / SP Football Life)
**Firmado y Desarrollado por EXEVAZ27**
**Documento de Ingeniería Inversa Forense - Nivel Bit y Byte**

---

## 1. CABECERA Y PROTOCOLO DE COMPRESIÓN (WESYS / zlib)

Todos los archivos de base de datos (`.bin`) de eFootball PES 2021 y SP Football Life comparten de manera uniforme la cabecera del contenedor **WESYS** de 16 bytes, seguida de un flujo de compresión estándar **zlib (RFC 1950)**.

### Estructura de la Cabecera de 16 bytes:
| Offset (Bytes) | Tamaño | Tipo de Dato | Endianness | Valor / Descripción |
|---|---|---|---|---|
| `0x00 - 0x02` | 3 bytes | `byte[3]` | N/A | `FF 10 81` (Firma mágica de PES/Konami) |
| `0x03 - 0x07` | 5 bytes | `ASCII` | N/A | `WESYS` (`57 45 53 59 53`) |
| `0x08 - 0x0B` | 4 bytes | `uint32` | Little-Endian | Tamaño exacto del flujo zlib comprimido |
| `0x0C - 0x0F` | 4 bytes | `uint32` | Little-Endian | Tamaño exacto de la carga útil descomprimida (Payload) |
| `0x10+` | Variable | Stream | RFC 1950 | Datos comprimidos con zlib (`78 01` / `78 9C` / `78 DA`) |

### Algoritmo de Descompresión:
1. Validar que los bytes en `[0x03..0x07]` correspondan exactamente al string ASCII `"WESYS"`.
2. Leer el `uint32` en `[0x0C..0x0F]` (`uncompressed_size`).
3. Extraer el subarreglo de bytes desde el offset `16` (`0x10`) hasta el final del archivo.
4. Descomprimir el flujo con el descompresor estándar zlib / Deflate con cabecera zlib.
5. Validar que la longitud de los bytes resultantes sea exactamente igual a `uncompressed_size`.

### Algoritmo de Compresión y Empaquetado:
1. Comprimir el payload descomprimido mediante zlib (nivel de compresión estándar 1 ó 6).
2. Crear un buffer de tamaño `comprimido.Length + 16`.
3. Escribir los 8 bytes iniciales: `FF 10 81 57 45 53 59 53`.
4. Escribir en `[0x08..0x0B]` los 4 bytes de `comprimido.Length` (uint32 Little-Endian).
5. Escribir en `[0x0C..0x0F]` los 4 bytes de `payload.Length` (uint32 Little-Endian).
6. Copiar los bytes del flujo comprimido a partir del offset `0x10`.
7. Guardar en disco.

---

## 2. FORMATOS DE REGISTRO INTERNO Y CODIFICACIÓN DE CAMPOS

En el motor de PES, los registros utilizan una técnica de empaquetado donde los campos pueden ser:
1. **Strings ASCII**: Arreglo de bytes ASCII terminados en byte nulo (`\0`).
2. **Strings UTF-8**: Arreglo de bytes UTF-8 para texto internacional / nombres con acentos.
3. **Valores enteros directos**: `uint32` (4 bytes LE), `uint16`/`short` (2 bytes LE), `byte` (1 byte).
4. **Campos de bits (Bitfields)**:
   - Se acceden leyendo una palabra de 16 bits en `offset` en Little-Endian.
   - Se desplazan a la derecha por `bitShift` bits.
   - Se enmascaran con `mask = (1 << bitCount) - 1`.
   - Fórmula de lectura: `(ToUInt16(buffer, offset) >> bitShift) & ((1 << bitCount) - 1)`.
   - Fórmula de escritura:
     ```csharp
     ushort val = ToUInt16(buffer, offset);
     val &= (ushort)(~(((1 << bitCount) - 1) << bitShift));
     val |= (ushort)((nuevoValor & ((1 << bitCount) - 1)) << bitShift);
     WriteUInt16(buffer, val, offset);
     ```
5. **Flags Booleanos**:
   - `(buffer[offset] & (1 << bitIndex)) != 0`.

---

## 3. ESPECIFICACIÓN DETALLADA POR ARCHIVO

### 3.1. `Competition.bin`
- **Tamaño de Registro**: **36 bytes** exactos.
- **Validación en archivo real**: 3,312 bytes / 36 bytes = **92 registros**.

| Campo | Tipo | Offset Byte | Bit Shift / Índice | Longitud / Máscara | Descripción |
|---|---|---|---|---|---|
| `Id` | uint32 / bitfield | `0x05` | `bit 0` | 8 bits (`0xFF`) | ID numérico de la competición |
| `IdParentCompetition` | uint8 | `0x04` | `bit 0` | 8 bits (`0xFF`) | ID de la competición padre (si es 2da div) |
| `Continent` | uint8 | `0x06` | `bit 0` | 4 bits (`0x0F`) | Región / Continente (0=Europa, 1=América, etc.) |
| `Licenced` | bool | `0x06` | `bit 5` | 1 bit | 1 = Licenciada, 0 = No licenciada |
| `Fake` | bool | `0x06` | `bit 6` | 1 bit | 1 = Falsa / Genérica, 0 = Real |
| `Value2` | bool | `0x06` | `bit 4` | 1 bit | Flag auxiliar de visualización |
| `Value3` | bool | `0x06` | `bit 7` | 1 bit | Flag auxiliar del motor |
| `Group` | uint8 | `0x03` | `bit 3` | 5 bits (`0x1F`) | Grupo de torneo |
| `Value1` | int16 | `0x00` | N/A | 2 bytes LE | Identificador interno secundario |
| `Name` | string ASCII | `0x08` | N/A | 28 bytes | Nombre ASCII en mayúsculas (ej: `PREMIER_LEAGUE\0`) |

---

### 3.2. `CompetitionEntry.bin`
- **Tamaño de Registro**: **12 bytes** exactos.
- **Validación en archivo real**: 15,300 bytes / 12 bytes = **1,275 registros**.

| Campo | Tipo | Offset Byte | Bit Shift / Índice | Longitud / Máscara | Descripción |
|---|---|---|---|---|---|
| `IdTeam` | uint32 | `0x00` | N/A | 4 bytes LE | ID del equipo asignado (coincide con Team.bin) |
| `Id` | uint16 | `0x04` | N/A | 2 bytes LE | ID de la entrada única |
| `Value1` | int16 | `0x06` | N/A | 2 bytes LE | Valor complementario de sorteo |
| `IdCompetition` | uint8 / ushort | `0x08` | N/A | 1 byte | ID de la competición a la que ingresa el equipo |
| `Position` | uint8 | `0x09` | N/A | 1 byte | Slot o posición en la grilla del torneo (0..N) |
| `Value2` | uint8 | `0x0A` | N/A | 1 byte | Bandera de orden / siembra en fase previa |
| `Padding` | uint8 | `0x0B` | N/A | 1 byte | Byte reservado de alineación |

---

### 3.3. `CompetitionRegulation.bin`
- **Tamaño de Registro**: **2,352 bytes** exactos.
- **Validación en archivo real**: 505,680 bytes / 2,352 bytes = **215 registros**.

| Campo | Tipo | Offset Byte | Bit Shift / Índice | Longitud / Máscara | Descripción |
|---|---|---|---|---|---|
| `IdDivision2` | uint16 | `0x00` | N/A | 2 bytes LE | ID de competición de la 2da División vinculada |
| `Id` | uint16 | `0x02` | N/A | 2 bytes LE | ID de la regulación de competición |
| `IdDivision1` | uint16 | `0x04` | N/A | 2 bytes LE | ID de competición de la 1ra División vinculada |
| `Value2020_1` | int16 | `0x06` | N/A | 2 bytes LE | Parámetro reglamentario interno |
| `IdCompetition` | uint8 | `0x08` | `bit 0` | 8 bits (`0xFF`) | ID de competición asignada |
| `CompetitionType` | uint8 | `0x09` | N/A | 1 byte | 0 = Liga, 1 = Copa, 2 = Supercopa, etc. |
| `Group` | uint8 | `0x0A` | N/A | 1 byte | Grupo de asignación de confederación |
| `TotalTeams` | uint8 | `0x0B` | `bit 0` | 6 bits (`0x3F`) | Cantidad total de equipos participantes en la liga |
| `Value2020_2` | uint8 | `0x0B` | `bit 6` | 2 bits | Flag reglamentario secundario |
| `RegisteredPlayers` | uint8 | `0x0C` | `bit 0` | 6 bits (`0x3F`) | Límite de jugadores inscriptos en plantilla |
| `Changes` | uint8 | `0x0C` | `bit 6` | 6 bits (en byte 12-13) | Cambios permitidos por partido (sustituciones) |
| `NumClassifiedTeams` | uint8 | `0x0E` | `bit 2` | 6 bits (`0x3F`) | Equipos que clasifican a copas internacionales |
| `FinalChampionship` | bool | `0x0F` | `bit 6` | 1 bit | Si define campeonato con final |
| `Championship` | bool | `0x0F` | `bit 7` | 1 bit | Si tiene playoffs de campeonato |
| `NumGroups` | uint8 | `0x10` | `bit 0` | 5 bits (`0x1F`) | Cantidad de grupos (en fases de grupos) |
| `LeagueClassification` | uint8 | `0x10` | `bit 5` | 4 bits | Sistema de puntuación / desempate |
| `CupFormat` | uint8 | `0x11` | `bit 1` | 3 bits | Formato de eliminatoria (ida, vuelta, neutral) |
| `LeagueEachTeamMatches` | uint8 | `0x11` | `bit 4` | 3 bits | Rondas de partidos (1 rueda, 2 ruedas, etc.) |
| `Division1` | bool | `0x11` | `bit 7` | 1 bit | Flag: Esta liga es Primera División |
| `Division2` | bool | `0x12` | `bit 0` | 1 bit | Flag: Esta liga es Segunda División (ascenso) |
| `SanctionType` | uint8 | `0x12` | `bit 5` | 3 bits | Límite y acumulación de tarjetas amarillas |
| `Knockout` | bool | `0x13` | `bit 3` | 1 bit | Si incluye fase de eliminación directa |
| `3rd` | bool | `0x13` | `bit 4` | 1 bit | Si se disputa partido por 3er puesto |
| `NameKorean` | UTF-8 | `0x14` | N/A | 115 bytes | Nombre traducido al Coreano |
| `NameJapanese` | UTF-8 | `0x87` (`135`) | N/A | 115 bytes | Nombre traducido al Japonés |
| `NameLatamSpanish` | UTF-8 | `0xFA` (`250`) | N/A | 115 bytes | Nombre traducido al Español Latinoamericano |
| `NameEnglish` | UTF-8 | `0x01E0` (`480`) | N/A | 115 bytes | Nombre traducido al Inglés Internacional |
| `NameSpanish` | UTF-8 | `0x0339` (`825`) | N/A | 115 bytes | Nombre traducido al Español de España |
| `NamePortuguese` | UTF-8 | `0x03AC` (`940`) | N/A | 115 bytes | Nombre traducido al Portugués |
| `NameGerman` | UTF-8 | `0x043F` (`1087`) | N/A | 115 bytes | Nombre traducido al Alemán |
| `NameBrazilian` | UTF-8 | `0x04B2` (`1202`) | N/A | 115 bytes | Nombre traducido al Portugués de Brasil |
| `NameDutch` | UTF-8 | `0x0525` (`1317`) | N/A | 115 bytes | Nombre traducido al Holandés |
| `NameItalian` | UTF-8 | `0x06F1` (`1777`) | N/A | 115 bytes | Nombre traducido al Italiano |
| `NameRussian` | UTF-8 | `0x07D7` (`2007`) | N/A | 115 bytes | Nombre traducido al Ruso |
| `NameUSEnglish` | UTF-8 | `0x08BD` (`2237`) | N/A | 115 bytes | Nombre traducido al Inglés Americano |
| `Name` (Base ASCII) | ASCII | `0x041F` (`1055`) | N/A | 32 bytes | Nombre corto de la liga en ASCII |

---

### 3.4. `Team.bin`
- **Tamaño de Registro**: **1,532 bytes** exactos.
- **Validación en archivo real**: 1,063,208 bytes / 1,532 bytes = **694 registros**.

| Campo | Tipo | Offset Byte | Bit Shift / Índice | Longitud / Máscara | Descripción |
|---|---|---|---|---|---|
| `Coach` | uint32 | `0x00` | N/A | 4 bytes LE | ID del Director Técnico (entrenador) |
| `Id` | uint32 | `0x08` | N/A | 4 bytes LE | ID único del club / equipo nacional |
| `Stadium` | uint16 | `0x3C` (`60`) | N/A | 2 bytes LE | ID del estadio asignado |
| `Country` | uint16 | `0x46` (`70`) | `bit 2` | 9 bits (`0x1FF`) | ID del país / nacionalidad del club |
| `National` | bool | `0x53` (`83`) | `bit 7` | 1 bit | 1 = Selección Nacional, 0 = Club |
| `NameSpanish` | UTF-8 | `0x9E` (`158`) | N/A | 70 bytes | Nombre del equipo en Español |
| `NameEnglish` | UTF-8 | `0x0170` (`368`) | N/A | 70 bytes | Nombre del equipo en Inglés |
| `NameDatabase` | ASCII | `0x0314` (`788`) | N/A | 24 bytes | Nombre del equipo en la base de datos interna |
| `ShortNameLicensed`| ASCII | `0x0372` (`882`) | N/A | 10 bytes | Abreviatura oficial (3 letras, ej: `BOC`, `RIV`, `FCB`) |
| `ShortNameFake` | ASCII | `0x0566` (`1382`)| N/A | 10 bytes | Abreviatura para equipos no licenciados |

---

### 3.5. `Player.bin`
- **Tamaño de Registro**: **312 bytes** (`0x138`) exactos.
- **Validación en archivo real**: 1.724.112 bytes útiles de registros (5.526 futbolistas base × 312 bytes; 1.724.196 bytes totales en disco con 84 bytes de relleno/alineación final) (hasta 25.208+ en parches completos).

| Campo | Tipo | Offset Byte / Bit | Longitud / Máscara | Descripción |
|---|---|---|---|---|
| `ClubId` | uint32 | `0x00` (4 bytes LE) | 32 bits | ID del club en la base de datos |
| `Id` | uint32 | `0x08` (4 bytes LE) | 32 bits | ID único oficial del futbolista |
| `Height` | uint8 | `0x1B` (1 byte) | `Valor + 100` cm | Altura real del jugador (150 a 215 cm) |
| `Weight` | bitfield | `Bit 256` (Byte `0x20`) | 7 bits (`Valor + 30` kg) | Peso corporal en kilogramos |
| `Country` | bitfield | `Bit 233` (Byte `0x1D`) | 9 bits (`0..511`) | ID de nacionalidad / país en `Country.bin` |
| `Age` | bitfield | `Bit 408` (Byte `0x33`) | 6 bits (`Valor + 15`) | Edad del futbolista (15 a 50 años) |
| `PositionIndex` | bitfield | `Byte 0x36, bit 2` | 4 bits (`0..12`) | Posición principal registrada (GK, CB... CF) |
| `StrongFoot` | bitfield | `Bit 514` (Byte `0x40`) | 1 bit | Pie hábil (0 = Derecho, 1 = Izquierdo) |
| `WeakFootUsage` | bitfield | `Bit 454` (Byte `0x38`) | 2 bits (`Valor + 1`) | Frecuencia uso de pierna mala (1 a 4) |
| `WeakFootAccuracy` | bitfield | `Bit 462` (Byte `0x39`) | 2 bits (`Valor + 1`) | Precisión pierna mala (1 a 4) |
| `Form` | bitfield | `Bit 438` (Byte `0x36`) | 3 bits (`Valor + 1`) | Regularidad / Estado de forma (1 a 8) |
| `InjuryResistance` | bitfield | `Bit 458` (Byte `0x39`) | 2 bits | Resistencia a lesiones (0 = A, 1 = B, 2 = C) |
| `PlayingStyle` | bitfield | `Bit 155` (Byte `0x13`) | 5 bits (`0..21`) | Estilo de juego según vector `DB_STYLE` Fox Engine |
| `NameJapanese` | UTF-8 | `0x81` (`129`) | 61 bytes | Nombre en caracteres japoneses / kanji |
| `ShirtName` | UTF-8 | `0xBE` (`190`) | 61 bytes | Nombre estampado en la camiseta (Mayúsculas) |
| `Name` | UTF-8 | `0xFB` (`251`) | 61 bytes | Nombre oficial visible en menús y relatos |

---

### 3.6. `PlayerAssignment.bin`
- **Tamaño de Registro**: **16 bytes** exactos.

| Campo | Tipo | Offset Byte | Longitud / Máscara | Descripción |
|---|---|---|---|---|
| `EntryId` | uint32 | `0x00` | 4 bytes LE | Clave primaria única en la tabla de asignaciones |
| `PlayerId` | uint32 | `0x04` | 4 bytes LE | ID del futbolista asignado (`Player.bin`) |
| `TeamId` | uint32 | `0x08` | 4 bytes LE | ID del club asignado (`Team.bin`) |
| `Pack` | uint32 | `0x0C` | 4 bytes LE empaquetados | Dorsal (`ShirtNumber`), Orden en plantilla (`SquadOrder`: 0=GK, 1..10=Titulares, 11..17=Suplentes, 18+=Reservas), Banderas de Capitán, Penales, Faltas y Córners |

---

## 4. CONCLUSIÓN DE FASE 1
Todos los offsets, bitshifts, máscaras de bits, cabeceras y tamaños de registros fueron extraídos de la ingeniería inversa del assembly `.NET` de `PES2020Editor.exe` (desofuscando ConfuserEx v1.0.0 AntiTamper, deriver de llaves, desencriptador de strings y tablas de inicialización de `GClass55`).
Quedan completamente desterradas las suposiciones y valores inventados.
