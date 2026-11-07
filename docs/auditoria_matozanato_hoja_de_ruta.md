# AUDITORÍA TÉCNICA Y HOJA DE RUTA MODULAR: PES 2021 LEAGUE EXPANSION
## Integración de Conocimientos: EXEVAZ27 + Matozanato + Agustinioquino
**Proyecto:** PES 2021 Editor Pro (PES2021EditorPro.exe)  
**Autor:** EXEVAZ27  
**Fecha:** Octubre 2026  
**Documento:** Hoja de Ruta Modular, Créditos y Lista de Verificación (Checklist)  

---

## 🏆 Créditos y Agradecimientos Especiales a la Comunidad

Este software y su ecosistema de ingeniería inversa integran descubrimientos y desarrollos de código abierto de destacados desarrolladores:

1. **Matozanato (`FootballLife-new-leagues`)**
   * **Repositorio GitHub:** https://github.com/Matozanato/FootballLife-new-leagues
   * **Aportes clave:** Descubrimiento de la tabla estática de parámetros (`0x1434FDF00`), ganchos de banderas y orden de menús, calendario y fechas de Liga Máster (Date Spread), inscripción anual (`fl26join.dll`), ascensos/descensos multidivisión (`fl26chain.dll`) y parches de estabilidad (Null-Guards).

2. **Agustinioquino (`Pes-Editor-Rebuilt`)**
   * **Repositorio GitHub:** https://github.com/Agustinioquino/Pes-Editor-Rebuilt
   * **Aportes clave:** Algoritmo de desencriptación/encriptación completa del archivo `EDIT00000000` con Mersenne Twister MT19937 y claves de Konami, mapeo a nivel de bits de la estructura de guardado, lector directo de archivos `.cpk` y descompresión nativa CRILAYLA, volcado de memoria `edit21_db.lua`, y sistema `kitpos.lua` para posicionamiento libre de dorsales y soporte de minúsculas/tildes en camisetas.

---

> [!NOTE]
> Este documento organiza todas las funciones descubiertas en la auditoría de ambos repositorios más nuestras propias arquitecturas previas. Cada tarea es modular y puede abordarse de forma independiente sin importar el orden estricto, permitiendo avanzar paso a paso sin olvidar ninguna característica técnica.

---

## 1. Matriz de Módulos y Estado de Implementación

| # | Módulo / Característica | Estado | Origen en la Comunidad | Destino en Nuestro Proyecto |
| :---: | :--- | :---: | :--- | :--- |
| **01** | [Expansión de Menú y Modo Editar](#01-expansión-de-menú-y-modo-editar) | `COMPLETADO` | Matozanato (`fl26comptab.lua`, `fl26editlist.lua`) | `league_manager.lua` (v4.0.0) |
| **02** | [Ganchos Anti-Cuelgues (Null-Guards)](#02-ganchos-anti-cuelgues-null-guards) | `COMPLETADO` | Matozanato (`sider/fl26nullguard*.lua`) | `SiderLuaGenerator.cs` / `league_manager.lua` |
| **03** | [Expansor de Países / Regiones (128 Regiones)](#03-expansor-de-países--regiones-128-regiones) | `COMPLETADO` | Matozanato (`sider/experimental/fl26reg128.lua`) | `league_manager.lua` |
| **04** | [Ampliación de Slots en Selección de Equipos (250 Slots)](#04-ampliación-de-slots-en-selección-de-equipos-250-slots) | `COMPLETADO` | Matozanato (`fl26slots250.lua` + `fl26slot7.lua`) | `SiderLuaGenerator.cs` / `league_manager.lua` |
| **05** | [Sistema de Fechas para Liga Máster (Date Spread Stub)](#05-sistema-de-fechas-para-liga-máster-date-spread-stub) | `COMPLETADO` | Matozanato (`fl26caps.lua` + `fl26joindll.lua`) | `SiderLuaGenerator.cs` / Sider |
| **06** | [Inscripción y Cierre Anual en Liga Máster (`pes_join`)](#06-inscripción-y-cierre-anual-en-liga-máster-pes_join) | `COMPLETADO` | Matozanato (`fl26join.dll` + `fl26world.txt`) | `SiderLuaGenerator.cs` / Sider |
| **07** | [Pirámides de 3 a 7 Divisiones (`pes_chain`)](#07-pirámides-de-3-a-7-divisiones-pes_chain) | `COMPLETADO` | Matozanato (`fl26chain.dll`, `fl26rank.lua`, `fl26deeprank.lua`) | `DatabaseManager.cs`, `SiderLuaGenerator.cs` |
| **08** | [Expansión de Capacidad de Memoria RAM (2.400 Clubes)](#08-expansión-de-capacidad-de-memoria-ram-2400-clubes) | `COMPLETADO` | Matozanato (`fl26caps.lua`) | `SiderLuaGenerator.cs` / Sider |
| **09** | [Optimización Anti-Congelamiento 31 de Enero (`pes_compcache`)](#09-optimización-anti-congelamiento-31-de-enero-pes_compcache) | `COMPLETADO` | Matozanato (`fl26compcache.dll` + `fl26compcache.lua`) | `SiderLuaGenerator.cs` / Sider |
| **10** | [Criptografía y Edición de `EDIT00000000` (`PesCrypt`)](#10-criptografía-y-edición-de-edit00000000-pescrypt) | `COMPLETADO` | Agustinioquino (`js/pescrypt.js`, `js/pesdata.js`) | `PesCrypt.cs` / C# Tool |
| **11** | [Dorsales Libres y Minúsculas/Tildes en Camisetas (`KitPos`)](#11-dorsales-libres-y-minúsculastildes-en-camisetas-kitpos) | `PENDIENTE` | Agustinioquino (`sider/kitpos.lua`) | `league_manager.lua` / Sider |
| **12** | [Lector Directo de CPK y Descompresor CRILAYLA](#12-lector-directo-de-cpk-y-descompresor-crilayla) | `COMPLETADO` | Agustinioquino (`js/gamedb.js`) | `CpkExtractor.cs` (v4.2.0) |
| **13** | [Persistencia de Clubes Extendidos en Modo Editar (`fl26edit`)](#13-persistencia-de-clubes-extendidos-en-modo-editar-fl26edit--fl26editlist) | `COMPLETADO` | Matozanato (`fl26edit.dll`, `fl26editlist.lua`) | `fl26edit.dll` / `PesCrypt.cs` / Sider |
| **14** | [Formato Suizo UEFA 2024 y Copas Continentales](#14-formato-suizo-uefa-2024-y-copas-continentales) | `PENDIENTE` | Matozanato (`tools/native/fl26swiss.c`) | Módulo Opcional |
| **15** | [Ecosistema Sudamericano Nativo (Copa Argentina, Promedios)](#15-ecosistema-sudamericano-nativo-copa-argentina-promedios) | `PENDIENTE` | Desarrollo Propio (Exclusivo) | C# Tool + `league_manager` |
| **16** | [Diseñador Visual de Pirámides en la GUI C#](#16-diseñador-visual-de-pirámides-en-la-gui-c) | `COMPLETADO` | Desarrollo Propio (Exclusivo) | `DatabaseManager.cs`, `MainForm.cs` (Tab 2) |
| **17** | [Escáner Inteligente de Colisiones de IDs](#17-escáner-inteligente-de-colisiones-de-ids) | `COMPLETADO` | Desarrollo Propio (Exclusivo) | `DatabaseManager.cs` |
| **18** | [Gestión Integral de Equipos Clásicos (European & World Classics)](#18-gestión-integral-de-equipos-clásicos) | `COMPLETADO` | Desarrollo Propio (Exclusivo) | `DatabaseManager.cs`, `MainForm.cs`, `SiderLuaGenerator.cs` |
| **19** | [Eliminación de Ligas y Competiciones Limpias](#19-eliminación-de-ligas-y-competiciones-limpias) | `COMPLETADO` | Desarrollo Propio (Exclusivo) | `DatabaseManager.cs`, `MainForm.cs` |
| **20** | [Gestión Integral de "Otros Equipos" (Europa, Latam, Asia)](#20-gestión-integral-de-otros-equipos) | `COMPLETADO` | Desarrollo Propio (Exclusivo) | `DatabaseManager.cs`, `MainForm.cs` |
| **21** | [Validación de Paridad de Fixtures (PES 2021 Safe)](#21-validación-de-paridad-de-fixtures) | `COMPLETADO` | Desarrollo Propio (Exclusivo) | `DatabaseManager.cs`, `MainForm.cs` |
| **22** | [Paquetes Modulares de Ligas (.pesleague) y Remapeo Relacional](#22-paquetes-modulares-de-ligas-pesleague-y-remapeo-relacional) | `COMPLETADO` | Desarrollo Propio (Exclusivo) | `LeaguePackageManager.cs`, `MainForm.cs` |
| **23** | [Motor Forense de Jugadores y Fichas Fox Engine (`Player.bin`)](#23-motor-forense-de-jugadores-y-fichas-fox-engine-playerbin) | `COMPLETADO` | Agustinioquino / Fox Engine | `PlayerBitHelper.cs`, `MinifaceProvider.cs`, `PlayerCardControl.cs` |

---

## Detalle de Módulos y Tareas de Trabajo

---

### 01. Expansión de Menú y Modo Editar
- [x] **Estado:** COMPLETADO (v4.1.0)
- **Descripción:** Expansión en RAM de la tabla de parámetros estática de Konami (`0x1434FDF00`, 165 filas x 264 bytes) mediante `VirtualAlloc`.
- **Ganchos y Módulos Implementados:**
  - Repuntado de las 3 instrucciones LEA (`0x1414FDBDA`, `0x1414FDD71`, `0x1414FDE07`).
  - Actualización dinámica de los 4 contadores de filas en el motor (`0x1414FDC4A`, `0x1414FDD60`, `0x1414FDE1B`, `0x1414FE1E0`).
  - **Inferencia Automática de Banderas (v4.1.0):** Decodificación nativa de `Country.bin` (stride 1420 bytes, nombre español offset +148, inglés +288) y resolución heredada desde la liga padre o los clubes integrantes (`0x140C950D0`).
  - **Orden Jerárquico Dinámico en Kick Off (v4.1.0):** Resolución de slot padre nativo o customizado y algoritmo recursivo `follows(s, a)` que inserta subdivisiones anidadas (`Tier 1 -> Tier 2 -> Tier 3 -> Tier 4`) inmediatamente debajo de su división padre sin colisiones (`0x140EAD990` y `0x1427D5754`).
  - **Nomenclatura Jerárquica y Cálculo de Tier (v4.1.0):** Detección dinámica de nivel en árbol genealógico (`1ª Div`, `2ª Div`, `3ª Div / Subliga`, etc.) tanto en la interfaz C# como en el explorador de competiciones.
  - Inyección en el vector de Modo Editar (`0x141EE7086` vía `0x14047BE40`).
- **Archivos:** [`league_manager.lua`](file:///E:/Juegos/eFootball%20PES%202021/sider/modules/league_manager.lua), [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`DatabaseManager.cs`](file:///E:/Editor%20ligas/src/DatabaseManager.cs), [`MainForm.cs`](file:///E:/Editor%20ligas/src/MainForm.cs).

---

### 02. Ganchos Anti-Cuelgues (Null-Guards)
- [x] **Estado:** COMPLETADO
- **Descripción:** Portados e integrados los 11 interceptores de punteros nulos de Matozanato para evitar que el juego crashee en situaciones límite expuestas por bases de datos personalizadas o en avances de temporada de Liga Máster.
- **Lista de Parches Integrados:**
  - [x] **Guard 1 (`0x141FEA5BA`):** Evita cuelgues en el saque inicial de amistosos con clubes nuevos (trampolín en cueva `.trace` `0x14252e800`).
  - [x] **Guard 2 (`0x140FC9238`):** Evita cuelgues durante el avance de días a mitad de temporada (trampolín en `0x14252e880`).
  - [x] **Guard 4 (`0x1415720D0`):** Protege el recorrido del array de directores técnicos (coaches) (trampolín en `0x14252e840`).
  - [x] **Guard 5 (`0x1414C66C0`):** Protege el pase de alineación automática de la IA tras la fecha 1 (trampolín en `0x14252e860`).
  - [x] **Guard 7 (`0x14128A3A3`):** Protege la lectura del primer elemento en tablas de plantilla de equipos (trampolín en `0x14252e8a0`).
  - [x] **Guard 8 (`0x1413236E1`):** Corrige el clásico error de índice negativo (`rank < 0` $\rightarrow$ clampa a 0) cuando un equipo no tiene posición previa en tabla (trampolín en `0x14252e8c0`).
  - [x] **Guard 9 (`0x140CD6A18`):** Protege la lectura de la lista de calendarios al cargar partidas y en reuniones de directiva (trampolín en `0x14252e8e0`).
  - [x] **Guard 10 (`0x140CAFC35` / `0x140CAFDAC`):** Corta bucles infinitos en calendarios combinados (febrero-diciembre).
  - [x] **Guard 11 (`0x140F3258E`):** Salta jugadores inexistentes al cargar partidas viejas de Liga Máster.
- **Implementación:** 
  - Incluidos los 11 guards directamente dentro de `install_nullguards()` en [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs) y [`league_manager.lua`](file:///E:/Juegos/eFootball%20PES%202021/sider/modules/league_manager.lua).
  - Verificación estricta de bytes originales antes de parchear y soporte de "ya aplicado" para coexistir sin conflictos.
  - Opción de despliegue automático de archivo dedicado `fl26guards.lua` en Sider.

---

### 03. Expansor de Países / Regiones (128 Regiones)
- [x] **Estado:** COMPLETADO
- **Descripción:** PES 2021 de fábrica solo permite 29 países/regiones en los menús; cualquier ID $\ge 29$ se trunca o toma el encabezado de otro país.
- **Detalle técnico e Implementación:**
  - [x] Decodificación y codificación a nivel de bits en `CompetitionRecord` (`PesDbModel.cs`): decodifica 7 bits (0..127) mapeando byte +3 bits 3..7 como bits 0..4, bit 0 como bit 5, y bit 1 como bit 6, preservando el bit 2 de Konami. 100% de coincidencia bidireccional en los 128 valores.
  - [x] `REG128_PATCHES` (44 parches): Inyectados en `fl26reg128.lua` y `league_manager.lua`. Reubica la lectura en el offset `+0x2fe` y neutraliza las comparaciones hardcodeadas de región 29 (`0x141575f1d`, `0x1415773af`, `0x141577414`).
  - [x] `fl26regfast.lua` (3 parches): Acelerador de búsquedas lineales en `0x141543510`, reduciendo en 65% los tiempos de carga de Liga Máster.
  - [x] `fl26regs600.lua` (4 parches): Expande el recuento de regulaciones en memoria de 300 a 600.
  - [x] Mapeo nativo de países sudamericanos y del resto del mundo a regiones extendidas (Bolivia 29, Ecuador 30, Paraguay 31, Venezuela 32, México 33, USA 34, Canadá 35, etc.).
- **Beneficio:** Cualquier país del mundo tiene su propia sección y bandera en los menús sin reemplazar a ningún país existente.
- **Archivos:** [`PesDbModel.cs`](file:///E:/Editor%20ligas/src/PesDbModel.cs), [`DatabaseManager.cs`](file:///E:/Editor%20ligas/src/DatabaseManager.cs), [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`fl26reg128.lua`](file:///E:/Editor%20ligas/fl26reg128.lua).

---

### 04. Ampliación de Slots en Selección de Equipos (250 Slots)
- [x] **Estado:** COMPLETADO
- **Descripción:** Por defecto, Konami tiene 84 slots vanilla y una tabla fija de 123 slots máximos para la pantalla de selección de equipos.
- **Detalle técnico e Implementación:**
  - [x] `SLOTS250_PATCHES` (35 parches): Reserva página en `0x163400000`, inicializa tabla de capacidades (324 entradas por slot, 2600 en slots 79/80, 750 en 0..5 y 69..78, centinela 123 = 0), y reescribe los 5 accesores del motor (`0x1414d6290..0x1414d637f`).
  - [x] `SLOT7_PATCHES` (59 parches): Reserva página en `0x163c00000`, instala trampolín `id2slot` y actualiza los 58 lectores para que utilicen una máscara `0xFF` en lugar de `0x7F` en el byte `+0x41c`, reubicando el bit de bandera a `+0x41a`.
- **Beneficio:** Permite registrar hasta 250 competiciones en slots `124..249` sin que `slot >= 128` colisione ni se enmascare a `slot = 7`.
- **Archivos:** [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`fl26slots250.lua`](file:///E:/Editor%20ligas/fl26slots250.lua), [`fl26slot7.lua`](file:///E:/Editor%20ligas/fl26slot7.lua).

---

### 05. Sistema de Fechas para Liga Máster (Date Spread Stub)
- [x] **Estado:** COMPLETADO
- **Descripción:** Resolver el motivo por el cual una liga nueva no genera partidos en Liga Máster (0 partidos disputados).
- **Detalle técnico e Implementación:**
  - [x] `fl26caps.lua` (3.534 parches): Expande la memoria a 8.000 fixtures y reasigna los punteros de fechas.
  - [x] `fl26joindll.lua` y `fl26join.dll` (DLL nativa x64 AMD64): Asocian automáticamente las ligas declaradas en `fl26world.txt` con la generación de fixtures de 38 jornadas completas en Liga Máster, asignando fechas repartidas a lo largo de la temporada.
- **Beneficio:** Las ligas creadas juegan temporadas completas con fixtures reales en Liga Máster sin quedar en 0 partidos disputados.
- **Archivos:** [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`fl26caps.lua`](file:///E:/Editor%20ligas/fl26caps.lua), [`fl26joindll.lua`](file:///E:/Editor%20ligas/fl26joindll.lua), [`fl26join.dll`](file:///E:/Editor%20ligas/fl26join.dll), `fl26world.txt`.

---

### 06. Inscripción y Cierre Anual en Liga Máster (`pes_join`)
- [x] **Estado:** COMPLETADO
- **Descripción:** Inscribir automáticamente las nuevas ligas al inicio de la Liga Máster y asegurar que al llegar al 31 de julio (o diciembre) la temporada se cierre correctamente y se limpie la tabla para el año siguiente.
- **Detalle técnico e Implementación:**
  - [x] `fl26join.dll` intercepta el salto de registro de temporada (`register_all` en `0x141343bf0`), inyectando los IDs de las nuevas ligas desde `fl26world.txt`.
  - [x] `fl26join.dll` intercepta el bucle de desmontaje y reinicio anual de temporada (*teardown & rollover* en julio / fin de año), archivando el historial y reiniciando las tablas a 0 puntos para avanzar de año de forma infinita sin bloqueos.
  - [x] `fl26compcache.dll` y `fl26compcache.lua`: Resuelven el congelamiento del 31 de enero en Liga Máster al cachear las tablas de competiciones.
- **Beneficio:** Avance infinito y autónomo año tras año en Liga Máster con reinicio real de tablas.
- **Archivos:** [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`fl26join.dll`](file:///E:/Editor%20ligas/fl26join.dll), [`fl26joindll.lua`](file:///E:/Editor%20ligas/fl26joindll.lua), [`fl26compcache.dll`](file:///E:/Editor%20ligas/fl26compcache.dll), [`fl26compcache.lua`](file:///E:/Editor%20ligas/fl26compcache.lua).

---

### 07. Pirámides de 3 a 7 Divisiones (`pes_chain`)
- [x] **Estado:** COMPLETADO
- **Descripción:** Habilitar ascensos y descensos automáticos entre más de 2 divisiones (D1 <-> D2 <-> D3 <-> D4).
- **Detalle técnico e Implementación:**
  - [x] `fl26rank.lua` (151 parches verificados al 100% en `PES2021.exe`): Amplía el campo de rango de división en el motor de 2 bits a 3 bits (offset `+0x304`), desbloqueando de 3 a 7 divisiones completas (D1..D7).
  - [x] `fl26deeprank.lua` (2 parches): Reemplaza la comprobación estricta de `rank == 2` en las compuertas de descenso (`0x141510607`, `0x1415143ec`) por `rank >= 2`, permitiendo descender desde cualquier categoría inferior a la siguiente división.
  - [x] `fl26chain.dll` y `fl26chain.lua` (DLL nativa x64 AMD64): Al final de la temporada, inspecciona las posiciones finales mediante las funciones internas de Konami, desciende los $N$ últimos equipos de la categoría intermedia y asciende los $N$ primeros de la división inferior, reescribiendo ambas listas con el escritor nativo del juego.
  - [x] Generador unificado de `fl26world.txt`: Sintetiza en tiempo real los atributos `tier`, `above`, `promote`, `demote`, `clubs`, `slot`, `region` y `country` de cada liga creada en la base de datos y la jerarquía de la pirámide.
- **Beneficio:** Pirámides multinivel 100% operativas en el juego (ej. Primera División <-> Primera B Nacional <-> Primera B Metropolitana) con ascensos y descensos automáticos y persistentes cada año.
- **Archivos:** [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`MainForm.cs`](file:///E:/Editor%20ligas/src/MainForm.cs), [`fl26chain.dll`](file:///E:/Editor%20ligas/fl26chain.dll), [`fl26chain.lua`](file:///E:/Editor%20ligas/fl26chain.lua), [`fl26rank.lua`](file:///E:/Editor%20ligas/fl26rank.lua), [`fl26deeprank.lua`](file:///E:/Editor%20ligas/fl26deeprank.lua).

---

### 08. Expansión de Capacidad de Memoria RAM (2.400 Clubes)
- [x] **Estado:** COMPLETADO
- **Descripción:** Elevar el límite global del juego de 750 a 2.400 clubes y de 30.000 a 61.905 jugadores.
- **Detalle técnico e Implementación:**
  - [x] `fl26caps.lua` (3.534 parches de memoria en tiempo real, 506 KB): Reasigna el edit block completo de Konami a páginas de memoria extendidas con `VirtualAlloc`, reescribe 3.534 comprobaciones de límites para permitir hasta 2.400 clubes, 8.000 fixtures de Liga Máster y 61.905 jugadores sin desbordar buffers.
- **Beneficio:** Se elimina el tope de equipos del juego; se pueden añadir cientos de clubes y ligas sin sacrificar contenido previo.
- **Archivos:** [`fl26caps.lua`](file:///E:/Editor%20ligas/fl26caps.lua), [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs).

---

### 09. Optimización Anti-Congelamiento 31 de Enero (`pes_compcache`)
- [x] **Estado:** COMPLETADO
- **Descripción:** Eliminar la pantalla de carga de 20 a 40 minutos que ocurre el 31 de enero en Liga Máster al tener muchos clubes y jugadores.
- **Detalle técnico e Implementación:**
  - [x] En el último día del mercado de fichajes de invierno, Konami evalúa cada club frente a cada liga consultando el ID de competición repetidamente para cada jugador.
  - [x] Las librerías `fl26compcache.dll` (DLL nativa x64 AMD64) y `fl26compcache.lua` cachean el resultado durante el barrido, reduciendo el proceso a 0.2 segundos.
  - [x] Incluido y desplegado automáticamente en `SiderLuaGenerator.DeploySiderModules()` en la posición 7 de carga de `sider.ini`.
- **Beneficio:** Avance de fecha instantáneo y fluido durante todo el mercado de pases de invierno sin cuelgues.
- **Archivos:** [`fl26compcache.dll`](file:///E:/Editor%20ligas/fl26compcache.dll), [`fl26compcache.lua`](file:///E:/Editor%20ligas/fl26compcache.lua), [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs).

---

### 10. Criptografía y Edición de EDIT00000000 (PesCrypt)
- [x] **Estado:** COMPLETADO
- **Descripción:** Motor criptográfico in-process en C# (.NET 4.8) para desencriptar, editar y re-encriptar archivos `EDIT00000000` de PES 2021 y PES 2020 con coincidencia exacta 100% bit a bit.
- **Implementación Forense:**
  - [x] Generador de números pseudoaleatorios Mersenne Twister MT19937 de 32 bits alimentado con clave de 64 bytes (`0x9061d8...` para PES 2021).
  - [x] Cifrador de flujo (`CryptStream`) con rotaciones circulares de 32 bits (`rol`/`ror`) y cabecera mezclada (`CryptHeader` de 320 bytes).
  - [x] Extracción y montaje de payload completo `data.dat` (~10.99 MB), cabecera `header.dat` (208 bytes), icono de partida `logo.png`, descripción y SteamID/SID.
  - [x] **Desbloqueador de Option Files (Anti-Bloqueo):** Reasigna en 1 clic el SteamID/SID de la partida al del usuario local de Windows, eliminando permanentemente el error de Konami *"Los datos pertenecen a otro usuario y no pueden cargarse"*.
  - [x] **Seguridad y Respaldos:** Creación automática de copia de seguridad `.bak` antes de sobrescribir, exportador e importador directo de `data.dat` sin encriptar.
  - [x] **Pestaña Dedicada en GUI C#:** Pestaña `💾 Guardado (EDIT)` en `PES2021EditorPro.exe` con auto-detección de partidas en `Documents\KONAMI`, consola de registro criptográfico en vivo y previsualización del logo oficial.
- **Archivos:** [`src/PesCrypt.cs`](file:///E:/Editor%20ligas/src/PesCrypt.cs), [`src/MainForm.cs`](file:///E:/Editor%20ligas/src/MainForm.cs).

---

### 11. Formato Suizo UEFA 2024 y Copas Continentales
- [ ] **Estado:** PENDIENTE
- **Descripción:** Actualizar las competiciones continentales europeas al formato actual (Champions League de 36 equipos en tabla única, Europa League y Conference League).
- **Detalle técnico:**
  - Reemplazo del generador de fixtures de torneos europeos mediante `fl26swiss.dll`.
  - Cuadros de playoff del puesto 9 al 24 y bracket fijo a partir de octavos de final.
  - Asignación de plazas de clasificación europea según ranking de ligas.

---

### 12. Ecosistema Sudamericano y Formatos Especiales (Zonas, Playoffs y Calendarios Anuales)
- [x] **Estado:** COMPLETADO
- **Descripción:** Implementar las estructuras de torneos reales de Sudamérica y formatos multi-zona con playoffs eliminatorios y calendarios estacionales Feb-Dic.
- **Características Implementadas:**
  - [x] **Vinculación de Copas Nacionales y Supercopas en Liga Máster:** Asignación de `cup=<id>` y `scup=<id>` por liga en `fl26world.txt` con participación multigeneracional (`cupall=1`).
  - [x] **Selector de Calendario Estacional (Liga Máster):** Soporte para Temporada Europea (Ago-May, tipo 0) y Sudamericana/Anual (Feb-Dic, tipo 1) con generación automática de directivas `season <region> <type>` para `fl26joindll.lua`.
  - [x] **Selector de Presets de Formato de Torneo:**
    - 🏆 Liga Regular Round-Robin (Ida y Vuelta Tradicional)
    - ⚡ Torneo Corto / Apertura-Clausura (1 Sola Rueda)
    - 🇦🇷 Copa de la Liga / 2 Zonas + Playoffs (14/15 por Zona, Cuartos, Semis y Final)
    - 🇺🇸 Conferencias con Playoffs (2 Zonas Este/Oeste + Playoffs Eliminatorios)
    - ⚙️ Personalizado (Ajuste Manual de Zonas 1..4 y Clasificados a Playoffs 0..16)
  - [x] **Serialización Binaria en `CompetitionRegulation.bin`:** Empaquetado a nivel de bits de `NumClassifiedTeams` (bits 2..7 de offset 14-15), `FinalChampionship` (bit 14) y `Championship` (bit 15) respetando la máscara Konami `0x3F03`.
  - [x] **Plantilla Rápida de Clonación:** Asistente modal en 1 clic para clonar la configuración completa (país, formato, sustituciones, calendario y copas) de cualquier liga existente al crear una nueva.
  - [x] **Generador de Nombres Temáticos Regionales:** Generación de clubes locales realistas (Argentina, Inglaterra, España, Brasil) al autogenerar plazas libres de equipos.
- **Archivos:** [`PesDbModel.cs`](file:///E:/Editor%20ligas/src/PesDbModel.cs), [`DatabaseManager.cs`](file:///E:/Editor%20ligas/src/DatabaseManager.cs), [`SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`MainForm.cs`](file:///E:/Editor%20ligas/src/MainForm.cs).

---

### 16. Diseñador Visual de Pirámides en la GUI C#
- [x] **Estado:** COMPLETADO
- **Descripción:** Interfaz gráfica interactiva en `MainForm.cs` (Pestaña 2: `tabPyramid` - "🌲 Pirámide de Ligas") para visualizar y diseñar la estructura de divisiones y copas de cualquier país.
- **Características Implementadas:**
  - [x] **Selector de País/Sistema:** Dropdown con recuento de divisiones y clubes totales (Argentina, Inglaterra, España, Brasil, Francia, Italia...).
  - [x] **Tarjetas Jerárquicas por Tier:**
    - Tier 1 (Borde Oro `0xFFD700`): Primera División con escudo en alta resolución, conteo de equipos, badge de paridad (`✔️ Par`), normas de cambios/suplentes y cupos continentales (Libertadores/Champions).
    - Tier 2 (Borde Plata `0xC0C0C0`): Segunda División con conteo, ascensos a D1 y descensos a D3.
    - Tier 3 (Borde Bronce `0xCD7F32`) y Tier 4+: Subligas subordinadas.
  - [x] **Puentes Gráficos de Transferencia:** Indicadores verticales entre divisiones con flechas (`⬇️ Descensos` / `⬆️ Ascensos`) y balance automático (`✔️ Sincronizado` o `⚠️ Desbalance`).
  - [x] **Navegación Interactiva:** Botones `✏️ Editar Liga` (salta al editor de la Pestaña 1 con la liga seleccionada) y `🛡️ Ver Equipos`.
  - [x] **Ranura Inferior Dinámica:** Botón/Tarjeta punteada `"➕ Añadir Tier N+1..."` que preconfigura automáticamente el asistente de creación en la Pestaña 3 con el país, la liga padre y el siguiente nivel de jerarquía.
  - [x] **Sección de Copas y Supercopas Nacionales:** Muestra tarjetas dedicadas para la Copa Argentina/FA Cup/Copa del Rey y la Supercopa nacional con acceso a edición directa.

---

### 14. Escáner Inteligente de Colisiones de IDs
- [ ] **Estado:** PENDIENTE
- **Descripción:** Auditor de integridad en `DatabaseManager.cs` que prevenga conflictos con grandes parches instalados (VirtuaRED, Dream Patch, etc.).
- **Características:**
  - [ ] Verificación en tiempo real de IDs libres para competiciones (1..255).
  - [ ] Verificación de IDs libres para regulaciones (179..999).
  - [ ] Verificación de IDs libres para equipos y jugadores.
  - [ ] Alerta inmediata si el usuario intenta usar un ID ocupado por el juego o por un mod activo.

---

### 17. Escáner Inteligente de Colisiones de IDs y Diagnóstico
- [x] **Estado:** COMPLETADO
- **Descripción:** Herramienta de auditoría integral y escáner en tiempo real para verificar la integridad de la base de datos de PES 2021.
- **Implementación Forense:**
  - [x] Detección de colisiones de IDs de Competición (1..255) y Regulación (1..600).
  - [x] Detección de colisiones multilíga: identifica si un mismo club está inscrito en más de una liga doméstica simultánea (causante de cuelgues en el fixture de Liga Máster).
  - [x] Recomendador de próximos IDs libres: calcula automáticamente el próximo Competition ID (ej. #51), Regulation ID (ej. #180) y Slot Sider disponible (ej. #85 de 250).
  - [x] Verificación de paridad de clubes por liga (advertencia preventiva para ligas impares).
  - [x] Validación de rangos de regiones extendidas (0..127).
  - [x] Ventana modal interactiva en la GUI C# ("🔍 Escáner de IDs") con informe completo exportable y copiable al portapapeles.
- **Archivos:** [`DatabaseManager.cs`](file:///E:/Editor%20ligas/src/DatabaseManager.cs), [`MainForm.cs`](file:///E:/Editor%20ligas/src/MainForm.cs).

---

### 18. Gestión Integral de Equipos Clásicos (European & World Classics)
- [x] **Estado:** COMPLETADO
- **Descripción:** Detección, unificación, creación y visualización de la categoría/liga de Equipos Clásicos (`European Classics` ID 2703 y `World Classics` ID 2704) en la sección de ligas y copas.
- **Implementación Forense:**
  - [x] Auto-detección y creación segura de la competición `ID 53` (`CLASSIC_TEAMS`) con reglamento `RegID 182`.
  - [x] Asignación contigua en `CompetitionEntry.bin` de `TeamId = 2703` y `2704`.
  - [x] Reubicación automática si los clásicos estaban asignados erróneamente en ligas de prueba (ej. ID 52), rellenando la vacante con clubes libres de `Team.bin`.
  - [x] Generación de escudo retro dorado full-color en Sider LiveCPK (`emb_0053_*.png` en 6 variantes).
  - [x] Integración en `MainForm.cs` con insignia especial `⭐ Especial / Clásicos`, visible en los filtros "Ligas" y "Todas".
  - [x] Hook de memoria en `league_manager.lua` con `kickoff_after = 5` (Ranura nativa de exhibición de clásicos de Konami).

---

### 19. Eliminación de Ligas y Competiciones Limpias
- [x] **Estado:** COMPLETADO
- **Descripción:** Botón y método transaccional para eliminar cualquier liga o copa en memoria y en disco.
- **Implementación Forense:**
  - [x] Limpieza en cascada: borrado en `Competition.bin` y `CompetitionRegulation.bin`.
  - [x] Liberación de clubes en `CompetitionEntry.bin` (se desasignan de la competición sin eliminarlos de `Team.bin`).
  - [x] Desvinculación de jerarquías de ascensos/descensos en ligas padre e hijas.
  - [x] Eliminación de escudos personalizados en Sider LiveCPK (`emb_XXXX_*.png`).
  - [x] Confirmaciones de seguridad diferenciadas: advertencia de riesgo para ligas base de Konami (ID ≤ 60) y confirmación detallada para ligas creadas.

---

### 20. Gestión Integral de "Otros Equipos" (Europa, América Latina, Asia)
- [x] **Estado:** COMPLETADO
- **Descripción:** Soporte de primera clase para clubes sin liga doméstica (ej. Bayern München 127, Bayer Leverkusen 128, Olympiacos 133, Shakhtar 1232).
- **Implementación Forense:**
  - [x] 3 categorías virtuales dedicadas en el panel izquierdo de competiciones (`Otros Equipos (Europa)`, `Otros Equipos (América Latina)`, `Otros Equipos (Asia)`).
  - [x] Detección forense mediante `IsClubInDomesticLeague(teamId)` y asignación automática por continente en `Country.bin`.
  - [x] Acciones interactivas en la ficha: botón para añadir clubes desvinculándolos de sus ligas y botón para transferir clubes de Otros Equipos a cualquier liga doméstica en 1 clic.
  - [x] Transparencia en el Gestor de Equipos (Tab 3): visualización de `[Otros: Europa]`, etc., en la columna Liga Actual y opciones de asignación directa.

---

### 21. Validación de Paridad de Fixtures (PES 2021 Safe)
- [x] **Estado:** COMPLETADO
- **Descripción:** Validación preventiva para evitar bloqueos del motor de generación de calendarios round-robin de Konami (`0x1414F2030`).
- **Implementación Forense:**
  - [x] Comprobación de paridad en tiempo real en la ficha de edición (`lblParityWarning`) con indicadores visuales verdes (par) y coral (impar).
  - [x] Modal de advertencia interactivo al guardar cambios en ligas regulares con cantidad impar de equipos.
  - [x] Validación y advertencia en el asistente de creación de nuevas ligas (Tab 2).


---

### 22. Paquetes Modulares de Ligas (.pesleague) y Remapeo Relacional
- [x] **Estado:** COMPLETADO
- **Descripción:** Motor integral de exportación e importación modular de ligas completas con reasignación inteligente anti-colisiones.
- **Implementación Forense:**
  - [x] Contenedor ZIP estándar (.pesleague) con manifest.json, emblem.png y escudos de clubes (teams/<id>.png).
  - [x] Serialización binaria de clubes (1532 bytes en Base64), reglamentos oficiales, formatos (grupos/playoffs) y reglas de Liga Máster (ascensos, descensos, calendarios).
  - [x] Motor de Remapeo Relacional de 3 niveles: remapeo de ID de competición (rango 50..254), remapeo de IDs de equipos (rango 1000+) y remapeo de rutas gráficas en LiveCPK.
  - [x] Botón "📦 Exportar .pesleague" en el Explorador de Competiciones (Tab 1) y botón "📥 Importar (.pesleague)" en el Creador de Ligas (Tab 2).
  - [x] Verificado E2E con éxito total al 100%.
- **Archivos:** [LeaguePackageManager.cs](file:///E:/Editor%20ligas/src/LeaguePackageManager.cs), [DatabaseManager.cs](file:///E:/Editor%20ligas/src/DatabaseManager.cs), [MainForm.cs](file:///E:/Editor%20ligas/src/MainForm.cs), [docs/paquetes_modulares_pesleague.md](file:///E:/Editor%20ligas/docs/paquetes_modulares_pesleague.md).

---

### 13. Persistencia de Clubes Extendidos en Modo Editar (`fl26edit` & `fl26editlist`)
- [x] **Estado:** COMPLETADO
- **Descripción:** Motor de persistencia y visibilidad total en Modo Editar (Editar > Equipos, Jugadores, Traspasos, Estrategias) para clubes y ligas personalizadas con ID >= 750, superando el límite nativo de Konami de 749 clubes por tabla.
- **Implementación Forense y Arquitectura de Memoria:**
  - [x] **Expansión de Memoria RAM a 2.048 Clubes (`fl26edit.dll`):** Inyección al arranque que parchea las tablas de Modo Editar antes de su construcción (`0x800` filas por tabla en `team`, `squad`, `tactics` y `manager`).
  - [x] **Almacenamiento Dual Transparente (`EDIT00000000.fl26x`):**
    - El archivo estándar `EDIT00000000` preserva los primeros 749 clubes para mantener 100% de compatibilidad con herramientas externas y arranques sin mods.
    - Los registros a partir del club 750 son interceptados y serializados en el archivo anexo `EDIT00000000.fl26x` con cabecera mágica de 8 bytes `FL26EDX2` (v2), token hash de emparejamiento de 32 bytes y contadores `uint32` de tablas.
    - Soporte de copia de seguridad automática `.prev` (`EDIT00000000.fl26x.prev`) para preservar el estado anterior si en el juego se responde "No" a la sobrescritura.
  - [x] **Inyección Dinámica de Ligas en Menús de Edición (`fl26editlist.lua`):**
    - Parche en el epílogo de la función constructora de listas de equipos de Modo Editar (`0x141ee7086`, `mov rbx, [rsp+0x38]`).
    - Trampolín que ejecuta `push_back(vector, &slot)` nativo (`0x14047be40`) para cada slot de liga personalizada extraído dinámicamente de `fl26world.txt`.
    - Todas las ligas y subdivisiones personalizadas aparecen y se pueden editar directamente en los menús del juego.
  - [x] **Integración en la GUI C# (`MainForm.cs` - Tab 6):**
    - Tarjeta dedicada en la pestaña `💾 Guardado (EDIT)`: estado del módulo Sider, estado del contenedor `.fl26x` y disponibilidad de la copia previa `.prev`.
    - Botón **"🔍 Auditar .fl26x"**: decodificación forense in-process, cálculo de hash cruzado y desglose de clubes/plantillas/tácticas/entrenadores guardados en la consola criptográfica.
    - Botón **"🔄 Restaurar .prev"**: restauración en 1 clic de la versión previa si se canceló un guardado en el juego.
    - Botón **"🚀 Desplegar a Sider"**: despliegue directo y sincronización automática de `fl26edit.dll`, `fl26edit.lua` y `fl26editlist.lua` con configuración en `sider.ini`.
  - [x] **Módulo C# Forense (`PesCrypt.cs`):**
    - Métodos nativos `PesCrypt.InspectFl26xFile(editPath)` y `PesCrypt.RestoreFl26xBackup(editPath, out msg)`.
- **Archivos:** [`src/PesCrypt.cs`](file:///E:/Editor%20ligas/src/PesCrypt.cs), [`src/SiderLuaGenerator.cs`](file:///E:/Editor%20ligas/src/SiderLuaGenerator.cs), [`src/MainForm.cs`](file:///E:/Editor%20ligas/src/MainForm.cs), [`fl26edit.dll`](file:///E:/Editor%20ligas/fl26edit.dll), [`fl26edit.lua`](file:///E:/Editor%20ligas/fl26edit.lua), [`fl26editlist.lua`](file:///E:/Editor%20ligas/fl26editlist.lua).

---

## Próximo Paso Recomendado

Con el **Módulo 10 (`PesCrypt`)** y el **Módulo 13 (`fl26edit` / `fl26editlist`)** completamente terminados, integrados y verificados:
1. **Módulo 11: Dorsales Libres y Minúsculas/Tildes en Camisetas (`KitPos`):** Soporte en Sider para nombres con caracteres acentuados, minúsculas y posicionamiento milimétrico de dorsales.
2. **Módulo 14: Formato Suizo UEFA Champions League 2024:** Sistema de fase de liga única de 36 equipos para competiciones europeas (`fl26swiss.dll`).
3. **Módulo 15: Ecosistema Sudamericano Nativo (Copa Argentina, Promedios):** Sistema de promedios para descensos y copa nacional con cuadro de eliminación directa.

---

## 🏆 ESTADO DE EJECUCIÓN: GESTOR DE EQUIPOS (TAB 3: EQUIPOS) [100% COMPLETADO Y VERIFICADO]

- [x] **Arquitectura Master-Detail:** Implementado `splitTeamsMain` con panel izquierdo de exploración masiva y panel derecho de inspección / edición técnica individual.
- [x] **Filtros Multicriterio Dinámicos:**
  - Búsqueda por texto (ID, Nombre Español, Nombre Inglés, Sigla, País).
  - Filtro por Ámbito (Todos, Solo Clubes, Solo Selecciones, Con Liga, Sin Liga).
  - Filtro por Continente (Europa, América Latina, Asia-Oceanía, Norteamérica, África).
  - Filtro por Liga específica con sincronización automática.
  - Contador KPI dinámico de equipos mostrados vs total cargados.
- [x] **Visualización y Carga de Escudos:**
  - Preview de alta resolución en `picTeamEmblemLarge`.
  - Importación de escudos PNG/JPG con generación automática de las 8 variantes Fox Engine para Sider LiveCPK (`e_{id:D6}_r.png`, `_r_l`, `_r_ll`, `_f`, `_f_l`, `_f_ll`, `_r_b`, `_r_w`).
  - Exportación de escudos a PNG.
- [x] **Ficha Técnica Editable:**
  - Nombre en Español y Nombre Internacional.
  - Sigla / Abreviatura (3-4 caracteres para marcadores de juego).
  - Nombre DB interno de solo lectura.
  - Desplegable de países con nombres reales en español (`db.CountriesById`).
  - CheckBox de Selección Nacional.
  - Asignación de ID de Estadio y Entrenador/DT.
  - Conteo relacional en tiempo real de jugadores en plantilla (`PlayerAssignment.bin`).
- [x] **Gestor de Competiciones:**
  - Asignación rápida en bloque o individual a cualquier liga de la base de datos.
  - Desvinculación de ligas hacia "Otros Equipos" (Europa, Latam, Asia) o "Libre".
- [x] **Creación y Clonación de Clubes:**
  - Diálogo modal especializado `CreateNewTeamDialog` con cálculo de siguiente ID libre ($\ge 1000$), autocompletado de siglas y preview de escudo.
  - Clonación instantánea de clubes existentes con clonación de escudo y duplicación profunda de bytes en `Team.bin`.
  - Eliminación segura con confirmación (física para clubes creados, desvinculación preventiva para clubes oficiales).
- [x] **Suite de Pruebas Automatizadas:** 100% PASS verificado contra base de datos real de PES 2021 (`D:\Nueva carpeta\common\etc\pesdb`).
- [x] **Módulo 1: DTs & Estadios Reales (`Coach.bin` & `Stadium.bin`):** Selección de estadios y entrenadores por nombre real, solución definitiva al bug de Jorge Jesus en clubes nuevos y creador rápido de DTs ("➕ Nuevo DT").
- [x] **Módulo 2: Clásicos y Derbies (`Derby.bin` & `Team.bin`):** Gestión de hasta 3 rivales clásicos con sincronización bidireccional y guardado directo.
- [x] **Módulo 3: Plantilla, Traspasos y Agentes Libres (`Player.bin` & `PlayerAssignment.bin`):** Vista de plantilla con dorsales, orden táctico y roles especiales; edición de dorsales (1..99), traspasos entre clubes, rescisión a agentes libres y fichaje desde un catálogo de más de 25.200 jugadores.
- [x] **Módulo 4: Auto-Asignación Táctica de Roles:** Asignación inteligente con 1 clic de Capitán, Penales, Faltas y Córners excluyendo al arquero (`SquadOrder == 0`).
- [x] **Módulo 5: Exportación/Importación CSV & Creador Masivo de Clubes:** Formato CSV estándar de 148 columnas compatible con `PES2020Editor` y generador masivo (`BatchCreateTeams`) para divisiones completas (2 a 32 clubes) con DTs independientes y ranuras de liga en 1 clic.
- [x] **Compilación Limpia:** Binario final generado en [`PES2021EditorPro.exe`](file:///E:/Editor%20ligas/PES2021EditorPro.exe) sin errores ni advertencias.

---

## ⚽ ESTADO DE EJECUCIÓN: MÓDULO JUGADORES (TAB 6: JUGADORES) [100% COMPLETADO Y VERIFICADO]

### 23. Motor Forense de Jugadores y Fichas Fox Engine (`Player.bin`)
- [x] **Estado:** COMPLETADO (v4.3.0)
- **Decodificación de Bitfields Fox Engine (`PlayerBitHelper.cs`):**
  - Stride de 312 bytes (`0x138`) por registro en `Player.bin`.
  - Altura (`offset 0x1B + 100` cm) y peso (`bit 256, 7 bits + 30` kg).
  - Edad (`bit 408, 6 bits + 15`), pie hábil (`bit 514, 1 bit`), uso de pierna mala (`bit 454, 2 bits + 1`), precisión de pierna mala (`bit 462, 2 bits + 1`), estado de forma (`bit 438, 3 bits + 1`) y resistencia a lesiones (`bit 458, 2 bits: 0=A, 1=B, 2=C`).
  - Las 25 habilidades numéricas de Konami (`DB_ABIL`, 6 bits + 40, rango 40..99) agrupadas en Ataque, Técnica, Físico, Defensa y Portero.
  - Las 40 habilidades especiales (`DB_SKILL`, 1 bit cada una) con checkboxes interactivos.
  - Compatibilidad táctica de 13 posiciones (`DB_POSFLAG`, 2 bits cada una: 0 = No apto, 1 = B, 2 = A).
  - 22 estilos de juego de campo (`bit 155, 5 bits` con remapeo `DB_STYLE`) y 7 estilos de la IA COM (1 bit cada uno).
- [x] **Motor de Fotos y Minifaces (`MinifaceProvider.cs`):**
  - Decodificador directo de texturas DDS (DXT1, DXT3, DXT5) desde Sider `content\miniface_server\MiniFaces\`.
  - Generador dinámico de avatares posicionales vectoriales en alta definición para jugadores sin foto oficial.
  - Caché de renderizado instantáneo en memoria RAM (300 minifaces procesadas en < 190 ms).
- [x] **Visualización en Listas y Roster:**
  - `lvPlayersBrowser`: Lista masiva con `SmallImageList` (36×36 px) mostrando la miniface oficial en la columna de futbolista, ID, posición, media OVR, edad, equipo actual, nacionalidad, pie hábil y biometría.
  - `lvTeamRoster`: Plantilla del club seleccionado con `SmallImageList` (28×28 px) mostrando las fotos de los 32 jugadores.
- [x] **Ficha Premium Multi-Pestaña (`PlayerCardControl.cs`):**
  - Cabecera con miniface de 130×130, insignia de OVR y posición por color de calidad, nombre editable, dorsal, club y chip de país con bandera/nombre real.
  - Pestaña 1: Radar hexagonal interactivo de 6 ejes con recálculo dinámico en tiempo real y selectores numéricos con barras de progreso de color.
  - Pestaña 2: Campo 2D interactivo de 13 posiciones con selección de aptitud y posición principal con clic.
  - Pestaña 3: 40 habilidades especiales.
  - Pestaña 4: Estilos de juego y COM IA.
  - Pestaña 5: Biometría, país, edad, pie dominante, posición registrada y nombre estampado en camiseta.
- [x] **Operaciones CRUD y Traspasos:**
  - Creación de nuevos jugadores (`CreatePlayer`) asignando IDs libres en rango 150.000+.
  - Clonación profunda (`ClonePlayer`) manteniendo todas las habilidades y estilos.
  - Eliminación segura (`DeletePlayer`) con desvinculación en `PlayerAssignment.bin`.
  - Traspasos entre clubes y rescisión a agentes libres con actualización bidireccional.

---

## 🛡️ ESTADO DE RESOLUCIÓN: AUDITORÍA DE SEGURIDAD Y INTEGRIDAD (REVISIÓN COMPLETA V2)

### Matriz de Grupos de Auditoría

| Grupo | Nombre / Alcance | Estado | Altos Involucrados | Notas de Implementación |
| :---: | :--- | :---: | :---: | :--- |
| **A** | **Persistencia y Respaldos Atómicos** | **100% COMPLETADO** | Altos 1, 42, 46 | Guardado en 3 fases (.tmp -> transaccional), 11 binarios + JSON respaldados con microsegundos, renumeración de posiciones persistida, PesCrypt.Save atómico. |
| **B** | **UI Clamping, Defaults y Combos** | **100% COMPLETADO** | Altos 2, 3, 4, 12, 14, 15 | ClampValue en NumericUpDowns, preservación de reglamentos no estándar Konami, dinámicos para continentes/estadios/DTs/países, safe cast y alerta >32 equipos. |
| **C** | **Unificación de Países y Continentes (Bloque 2)** | **100% COMPLETADO** | Altos 27, 28, 29, 39 | Armonización de IDs en IsCalendarYearCountry, GetThemedClubNames (204, 236, 146), GetTeamContinent (CONCACAF 5 y rangos limpios) y SetLeagueCountry con HasUnsavedChanges. |
| **D** | **Sider Lua Generator y Despliegue** | **100% COMPLETADO** | Altos 48, 49, 50, 51 | EscapeLuaString/EscapeLuaComment, check_sites como Paso 1 en m.init, persistencia segura de sider.ini, reporte detallado de archivos copiados/faltantes. |
| **E** | **Integridad de Modelos y Serialización (Bloque 5)** | **100% COMPLETADO** | Altos 52, 53 | Preservación de 17 idiomas Konami en ToBytes(), null-guards defensivos contra NRE en Competition/Regulation/EntryRecord. |
| **F** | **Excepciones Silenciadas y Logs (Bloque 5)** | **100% COMPLETADO** | Altos 30, 31 | Reemplazo de bloques catch vacíos en los 8 archivos opcionales por LoadWarnings y diálogo informativo; guardas de resto final. |
| **G** | **Algoritmos y Loops de Generación (Bloque 4)** | **100% COMPLETADO** | Altos 36, 37, 38 | Asignación segura de IDs globales sin colisión en CreateNewLeague, validación total de teamIds y protección D2. |
| **H** | **Reglas de Competencia y Estabilidad (Bloque 4)** | **100% COMPLETADO** | Altos 8, 9, 10, 11, 35, 40 | Blindaje atómico try/catch en ApplyCompetitionChanges, reconciliación TotalTeams, re-sellado ToBytes() y protección de clásicos/estadios. |
| **I** | **Validación de Exe y Documentación (Bloque 6)** | **100% COMPLETADO** | Altos 58, 59, 60, 61, 62, 63 | Reconciliación de tamaño del ejecutable (458 MB), gatekeeper, transición v3.3.0->v4, aritmética de Player.bin y off-by-one 750 Modo Editar. |

### 📦 BLOQUE 1: MÓDULO JUGADORES, PLANTILLA Y FICHA FOX ENGINE [100% COMPLETADO Y VERIFICADO]
- [x] **Bandera `_isPopulating` (Alto 47):** Protege los 14 manejadores de eventos en `PlayerCardControl.cs` para evitar mutación de `RawBytes` al inspeccionar jugadores.
- [x] **Traspasos Robustos (Altos 18 y 19):** Exclusión de selecciones nacionales, placeholder de selección obligatoria, confirmación modal y refresco bidireccional de plantillas emisor/receptor.
- [x] **Integridad y Límite de 32 Jugadores (Altos 32 y 33):** Sincronización indivisible de `PlayerAssignments` y `PlayerAssignmentsByTeam`, límite reglamentario de 32 futbolistas y derivación automática de doble fichaje a traspaso.
- [x] **Creación Segura de Futbolistas (Alto 20):** Validación obligatoria de nombre, control de posición nula y feedback preventivo ante clubes con cupo completo.

### 📦 BLOQUE 3: SEGURIDAD DE ARCHIVOS, PAQUETES PESLEAGUE Y PORTABILIDAD [100% COMPLETADO Y VERIFICADO]
- [x] **Anti Path-Traversal en CPK (Alto 43):** Validación canónica `fullTargetDir` y neutralización de rutas relativas `..\` en `CpkExtractor.cs`.
- [x] **Parsing Seguro de Tablas @UTF (Alto 44):** Guardas de límites en `nCols <= 256`, `nRows <= 100.000` y verificación estricta de offsets de bytes en `ParseUtfTable`.
- [x] **Anti Zip-Bomb y Clamping de Manifiestos (Altos 5 y 54):** Límites de 500 archivos / 100 MB en `.pesleague`, `GetJsonClampedInt` para evitar desbordamientos de módulo y respaldo previo automático en UI.
- [x] **Portabilidad y Detección Dinámica de Sider (Alto 23):** Helper `GetDetectedSiderDirectory()` reemplazando todas las rutas absolutas fijas en `MainForm.cs` y `SiderLuaGenerator.cs`.

### 📦 BLOQUE 2: GEOGRAFÍA, CONTINENTES Y CALENDARIOS ANUALES [100% COMPLETADO Y VERIFICADO]
- [x] **Unificación de Países en Calendarios Anuales (Alto 28):** Corrección de IDs numéricos en `IsCalendarYearCountry` según `Country.bin` (Bolivia 145, Brasil 146, Ecuador 151, China 261, Japón 262, USA 95, México 89) para generación correcta de temporadas Feb-Dic.
- [x] **Reorientación de Nombres Temáticos (Alto 29):** Corrección de IDs de países en `GetThemedClubNames` (Inglaterra 204, España 236, Brasil 146, Sudamérica unificada) y generación cartesiana anti-bucles con `CombineThemedPool`.
- [x] **Clasificación de Continentes y Fallbacks (Alto 39):** Detección explícita de CONCACAF (continente 5) para naciones centro y norteamericanas; saneamiento de rangos numéricos de respaldo en `GetTeamContinent`.
- [x] **Persistencia de País de Competición (Alto 27):** Señalización obligatoria con `HasUnsavedChanges = true` en `SetLeagueCountry`.

### 📦 BLOQUE 4: REGLAS DE COMPETICIÓN, FORMATOS Y MAPEOS RELACIONALES [100% COMPLETADO Y VERIFICADO]
- [x] **Blindaje Atómico en Edición de Ligas (Alto 8):** Bloque integral `try / catch` con reporte informativo modal en `ApplyCompetitionChanges`; protección contra fallos al eliminar logos en Sider LiveCPK.
- [x] **Reconciliación de Capacidad Oficial (Alto 11):** Verificación proactiva entre `TotalTeams` y la cantidad real de clubes inscritos en `CompetitionEntry.bin` con diálogo de ajuste automático para evitar inconsistencias en el juego.
- [x] **Corrección de Jerarquías y Re-Sellado Limpio (Altos 9 y 10):** Validación de selección de liga padre; resolución estricta por `RegulationRecord.Id` (sin mezclar `CompetitionId`); eliminación de escrituras en crudo a offset 0 reemplazadas por `parentReg.RawBytes = parentReg.ToBytes()`.
- [x] **Preservación de Equipos Clásicos (Alto 35):** `EnsureClassicTeamsCompetition` no extrae 2703/2704 si ya están asignados a ligas del usuario ni altera cupos con clubes arbitrarios; solo asigna clásicos si están libres.
- [x] **Validación Forense en Creación de Ligas (Altos 36, 37 y 38):**
  - Validación exhaustiva de `teamIds`: no nulo, recuento exacto, sin duplicados y existencia en `Team.bin`.
  - Desvinculación automática previa mediante `RemoveTeamFromDomesticLeagues` para evitar duplicidad multilíga.
  - Protección de enlace `IdDivision2` del padre (no sobrescribe enlaces D2 preexistentes).
  - Asignación global de IDs de entrada contiguos en `CompetitionEntry.bin` consultando `usedEntryIds` y previniendo overflow de `ushort`.
- [x] **Prevención de Colisiones en Creador de Estadios (Alto 40):** Comprobación de existencia para IDs explícitos en `CreateNewStadium` y búsqueda segura de identificador libre en rango `1..65534`.

### 📦 BLOQUE 5: INTEGRIDAD DE MODELOS, SERIALIZACIÓN Y ESTRUCTURAS BINARIAS [100% COMPLETADO Y VERIFICADO]
- [x] **Guardas de Resto Final en Parseo (Alto 31):** Sustitución de `i < length` por `i + RecordSize <= length` en los 4 binarios principales (`Competition.bin`, `CompetitionRegulation.bin`, `Team.bin`, `CompetitionEntry.bin`) evitando `IndexOutOfRangeException`.
- [x] **Captura y Telemetría de Errores de Carga (Alto 30):** Erradicación de bloques `catch { }` vacíos en los 8 archivos opcionales (`Country.bin`, `Coach.bin`, `Stadium.bin`, `Derby.bin`, `PlayerAssignment.bin`, `Player.bin`, `Tactics.bin`, `TacticsFormation.bin`), canalización a `LoadWarnings` y aviso en la GUI.
- [x] **Preservación de Idiomas Nativos Konami (Alto 52):** `RegulationRecord.ToBytes` ya no destruye las 17 traducciones nativas en competiciones existentes (`IsModified && !IsNew`), aplicándose la homogenización completa de las 20 ranuras únicamente a ligas nuevas (`IsNew`).
- [x] **Null-Guards en `ToBytes()` (Alto 53):** Control defensivo `RawBytes != null && RawBytes.Length >= RecordSize` en `CompetitionRecord`, `RegulationRecord` y `EntryRecord`, evitando excepciones por clonación nula.

### 📦 BLOQUE 6: AUDITORÍA DOCUMENTAL Y COHERENCIA DE ESPECIFICACIONES [100% COMPLETADO Y VERIFICADO]
- [x] **Reconciliación de Tamaño de Ejecutable (Alto 58):** Tamaño unificado a 458.739.200 bytes (sección `.text` de 38.983.680 bytes) en `DOCUMENTACION_INGENIERIA_INVERSA_PES2021.md`.
- [x] **Aclaración de Gatekeeper e Índices Vanilla (Alto 59):** Detalle de las 6 ligas domésticas oficiales (18 a 23) y exclusión de 'Otros' (24) en el salto condicional `0x141F13143`.
- [x] **Reconciliación Histórica Append-30 vs Tabla Maestra v4 (Altos 60 y 61):** Contextualización de la técnica append v3.3.0 como paso intermedio superado por la expansión dinámica de `0x1434FDF00` (165 filas, `VirtualAlloc`, 250 slots).
- [x] **Corrección Aritmética en Player.bin (Alto 62):** Aclaración en `docs/estructura_binaria.md` de 5.526 jugadores × 312 bytes = 1.724.112 bytes útiles + 84 bytes de relleno.
- [x] **Corrección Off-by-one en Modo Editar (Alto 63):** Cobertura delimitada a los primeros 750 clubes (0..749) en `EDIT00000000` y clubes a partir del índice 750 en `EDIT00000000.fl26x`.

---

## 🏆 ESTADO FINAL DEL PROYECTO: AUDITORÍA COMPLETA AL 100%
Todos los 6 bloques de la auditoría técnica `revision-completa-v2.md` han sido completamente implementados, probados, compilados sin errores con `csc.exe`, desplegados en los ejecutables del proyecto y respaldados documentalmente.
