// ============================================================
// Secciones: Piramide, Sider y Lua, Guardado EDIT
// ============================================================
(function () {
  "use strict";
  var A = window.App, rpc = A.rpc, esc = A.esc, toast = A.toast;

  // ---------------- PIRAMIDE (como la version de escritorio) ----------------
  A.register("piramide", function (page) {
    if (!A.S.dbLoaded) {
      page.innerHTML = '<div class="empty-state"><div class="empty-ico">🌲</div><h2>Sin base cargada</h2><p>Cargá tu pesdb para ver las pirámides de ligas por país.</p></div>';
      return Promise.resolve();
    }
    var selectedCountry = 0;
    page.innerHTML = '<h2 class="page-title">Pirámide de ligas</h2><p class="page-sub">El sistema de divisiones de cada país, como en tu versión de escritorio: un país por vez, sus tarjetas, los puentes de ascenso/descenso y añadir divisiones desde acá.</p><div id="pyrBody"><span class="spin"></span> Calculando pirámides…</div>';
    return load();

    function load() {
      return rpc("getPyramids").then(function (r) { render(r); }).catch(function (e) { toast(e.message, true); });
    }

    function tierColor(n) { return n === 1 ? "#FFD700" : n === 2 ? "#C0C0C0" : n === 3 ? "#CD7F32" : "#00E5FF"; }

    function render(r) {
      var body = page.querySelector("#pyrBody");
      if (!body) return;
      if (!r.pyramids.length) { body.innerHTML = '<div class="empty-state"><h2>Sin pirámides</h2><p>No se detectaron sistemas de ligas por país en esta base.</p></div>'; return; }
      var sys = null;
      r.pyramids.forEach(function (x) { if (x.countryId === selectedCountry) sys = x; });
      if (!sys) {
        r.pyramids.forEach(function (x) { if (x.countryId === 144) sys = x; });
        if (!sys) sys = r.pyramids[0];
        selectedCountry = sys.countryId;
      }
      var maxTier = 0;
      sys.tiers.forEach(function (t) { if (t.n > maxTier) maxTier = t.n; });
      var selOpts = r.pyramids.map(function (x) {
        return '<option value="' + x.countryId + '"' + (x.countryId === sys.countryId ? " selected" : "") + ">" + esc(x.countryName) + "</option>";
      }).join("");
      var toolbar =
        '<div class="pyr-toolbar"><span class="pyr-tlabel">Sistema de Ligas:</span>' +
        '<select class="input" id="pyrCountry" style="width:300px">' + selOpts + "</select>" +
        '<span class="pill">🏛️ ' + sys.tiers.length + " División(es)</span>" +
        '<span class="pill">🛡️ ' + (sys.totalClubs || 0) + " Clubes Totales</span>" +
        '<span class="pill" style="color:' + (sys.isBalanced ? "var(--green)" : "var(--gold)") + '">' + (sys.isBalanced ? "⚖️ " : "⚠️ ") + esc(sys.balanceMessage || "") + "</span>" +
        '<span style="margin-left:auto; display:flex; gap:8px"><button class="btn btn-primary" id="pyrAddTop">➕ Añadir División Inferior...</button>' +
        '<button class="btn btn-ghost" id="pyrRefresh">🔄</button></span></div>';

      var cards = "";
      if (!sys.tiers.length) {
        cards = '<div class="empty-state"><h2>Sin divisiones</h2><p>No hay divisiones registradas para este país. Tocá «Añadir División Inferior» para crear la primera.</p></div>';
      }
      sys.tiers.forEach(function (t, i) {
        if (i > 0) {
          var prev = sys.tiers[i - 1];
          if (t.parentCompId && t.parentCompId === prev.compId) {
            var balanced = prev.demote === t.promote;
            cards += '<div class="pyr-bridge"><span style="color:#FF7878">⬇️ ' + prev.demote + ' Descendidos</span>' +
              '<span class="pill" style="color:' + (balanced ? "var(--green)" : "var(--gold)") + '">' + (balanced ? "✔️ Sincronizado" : "⚠️ Desbalance") + "</span>" +
              '<span style="color:#78FF78">⬆️ ' + t.promote + ' Ascendidos</span></div>';
          } else {
            cards += '<div class="pyr-bridge" style="justify-content:center; opacity:.45"><span>⬇ ⬇ ⬇</span></div>';
          }
        }
        var rules = "";
        if (t.n > 1) rules += '<span style="color:#78FF78">⬆️ ' + t.promote + " Ascensos a Tier " + (t.n - 1) + "</span>";
        if (t.n < maxTier) rules += '<span style="color:#FF7878">⬇️ ' + t.demote + " Descensos a Tier " + (t.n + 1) + "</span>";
        if (t.n === 1 && t.continental1 > 0) rules += '<span style="color:var(--gold)">🏆 ' + t.continental1 + " a " + (t.continent === 4 ? "Libertadores" : "Champions") + "</span>";
        cards +=
          '<div class="pyr-card" style="border-top:4px solid ' + tierColor(t.n) + '">' +
          '<div style="display:flex; justify-content:space-between; align-items:center"><span class="pill" style="color:' + tierColor(t.n) + '">' + esc((t.title || "").toUpperCase()) + '</span><span class="pill">ID ' + t.compId + "</span></div>" +
          '<div class="pyr-card-name">' + esc(t.name || t.title) + "</div>" +
          '<div class="mut">' + t.teamCount + " equipos</div>" +
          (rules ? '<div class="pyr-rules">' + rules + "</div>" : "") +
          '<div class="pyr-card-btns"><button class="btn btn-primary sm" data-goto-comp="' + t.compId + '">✏️ Editar Liga</button>' +
          '<button class="btn btn-ghost sm" data-goto-comp="' + t.compId + '">🛡️ Ver Equipos</button></div></div>';
      });
      var nextTier = (sys.tiers.length ? sys.tiers[sys.tiers.length - 1].n : 0) + 1;
      if (sys.tiers.length)
        cards += '<button class="pyr-addslot" id="pyrAddSlot">➕ Añadir Tier ' + nextTier + " (" + nextTier + "ª División) para " + esc(sys.countryName) + "...</button>";
      var cupsHtml = "";
      var allCups = (sys.cups || []).concat(sys.superCups || []);
      if (allCups.length) {
        cupsHtml = '<div class="pyr-cups"><h3>🏆 COPAS NACIONALES Y SUPERCOPAS VINCULADAS</h3>' +
          allCups.map(function (c) { return '<button class="pill cup pyr-cup" data-goto-comp="' + c.compId + '">' + esc(c.name) + "</button>"; }).join(" ") + "</div>";
      }
      body.innerHTML = toolbar + '<div class="pyr-wrap">' + cards + cupsHtml + "</div>";
      wire(r, sys);
    }

    function gotoCreate(sys) {
      var last = sys.tiers.length ? sys.tiers[sys.tiers.length - 1] : null;
      A.S.pyrPreset = {
        countryId: sys.countryId,
        parentCompId: last ? last.compId : 0,
        parentName: last ? (last.name || last.title) : "",
        nextTier: last ? last.n + 1 : 1
      };
      A.goto("crear");
    }

    function wire(r, sys) {
      var body = page.querySelector("#pyrBody");
      var sel = body.querySelector("#pyrCountry");
      if (sel) sel.addEventListener("change", function () { selectedCountry = Number(sel.value); render(r); });
      var addTop = body.querySelector("#pyrAddTop");
      if (addTop) addTop.addEventListener("click", function () { gotoCreate(sys); });
      var addSlot = body.querySelector("#pyrAddSlot");
      if (addSlot) addSlot.addEventListener("click", function () { gotoCreate(sys); });
      var ref = body.querySelector("#pyrRefresh");
      if (ref) ref.addEventListener("click", function () { load(); });
      A.$$("[data-goto-comp]", body).forEach(function (b) {
        b.addEventListener("click", function () { A.S.pendingCompId = Number(b.getAttribute("data-goto-comp")); A.goto("ligas"); });
      });
    }
  });

  // ---------------- SIDER ----------------
  A.register("sider", function (page) {
    page.innerHTML =
      '<h2 class="page-title">Sider y Lua</h2>' +
      '<p class="page-sub">El ecosistema que hace que tus ligas nuevas aparezcan en el juego: módulos Lua, league_manager y el gestor de sider.ini.</p>' +
      '<div id="sdBody"><span class="spin"></span> Leyendo el estado de Sider…</div>';
    return load();

    function load() {
      return rpc("getSider").then(function (r) {
        var body = page.querySelector("#sdBody");
        body.innerHTML =
          '<div class="card-sec"><h3>Estado</h3>' +
          '<div class="kv"><span>Carpeta de Sider</span><code>' + esc(r.siderFolder || "— sin configurar —") + "</code></div>" +
          '<div class="kv"><span>league_manager.lua</span><code>' + (r.luaExists ? "instalado en modules" : "no instalado todavía") + "</code></div>" +
          '<div class="kv"><span>Ligas custom detectadas</span><code>' + (r.customLeagues != null ? r.customLeagues : "— (sin base)") + "</code></div>" +
          '<div class="btn-row" style="margin-top:14px">' +
          '<button class="btn btn-primary" id="sdDeploy">🚀 Desplegar ecosistema Sider</button>' +
          '<button class="btn btn-ghost" id="sdSaveLua">💾 Guardar league_manager.lua</button>' +
          '<button class="btn btn-ghost" id="sdViewLua">👁️ Ver Lua</button>' +
          '<button class="btn btn-ghost" id="sdWorld">🌍 Ver world.txt</button>' +
          "</div></div>" +
          '<div id="sdScan" style="margin-top:16px"></div>' +
          '<div class="card-sec" style="margin-top:16px"><h3>Formato Suizo UEFA Champions League <span class="sub">Nuevo sistema UEFA 36 clubes (8 jornadas en tabla única vía fl26swiss.dll)</span></h3>' +
          '<label class="check-line"><input type="checkbox" id="sdUefaSwiss"' + (r.enableUefaSwiss ? " checked" : "") + '> Activar módulo moderno fl26swiss (36 clubes / 4 bombos / fase de liga única)</label>' +
          '<p class="mut small" style="margin-top:6px">Al activarlo, se incluye fl26swiss.lua en sider.ini y se inyecta fl26swiss.dll para reemplazar el emparejamiento circular clásico por el sistema suizo moderno sin romper las ligas originales.</p>' +
          '</div>' +
          '<div class="card-sec" style="margin-top:16px"><h3>Módulos en sider.ini <span class="sub">activá o desactivá cada módulo</span></h3>' +
          (r.modules.length ? r.modules.map(function (m) {
            return '<label class="check-line"><input type="checkbox" data-mod="' + esc(m.name) + '"' + (m.active ? " checked" : "") + "> " + esc(m.name) + "</label>";
          }).join("") : '<p class="mut">No se encontraron módulos (¿está bien la carpeta de Sider?).</p>') +
          "</div>" +
          '<div id="sdMaps" style="margin-top:16px"></div>' +
          '<div id="sdBackups" style="margin-top:16px"></div>' +
          '<div id="sdProfiles" style="margin-top:16px"></div>';

        A.$$("[data-mod]", body).forEach(function (cb) {
          cb.addEventListener("change", function () {
            rpc("setSiderModule", { name: cb.getAttribute("data-mod"), active: cb.checked }).then(function () {
              toast("Módulo " + cb.getAttribute("data-mod") + (cb.checked ? " activado." : " desactivado."));
            }).catch(function (e) { toast(e.message, true); cb.checked = !cb.checked; });
          });
        });
        rpc("getIdScan", {}).then(function (sc) {
          var box = body.querySelector("#sdScan");
          if (box) box.innerHTML = '<div class="card-sec"><h3>🔍 Escáner de IDs e Integridad</h3>' +
            '<div class="kv"><span>Próximo Comp. ID libre</span><code>ID #' + sc.nextCompId + '</code></div>' +
            '<div class="kv"><span>Próximo Regulation ID libre</span><code>ID #' + sc.nextRegId + '</code></div>' +
            '<div class="kv"><span>Próximo Slot Sider libre</span><code>Slot #' + sc.nextSlot + ' (de 250)</code></div></div>';
        }).catch(function () { });
        var swissCb = body.querySelector("#sdUefaSwiss");
        if (swissCb) {
          swissCb.addEventListener("change", function () {
            var en = swissCb.checked;
            rpc("setUefaSwissFormat", { enabled: en }).then(function (r2) {
              toast("Formato Suizo UEFA " + (r2.enabled ? "activado" : "desactivado") + " en sider.ini y configuración.");
              load();
            }).catch(function (e) {
              toast(e.message, true);
              swissCb.checked = !en;
            });
          });
        }
        loadServerMaps();
        loadBackups();
        loadProfiles();
        body.querySelector("#sdDeploy").addEventListener("click", function () {
          A.confirmModal("Desplegar ecosistema Sider", "Se instalan/actualizan los módulos Lua en tu Sider (con el arreglo que preserva tu sider.ini tal cual). Antes se crea un punto de restauración automático. ¿Continuar?", "Desplegar", function () {
            rpc("deploySider").then(function (r2) {
              toast(r2.message || (r2.deployed ? "Ecosistema desplegado." : "No se pudo desplegar."), !r2.deployed);
              load();
            }).catch(function (e) { toast(e.message, true); });
          });
        });
        body.querySelector("#sdSaveLua").addEventListener("click", function () {
          rpc("saveLua").then(function (r2) { toast("Lua guardado en " + r2.path); load(); }).catch(function (e) { toast(e.message, true); });
        });
        body.querySelector("#sdViewLua").addEventListener("click", function () {
          rpc("getLuaPreview").then(function (r2) {
            A.showModal("league_manager.lua", '<pre class="code">' + esc(r2.lua) + "</pre>", [{ label: "Cerrar" }]);
          }).catch(function (e) { toast(e.message, true); });
        });
        body.querySelector("#sdWorld").addEventListener("click", function () {
          rpc("getWorldTxt").then(function (r2) {
            A.showModal("world.txt (resumen del mundo)", '<pre class="code">' + esc(r2.world) + "</pre>", [{ label: "Cerrar" }]);
          }).catch(function (e) { toast(e.message, true); });
        });
      }).catch(function (e) { toast(e.message, true); });
    }

    // ---- Mapas de servidores (Punto B): stadium-server y kit-server ----
    var mapServer = "stadium";
    function loadServerMaps() {
      var box = page.querySelector("#sdMaps");
      if (!box) return;
      rpc("getServerMap", { server: mapServer }).then(function (r) {
        var srvList = [
          { id: "stadium", label: "🏟️ Estadios" },
          { id: "kit", label: "👕 Kits" },
          { id: "ball", label: "⚽ Balones" },
          { id: "scoreboard", label: "📺 Marcadores" },
          { id: "badge", label: "🏅 Badges" },
          { id: "referee", label: "🟨 Árbitros" },
          { id: "commentary", label: "🎙️ Relatos" }
        ];
        var tabs = '<div class="subtabs" style="display:flex; flex-wrap:wrap; gap:6px; margin-bottom:12px">' +
          srvList.map(function (s) {
            return '<button class="subtab' + (mapServer === s.id ? " on" : "") + '" data-srv="' + s.id + '">' + s.label + '</button>';
          }).join("") + '</div>';

        if (!r.installed) {
          box.innerHTML = '<div class="card-sec"><h3>Mapas de servidores <span class="sub">asignaciones oficiales para ' + esc(r.serverName || mapServer) + '</span></h3>' + tabs +
            '<p class="mut">El servidor <strong>' + esc(r.serverName || mapServer) + '</strong> no está en tu carpeta content (' + esc(r.dir || "") + '). Creá o copiá la carpeta del servidor para que sus asignaciones aparezcan acá.</p></div>';
          wireMapTabs(box);
          return;
        }

        var isComp = (r.keyKind === "comp");
        var entLabel = isComp ? "Competición" : "Equipo";

        var rows = r.entries.length ? r.entries.map(function (e) {
          var entName = e.name || e.teamName || (entLabel + " #" + e.id);
          return '<div class="bk-row"><div style="flex:1; min-width:0">' +
            '<span class="nm">' + esc(entName) + '</span> <span class="sub">#' + e.id + '</span>' +
            '<span class="sub"> → ' + esc(e.target) + '</span>' +
            (e.targetExists ? "" : ' <span class="tag warn">la carpeta no existe</span>') +
            (e.duplicate ? ' <span class="tag warn">duplicado: vale la primera</span>' : "") +
            '</div><span class="bk-actions">' +
            '<button class="btn btn-ghost" data-map-edit="' + e.id + '" data-map-target="' + esc(e.target) + '">Cambiar</button>' +
            '<button class="btn danger" data-map-del="' + e.id + '">Quitar</button></span></div>';
        }).join("") : '<p class="mut">El mapa todavía no tiene asignaciones' + (r.mapExists ? "." : " (el archivo se crea con la primera).") + '</p>';

        var opts = (r.entityOptions || []).map(function (o) {
          return '<option value="' + o.id + '">' + esc(o.name) + '</option>';
        }).join("");
        var entSelect = '<option value="">' + entLabel + '…</option>' + opts;

        var targetOpts = '<option value="">' + (r.server === "commentary" ? "Idioma de relato…" : "Carpeta destino…") + '</option>' +
          (r.targets || []).map(function (t) { return '<option value="' + esc(t) + '">' + esc(t) + "</option>"; }).join("");

        box.innerHTML = '<div class="card-sec"><h3>Mapas de servidores <span class="sub">' + esc(r.mapPath) + '</span></h3>' + tabs + rows +
          '<div class="btn-row" style="margin-top:12px; align-items:center; flex-wrap:wrap; gap:8px">' +
          '<select class="input" id="mapEntity" style="max-width:280px">' + entSelect + '</select>' +
          '<select class="input" id="mapTarget" style="max-width:280px">' + targetOpts + '</select>' +
          '<button class="btn btn-primary" id="mapSet">Asignar</button></div>' +
          '<p class="mut small" style="margin-top:8px">El archivo se edita quirúrgicamente sin alterar comentarios ni orden. Antes de escribir queda un respaldo .bak y un punto de restauración automático.</p></div>';
        wireMapTabs(box);

        box.querySelector("#mapSet").addEventListener("click", function () {
          var id = Number(box.querySelector("#mapEntity").value || 0);
          var target = box.querySelector("#mapTarget").value;
          if (!id || !target) { toast("Elegí " + entLabel.toLowerCase() + " y destino.", true); return; }
          rpc("setServerMapEntry", { server: mapServer, id: id, teamId: id, target: target }).then(function (r2) {
            if (r2.message) toast(r2.message); loadServerMaps();
          }).catch(function (e) { toast(e.message, true); });
        });

        A.$$("[data-map-del]", box).forEach(function (b) {
          b.addEventListener("click", function () {
            A.confirmModal("Quitar asignación", "Se borra la línea de este elemento del mapa (queda copia .bak y punto de restauración).", "Quitar", function () {
              rpc("removeServerMapEntry", { server: mapServer, id: Number(b.getAttribute("data-map-del")), teamId: Number(b.getAttribute("data-map-del")) }).then(function (r2) {
                if (r2.message) toast(r2.message); loadServerMaps();
              }).catch(function (e) { toast(e.message, true); });
            });
          });
        });

        A.$$("[data-map-edit]", box).forEach(function (b) {
          b.addEventListener("click", function () {
            var entSel = box.querySelector("#mapEntity");
            var targetSel = box.querySelector("#mapTarget");
            if (entSel) entSel.value = b.getAttribute("data-map-edit");
            if (targetSel) targetSel.value = b.getAttribute("data-map-target");
            if (targetSel) targetSel.focus();
          });
        });
      }).catch(function () { box.innerHTML = ""; });
    }
    function wireMapTabs(box) {
      A.$$("[data-srv]", box).forEach(function (b) {
        b.addEventListener("click", function () { mapServer = b.getAttribute("data-srv"); loadServerMaps(); });
      });
    }

    function loadBackups() {
      var box = page.querySelector("#sdBackups");
      if (!box) return;
      rpc("listRestorePoints").then(function (r) {
        box.innerHTML =
          '<div class="card-sec"><h3>Puntos de restauración <span class="sub">foto del Sider: sider.ini, módulos y paquete</span></h3>' +
          (r.points.length ? r.points.map(function (pt) {
            return '<div class="bk-row" style="align-items:center"><div style="flex:1; min-width:0">' +
              '<div class="nm">' + esc(pt.motivo || "Punto") + (pt.automatico ? ' <span class="tag">auto</span>' : '') + "</div>" +
              '<div class="sub">' + esc(pt.fecha) + " · " + pt.tamanoMb + " MB" + (pt.veredicto ? " · " + esc(pt.veredicto) : "") + "</div></div>" +
              '<button class="btn btn-ghost" style="flex:none" data-restore="' + esc(pt.id) + '">Restaurar</button></div>';
          }).join("") : '<p class="mut">Todavía no hay puntos: se crean solos antes de desplegar, aplicar un perfil o arreglar — o creá uno ahora.</p>') +
          '<div class="btn-row" style="margin-top:12px"><button class="btn btn-primary" id="sdNewPoint">📸 Crear punto ahora</button></div></div>';
        box.querySelector("#sdNewPoint").addEventListener("click", function () {
          rpc("createRestorePoint", { motivo: "manual" }).then(function (r2) {
            toast(r2.message || "Punto creado."); loadBackups();
          }).catch(function (e) { toast(e.message, true); });
        });
        A.$$("[data-restore]", box).forEach(function (b) {
          b.addEventListener("click", function () {
            A.confirmModal("Restaurar punto", "El Sider (sider.ini, módulos Lua y paquete) vuelve a como estaba en este punto. Antes se crea un punto de seguridad con el estado actual, así que nada se pierde. ¿Continuar?", "Restaurar", function () {
              rpc("restoreRestorePoint", { id: b.getAttribute("data-restore") }).then(function (r2) {
                toast(r2.message || "Punto restaurado."); load();
              }).catch(function (e) { toast(e.message, true); });
            });
          });
        });
      }).catch(function () { box.innerHTML = ""; });
    }

    function loadProfiles() {
      var box = page.querySelector("#sdProfiles");
      if (!box) return;
      rpc("listProfiles").then(function (r) {
        box.innerHTML =
          '<div class="card-sec"><h3>Perfiles de módulos <span class="sub">estados de sider.ini con nombre</span></h3>' +
          (r.profiles.length ? r.profiles.map(function (pf) {
            return '<div class="bk-row" style="align-items:center"><div style="flex:1; min-width:0">' +
              '<div class="nm">' + esc(pf.nombre) + (pf.esComoEstaba ? ' <span class="tag">protegido</span>' : "") + "</div>" +
              '<div class="sub">' + pf.activos + " de " + pf.modulos + " módulos activos</div></div>" +
              '<button class="btn btn-ghost" style="flex:none" data-apply-profile="' + esc(pf.nombre) + '">Aplicar</button>' +
              (pf.esComoEstaba ? "" : '<button class="btn btn-ghost" style="flex:none" data-del-profile="' + esc(pf.nombre) + '">Borrar</button>') + "</div>";
          }).join("") : '<p class="mut">Sin perfiles todavía.</p>') +
          '<div class="btn-row" style="margin-top:12px"><input class="input" id="sdProfileName" placeholder="Nombre del perfil nuevo" style="flex:1">' +
          '<button class="btn btn-primary" id="sdSaveProfile">💾 Guardar estado actual</button></div></div>';
        box.querySelector("#sdSaveProfile").addEventListener("click", function () {
          rpc("saveProfile", { nombre: box.querySelector("#sdProfileName").value }).then(function (r2) {
            toast(r2.message || "Perfil guardado."); loadProfiles();
          }).catch(function (e) { toast(e.message, true); });
        });
        A.$$("[data-apply-profile]", box).forEach(function (b) {
          b.addEventListener("click", function () {
            A.confirmModal("Aplicar perfil", "Los módulos de sider.ini quedan como en el perfil «" + b.getAttribute("data-apply-profile") + "». Antes se crea un punto de restauración automático. ¿Continuar?", "Aplicar", function () {
              rpc("applyProfile", { nombre: b.getAttribute("data-apply-profile") }).then(function (r2) {
                toast(r2.message || "Perfil aplicado."); load();
              }).catch(function (e) { toast(e.message, true); });
            });
          });
        });
        A.$$("[data-del-profile]", box).forEach(function (b) {
          b.addEventListener("click", function () {
            rpc("deleteProfile", { nombre: b.getAttribute("data-del-profile") }).then(function (r2) {
              toast(r2.message || "Perfil borrado."); loadProfiles();
            }).catch(function (e) { toast(e.message, true); });
          });
        });
      }).catch(function () { box.innerHTML = ""; });
    }
  });

  // ---------------- CREDITOS ----------------
  A.register("creditos", function (page) {
    page.innerHTML =
      '<h2 class="page-title">PES 2021 EDITOR PRO</h2>' +
      '<p class="page-sub" style="color:var(--gold); font-style:italic">Desarrollado con dedicación por EXEVAZ27 | Edición Portable | 100% Gratis para la Comunidad</p>' +
      '<div class="card" style="max-width:900px"><p>Editor completo de ligas para eFootball PES 2021: crea y gestiona ligas, copas y divisiones nuevas en Competition.bin, CompetitionRegulation.bin y CompetitionEntry.bin <b>sin pisar ni reemplazar ninguna de las ligas existentes</b>, con pirámides de ascensos y descensos, ecosistema Sider (league_manager), diagnóstico de la instalación, puntos de restauración, mundos intercambiables e importadores de equipos y jugadores.</p>' +
      '<p class="mut small">Desarrollado desde cero mediante ingeniería inversa a nivel de bit y byte sobre los archivos de base de datos de Konami (WESYS/zlib) y el motor x64 de PES2021.exe.</p></div>' +
      '<div class="card" style="max-width:900px; margin-top:16px"><h3 style="margin-top:0">CRÉDITOS Y AGRADECIMIENTOS ESPECIALES A LA COMUNIDAD</h3>' +
      '<p>Este software integra conocimientos, descubrimientos y desarrollos de destacados investigadores de código abierto de la comunidad:</p>' +
      '<div class="kv"><span><b>Matozanato</b> — FootballLife-new-leagues</span></div>' +
      '<p class="small" style="margin:2px 0 4px">GitHub: <code>https://github.com/Matozanato/FootballLife-new-leagues</code> (licencia MIT)</p>' +
      '<p class="small mut" style="margin:0 0 14px">Aportes: Arquitectura de tabla de parámetros (0x1434FDF00), ganchos de banderas y orden de menús, investigación de fechas en Liga Máster, módulos de ascensos multidivisión (fl26chain) y parches de estabilidad (Null-Guards).</p>' +
      '<div class="kv"><span><b>Agustinioquino</b> — Pes-Editor-Rebuilt</span></div>' +
      '<p class="small" style="margin:2px 0 4px">GitHub: <code>https://github.com/Agustinioquino/Pes-Editor-Rebuilt</code> (licencia BSD-3-Clause)</p>' +
      '<p class="small mut" style="margin:0">Aportes: Criptografía y decodificación completa del archivo EDIT00000000 (Mersenne Twister MT19937), lector de contenedores CRI .cpk y descompresor nativo CRILAYLA, módulo de extracción en memoria (edit21_db.lua), y sistema KitPos (posicionamiento libre de dorsales y minúsculas en camisetas).</p></div>' +
      '<div class="card" style="max-width:900px; margin-top:16px"><p style="margin:0">Desarrollado con dedicación para la comunidad modder de PES.<br>Firmado por <b>EXEVAZ27</b>.</p>' +
      '<p class="mut small" style="margin:10px 0 0">Versión de esta aplicación web: <b id="crVersion">web</b> · PES 2021 EDITOR PRO</p></div>';
    return rpc("ping", {}).then(function (r) { var v = page.querySelector("#crVersion"); if (v && r.shell) v.textContent = r.shell; }).catch(function () { });
  });
  // ---------------- EDIT ----------------
  var editInfo = null;
  A.register("edit", function (page) {
    var cfg = A.S.config || {};
    page.innerHTML =
      '<h2 class="page-title">Guardado EDIT</h2>' +
      '<p class="page-sub">Tu EDIT00000000: cargarlo, desbloquearlo para tu usuario, editar su descripción y guardar con respaldo automático.</p>' +
      '<div class="card" style="max-width:820px">' +
      '<div class="field"><label>Archivo EDIT</label><div style="display:flex; gap:8px">' +
      '<input class="input" id="edPath" value="' + esc(cfg.editFile || "") + '" placeholder="Ruta a EDIT00000000">' +
      '<button class="btn btn-ghost" id="edPick" style="flex:0 0 auto">…</button>' +
      '<button class="btn btn-ghost" id="edScan" style="flex:0 0 auto">Buscar</button></div>' +
      '<span class="hint" id="edScanResult"></span></div>' +
      '<div class="btn-row"><button class="btn btn-primary" id="edLoad">📂 Cargar EDIT</button></div>' +
      '<div id="edBody"></div>' +
      "</div>";

    page.querySelector("#edPick").addEventListener("click", function () {
      rpc("pickFile", { title: "Elegí el EDIT00000000", initial: page.querySelector("#edPath").value }).then(function (r) {
        if (r.path) page.querySelector("#edPath").value = r.path;
      });
    });
    page.querySelector("#edScan").addEventListener("click", function () {
      rpc("scanEditFiles").then(function (r) {
        page.querySelector("#edScanResult").textContent = r.files.length ? r.files.join(" · ") : "Ningún EDIT encontrado en Documentos\\KONAMI";
        if (r.files.length) page.querySelector("#edPath").value = r.files[0];
      }).catch(function (e) { toast(e.message, true); });
    });
    page.querySelector("#edLoad").addEventListener("click", function () {
      rpc("editLoad", { path: page.querySelector("#edPath").value }).then(function (r) {
        editInfo = r.edit;
        toast("EDIT cargado (" + editInfo.game + ").");
        renderInfo();
        return rpc("saveConfig", { editFile: editInfo.path });
      }).catch(function (e) { toast(e.message, true); });
    });

    function renderInfo() {
      var e = editInfo, body = page.querySelector("#edBody");
      if (!e) { body.innerHTML = ""; return; }
      body.innerHTML =
        '<div style="border-top:1px solid var(--border); padding-top:16px; display:flex; flex-direction:column; gap:12px">' +
        (e.logoPng ? '<img src="data:image/png;base64,' + e.logoPng + '" style="max-width:220px; border-radius:12px; border:1px solid var(--border)">' : "") +
        '<div class="kv"><span>Juego</span><code>' + esc(e.game) + "</code></div>" +
        '<div class="kv"><span>Ruta</span><code>' + esc(e.path) + "</code></div>" +
        '<div class="kv"><span>Tamaño de datos</span><code>' + (e.dataSize ? Math.round(e.dataSize / 1024) + " KB" : "—") + "</code></div>" +
        '<div class="kv"><span>Serial / SID actual</span><code>' + esc(e.serial || "—") + "</code></div>" +
        '<div class="field"><label>Descripción del guardado</label><div style="display:flex; gap:8px">' +
        '<input class="input" id="edDesc" value="' + esc(e.description || "") + '"><button class="btn btn-ghost" id="edSetDesc" style="flex:0 0 auto">Aplicar</button></div></div>' +
        '<div class="field"><label>SID personalizado</label><div style="display:flex; gap:8px">' +
        '<input class="input" id="edSid" placeholder="S-1-5-21-…"><button class="btn btn-ghost" id="edSetSid" style="flex:0 0 auto">Aplicar SID</button></div></div>' +
        '<div class="btn-row">' +
        '<button class="btn btn-save" style="width:auto" id="edSave">💾 Guardar EDIT</button>' +
        '<button class="btn btn-ghost" id="edUnlock">🔓 Desbloquear para mi usuario</button>' +
        '<button class="btn btn-ghost" id="edInspect">🔍 Inspeccionar (FL26)</button>' +
        '<button class="btn btn-ghost" id="edExport">📤 Exportar data.dat</button>' +
        '<button class="btn btn-ghost" id="edImport">📥 Importar data.dat</button>' +
        '<button class="btn btn-ghost" id="edRestore">↩️ Restaurar backup</button>' +
        "</div>" +
        '<div style="border:1px solid var(--border); border-radius:8px; padding:12px">' +
        '<b>🧩 Persistencia extendida FL26EDIT (clubes con ID ≥ 750 en Modo Editar)</b>' +
        '<p class="mut small" style="margin:6px 0 0">fl26edit.dll expande el Modo Editar a 2.048 clubes y guarda los clubes con ID ≥ 750 en <code>EDIT00000000.fl26x</code>, sin romper el guardado original. fl26editlist.lua inyecta tus ligas en los menús de Editar.</p>' +
        '<div id="edFl26Status" class="small" style="margin-top:8px">Archivo secundario (.fl26x): consultando…</div>' +
        '<div class="btn-row" style="margin-top:10px">' +
        '<button class="btn btn-ghost" id="edFl26Audit">🔍 Auditar .fl26x</button>' +
        '<button class="btn btn-ghost" id="edFl26Restore">🔄 Restaurar .prev</button>' +
        '<button class="btn btn-save" style="width:auto" id="edFl26Deploy">🚀 Desplegar a Sider</button>' +
        "</div></div><div id='edInfo'></div></div>";

      body.querySelector("#edSetDesc").addEventListener("click", function () {
        rpc("editSetDescription", { text: body.querySelector("#edDesc").value }).then(function (r) { editInfo = r.edit; toast("Descripción aplicada (guardá el EDIT)."); }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edSetSid").addEventListener("click", function () {
        rpc("editSetSid", { sid: body.querySelector("#edSid").value }).then(function (r) { editInfo = r.edit; renderInfo(); toast("SID aplicado (guardá el EDIT)."); }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edUnlock").addEventListener("click", function () {
        rpc("editUnlock").then(function (r) { editInfo = r.edit; renderInfo(); toast("EDIT desbloqueado para tu usuario de Windows (guardá el EDIT)."); }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edSave").addEventListener("click", function () {
        rpc("editSave").then(function () { toast("EDIT guardado (con backup automático)."); }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edInspect").addEventListener("click", function () {
        rpc("editInspectFl26", { path: e.path }).then(function (r) {
          var i = r.info || {};
          body.querySelector("#edInfo").innerHTML = '<div class="warn-box">Inspección: ' + esc(i.status || "sin datos") +
            "<br>Magic: " + esc(i.magic || "—") + " · Versión: " + (i.version || "—") + " · Válido: " + (i.isValid ? "sí" : "no") +
            "<br>Equipos: " + (i.teams || 0) + " · Plantillas: " + (i.squads || 0) + " · Tácticas: " + (i.tactics || 0) + " · DTs: " + (i.managers || 0) +
            "<br>Backup previo: " + (i.hasBackup ? "sí" : "no") + "</div>";
        }).catch(function (err) { toast(err.message, true); });
      });
      function fl26Status() {
        rpc("editInspectFl26", { path: e.path }).then(function (r) {
          var i = r.info || {};
          var st = body.querySelector("#edFl26Status");
          if (!st) return;
          if (!i.exists) st.innerHTML = "Archivo secundario (.fl26x): <b>NO DETECTADO</b> (todavía no hay cambios en clubes ≥ 750).";
          else st.innerHTML = "Archivo secundario (.fl26x): <b>" + (i.isValid ? "VÁLIDO Y SINCRONIZADO" : "CORRUPTO O NO RECONOCIDO") + "</b> · " + Math.round((i.fileSize || 0) / 1024) + " KB · clubes: " + (i.teams || 0) + " · plantillas: " + (i.squads || 0) + " · tácticas: " + (i.tactics || 0) + " · copia previa (.prev): " + (i.hasBackup ? "DISPONIBLE" : "no existe");
        }).catch(function () { });
      }
      fl26Status();
      body.querySelector("#edFl26Audit").addEventListener("click", function () {
        rpc("editInspectFl26", { path: e.path }).then(function (r) {
          var i = r.info || {};
          A.showModal("Auditoría del contenedor .fl26x",
            '<div class="kv"><span>Estado en disco</span><code>' + (!i.exists ? "NO DETECTADO (sin cambios en clubes ≥ 750 aún)" : (i.isValid ? "VÁLIDO Y SINCRONIZADO" : "CORRUPTO O NO RECONOCIDO")) + "</code></div>" +
            (i.exists ? '<div class="kv"><span>Tamaño en disco</span><code>' + (i.fileSize || 0).toLocaleString("es-AR") + " bytes</code></div>" +
            '<div class="kv"><span>Firma mágica</span><code>' + esc(i.magic || "—") + " (Versión " + (i.version || "—") + " · Motor 2.048 Clubes)</code></div>" +
            '<div class="kv"><span>Clubes adicionales (&gt; 749)</span><code>' + (i.teams || 0) + "</code></div>" +
            '<div class="kv"><span>Plantillas de jugadores</span><code>' + (i.squads || 0) + "</code></div>" +
            '<div class="kv"><span>Tácticas y alineaciones</span><code>' + (i.tactics || 0) + "</code></div>" +
            ((i.version === 2) ? '<div class="kv"><span>Cuerpos técnicos</span><code>' + (i.managers || 0) + "</code></div>" : "") : "") +
            '<div class="kv"><span>Copia previa (.fl26x.prev)</span><code>' + (i.hasBackup ? "DISPONIBLE" : "NO EXISTE") + "</code></div>",
            [{ label: "Cerrar" }]);
        }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edFl26Restore").addEventListener("click", function () {
        A.confirmModal("Restaurar .fl26x.prev", "Se restaura la copia previa del contenedor extendido (.fl26x.prev). ¿Continuar?", "Restaurar", function () {
          rpc("editRestoreBackup", { path: e.path }).then(function (r) { toast(r.message || "Copia restaurada."); fl26Status(); }).catch(function (err) { toast(err.message, true); });
        });
      });
      body.querySelector("#edFl26Deploy").addEventListener("click", function () {
        rpc("deploySider").then(function (r) { toast(r.message || "Ecosistema Sider desplegado (incluye fl26edit y fl26editlist)."); }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edExport").addEventListener("click", function () {
        rpc("saveFile", { title: "Exportar data.dat", defaultName: "data.dat" }).then(function (r) {
          if (!r.path) return;
          return rpc("editExportDataFile", { path: r.path });
        }).then(function (r) { if (r) toast("data.dat exportado."); }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edImport").addEventListener("click", function () {
        rpc("pickFile", { title: "Importar data.dat" }).then(function (r) {
          if (!r.path) return;
          return rpc("editImportDataFile", { path: r.path });
        }).then(function (r) { if (r) { editInfo = r.edit; renderInfo(); toast("data.dat importado (guardá el EDIT)."); } }).catch(function (err) { toast(err.message, true); });
      });
      body.querySelector("#edRestore").addEventListener("click", function () {
        A.confirmModal("Restaurar backup", "Se restaura el backup previo del EDIT (si existe). ¿Continuar?", "Restaurar", function () {
          rpc("editRestoreBackup", { path: e.path }).then(function (r) {
            toast(r.message || (r.restored ? "Backup restaurado." : "No se pudo restaurar."), !r.restored);
          }).catch(function (err) { toast(err.message, true); });
        });
      });
    }

    if (editInfo) renderInfo();
    return Promise.resolve();
  });
})();
