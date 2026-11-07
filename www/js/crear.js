// ============================================================
// Seccion: Crear Liga (flujo completo, sin reemplazar ninguna)
// ============================================================
(function () {
  "use strict";
  var A = window.App, rpc = A.rpc, esc = A.esc, toast = A.toast;
  var options = null, picked = {}; // teamId -> true
  // Cantidad de Equipos: mismos presets que la version de escritorio (cmbTeamCount)
  var TEAM_COUNT_PRESETS = [[2, "2 equipos (Supercopa)"], [4, "4 equipos (Cuadrangular)"], [6, "6 equipos"], [8, "8 equipos"], [10, "10 equipos (Suiza / Corto)"], [12, "12 equipos (Escocia)"], [14, "14 equipos"], [16, "16 equipos (Chile)"], [18, "18 equipos (Alemania)"], [20, "20 equipos (Premier / La Liga / Serie A)"], [22, "22 equipos (La Liga 2)"], [24, "24 equipos (Championship)"], [26, "26 equipos (Liga Argentina Clasica)"], [28, "28 equipos (Liga Argentina 28)"], [30, "30 equipos (Liga Argentina 30 / MLS)"], [32, "32 equipos (Mundial / Champions)"]];

  A.register("crear", function (page) {
    if (!A.S.dbLoaded) {
      page.innerHTML = '<div class="empty-state"><div class="empty-ico">➕</div><h2>Sin base cargada</h2><p>Cargá tu pesdb desde Configuración para crear ligas nuevas.</p></div>';
      return Promise.resolve();
    }
    return rpc("createLeagueOptions").then(function (r) {
      options = r;
      A.S.countries = r.countries || [];
      render(page);
    }).catch(function (e) { toast(e.message, true); });
  });

  function render(page) {
    var ids = options.availableIds || [];
    page.innerHTML =
      '<h2 class="page-title">Crear una liga nueva</h2>' +
      '<p class="page-sub">Se agrega a tu base <b>sin reemplazar ninguna existente</b>: el ID se elige entre los libres y los equipos salen de los libres o de los que vos selecciones.</p>' +

      '<div class="card-sec"><h3>1 · Datos de la competición</h3><div class="form-grid">' +
      fld("Nombre en español *", '<input class="input" id="cNameSpanish" placeholder="PRIMERA B NACIONAL">') +
      fld("Nombre en inglés", '<input class="input" id="cNameEnglish" placeholder="Primera B Nacional">') +
      fld("Nombre latinoamericano", '<input class="input" id="cNameLatam">') +
      fld("Nombre interno (ASCII, sin espacios)", '<input class="input" id="cNameAscii" placeholder="PRIMERA_B_NACIONAL">') +
      fld("ID de competición (libre)", '<select class="input" id="cCompId">' + ids.map(function (i) { return '<option value="' + i + '">' + i + "</option>"; }).join("") + "</select>") +
      fld("Tipo", '<select class="input" id="cCompType"><option value="0">Liga</option><option value="1">Copa</option><option value="2">Supercopa</option></select>') +
      fld("País", '<select class="input" id="cCountry">' + '<option value="0">— Internacional / sin país —</option>' + (options.countries || []).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + "</option>"; }).join("") + "</select>") +
      fld("Cantidad de Equipos", '<select class="input" id="cTeamCount">' + TEAM_COUNT_PRESETS.map(function (p) { return '<option value="' + p[0] + '"' + (p[0] === 20 ? " selected" : "") + ">" + p[1] + "</option>"; }).join("") + '<option value="custom">Otro numero (escribir)...</option></select>') +
      '<div class="field" id="cTeamCountCustomWrap" style="display:none"><label>Cantidad exacta (2 a 60)</label><input class="input" type="number" id="cTeamCountCustom" value="20" min="2" max="60"></div>' +
      fld("Continente", '<select class="input" id="cContinent"><option value="1">Otros / Mundial</option><option value="2">Europa</option><option value="3">Asia-Oceanía</option><option value="4" selected>América del Sur</option><option value="5">Norteamérica</option><option value="6">África</option></select>') +
      "</div>" +
      '<label class="check-line"><input type="checkbox" id="cIndependent" checked> Liga independiente (no es división de otra)</label>' +
      fld("Liga madre (si es división B/C…)", '<select class="input" id="cParent"><option value="0">— Ninguna —</option>' + (options.parentLeagues || []).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + " (ID " + c.id + ")</option>"; }).join("") + "</select>") +
      "</div>" +
      '<div style="display:flex; gap:8px; align-items:end; margin-top:12px"><div class="field" style="flex:1; margin:0"><label>📋 Clonar Plantilla (copia formato y sistema de una liga base)</label><select class="input" id="cTemplate"><option value="0">— Elegir liga base —</option>' + (options.parentLeagues || []).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + " (ID " + c.id + ")</option>"; }).join("") + '</select></div>' +
      '<button class="btn btn-ghost" id="cApplyTemplate" style="flex:none">✓ Aplicar como Plantilla</button></div>' +

      '<div class="card-sec" style="margin-top:16px"><h3>2 · Formato</h3>' +
      fld("Formato de Torneo (preset)", '<select class="input" id="cFmtPreset">' +
        '<option value="0">🏆 Liga Regular Round-Robin (Ida y Vuelta Tradicional)</option>' +
        '<option value="1">⚡ Torneo Corto / Apertura-Clausura (1 Sola Rueda)</option>' +
        '<option value="2">🇦🇷 Copa de la Liga / 2 Zonas + Playoffs (14/15 por Zona)</option>' +
        '<option value="3">🇺🇸 Formato Conferencias con Playoffs (2 Zonas + Playoffs)</option>' +
        '<option value="4">⚙️ Personalizado (Ajuste Manual)</option></select>') +
      '<div class="form-grid3">' +
      fld("Sustituciones", '<select class="input" id="cChanges"><option value="3">3 cambios (Clásico)</option><option value="5" selected>5 cambios (IFAB / FIFA Moderno)</option><option value="6">6 cambios (Amistosos / Prórroga)</option></select>') +
      fld("Banco de Suplentes", '<select class="input" id="cRegistered"><option value="18">7 suplentes (18 en planilla - Clásico)</option><option value="20">9 suplentes (20 en planilla)</option><option value="23" selected>12 suplentes (23 en planilla - Serie A / FIFA)</option><option value="26">15 suplentes (26 en planilla - Qatar / Moderno)</option></select>') +
      fld("Rondas / Partidos", '<select class="input" id="cRounds"><option value="1">1 ronda (Todos contra todos único)</option><option value="2" selected>2 rondas (Ida y Vuelta tradicional)</option><option value="3">3 rondas (Torneo corto / 3 ruedas)</option><option value="4">4 rondas (Liga reducida / 4 ruedas)</option></select>') +
      fld("Criterio de Desempate", '<select class="input" id="cTie"><option value="1">Diferencia de Gol (GD - Premier/Bundesliga)</option><option value="2">Enfrentamiento Directo (H2H - La Liga)</option><option value="3">H2H + Dif. de Gol (Serie A TIM)</option><option value="4">Mayor Cantidad de Victorias (Ligue 1)</option><option value="0">Puntos / General (Por Defecto)</option></select>') +
      fld("Formato Llave", '<select class="input" id="cCupFormat"><option value="0">0 - Partido Único (Sede Neutral)</option><option value="1">1 - Doble Partido (Ida y Vuelta)</option><option value="2">2 - Ida y Vuelta con Final Neutral</option><option value="3">3 - Doble Partido con Final Ida/Vuelta</option></select>') +
      num("Zonas / Grupos", "cNumGroups", 1, 1, 16) + num("Clasif. Playoffs (por zona)", "cNumClassified", 0, 0, 64) +
      '</div><div style="margin-top:10px">' +
      chk("cFinalChamp", "Playoffs / Fase Final Eliminatoria (cruces directos por el título)") + "<br>" +
      chk("cExtraTime", "Prórroga") + chk("cPenalties", "Penales") + chk("cAwayGoals", "Gol de visitante") + chk("cThirdPlace", "3er puesto") + chk("cSingleLeg", "Partido único") +
      "</div></div>" +

      '<div class="card-sec" style="margin-top:16px"><h3>3 · Sistema (ascensos y copas)</h3><div class="form-grid3">' +
      num("Ascensos (-1 = automático)", "cPromote", -1, -1, 10) + num("Descensos (-1 = automático)", "cDemote", -1, -1, 10) +
      num("Plazas continental 1", "cCont1", 4, 0, 10) + num("Plazas continental 2", "cCont2", 2, 0, 10) +
      fld("Temporada", '<select class="input" id="cSeason"><option value="0">Europa (ago–may)</option><option value="1">Año calendario (feb–dic)</option></select>') +
      fld("🏆 Copa Nacional Vinculada", '<select class="input" id="cLinkedCup"><option value="0">— Ninguna —</option></select>') +
      fld("⭐ Supercopa Vinculada", '<select class="input" id="cLinkedSuperCup"><option value="0">— Ninguna —</option></select>') +
      chk("cApClau", "Formato Apertura / Clausura (Tabla Acumulada Anual)") +
      "</div></div>" +

      '<div class="card-sec" style="margin-top:16px"><h3>4 · Equipos <span class="sub" id="cPickedCount">Equipos asignados: 0 / 20</span></h3>' +
      '<label class="check-line"><input type="checkbox" id="cGenMode"> ⚡ OPCIÓN B (Avanzado): Autogenerar Nuevos Equipos en Team.bin</label>' +
      '<div id="cGenBox" style="display:none; margin:8px 0 12px"><div class="field" style="max-width:340px; margin:0"><label>Prefijo / nombre base de los equipos nuevos</label><input class="input" id="cGenPrefix" placeholder="Club"></div>' +
      '<p class="mut small" style="margin-top:6px">Se genera la cantidad exacta de equipos nuevos (vacíos, sin jugadores) con ese prefijo y quedan asignados a esta liga. Después les completás el plantel desde Equipos.</p></div>' +
      '<p class="mut small" style="margin:4px 0 10px">OPCIÓN A: equipos reales libres con plantilla completa · OPCIÓN C: todos los de la base (los que ya juegan en otra liga solo entran clonados, nunca se mueven).</p>' +
      '<div id="cManualTeams"><div style="display:flex; gap:8px; margin-bottom:12px"><input class="input" id="cTeamSearch" placeholder="Buscar equipos…"><div class="chips">' +
      '<button class="chip on" data-src="free">Libres jugables</button><button class="chip" data-src="all">Todos</button></div></div>' +
      '<div class="list-panel" style="min-height:0"><div class="lp-body" id="cTeamList" style="max-height:380px"></div></div></div></div>' +

      '<div class="card-sec" style="margin-top:16px"><h3>5 · Logo y Escudo de la Liga (opcional)</h3>' +
      '<div style="display:flex; gap:14px; align-items:center"><div class="logo-prev" id="cLogoPrev"><span class="mut small" style="text-align:center; padding:6px">Sin logo</span></div>' +
      '<div><p class="mut small" style="margin:0 0 8px">Asigná un logo oficial de PES 2021 o subí una imagen propia. Se generan las resoluciones y siluetas en Sider LiveCPK al crear la liga.</p>' +
      '<div style="display:flex; gap:8px; flex-wrap:wrap"><button class="btn btn-ghost sm" id="cLogoGame">🖼️ Elegir del Juego…</button><button class="btn btn-ghost sm" id="cLogoFile">📂 Subir Imagen…</button><button class="btn btn-ghost sm" id="cLogoClear">🗑️ Quitar</button></div>' +
      '<p class="small" id="cLogoName" style="margin:8px 0 0"></p></div></div></div>' +

      '<div class="btn-row" style="margin-top:18px"><button class="btn btn-save" style="width:auto; padding:14px 26px" id="cCreate">🏆 Crear la liga</button>' +
      '<span class="mut small">Después tocá <b>Guardar (.bin)</b> en la barra lateral para escribir los cambios y sincronizar con Sider.</span></div>' +
      '<div id="crPresets" style="margin-top:22px"></div>';

    function fld(label, inner) { return '<div class="field"><label>' + label + "</label>" + inner + "</div>"; }
    function num(label, id, val, min, max) { return fld(label, '<input class="input" type="number" id="' + id + '" value="' + val + '" min="' + min + '" max="' + max + '">'); }
    function chk(id, label) { return '<label class="check-line" style="display:inline-flex; margin-right:16px"><input type="checkbox" id="' + id + '"> ' + label + "</label>"; }

    var src = "free";
    A.$$(".chip[data-src]", page).forEach(function (c) {
      c.addEventListener("click", function () {
        src = c.getAttribute("data-src");
        A.$$(".chip[data-src]", page).forEach(function (x) { x.classList.toggle("on", x === c); });
        drawTeams();
      });
    });
    var deb = null;
    page.querySelector("#cTeamSearch").addEventListener("input", function () { clearTimeout(deb); deb = setTimeout(drawTeams, 200); });

    function drawTeams() {
      var q = (page.querySelector("#cTeamSearch").value || "").toLowerCase();
      var pool = (src === "free" ? options.freeTeams : options.allTeams) || [];
      var list = page.querySelector("#cTeamList");
      list.innerHTML = "";
      pool.filter(function (t) {
        var tNm = A.teamName(t);
        return !q || ((tNm + " " + (t.nameSpanish || "") + " " + (t.nameEnglish || "") + " " + t.id).toLowerCase().indexOf(q) >= 0);
      }).slice(0, 300).forEach(function (t) {
        var tNm = A.teamName(t);
        var sNm = t.shortName || (tNm.length >= 3 ? tNm.substring(0, 3).toUpperCase() : tNm);
        var row = A.el('<label class="row" style="cursor:pointer">' +
          '<input type="checkbox" data-team="' + t.id + '"' + (picked[t.id] ? " checked" : "") + ">" +
          A.crestHtml(t.id, tNm, sNm) +
          '<span><span class="nm-line"><span class="nm">' + esc(tNm) + "</span>" +
          (t.isNew ? '<span class="pill new">NUEVO</span>' : "") + "</span>" +
          '<span class="sub">' + esc(t.countryName || "") + (t.leagueName ? " · " + esc(t.leagueName) : " · Libre") + " · " + (t.playerCount || 0) + ' jug.</span></span>' +
          '<span class="r-meta">ID ' + t.id + "</span></label>");
        row.querySelector("input").addEventListener("change", function (ev) {
          if (ev.target.checked) picked[t.id] = true; else delete picked[t.id];
          updateCount();
        });
        list.appendChild(row);
      });
      updateCount();
    }
    function targetCount() {
      var sel = page.querySelector("#cTeamCount");
      if (!sel) return 20;
      if (sel.value === "custom") {
        var c = Number(page.querySelector("#cTeamCountCustom").value);
        if (!isFinite(c) || c <= 0) return 20;
        return Math.max(2, Math.min(60, Math.floor(c)));
      }
      return Number(sel.value) || 20;
    }
    function updateCount() {
      var n = Object.keys(picked).length;
      var target = targetCount();
      var isLeague = page.querySelector("#cCompType").value === "0";
      var parity = isLeague ? (n % 2 === 0 ? " (Par \u2705)" : " (Impar \u26A0\uFE0F)") : "";
      var el = page.querySelector("#cPickedCount");
      var genEl = page.querySelector("#cGenMode");
      if (genEl && genEl.checked) { el.textContent = "Equipos asignados: " + target + " / " + target + " (autogenerados, vacíos)"; el.style.color = "var(--gold)"; return; }
      el.textContent = "Equipos asignados: " + n + " / " + target + parity;
      el.style.color = (n === target && !(isLeague && n % 2 === 1)) ? "var(--green)" : "var(--gold)";
    }
    var tcSel = page.querySelector("#cTeamCount");
    if (tcSel) tcSel.addEventListener("change", function () {
      page.querySelector("#cTeamCountCustomWrap").style.display = tcSel.value === "custom" ? "" : "none";
      updateCount();
    });
    var tcCustom = page.querySelector("#cTeamCountCustom");
    if (tcCustom) tcCustom.addEventListener("input", updateCount);
    var tcType = page.querySelector("#cCompType");
    if (tcType) tcType.addEventListener("change", updateCount);
    // Precarga desde la Piramide (como PreFillNewLeagueForPyramid del escritorio)
    if (A.S.pyrPreset) {
      var P = A.S.pyrPreset; A.S.pyrPreset = null;
      var ctry = page.querySelector("#cCountry"); if (ctry) ctry.value = String(P.countryId);
      (options.countries || []).forEach(function (c) { if (c.id === P.countryId) { var ct = page.querySelector("#cContinent"); if (ct) ct.value = String(c.continent); } });
      var ind = page.querySelector("#cIndependent"); if (ind) ind.checked = false;
      var par = page.querySelector("#cParent"); if (par) par.value = String(P.parentCompId);
      var nm = "DIVISIÓN " + P.nextTier + " " + (P.parentName || "LIGA");
      ["cNameSpanish", "cNameLatam", "cNameEnglish"].forEach(function (id) { var el = page.querySelector("#" + id); if (el) el.value = nm; });
      var asc = ("DIV" + P.nextTier + " " + (P.parentName || "LIGA").replace(/\s+/g, "")).toUpperCase();
      if (asc.length > 20) asc = asc.substring(0, 20);
      var ea = page.querySelector("#cNameAscii"); if (ea) ea.value = asc;
      toast("Asistente listo para crear el Tier " + P.nextTier + " debajo de " + (P.parentName || "la liga elegida") + ": ajustá el nombre si querés, elegí los equipos y creá la liga.");
    }

    // ===== Completado 2026-11-06: plantilla, vinculadas, logo, Opcion B, presets =====
    var teamById = {};
    (options.allTeams || []).forEach(function (t) { teamById[t.id] = t; });
    (options.freeTeams || []).forEach(function (t) { if (!teamById[t.id]) teamById[t.id] = t; });

    function rebuildLinkedCups() {
      var countryId = Number(page.querySelector("#cCountry").value) || 0;
      function fill(selId, list) {
        var sel = page.querySelector("#" + selId); if (!sel) return;
        var cur = sel.value;
        sel.innerHTML = '<option value="0">— Ninguna —</option>' + (list || []).filter(function (c) { return !countryId || c.countryId === countryId || c.countryId === 0; }).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + "</option>"; }).join("");
        sel.value = cur;
      }
      fill("cLinkedCup", options.cups);
      fill("cLinkedSuperCup", options.superCups);
    }
    page.querySelector("#cCountry").addEventListener("change", rebuildLinkedCups);
    rebuildLinkedCups();

    page.querySelector("#cFmtPreset").addEventListener("change", function () {
      var idx = Number(this.value);
      function set(id, val) { var el = page.querySelector("#" + id); if (el) el.value = String(val); }
      function setChk(id, on) { var el = page.querySelector("#" + id); if (el) el.checked = !!on; }
      if (idx === 0) { set("cRounds", 2); set("cNumGroups", 1); set("cNumClassified", 0); setChk("cFinalChamp", false); }
      else if (idx === 1) { set("cRounds", 1); set("cNumGroups", 1); set("cNumClassified", 0); setChk("cFinalChamp", false); }
      else if (idx === 2) { set("cRounds", 1); set("cNumGroups", 2); set("cNumClassified", 4); setChk("cFinalChamp", true); setChk("cExtraTime", true); setChk("cPenalties", true); setChk("cAwayGoals", false); set("cCupFormat", 0); }
      else if (idx === 3) { set("cRounds", 1); set("cNumGroups", 2); set("cNumClassified", 8); setChk("cFinalChamp", true); setChk("cExtraTime", true); setChk("cPenalties", true); setChk("cAwayGoals", false); set("cCupFormat", 0); }
    });

    function applyTemplate(baseId) {
      if (!baseId) { toast("Elegí primero una liga base.", true); return; }
      rpc("getCompetition", { id: baseId }).then(function (d) {
        function set(id, val) { var el = page.querySelector("#" + id); if (el && val != null) el.value = String(val); }
        function setChk(id, on) { var el = page.querySelector("#" + id); if (el) el.checked = !!on; }
        set("cCountry", d.countryId); rebuildLinkedCups();
        set("cChanges", d.changes); set("cRegistered", d.registeredPlayers); set("cRounds", d.rounds);
        set("cTie", d.tieBreaker); set("cCupFormat", d.cupFormat); set("cNumGroups", d.numGroups); set("cNumClassified", d.numClassified);
        setChk("cFinalChamp", d.finalChampionship); setChk("cExtraTime", d.extraTime); setChk("cPenalties", d.penalties);
        setChk("cAwayGoals", d.awayGoals); setChk("cThirdPlace", d.thirdPlace); setChk("cSingleLeg", d.singleLeg);
        set("cPromote", d.promote); set("cDemote", d.demote); set("cCont1", d.continental1); set("cCont2", d.continental2);
        set("cSeason", d.seasonType); setChk("cApClau", d.aperturaClausura);
        set("cLinkedCup", d.linkedCup); set("cLinkedSuperCup", d.linkedSuperCup);
        toast("Plantilla aplicada: se copiaron el formato y el sistema de " + (d.nameSpanish || d.name || "la liga base") + ".");
      }).catch(function (e) { toast(e.message, true); });
    }
    page.querySelector("#cApplyTemplate").addEventListener("click", function () { applyTemplate(Number(page.querySelector("#cTemplate").value) || 0); });
    if (A.S.leagueTemplate) { var lt = A.S.leagueTemplate; A.S.leagueTemplate = null; var tsel = page.querySelector("#cTemplate"); if (tsel) tsel.value = String(lt); applyTemplate(lt); }

    var pendingLogo = null;
    function drawLogoPrev() {
      var prev = page.querySelector("#cLogoPrev"), nm = page.querySelector("#cLogoName");
      if (!prev) return;
      if (!pendingLogo) { prev.innerHTML = '<span class="mut small" style="text-align:center; padding:6px">Sin logo</span>'; nm.textContent = ""; return; }
      if (pendingLogo.kind === "game") { prev.innerHTML = pendingLogo.png ? '<img src="data:image/png;base64,' + pendingLogo.png + '">' : '<span class="mut small">Logo del juego</span>'; nm.textContent = "Logo del juego: " + (pendingLogo.name || ""); }
      else { prev.innerHTML = '<span class="mut small" style="padding:6px; text-align:center">Imagen propia</span>'; nm.textContent = "Imagen: " + pendingLogo.path.split(/[\\/]/).pop(); }
    }
    page.querySelector("#cLogoGame").addEventListener("click", function () { A.pickGameEmblem(function (it) { pendingLogo = { kind: "game", compId: it.compId, name: it.name, png: it.png }; drawLogoPrev(); }); });
    page.querySelector("#cLogoFile").addEventListener("click", function () {
      rpc("pickFile", { title: "Elegí la imagen del logo", filter: "Imágenes (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp" }).then(function (r) { if (r && r.path) { pendingLogo = { kind: "file", path: r.path }; drawLogoPrev(); } }).catch(function (e) { toast(e.message, true); });
    });
    page.querySelector("#cLogoClear").addEventListener("click", function () { pendingLogo = null; drawLogoPrev(); });

    page.querySelector("#cGenMode").addEventListener("change", function () {
      var on = this.checked;
      page.querySelector("#cGenBox").style.display = on ? "" : "none";
      page.querySelector("#cManualTeams").style.display = on ? "none" : "";
      updateCount();
    });

    drawTeams();

    page.querySelector("#cCreate").addEventListener("click", function () {
      function v(id) { return page.querySelector("#" + id).value; }
      function n(id) { return Number(v(id)) || 0; }
      function b(id) { return page.querySelector("#" + id).checked; }
      var gen = page.querySelector("#cGenMode").checked;
      var teamIds = gen ? [] : Object.keys(picked).map(Number);
      var nameSpanish = v("cNameSpanish").trim();
      if (!nameSpanish) { toast("Poné al menos el nombre en español.", true); return; }
      var target = targetCount();
      if (!gen && teamIds.length !== target) { toast("Debe asignar exactamente " + target + " equipos a la liga (actualmente hay " + teamIds.length + "). El calendario del motor requiere la cantidad exacta.", true); return; }

      // Ocupados: como en el escritorio, nunca se mueven; se quitan o se clonan.
      var occupied = gen ? [] : teamIds.filter(function (id) { var t = teamById[id]; return t && t.domesticLeagueName; });
      if (occupied.length) {
        A.showModal("Equipos que ya juegan en otra liga",
          '<p>Estos equipos ya juegan en una liga doméstica:</p>' + occupied.map(function (id) { var t = teamById[id]; return '<div class="bk-row"><span class="nm">' + esc(A.teamName(t)) + '</span><span class="sub">' + esc(t.domesticLeagueName) + '</span></div>'; }).join("") +
          '<p class="mut" style="margin-top:8px">Un equipo nunca se mueve de su liga: podés clonarlos (los clones entran a la liga nueva) o quitarlos de la lista.</p>',
          [{ label: "Cancelar" },
           { label: "Quitar ocupados", onClick: function () { occupied.forEach(function (id) { delete picked[id]; }); drawTeams(); toast("Se quitaron los equipos ocupados. Completá la cantidad y volvé a crear."); } },
           { label: "Clonar ocupados y crear", cls: "btn-primary", onClick: function () { cloneOccupied(occupied, teamIds); } }]);
        return;
      }
      warningsThenCreate(teamIds);

      function cloneOccupied(occ, ids) {
        var map = {};
        var chain = Promise.resolve();
        occ.forEach(function (id) { chain = chain.then(function () { return rpc("cloneTeam", { id: id }).then(function (r) { map[id] = r.team.id; }); }); });
        chain.then(function () { warningsThenCreate(ids.map(function (id) { return map[id] || id; })); })
          .catch(function (e) { toast(e.message, true); });
      }

      function warningsThenCreate(finalIds) {
        var isLeague = n("cCompType") === 0;
        function shortStep() {
          var shorts = gen ? target : finalIds.filter(function (id) { var t = teamById[id]; return t && (t.playerCount || 0) < 11; }).length;
          if (shorts > 0) {
            A.confirmModal("Plantillas incompletas",
              shorts + " de los " + target + " equipos tienen menos de 11 jugadores registrados. En PES 2021, el menú de Partido Amistoso descarta automáticamente las competiciones cuyos equipos no tienen plantilla completa. ¿Crear la liga de todos modos?",
              "Crear de todos modos", function () { doCreate(finalIds); });
          } else doCreate(finalIds);
        }
        if (isLeague && target % 2 === 1) {
          A.confirmModal("Aviso de Paridad Impar",
            "La nueva liga regular tiene una cantidad IMPAR de equipos (" + target + "). El motor de PES 2021 genera calendarios de liga 'todos contra todos' asumiendo una cantidad PAR de participantes. Una cantidad impar puede provocar errores al armar las fechas de la temporada o cuelgues en Liga Máster. ¿Crear la liga con esta cantidad impar de todos modos?",
            "Crear igual", shortStep);
        } else shortStep();
      }

      function doCreate(finalIds) {
        rpc("createLeague", {
          compId: n("cCompId"), nameSpanish: nameSpanish, nameEnglish: v("cNameEnglish") || nameSpanish,
          nameLatam: v("cNameLatam") || nameSpanish, nameAscii: v("cNameAscii") || nameSpanish.replace(/\s+/g, "_").toUpperCase(),
          continent: n("cContinent"), totalTeams: target, compType: n("cCompType"),
          isIndependent: b("cIndependent"), parentCompId: n("cParent"), teamIds: finalIds, teamIdsCsv: finalIds.join(","),
          countryId: n("cCountry"), changes: n("cChanges"), registeredPlayers: n("cRegistered"), rounds: n("cRounds"),
          cupFormat: n("cCupFormat"), tieBreaker: n("cTie"), extraTime: b("cExtraTime"), penalties: b("cPenalties"), awayGoals: b("cAwayGoals"),
          thirdPlace: b("cThirdPlace"), singleLeg: b("cSingleLeg"), finalChampionship: b("cFinalChamp"),
          promote: n("cPromote"), demote: n("cDemote"), continental1: n("cCont1"), continental2: n("cCont2"),
          numGroups: n("cNumGroups"), numClassified: n("cNumClassified"), seasonType: n("cSeason"), aperturaClausura: b("cApClau"),
          linkedCup: n("cLinkedCup"), linkedSuperCup: n("cLinkedSuperCup"),
          genCount: gen ? target : 0, genPrefix: v("cGenPrefix") || "Club"
        }).then(function (r) {
          function after() {
            toast("Liga creada con ID " + r.compId + ". Guardá los .bin para escribirla en el juego.");
            picked = {};
            return A.reloadState();
          }
          if (pendingLogo) {
            var apply = pendingLogo.kind === "game"
              ? rpc("setCompEmblemFromGame", { gameCompId: pendingLogo.compId, id: r.compId })
              : rpc("setCompEmblemFile", { path: pendingLogo.path, id: r.compId });
            return apply.then(after).catch(function (e) { toast("La liga se creó, pero el logo no se pudo aplicar: " + e.message, true); return after(); });
          }
          return after();
        }).then(function () { A.goto("ligas"); })
          .catch(function (e) { toast(e.message, true); });
      }
    });
    loadPresetsPanel(page);
  }

  // ---------------- Fase 4: presets reales + censo ----------------
  var ESTADOS = {
    ok: { tag: "ok", label: "entra" },
    ocupado: { tag: "bad", label: "ocupado" },
    "no-existe": { tag: "bad", label: "no existe" },
    ambiguo: { tag: "warn", label: "ambiguo" },
    "no-jugable": { tag: "warn", label: "no jugable" },
    duplicado: { tag: "warn", label: "duplicado" }
  };

  function loadPresetsPanel(page) {
    var box = page.querySelector("#crPresets");
    if (!box) return;
    box.innerHTML = '<div class="card-sec"><h3>Presets reales <span class="sub">pirámides reales con equipos que ya están en el juego</span></h3><span class="spin"></span> Leyendo el censo de tu base…</div>';
    var census = null, presets = null;
    rpc("getCensus").then(function (c) {
      census = c;
      return rpc("listPresets");
    }).then(function (lp) {
      presets = lp;
      pintar();
    }).catch(function (e) { box.innerHTML = ""; toast(e.message, true); });

    function pintar(preview, applyResults) {
      var paisOpts = census.paises.map(function (p, i) {
        return '<option value="' + i + '">' + esc(p.nombre) + " (" + p.cantidad + ")</option>";
      }).join("");
      var presetOpts = presets.presets.map(function (p) {
        return '<option value="' + esc(p.archivo) + '">' + esc(p.nombre) + " — " + p.ligas + " ligas · " + p.equipos + " equipos</option>";
      }).join("");
      var html =
        '<div class="card-sec"><h3>Presets reales <span class="sub">pirámides reales con equipos que ya están en el juego</span></h3>' +
        '<div class="kv"><span>Equipos libres jugables en tu base</span><code>' + census.total + '</code></div>' +
        '<div style="display:flex; gap:8px; align-items:flex-start; margin-top:10px">' +
        '<select class="input" id="cpPais" style="flex:1">' + paisOpts + '</select></div>' +
        '<div class="list-panel" style="min-height:0; margin-top:8px"><div class="lp-body" id="cpEquipos" style="max-height:220px"></div></div>' +
        '<h3 style="margin-top:18px">Aplicar un preset</h3>' +
        (presets.presets.length
          ? '<div style="display:flex; gap:8px; align-items:center"><select class="input" id="cpPreset" style="flex:1">' + presetOpts + '</select>' +
            '<button class="btn btn-ghost" id="cpPreview" style="flex:none">👁️ Vista previa</button></div>'
          : '<p class="mut">No hay presets cargados todavía. Los archivos .json van en <code>' + esc(presets.presetsDir) + '</code>: el piloto lo definimos con tu censo en la mano.</p>') +
        (presets.errores && presets.errores.length
          ? '<p class="mut" style="color:var(--coral)">Presets con error: ' + esc(presets.errores.join(" · ")) + '</p>' : "") +
        '<div id="cpPreviewBox" style="margin-top:12px"></div></div>';
      box.innerHTML = html;

      var selPais = box.querySelector("#cpPais");
      function drawEquipos() {
        var p = census.paises[Number(selPais.value)] || { equipos: [] };
        box.querySelector("#cpEquipos").innerHTML = p.equipos.map(function (e) {
          return '<div class="row" style="cursor:default"><span></span><span class="nm">' + esc(e.nombre) + '</span><span class="r-meta">ID ' + e.id + '</span></div>';
        }).join("") || '<p class="mut" style="padding:10px">Sin equipos.</p>';
      }
      if (selPais) { selPais.addEventListener("change", drawEquipos); drawEquipos(); }

      var btnPrev = box.querySelector("#cpPreview");
      if (btnPrev) btnPrev.addEventListener("click", function () {
        var archivo = box.querySelector("#cpPreset").value;
        rpc("previewPreset", { archivo: archivo }).then(function (pv) {
          pintarPreview(box, pv, archivo);
        }).catch(function (e) { toast(e.message, true); });
      });
      if (applyResults) pintarResultados(box, applyResults);
    }

    function pintarPreview(box, pv, archivo) {
      var target = box.querySelector("#cpPreviewBox");
      var algunaViable = pv.ligas.some(function (l) { return l.viable; });
      var html = '<h3>Vista previa: ' + esc(pv.preset.nombre) + '</h3>';
      pv.ligas.forEach(function (l) {
        html += '<div class="card-sec" style="margin-top:10px"><h3>' + esc(l.nombre) +
          ' <span class="sub">nivel ' + l.tier + (l.padre ? ' · padre: ' + esc(l.padre) : '') + '</span></h3>' +
          (l.viable
            ? '<p class="mut">Se crea con <b>CompID ' + l.compId + '</b> y ' + l.asignados + ' equipos.</p>'
            : '<p class="mut" style="color:var(--coral)">Se salta: ' + esc(l.motivo) + '</p>');
        l.equipos.forEach(function (e) {
          var st = ESTADOS[e.estado] || ESTADOS.ok;
          html += '<div class="bk-row"><span class="tag ' + st.tag + '">' + st.label + '</span>' +
            '<div style="flex:1; min-width:0"><span class="nm">' + esc(e.nombreReal || e.pedido) + '</span>' +
            (e.detalle ? '<span class="sub">' + esc(e.detalle) + '</span>' : '') + '</div>' +
            (e.teamId ? '<span class="r-meta">ID ' + e.teamId + '</span>' : '') + '</div>';
        });
        html += '</div>';
      });
      if (algunaViable) {
        html += '<div class="btn-row" style="margin-top:12px"><button class="btn btn-primary" id="cpApply">✅ Aplicar el preset</button>' +
          '<span class="mut small">Antes de crear se guarda un punto de restauración del Sider. Los equipos ocupados nunca se mueven.</span></div>';
      }
      target.innerHTML = html;
      var btn = target.querySelector("#cpApply");
      if (btn) btn.addEventListener("click", function () {
        A.confirmModal("Aplicar preset", "Se crean las ligas viables de la vista previa en tu base (todavía sin guardar los .bin). Antes se crea un punto de restauración. ¿Continuar?", "Aplicar", function () {
          rpc("applyPreset", { archivo: archivo }).then(function (r) {
            toast(r.message);
            pintarResultados(box, r);
            A.reloadState();
          }).catch(function (e) { toast(e.message, true); });
        });
      });
    }

    function pintarResultados(box, r) {
      var target = box.querySelector("#cpPreviewBox");
      target.innerHTML = '<h3>Resultado</h3>' + r.results.map(function (x) {
        return '<div class="bk-row"><span class="tag ' + (x.creada ? "ok" : "bad") + '">' + (x.creada ? "creada" : "no") + '</span>' +
          '<div style="flex:1; min-width:0"><span class="nm">' + esc(x.nombre) + '</span><span class="sub">' + esc(x.mensaje) + '</span></div></div>';
      }).join("") +
      '<p class="mut" style="margin-top:10px">Para que las ligas queden en el juego: <b>Guardar (.bin)</b> y después redesplegar desde Sider y Lua.</p>';
    }
  }
})();
