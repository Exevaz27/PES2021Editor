// ============================================================
// Secciones: Inicio (dashboard) y Configuracion (rutas/asistente)
// ============================================================
(function () {
  "use strict";
  var A = window.App, rpc = A.rpc, esc = A.esc, el = A.el, toast = A.toast;

  // ---------------- INICIO ----------------
  A.register("inicio", function (page) {
    var st = A.S.stats;
    page.innerHTML =
      '<div class="hero"><div class="hero-glow"></div><div class="hero-content">' +
      '<div class="hero-kicker">eFootball PES 2021 · Suite de edición</div>' +
      '<h1 class="hero-title">Tu fútbol,<br>tus ligas.</h1>' +
      '<p class="hero-sub">Creá ligas nuevas sin reemplazar ninguna, gestioná equipos y jugadores, y desplegá todo en el juego con Sider. La misma potencia de siempre, en una casa nueva.</p>' +
      '<div class="hero-actions">' +
      '<button class="btn btn-primary" id="hCrear">➕ Crear una liga</button>' +
      '<button class="btn btn-ghost" id="hLigas">Explorar ligas y copas</button>' +
      (A.S.dbLoaded ? "" : '<button class="btn btn-gold" id="hCargar">📂 Cargar base de datos</button>') +
      "</div></div></div>" +
      '<div class="stat-row">' +
      statCard(st ? st.competitions : "—", "Competiciones") +
      statCard(st ? st.teams : "—", "Equipos") +
      statCard(st ? st.players : "—", "Jugadores") +
      statCard(st ? st.customLeagues : "—", "Ligas creadas por vos") +
      "</div>" +
      '<h2 class="sec-title">Accesos rápidos</h2>' +
      '<div class="quick-row">' +
      quick("📁", "Carpeta del juego", "Elegí dónde está instalado tu PES 2021. De ahí se deriva todo: Sider, livecpk y módulos.", "Elegir carpeta…", "qGame") +
      quick("🏆", "Ligas y Copas", "Explorá las competiciones de tu base, editá nombres, formatos, ascensos y equipos participantes.", "Abrir el explorador", "qLigas") +
      quick("🧭", "Configuración", "Rutas del juego, pesdb, archivo EDIT y módulos de Sider. Todo en un solo lugar.", "Revisar configuración", "qConfig") +
      "</div>" +
      (A.S.dbLoaded ? "" : '<div class="warn-box" style="margin-top:16px">Todavía no hay una base cargada. Si ya configuraste tus rutas en la app actual, esta versión usa la misma configuración: revisá <b>Configuración</b> y tocá <b>Cargar base</b>.</div>');

    function statCard(n, l) { return '<div class="stat-card"><div class="stat-num">' + n + '</div><div class="stat-label">' + l + "</div></div>"; }
    function quick(ico, t, txt, btn, id) {
      return '<div class="quick-card"><div class="quick-ico">' + ico + '</div><div class="quick-title">' + t + '</div><p class="quick-text">' + txt + '</p><button class="btn btn-ghost" id="' + id + '">' + btn + '</button><div class="quick-path" id="' + id + 'Path"></div></div>';
    }

    page.querySelector("#hCrear").addEventListener("click", function () { A.goto("crear"); });
    page.querySelector("#hLigas").addEventListener("click", function () { A.goto("ligas"); });
    var hc = page.querySelector("#hCargar");
    if (hc) hc.addEventListener("click", function () { A.goto("config"); });
    page.querySelector("#qLigas").addEventListener("click", function () { A.goto("ligas"); });
    page.querySelector("#qConfig").addEventListener("click", function () { A.goto("config"); });
    page.querySelector("#qGame").addEventListener("click", function () {
      rpc("pickFolder", { title: "Elegí la carpeta de tu PES 2021 (donde está PES2021.exe)" }).then(function (r) {
        if (!r.path) return;
        page.querySelector("#qGamePath").textContent = r.path;
        return rpc("deriveFromGame", { gameFolder: r.path });
      }).then(function (d) {
        if (!d) return;
        return rpc("saveConfig", {
          gameFolder: d.gameFolder, siderFolder: d.siderFolder, liveCpkFolder: d.liveCpkFolder,
          modulesFolder: d.modulesFolder, contentFolder: d.contentFolder,
          pesdbFolder: d.pesdbPackageExists ? d.pesdbPackage : (A.S.config ? A.S.config.pesdbFolder : "")
        });
      }).then(function () {
        toast("Carpeta del juego guardada. Revisá Configuración para cargar la base.");
        return A.reloadState();
      }).catch(function (e) { toast(e.message, true); });
    });
    return Promise.resolve();
  });

  // ---------------- CONFIGURACION ----------------
  A.register("config", function (page) {
    var cfg = A.S.config || {};
    page.innerHTML =
      '<h2 class="page-title">Configuración</h2>' +
      '<p class="page-sub">Las rutas de tu instalación. Esta app comparte la configuración con la versión actual (mismo editor_config.json), así que si ya la tenías armada, viene sola.</p>' +
      '<div class="card" style="max-width:820px">' +
      '<h3 style="font-size:16px">Juego y Sider</h3>' +
      pathField("Carpeta del juego", "cfgGame", cfg.gameFolder, "Donde está PES2021.exe") +
      '<div class="btn-row"><button class="btn btn-primary" id="btnDerive">Derivar todo desde el juego</button>' +
      '<button class="btn btn-ghost" id="btnDetect">Detectar instalaciones</button>' +
      '<span class="mut small" id="detectResult"></span></div>' +
      pathField("Carpeta de Sider", "cfgSider", cfg.siderFolder, "sider o SiderAddons dentro del juego") +
      pathField("livecpk", "cfgLive", cfg.liveCpkFolder, "Se deriva de Sider") +
      pathField("Módulos Lua", "cfgModules", cfg.modulesFolder, "Se deriva de Sider") +
      pathField("Contenido (content)", "cfgContent", cfg.contentFolder, "Se deriva de Sider") +
      '<h3 style="font-size:16px; margin-top:8px">Minifaces de futbolistas (Sider)</h3>' +
      pathField("Carpeta de Minifaces (Sider)", "cfgMiniface", cfg.minifaceFolder, "content\\miniface-server en Sider") +
      '<div class="btn-row"><button class="btn btn-ghost" id="btnRescanMini">🔄 Re-escanear minifaces</button>' +
      '<button class="btn btn-ghost" id="btnOpenMiniFolder">📂 Abrir carpeta en el Explorador</button>' +
      '<span class="mut small" id="miniStatus"></span></div>' +
      '<h3 style="font-size:16px; margin-top:8px">Base de datos (pesdb)</h3>' +
      pathField("Carpeta pesdb de trabajo", "cfgPesdb", cfg.pesdbFolder, "Donde están los .bin que editás") +
      '<div class="btn-row"><button class="btn btn-ghost" id="btnExtract">Extraer pesdb desde el CPK del juego…</button>' +
      '<span class="mut small" id="cpkInfo"></span></div>' +
      '<h3 style="font-size:16px; margin-top:8px">Archivo EDIT</h3>' +
      pathField("EDIT00000000", "cfgEdit", cfg.editFile, "Tu archivo de opciones/guardado") +
      '<div class="btn-row"><button class="btn btn-ghost" id="btnScanEdit">Buscar EDIT en Documentos\\KONAMI</button>' +
      '<span class="mut small" id="scanResult"></span></div>' +
      '<div class="btn-row" style="margin-top:10px"><button class="btn btn-primary" id="btnSaveCfg">Guardar configuración</button>' +
      '<button class="btn btn-save" style="width:auto" id="btnLoadDb">📂 Cargar base ahora</button></div>' +
      "</div>";

    function pathField(label, id, value, hint) {
      return '<div class="field"><label>' + label + '</label><div style="display:flex; gap:8px">' +
        '<input class="input" id="' + id + '" value="' + esc(value || "") + '" placeholder="' + esc(hint) + '">' +
        '<button class="btn btn-ghost" data-pick="' + id + '" style="flex:0 0 auto">…</button></div></div>';
    }

    A.$$(".btn[data-pick]", page).forEach(function (b) {
      b.addEventListener("click", function () {
        var input = page.querySelector("#" + b.getAttribute("data-pick"));
        var isFile = b.getAttribute("data-pick") === "cfgEdit";
        var action = isFile ? "pickFile" : "pickFolder";
        rpc(action, { title: "Elegí la ruta", initial: input.value }).then(function (r) {
          if (r.path) input.value = r.path;
        }).catch(function (e) { toast(e.message, true); });
      });
    });

    function collect() {
      return {
        gameFolder: page.querySelector("#cfgGame").value,
        siderFolder: page.querySelector("#cfgSider").value,
        liveCpkFolder: page.querySelector("#cfgLive").value,
        modulesFolder: page.querySelector("#cfgModules").value,
        contentFolder: page.querySelector("#cfgContent").value,
        pesdbFolder: page.querySelector("#cfgPesdb").value,
        editFile: page.querySelector("#cfgEdit").value,
        minifaceFolder: page.querySelector("#cfgMiniface").value
      };
    }

    page.querySelector("#btnDerive").addEventListener("click", function () {
      rpc("deriveFromGame", { gameFolder: page.querySelector("#cfgGame").value }).then(function (d) {
        page.querySelector("#cfgSider").value = d.siderFolder || "";
        page.querySelector("#cfgLive").value = d.liveCpkFolder || "";
        page.querySelector("#cfgModules").value = d.modulesFolder || "";
        page.querySelector("#cfgContent").value = d.contentFolder || "";
        if (d.pesdbPackageExists) page.querySelector("#cfgPesdb").value = d.pesdbPackage;
        page.querySelector("#cpkInfo").textContent = d.bestCpk ? "CPK de base detectado: " + d.bestCpk : "Sin CPK detectado";
        toast(d.siderFolder ? "Rutas derivadas de la carpeta del juego." : "No se encontró Sider dentro del juego.", !d.siderFolder);
      }).catch(function (e) { toast(e.message, true); });
    });

    page.querySelector("#btnDetect").addEventListener("click", function () {
      rpc("detectGames").then(function (r) {
        page.querySelector("#detectResult").textContent = r.games.length ? r.games.join(" · ") : "No se detectaron instalaciones";
        if (r.games.length) page.querySelector("#cfgGame").value = r.games[0];
      }).catch(function (e) { toast(e.message, true); });
    });

    page.querySelector("#btnExtract").addEventListener("click", function () {
      var game = page.querySelector("#cfgGame").value;
      var target = page.querySelector("#cfgPesdb").value;
      if (!target) { toast("Primero definí la carpeta pesdb destino.", true); return; }
      toast("Extrayendo la pesdb desde el CPK…");
      rpc("extractPesdb", { gameFolder: game, targetFolder: target }).then(function (r) {
        toast("Extraídos " + r.files + " archivos desde " + r.cpk);
      }).catch(function (e) { toast(e.message, true); });
    });

    function updateMiniStatus(count, folder) {
      var el = page.querySelector("#miniStatus");
      if (!el) return;
      if (count > 0) {
        el.innerHTML = '<b style="color:var(--cyan)">' + count + ' fotos indexadas</b> y listas para usar.';
      } else {
        el.innerHTML = '<span style="color:var(--muted)">0 fotos detectadas — Carpeta vinculada en Sider lista para recibir fotos .dds o .png</span>';
      }
    }
    updateMiniStatus(cfg.minifaceCount || 0, cfg.minifaceFolder);

    page.querySelector("#btnRescanMini").addEventListener("click", function () {
      toast("Escaneando carpetas de minifaces en Sider…");
      rpc("rescanMinifaces").then(function (r) {
        if (r.minifaceFolder && !page.querySelector("#cfgMiniface").value) {
          page.querySelector("#cfgMiniface").value = r.minifaceFolder;
        }
        updateMiniStatus(r.minifaceCount, r.minifaceFolder);
        toast("Minifaces re-escaneadas: " + r.minifaceCount + " encontradas.");
      }).catch(function (e) { toast(e.message, true); });
    });

    page.querySelector("#btnOpenMiniFolder").addEventListener("click", function () {
      var p = page.querySelector("#cfgMiniface").value;
      rpc("openFolder", { path: p }).catch(function (e) { toast(e.message, true); });
    });

    page.querySelector("#btnScanEdit").addEventListener("click", function () {
      rpc("scanEditFiles").then(function (r) {
        page.querySelector("#scanResult").textContent = r.files.length ? r.files.length + " archivo(s) encontrado(s)" : "Ninguno encontrado";
        if (r.files.length) page.querySelector("#cfgEdit").value = r.files[0];
      }).catch(function (e) { toast(e.message, true); });
    });

    page.querySelector("#btnSaveCfg").addEventListener("click", function () {
      rpc("saveConfig", collect()).then(function () {
        toast("Configuración guardada.");
        return A.reloadState();
      }).catch(function (e) { toast(e.message, true); });
    });

    page.querySelector("#btnLoadDb").addEventListener("click", function () {
      rpc("saveConfig", collect()).then(function () {
        return A.loadDb(page.querySelector("#cfgPesdb").value);
      }).then(function () {
        toast("Base cargada: " + A.S.stats.competitions + " competiciones, " + A.S.stats.teams + " equipos.");
        A.goto("inicio");
      }).catch(function (e) { toast(e.message, true); });
    });

    return Promise.resolve();
  });
})();
