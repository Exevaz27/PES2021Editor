// ============================================================
// Seccion: Ligas y Copas (explorador master-detail completo)
// ============================================================
(function () {
  "use strict";
  var A = window.App, rpc = A.rpc, esc = A.esc, toast = A.toast;

  var filter = "all", search = "", selectedId = null, detail = null;

  function ensureCountries() {
    if (A.S.countries && A.S.countries.length) return Promise.resolve(A.S.countries);
    return rpc("createLeagueOptions").then(function (r) { A.S.countries = r.countries || []; return A.S.countries; })
      .catch(function () { return []; });
  }

  function countryOptions(sel) {
    var opts = '<option value="0">— Sin país —</option>';
    (A.S.countries || []).forEach(function (c) {
      opts += '<option value="' + c.id + '"' + (c.id === sel ? " selected" : "") + ">" + esc(c.name) + "</option>";
    });
    return opts;
  }
  A.countryOptions = countryOptions; A.ensureCountries = ensureCountries;

  A.register("ligas", function (page) {
    if (!A.S.dbLoaded) {
      page.innerHTML = '<div class="empty-state"><div class="empty-ico">🏆</div><h2>Sin base cargada</h2><p>Cargá tu pesdb desde Configuración para explorar las ligas y copas.</p></div>';
      return Promise.resolve();
    }
    page.innerHTML =
      '<div class="md"><div class="md-left"><div class="list-panel">' +
      '<div class="lp-head">' +
      '<input class="input" id="lgSearch" placeholder="Buscar liga o copa…" value="' + esc(search) + '">' +
      '<div class="chips">' +
      chip("all", "Todas") + chip("leagues", "Ligas") + chip("cups", "Copas") +
      "</div>" +
      '<div class="mut" id="lgCount"></div>' +
      "</div>" +
      '<div class="lp-body" id="lgList"></div>' +
      "</div></div>" +
      '<div class="md-right" id="lgDetail"><div class="empty-state"><div class="empty-ico">👈</div><h2>Elegí una competición</h2><p>Seleccioná una liga o copa de la lista para ver su ficha completa y editarla.</p></div></div>' +
      "</div>";

    function chip(f, label) {
      return '<button class="chip' + (filter === f ? " on" : "") + '" data-f="' + f + '">' + label + "</button>";
    }

    A.$$(".chip", page).forEach(function (c) {
      c.addEventListener("click", function () {
        filter = c.getAttribute("data-f");
        A.$$(".chip", page).forEach(function (x) { x.classList.toggle("on", x === c); });
        loadList();
      });
    });
    var searchInput = page.querySelector("#lgSearch");
    var deb = null;
    searchInput.addEventListener("input", function () {
      clearTimeout(deb);
      deb = setTimeout(function () { search = searchInput.value; loadList(); }, 220);
    });

    function loadList() {
      rpc("listCompetitions", { filter: filter, search: search }).then(function (r) {
        var list = page.querySelector("#lgList");
        page.querySelector("#lgCount").textContent = r.total + " competiciones";
        list.innerHTML = "";
        r.competitions.forEach(function (c) {
          var b = A.el(
            '<button class="row' + (c.id === selectedId ? " sel" : "") + '" data-id="' + c.id + '">' +
            A.crestHtml(c.id, c.name, "") +
            '<span><span class="nm-line"><span class="nm">' + esc(c.name) + "</span>" +
            (c.isNew ? '<span class="pill new">NUEVA</span>' : (c.isCustom ? '<span class="pill custom">CUSTOM</span>' : "")) +
            "</span>" +
            '<span class="sub">' + esc(c.typeName) + " · " + c.teamCount + " equipos" + (c.countryName ? " · " + esc(c.countryName) : "") + "</span></span>" +
            '<span class="r-meta">ID ' + c.id + "</span></button>");
          b.addEventListener("click", function () { selectComp(c.id); });
          list.appendChild(b);
        });
        if (selectedId != null && !page.querySelector("#lgDetail .detail-head")) selectComp(selectedId);
      }).catch(function (e) { toast(e.message, true); });
    }

    function selectComp(id) {
      selectedId = id;
      A.$$(".row", page).forEach(function (x) { x.classList.toggle("sel", Number(x.getAttribute("data-id")) === id); });
      rpc("getCompetition", { id: id }).then(function (r) {
        detail = r.competition;
        return ensureCountries();
      }).then(function () { renderDetail(); }).catch(function (e) { toast(e.message, true); });
    }

    function renderDetail() {
      var d = detail, box = page.querySelector("#lgDetail");
      var typePill = d.isLeague ? "" : '<span class="pill cup">' + esc(d.typeName).toUpperCase() + "</span>";
      box.innerHTML =
        '<div class="detail-head">' +
        '<div class="crest big" id="lgEmblem" style="--ca:' + A.crestColor(d.id) + ';--cb:' + A.crestColor2(d.id) + '">' + esc(d.name.substring(0, 2).toUpperCase()) + "</div>" +
        '<div class="dh-main"><div class="detail-title">' + esc(d.name) + "</div>" +
        '<div class="detail-meta"><span>ID <b>' + d.id + "</b></span><span>·</span><span>Reglamento <b>" + d.regId + "</b></span><span>·</span><span>" + esc(d.typeName) + "</span>" +
        (d.countryName ? "<span>·</span><span>🏳️ <b>" + esc(d.countryName) + "</b></span>" : "") +
        "<span>·</span><span><b>" + d.teamCount + "</b> equipos</span>" + (d.tier ? "<span>·</span><span>Nivel <b>" + d.tier + "</b></span>" : "") +
        "</div>" +
        '<div class="btn-row" style="margin-top:14px">' +
        '<button class="btn btn-ghost sm" id="lgPickEmblem">🖼️ Cambiar escudo</button>' +
        '<button class="btn btn-ghost sm" id="lgPickGame">🖼️ Elegir del Juego…</button>' +
        '<button class="btn btn-ghost sm" id="lgClearEmblem">🗑️ Quitar logo</button>' +
        '<button class="btn btn-ghost sm" id="lgRepair">🩺 Reparar Siluetas</button>' +
        '<button class="btn btn-ghost sm" id="lgTemplate">📋 Crear desde esta plantilla</button>' +
        '<button class="btn btn-ghost sm" id="lgExport">📤 Exportar paquete</button>' +
        '<button class="btn btn-ghost sm" id="lgImport">📥 Importar paquete</button>' +
        (d.protected ? "" : '<button class="btn danger sm" id="lgDelete">🗑️ Eliminar competición</button>') +
        "</div></div>" + typePill + "</div>" +

        '<div class="card-sec"><h3>Nombres oficiales</h3><div class="form-grid">' +
        field("Nombre en español", "fNameSpanish", d.nameSpanish) +
        field("Nombre en inglés", "fNameEnglish", d.nameEnglish) +
        field("Nombre latinoamericano", "fNameLatam", d.nameLatam) +
        field("Nombre interno (ASCII)", "fNameAscii", d.nameAscii) +
        "</div></div>" +

        '<div class="card-sec"><h3>Formato y reglamento</h3><div class="form-grid3">' +
        numField("Equipos (aforo)", "fTotalTeams", d.totalTeams, 2, 63) +
        numField("Sustituciones", "fChanges", d.changes, 0, 9) +
        numField("Jugadores registrados", "fRegistered", d.registeredPlayers, 11, 40) +
        numField("Rondas / partidos entre sí", "fRounds", d.rounds, 1, 4) +
        numField("Grupos (copas)", "fNumGroups", d.numGroups, 1, 16) +
        numField("Clasificados", "fNumClassified", d.numClassified, 0, 64) +
        '</div><div style="margin-top:12px">' +
        check("fExtraTime", "Prórroga", d.extraTime) + check("fPenalties", "Definición por penales", d.penalties) +
        check("fAwayGoals", "Gol de visitante", d.awayGoals) + check("fThirdPlace", "Partido por el 3er puesto", d.thirdPlace) +
        check("fSingleLeg", "Partido único (sin ida y vuelta)", d.singleLeg) + check("fFinalChamp", "Campeonato final / playoff", d.finalChampionship) +
        "</div></div>" +

        '<div class="card-sec"><h3>Sistema de liga</h3><div class="form-grid">' +
        '<div class="field"><label>País / bandera</label><select class="input" id="fCountry">' + countryOptions(d.countryId) + "</select></div>" +
        '<div class="field"><label>Temporada</label><select class="input" id="fSeason"><option value="0"' + (d.seasonType === 0 ? " selected" : "") + '>Europa (ago–may)</option><option value="1"' + (d.seasonType === 1 ? " selected" : "") + '>Año calendario (feb–dic)</option></select></div>' +
        numField("Ascensos", "fPromote", d.promote, 0, 10) +
        numField("Descensos", "fDemote", d.demote, 0, 10) +
        numField("Plazas continental 1 (Champions/Libertadores)", "fCont1", d.continental1, 0, 10) +
        numField("Plazas continental 2 (Europa/Sudamericana)", "fCont2", d.continental2, 0, 10) +
        '<div class="field"><label>Copa nacional vinculada</label><select class="input" id="fLinkedCup">' + cupOptions(d.availableCups, d.linkedCup) + "</select></div>" +
        '<div class="field"><label>Supercopa vinculada</label><select class="input" id="fLinkedScup">' + cupOptions(d.availableSuperCups, d.linkedSuperCup) + "</select></div>" +
        '<div class="field" style="grid-column: 1 / -1">' +
        check("fApClau", "Formato Apertura / Clausura (Tabla Anual Acumulada)", d.aperturaClausura) +
        '<p class="mut small" style="margin-top:2px; margin-bottom:8px">Divide la temporada en dos torneos semestrales (Apertura y Clausura) con cómputo anual unificado para ascensos, descensos y copas.</p></div>' +
        '<div class="field"><label>División superior en la pirámide (D1 ⇄ D2 ⇄ D3…)</label><select class="input" id="fParentComp">' + parentOptions(d.parentLeagues, d.parentCompId) + '</select></div>' +
        '<div class="field"><label>Nivel en la pirámide</label><input class="input" readonly style="opacity:0.85; background:var(--bg2,#1f293d)" value="Tier ' + (d.tier || 1) + ' · ' + (d.tier === 1 ? '1ª División / Independiente' : (d.tier === 2 ? '2ª División' : (d.tier + 'ª División'))) + '"></div>' +
        "</div></div>" +

        '<div class="card-sec"><h3>Equipos participantes <span class="sub">' + d.teams.length + " de " + d.totalTeams + '</span></h3>' +
        '<div id="lgTeams" style="border:1px solid var(--border); border-radius:8px; overflow:hidden">' + d.teams.map(function (t) {
          return '<div class="row" style="cursor:default">' + A.crestHtml(t.id, t.name, t.shortName) +
            '<span><span class="nm">' + esc(t.name) + '</span><span class="sub">Posición ' + t.position + '</span></span>' +
            '<span style="display:flex; gap:6px"><button class="btn btn-ghost xs" data-move="' + t.id + '" data-mname="' + esc(t.name) + '">🚀 Mover</button>' +
            '<button class="btn danger xs" data-remove="' + t.id + '">Quitar</button></span></div>';
        }).join("") + "</div>" +
        '<div style="display:flex; gap:8px; margin-top:12px"><input class="input" id="lgAddSearch" placeholder="Buscar equipo para agregar…"><select class="input" id="lgAddSelect" style="flex:1"></select><button class="btn btn-primary" id="lgAddBtn">Agregar</button></div>' +
        "</div>" +

        '<div class="btn-row"><button class="btn btn-save" style="width:auto; padding:13px 22px" id="lgSave">💾 Guardar cambios de la competición</button></div>';

      function field(label, id, val) {
        return '<div class="field"><label>' + label + '</label><input class="input" id="' + id + '" value="' + esc(val || "") + '"></div>';
      }
      function numField(label, id, val, min, max) {
        return '<div class="field"><label>' + label + '</label><input class="input" type="number" id="' + id + '" value="' + (val == null ? "" : val) + '" min="' + min + '" max="' + max + '"></div>';
      }
      function check(id, label, on) {
        return '<label class="check-line"><input type="checkbox" id="' + id + '"' + (on ? " checked" : "") + "> " + label + "</label>";
      }
      function parentOptions(list, sel) {
        var o = '<option value="0">— Ninguna (1ª División / Independiente) —</option>';
        (list || []).forEach(function (c) { o += '<option value="' + c.id + '"' + (c.id === sel ? " selected" : "") + ">" + esc(c.name) + " (ID " + c.id + ")</option>"; });
        return o;
      }
      function cupOptions(list, sel) {
        var o = '<option value="0">— Ninguna —</option>';
        (list || []).forEach(function (c) { o += '<option value="' + c.regId + '"' + (c.regId === sel ? " selected" : "") + ">" + esc(c.name) + "</option>"; });
        return o;
      }

      // escudo real si existe
      rpc("compEmblem", { id: d.id }).then(function (r) {
        var box2 = page.querySelector("#lgEmblem");
        if (r.png && box2) box2.innerHTML = '<img src="data:image/png;base64,' + r.png + '" style="width:100%;height:100%;object-fit:contain">';
      }).catch(function () { });

      page.querySelector("#lgPickEmblem").addEventListener("click", function () {
        rpc("pickFile", { title: "Elegí la imagen del escudo", filter: "Imágenes (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp" }).then(function (r) {
          if (!r.path) return;
          return rpc("setCompEmblemFile", { path: r.path, id: d.id });
        }).then(function (r) { if (r) { toast("Escudo actualizado."); selectComp(d.id); } }).catch(function (e) { toast(e.message, true); });
      });

      page.querySelector("#lgPickGame").addEventListener("click", function () {
        A.pickGameEmblem(function (it) {
          rpc("setCompEmblemFromGame", { gameCompId: it.compId, id: d.id }).then(function () { toast("Logo del juego asignado."); selectComp(d.id); }).catch(function (e) { toast(e.message, true); });
        });
      });
      page.querySelector("#lgClearEmblem").addEventListener("click", function () {
        A.confirmModal("Quitar logo", "Se elimina el logo personalizado de esta competición en LiveCPK. ¿Continuar?", "Quitar", function () {
          rpc("deleteCompEmblem", { id: d.id }).then(function (r) { toast(r.deleted ? "Logo eliminado." : "No había logo personalizado."); selectComp(d.id); }).catch(function (e) { toast(e.message, true); });
        });
      });
      page.querySelector("#lgRepair").addEventListener("click", function () {
        rpc("repairLiveCpkEmblems", {}).then(function (r) { toast("Diagnóstico completo: se verificaron y repararon " + r.repaired + " escudo(s) en Sider LiveCPK."); selectComp(d.id); }).catch(function (e) { toast(e.message, true); });
      });
      page.querySelector("#lgTemplate").addEventListener("click", function () {
        A.S.leagueTemplate = d.id;
        toast("Plantilla lista: en Crear Liga se copian el formato y el sistema de " + d.name + ".");
        A.goto("crear");
      });
      page.querySelector("#lgExport").addEventListener("click", function () {
        rpc("saveFile", { title: "Exportar paquete de liga", filter: "Paquetes de liga (*.pesleague)|*.pesleague", defaultName: "liga_" + d.id + ".pesleague" }).then(function (r) {
          if (!r.path) return;
          return rpc("exportLeaguePackageFile", { path: r.path, compId: d.id });
        }).then(function (r) { if (r) toast(r.message || "Paquete exportado."); }).catch(function (e) { toast(e.message, true); });
      });
      page.querySelector("#lgImport").addEventListener("click", function () {
        rpc("pickFile", { title: "Importar paquete de liga", filter: "Paquetes de liga (*.pesleague)|*.pesleague" }).then(function (r) {
          if (!r.path) return;
          return rpc("importLeaguePackageFile", { path: r.path });
        }).then(function (r) {
          if (!r) return;
          toast("Paquete importado: " + r.teamsMapped + " equipos mapeados.");
          loadList();
        }).catch(function (e) { toast(e.message, true); });
      });
      var del = page.querySelector("#lgDelete");
      if (del) del.addEventListener("click", function () {
        A.confirmModal("Eliminar competición", "Se elimina «" + d.name + "» (ID " + d.id + ") con su reglamento y sus entradas. Las competiciones originales están protegidas. ¿Seguro?", "Eliminar", function () {
          rpc("deleteCompetition", { id: d.id }).then(function () {
            toast("Competición eliminada."); selectedId = null; loadList();
            page.querySelector("#lgDetail").innerHTML = '<div class="empty-state"><div class="empty-ico">🗑️</div><h2>Eliminada</h2><p>Elegí otra competición de la lista.</p></div>';
          }).catch(function (e) { toast(e.message, true); });
        });
      });

      // participantes: buscar y agregar
      var addSearch = page.querySelector("#lgAddSearch"), addSelect = page.querySelector("#lgAddSelect");
      var deb2 = null;
      function refreshAddOptions() {
        rpc("listTeams", { search: addSearch.value, limit: 120 }).then(function (r) {
          addSelect.innerHTML = r.teams.map(function (t) { return '<option value="' + t.id + '">' + esc(A.teamName(t)) + " (ID " + t.id + ")</option>"; }).join("");
        }).catch(function () { });
      }
      addSearch.addEventListener("input", function () { clearTimeout(deb2); deb2 = setTimeout(refreshAddOptions, 250); });
      refreshAddOptions();
      page.querySelector("#lgAddBtn").addEventListener("click", function () {
        var tid = Number(addSelect.value);
        if (!tid) return;
        rpc("addTeamToCompetition", { compId: d.id, teamId: tid }).then(function () {
          toast("Equipo agregado."); selectComp(d.id); loadList();
        }).catch(function (e) { toast(e.message, true); });
      });
      A.$$("[data-move]", page).forEach(function (b) {
        b.addEventListener("click", function () {
          var tid = Number(b.getAttribute("data-move"));
          var opts = '<option value="0">— Sin liga (Otros Equipos / libre) —</option>' + (d.parentLeagues || []).filter(function (c) { return c.id !== d.id; }).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + " (ID " + c.id + ")</option>"; }).join("");
          A.showModal("🚀 Mover Club a una Liga", '<div class="field"><label>Destino de ' + esc(b.getAttribute("data-mname") || "el club") + '</label><select class="input" id="mvTarget">' + opts + "</select></div>" +
            '<p class="mut small">Mover desvincula al club de su liga doméstica actual y lo asigna a la elegida (nunca queda en dos ligas).</p>',
            [{ label: "Cancelar" }, { label: "Mover", cls: "btn-primary", onClick: function () {
              var target = Number(document.querySelector("#mvTarget").value) || 0;
              rpc("assignTeamToLeague", { teamIds: [tid], compId: target }).then(function () { toast("Club movido."); selectComp(d.id); loadList(); }).catch(function (e) { toast(e.message, true); });
            } }]);
        });
      });
      A.$$("[data-remove]", page).forEach(function (b) {
        b.addEventListener("click", function () {
          rpc("removeTeamFromCompetition", { compId: d.id, teamId: Number(b.getAttribute("data-remove")) }).then(function () {
            toast("Equipo quitado."); selectComp(d.id); loadList();
          }).catch(function (e) { toast(e.message, true); });
        });
      });

      page.querySelector("#lgSave").addEventListener("click", function () {
        function val(id) { var n = page.querySelector("#" + id); return n ? n.value : ""; }
        function num(id) { return Number(val(id)) || 0; }
        function chk(id) { var n = page.querySelector("#" + id); return n ? n.checked : false; }
        rpc("updateCompetition", {
          id: d.id,
          nameSpanish: val("fNameSpanish"), nameEnglish: val("fNameEnglish"), nameLatam: val("fNameLatam"), nameAscii: val("fNameAscii"),
          totalTeams: num("fTotalTeams"), changes: num("fChanges"), registeredPlayers: num("fRegistered"), rounds: num("fRounds"),
          numGroups: num("fNumGroups"), numClassified: num("fNumClassified"),
          extraTime: chk("fExtraTime"), penalties: chk("fPenalties"), awayGoals: chk("fAwayGoals"),
          thirdPlace: chk("fThirdPlace"), singleLeg: chk("fSingleLeg"), finalChampionship: chk("fFinalChamp"),
          countryId: num("fCountry"), seasonType: num("fSeason"),
          promote: num("fPromote"), demote: num("fDemote"), continental1: num("fCont1"), continental2: num("fCont2"),
          linkedCup: num("fLinkedCup"), linkedSuperCup: num("fLinkedScup"), parentCompId: num("fParentComp"), aperturaClausura: chk("fApClau")
        }).then(function () {
          toast("Cambios guardados en la competición (se escriben al Guardar (.bin)).");
          return A.reloadState();
        }).then(function () { selectComp(d.id); loadList(); })
          .catch(function (e) { toast(e.message, true); });
      });
    }

    if (A.S.pendingCompId != null) { selectedId = A.S.pendingCompId; A.S.pendingCompId = null; }
    loadList();
    return Promise.resolve();
  });
})();
