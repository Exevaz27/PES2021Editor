# DOCUMENTACIÓN TÉCNICA DE INGENIERÍA INVERSA: PES 2021 (EXE v1.07.02)
## Expansión Limpia de Ligas en Partido Amistoso (Exhibition Match)
**Autor:** EXEVAZ27  
**Fecha:** Octubre 2026 (Revisión v3.3.0 - Diagnóstico Definitivo y Solución)  
**Objetivo Binario:** `PES2021.exe` (Versión 1.07.02 / SP Football Life)  
**Tamaño del Binario:** 458.739.200 bytes (437.49 MB / PE32+ sin comprimir con sección `.text` de 38.983.680 bytes / `0x0252D800`)  
**Arquitectura:** x86-64 (AMD64 / Windows PE32+)  

---

## 1. El Diagnóstico Técnico Definitivo: ¿Por qué no aparecía la liga?

Mediante el desensamblado forense de todo el pipeline de interfaz de usuario de Partido Amistoso (`0x140C0278B`, `0x140DC7530`, `0x141F00850`, `0x141F12FE0`, `0x141407ED0` y `0x141409640`), se descubrieron **las dos causas exactas a nivel de opcode** que impedían la aparición de `PRIMERA B NACIONAL [MOD]`:

### Causa 1: El Hook 3 destructivo en `0x141F00850` (`BuildExhibitionTeamList`)
En versiones previas se instaló un hook en `0x141F00850`:
```x86asm
0x14252E740: cmp edx, 0x32       ; ¿Es CompID 50?
0x14252E743: jne 0x14252E74b
0x14252E745: mov eax, 0x14       ; Retorna 20
0x14252E74a: ret                 ; ¡Sale de inmediato!
0x14252E74b: ...
0x14252E757: jmp 0x141ECE860     ; Salto a dirección inválida para cualquier otra liga
```
**Impacto letal:**
1. Al interceptar `0x141F00850` y hacer `ret` directo, **la función nativa de Konami NUNCA ejecutó el bucle de población de equipos**. Nunca llamó a `0x141407ED0(Slot 30)` ni a `fn1` (`0x14252E6A0`). Por esa razón, el contador de accesos en vivo (`Hits`) permanecía permanentemente en **0** y el array interno de equipos de la UI quedaba completamente vacío.
2. Para cualquier otra liga (`edx != 50`), el trampolín ejecutaba un salto a una dirección corrupta (`0x141ECE860`), desestabilizando el motor de selección.
3. **Descubrimiento fundamental:** `0x141F00850` **NO DEBE SER HOOKEADA**. Su código nativo original ya ejecuta de forma limpia el bucle de equipos sobre `Slot 30`, delegando en `0x141407ED0` y en nuestro getter nativo `fn1`.

---

### Causa 2: El desplazamiento por inserción en la Entrada 19
En el archivo Lua previo, se insertó Primera B Nacional en el índice 19 de la tabla de exhibición (`0x1434C7000`), desplazando todas las ligas sudamericanas y asiáticas posteriores:
* Entrada 18: Liga Argentina (CompID 22)
* **Entrada 19: Primera B Nacional (CompID 50)**
* Entrada 20 (era 19): Brasileirão Série A (CompID 21)
* Entrada 21 (era 20): Brasileirão Série B (CompID 90)
* Entrada 22 (era 21): Liga Chilena (CompID 23)
* Entrada 23 (era 22): Liga Colombiana (CompID 122)
* Entrada 24 (era 23): Liga PLA (CompID 40)
* Entrada 25 (era 24): Otros Latinoamérica (CompID 65281)

**Consecuencias en los límites duros de Konami:**
1. **Gatekeeper de Rango Sudamericano (`0x141F13143` y `0x141F136CF`):**
   ```x86asm
   0x141F13139: cmp r14d, 0x12       ; ¿Índice >= 18?
   0x141F1313D: jl exit
   0x141F13143: cmp r14d, 0x18       ; ¿Índice < 24? (Ligas domésticas vanilla = 18..23)
   0x141F13147: jae exit             ; ¡SI ES >= 24, DESCARTA LA LIGA DOMÉSTICA!
   ```
   En vanilla, las 6 ligas domésticas de Sudamérica ocupan exactamente los índices 18 a 23 (Argentina, Brasil A, Brasil B, Chile, Colombia, PLA); la entrada 24 corresponde a 'Otros Equipos' y es procesada por otra rutina de visualización. Al desplazar los índices con la inserción (+1), la Liga PLA quedó empujada al índice 24, cayendo en el `jae exit` de las ligas domésticas y desapareciendo del menú.
2. **Desalineación de Slots de Propiedades (`0x141F13178`):**
   El motor busca descriptores en `0x1434F59A0` calculando `r14 - 14`. Al estar desplazada la tabla en +1, Primera B Nacional tomó el slot de la Série A (5), Série A tomó el de Série B (6), y así sucesivamente, corrompiendo la lectura de nombres y tarjetas.

---

## 2. Evolución Arquitectural: Del Enfoque Append (v3.3.0) a la Expansión Maestra v4 (Matozanato)

> [!NOTE]
> **Contexto Histórico y Vigencia Arquitectural (Altos 60 y 61):**
> La arquitectura por **Append a Entrada 30** documentada a continuación representó el descubrimiento intermedio clave de la versión v3.3.0, al probar empíricamente que el carrusel de amistosos de Konami no está limitado por posiciones fijas sino que recorre dinámicamente las entradas evaluando `continent == active_tab`.
> En la arquitectura definitiva actual (**v4.x**, documentada a partir de la Sección 8 e implementada en `SiderLuaGenerator.cs` y `league_manager.lua`), este mecanismo fue superado por la **Expansión de la Tabla Estática de Parámetros de Konami (`0x1434FDF00`)**: mediante `VirtualAlloc` en memoria RAM se reservan 165+ filas completas de 264 bytes, repuntando las 3 instrucciones LEA (`0x1414FDBDA`, `0x1414FDD71`, `0x1414FDE07`) y actualizando dinámicamente los 4 contadores de bucle del motor (`0x1414FDC4A`, `0x1414FDD60`, `0x1414FDE1B`, `0x1414FE1E0`). Esto elimina cualquier dependencia del límite original de 30 entradas (`0x1E`) y permite registrar hasta 250 slots de competición concurrentes.

Al analizar la función `0x140DC75F0` (`PopulateCarouselForTab`), invocada en `0x140C0278B` al abrir la pestaña de Sudamérica:
```c
void PopulateCarouselForTab(MatchRosterList* this, int active_tab) {
    int total_comps = GetExhibitionCompetitionCount(); // 31
    for (int i = 0; i < total_comps; i++) {
        ExhibitionEntry* entry = GetExhibitionCompetitionByIndex(i);
        if (entry->continent == active_tab) {
            int team_count = BuildExhibitionTeamList(entry->comp_id);
            if (team_count > 0) {
                carousel_vector.push_back(entry->comp_id);
            }
        }
    }
}
```
**Descubrimiento clave:** `PopulateCarouselForTab` **recorre toda la tabla y filtra únicamente por `continent == active_tab`**. No le importa en absoluto la posición numérica del índice en la tabla.

Por lo tanto:
1. **Entradas 0..29 permanecen 100% idénticas a la memoria original de Konami:**
   * Cero desplazamiento.
   * Liga Argentina (18), Série A (19), Série B (20), Chile (21), Colombia (22), PLA (23), Otros (24) y Asia (25..29) permanecen en sus índices nativos exactos.
   * Ningún límite vanilla (`cmp r14d, 0x18`) es violado.
2. **Primera B Nacional se anexa al final como Entrada 30 (`0x1434C71E0`):**
   * `CompID = 50`
   * `Continent = 1` (Sudamérica)
   * `DisplaySlot = 30`
   * `Flags = 32` (`0x20`)
3. **Al seleccionar la pestaña de Sudamérica (`active_tab == 1`):**
   * El bucle procesa las 7 ligas vanilla de Sudamérica.
   * Llega a la Entrada 30: `entry->continent == 1` (Coincide).
   * Llama a `BuildExhibitionTeamList(50)` de forma nativa.
   * `0x141F00850` busca CompID 50 -> encuentra Entrada 30 -> `DisplaySlot = 30`.
   * Llama a `0x141407ED0(30)` -> obtiene el descriptor de `Slot 30`.
   * Ejecuta el bucle de 32 iteraciones llamando a `fn1` (`0x14252E6A0`).
   * `fn1` copia los 20 equipos reales desde `0x1434C7800` y descarta los centinelas `262143`.
   * Retorna `20` equipos (`team_count > 0`).
   * **`carousel_vector.push_back(50)`**: Agrega Primera B Nacional al carrusel vertical de Sudamérica.
   * Cuando el usuario selecciona la liga, `0x140C074C0` y `0x140C0786A` renderizan sus 20 equipos.

---

## 3. Estado de Verificación y Archivos Desplegados

1. **`SiderLuaGenerator.cs`:**
   * Corregida la expansión a modo Append puro (Entrada 30).
   * Eliminado Hook 3; restaurado `0x141F00850` a su opcode nativo `48 89 5C 24 08 55 56 57`.
   * Actualizada la sonda de telemetría a `Comp30`.
2. **Binarios Compilados:**
   * `<carpeta-del-editor> ligas\EditorLigasPes.exe` (compilado con `csc.exe` v4.8).
   * `<carpeta-del-editor> ligas\CreadorLigasPES2021.exe` (copia sincronizada).
3. **Módulo Sider Activo:**
   * `<carpeta-del-juego>\sider\modules\league_manager.lua` generado y desplegado.

---

## 9. SISTEMA INTEGRAL DE LOGOS Y ESCUDOS (SIDER LIVECPK & CPK EXTRACTOR)

### 9.1. Nomenclatura Forense y Rutas en PES 2021
1. **Emblemas de Competiciones (`emblemLc`):**
   * Ruta en juego/Sider: `common/render/symbol/emblemLc/emb_<CompetitionID, 4 dígitos><variante>.png`
   * Resoluciones y variantes:
     * `_l.png` (256×256 px): Color principal para menús y tablas.
     * `_ll.png` (512×512 px): Alta resolución para presentación y tarjetas.
     * `_b_l.png` y `_b_ll.png`: Siluetas oscuras para fondos claros (`#202024`).
     * `_w_l.png` y `_w_ll.png`: Siluetas claras para fondos oscuros (`#F5F5FA`).
2. **Escudos de Clubes y Selecciones (`flag`):**
   * Ruta en juego/Sider: `common/render/symbol/flag/e_<TeamID, 6 dígitos>_<sufijo>.png`
   * Resoluciones: `_r` (256×256), `_r_l` (256×256), `_r_ll` (512×512/1024×1024), `_f`, `_f_l`, `_f_ll`, `_r_b`, `_r_w`.

### 9.2. Procesamiento Automático (`ImageProcessor.cs`)
* Carga segura en memoria sin bloqueo de archivos (`MemoryStream`).
* **Auto-Trim:** Detección de canal alfa (alpha > 12) y recorte milimétrico de bordes transparentes innecesarios.
* **Escalado Bicúbico:** Ajuste con relación de aspecto preservada, centrado perfecto y relleno transparente.
* **Generador de Siluetas:** Procesamiento por punteros (`unsafe BitmapData`) para generar automáticamente las variantes de contraste exigidas por el Fox Engine.

### 9.3. Galería y Catálogo Oficial (`GameEmblemSelectorForm.cs` & `GameEmblemProvider.cs`)
* Lector nativo de tablas `@UTF` del archivo `Data\dt15_x64.cpk`.
* Extracción y visualización instantánea bajo demanda de los 246 logos oficiales del juego.
* Buscador en tiempo real por nombre de torneo o ID para clonar o reasignar logos con un solo clic.

---

## 10. SISTEMA DE PAQUETES MODULARES (.PESLEAGUE) Y REMAPEO RELACIONAL

### 10.1. Especificación del Contenedor
El formato `.pesleague` empaqueta como contenedor ZIP:
1. `manifest.json`: Serialización de la competición, país, reglamentos, formato de torneo, calendario, reglas LM y lista de equipos con su carga binaria de 1532 bytes codificada en Base64.
2. `emblem.png`: Logo oficial de la liga.
3. `teams/<id>.png`: Escudos y banderas de los clubes participantes.

### 10.2. Motor de Remapeo Relacional Anti-Colisiones
Para evitar conflictos de claves primarias y superposiciones al compartir ligas:
1. **Competición:** Si el ID original colisiona o es menor a 50, se asigna el primer ID vacante en el rango de ligas (50..254).
2. **Equipos:** Si los IDs numéricos de los clubes ya existen en la base destino, se genera una tabla de remapeo `TeamIdMap` reasignando a partir de 1000+, sobreescribiendo el `TeamId` en el offset 8 de `Team.bin`.
3. **Assets Sider LiveCPK:** Los escudos y logos son renombrados y procesados automáticamente hacia las carpetas correspondientes con la nomenclatura exacta requerida por el motor.
4. **Sincronización:** Se actualizan atómicamente `Competition.bin`, `CompetitionRegulation.bin`, `CompetitionEntry.bin`, `Team.bin`, `league_rules.json` y `fl26world.txt`.

---

## 11. CRIPTOGRAFÍA FORENSE Y FORMATO DE GUARDADO (EDIT00000000)

### 11.1. Arquitectura de Cifrado y Claves Maestras
El archivo de guardado del Modo Editar (`EDIT00000000`) utiliza una capa de cifrado de flujo personalizada basada en el generador pseudoaleatorio **Mersenne Twister MT19937 (32-bit)** alimentado por una clave simétrica de 64 bytes (512 bits):
* **PES 2021 / Football Life:** `90 61 D8 66 43 77 24 F8 92 BA B8 71 21 C7 60 63 F0 91 9A 7D ED 47 80 DE 51 F5 DD D1 08 FE 32 84 F5 09 92 00 B2 3E 88 9F EB 24 43 05 58 76 00 22 9B FE EC F6 50 00 29 D3 42 75 50 B9 EC D2 F6 75`
* **PES 2020:** `E2 BF 51 07 54 E6 21 78 2C 5E 8D 33 13 7A C9 15 99 77 D9 A0 1B C2 95 D9 BB 9B B1 00 84 1C B3 62 E5 40 D9 56 45 5B 7C 7C 4F F1 DA 26 B4 5A 0C 5C 4D 6B 9E 98 75 A9 39 07 4C 4B 55 BD 8E 01 A9 31`

### 11.2. Estructura de Segmentos y Derivación de Claves
El archivo encriptado se compone de 6 bloques concatenados:
1. `EncHeader` (320 bytes): Cabecera de autenticación cifrada con `hk = input[256..319] ^ master`.
2. `Header` (208 bytes): Metadatos del contenedor descifrados con `DeriveKey(enc, 208)`. Contiene los tamaños `dataSize` (+64), `logoSize` (+68), `descSize` (+72) y `serialLength` (+76).
3. `Desc` (`descSize` bytes): Descripción de la partida en UTF-8 cifrada con `DeriveKey(enc, 0)`.
4. `Logo` (`logoSize` bytes): Imagen PNG oficial de la miniatura de guardado cifrada con `DeriveKey(enc, 1)`.
5. `Data` (`dataSize` bytes): Payload masivo descompreso (`data.dat`, ~10.99 MB) cifrado con `DeriveKey(enc, 2)`. Contiene las tablas completas de jugadores, clubes, tácticas y alineaciones.
6. `Serial` (`serialLength * 2` bytes): Cuenta de usuario SteamID / Windows SID cifrada con `DeriveKey(enc, 3)`.

### 11.3. Mecanismo Anti-Bloqueo de Option Files (Desbloqueador de Cuenta)
Konami valida en el arranque si el `Serial` de la partida coincide con el SteamID o Windows SID de la sesión activa. Si difiere, aborta la carga con: *"Los datos pertenecen a otro usuario y no pueden cargarse"*.
El motor `PesCrypt` permite reemplazar el bloque de `Serial` por el SID del usuario local y recalcular la firma completa en 1 solo clic, logrando compatibilidad instantánea con Option Files compartidos.

---

## 12. PERSISTENCIA DE CLUBES EXTENDIDOS EN MODO EDITAR (`fl26edit` Y `fl26editlist`)

### 12.1. El Límite Nativo de 749 Clubes en Modo Editar
En PES 2021 vainilla, las estructuras de datos en memoria para el Modo Editar (`EditData`) asignan arrays con un límite rígido de **750 ranuras** (índices 0 a 749) por tabla:
* `TeamData` (Datos básicos de club, colores, nombres, insignias)
* `Squad` (Plantillas de jugadores y dorsales)
* `Tactics` (Estrategias, alineaciones y directivas)
* `Manager` (Cuerpos técnicos)

Cuando el usuario modifica un club con ID >= 750 (como los creados para ligas personalizadas) y guarda la partida desde el menú Editar interno del juego, Konami solo serializa los primeros 750 registros (índices 0 a 749) dentro de `EDIT00000000`. Todas las modificaciones efectuadas a clubes extendidos (índice 750 en adelante) son descartadas silenciosamente y se pierden en el siguiente reinicio.

### 12.2. Expansión a 2.048 Clubes en Memoria (`fl26edit.dll`)
La biblioteca nativa de 64 bits `fl26edit.dll` intercepta el motor durante la rutina de inicialización temprana antes de que se construyan las estructuras del Modo Editar:
1. **Reasignación de Tablas:** Amplía los búferes de memoria RAM asignados por el juego de 750 clubes a **2.048 clubes** (`0x800` filas).
2. **Hook de Guardado Interceptado:** Al presionar "Guardar" en el menú Editar:
   * Los primeros 750 clubes de cada tabla (índices 0 a 749) se escriben normalmente en `EDIT00000000`, preservando compatibilidad bit a bit con herramientas de edición estándar y con instalaciones sin parches.
   * Todos los clubes a partir del índice 750 (el club 751º en adelante) son interceptados y volcados al contenedor secundario `EDIT00000000.fl26x`.
   * Si existía un archivo `.fl26x` previo y el usuario responde "No" a la confirmación de sobrescritura en el juego, se preserva como `EDIT00000000.fl26x.prev`.
3. **Hook de Carga en Memoria:** Al cargar `EDIT00000000`, la DLL detecta la presencia del archivo `.fl26x` en la misma carpeta, valida la cabecera mágica y el hash de emparejamiento contra el archivo base, y reconstituye los 2.048 registros en memoria RAM sin fricción.

### 12.3. Estructura Binaria del Contenedor `.fl26x`
El archivo secundario `.fl26x` presenta la siguiente arquitectura binaria:
* **Offset 0x00..0x07 (8 bytes):** Firma Mágica ASCII. `FL26EDX2` (Versión 2 con soporte de 4 tablas incluyendo directores técnicos) o `FL26EDX1` (Versión 1 de 3 tablas).
* **Offset 0x08..0x27 (32 bytes):** Token de Validación / Hash de Emparejamiento. Empareja de forma unívoca el `.fl26x` con el payload del `EDIT00000000` correspondiente para prevenir cargas cruzadas desincronizadas.
* **Offset 0x28..0x37 (16 bytes en v2, 12 bytes en v1):** 4 enteros de 32 bits Little Endian (`uint32`) con el recuento de registros guardados para cada tabla:
  - `uint32 TeamCount`: cantidad de clubes extendidos guardados.
  - `uint32 SquadCount`: cantidad de plantillas guardadas.
  - `uint32 TacticsCount`: cantidad de tácticas guardadas.
  - `uint32 ManagerCount`: cantidad de entrenadores guardados (en v2).
* **Offset 0x38 en adelante:** Payloads continuos de registros en formato nativo Konami.

### 12.4. Inyección de Ligas en Listas de Selección de Modo Editar (`fl26editlist.lua`)
Los menús internos de Editar (Editar > Equipos, Editar > Jugadores > Seleccionar Equipo, Traspasos, etc.) no consultan las tablas de reglamentaciones de torneos, sino que invocan una función con 30 slots codificados en su propio ensamblador:
* **Función del Juego:** `0x141ee68f0` (ejecuta 30 llamadas a `push_back` con slots fijos como 7, 50, 8, 52, etc.).
* **Epílogo:** `0x141ee7086`: `48 8b 5c 24 38` (`mov rbx, [rsp+0x38]`).
* **Mecanismo de Hook:** `league_manager.lua` sustituye los 5 bytes de `0x141ee7086` por un salto `jmp` a un trampolín ejecutable que:
  1. Lee dinámicamente las ligas registradas en `modules\fl26world.txt` (o lista de slots adicionales).
  2. Para cada slot de competición que no esté ya en la lista por defecto, llama a la función nativa del motor `0x14047be40(vector, &slot)`.
  3. Ejecuta la instrucción reemplazada `mov rbx, [rsp+0x38]` y salta de regreso a `0x141ee708b`.

* **Desensamblado Forense y Aritmética del Trampolín (46 bytes exactos):**
  * `0..6` (7 bytes): `lea rdi, [rip->slots]` (`48 8d 3d rel32`)
  * `7..9` (3 bytes): `loop: cmp dword [rdi], -1` (`83 3f ff`)
  * `10..11` (2 bytes): `je done (+0x18 -> offset 36)` (`74 18`)
  * `12..14` (3 bytes): `mov rdx, rdi` (`48 89 fa`)
  * `15..17` (3 bytes): `mov rcx, rbx` (`48 89 d9`)
  * `18..27` (10 bytes): `mov rax, 0x14047be40` (`EDIT_PUSH`) (`48 b8 ...`)
  * `28..29` (2 bytes): `call rax` (`ff d0`)
  * `30..33` (4 bytes): `add rdi, 4` (`48 83 c7 04`)
  * `34..35` (2 bytes): `jmp loop (-0x1d -> offset 7)` (`eb e3`)
  * `36..40` (5 bytes): `done: mov rbx, [rsp+0x38]` (`48 8b 5c 24 38`) (instrucción reemplazada)
  * `41..45` (5 bytes): `jmp back` (`e9 rel32`). El opcode `0xe9` reside en offset 41; al ejecutarse, el puntero RIP apunta a `stub_addr + 46` (`stub_addr + 41 + 5`). El salto relativo se calcula como `rel32(stub_addr + 46, EDIT_BACK)` y se protege con `assert(#s == 46)` para prevenir desalineaciones off-by-one de instrucción.

**Resultado:** Todas las ligas y copas personalizadas aparecen de manera automática y nativa con sus banderas y nombres en todos los menús desplegables del Modo Editar.

### 12.5. Protección de Integridad de Ligas Base Vanilla y Prevención de Colisiones de IDs
Para evitar cuelgues irreversibles en el motor de juego al manipular competiciones en la base de datos binaria:
* **Ligas Estructurales Inmutables (Hard Lock):** El motor de PES 2021 asume en su lógica de Liga Master el acoplamiento rígido de divisiones hermanas (D1/D2) y anclas sudamericanas:
  * **ID 21 (Brasileirão Série A) e ID 90 (Brasileirão Série B):** su eliminación destruye el vínculo `IdDivision2 = 90` y hace crashear el cálculo de descensos a fin de temporada.
  * **ID 17 / 88:** Premier League / Championship (acoplamiento inglés D1/D2)
  * **ID 18 / 89:** Ligue 1 / Ligue 2 (acoplamiento francés D1/D2)
  * **ID 19 / 92:** Serie A / Serie B (acoplamiento italiano D1/D2)
  * **ID 20 / 93:** LaLiga / LaLiga 2 (acoplamiento español D1/D2)
  * **Regla:** Bloqueo absoluto en la capa de interfaz y en `DatabaseManager.DeleteLeague` ante cualquier intento de borrado.
* **Cierre del Agujero 61-149:** Toda competición oficial de Konami (`!isCustom`, rango vanilla 1..149) cuenta con confirmación de seguridad crítica obligatoria para impedir el borrado accidental de copas nacionales, torneos continentales y segundas divisiones.
* **Validación Temprana y Feedback Visual en Tiempo Real:** En el formulario de creación, `numCompId` valida en tiempo real la disponibilidad del ID con indicador visual dinámico (`✓ Libre` / `⚠️ Ocupado por '{Nombre}'`) y comprobación preventiva al pulsar `Crear Liga`, evitando excepciones tardías y pérdida de datos.

### 12.6. Generación Combinatoria Temática de Equipos (`CombineThemedPool`)
Para evitar bucles infinitos en la creación automatizada de clubes:
* **Fallo de la Diagonal Modular:** Cuando dos listas de longitud $M$ y $N$ se recorren en pasos unitarios simultáneos (`ci++; si++;`), el período de repetición no es $M \times N$, sino $\operatorname{lcm}(M, N)$. Si $M$ es múltiplo de $N$ (como Inglaterra con 24 ciudades y 8 sufijos, donde $\operatorname{lcm}(24, 8) = 24$), el ciclo colapsa en 24 combinaciones fijas. Al pedir 25 o más equipos, `result.Contains(name)` rechazaba el candidato repetido y el `while` entraba en un bucle infinito al 100% de CPU que congelaba el programa.
* **Modelo Producto Cartesiano con Muestreo Disperso:** El generador ahora computa el espacio muestral completo ($|A| \times |B| = 192$ nombres para Inglaterra, 200 para Brasil, 220 para España, 680 para Sudamérica).
* **Terminación Garantizada $O(N)$:** El muestreo recorre el espacio con paso coprimo para máxima variedad estilística y cuenta con fallback numérico indexado determinista, garantizando matemáticamente que la función termine en tiempo constante sin colgar la UI bajo ninguna cantidad de equipos.

### 12.7. Reconciliación de Desensamblado y Blindaje de Integridad de Guardado
* **Reconciliación de Desensamblado 0x140C9C740..0x140C9CB50:**
  * **Causa de la discrepancia:** La dirección virtual `0x140C9C740` (RVA `0xC9C740`, ImageBase `0x140000000`) reside dentro de la sección `.trace` de `PES2021.exe` (`VirtualAddress = 0xC84000`, `PointerToRawData = 0xC83400`). Por aritmética PE, el offset de archivo real es `0xC83400 + (0xC9C740 - 0xC84000) = 0xC9BD40`, donde residen los bytes legítimos `56 44 41 89 4E 4C 48 8B D3...` (`push rsi; mov [r14+0x4c], ecx...`). El archivo discrepante utilizaba el offset de archivo directo `0xC9C740` sin conversión de RVA, desensamblando una región desplazada por `0xA00` bytes. La versión verificada y canónica es `disasm_menu.txt`.
* **Preservación Estricta de Estadio y Entrenador en Inspector de Equipos:** Al cargar un club en el inspector, si su `StadiumId` o `CoachId` no figura en el catálogo oficial de `Stadium.bin` / `Coach.bin` (o es `0`, genérico), el editor crea dinámicamente una entrada `"Estadio/DT no catalogado (ID X)"` o `"(Sin asignar)"`, impidiendo que se fuerce el índice 0 y evitando la corrupción silenciosa de estadios o técnicos al guardar cambios.
* **Sincronización Transparente con Respaldo en Destino:** El proceso de guardado genera copias `.bak` de los archivos previos en la carpeta `livecpk` y en `league_manager.lua` antes de escribir. Los errores de acceso son capturados y reportados de forma explícita al usuario, evitando falsos positivos de sincronización cuando la ruta de Sider no existe en la máquina.
* **Eliminación de Testigos de Diagnóstico Invasivos:** Se eliminó de los flujos de guardado el testigo automático (`ApplyDiagnosticCanary`), asegurando que las ligas existentes (como Liga Argentina ID 22) conserven su integridad y nombres oficiales inalterados.

### 12.8. Guardado Atómico Transaccional y Sistema de Respaldos Integrales (Grupo A)
Para prevenir la corrupción de datos ante interrupciones repentinas, excepciones de serialización o fallos de energía, se rediseñó por completo el pipeline de persistencia de la base de datos binaria:
* **Respaldo Integral de Todos los Archivos Binarios (`CreateTimestampedBackup` y `CreateInitialBackups`):**
  * **Cobertura Total:** En lugar de respaldar únicamente 4 archivos, el sistema realiza copias de seguridad de los 11 binarios del ecosistema de base de datos (`Competition.bin`, `CompetitionRegulation.bin`, `CompetitionEntry.bin`, `Team.bin`, `Coach.bin`, `Stadium.bin`, `Derby.bin`, `PlayerAssignment.bin`, `Player.bin`, `Tactics.bin`, `TacticsFormation.bin`), tablas auxiliares (`Country.bin`, `Ball.bin`, `Boots.bin`, `Glove.bin`) y reglas de torneo (`league_rules.json`).
  * **Resolución Temporal de Alta Precisión:** Las carpetas de respaldo adoptan el patrón `Backups_yyyyMMdd_HHmmss_fff` con milisegundos y un bucle anti-colisión incremental, imposibilitando que guardados sucesivos sobrescriban respaldos previos.
* **Arquitectura de Guardado Atómico en 3 Fases (`Save`):**
  * **Fase 1 (Serialización Segura en RAM):** La totalidad de las colecciones se serializa y comprime en memoria (`WesysCompressor`) antes de acceder al sistema de archivos. Si ocurre cualquier excepción en esta etapa, el disco queda 100% inalterado.
  * **Fase 2 (Escritura en Archivos Temporales `.tmp`):** Todos los archivos binarios y JSON se escriben simultáneamente en archivos temporales aislados mediante un identificador de sesión único (`.tmp_<guid>`). Si la escritura de algún archivo falla (por ejemplo, falta de espacio en disco), se eliminan los archivos temporales y se aborta la transacción sin tocar los binarios activos.
  * **Fase 3 (Commit Atómico):** Una vez validados todos los temporales, se procede a su reemplazo atómico mediante `File.Replace` con fallback transaccional, eliminando cualquier ventana de vulnerabilidad donde la base de datos pueda quedar a medio escribir.
* **Persistencia de Renumeración de Posiciones (`RemoveTeamFromDomesticLeagues` y UI):**
  * Al compactar las posiciones de los clubes de una competición (`Position = (byte)(i + 1)`), el sistema marca explícitamente `entry.IsModified = true` y actualiza `entry.RawBytes = entry.ToBytes()`. Esto erradica el fallo donde los cambios de orden de equipos se descartaban silenciosamente al guardar la base de datos.
* **Persistencia Criptográfica Segura de Partidas (`PesCrypt.Save`):**
  * El guardado de archivos `EDIT00000000` adopta el mismo estándar atómico (`AtomicWriteFile`), verificando además la integridad de las copias de seguridad previas `.bak` antes de reemplazar la partida.


### 12.9. Defensas de UI, Clamping Numérico, Integridad de Combos y Despliegue Robusto de Sider (Grupos B y D)
Para eliminar fallos por desbordamiento de límites de controles (ArgumentOutOfRangeException), retención errónea de índices de comboboxes y garantizar la consistencia en la generación y despliegue de módulos Lua para Sider:
* **Clamping Numérico Defensivo (ClampValue):**
  * Todos los controles numéricos de configuración de liga (
umEditGroups, 
umEditPlayoffs, 
umEditPromote, 
umEditDemote, 
umEditContinental1, 
umEditContinental2) implementan la función ClampValue(val, min, max) en MainForm.cs, garantizando que valores no estándar o superiores a los límites del control no lancen excepciones de UI al navegar entre torneos.
* **Integridad de Comboboxes y Preservación de Metadatos No Catalogados:**
  * **Continentes (cmbEditContinent):** Al seleccionar una competición, se limpian los elementos dinámicos previos y se selecciona el continente exacto. Si el ID de continente no pertenece al catálogo estándar (Europa, América del Sur, Asia), se genera dinámicamente una opción personalizada (Continente no catalogado (ID X)) impidiendo que la liga herede erróneamente el continente de la liga visitada con anterioridad.
  * **Reglamentos y Variantes Oficiales Konami (ApplyCompetitionChanges):** Al guardar una liga, el sistema respeta y preserva los valores exactos no catalogados de Changes (sustituciones), RegisteredPlayers (banco de suplentes), LeagueEachTeamMatches (vueltas de liga), LeagueClassification (criterio de desempate) y variantes de CompetitionType (tipos 1, 2, 5, 9), evitando que se degraden a valores por defecto estándar.
  * **Países, Estadios y Directores Técnicos (LoadTeamIntoInspector):** Cuando un club posee un país, estadio o DT fuera de las listas convencionales de la interfaz gráfica, el inspector genera dinámicamente un registro sustituto (País/Estadio/DT no catalogado (ID X)) y lo selecciona de inmediato. Esto neutraliza la corrupción silenciosa donde clubes sin estadio o DT cambiaban de entidad al guardar sin que el usuario lo tocara.
* **Protección de Capacidad Máxima y Ligas Vanilla en Asignaciones Masivas (AssignSelectedTeamToLeague & DatabaseManager.AssignTeamToLeague):**
  * La interfaz advierte al usuario si una asignación de equipos supera el límite estructural de 32 clubes soportado por el motor de PES 2021.
  * Se requiere confirmación explícita antes de asignar múltiples equipos en bloque a cualquier competición base del juego (compId < 150).
  * En el núcleo (DatabaseManager.AssignTeamToLeague), se incluye una verificación anti-duplicados para imposibilitar la inserción de múltiples instancias de un mismo club en la misma competición.
* **Sanitización de Cadenas y Seguridad en Scripts Lua (SiderLuaGenerator.cs):**
  * Se implementaron EscapeLuaString y EscapeLuaComment, procesando comillas dobles, barras invertidas y saltos de línea para evitar inyecciones de sintaxis o fallos de interpretación en LuaJIT.
  * En el descriptor l26world.txt, los nombres de las competiciones son saneados de saltos de línea y caracteres de control.
* **Verificación de Firmas Previa a Ganchos de Memoria (league_manager.lua & SiderLuaGenerator):**
  * En la rutina de inicialización m.init, la validación de firmas de memoria (check_sites(LEAS) and check_sites(COUNT_SITES)) se ejecuta como Paso 1 absoluto antes de inyectar trampolines de código o instalar null-guards. Si las firmas no coinciden, el módulo aborta limpiamente sin alterar la memoria del juego.
* **Persistencia Transaccional de sider.ini y Despliegue Transparente (DeploySiderModules):**
  * Se asegura la creación de respaldos .bak de sider.ini, league_manager.lua y l26world.txt antes de escribir cambios.
  * Se garantiza que la directiva luajit.ext.enabled = 1 se guarde en sider.ini incluso si la lista de módulos ya se encontraba completa.
  * Se implementó el rastreo de archivos desplegados vs faltantes en la distribución (deployedFiles y missingFiles), presentándolos con claridad en el reporte al usuario en vez de fallar en silencio.


---

## 13. ARQUITECTURA FORENSE DEL MOTOR DE JUGADORES (PLAYER.BIN & FOX ENGINE BITFIELDS)

### 13.1. Estructura del Registro en `Player.bin` (Stride 312 bytes / 0x138)
Cada futbolista en PES 2021 se almacena en un bloque estricto de 312 bytes (`0x138` bytes), con la siguiente distribución:
* **Offset 0x00..0x03 (uint32):** `ClubId` asignado en la base de datos.
* **Offset 0x08..0x0B (uint32):** `PlayerId` único oficial (rango vanilla 1..130.000; creados/modificados 150.000+).
* **Offset 0x1B (1 byte):** Altura del jugador: `Valor + 100` cm (rango 150..215 cm).
* **Offset 0x33 (1 byte):** Edad del jugador: `(Byte & 0x3F) + 15` (rango 15..50 años). En bitfield: `Bit 408, 6 bits`.
* **Offset 0x36 (1 byte):** Posición Principal Registrada: `(Byte >> 2) & 0x0F` (0..12: GK, CB, LB, RB, DMF, CMF, LMF, RMF, AMF, LWF, RWF, SS, CF).
* **Offset 129..189 (61 bytes UTF-8):** `NameJapanese` (nombre con caracteres kanji/katakana o fallback).
* **Offset 190..250 (61 bytes UTF-8):** `ShirtName` (nombre estampado en camiseta, en mayúsculas).
* **Offset 251..311 (61 bytes UTF-8):** `Name` (nombre principal de visualización en interfaz y menús).

### 13.2. Mapeo Forense a Nivel de Bits (Fox Engine Bitfields)
Konami empaqueta estadísticas, habilidades especiales y compatibilidades tácticas en cadenas continuas de bits en `Player.bin`:

#### 1. Físico y Biometría
* **Peso:** `Bit 256, 7 bits` (`Valor + 30` kg).
* **Nacionalidad:** `Bit 233, 9 bits` (Mapea unívocamente al `CountryID` en `Country.bin`).
* **Pie Hábil:** `Bit 514, 1 bit` (0 = Diestro / Derecho, 1 = Zurdo / Izquierdo).
* **Uso de Pierna Mala:** `Bit 454, 2 bits` (`Valor + 1`, escala 1 a 4).
* **Precisión de Pierna Mala:** `Bit 462, 2 bits` (`Valor + 1`, escala 1 a 4).
* **Estado de Forma:** `Bit 438, 3 bits` (`Valor + 1`, escala 1 a 8).
* **Resistencia a Lesiones:** `Bit 458, 2 bits` (0 = A [Muy Resistente], 1 = B [Normal], 2 = C [Propenso]).

#### 2. Las 25 Habilidades Oficiales (`DB_ABIL`, 6 bits cada una, `Valor + 40` para rango 40..99)
* **Ataque y Definición:** Actitud Ofensiva (`370`), Finalización (`396`), Cabeceo (`288`), Potencia de Tiro (`358`), Balón Parado (`250`), Efecto (`332`).
* **Técnica y Pase:** Control de Balón (`281`), Regate (`352`), Conducción Ajustada (`416`), Pase al Ras (`263`), Pase Bombeado (`402`).
* **Físico y Velocidad:** Velocidad (`306`), Aceleración (`344`), Equilibrio (`376`), Contacto Físico (`390`), Salto (`294`), Resistencia (`338`).
* **Defensa:** Actitud Defensiva (`275`), Recuperación de Balón (`312`), Agresividad (`384`).
* **Arquero (GK):** Actitud de Portero (`326`), Atajar (`364`), Despejar (`269`), Reflejos (`320`), Alcance (`300`).

#### 3. Las 40 Habilidades Especiales (`DB_SKILL`, 1 bit cada una)
* *Regate y Filigranas:* Bicicleta (`519`), Doble toque (`523`), Elástica (`485`), Ruleta (`497`), Sombrero (`482`), Giro cruzado (`481`), Recorte y giro (`528`), Amago de tiro (`525`), Pisar el balón (`500`), Taconazo (`505`).
* *Tiro y Definición:* Cabezazo (`495`), Disparo potente lejano (`527`), Vaselina (`506`), Tiro lejano (`518`), Disparo sin rosca (`513`), Disparo con caída (`494`), Disparo ascendente (`499`), Remate acrobático (`524`), Disparo al primer toque (`511`).
* *Pase y Asistencia:* Pase al primer toque (`507`), Pase en profundidad (`487`), Pase medido (`484`), Centro preciso (`483`), Rosca exterior (`493`), Rabona (`515`), Pase sin mirar (`512`), Pase bombeado bajo (`488`), Saque de banda largo (`521`).
* *Arquero:* Saque bajo GK (`490`), Saque alto GK (`496`), Saque largo con la mano (`522`), Atajapenales (`502`).
* *Inteligencia y Personalidad:* Especialista en penales (`501`), Picardía (`491`), Marcaje al hombre (`504`), Repliegue (`517`), Intercepción (`503`), Despeje acrobático (`530`), Capitanía (`492`), Revulsivo (`516`), Espíritu de lucha (`486`).

#### 4. Compatibilidad de Posiciones (`DB_POSFLAG`, 2 bits por posición: 0 = Incompatible, 1 = Nivel B, 2 = Nivel A Principal)
* `GK` (`350`), `CB` (`468`), `LB` (`318`), `RB` (`474`), `DMF` (`414`), `CMF` (`456`), `LMF` (`466`), `RMF` (`460`), `AMF` (`464`), `LWF` (`472`), `RWF` (`476`), `SS` (`478`), `CF` (`470`).

#### 5. Estilos de Juego y COM (IA)
* **Estilo de Juego en Campo:** `Bit 155, 5 bits`. Mapeo relacional `DB_STYLE` (`0..21`) a través del vector Fox Engine: `0: Ninguno`, `1: Cazagoles`, `2: Señuelo`, `3: Hombre de área`, `4: Extremo prolífico`, `5: Extremo móvil`, `6: Especialista en centros`, `7: Enganche clásico`, `8: Jugador de huecos`, `9: Todocampista`, `10: El destructor`, `11: Organizador`, `12: Medio defensivo`, `13: Lateral ofensivo`, `14: Lateral finalizador`, `15: Lateral defensivo`, `16: Salida de balón`, `17: Atacante extra`, `18: Portero ofensivo`, `19: Portero defensivo`.
* **Estilos COM IA (1 bit cada uno):** Malabarista (`489`), Regate en carrera (`531`), Bala (`526`), Desmarque incisivo (`510`), Experto en pase largo (`529`), Centro temprano (`447`), Tirador lejano (`520`).

---

### 13.3. Motor de Decodificación y Renderizado de Minifaces (`MinifaceProvider.cs`)
Para presentar las fotos oficiales de los futbolistas sin depender de librerías externas:
1. **Indexación Multifoco:** Búsqueda recursiva de archivos `.dds` en las rutas activas de Sider:
   * `content\miniface_server\MiniFaces\`
   * `sider\livecpk\`
2. **Decodificador Nativo DXT1 / DXT3 / DXT5:**
   * Descompresión directa de bloques de 4×4 píxeles comprimidos con color endpoint interpolation de 16 bits (RGB565).
   * Desempaquetado de canal alfa explícito (DXT3, 4 bits) y por interpolación de 8 pasos (DXT5).
   * Construcción de `Bitmap` de 32 bits ARGB nativo en memoria RAM.
3. **Generador de Avatares Vectoriales de Respaldo:** Si un futbolista carece de archivo DDS (creados, canteranos o ligas inventadas), el motor genera un avatar posicional circular antialiasing con monograma, gradiente suave y paleta institucional según su demarcación táctica.
4. **Caché de Alto Rendimiento:** Acceso indexado en RAM (300 minifaces procesadas en menos de 190 ms).

---

### 13.4. Relación con `PlayerAssignment.bin` (Stride 16 bytes)
La asignación de futbolistas a sus respectivos clubes se gestiona de forma desacoplada de `Player.bin`:
* **Offset 0x00 (uint32):** `EntryId` (clave primaria única en la tabla de asignaciones).
* **Offset 0x04 (uint32):** `PlayerId` (referencia foránea a `Player.bin`).
* **Offset 0x08 (uint32):** `TeamId` (referencia foránea a `Team.bin`).
* **Offset 0x0C (uint32 packed):** Dorsal (`ShirtNumber`), Orden en plantilla (`SquadOrder`: 0 = Arquero titular, 1..10 = Titulares de campo, 11..17 = Suplentes, 18+ = Reservas) y banderas tácticas (Capitán, Penales, Tiros Libres, Córners).

---

### 13.5. Ciclo CRUD Completo de Futbolistas (`DatabaseManager.cs`)
* **Creación (`CreatePlayer`):** Búsqueda automática del primer ID libre (rango 150.000+), inicialización de la plantilla binaria de 312 bytes con biometría, posición principal, habilidades acordes al nivel seleccionado (70 a 90 OVR) y vinculación opcional inmediata en `PlayerAssignment.bin`.
* **Clonación (`ClonePlayer`):** Duplicación íntegra de los 312 bytes de cualquier jugador existente en la base, asignando nuevo ID único y manteniendo sus 25 habilidades, 40 skills y estilos de juego.
* **Eliminación Segura (`DeletePlayer`):** Remueve al futbolista de `Player.bin` y limpia sus entradas correspondientes en `PlayerAssignment.bin` para evitar errores de punteros nulos en el juego.

---

### 13.6. Blindaje de Integridad en Ficha Fox Engine, Traspasos y Asignación de Plantillas (Bloque 1 - Altos 18, 19, 20, 32, 33, 47)
Para erradicar la corrupción silenciosa por eventos en cadena de la interfaz gráfica y garantizar la coherencia relacional entre `Player.bin` y `PlayerAssignment.bin`:
1. **Bandera de Protección durante la Carga (`_isPopulating` - Alto 47):**
   * Al seleccionar un futbolista y llamar a `DisplayPlayer(p)`, la asignación de valores a los controles (`NumericUpDown`, `ComboBox`, `CheckBox`) disparaba inmediatamente sus eventos (`ValueChanged`, `SelectedIndexChanged`, `CheckedChanged`), mutando los `RawBytes` del jugador en memoria sin intervención del usuario y sin marcar cambios pendientes.
   * Se incorporó la bandera `_isPopulating = true` encapsulada en un bloque `try ... finally` durante todo el renderizado de la ficha. Todos los manejadores de eventos (habilidades, posiciones, 40 skills, estilos de juego, com styles, pie hábil, altura, peso, forma, lesiones) verifican `if (_isPopulating || currentPlayer == null || currentPlayer.RawBytes == null) return;`, garantizando que la navegación y visualización de futbolistas sea de **estricta solo-lectura**. Las mutaciones en `RawBytes` y `HasUnsavedChanges` solo se persisten al pulsar explícitamente "Guardar Cambios".
2. **Traspasos Seguros y Refresco Bidireccional de Plantillas (Altos 18 y 19):**
   * **Exclusión de Selecciones:** El selector de destino filtra automáticamente los combinados nacionales (`!t.IsNational`), permitiendo únicamente traspasos hacia clubes de competición.
   * **Placeholder Obligatorio:** Se introdujo la opción neutra `(Seleccione un club de destino...)` en el índice 0, evitando que el primer club alfabético quede preseleccionado por omisión.
   * **Diálogo de Confirmación:** Se solicita confirmación explícita indicando nombre del jugador, club de origen y club de destino antes de ejecutar el traspaso.
   * **Refresco Concurrente Sin Punteros Rancios:** Se almacena el ID del club previo (`oldClubId = p.ClubId`) antes de mutar la entidad, refrescando simultáneamente la plantilla del club emisor y la del receptor si cualquiera de los dos se encuentra activo en el inspector.
3. **Validación de Capacidad y Prevención de Doble Plantilla en el Core (Altos 32 y 33):**
   * **Límite de 32 Futbolistas:** Tanto en `DatabaseManager.TransferPlayer` como en `SignFreeAgent`, se verifica que el club receptor no supere la capacidad máxima reglamentaria de 32 futbolistas soportada por el motor de Konami.
   * **Sincronización Relacional:** `TransferPlayer` actualiza de forma indivisible tanto el índice por equipo (`PlayerAssignmentsByTeam`) como la lista maestra (`PlayerAssignments`).
   * **Anti-Doble Fichaje:** `SignFreeAgent` comprueba que el jugador no posea un club activo (`p.ClubId == 0`) ni una entrada previa en `PlayerAssignments`. Si un jugador con club intenta ser fichado como agente libre, el sistema lo deriva automáticamente a un traspaso formal.
4. **Validación Estricta en Creación de Nuevos Futbolistas (Alto 20):**
   * `CreateNewPlayerAction` rechaza nombres vacíos mediante advertencia modal interactiva en vez de crear entradas genéricas anónimas.
   * Manejo seguro de valores nulos en el selector de posición principal y verificación de aforo previo en el club asignado, derivando limpiamente a Agente Libre si la plantilla destino ya contaba con 32 integrantes.

---

## 14. SEGURIDAD DE ARCHIVOS, PAQUETES PESLEAGUE, EXTRACTOR CPK Y PORTABILIDAD DE RUTAS (Bloque 3 - Altos 5, 23, 43, 44, 54)

### 14.1. Blindaje Anti Path-Traversal en Extracción de CPK (`CpkExtractor.cs` - Alto 43)
Para imposibilitar que archivos CPK especialmente manipulados o paquetes de terceros escriban archivos fuera de la carpeta de trabajo del usuario:
* **Validación Canónica de Rutas:** Se resuelve `fullTargetDir = Path.GetFullPath(targetFolder)`.
* **Sanitización de Separadores:** Se unifican barras directas e inversas (`entry.FileName.Replace('/', '\')`), resolviendo la ruta absoluta final con `Path.GetFullPath`.
* **Aislamiento Estricto:** Si `outPath` no comienza con `fullTargetDir`, el sistema neutraliza el intento de path traversal descartando la jerarquía relativa (`..\`) y extrayendo el archivo forzosamente en la raíz del directorio destino mediante `Path.GetFileName(entry.FileName)`.

---

### 14.2. Validación de Límites y Parsing Seguro de Tablas @UTF (`CpkExtractor.cs` - Alto 44)
Para evitar bucles infinitos, accesos fuera de rango u agotamiento de memoria ante archivos de juego dañados:
* **Guardas de Cabecera:** Comprobación estricta de tamaño mínimo de bloque (al menos 32 bytes) y validación de firma mágica `@UTF`.
* **Límites Estructurales:** Validación de `nCols <= 256` y `nRows <= 100.000` para rechazar de inmediato datos con campos corruptos o desbordados.
* **Comprobación de Offsets:** Validación de que los desplazamientos `t + rowsOff` y `t + strOff` no excedan la longitud física del bloque `data.Length`.
* **Lectura Segura de Cadenas y Filas:** En la rutina `getStr` y en los bucles de columnas y filas (`for r = 0..nRows`), cada lectura de tipo (bytes, enteros de 16/32/64 bits o punteros de texto) valida que `q + bytesNeeded <= data.Length`, abortando limpiamente si el archivo está truncado.

---

### 14.3. Validación de Manifiestos y Protección Anti Zip-Bomb (`LeaguePackageManager.cs` - Altos 5 y 54)
* **Protección contra Archivos Desproporcionados:** Antes de extraer un paquete `.pesleague`, se verifica que el archivo zip no contenga más de 500 elementos y que la suma total descomprimida no supere los 100 MB. Además, el archivo `manifest.json` se restringe a un tamaño máximo de 5 MB.
* **Clamping Numérico Defensivo (`GetJsonClampedInt`):** Se erradicó el desbordamiento de módulo por casteo directo (`(byte)` o `(ushort)`). Todos los campos del manifiesto (IDs, continente, país, total de equipos, ascensos, descensos, rondas, clasificados, copas vinculadas) se acotan estrictamente a sus rangos válidos del motor antes de instanciar los registros en memoria.
* **Respaldo Preventivo en UI y Manejo de Agotamiento de Ranuras:**
  * Al pulsar "Importar Paquete (.pesleague)", la interfaz solicita confirmación al usuario y genera automáticamente una copia de seguridad con microsegundos de la base de datos completa.
  * Si el ID de competición del paquete colisiona con una liga base o existente, se buscan ranuras libres. Si el rango de 150 a 250 se encuentra totalmente saturado, el importador devuelve un error seguro (`res.Success = false`) antes de clonar o insertar clubes en memoria.

---

### 14.4. Portabilidad Absoluta y Detección Dinámica de Sider (`MainForm.cs` & `SiderLuaGenerator.cs` - Alto 23)
* **Eliminación de Rutas Hardcodeadas:** Se reemplazaron todas las cadenas estáticas fijas (`<carpeta-del-juego>\...`, `<carpeta-del-editor> ligas\...`) por el método universal `GetDetectedSiderDirectory()`.
* **Búsqueda Multi-Entorno:** Detecta automáticamente instalaciones de Sider y SP Football Life en múltiples ubicaciones estándar (rutas de Steam, discos alternativos C:, D:, E:, y directorios relativos al ejecutable del editor).
* **Reporte Transparente:** En `SaveDatabase`, si Sider no está instalado en la máquina, el sistema informa honestamente que la sincronización con LiveCPK y `league_manager.lua` se omitió por ausencia del directorio, sin simular un guardado exitoso en rutas inexistentes.


---

## 15. Unificación Geográfica, Continentes y Calendarios Anuales (Revisión v4.4.0)

En la auditoría técnica integral (Altos 27, 28, 29, 39) se identificaron inconsistencias históricas en los mapeos de identificadores de países, calendarios estacionales Feb-Dic y asignación de continentes entre diferentes subsistemas del motor.

### 15.1. Unificación de Identificadores de País en Calendarios Anuales (`IsCalendarYearCountry` - Alto 28)
* **Corrección de Mapeos de Países:** Se corrigieron los IDs numéricos de países en `DatabaseManager.IsCalendarYearCountry` para alinearlos estrictamente con los registros oficiales de `Country.bin` y la tabla maestra de regiones (`GetDefaultRegionForCountry`):
  * Bolivia: Corregido a `145` (anteriormente `146`).
  * Brasil: Corregido a `146` (anteriormente `147`).
  * Ecuador: Corregido a `151` (anteriormente `150`).
  * China: Corregido a `261` (anteriormente `90`).
  * Japón: Corregido a `262` (anteriormente `101`).
  * Estados Unidos (MLS): Corregido a `95` (anteriormente `160`).
  * México: Corregido a `89` (anteriormente `153`).
* **Impacto en Liga Máster:** Asegura la generación automática y consistente de directivas de temporada anual Feb-Dic (`season <region> 1`) para `fl26joindll.lua` sin solapamientos erróneos de calendario.

---

### 15.2. Corrección de Generador de Nombres Temáticos Regionales (`GetThemedClubNames` - Alto 29)
* **Reorientación de Ramas por País:** Los bloques condicionales de autogeneración de clubes por país en `DatabaseManager.GetThemedClubNames` apuntaban a identificadores no coincidentes:
  * Inglaterra / UK: Corregido de `14` a `204`.
  * España: Corregido de `45` a `236`.
  * Brasil: Corregido de `147` a `146`.
  * Sudamérica / Hispano: Se incorporaron los IDs reales de Bolivia (`145`), Brasil (`146`) y Ecuador (`151`).
* **Garantía Anti-Bucle:** La generación se apoya en `CombineThemedPool`, que implementa un producto cartesiano ordenado ($N 	imes M$) con paso coprimo y sufijo numérico progresivo de respaldo ante sobrecupos, eliminando por completo bucles infinitos en ligas de hasta 60 equipos.

---

### 15.3. Clasificación de Continentes y Fallbacks Seguros (`GetTeamContinent` - Alto 39)
* **Detección Forense de CONCACAF / Norteamérica:** En la base de datos de PES, países de CONCACAF (México `89`, USA `95`, Canadá `77`, Costa Rica `79`, Honduras `84`, Jamaica `86`, Panamá `88`, etc.) comparten internamente la marca `Continent = 3` con Asia. `GetTeamContinent` ahora intercepta de forma explícita estos IDs y los clasifica como Norteamérica (`5`).
* **Saneamiento de Rangos de Respaldo:** Se eliminó el rango genérico `1..50 -> Asia` que causaba colisiones con naciones europeas de ID bajo. El fallback por rangos numéricos ahora delimita claramente:
  * `190..290`: Europa (`2`).
  * `144..165`: Sudamérica (`4`).
  * `Resto / No determinado`: Otros (`1`).

---

### 15.4. Marcado de Cambios en Mutación de País de Liga (`SetLeagueCountry` - Alto 27)
* **Señalización de Estado Sucio:** Al reasignar el país o la región de una competición mediante `DatabaseManager.SetLeagueCountry`, se invoca de forma obligatoria `HasUnsavedChanges = true` además de actualizar el grupo regional y marcar `comp.IsModified = true`, garantizando que la interfaz reconozca las modificaciones pendientes y prevenga pérdidas de datos al guardar.


---

## 16. Integridad de Reglas de Competición, Jerarquías y Mapeos Relacionales (Revisión v4.5.0)

En la auditoría exhaustiva de seguridad (Altos 8, 9, 10, 11, 35, 36, 37, 38, 40) se identificaron y corrigieron los riesgos de mutación atómica en memoria, colisiones de IDs relacionales y corrupción silenciosa de tablas entre ligas:

### 16.1. Blindaje Atómico en Edición de Competiciones (`ApplyCompetitionChanges` - Altos 8 y 11)
* **Protección Excepcional:** Todo el bloque de edición y persistencia en memoria se ejecuta dentro de un marco de contención transaccional (`try / catch`). En caso de error inesperado, el editor reporta el diagnóstico exacto mediante un cuadro modal y previene estados corruptos en la UI.
* **Manejo Seguro de Emblemas:** La eliminación de logos antiguos (`db.DeleteCompetitionEmblem`) se ejecuta protegida por control de excepciones para evitar que fallos de acceso a LiveCPK interrumpan la actualización de la base de datos.
* **Reconciliación de Capacidad (`TotalTeams` vs `CompetitionEntry.bin`):** Se erradicó la inconsistencia entre el objetivo seleccionado en el desplegable y la cantidad real de clubes inscritos. Si existe discrepancia, el sistema notifica al usuario y ofrece sincronizar automáticamente la capacidad oficial del reglamento (`reg.TotalTeams`) con el recuento real de participantes, garantizando calendarios Round-Robin válidos en el motor del juego.

---

### 16.2. Corrección de Jerarquía de Divisiones y Re-Sellado Binario (Altos 9 y 10)
* **Validación Obligatoria de Liga Padre:** Si una competición se define como subdivisión subordinada, la interfaz exige la selección explícita de una liga padre válida, impidiendo que el guardado omita silenciosamente la configuración jerárquica.
* **Preservación del Espacio de Nombres de IDs:** Se eliminó el fallback que inyectaba `CompetitionId` en el campo `IdDivision1` de la regulación. La vinculación se resuelve estrictamente mediante `RegulationRecord.Id`, manteniendo 100% de coherencia con las estructuras internas de Konami.
* **Re-Sellado Limpio de Búferes:** Se suprimió la copia manual de bytes en el offset cero del padre (`BitConverter.GetBytes(parentReg.IdDivision2).CopyTo(...)`), reemplazándola por la serialización nativa formal `parentReg.RawBytes = parentReg.ToBytes()`.

---

### 16.3. Preservación de Asignaciones en Equipos Clásicos (`EnsureClassicTeamsCompetition` - Alto 35)
* **Protección de Clubes Existentes:** Al construir o validar la competición `CLASSIC_TEAMS` (CompID 53), el motor ya no extrae agresivamente a `European Classics` (2703) ni `World Classics` (2704) si el usuario los había integrado intencionalmente en ligas personalizadas o torneos temáticos, ni sustituye plazas con clubes arbitrarios sin consentimiento. Solo son agregados a la categoría de clásicos si se encuentran en estado libre.

---

### 16.4. Validaciones Forenses al Crear Ligas (`CreateNewLeague` - Altos 36, 37 y 38)
* **Validación Integral de `teamIds` (Alto 37):** Se incorporaron verificaciones preventivas obligatorias: validación de lista no vacía, coincidencia estricta `teamIds.Count == totalTeams`, detección de identificadores de equipos duplicados y comprobación de existencia física en `Team.bin`.
* **Desvinculación Preventiva Multilíga:** Cada club asignado a la nueva liga se desvincula automáticamente de cualquier liga doméstica previa mediante `RemoveTeamFromDomesticLeagues(tid)`, previniendo colisiones de fixture y cuelgues al armar el calendario de Liga Máster.
* **Protección de Enlace D2 en Liga Padre (Alto 36):** La propiedad `parentReg.IdDivision2` solo se actualiza si la liga padre no contaba previamente con otra división subordinada, evitando romper enlaces existentes en estructuras jerárquicas preexistentes.
* **Asignación Segura de IDs de Entrada (Alto 38):** Se reemplazó la deducción local por vecindad por un algoritmo global que consulta `HashSet<ushort> usedEntryIds` en todo `CompetitionEntry.bin`, seleccionando secuencias contiguas libres con salvaguarda contra desbordamientos aritméticos (`ushort.MaxValue`).

---

### 16.5. Prevención de Colisiones en Creador de Estadios (`CreateNewStadium` - Alto 40)
* **Comprobación de ID Explícito:** Si se suministra un identificador específico de estadio, se comprueba exhaustivamente contra `StadiumsById` y `Stadiums`, lanzando una excepción clara si el ID ya se encuentra en uso.
* **Generación Concurrente Segura:** La asignación automática de IDs busca el siguiente hueco numérico disponible dentro de la tabla global con límite en 65534, previniendo desbordamientos y punteros duplicados en el diccionario en memoria.


---

## 17. Integridad de Modelos, Serialización y Estructuras Binarias (Revisión v4.6.0)

En la auditoría forense de modelos de base de datos y serializadores de bajo nivel (Altos 30, 31, 52, 53) se resolvieron vulnerabilidades de desbordamiento de búfer, pérdidas de traducciones nativas y fallos de parseo:

### 17.1. Guardas de Longitud en Archivos Principales (`DatabaseManager.cs` - Alto 31)
* **Protección contra `IndexOutOfRangeException`:** En versiones previas, los bucles de descompresión y lectura de los 4 archivos obligatorios (`Competition.bin`, `CompetitionRegulation.bin`, `Team.bin`, `CompetitionEntry.bin`) iteraban con `i < length`, lo que ante archivos con bytes residuales o truncados intentaba instanciar el registro fuera del búfer.
* **Condición Canónica:** Se ajustaron los 4 bucles a la condición estricta `i + RecordSize <= length`, garantizando que solo se procesen bloques binarios físicamente completos e íntegros.

---

### 17.2. Telemetría y Registro de Excepciones en Archivos Opcionales (`DatabaseManager.cs` - Alto 30)
* **Fin de las Excepciones Silenciadas:** Se reemplazaron todos los bloques `catch { }` vacíos en la carga de los 8 archivos complementarios (`Country.bin`, `Coach.bin`, `Stadium.bin`, `Derby.bin`, `PlayerAssignment.bin`, `Player.bin`, `Tactics.bin`, `TacticsFormation.bin`).
* **Colección Centralizada de Advertencias (`LoadWarnings`):** Cada fallo de lectura o descompresión se almacena en la propiedad pública `LoadWarnings` y se emite en la consola de depuración.
* **Feedback Inmediato en la GUI (`MainForm.cs`):** Al finalizar la carga de la base de datos, si algún archivo secundario falló, el editor muestra un diálogo informativo detallado al usuario indicando con precisión qué archivos no pudieron cargarse sin bloquear la apertura de la base de datos principal.

---

### 17.3. Preservación de Traducciones Multilingües (`RegulationRecord.ToBytes` - Alto 52)
* **Protección de Nombres Nativos Konami:** Anteriormente, cualquier modificación en una competición existente (`IsModified == true`) provocaba que las 20 ranuras de idioma (japonés, alemán, francés, italiano, portugués, ruso, turco, árabe, etc.) fueran sobrescritas en bloque con el nombre primario en español o inglés.
* **Comportamiento Diferenciado:**
  * **Registros Nuevos (`IsNew == true`):** Se pueblan las 20 ranuras con el nombre primario para asegurar que ligas recién creadas no aparezcan en blanco en ningún idioma o región del juego.
  * **Competiciones Existentes Editadas (`IsModified && !IsNew`):** Se actualizan únicamente las ranuras correspondientes a los idiomas efectivamente configurados en la interfaz (Español en offset 825, Latam en 250, Inglés en 480/2237 y Nombre ASCII en 1055), preservando el 100% de las traducciones oficiales en las restantes 17 ranuras.

---

### 17.4. Null-Guards Defensivos en Serialización (`PesDbModel.cs` - Alto 53)
* **Prevención de `NullReferenceException`:** Los métodos `ToBytes()` en `CompetitionRecord`, `RegulationRecord` y `EntryRecord` ejecutaban directamente `(byte[])RawBytes.Clone()`, provocando crasheos inmediatos si el registro había sido instanciado programáticamente sin asignar un búfer inicial.
* **Inicialización Segura:** Los tres serializadores verifican `RawBytes != null && RawBytes.Length >= RecordSize`, recurriendo a una asignación limpia `new byte[RecordSize]` en caso de búfer nulo o incompleto.


---

## 18. Auditoría Documental, Concordancia y Reconciliación de Especificaciones (Revisión v4.7.0)

En la auditoría técnica exhaustiva de documentación (Altos 58, 59, 60, 61, 62, 63) se subsanaron discrepancias de transcripción, ambigüedades de versión y cálculos aritméticos en las especificaciones del proyecto:

### 18.1. Reconciliación del Tamaño Físico del Ejecutable (Alto 58)
* **Alineación de Cifras:** Se corrigió la cifra discordante en la cabecera de la documentación (anteriormente 37.198.848 bytes, que correspondía a una medida comprimida parcial inferior al propio segmento de código).
* **Especificación Real:** Se unificó el tamaño oficial a **458.739.200 bytes** (437.49 MB) correspondiente al ejecutable desempacado de PES 2021 con sección `.text` de **38.983.680 bytes** (`0x0252D800`), en total consonancia con `docs/ingenieria_inversa_exe.md`.

---

### 18.2. Clarificación de Índices Vanilla y Gatekeeper Sudamericano (Alto 59)
* **Distinción entre Ligas Domésticas y Exhibición:** Se detalló la distinción entre las 6 ligas domésticas sudamericanas oficiales (índices 18 a 23: Argentina, Brasil A, Brasil B, Chile, Colombia, PLA) filtradas por el gatekeeper `0x141F13143` (`cmp r14d, 0x18`), y la entrada 24 ('Otros Equipos Latinoamérica'), que no pasa por dicha compuerta.
* **Explicación del Fallo Histórico:** Se documentó que desplazar los índices empujaba a la Liga PLA al índice 24, provocando su descarte en el motor.

---

### 18.3. Reconciliación Histórica: De Append-30 a la Tabla Maestra v4 (Altos 60 y 61)
* **Clarificación Metodológica:** Se estableció explícitamente en la Sección 2 la distinción entre la solución de prototipado temprano v3.3.0 (Append a Entrada 30, limitada a una sola liga extra en el carrusel de amistosos) y la arquitectura vigente definitiva **v4.x**.
* **Arquitectura Vigente (Matozanato):** Se documentó que la versión final no parchea límites de 30 o 31 entradas en `0x141F00850`, sino que reubica la tabla estática completa (`0x1434FDF00`) a memoria dinámica con `VirtualAlloc` (165+ filas de 264 bytes), repunta las tres instrucciones LEA y actualiza los 4 contadores de filas del motor, permitiendo el despliegue concurrente de hasta 250 competiciones en Kick Off.

---

### 18.4. Corrección Aritmética de `Player.bin` (Alto 62)
* **Desglose de Búfer:** En `docs/estructura_binaria.md`, se corrigió la operación aritmética de validación:
  $$	ext{Payload Útil} = 5.526 	ext{ futbolistas} 	imes 312 	ext{ bytes/registro} = 1.724.112 	ext{ bytes}$$
* **Bytes de Relleno:** Se aclaró que los 1.724.196 bytes totales en disco incluyen 84 bytes de relleno y alineación final de sector (`1.724.112 + 84 = 1.724.196`).

---

### 18.5. Corrección de Límite Off-By-One en Modo Editar (Alto 63)
* **Clarificación de Rangos de Clubes:** Se aclaró la cobertura exacta entre el archivo base y el contenedor extendido:
  * `EDIT00000000`: Almacena exactamente los primeros **750 clubes** (índices 0 a 749).
  * `EDIT00000000.fl26x`: Intercepta y serializa todos los clubes a partir del **índice 750** (el club 751º en adelante, hasta 2.048 clubes).
* Se eliminó la ambigüedad que excluía erróneamente al club ubicado en la ranura 749.
