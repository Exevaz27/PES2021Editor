// ============================================================

// Seccion: Jugadores (buscador + ficha completa con radar)

// ============================================================

(function () {

  "use strict";

  var A = window.App, rpc = A.rpc, esc = A.esc, toast = A.toast;



  var ABIL = {

    atk: "Actitud Ofensiva", def: "Actitud Defensiva", ball_win: "Recuperación de Balón",

    header: "Cabezazo", jump: "Salto", lowpass: "Pase Corto", loftpass: "Pase Largo",

    finish: "Finalización", place_kick: "Balón Parado", swerve: "Efecto",

    ball_ctrl: "Control de Balón", tight_pos: "Control Estrecho", drib: "Regate",

    body_ctrl: "Equilibrio", phys_cont: "Contacto Físico", aggres: "Agresividad",

    kick_pwr: "Potencia de Tiro", speed: "Velocidad", exp_pwr: "Explosividad", stamina: "Resistencia",

    gk: "Actitud de Portero", catching: "Atajar", clearing: "Despeje", reflex: "Reflejos", cover: "Cobertura"

  };

  var ABIL_ORDER = ["atk", "def", "ball_win", "lowpass", "loftpass", "finish", "place_kick", "swerve", "ball_ctrl", "tight_pos", "drib", "body_ctrl", "phys_cont", "aggres", "kick_pwr", "speed", "exp_pwr", "stamina", "header", "jump", "gk", "catching", "clearing", "reflex", "cover"];

  var POSITIONS = ["GK", "CB", "LB", "RB", "DMF", "CMF", "LMF", "RMF", "AMF", "LWF", "RWF", "SS", "CF"];



  function buildPosOptions(selectedVal) {

    var isEs = A.getPosLang() === "es";

    return '<option value="">Posición: todas</option>' +

      A.POS_LIST.map(function (p) {

        var text = isEs ? (p.es + " - " + p.desc) : (p.en + " - " + p.descEn);

        return '<option value="' + p.en + '"' + (selectedVal === p.en ? " selected" : "") + ">" + esc(text) + "</option>";

      }).join("");

  }



  var search = "", posFilter = "", minOvr = 0, freeOnly = false, offset = 0, total = 0;

  var selectedId = null, detail = null, tab = "habilidades";



  function abilColor(v) { return v >= 90 ? "#FFD166" : v >= 80 ? "#00E676" : v >= 70 ? "#7DD956" : v >= 60 ? "#B8C94A" : "#94A3B8"; }



  A.register("jugadores", function (page) {

    if (!A.S.dbLoaded) {

      page.innerHTML = '<div class="empty-state"><div class="empty-ico">⚽</div><h2>Sin base cargada</h2><p>Cargá tu pesdb desde Configuración para explorar los jugadores.</p></div>';

      return Promise.resolve();

    }

    page.innerHTML =

      '<div class="action-bar">' +

      '<button class="btn btn-primary" id="pNew">➕ Crear jugador</button>' +

      '<button class="btn btn-ghost" id="pClone">📋 Clonar</button>' +

      '<button class="btn danger" id="pDelete">🗑️ Eliminar</button>' +

      '<span class="spacer"></span>' +

      A.renderPosToggle() +

      "</div>" +

      '<div class="md"><div class="md-left"><div class="list-panel">' +

      '<div class="lp-head">' +

      '<input class="input" id="pSearch" placeholder="Buscar por nombre o ID…" value="' + esc(search) + '">' +

      '<div style="display:flex; gap:8px"><select class="input" id="pPos">' + buildPosOptions(posFilter) + "</select>" +

      '<select class="input" id="pOvr"><option value="0">Media: todas</option><option value="85">85+</option><option value="80">80+</option><option value="75">75+</option><option value="70">70+</option></select></div>' +

      '<label class="check-line"><input type="checkbox" id="pFree"' + (freeOnly ? " checked" : "") + "> Solo agentes libres</label>" +

      '<div class="mut" id="pCount"></div>' +

      "</div>" +

      '<div class="lp-body" id="pList"></div>' +

      '<button class="btn btn-ghost" id="pMore" style="border:0; border-top:1px solid var(--border); border-radius:0">Mostrar más</button>' +

      "</div></div>" +

      '<div class="md-right" id="pDetail"><div class="empty-state"><div class="empty-ico">👈</div><h2>Elegí un jugador</h2><p>Seleccioná un futbolista para ver su ficha completa: habilidades, skills, posiciones y estilo.</p></div></div>' +

      "</div>";



    var deb = null;

    page.querySelector("#pSearch").addEventListener("input", function (e) { clearTimeout(deb); deb = setTimeout(function () { search = e.target.value; offset = 0; loadList(true); }, 240); });

    page.querySelector("#pPos").addEventListener("change", function (e) { posFilter = e.target.value; offset = 0; loadList(true); });

    page.querySelector("#pOvr").addEventListener("change", function (e) { minOvr = Number(e.target.value); offset = 0; loadList(true); });

    page.querySelector("#pFree").addEventListener("change", function (e) { freeOnly = e.target.checked; offset = 0; loadList(true); });

    page.querySelector("#pMore").addEventListener("click", function () { offset += 300; loadList(false); });

    page.querySelector("#pPos").value = posFilter;

    page.querySelector("#pOvr").value = String(minOvr);



    function updatePosUi() {

      var sel = page.querySelector("#pPos");

      if (sel) {

        var curVal = sel.value;

        sel.innerHTML = buildPosOptions(curVal);

        sel.value = curVal;

      }

      loadList(true);

      if (detail) renderDetail();

    }

    A.bindPosToggle(page, function () { updatePosUi(); });

    window.addEventListener("poslangchange", updatePosUi);



    page.querySelector("#pNew").addEventListener("click", function () {
      // Alta completa nivel repos: perfil fisico + 25 habilidades (antes: nombre/pos/media/pais y todo fijo)
      rpc("createPlayerOptions").then(function (o) {
        var teamCount = {};
        (o.teams || []).forEach(function (t) { teamCount[t.id] = t.playerCount; });
        var abilHtml = ABIL_ORDER.map(function (k) {
          return '<div class="abil-row"><label>' + ABIL[k] + '</label><input class="input small" type="number" min="40" max="99" value="80" data-npabil="' + k + '"></div>';
        }).join("");
        A.showModal("Crear Nuevo Futbolista en PES 2021",
          '<div class="form-grid">' +
          '<div class="field"><label>Nombre del Futbolista *</label><input class="input" id="npName" placeholder="Nuevo Jugador"></div>' +
          '<div class="field"><label>Nombre en camiseta</label><input class="input" id="npShirt" placeholder=""></div>' +
          '<div class="field"><label>Posición Principal</label><select class="input" id="npPos">' + A.POS_LIST.map(function (p) { var lbl = (A.getPosLang() === "es") ? (p.es + " - " + p.desc) : (p.en + " - " + p.descEn); return '<option value="' + p.en + '"' + (p.en === "CF" ? " selected" : "") + '>' + esc(lbl) + '</option>'; }).join("") + "</select></div>" +
          '<div class="field"><label>País / Nacionalidad</label><select class="input" id="npNat">' + (o.countries || []).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + "</option>"; }).join("") + "</select></div>" +
          '<div class="field"><label>Club Inicial (opcional)</label><select class="input" id="npClub"><option value="0">Agente Libre (Sin Club)</option>' + (o.teams || []).map(function (t) { return '<option value="' + t.id + '">' + esc(t.name) + " (" + t.playerCount + ' jug.)</option>'; }).join("") + "</select></div>" +
          '<div class="field"><label>Nivel / Calidad Base</label><select class="input" id="npPreset"><option value="90">90 (Superestrella)</option><option value="85">85 (Crack Mundial)</option><option value="80" selected>80 (Titular Primera)</option><option value="75">75 (Profesional)</option><option value="70">70 (Juvenil Promesa)</option></select></div>' +
          '<div class="field"><label>Edad</label><input class="input" type="number" id="npAge" value="24" min="15" max="50"></div>' +
          '<div class="field"><label>Altura (cm)</label><input class="input" type="number" id="npHeight" value="180" min="140" max="220"></div>' +
          '<div class="field"><label>Peso (kg)</label><input class="input" type="number" id="npWeight" value="75" min="40" max="130"></div>' +
          '<div class="field"><label>Pie hábil</label><select class="input" id="npFoot"><option value="0">Diestro</option><option value="1">Zurdo</option></select></div>' +
          '<div class="field"><label>Uso de pie débil (0-3)</label><input class="input" type="number" id="npWfu" value="2" min="0" max="3"></div>' +
          '<div class="field"><label>Precisión pie débil (0-3)</label><input class="input" type="number" id="npWfa" value="2" min="0" max="3"></div>' +
          '<div class="field"><label>Estado de forma (0-7)</label><input class="input" type="number" id="npForm" value="5" min="0" max="7"></div>' +
          '<div class="field"><label>Resistencia a lesiones (0-3)</label><input class="input" type="number" id="npInjury" value="1" min="0" max="3"></div>' +
          '<div class="field"><label>Estilo de juego</label><select class="input" id="npStyle">' + (o.styles || []).map(function (sn, i) { return '<option value="' + i + '">' + esc(sn) + "</option>"; }).join("") + "</select></div>" +
          "</div>" +
          '<h3 style="margin:14px 0 8px">Habilidades (25) <span class="sub">el nivel base las rellena a todas; ajustá las que quieras</span></h3>' +
          '<div class="abil-grid">' + abilHtml + "</div>",
          [{ label: "Cancelar" }, {
            label: "Crear Futbolista", cls: "btn-primary", onClick: function () {
              function gv(id) { return document.querySelector("#" + id); }
              var name = gv("npName").value.trim();
              if (!name) { toast("Poné el nombre del futbolista.", true); return; }
              var clubId = Number(gv("npClub").value) || 0;
              if (clubId && (teamCount[clubId] || 0) >= 32) { toast("Ese club ya tiene la plantilla máxima de 32: el jugador se crea como Agente Libre."); clubId = 0; }
              var abilities = {};
              document.querySelectorAll("[data-npabil]").forEach(function (inp) { abilities[inp.getAttribute("data-npabil")] = Math.max(40, Math.min(99, Number(inp.value) || 40)); });
              rpc("createPlayer", {
                name: name, shirtName: gv("npShirt").value, posName: gv("npPos").value,
                ovr: Number(gv("npPreset").value) || 80, natId: Number(gv("npNat").value) || 0, clubId: clubId,
                age: Number(gv("npAge").value) || 24, height: Number(gv("npHeight").value) || 180, weight: Number(gv("npWeight").value) || 75,
                leftFooted: gv("npFoot").value === "1", weakFootUsage: Number(gv("npWfu").value) || 0, weakFootAccuracy: Number(gv("npWfa").value) || 0,
                form: Number(gv("npForm").value) || 0, injury: Number(gv("npInjury").value) || 0, style: Number(gv("npStyle").value) || 0,
                abilities: abilities
              }).then(function (r) {
                toast("Jugador creado: ID " + r.player.id); offset = 0; loadList(true); selectPlayer(r.player.id);
              }).catch(function (e) { toast(e.message, true); });
            }
          }]);
        var presetSel = document.querySelector("#npPreset");
        if (presetSel) presetSel.addEventListener("change", function () {
          var v = this.value;
          document.querySelectorAll("[data-npabil]").forEach(function (inp) { inp.value = v; });
        });
      }).catch(function (e) { toast(e.message, true); });
    });
    page.querySelector("#pClone").addEventListener("click", function () {

      if (!detail) { toast("Elegí primero un jugador.", true); return; }

      rpc("clonePlayer", { id: detail.id }).then(function (r) { toast("Clonado: ID " + r.player.id); selectedId = r.player.id; loadList(true); selectPlayer(r.player.id); }).catch(function (e) { toast(e.message, true); });

    });

    page.querySelector("#pDelete").addEventListener("click", function () {

      if (!detail) { toast("Elegí primero un jugador.", true); return; }

      A.confirmModal("Eliminar jugador", "Se elimina a «" + detail.name + "» (ID " + detail.id + "). ¿Seguro?", "Eliminar", function () {

        rpc("deletePlayer", { id: detail.id }).then(function () {

          toast("Jugador eliminado."); selectedId = null; detail = null;

          page.querySelector("#pDetail").innerHTML = '<div class="empty-state"><div class="empty-ico">👈</div><h2>Elegí un jugador</h2><p>Seleccioná un futbolista para ver su ficha completa: habilidades, skills, posiciones y estilo.</p></div>';

          loadList(true);

        }).catch(function (e) { toast(e.message, true); });

      });

    });



    function loadList(reset) {

      rpc("listPlayers", { search: search, pos: posFilter, minOvr: minOvr, freeOnly: freeOnly, limit: 300, offset: offset }).then(function (r) {

        total = r.total;

        page.querySelector("#pCount").textContent = total + " futbolistas";

        var list = page.querySelector("#pList");

        if (reset) list.innerHTML = "";

        r.players.forEach(function (p) {

          var b = A.el('<button class="row' + (p.id === selectedId ? " sel" : "") + '" data-id="' + p.id + '">' +

            A.posBadge(p.pos) +

            '<span><span class="nm">' + esc(p.name) + '</span>' +

            '<span class="sub">' + esc(p.clubName) + " · " + esc(p.natName) + " · " + p.age + ' años</span></span>' +

            '<span class="ovr" style="color:' + abilColor(p.ovr) + '">' + p.ovr + "</span></button>");

          b.addEventListener("click", function () { selectPlayer(p.id); });

          list.appendChild(b);

        });

        page.querySelector("#pMore").style.display = (offset + 300 < total) ? "" : "none";

      }).catch(function (e) { toast(e.message, true); });

    }



    function selectPlayer(id) {

      selectedId = id;

      A.$$(".row", page).forEach(function (x) { x.classList.toggle("sel", Number(x.getAttribute("data-id")) === id); });

      rpc("getPlayer", { id: id }).then(function (r) { detail = r.player; renderDetail(); }).catch(function (e) { toast(e.message, true); });

    }

    A.selectPlayer = selectPlayer;



    function renderDetail() {

      var d = detail, box = page.querySelector("#pDetail");

      var tabs = [["habilidades", "📊 Habilidades"], ["perfil", "👤 Perfil"], ["skills", "⭐ Skills y estilos"], ["posiciones", "🗺️ Posiciones"]];

      box.innerHTML =

        '<div class="detail-head">' +

        A.posBadge(d.pos, true).replace(">", ' id="pFace">') +

        '<div class="dh-main"><div class="detail-title">' + esc(d.name) + "</div>" +

        '<div class="detail-meta"><span>' + esc(d.shirtName || "") + "</span><span>·</span><span>🏳️ <b>" + esc(d.natName) + "</b></span><span>·</span><span><b>" + d.age + "</b> años</span><span>·</span><span><b>" + d.height + "</b> cm / <b>" + d.weight + "</b> kg</span><span>·</span><span>" + (d.leftFooted ? "Zurdo" : "Diestro") + "</span><span>·</span><span>ID <b>" + d.id + "</b></span></div>" +

        '<div class="detail-meta"><span>Equipo: <b>' + esc(d.clubName) + "</b></span><span>·</span><span>Estilo: <b>" + esc(d.styleName || "—") + "</b></span>" + (d.hasRealFace ? "<span>·</span><span>📸 Miniface real</span>" : "") + "</div></div>" +

        '<div class="ovr-big"><div class="n">' + d.ovr + '</div><div class="l">VALORACIÓN</div></div></div>' +



        '<div class="subtabs">' + tabs.map(function (t) {

          return '<button class="subtab' + (tab === t[0] ? " on" : "") + '" data-tab="' + t[0] + '">' + t[1] + "</button>";

        }).join("") + "</div>" +

        '<div id="pTabBody"></div>' +

        '<div class="btn-row"><button class="btn btn-save" style="width:auto; padding:13px 22px" id="pSave">💾 Guardar cambios del jugador</button></div>';



      A.$$(".subtab[data-tab]", box).forEach(function (c) {

        c.addEventListener("click", function () {

          tab = c.getAttribute("data-tab");

          A.$$(".subtab[data-tab]", box).forEach(function (x) { x.classList.toggle("on", x === c); });

          renderTab();

        });

      });



      rpc("playerMiniface", { id: d.id, pos: d.pos, name: d.name }).then(function (r) {

        var f = page.querySelector("#pFace");

        if (r.png && f) f.innerHTML = '<img src="data:image/png;base64,' + r.png + '" style="width:100%;height:100%;object-fit:cover">';

      }).catch(function () { });



      function renderTab() {

        var body = box.querySelector("#pTabBody");

        if (tab === "habilidades") {

          body.innerHTML =

            '<div class="card-sec"><h3>Habilidades (25)</h3>' +

            '<div style="display:flex; gap:18px; flex-wrap:wrap; align-items:flex-start"><canvas id="pRadar" class="radar" width="250" height="250"></canvas>' +

            '<div class="abil-grid" style="flex:1; min-width:300px">' +

            ABIL_ORDER.map(function (k) {

              var v = d.abilities[k] || 0;

              return '<div class="abil-row"><label>' + ABIL[k] + '</label><span class="bar"><i style="width:' + v + '%; background:' + abilColor(v) + '"></i></span><input class="input small" type="number" min="40" max="99" value="' + v + '" data-abil="' + k + '"></div>';

            }).join("") + "</div></div></div>";

          drawRadar();

          A.$$("[data-abil]", body).forEach(function (inp) {

            inp.addEventListener("input", function () { d.abilities[inp.getAttribute("data-abil")] = Number(inp.value) || 0; drawRadar(); });

          });

        } else if (tab === "perfil") {

          body.innerHTML =

            '<div class="card-sec"><h3>Datos del jugador</h3><div class="form-grid">' +

            pf("Nombre", "dName", d.name) + pf("Nombre en camiseta", "dShirt", d.shirtName) +

            '<div class="field"><label>Nacionalidad</label><select class="input" id="dNat">' + (d.countries || []).map(function (c) { return '<option value="' + c.id + '"' + (c.id === d.natId ? ' selected' : '') + '>' + esc(c.name) + '</option>'; }).join("") + "</select></div>" +

            '<div class="field"><label>Posición principal</label><select class="input" id="dPos">' + A.POS_LIST.map(function (p, i) { var lbl = (A.getPosLang() === "es") ? (p.es + " - " + p.desc) : (p.en + " - " + p.descEn); return '<option value="' + i + '"' + (i === d.posIndex ? " selected" : "") + ">" + esc(lbl) + "</option>"; }).join("") + "</select></div>" +

            nf("Edad", "dAge", d.age, 15, 50) + nf("Altura (cm)", "dHeight", d.height, 140, 220) + nf("Peso (kg)", "dWeight", d.weight, 40, 130) +

            nf("Uso de pie débil (0-3)", "dWfu", d.weakFootUsage, 0, 3) + nf("Precisión pie débil (0-3)", "dWfa", d.weakFootAccuracy, 0, 3) +

            nf("Estado de forma (0-7)", "dForm", d.form, 0, 7) + nf("Resistencia a lesiones (0-3)", "dInjury", d.injury, 0, 3) +

            nf("🎙️ Callname / Audio (ID)", "dCallname", d.callnameId || 0, 0, 65535) +

            '<div class="field"><label>Pie hábil</label><select class="input" id="dFoot"><option value="0"' + (!d.leftFooted ? " selected" : "") + '>Diestro</option><option value="1"' + (d.leftFooted ? " selected" : "") + ">Zurdo</option></select></div>" +

            '<div class="field"><label>Estilo de juego</label><select class="input" id="dStyle">' + (d.styles || []).map(function (sn, i) { return '<option value="' + i + '"' + (i === d.style ? " selected" : "") + ">" + esc(sn) + "</option>"; }).join("") + "</select></div>" +

            "</div></div>";

          function pf(l, id, v) { return '<div class="field"><label>' + l + '</label><input class="input" id="' + id + '" value="' + esc(v || "") + '"></div>'; }

          function nf(l, id, v, mn, mx) { return '<div class="field"><label>' + l + '</label><input class="input" type="number" id="' + id + '" value="' + v + '" min="' + mn + '" max="' + mx + '"></div>'; }

        } else if (tab === "skills") {

          body.innerHTML =

            '<div class="card-sec"><h3>Skills de jugador</h3><div class="skill-wrap">' +

            (d.skills || []).map(function (s) {

              return '<button class="chip' + (s.on ? " on green" : "") + '" data-skill="' + s.id + '">' + esc(s.label) + "</button>";

            }).join("") + "</div></div>" +

            '<div class="card-sec" style="margin-top:16px"><h3>Estilos COM / IA</h3><div class="skill-wrap">' +

            (d.comStyles || []).map(function (s) {

              return '<button class="chip' + (s.on ? " on gold" : "") + '" data-com="' + s.id + '">' + esc(s.label) + "</button>";

            }).join("") + "</div></div>";

          A.$$("[data-skill]", body).forEach(function (c) {

            c.addEventListener("click", function () {

              var s = d.skills.find(function (x) { return x.id === Number(c.getAttribute("data-skill")); });

              if (s) { s.on = !s.on; c.classList.toggle("on", s.on); c.classList.toggle("green", s.on); }

            });

          });

          A.$$("[data-com]", body).forEach(function (c) {

            c.addEventListener("click", function () {

              var s = d.comStyles.find(function (x) { return x.id === Number(c.getAttribute("data-com")); });

              if (s) { s.on = !s.on; c.classList.toggle("on", s.on); c.classList.toggle("gold", s.on); }

            });

          });

        } else if (tab === "posiciones") {

          body.innerHTML =

            '<div class="card-sec"><h3>Compatibilidad por posición</h3>' +

            '<p class="mut small" style="margin-bottom:12px">A = natural · B = puede jugar · — = no registrada.</p><div class="form-grid3">' +

            (d.positions || []).map(function (p) {

              var item = A.POS_LIST[p.index];

              var nm = item ? ((A.getPosLang() === "es") ? (item.es + " (" + item.desc + ")") : (item.en + " (" + item.descEn + ")")) : ("Pos " + p.index);

              return '<div class="field"><label>' + esc(nm) + '</label><select class="input" data-posidx="' + p.index + '">' +

                '<option value="0"' + (p.level === 0 ? " selected" : "") + '>—</option>' +

                '<option value="1"' + (p.level === 1 ? " selected" : "") + '>B</option>' +

                '<option value="2"' + (p.level === 2 ? " selected" : "") + ">A</option></select></div>";

            }).join("") + "</div></div>";

          A.$$("[data-posidx]", body).forEach(function (sel) {

            sel.addEventListener("change", function () {

              var p = d.positions.find(function (x) { return x.index === Number(sel.getAttribute("data-posidx")); });

              if (p) p.level = Number(sel.value);

            });

          });

        }

      }

      renderTab();



      function drawRadar() {

        var cv = box.querySelector("#pRadar");

        if (!cv) return;

        var ab = d.abilities, avg = function () { var s = 0; for (var i = 0; i < arguments.length; i++) s += ab[arguments[i]] || 0; return s / arguments.length; };

        var axes = d.pos === "GK"

          ? [["Portero", ab.gk || 0], ["Atajar", ab.catching || 0], ["Despeje", ab.clearing || 0], ["Reflejos", ab.reflex || 0], ["Cobertura", ab.cover || 0], ["Salto", ab.jump || 0]]

          : [["Ataque", ab.atk || 0], ["Regate", ab.drib || 0], ["Pase", avg("lowpass", "loftpass")], ["Definición", ab.finish || 0], ["Defensa", avg("def", "ball_win")], ["Velocidad", ab.speed || 0]];

        var ctx = cv.getContext("2d"), cx = 125, cy = 125, R = 88;

        ctx.clearRect(0, 0, 250, 250);

        function pt(i, r) { var a = -Math.PI / 2 + i * (Math.PI * 2 / 6); return [cx + Math.cos(a) * r, cy + Math.sin(a) * r]; }

        ctx.strokeStyle = "rgba(148,163,184,.35)"; ctx.lineWidth = 1;

        [0.33, 0.66, 1].forEach(function (f) {

          ctx.beginPath();

          for (var i = 0; i < 6; i++) { var p = pt(i, R * f); i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1]); }

          ctx.closePath(); ctx.stroke();

        });

        ctx.beginPath();

        axes.forEach(function (ax, i) { var p = pt(i, R * Math.max(0, Math.min(99, ax[1])) / 99); i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1]); });

        ctx.closePath();

        ctx.fillStyle = "rgba(0,229,255,.28)"; ctx.fill();

        ctx.strokeStyle = "#00E5FF"; ctx.lineWidth = 2; ctx.stroke();

        ctx.fillStyle = "#C7D2E4"; ctx.font = "600 10.5px Segoe UI"; ctx.textAlign = "center";

        axes.forEach(function (ax, i) { var p = pt(i, R + 16); ctx.fillText(ax[0], p[0], p[1] + 3); });

      }



      box.querySelector("#pSave").addEventListener("click", function () {

        var payload = { id: d.id, abilities: d.abilities, skills: d.skills, comStyles: d.comStyles, positions: d.positions };

        var q = function (id) { var n = box.querySelector("#" + id); return n ? n.value : null; };

        if (q("dName") !== null) {

          payload.name = q("dName"); payload.shirtName = q("dShirt");

          payload.natId = Number(q("dNat")) || 0; payload.posIndex = Number(q("dPos")) || 0;

          payload.age = Number(q("dAge")) || 0; payload.height = Number(q("dHeight")) || 0; payload.weight = Number(q("dWeight")) || 0;

          payload.weakFootUsage = Number(q("dWfu")) || 0; payload.weakFootAccuracy = Number(q("dWfa")) || 0;

          payload.form = Number(q("dForm")) || 0; payload.injury = Number(q("dInjury")) || 0;

          payload.callnameId = Number(q("dCallname")) || 0;

          payload.leftFooted = q("dFoot") === "1";

          var stEl = box.querySelector("#dStyle"); if (stEl) payload.style = Number(stEl.value) || 0;

        }

        // Las habilidades se leen siempre de los inputs si la tab esta viva

        A.$$("[data-abil]", box).forEach(function (inp) { payload.abilities[inp.getAttribute("data-abil")] = Number(inp.value) || 0; });

        rpc("updatePlayer", payload).then(function () {

          toast("Jugador guardado (se escribe al Guardar (.bin)).");

          return A.reloadState();

        }).then(function () { selectPlayer(d.id); }).catch(function (e) { toast(e.message, true); });

      });

    }



    A.ensureCountries().then(function () { loadList(true); });

    return Promise.resolve();

  });

})();

