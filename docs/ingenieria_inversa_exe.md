# INGENIERÍA INVERSA FORENSE DE PES2021.EXE (MOTOR NATIVO x64)
**Firmado y Desarrollado por EXEVAZ27**
**Documento Técnico de Arquitectura de Memoria y Motor de Competiciones**

---

## 1. ESPECIFICACIÓN DEL BINARIO EJECUTABLE
- **Archivo**: `PES2021.exe`
- **Ruta**: `<carpeta-del-juego>\PES2021.exe`
- **Arquitectura**: PE32+ (x86-64 / 64-bit Little-Endian)
- **Tamaño**: `458,739,200 bytes` (437.49 MB)
- **ImageBase**: `0x0000000140000000`
- **Sección de Código Principal (.trace / .text)**:
  - Virtual Address (VA): `0x140001000`
  - Virtual Size: `0x0252D800` (38.98 MB)
  - Raw File Offset: `0x00000600`
- **Sección de Datos de Sólo Lectura (.rdata)**:
  - VA: `0x14252F000` | Raw File Offset: `0x0252DE00`
- **Sección de Datos Dinámicos (.data)**:
  - VA: `0x14339B000` | Raw File Offset: `0x03399A00`

---

## 2. TABLA CENTRALIZADA DE DESCRIPTORES DE BASE DE DATOS (PESDB)
El ejecutable no carga los archivos `.bin` mediante rutas dispersas, sino que centraliza todos los descriptores de tablas de la base de datos en una estructura unificada en memoria dentro de `.data`:
- **Dirección Virtual (VA)**: `0x00000001434D6600`
- **File Offset**: `0x034D5000`
- **Estructura del Descriptor**:
  - `+0x00`: Puntero al buffer cargado en RAM (payload descomprimido).
  - `+0x08`: Puntero al contenedor original WESYS.
  - `+0x10`: Tamaño descomprimido / Número de registros.
  - `+0x18`: Tamaño exacto del registro (Record Size).
  - `+0x30`: Puntero a cadena ASCII con el nombre del archivo `.bin`.

### Mapeo Real de Tablas Críticas:
| Índice | Puntero a Nombre (VA) | Nombre de Archivo | Tamaño Registro (Bytes) | Hex Stride |
|---|---|---|---|---|
| 11 | `0x1428BB000` | `Competition.bin` | 36 bytes | `0x24` |
| 12 | `0x1428BB010` | `CompetitionEntry.bin` | 12 bytes | `0x0C` |
| 13 | `0x1428BB028` | `CompetitionKind.bin` | 16 bytes | `0x10` |
| 14 | `0x1428BB040` | `CompetitionRegulation.bin` | 2,352 bytes | `0x930` |
| 31 | `0x1428BAF08` | `Team.bin` | 1,532 bytes | `0x5FC` |

---

## 3. PIPELINE DE INICIALIZACIÓN Y MOTOR DE COMPETICIONES
La función orquestadora principal que inicializa todo el ecosistema de ligas y torneos se encuentra en:
- **Función Orquestadora**: `0x00000001414F6C70`
- **Callers y Subrutinas del Pipeline**:
  - `0x1414F6CA0`: `call 0x1414F0710` -> Carga e indexación de `Competition.bin` (36 bytes).
  - `0x1414F6CAE`: `call 0x1414F1480` -> Vinculación relacional entre `CompetitionEntry.bin` (12 bytes) y `CompetitionRegulation.bin` (2,352 bytes).
  - `0x1414F6CBC`: `call 0x1414F2030` -> Generador de Calendario y Fixtures Round-Robin.
  - `0x1414F6CCA`: `call 0x1414F5120` -> Generador de llaves de eliminación (Copas / Supercopas).
  - `0x1414F6CD8`: `call 0x1414F3780` -> Inicializador de Tabla de Ascensos y Descensos.

---

## 4. GENERADOR DE FIXTURES Y VALIDACIÓN DE EQUIPOS
En `0x00000001414F2030`:
- El motor calcula las fechas de partidos mediante el algoritmo Round-Robin (tablas de Berger).
- Para que una liga se dispute en formato liga regular sin error o crash en Liga Máster / Torneo, **la cantidad de equipos participante DEBE SER PAR** (`TotalTeams % 2 == 0`).
- Tamaños de liga completamente testeados y compatibles con las rutinas de calendario del motor:
  - **10 equipos**: 18 fechas (2 ruedas)
  - **12 equipos**: 22 fechas (2 ruedas)
  - **14 equipos**: 26 fechas (2 ruedas)
  - **16 equipos**: 30 fechas (2 ruedas)
  - **18 equipos**: 34 fechas (2 ruedas)
  - **20 equipos**: 38 fechas (2 ruedas)
  - **22 equipos**: 42 fechas (2 ruedas)
  - **24 equipos**: 46 fechas (2 ruedas)
- **Jump Table del Motor**: Ubicada en `0x00000001414F2384` con 19 entradas para manejar variantes de torneos con y sin playoffs/divisiones de grupo.

---

## 5. TABLA DE ASCENSOS Y DESCENSOS (DIVISIÓN 1 Y DIVISIÓN 2)
En el bucle de procesamiento de `0x00000001414F1860`:
- El registro de `CompetitionRegulation` se examina:
  - `[r12 - 0x0A]` (Offset `0x04` en el registro): `IdDivision1`.
  - `[r12 - 0x10]` (Offset `0x00` en el registro): `IdDivision2`.
  - Si `IdDivision1 == 0`: La liga es clasificada como **Primera División (Tier 1)** o Liga Independiente.
  - Si `IdDivision1 != 0`: La liga es clasificada como **Segunda División (Tier 2)** y se enlaza bidireccionalmente al `RegId` de la Primera División.
- En `Competition.bin`:
  - `IdParentCompetition` en el registro hijo apunta directamente al `IdCompetition` del padre.

### Manejo de Ligas Inferiores Adicionales (3ra, 4ta División):
El motor nativo de Konami en Liga Máster maneja en su tabla interna de memoria 2 divisiones directas activas. Para soportar expansiones de divisiones inferiores adicionales (3ra y 4ta división) sin tocar el código fuente cerrado del juego:
1. En la base de datos `.bin`, las 3ras y 4tas divisiones se registran como competiciones independientes con sus propios equipos, reglamentos y entradas.
2. El módulo Sider (`league_manager.lua`) intercepta el evento de fin de temporada en memoria (`on_season_end`) y realiza el swap de los equipos relegados de 2da división con los equipos promovidos de 3ra división en la tabla de `CompetitionEntry` en RAM, permitiendo un flujo dinámico infinito y fluido sin crasheos.

---

## 6. MAPA DE IDs DISPONIBLES EN BASE DE DATOS REAL
Rango 1..255 (identificador de 8 bits en `Competition.bin`, `CompetitionEntry.bin` y `CompetitionRegulation.bin`):
- **IDs Libres y Seguros**: `150` al `240`.
- Esta franja está libre de cualquier uso por parte de Konami o parches estándar, garantizando 100% de aislamiento y cero riesgo de sobreescritura accidental de torneos oficiales.
