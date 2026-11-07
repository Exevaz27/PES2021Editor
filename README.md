# PES 2021 EDITOR PRO

**Editor de ligas para eFootball PES 2021: agrega ligas, copas y divisiones NUEVAS sin reemplazar ninguna de las existentes.**

Desarrollado con dedicación por **EXEVAZ27** | Edición Portable | 100% Gratis para la Comunidad

> *EN: A league editor for eFootball PES 2021 that ADDS new leagues, cups and lower divisions without replacing any existing one, with pyramids, Sider integration, diagnostics and full team/player editing. Spanish UI.*

![version](https://img.shields.io/badge/versi%C3%B3n-Beta_1-orange)

---

## Qué hace

- **Crear ligas, copas y supercopas nuevas** en `Competition.bin`, `CompetitionRegulation.bin` y `CompetitionEntry.bin` con IDs seguros, sin pisar contenido original de Konami.
- **Pirámide de divisiones** (2ª, 3ª, 4ª…) con ascensos y descensos vinculados, como el sistema real.
- **Crear Liga completo**: logo desde la galería oficial del juego o imagen propia, copa/supercopa vinculadas, formato (round-robin, Apertura/Clausura, zonas + playoffs, formato suizo UEFA), desempate, autogeneración de equipos, plantillas clonables y protección total: **un equipo que ya juega en una liga nunca se mueve ni se reemplaza** (se clona o se omite).
- **Equipos y jugadores completos**: alta de clubes con estadio, DT, escudo y liga opcional; editor de jugadores con las 25 habilidades, físicos y estilos; alta de jugadores al mismo nivel.
- **Importadores**: clubes reales y planteles desde CSV simple, Football Manager y EA FC / SoFIFA.
- **Ecosistema Sider**: genera y despliega `league_manager.lua` + `fl26world.txt`, gestiona `sider.ini`, mapas de servidores (stadium-server, kit-server y más) y el formato suizo.
- **Diagnóstico de la instalación** con arreglos de un clic, **puntos de restauración** del Sider y **mundos intercambiables** (varias bases en una misma instalación).
- **Guardado EDIT**: inspección y persistencia extendida FL26EDIT (clubes con ID ≥ 750 en Modo Editar, contenedor `.fl26x`).

Todo sale de **tu** configuración: el programa solo te pide la carpeta del juego y deriva Sider, livecpk, modules, content y la pesdb. Ninguna ruta viene escrita de fábrica.

## Requisitos

- Windows 10/11 de 64 bits.
- **eFootball PES 2021** instalado.
- **Sider** (con livecpk) en la carpeta del juego.
- **Microsoft Edge WebView2 Runtime**: viene con Windows 11; en Windows 10, si el programa no abre, instalalo desde la página oficial de Microsoft.
- .NET Framework 4.8 (incluido en Windows).

## Instalación y primer uso

1. Descargá el zip de la pestaña **Releases** y extraelo donde quieras (es portable).
2. Ejecutá `PES2021EditorWeb.exe` (o `Iniciar PES 2021 Editor Web.bat`).
3. En **Inicio** elegí la carpeta de tu juego (donde está `PES2021.exe`). El resto de las rutas se completa solo.
4. Cargá tu base, trabajá, **guardá los .bin** y desde **Sider y Lua** desplegá el ecosistema.
5. Entrá al juego: tus ligas nuevas aparecen en los menús.

> ⚠️ **Estado: BETA 1 — versión de prueba pública.** (Build interno: web-5.1-creditos, 2026-11-06.) El programa crea puntos de restauración antes de las operaciones sensibles, pero hacé tu propia copia de la carpeta de Sider y de tus guardados antes de probar. Está desarrollado y probado sobre la instalación del autor; en otras instalaciones pueden aparecer errores: **reportalos**.

## Cómo reportar un error

Abrí un **Issue** con la plantilla de reporte. Lo que más ayuda:

- El **marcador de versión** que se ve abajo a la izquierda del programa (ej: `beta-1`).
- El **veredicto de la pestaña Diagnóstico** (y su primer problema, ej: `L7`).
- Qué estabas haciendo, paso a paso, y qué esperabas que pasara.
- Si el problema es dentro del juego: el fragmento de **`sider.log`** de esa corrida.
- Tu configuración: carpeta del juego, qué servers tenés instalados (stadium-server, kit-server…) y si usás otro parche grande.

## Créditos

Este software integra conocimientos, descubrimientos y desarrollos de la comunidad de código abierto:

- **Matozanato — FootballLife-new-leagues** · https://github.com/Matozanato/FootballLife-new-leagues (MIT)
  Arquitectura de tabla de parámetros (0x1434FDF00), ganchos de banderas y orden de menús, investigación de fechas en Liga Máster, módulos de ascensos multidivisión (fl26chain) y parches de estabilidad (Null-Guards).
- **Agustinioquino — Pes-Editor-Rebuilt** · https://github.com/Agustinioquino/Pes-Editor-Rebuilt (BSD-3-Clause)
  Criptografía y decodificación del archivo EDIT00000000 (MT19937), lector de contenedores CRI .cpk y descompresor CRILAYLA, módulo de extracción en memoria (edit21_db.lua) y sistema KitPos.

Las licencias de terceros están en [`THIRD-PARTY-LICENSES/`](THIRD-PARTY-LICENSES/). La ventana **Créditos** del programa muestra este mismo reconocimiento.

## Licencia

Código propio bajo licencia **MIT** (ver [`LICENSE`](LICENSE)): gratis para usar, copiar y modificar, conservando el aviso de autoría.

---

*Desarrollado con dedicación para la comunidad modder de PES. Firmado por EXEVAZ27.*
