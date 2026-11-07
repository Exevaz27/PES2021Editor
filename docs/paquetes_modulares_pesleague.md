# ESPECIFICACIÓN TÉCNICA: PAQUETES MODULARES DE LIGAS (.PESLEAGUE)
**Firmado y Desarrollado por EXEVAZ27**
**Documento de Arquitectura y Formato de Intercambio Modular para PES 2021 / SP Football Life**

---

## 1. INTRODUCCIÓN Y OBJETIVO
El formato **`.pesleague`** es un estándar de contenedor modular abierto diseñado para permitir la exportación, compartición e importación de ligas completas (primeras divisiones, segundas divisiones, copas o torneos personalizados) entre diferentes instalaciones o bases de datos de PES 2021 y SP Football Life sin provocar colisiones de IDs, desbordamientos de índices ni pérdidas de integridad relacional.

El paquete encapsula de manera atómica:
1. La definición de la competición (`Competition.bin`).
2. El reglamento oficial y formato de torneo (`CompetitionRegulation.bin`).
3. La configuración de calendario y reglas de Liga Máster (`league_rules.json` y `fl26world.txt`).
4. Los registros binarios de todos los clubes participantes (`Team.bin`).
5. Las entradas ordenadas y enlazadas (`CompetitionEntry.bin`).
6. Los escudos y logos en alta resolución (`emblem.png` y `teams/<id>.png`).

---

## 2. ESTRUCTURA FÍSICA DEL CONTENEDOR ZIP

El archivo `.pesleague` utiliza la especificación de contenedor **ZIP estándar (RFC 1950/1951)** con compresión deflate:

```text
archivo_exportado.pesleague (ZIP)
│
├── manifest.json            # Metadatos completos, reglas y equipos en JSON
├── emblem.png               # Logo principal de la competición (512x512 PNG con transparencia)
└── teams/                   # Directorio con escudos de los clubes participantes
    ├── 1050.png             # Escudo original del equipo ID 1050
    ├── 1051.png             # Escudo original del equipo ID 1051
    └── ...
```

---

## 3. ESPECIFICACIÓN DEL ARCHIVO `manifest.json`

El archivo `manifest.json` contiene la serialización declarativa de la liga y la carga binaria en Base64 de cada equipo:

```json
{
  "version": "1.0",
  "created_at": "2026-10-26T07:30:00Z",
  "generator": "PES2021EditorPro",
  "original_id": 65,
  "name_ascii": "LIGA_ARGENTINA",
  "name_spanish": "Liga Profesional de Fútbol",
  "name_latam": "Liga Profesional",
  "name_english": "Argentine League",
  "continent": 4,
  "country_id": 144,
  "total_teams": 28,
  "comp_type": 0,
  "is_independent": true,
  "tier": 1,
  "parent_comp_id": 0,
  "parent_comp_name": "",
  "changes": 5,
  "registered_players": 23,
  "rounds": 1,
  "tie_breaker": 1,
  "cup_format": 0,
  "extra_time": false,
  "penalties": false,
  "away_goals": false,
  "third_place": false,
  "single_leg": false,
  "num_groups": 2,
  "num_classified_teams": 8,
  "final_championship": true,
  "championship": true,
  "promote": 2,
  "demote": 2,
  "continental1": 4,
  "continental2": 6,
  "season_type": 1,
  "linked_cup_id": 49,
  "linked_cup_name": "Copa Argentina",
  "linked_super_cup_id": 79,
  "linked_super_cup_name": "Supercopa Argentina",
  "teams": [
    {
      "original_id": 1050,
      "is_custom": true,
      "name_spanish": "Boca Juniors",
      "name_english": "Boca Juniors",
      "short_name": "BOC",
      "country_id": 144,
      "stadium_id": 105,
      "coach_id": 10020,
      "is_national": false,
      "raw_base64": "<1532 bytes serializados en Base64>"
    }
  ]
}
```

---

## 4. MOTOR DE REMAPEO RELACIONAL ANTI-COLISIONES (ID RELATIONAL REMAPPING)

Cuando un usuario importa un paquete `.pesleague` en una base de datos que ya posee equipos o ligas en los mismos identificadores numéricos, el motor de importación ejecuta un algoritmo de remapeo de tres niveles:

### 4.1. Nivel 1: Remapeo de ID de Competición
1. Verifica si `manifest.original_id` está ocupado en `Competitions` o si es menor que 50 (reservado para ligas oficiales nativas).
2. Si existe colisión, consulta la tabla de slots libres (`GetAvailableCompetitionIds()`) o busca el primer slot vacante en el rango `[50..254]`.
3. Reasigna el `AssignedCompId` al nuevo slot libre.

### 4.2. Nivel 2: Remapeo de IDs de Equipos y Carga Binaria
1. Escanea todos los IDs utilizados en la base destino (`db.Teams`).
2. Establece un puntero de búsqueda de ID libre a partir de `1000`.
3. Para cada club en `manifest.teams`:
   - Si el ID original no está ocupado y es `>= 500`, se conserva el ID original.
   - Si el ID original ya está ocupado por otro club, se le asigna el siguiente ID libre disponible.
   - Se registra la correspondencia en la tabla `TeamIdMap[OriginalId] = NewId`.
   - Se decodifica el bloque de 1532 bytes de `raw_base64`.
   - Se sobreescribe el ID numérico en el offset 8 del binario del equipo:
     ```csharp
     BitConverter.GetBytes(newTid).CopyTo(raw, 8);
     ```
   - Se instancia el nuevo `TeamRecord` con sus nombres y banderas correspondientes y se inserta en la base de datos destino.

### 4.3. Nivel 3: Remapeo de Gráficos y Assets en LiveCPK
1. Extrae los escudos empaquetados en `teams/<orig_id>.png`.
2. Utiliza la tabla `TeamIdMap` para renombrar los archivos con el nuevo identificador:
   `teams/1050.png` ➔ `e_001000_r.png`, `e_001000_r_l.png`, etc.
3. El motor `ImageProcessor` genera automáticamente las variantes de tamaño y siluetas para fondos oscuros y claros requeridas por el Fox Engine.
4. Extrae `emblem.png` y lo almacena en `common/render/symbol/emblemLc/emb_<AssignedCompId>_l.png` y sus variantes.

---

## 5. SINCRONIZACIÓN ATÓMICA DE MOTOR Y REGLAS
Una vez finalizada la inserción de registros:
1. Se invocan los métodos de persistencia:
   - `SaveCustomLeagueRules()`: Genera o actualiza `league_rules.json` asociando ascensos, descensos, cupos continentales y tipo de calendario al nuevo ID de competición.
   - `fl26world.txt`: Si `SeasonType == 1` (calendario anual Feb-Dic), se inyecta la directiva correspondiente para que la Liga Máster no empiece en Agosto.
2. Se compilan las entradas en `CompetitionEntry.bin` de forma 1-indexed contigua.
3. Se despliega en la interfaz de usuario el informe con el desglose de IDs reasignados y escudos instalados.
