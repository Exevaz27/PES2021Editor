// ============================================================



// Seccion: Equipos (gestor completo master-detail)



// ============================================================



(function () {



  "use strict";



  var A = window.App, rpc = A.rpc, esc = A.esc, toast = A.toast;



  var search = "", scope = "all", continent = 0, leagueFilter = -1;



  var selectedId = null, detail = null, leaguesCache = null;



  var activeTeamTab = "plantilla";



  var playerLook = localStorage.getItem("pes_tactics_look") || "kits";



  var showPlayerIds = localStorage.getItem("pes_tactics_ids") === "1";



  var selectedStarterFi = null;



  var playerFaceCache = new Map();







  A.register("equipos", function (page) {



    if (!A.S.dbLoaded) {



      page.innerHTML = '<div class="empty-state"><div class="empty-ico">🛡️</div><h2>Sin base cargada</h2><p>Cargá tu pesdb desde Configuración para gestionar los equipos.</p></div>';



      return Promise.resolve();



    }



    page.innerHTML =



      '<div class="action-bar">' +



      '<button class="btn btn-primary" id="tNew">➕ Nuevo club</button>' +



      '<button class="btn btn-ghost" id="tClone">📋 Clonar</button>' +



      '<button class="btn danger" id="tDelete">🗑️ Eliminar</button>' +



      '<button class="btn gold" id="tBatch">⚡ Creador masivo</button>' +



      '<button class="btn btn-ghost" id="tGenerate">🎲 Generar equipos</button>' +



      '<button class="btn btn-ghost" id="tImport">🌍 Importar equipos</button>' +



      '<span class="spacer"></span>' +



      '<span class="inline-group"><select class="input" id="tAssignLeague"></select>' +



      '<button class="btn btn-ghost" id="tAssign">Asignar a liga</button></span>' +



      '<button class="btn btn-ghost" id="tExportCsv">📤 CSV</button>' +



      '<button class="btn btn-ghost" id="tImportCsv">📥 CSV</button>' +



      A.renderPosToggle() +



      "</div>" +



      '<div class="md"><div class="md-left"><div class="list-panel">' +



      '<div class="lp-head">' +



      '<input class="input" id="tSearch" placeholder="Buscar equipo, sigla o ID…" value="' + esc(search) + '">' +



      '<div class="chips">' +



      chip("all", "Todos") + chip("clubs", "Clubes") + chip("nationals", "Selecciones") + chip("free", "Libres") +



      "</div>" +



      '<select class="input" id="tContinent"><option value="0">Todos los continentes</option><option value="2">Europa</option><option value="4">América del Sur</option><option value="3">Asia-Oceanía</option><option value="5">Norteamérica</option><option value="6">África</option><option value="1">Otros</option></select>' +



      '<select class="input" id="tLeague"><option value="-1">Todas las ligas</option></select>' +



      '<div class="mut" id="tCount"></div>' +



      "</div>" +



      '<div class="lp-body" id="tList"></div>' +



      "</div></div>" +



      '<div class="md-right" id="tDetail"><div class="empty-state"><div class="empty-ico">👈</div><h2>Elegí un equipo</h2><p>Seleccioná un equipo para ver su ficha, plantilla y tácticas.</p></div></div>' +



      "</div>";







    function chip(f, l) { return '<button class="chip' + (scope === f ? " on" : "") + '" data-f="' + f + '">' + l + "</button>"; }







    // ligas para filtros y asignacion



    rpc("listCompetitions", { filter: "leagues" }).then(function (r) {



      leaguesCache = r.competitions;



      var sel = page.querySelector("#tLeague"), asg = page.querySelector("#tAssignLeague");



      r.competitions.forEach(function (c) {



        sel.appendChild(A.el('<option value="' + c.id + '">' + esc(c.name) + "</option>"));



        asg.appendChild(A.el('<option value="' + c.id + '">' + esc(c.name) + "</option>"));



      });



      asg.appendChild(A.el('<option value="0">— Otros equipos (desvincular) —</option>'));



      sel.value = String(leagueFilter);



    }).catch(function () { });







    A.$$(".chip", page).forEach(function (c) {



      c.addEventListener("click", function () {



        scope = c.getAttribute("data-f");



        A.$$(".chip", page).forEach(function (x) { x.classList.toggle("on", x === c); });



        loadList();



      });



    });



    var deb = null;



    page.querySelector("#tSearch").addEventListener("input", function (e) { clearTimeout(deb); deb = setTimeout(function () { search = e.target.value; loadList(); }, 220); });



    page.querySelector("#tContinent").value = String(continent);



    page.querySelector("#tContinent").addEventListener("change", function (e) { continent = Number(e.target.value); loadList(); });



    page.querySelector("#tLeague").addEventListener("change", function (e) { leagueFilter = Number(e.target.value); loadList(); });







    function loadList() {



      rpc("listTeams", { search: search, scope: scope, continent: continent, leagueId: leagueFilter, limit: 800 }).then(function (r) {



        page.querySelector("#tCount").textContent = r.total + " equipos";



        var list = page.querySelector("#tList");



        list.innerHTML = "";



        r.teams.forEach(function (t) {



          var teamName = (t.name && t.name.trim()) || (t.nameSpanish && t.nameSpanish.trim()) || (t.nameEnglish && t.nameEnglish.trim()) || A.teamName(t) || ("Equipo " + t.id);



          var shortName = t.shortName || (teamName.length >= 3 ? teamName.substring(0, 3).toUpperCase() : teamName);



          var b = A.el('<button class="row' + (t.id === selectedId ? " sel" : "") + '" data-id="' + t.id + '">' +



            A.crestHtml(t.id, teamName, shortName) +



            '<span><span class="nm-line"><span class="nm">' + esc(teamName) + "</span>" +



            (t.isNew ? '<span class="pill new">NUEVO</span>' : "") + (t.isNational ? '<span class="pill nat">SEL</span>' : "") +



            "</span>" +



            '<span class="sub">' + (t.leagueName ? esc(t.leagueName) : "Libre") + " · " + esc(t.countryName || "") + " · " + (t.playerCount || 0) + ' jug.</span></span>' +



            '<span class="r-meta">ID ' + t.id + "</span></button>");



          b.addEventListener("click", function () { selectTeam(t.id); });



          list.appendChild(b);



        });



      }).catch(function (e) { toast(e.message, true); });



    }







    function selectTeam(id) {



      selectedId = id;



      A.$$(".row", page).forEach(function (x) { x.classList.toggle("sel", Number(x.getAttribute("data-id")) === id); });



      rpc("getTeam", { id: id }).then(function (r) { detail = r.team; renderDetail(); })



        .catch(function (e) { toast(e.message, true); });



    }



    A.selectTeam = selectTeam;







    function updatePosUi() {



      if (selectedId && detail) renderDetail();



    }



    A.bindPosToggle(page, function () { updatePosUi(); });



    window.addEventListener("poslangchange", updatePosUi);







    // ---------- acciones globales ----------



    page.querySelector("#tNew").addEventListener("click", function () { teamFormModal(null); });



    page.querySelector("#tClone").addEventListener("click", function () {



      if (!detail) { toast("Elegí primero un equipo.", true); return; }



      var tName = A.teamName(detail);



      A.confirmModal("Clonar equipo", "Se crea una copia de «" + tName + "» con ID nuevo.", "Clonar", function () {



        rpc("cloneTeam", { id: detail.id }).then(function (r) { toast("Equipo clonado: ID " + r.team.id); loadList(); selectTeam(r.team.id); }).catch(function (e) { toast(e.message, true); });



      });



    });



    page.querySelector("#tDelete").addEventListener("click", function () {



      if (!detail) { toast("Elegí primero un equipo.", true); return; }



      var tName = A.teamName(detail);



      A.confirmModal("Eliminar equipo", "Se elimina «" + tName + "» (ID " + detail.id + ") y sus asignaciones. ¿Seguro?", "Eliminar", function () {



        rpc("deleteTeam", { id: detail.id }).then(function () {



          toast("Equipo eliminado."); selectedId = null; detail = null;



          page.querySelector("#tDetail").innerHTML = '<div class="empty-state"><div class="empty-ico">👈</div><h2>Elegí un equipo</h2><p>Seleccioná un equipo para ver su ficha, plantilla y tácticas.</p></div>';



          loadList();



        }).catch(function (e) { toast(e.message, true); });



      });



    });



    page.querySelector("#tBatch").addEventListener("click", function () { batchModal("batchCreateTeams", "Creador masivo de equipos", true); });



    page.querySelector("#tGenerate").addEventListener("click", function () { batchModal("generateTeams", "Generar equipos con nombres temáticos", false); });



    page.querySelector("#tImport").addEventListener("click", function () {



      A.ensureCountries().then(function () {



        var helps = {



          newlife: "La base de clubes reales en formato NewLife: una carpeta (o las partes .zip) con newlife.json, clubs.csv y players.csv. Los ratings ya vienen en la escala del juego.",



          simple: "Un CSV, una fila por jugador: <b>Equipo, Jugador, Posicion</b> obligatorias. Opcionales: Pais, Media (siembra las habilidades), IdJuego, Edad, Altura, Peso, Pie, Nacionalidad y las habilidades por su nombre en ingles (Speed, Finishing, ...).",



          fm: "Un CSV con columnas de Football Manager (Finishing, Passing, Pace, ... en escala <b>1 a 20</b>): se convierten a la escala del juego (1 = 40, 20 = 99) y las posiciones tipo ST (C) o AM (L) se traducen solas.",



          eafc: "Un CSV tipo SoFIFA / EA FC: atributos 0 a 99 con sus nombres (SprintSpeed, ShortPassing, ...) y posiciones como ST, CAM o CDM, que se traducen a las del juego."



        };



        A.showModal("Importar equipos",



          '<div class="field"><label>¿Qué vas a importar?</label>' +



          '<select class="input" id="impFormat">' +



          '<option value="simple">CSV simple (escala del juego, 1-99)</option>' +



          '<option value="fm">CSV de Football Manager (atributos 1-20)</option>' +



          '<option value="eafc">CSV de EA FC / SoFIFA (atributos 0-99)</option>' +



          '<option value="newlife">Paquete NewLife (carpeta o .zip)</option>' +



          "</select></div>" +



          '<p class="mut" id="impHelp" style="margin-top:10px">' + helps.simple + "</p>" +



          '<div class="field" id="impCountryField" style="margin-top:10px"><label>País por defecto (para los clubes sin columna Pais)</label>' +



          '<select class="input" id="impCountry">' + A.countryOptions(4) + "</select></div>",



          [{ label: "Cancelar" }, {



            label: "Continuar", cls: "btn-primary", onClick: function () {



              var formato = document.querySelector("#impFormat").value;



              var countryId = Number(document.querySelector("#impCountry").value) || 0;



              if (formato === "newlife") startNewLifeImport();



              else startCsvImport(formato, countryId);



            }



          }]);



        var sel = document.querySelector("#impFormat");



        sel.addEventListener("change", function () {



          document.querySelector("#impHelp").innerHTML = helps[sel.value];



          document.querySelector("#impCountryField").style.display = sel.value === "newlife" ? "none" : "";



        });



      });



    });







    function startNewLifeImport() {



      rpc("pickFolder", { title: "Elegí la carpeta de la base de clubes reales (trae newlife.json, clubs.csv y players.csv, o las partes .zip)" }).then(function (r) {



        if (!r.path) return;



        return rpc("previewNewLife", { folder: r.path }).then(function (pv) {



          var estadoTag = { nuevo: '<span class="tag ok">nuevo</span>', existente: '<span class="tag warn">ya está en el juego</span>', "ya-importado": '<span class="tag warn">ya importado</span>' };



          var html = '<p class="mut">' + esc(pv.name) + " · versión " + esc(pv.version) + "</p>";



          (pv.warnings || []).forEach(function (w) { html += '<p class="mut" style="color:var(--gold)">' + esc(w) + "</p>"; });



          pv.clubs.forEach(function (c) {



            html += '<div class="bk-row">' + (estadoTag[c.estado] || "") +



              '<div style="flex:1; min-width:0"><span class="nm">' + esc(c.nombre) + '</span>' +



              '<span class="sub">' + esc(c.liga || "Sin liga") + (c.estado === "nuevo"



                ? " · plantel " + c.entran + " (" + c.nuevos + " jugadores nuevos, " + c.reutilizados + " reutilizados)" +



                  (c.fuera ? " · " + c.fuera + " quedan fuera del plantel (tope 30)" : "") +



                  (c.quedan && c.quedan.length ? " · " + c.quedan.length + " quedan en su club" : "")



                : "") + "</span>" +



              (c.avisos || []).map(function (av) { return '<span class="sub" style="color:var(--gold)">' + esc(av) + "</span>"; }).join("") +



              "</div></div>";



          });



          html += '<p class="mut small">Los clubes que el juego ya tiene no se tocan, y ningún jugador se mueve de su club: los que ya juegan en otro equipo quedan donde están y se reportan. Antes de importar se crea un punto de restauración.</p>';



          var hayNuevos = pv.clubs.some(function (c) { return c.estado === "nuevo"; });



          var buttons = [{ label: "Cancelar" }];



          if (hayNuevos) buttons.push({



            label: "🌍 Importar", cls: "btn-primary", onClick: function () {



              rpc("applyNewLife", { folder: r.path }).then(function (res) {



                toast(res.message);



                A.showModal("Resultado de la importación",



                  res.results.map(function (x) {



                    return '<div class="bk-row"><span class="tag ' + (x.creado ? "ok" : "warn") + '">' + (x.creado ? "creado" : "—") + '</span><div style="flex:1; min-width:0"><span class="nm">' + esc(x.club) + '</span><span class="sub">' + esc(x.mensaje) + "</span></div></div>";



                  }).join("") + '<p class="mut" style="margin-top:10px">Para que los clubes queden en el juego: <b>Guardar (.bin)</b> y redesplegar. Las ligas se arman con Crear Liga o con un preset, usando estos clubes ya creados.</p>',



                  [{ label: "Cerrar" }]);



                loadList(); A.reloadState();



              }).catch(function (e) { toast(e.message, true); });



            }



          });



          A.showModal("Importar equipos — NewLife", html, buttons);



        });



      }).catch(function (e) { toast(e.message, true); });



    }







    function startCsvImport(format, countryId) {



      var titles = { simple: "Importar plantel desde CSV", fm: "Importar desde Football Manager", eafc: "Importar desde EA FC" };



      rpc("pickFile", { title: "Elegí el CSV de jugadores", filter: "CSV (*.csv)|*.csv" }).then(function (r) {



        if (!r.path) return;



        return rpc("previewCsvPlayers", { path: r.path, countryId: countryId, format: format }).then(function (pv) {



          var estadoTag = { nuevo: '<span class="tag ok">nuevo</span>', "existente-libre": '<span class="tag ok">existe: se le carga el plantel</span>', "ya-tiene-plantel": '<span class="tag warn">ya tiene plantel</span>' };



          var html = "";



          if (format === "fm") html += '<p class="mut">Las medias ya están <b>convertidas de la escala 1-20 de Football Manager</b> a la del juego.</p>';



          if (format === "eafc") html += '<p class="mut">Atributos y posiciones <b>traducidos desde el formato EA FC</b>.</p>';



          (pv.warnings || []).forEach(function (w) { html += '<p class="mut" style="color:var(--gold)">' + esc(w) + "</p>"; });



          pv.clubs.forEach(function (c) {



            html += '<div class="bk-row">' + (estadoTag[c.estado] || "") +



              '<div style="flex:1; min-width:0"><span class="nm">' + esc(c.nombre) + "</span>" +



              '<span class="sub">' + (c.estado === "ya-tiene-plantel" ? "No se toca." :



                "entran " + c.entran + " jugadores (" + c.nuevos + " nuevos, " + c.reutilizados + " reutilizados)" +



                (c.fuera ? " · " + c.fuera + " quedan fuera (tope 30)" : "") +



                (c.quedan && c.quedan.length ? " · " + c.quedan.length + " quedan en su club" : "")) + "</span>" +



              (c.avisos || []).map(function (av) { return '<span class="sub" style="color:var(--gold)">' + esc(av) + "</span>"; }).join("") +



              "</div></div>";



          });



          html += '<p class="mut small">Ningún jugador se mueve de su club: el que ya juega en otro equipo queda donde está y se reporta. Antes de importar se crea un punto de restauración.</p>';



          var hay = pv.clubs.some(function (c) { return c.estado !== "ya-tiene-plantel"; });



          var buttons = [{ label: "Cancelar" }];



          if (hay) buttons.push({



            label: "📋 Importar", cls: "btn-primary", onClick: function () {



              rpc("applyCsvPlayers", { path: r.path, countryId: countryId, format: format }).then(function (res) {



                toast(res.message);



                A.showModal("Resultado de la importación",



                  res.results.map(function (x) {



                    return '<div class="bk-row"><span class="tag ' + (x.creado ? "ok" : "warn") + '">' + (x.creado ? "listo" : "—") + '</span><div style="flex:1; min-width:0"><span class="nm">' + esc(x.club) + '</span><span class="sub">' + esc(x.mensaje) + "</span></div></div>";



                  }).join("") + '<p class="mut" style="margin-top:10px">Para que quede en el juego: <b>Guardar (.bin)</b> y redesplegar.</p>',



                  [{ label: "Cerrar" }]);



                loadList(); A.reloadState();



              }).catch(function (e) { toast(e.message, true); });



            }



          });



          A.showModal(titles[format] || titles.simple, html, buttons);



        });



      }).catch(function (e) { toast(e.message, true); });



    }



    page.querySelector("#tAssign").addEventListener("click", function () {



      if (!detail) { toast("Elegí primero un equipo.", true); return; }



      var compId = Number(page.querySelector("#tAssignLeague").value);



      rpc("assignTeamToLeague", { teamIds: [detail.id], compId: compId }).then(function (r) {



        toast(compId === 0 ? "Equipo desvinculado de su liga." : "Equipo asignado (" + r.moved + ").");



        loadList(); selectTeam(detail.id);



      }).catch(function (e) { toast(e.message, true); });



    });



    page.querySelector("#tExportCsv").addEventListener("click", function () {



      rpc("saveFile", { title: "Exportar equipos a CSV", filter: "CSV (*.csv)|*.csv", defaultName: "equipos.csv" }).then(function (r) {



        if (!r.path) return;



        return rpc("exportTeamsCsvFile", { path: r.path });



      }).then(function (r) { if (r) toast("CSV exportado."); }).catch(function (e) { toast(e.message, true); });



    });



    page.querySelector("#tImportCsv").addEventListener("click", function () {



      rpc("pickFile", { title: "Importar equipos desde CSV", filter: "CSV (*.csv)|*.csv" }).then(function (r) {



        if (!r.path) return;



        return rpc("importTeamsCsvFile", { path: r.path });



      }).then(function (r) {



        if (!r) return;



        toast("Importados " + r.imported + " equipos." + (r.skipped && r.skipped.length ? " Saltados: " + r.skipped.length : ""));



        loadList();



      }).catch(function (e) { toast(e.message, true); });



    });







    function batchModal(action, title, allowTarget) {



      A.ensureCountries().then(function () {



        A.showModal(title,



          '<div class="form-grid">' +



          '<div class="field"><label>Cantidad</label><input class="input" type="number" id="bCount" value="4" min="1" max="60"></div>' +



          '<div class="field"><label>Prefijo / tema</label><input class="input" id="bPrefix" value="Club"></div>' +



          '<div class="field"><label>País</label><select class="input" id="bCountry">' + A.countryOptions(0) + "</select></div>" +



          (allowTarget



            ? '<div class="field"><label>Liga destino (opcional)</label><select class="input" id="bLeague"><option value="0">— Sin liga —</option>' + (leaguesCache || []).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + "</option>"; }).join("") + "</select></div>"



            : '<p class="mut small" style="align-self:end">Los equipos generados quedan libres, sin liga asignada.</p>') +



          "</div>",



          [{ label: "Cancelar" }, {



            label: "Crear", cls: "btn-primary", onClick: function () {



              rpc(action, {



                count: Number(document.querySelector("#bCount").value) || 0,



                prefix: document.querySelector("#bPrefix").value,



                countryId: Number(document.querySelector("#bCountry").value) || 0,



                targetCompId: allowTarget ? (Number(document.querySelector("#bLeague").value) || 0) : 0



              }).then(function (r) { toast("Creados " + r.created + " equipos."); loadList(); }).catch(function (e) { toast(e.message, true); });



            }



          }]);



      });



    }







    function teamFormModal(team) {
      if (!team) { createTeamModal(); return; }



      A.ensureCountries().then(function () {



        rpc("createLeagueOptions").then(function (opts) {



          var t = team || { nameSpanish: "", nameEnglish: "", shortName: "", countryId: 0, stadiumId: 0, coachId: 0 };



          A.showModal(team ? "Editar equipo" : "Nuevo club",



            '<div class="form-grid">' +



            '<div class="field"><label>Nombre español *</label><input class="input" id="nNameSp" value="' + esc(t.nameSpanish) + '"></div>' +



            '<div class="field"><label>Nombre internacional</label><input class="input" id="nNameEn" value="' + esc(t.nameEnglish) + '"></div>' +



            '<div class="field"><label>Sigla (3-4)</label><input class="input" id="nShort" maxlength="4" value="' + esc(t.shortName) + '"></div>' +



            '<div class="field"><label>País</label><select class="input" id="nCountry">' + A.countryOptions(t.countryId) + "</select></div>" +



            "</div>",



            [{ label: "Cancelar" }, {



              label: team ? "Guardar" : "Crear club", cls: "btn-primary", onClick: function () {



                var payload = {



                  nameSpanish: document.querySelector("#nNameSp").value,



                  nameEnglish: document.querySelector("#nNameEn").value,



                  shortName: document.querySelector("#nShort").value,



                  countryId: Number(document.querySelector("#nCountry").value) || 0



                };



                if (team) {



                  payload.id = team.id; payload.stadiumId = team.stadiumId; payload.coachId = team.coachId; payload.isNational = team.isNational;



                  rpc("updateTeam", payload).then(function () { toast("Equipo actualizado."); loadList(); selectTeam(team.id); }).catch(function (e) { toast(e.message, true); });



                } else {



                  rpc("createTeam", payload).then(function (r) { toast("Club creado: ID " + r.team.id); loadList(); selectTeam(r.team.id); }).catch(function (e) { toast(e.message, true); });



                }



              }



            }]);



        });



      });



    }

    // Alta de club completa (espejo de CreateNewTeamDialog del escritorio, con liga opcional)
    function createTeamModal() {
      A.ensureCountries().then(function () {
        return rpc("createTeamOptions");
      }).then(function (o) {
        var emblemPath = null;
        A.showModal("Crear Nuevo Club - PES 2021",
          '<div class="form-grid">' +
          '<div class="field"><label>Nombre del Club (Español) *</label><input class="input" id="nNameSp" value=""></div>' +
          '<div class="field"><label>Nombre Internacional (Inglés)</label><input class="input" id="nNameEn" value=""></div>' +
          '<div class="field"><label>Sigla (3-4 letras)</label><input class="input" id="nShort" maxlength="4" value=""></div>' +
          '<div class="field"><label>País / Nacionalidad *</label><select class="input" id="nCountry">' + A.countryOptions(0) + "</select></div>" +
          '<div class="field"><label>Estadio Asignado</label><select class="input" id="nStadium"><option value="0">— Sin estadio —</option>' + (o.stadiums || []).map(function (st) { return '<option value="' + st.id + '">' + esc(st.name) + "</option>"; }).join("") + "</select></div>" +
          '<div class="field"><label>Director Técnico (DT)</label><select class="input" id="nCoach"><option value="0">Automático (crea su DT propio)</option>' + (o.coaches || []).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + "</option>"; }).join("") + "</select></div>" +
          '<div class="field"><label>Asignar a Liga / Categoría (opcional)</label><select class="input" id="nLeague"><option value="0">— Sin liga (libre) —</option>' + (o.leagues || []).map(function (c) { return '<option value="' + c.id + '">' + esc(c.name) + " (ID " + c.id + ")</option>"; }).join("") + "</select></div>" +
          '<div class="field"><label>Team ID asignado (Libre)</label><input class="input" type="number" id="nTeamId" value="' + (o.nextTeamId || 0) + '"></div>' +
          "</div>" +
          '<div style="display:flex; gap:8px; align-items:center; margin-top:10px"><button class="btn btn-ghost sm" id="nEmblemBtn">📁 Escudo…</button><span class="small mut" id="nEmblemName">Sin escudo (se puede poner después en la ficha)</span></div>',
          [{ label: "Cancelar" }, {
            label: "✔ Crear Club", cls: "btn-primary", onClick: function () {
              var payload = {
                nameSpanish: document.querySelector("#nNameSp").value,
                nameEnglish: document.querySelector("#nNameEn").value,
                shortName: document.querySelector("#nShort").value,
                countryId: Number(document.querySelector("#nCountry").value) || 0,
                stadiumId: Number(document.querySelector("#nStadium").value) || 0,
                coachId: Number(document.querySelector("#nCoach").value) || 0,
                targetCompId: Number(document.querySelector("#nLeague").value) || 0,
                explicitId: Number(document.querySelector("#nTeamId").value) || 0
              };
              if (!payload.nameSpanish.trim()) { toast("Poné el nombre del club.", true); return; }
              rpc("createTeam", payload).then(function (r) {
                function done() { toast("Club creado: ID " + r.team.id); loadList(); selectTeam(r.team.id); }
                if (emblemPath) {
                  rpc("setTeamEmblemFile", { path: emblemPath, id: r.team.id }).then(done).catch(function (e) { toast("El club se creó, pero el escudo no se pudo aplicar: " + e.message, true); done(); });
                } else done();
              }).catch(function (e) { toast(e.message, true); });
            }
          }]);
        var eb = document.querySelector("#nEmblemBtn");
        if (eb) eb.addEventListener("click", function () {
          rpc("pickFile", { title: "Elegí la imagen del escudo", filter: "Imágenes (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp" }).then(function (r) {
            if (r && r.path) { emblemPath = r.path; document.querySelector("#nEmblemName").textContent = "Escudo: " + r.path.split(/[\\/]/).pop(); }
          }).catch(function (e) { toast(e.message, true); });
        });
      }).catch(function (e) { toast(e.message, true); });
    }




    A.teamFormModal = teamFormModal;







    var FORMATION_CATALOG = [



      { name: "4-4-2", cat: "4" },



      { name: "4-3-3", cat: "4" },



      { name: "4-2-3-1", cat: "4" },



      { name: "4-1-2-1-2", cat: "4" },



      { name: "4-1-4-1", cat: "4" },



      { name: "4-2-1-3", cat: "4" },



      { name: "4-2-2-2", cat: "4" },



      { name: "4-3-1-2", cat: "4" },



      { name: "4-1-3-2", cat: "4" },



      { name: "4-3-2-1", cat: "4" },



      { name: "4-2-4", cat: "4" },



      { name: "3-5-2", cat: "3" },



      { name: "3-4-3", cat: "3" },



      { name: "3-4-1-2", cat: "3" },



      { name: "3-4-2-1", cat: "3" },



      { name: "3-2-4-1", cat: "3" },



      { name: "3-3-3-1", cat: "3" },



      { name: "5-3-2", cat: "5" },



      { name: "5-4-1", cat: "5" },



      { name: "5-2-1-2", cat: "5" },



      { name: "5-2-2-1", cat: "5" },



      { name: "5-2-3", cat: "5" }



    ];







    var ADV_INSTRUCTIONS = {



      att: [



        { code: 1, name: "Ensanchar la cancha", desc: "Los jugadores de banda juegan pegados a la línea lateral para abrir la defensa rival." },



        { code: 2, name: "Falso nueve", desc: "El delantero centro baja a recibir y deja espacio para que lleguen los de segunda línea." },



        { code: 3, name: "Falsos laterales", desc: "Los laterales se cierran hacia el medio para ayudar en la creación." },



        { code: 4, name: "Laterales ofensivos", desc: "Los laterales suben con frecuencia para sumarse al ataque." },



        { code: 5, name: "Rotación en banda", desc: "Los jugadores de banda intercambian posiciones para desordenar la marca." },



        { code: 6, name: "Tiqui-taca", desc: "Pases cortos y movimiento constante para tener la pelota y desordenar al rival." },



        { code: 7, name: "Centros al área", desc: "Varios jugadores entran al área cuando se centra desde la banda." },



        { code: 9, name: "Falso extremo", desc: "El extremo se cierra hacia el medio y deja la banda libre para el lateral." }



      ],



      def: [



        { code: 11, name: "Poblar el área", desc: "Cuando el rival centra, varios jugadores vuelven a defender dentro del área." },



        { code: 12, name: "Defensiva atrasada", desc: "La línea defensiva se queda atrás para cubrir el espacio a su espalda." },



        { code: 13, name: "Presión alta", desc: "Se presiona en campo rival para recuperar la pelota rápido. Exige mucha resistencia." },



        { code: 14, name: "Marcaje estrecho", desc: "Se marca muy de cerca al rival que recibe la pelota." },



        { code: 15, name: "Delantero liberado", desc: "Un delantero no baja a defender y espera arriba para salir de contraataque." },



        { code: 16, name: "Carrilero", desc: "Los de banda bajan a defender su costado como carrileros." }



      ]



    };







    // ---------- detalle ----------



    function renderDetail() {



      var d = detail, box = page.querySelector("#tDetail");
      var teamRivals = (d.rivals || []).slice(0, 3);
      while (teamRivals.length < 3) teamRivals.push({ id: 0, name: "", shortName: "" });



      var starterPids = new Set((d.eleven || []).map(function (x) { return x.playerId; }));



      var rosterByPid = new Map((d.roster || []).map(function (p) { return [p.playerId, p]; }));



      var benchPlayers = (d.roster || []).filter(function (p) { return !starterPids.has(p.playerId); });



      var pitchTheme = localStorage.getItem("pes_pitch_theme") || "clasico";



      var pitchBgUrl = (A.pitchTexture ? A.pitchTexture(pitchTheme, d.color1, d.color2) : "");







      function spOptions(curPid) {



        curPid = Number(curPid) || 0;



        var starters = (d.eleven || []);



        var bPlayers = benchPlayers;



        return '<option value="0"' + (curPid === 0 ? " selected" : "") + '>— Sin asignar —</option>' +



          '<optgroup label="Titulares">' +



            starters.map(function (tp) {



              var rp = rosterByPid.get(tp.playerId);



              var ovr = rp ? rp.ovr : 70;



              return '<option value="' + tp.playerId + '"' + (tp.playerId === curPid ? ' selected' : '') + '>' +



                '#' + tp.shirt + ' ' + esc(tp.playerName) + ' (' + A.posLabel(tp.roleName) + ' · ' + ovr + ')' +



              '</option>';



            }).join("") +



          '</optgroup>' +



          (bPlayers.length ? (



            '<optgroup label="Suplentes">' +



              bPlayers.map(function (bp) {



                return '<option value="' + bp.playerId + '"' + (bp.playerId === curPid ? ' selected' : '') + '>' +



                  '#' + bp.shirt + ' ' + esc(bp.name) + ' (' + A.posLabel(bp.pos) + ' · ' + bp.ovr + ')' +



                '</option>';



              }).join("") +



            '</optgroup>'



          ) : '');



      }







            var stData = d.strategy || { attackStyle: 0, buildUp: 1, attackArea: 0, positioning: 0, defensiveStyle: 0, containmentArea: 0, pressuring: 0, defensiveLine: 5, compactness: 5, supportRange: 5 };



      var curAdvAtt1 = (stData.advAtt1 || 0);



      var curAdvAtt2 = (stData.advAtt2 || 0);



      var curAdvDef1 = (stData.advDef1 || 0);



      var curAdvDef2 = (stData.advDef2 || 0);



      var lineCounts = { def: 0, mid: 0, fwd: 0 };



      (d.eleven || []).filter(function (f) { return f.squadIndex > 0 && f.squadIndex < 11; }).forEach(function (f) {



        if (f.y <= 16) lineCounts.def++;



        else if (f.y <= 34) lineCounts.mid++;



        else lineCounts.fwd++;



      });



      var xiRatings = (d.eleven || []).filter(function (f) { return f.squadIndex < 11; }).map(function (f) {



        var rp = rosterByPid.get(f.playerId);



        return rp ? rp.ovr : 70;



      });



      var avgOvr = xiRatings.length ? Math.round(xiRatings.reduce(function (a, b) { return a + b; }, 0) / xiRatings.length) : 75;



      var curFormName = d.formationName || (lineCounts.def + "-" + lineCounts.mid + "-" + lineCounts.fwd);



var curCaptain = ((d.roster || []).find(function (p) { return p.captain; }) || {}).playerId || 0;



      var curPenalty = ((d.roster || []).find(function (p) { return p.penalty; }) || {}).playerId || 0;



      var curShortFk = ((d.roster || []).find(function (p) { return p.shortFk; }) || {}).playerId || 0;



      var curLongFk = ((d.roster || []).find(function (p) { return p.longFk; }) || {}).playerId || 0;



      var curLeftCorner = ((d.roster || []).find(function (p) { return p.leftCorner; }) || {}).playerId || 0;



      var curRightCorner = ((d.roster || []).find(function (p) { return p.rightCorner; }) || {}).playerId || 0;



      var teamName = A.teamName(d);



      var shortName = d.shortName || (teamName.length >= 2 ? teamName.substring(0, 2).toUpperCase() : teamName);



      box.innerHTML =



        '<div class="detail-head">' +



        '<div class="crest big" id="tEmblem" style="--ca:' + A.crestColor(d.id) + ';--cb:' + A.crestColor2(d.id) + '">' + esc(shortName) + "</div>" +



        '<div class="dh-main"><div class="detail-title">' + esc(teamName) + "</div>" +



        '<div class="detail-meta"><span>Team ID <b>' + d.id + "</b></span><span>·</span><span>" + (d.isNational ? "Selección Nacional" : "Club") + "</span>" +



        (d.leagueName ? "<span>·</span><span>🏆 <b>" + esc(d.leagueName) + "</b></span>" : "<span>·</span><span>Libre</span>") +



        "<span>·</span><span>🏳️ <b>" + esc(d.countryName) + "</b></span><span>·</span><span><b>" + d.playerCount + "</b> futbolistas</span></div>" +



        '<div class="btn-row" style="margin-top:14px">' +



        '<button class="btn btn-ghost sm" id="tPickEmblem">🖼️ Escudo…</button>' +



        '<button class="btn btn-ghost sm" id="tEditBasic">✏️ Datos básicos</button>' +



        "</div></div></div>" +







        '<div class="subtabs" id="tSubtabs" style="margin-top:14px">' +



        '<button class="subtab' + (activeTeamTab === "plantilla" ? " on" : "") + '" data-ttab="plantilla">👥 Plantilla <span class="subtab-count">' + (d.roster || []).length + '</span></button>' +



        '<button class="subtab' + (activeTeamTab === "tacticas" ? " on" : "") + '" data-ttab="tacticas">📋 Tácticas y Formación</button>' +



        '<button class="subtab' + (activeTeamTab === "ficha" ? " on" : "") + '" data-ttab="ficha">⚙️ Ficha técnica</button>' +



        '<button class="subtab' + (activeTeamTab === "identidad" ? " on" : "") + '" data-ttab="identidad">🎨 Identidad y Rivales</button>' +



        '</div>' +







        // Tab 1: Plantilla



        '<div class="ttab-pane" id="tPane-plantilla" style="' + (activeTeamTab === "plantilla" ? "" : "display:none") + '">' +



        '<div id="tSquadGen"></div>' +



        '<div class="card-sec"><h3>Plantilla <span class="sub">' + (d.roster || []).length + ' futbolistas</span></h3>' +



        '<table class="tbl"><thead><tr><th style="width:52px">Nº</th><th>Futbolista</th><th style="width:58px">Pos</th><th style="width:58px">Media</th><th style="width:68px">Edad</th><th>Nacionalidad</th><th>Roles</th><th style="width:170px">Acciones</th></tr></thead><tbody>' +

        (d.roster || []).map(function (a) {

          var roles = [a.captain ? "★ Capitán" : "", a.penalty ? "Penales" : "", a.longFk ? "TL largo" : "", a.shortFk ? "TL corto" : "", a.leftCorner ? "Córner izq" : "", a.rightCorner ? "Córner der" : ""].filter(Boolean).join(" · ");

          var ovrColor = (a.ovr >= 90 ? "#FFD166" : a.ovr >= 80 ? "#00E676" : a.ovr >= 70 ? "#7DD956" : a.ovr >= 60 ? "#FF9F43" : "#FF5252");

          var ageDisplay = a.age ? a.age + " años" : "—";

          var natDisplay = (a.natName && a.natName !== "País 0" && a.natName !== "País") ? a.natName : (a.natId === 0 ? "Internacional" : (a.natId ? "País " + a.natId : (a.countryName || "—")));

          return "<tr><td><input class='input small' style='width:52px; text-align:center' type='number' min='1' max='99' value='" + a.shirt + "' data-shirt='" + a.playerId + "'></td>" +

            "<td><a href='javascript:void(0)' class='p-link' data-player-id='" + a.playerId + "' style='color:var(--text); font-weight:700; text-decoration:none' title='Ver / editar futbolista'>" + esc(a.name) + "</a> <span class='mut small'>ID " + a.playerId + "</span></td>" +

            "<td>" + A.posBadge(a.pos) + "</td>" +

            "<td><b style='color:" + ovrColor + "; font-size:13.5px'>" + a.ovr + "</b></td>" +

            "<td><span class='mut small'>" + ageDisplay + "</span></td>" +

            "<td><span class='small' style='font-weight:500; display:inline-flex; align-items:center; gap:5px'>🌐 " + esc(natDisplay) + "</span></td>" +

            "<td class='small mut'>" + esc(roles) + "</td>" +

            "<td><button class='btn btn-ghost xs' data-transfer='" + a.playerId + "'>Traspasar</button> <button class='btn danger xs' data-release='" + a.playerId + "'>Liberar</button></td></tr>";

        }).join("") + "</tbody></table>" +



        '<div style="display:flex; gap:8px; margin-top:14px"><input class="input" id="tAgentSearch" placeholder="Buscar agente libre para fichar…"><select class="input" id="tAgentSelect" style="flex:1"></select><button class="btn btn-primary" id="tSign">Fichar</button>' +



        '<button class="btn btn-ghost" id="tAutoRoles">Auto-asignar roles</button></div></div></div>' +







        // Tab 2: Tacticas y formacion interactiva



        '<div class="ttab-pane" id="tPane-tacticas" style="' + (activeTeamTab === "tacticas" ? "" : "display:none") + '">' +







        // 1. Banner Superior Tactico (inspirado en el repo)



        '<div class="fbanner" style="--ca:' + A.crestColor(d.id) + ';--cb:' + A.crestColor2(d.id) + '">' +



        '  <div class="fbteam">' +



        '    ' + A.crestHtml(d.id, teamName, shortName, true) +



        '    <div class="fbt">' +



        '      <span class="fbig">' + esc(curFormName) + '</span>' +



        '      <span class="fsub">' + esc(teamName) + ' · Esquema táctico activo</span>' +



        '    </div>' +



        '  </div>' +



        '  <div class="frate">' +



        '    <span class="frnum">' + avgOvr + '</span>' +



        '    <div class="frbox">' +



        '      <span class="frl">Media del 11</span>' +



        '      <span class="frv">' + (d.eleven || []).length + ' titulares en cancha</span>' +



        '    </div>' +



        '  </div>' +



        '  <div class="flines">' +



        '    <span class="ln-df">' + lineCounts.def + ' <small>DEF</small></span>' +



        '    <span class="ln-mf">' + lineCounts.mid + ' <small>MED</small></span>' +



        '    <span class="ln-fw">' + lineCounts.fwd + ' <small>ATA</small></span>' +



        '  </div>' +



        '</div>' +







        // 2. Barra de herramientas tactica (Cesped, Fotos/Camisetas, IDs, Formaciones rapidas)



        '<div class="ftools">' +



        '  <div class="ftools-row" style="justify-content:space-between">' +



        '    <div class="tool-item">' +



        '      <span class="lab" style="font-size:12px; font-weight:700; color:var(--muted)">Césped:</span>' +



        '      <select class="input small" id="tPitchTheme" style="width:160px">' +



        '        <option value="clasico"' + (pitchTheme === "clasico" ? " selected" : "") + '>🌱 Clásico</option>' +



        '        <option value="noche"' + (pitchTheme === "noche" ? " selected" : "") + '>💡 Nocturno (Focos)</option>' +



        '        <option value="atardecer"' + (pitchTheme === "atardecer" ? " selected" : "") + '>🌅 Atardecer</option>' +



        '        <option value="pizarra"' + (pitchTheme === "pizarra" ? " selected" : "") + '>📋 Pizarra táctica</option>' +



        '        <option value="retro"' + (pitchTheme === "retro" ? " selected" : "") + '>📜 Retro sepia</option>' +



        '        <option value="equipo"' + (pitchTheme === "equipo" ? " selected" : "") + '>🛡️ Colores del club</option>' +



        '      </select>' +



        '    </div>' +



        '    <div class="tool-item">' +



        '      <span class="lab" style="font-size:12px; font-weight:700; color:var(--muted)">Ver jugadores:</span>' +



        '      <div class="seg mini" id="tLookSeg">' +



        '        <button type="button" class="btn sm' + (playerLook === "kits" ? " active" : "") + '" id="tLookKits">🎽 Camisetas</button>' +



        '        <button type="button" class="btn sm' + (playerLook === "photos" ? " active" : "") + '" id="tLookPhotos">👤 Fotos</button>' +



        '      </div>' +



        '    </div>' +



        '    <div class="tool-item">' +



        '      <label class="chk" style="font-size:12px; cursor:pointer"><input type="checkbox" id="tShowIds"' + (showPlayerIds ? " checked" : "") + '> Mostrar IDs</label>' +



        '    </div>' +



        '  </div>' +



        '  <div class="preset-row">' +



        '    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:8px; flex-wrap:wrap; gap:6px">' +



        '      <span class="lab" style="font-size:12px; font-weight:800; color:var(--text); text-transform:uppercase; letter-spacing:.4px">📐 Esquemas Tácticos (22):</span>' +



        '      <div class="preset-filter-chips">' +



        '        <button type="button" class="preset-filter-chip active" data-cat="all">Todas (22)</button>' +



        '        <button type="button" class="preset-filter-chip" data-cat="4">Línea de 4 (11)</button>' +



        '        <button type="button" class="preset-filter-chip" data-cat="3">Línea de 3 (6)</button>' +



        '        <button type="button" class="preset-filter-chip" data-cat="5">Línea de 5 (5)</button>' +



        '      </div>' +



        '    </div>' +



        '    <div class="preset-btns" id="tPresetBtns">' +



        FORMATION_CATALOG.map(function(fc) {



          var isOn = (curFormName.indexOf(fc.name) >= 0 || curFormName === fc.name);



          return '<button type="button" class="btn small' + (isOn ? ' on' : '') + '" data-preset="' + fc.name + '" data-cat="' + fc.cat + '">' + fc.name + '</button>';



        }).join("") +



        '    </div>' +



        '  </div>' +



        '</div>' +







        // 3. Cancha interactiva + Panel lateral (Detalle de seleccionado / Banco)



        '<div class="formwrap">' +



        '  <div class="fpitch-container">' +



        '    <div class="fpitch" id="tFpitch" style="background-image:url(' + pitchBgUrl + ')">' +



        (d.eleven || []).filter(function (f) { return f.squadIndex < 11; }).map(function (f) {



          var leftPct = Math.max(5, Math.min(95, f.x));



          var bottomPct = Math.max(3, Math.min(93, 3 + (f.y / 50) * 94));



          var isGk = f.squadIndex === 0 || f.role === 0 || f.roleName === "GK";



          var line = A.posLine ? A.posLine(f.roleName) : (isGk ? "gk" : "df");



          var kitC1 = isGk ? "#E5A910" : (d.color1 || "#2F6DB3");



          var kitC2 = isGk ? "#111111" : (d.color2 || "#ffffff");



          var rp = rosterByPid.get(f.playerId);



          var ovr = rp ? rp.ovr : 70;



          var ovrCls = ovr >= 90 ? "v90" : ovr >= 80 ? "v80" : ovr >= 70 ? "v70" : "v60";



          var shortName = (f.playerName || "").split(" ").slice(-1)[0] || "—";



          var isSel = selectedStarterFi === f.squadIndex;



          var photoUrl = (playerLook === "photos" && playerFaceCache.get(f.playerId)) ? playerFaceCache.get(f.playerId) : null;







          return '<button type="button" class="fm ln-' + line + (isGk ? " gk" : "") + (isSel ? " on" : "") + (photoUrl ? " ph" : "") + '" data-fi="' + f.squadIndex + '" data-pid="' + f.playerId + '" style="left:' + leftPct + '%; bottom:' + bottomPct + '%" title="' + esc(f.playerName) + ' · ' + A.posLabel(f.roleName) + (showPlayerIds ? ' · ID ' + f.playerId : '') + '">' +



            '<span class="fav" data-face-pid="' + f.playerId + '">' +



              (photoUrl ? '<img src="' + photoUrl + '" class="fphoto">' : ('<span class="shirtwrap">' + (A.shirtSvg ? A.shirtSvg({ c1: kitC1, c2: kitC2 }) : '') + '</span>')) +



              '<span class="fnum">' + (f.shirt || (f.squadIndex + 1)) + '</span>' +



            '</span>' +



            '<span class="fname">' + esc(shortName) + '</span>' +



            '<span class="fmeta">' + A.posBadge(f.roleName) + '<span class="frt ' + ovrCls + '">' + ovr + '</span>' + (showPlayerIds ? '<span class="fid">' + f.playerId + '</span>' : '') + '</span>' +



          '</button>';



        }).join("") +



        '      <span class="fdir">ATAQUE ↑</span>' +



        '    </div>' +



        '  </div>' +



        '  <div class="fside">' +



        '    <div id="tPitchSelCard">' +



        '      <div class="selcard">' +



        '        <div class="selcard-info">' +



        '          <div class="selcard-name">🛡️ Cancha interactiva</div>' +



        '          <div class="selcard-sub">Arrastrá cualquier futbolista con el mouse para ajustar su posición. Soltalo encima de otro titular para intercambiarlos. Tocalo para cambiar su rol o sustituirlo.</div>' +



        '        </div>' +



        '      </div>' +



        '    </div>' +



        '    <div class="card-sec" style="margin-top:0">' +



        '      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:4px">' +



        '        <h4 style="margin:0">Banco de suplentes (' + benchPlayers.length + ')</h4>' +



        '        <span class="hint" style="font-size:.74rem">Arrastrá a la cancha o tocá</span>' +



        '      </div>' +



        '      <div class="bench-grid" id="tBenchGrid">' +



        benchPlayers.map(function (b) {



          return '<button type="button" class="bn" data-bpid="' + b.playerId + '">' +



            '<span class="bnn">' + b.shirt + '</span>' +



            '<span class="bnm">' + esc(b.name) + '</span>' +



            A.posBadge(b.pos) +



            '<span class="frt small" style="margin-left:auto">' + b.ovr + '</span>' +



          '</button>';



        }).join("") +



        '      </div>' +



        '    </div>' +



        '  </div>' +



        '</div>' +







        // 4. Panel de Estrategia y Estilo de Juego (como en el repo)



        '<div class="card-sec" style="margin-top:16px">' +



        '  <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:10px; margin-bottom:12px">' +



        '    <div>' +



        '      <h4 style="margin:0; font-size:1.05rem">⚙️ Estrategia y Estilo de Juego</h4>' +



        '      <span class="hint">Comportamiento del equipo en posesión, repliegue y presión.</span>' +



        '    </div>' +



        '    <button class="btn btn-primary sm" id="tSaveStrategy">💾 Guardar estrategia</button>' +



        '  </div>' +



        '  <div style="margin-bottom:12px">' +



        '    <label style="font-size:12px; font-weight:700; color:var(--muted); text-transform:uppercase; letter-spacing:.5px">Tácticas Rápidas (1 toque):</label>' +



        '    <div class="btn-row" style="margin-top:6px; flex-wrap:wrap">' +



        '      <button type="button" class="btn btn-ghost sm" data-quick-strat="possession">⚽ Posesión de balón</button>' +



        '      <button type="button" class="btn btn-ghost sm" data-quick-strat="counter">⚡ Contraataques rápidos</button>' +



        '      <button type="button" class="btn btn-ghost sm" data-quick-strat="bus">🛡️ Defensa total</button>' +



        '      <button type="button" class="btn btn-ghost sm" data-quick-strat="press">🔥 Presión asfixiante</button>' +



        '    </div>' +



        '  </div>' +



        '  <div class="form-grid-3">' +



        '    <div class="strat-column">' +



        '      <div class="strat-col-title">⚔️ Fase Ofensiva</div>' +



        '      <div class="field"><label>Estilo de ataque</label><select class="input" id="stAttackStyle"><option value="0"' + (stData.attackStyle === 0 ? ' selected' : '') + '>Contraataque (Vertical)</option><option value="1"' + (stData.attackStyle === 1 ? ' selected' : '') + '>Juego de posesión (Control)</option></select></div>' +



        '      <div class="field"><label>Creación</label><select class="input" id="stBuildUp"><option value="0"' + (stData.buildUp === 0 ? ' selected' : '') + '>Pase largo (Directo)</option><option value="1"' + (stData.buildUp === 1 ? ' selected' : '') + '>Pase corto (Asociativo)</option></select></div>' +



        '      <div class="field"><label>Zona de ataque</label><select class="input" id="stAttackArea"><option value="0"' + (stData.attackArea === 0 ? ' selected' : '') + '>Por las bandas</option><option value="1"' + (stData.attackArea === 1 ? ' selected' : '') + '>Por el centro</option></select></div>' +



        '      <div class="field"><label>Posicionamiento</label><select class="input" id="stPositioning"><option value="0"' + (stData.positioning === 0 ? ' selected' : '') + '>Mantener formación (Estricto)</option><option value="1"' + (stData.positioning === 1 ? ' selected' : '') + '>Flexible (Libertad)</option></select></div>' +



        '      <div class="field"><label>Distancia entre líneas: <b id="valSupportRange">' + stData.supportRange + '</b>/10</label><input type="range" class="range-slider" id="stSupportRange" min="1" max="10" value="' + stData.supportRange + '"></div>' +



        '    </div>' +



        '    <div class="strat-column">' +



        '      <div class="strat-col-title">🛡️ Fase Defensiva</div>' +



        '      <div class="field"><label>Estilo defensivo</label><select class="input" id="stDefensiveStyle"><option value="0"' + (stData.defensiveStyle === 0 ? ' selected' : '') + '>Presión en la frontal</option><option value="1"' + (stData.defensiveStyle === 1 ? ' selected' : '') + '>Defensa total (Bloque bajo)</option></select></div>' +



        '      <div class="field"><label>Zona de contención</label><select class="input" id="stContainmentArea"><option value="0"' + (stData.containmentArea === 0 ? ' selected' : '') + '>Por el centro</option><option value="1"' + (stData.containmentArea === 1 ? ' selected' : '') + '>Por las bandas</option></select></div>' +



        '      <div class="field"><label>Presión</label><select class="input" id="stPressuring"><option value="0"' + (stData.pressuring === 0 ? ' selected' : '') + '>Agresivo (Anticipación)</option><option value="1"' + (stData.pressuring === 1 ? ' selected' : '') + '>Conservador (Cerrar líneas)</option></select></div>' +



        '      <div class="field"><label>Línea defensiva (Altura): <b id="valDefLine">' + stData.defensiveLine + '</b>/10</label><input type="range" class="range-slider" id="stDefLine" min="1" max="10" value="' + stData.defensiveLine + '"></div>' +



        '      <div class="field"><label>Densidad defensiva: <b id="valCompactness">' + stData.compactness + '</b>/10</label><input type="range" class="range-slider" id="stCompactness" min="1" max="10" value="' + stData.compactness + '"></div>' +



        '    </div>' +



        '    <div class="strat-column" style="grid-column: 1 / -1; margin-top: 6px">' +



        '      <div class="strat-col-title">⚔️ Instrucciones Avanzadas (PES 2021 Oficial)</div>' +



        '      <div class="adv-section-title"><span>Instrucciones de Ataque (Máximo 2)</span><span class="adv-count" id="advAttCount">0/2</span></div>' +



        '      <div class="advgrid" id="advGridAtt">' +



        ADV_INSTRUCTIONS.att.map(function(item) {



          var badgeText = "";



          var isOn = false;



          if (curAdvAtt1 === item.code) { isOn = true; badgeText = "Ataque 1"; }



          else if (curAdvAtt2 === item.code) { isOn = true; badgeText = "Ataque 2"; }



          return '<div class="advt' + (isOn ? ' on' : '') + '" data-adv-kind="att" data-adv-code="' + item.code + '">' +



            '<div class="advt-top"><span class="advn">' + esc(item.name) + '</span><span class="advbadge" style="' + (badgeText ? '' : 'display:none') + '">' + badgeText + '</span></div>' +



            '<span class="advd">' + esc(item.desc) + '</span>' +



          '</div>';



        }).join("") +



        '      </div>' +



        '      <div class="adv-section-title"><span>Instrucciones de Defensa (Máximo 2)</span><span class="adv-count def" id="advDefCount">0/2</span></div>' +



        '      <div class="advgrid" id="advGridDef">' +



        ADV_INSTRUCTIONS.def.map(function(item) {



          var badgeText = "";



          var isOn = false;



          if (curAdvDef1 === item.code) { isOn = true; badgeText = "Defensa 1"; }



          else if (curAdvDef2 === item.code) { isOn = true; badgeText = "Defensa 2"; }



          return '<div class="advt' + (isOn ? ' on-def' : '') + '" data-adv-kind="def" data-adv-code="' + item.code + '">' +



            '<div class="advt-top"><span class="advn">' + esc(item.name) + '</span><span class="advbadge def" style="' + (badgeText ? '' : 'display:none') + '">' + badgeText + '</span></div>' +



            '<span class="advd">' + esc(item.desc) + '</span>' +



          '</div>';



        }).join("") +



        '      </div>' +



        '    </div>' +



        '  </div>' +



        '</div>' +







        // 5. Panel de Capitan y Balon Parado



        '<div class="card-sec" style="margin-top:16px">' +



        '  <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:10px">' +



        '    <div>' +



        '      <h4 style="margin:0; font-size:1.05rem">🎯 Capitán y Balón Parado</h4>' +



        '      <span class="hint">Ejecutores oficiales de faltas, córners, penales y capitanía.</span>' +



        '    </div>' +



        '    <div style="display:flex; gap:8px">' +



        '      <button class="btn btn-ghost sm" id="tAutoSetPieces">⚡ Auto-asignar por habilidades</button>' +



        '      <button class="btn btn-primary sm" id="tSaveSetPieces">💾 Guardar lanzadores</button>' +



        '    </div>' +



        '  </div>' +



        '  <div class="form-grid">' +



        '    <div class="field"><label>👑 Capitán del equipo</label><select class="input" id="spCaptain">' + spOptions(curCaptain) + '</select></div>' +



        '    <div class="field"><label>🎯 Penales</label><select class="input" id="spPenalty">' + spOptions(curPenalty) + '</select></div>' +



        '    <div class="field"><label>⚡ Tiro libre corto</label><select class="input" id="spShortFk">' + spOptions(curShortFk) + '</select></div>' +



        '    <div class="field"><label>🚀 Tiro libre largo</label><select class="input" id="spLongFk">' + spOptions(curLongFk) + '</select></div>' +



        '    <div class="field"><label>🚩 Córner izquierdo</label><select class="input" id="spLeftCorner">' + spOptions(curLeftCorner) + '</select></div>' +



        '    <div class="field"><label>🚩 Córner derecho</label><select class="input" id="spRightCorner">' + spOptions(curRightCorner) + '</select></div>' +



        '  </div>' +



        '</div>' +







        '</div>' + 



        // Tab 3: Ficha tecnica



        '<div class="ttab-pane" id="tPane-ficha" style="' + (activeTeamTab === "ficha" ? "" : "display:none") + '">' +



        '<div class="card-sec"><h3>Ficha técnica</h3><div class="form-grid">' +



        tf("Nombre español", "eNameSp", d.nameSpanish) + tf("Nombre internacional", "eNameEn", d.nameEnglish) +



        tf("Sigla", "eShort", d.shortName) +



        '<div class="field"><label>Nombre DB interno</label><input class="input" value="' + esc(d.nameDatabase) + '" disabled></div>' +



        '<div class="field"><label>País / Nacionalidad</label><select class="input" id="eCountry">' + (d.countries || []).map(function (c) { return '<option value="' + c.id + '"' + (c.id === d.countryId ? ' selected' : '') + '>' + esc(c.name) + '</option>'; }).join("") + "</select></div>" +



        '<div class="field"><label>Estadio</label><select class="input" id="eStadium">' + (d.stadiums || []).map(function (s) { return '<option value="' + s.id + '"' + (s.id === d.stadiumId ? " selected" : "") + ">" + esc(s.name) + "</option>"; }).join("") + "</select></div>" +



        '<div class="field"><label>Director técnico</label><select class="input" id="eCoach">' + (d.coaches || []).map(function (c) { return '<option value="' + c.id + '"' + (c.id === d.coachId ? " selected" : "") + ">" + esc(c.name) + "</option>"; }).join("") + "</select></div>" +



        '<div class="field"><label>🎙️ Callname / Audio (ID)</label><input class="input" type="number" id="eCallname" min="0" max="65535" value="' + (d.callnameId || 0) + '" placeholder="0 = Sin audio"><span class="hint">ID de audio para relatores oficiales</span></div>' +



        '<div class="field"><label>⚔️ Rivales y Clásicos</label><div class="mut small" style="padding:9px 12px; background:var(--input); border-radius:8px; border:1px solid var(--border); display:flex; align-items:center; justify-content:space-between">' +
        '  <span>' + (teamRivals.filter(function (r) { return r.id > 0; }).map(function (r) { return '<b>' + esc(r.name) + '</b> (ID ' + r.id + ')'; }).join(" · ") || "Sin rivales configurados") + '</span>' +
        '  <button type="button" class="btn btn-ghost xs" id="tGoToIdentity">Gestionar en Identidad ➔</button>' +
        '</div></div>' +
        '</div><label class="check-line"><input type="checkbox" id="eNational"' + (d.isNational ? " checked" : "") + "> Es selección nacional</label>" +
        '<div class="btn-row" style="margin-top:10px"><button class="btn btn-primary" id="tSaveFicha">💾 Guardar ficha</button></div></div></div>' +

        // Tab 4: Identidad visual, colores y rivales
        '<div class="ttab-pane" id="tPane-identidad" style="' + (activeTeamTab === "identidad" ? "" : "display:none") + '">' +

        // Card 1: Colores del Radar
        '<div class="card-sec"><h3>🎨 Colores oficiales de radar y uniforme <span class="sub">Bloque BANC · Team.bin</span></h3>' +
        '<p class="mut small" style="margin-bottom:14px">Definen los puntos del mini-mapa en pantalla y las tonalidades representativas del club en PES 2021.</p>' +
        '<div style="display:flex; gap:24px; align-items:center; flex-wrap:wrap">' +
        '  <div class="form-grid" style="flex:1; min-width:280px">' +
        '    <div class="field"><label>Color radar 1 (Local)</label><div class="color-line"><input type="color" class="input" id="eColor1" value="' + esc(d.color1 || "#FF0000") + '"><input type="text" class="input small mono" id="eColor1Hex" value="' + esc(d.color1 || "#FF0000") + '" style="width:90px" maxlength="7"></div></div>' +
        '    <div class="field"><label>Color radar 2 (Visitante)</label><div class="color-line"><input type="color" class="input" id="eColor2" value="' + esc(d.color2 || "#0000FF") + '"><input type="text" class="input small mono" id="eColor2Hex" value="' + esc(d.color2 || "#0000FF") + '" style="width:90px" maxlength="7"></div></div>' +
        '  </div>' +
        '  <div class="radar-preview-box">' +
        '    <div class="radar-circle">' +
        '      <div class="radar-half" id="tRadarHalf1" style="background:' + esc(d.color1 || "#FF0000") + '"></div>' +
        '      <div class="radar-half" id="tRadarHalf2" style="background:' + esc(d.color2 || "#0000FF") + '"></div>' +
        '    </div>' +
        '    <div class="radar-preview-label">Vista previa Radar PES</div>' +
        '  </div>' +
        '</div></div>' +

        // Card 2: Pancartas de Tribuna
        '<div class="card-sec" style="margin-top:14px"><h3>🚩 Banderas de tribuna <span class="sub">4 banderas de la hinchada · Máx 15 caracteres c/u</span></h3>' +
        '<p class="mut small" style="margin-bottom:12px">Textos que cuelgan de las barandas del estadio en los fondos de la tribuna. Usá mayúsculas.</p>' +
        '<div class="banner-preview-strip" id="tBannerPreviewStrip"></div>' +
        '<div style="display:grid; grid-template-columns:repeat(auto-fit, minmax(210px, 1fr)); gap:10px; margin-top:12px">' +
        [0, 1, 2, 3].map(function (i) {
          var bv = (d.banners || [])[i] || "";
          return '<div class="field"><label>Bandera ' + (i + 1) + '</label>' +
            '<input class="input banner-inp" maxlength="15" id="eBanner' + i + '" value="' + esc(bv) + '" placeholder="Bandera ' + (i + 1) + '">' +
            '<span class="hint banner-cnt" id="cntBanner' + i + '">' + bv.length + '/15 letras</span></div>';
        }).join("") +
        '</div></div>' +

        // Card 3: Rivales y Clásicos Oficiales
        '<div class="card-sec" style="margin-top:14px"><h3>⚔️ Rivales y Clásicos oficiales <span class="sub">3 rivales oficiales · Team.bin</span></h3>' +
        '<p class="mut small" style="margin-bottom:14px">Configuran los clásicos y derbis en Liga Master y amistosos. Aumentan la intensidad táctica, roces, faltas y ambiente en tribunas.</p>' +
        '<div class="rivals-grid" id="tRivalsGrid"></div>' +
        '</div>' +

        // Guardar identidad
        '<div class="btn-row" style="margin-top:16px">' +
        '<button class="btn btn-save" id="tSaveIdentity" style="padding:12px 24px; font-size:15px; font-weight:700">💾 Guardar identidad y rivales</button>' +
        '</div></div>';

      function tf(label, id, val) { return '<div class="field"><label>' + label + '</label><input class="input" id="' + id + '" value="' + esc(val || "") + '"></div>'; }







      // Eventos sub-pestañas



      A.$$("[data-ttab]", box).forEach(function (btn) {



        btn.addEventListener("click", function () {



          var tabKey = btn.getAttribute("data-ttab");



          activeTeamTab = tabKey;



          A.$$("[data-ttab]", box).forEach(function (b) { b.classList.toggle("on", b === btn); });



          ["plantilla", "tacticas", "ficha", "identidad"].forEach(function (k) {



            var pane = box.querySelector("#tPane-" + k);



            if (pane) pane.style.display = (k === tabKey ? "" : "none");



          });



          if (tabKey === "tacticas") {



            var pEl = box.querySelector("#tFpitch");



            if (pEl && A.pitchTexture) pEl.style.backgroundImage = "url(" + A.pitchTexture(pitchTheme, d.color1, d.color2) + ")";



          }



        });



      });







      // escudo real



      rpc("teamEmblem", { id: d.id }).then(function (r) {



        var box = page.querySelector("#tEmblem");



        if (r.png && box) box.innerHTML = '<img src="data:image/png;base64,' + r.png + '" style="width:100%;height:100%;object-fit:contain">';



      }).catch(function () { });







      function initTacticsPitch() {



        var pitchEl = page.querySelector("#tFpitch");



        if (!pitchEl) return;



        var activeDrag = null;







        // Carga asincrona de minifaces cuando el modo fotos esta activo



        if (playerLook === "photos") {



          (d.eleven || []).forEach(function (f) {



            if (!playerFaceCache.has(f.playerId)) {



              rpc("playerMiniface", { id: f.playerId, pos: f.roleName, name: f.playerName }).then(function (r) {



                var url = r.png ? ("data:image/png;base64," + r.png) : null;



                playerFaceCache.set(f.playerId, url);



                if (url) {



                  var holder = pitchEl.querySelector('.fav[data-face-pid="' + f.playerId + '"]');



                  if (holder) holder.innerHTML = '<img src="' + url + '" class="fphoto"><span class="fnum">' + (f.shirt || (f.squadIndex + 1)) + '</span>';



                }



              }).catch(function () { playerFaceCache.set(f.playerId, null); });



            }



          });



        }







        // Selector de Cesped



        var thSel = page.querySelector("#tPitchTheme");



        if (thSel) {



          thSel.addEventListener("change", function () {



            pitchTheme = thSel.value;



            localStorage.setItem("pes_pitch_theme", pitchTheme);



            var newBg = A.pitchTexture ? A.pitchTexture(pitchTheme, d.color1, d.color2) : "";



            pitchEl.style.backgroundImage = "url(" + newBg + ")";



          });



        }







        // Selector de vista: Camisetas vs Fotos



        var lookKits = page.querySelector("#tLookKits");



        var lookPhotos = page.querySelector("#tLookPhotos");



        if (lookKits && lookPhotos) {



          lookKits.addEventListener("click", function () {



            playerLook = "kits";



            localStorage.setItem("pes_tactics_look", "kits");



            renderDetail();



          });



          lookPhotos.addEventListener("click", function () {



            playerLook = "photos";



            localStorage.setItem("pes_tactics_look", "photos");



            renderDetail();



          });



        }







        // Checkbox Mostrar IDs



        var idsChk = page.querySelector("#tShowIds");



        if (idsChk) {



          idsChk.addEventListener("change", function () {



            showPlayerIds = idsChk.checked;



            localStorage.setItem("pes_tactics_ids", showPlayerIds ? "1" : "0");



            renderDetail();



          });



        }







        // Chips de filtro de formación



        A.$$(".preset-filter-chips [data-cat]", page).forEach(function(chip) {



          chip.addEventListener("click", function() {



            var cat = chip.getAttribute("data-cat");



            A.$$(".preset-filter-chips [data-cat]", page).forEach(function(c) { c.classList.toggle("active", c === chip); });



            A.$$(".preset-btns [data-preset]", page).forEach(function(btn) {



              var btnCat = btn.getAttribute("data-cat");



              btn.style.display = (cat === "all" || btnCat === cat) ? "" : "none";



            });



          });



        });







        // Botones rapidos de plantillas (cambio fluido e instantáneo en la cancha, sin cartel molesto)



        A.$$(".preset-btns [data-preset]", page).forEach(function (btn) {



          btn.addEventListener("click", function () {



            var pr = btn.getAttribute("data-preset");



            A.$$(".preset-btns [data-preset]", page).forEach(function(b) { b.classList.toggle("on", b === btn); });



            rpc("applyFormation", { teamId: d.id, preset: pr }).then(function (res) {



              if (!res || !res.eleven) return;



              d.formationName = res.formationName || pr;



              d.eleven = res.eleven;



              var fb = page.querySelector(".fbanner .fbig");



              if (fb) fb.textContent = d.formationName;



              (res.eleven || []).forEach(function (f) {



                var elToken = page.querySelector('.fm[data-fi="' + f.squadIndex + '"]');



                if (elToken) {



                  var leftPct = Math.max(5, Math.min(95, f.x));



                  var bottomPct = Math.max(3, Math.min(93, 3 + (f.y / 50) * 94));



                  elToken.style.left = leftPct + "%";



                  elToken.style.bottom = bottomPct + "%";



                  var fpos = elToken.querySelector(".fmeta");



                  if (fpos) {



                    var rp = rosterByPid.get(f.playerId);



                    var ovr = rp ? rp.ovr : 70;



                    var ovrCls = ovr >= 90 ? "v90" : ovr >= 80 ? "v80" : ovr >= 70 ? "v70" : "v60";



                    fpos.innerHTML = A.posBadge(f.roleName) + '<span class="frt ' + ovrCls + '">' + ovr + '</span>' + (showPlayerIds ? '<span class="fid">' + f.playerId + '</span>' : '');



                  }



                }



              });



              if (selectedStarterFi != null) renderSelectedCard(selectedStarterFi);



            }).catch(function (e) { toast(e.message, true); });



          });



        });







        // Manejo interactivo de Instrucciones Avanzadas (máx 2 ataque, máx 2 defensa)



        function updateAdvCounts() {



          var attCount = (curAdvAtt1 ? 1 : 0) + (curAdvAtt2 ? 1 : 0);



          var defCount = (curAdvDef1 ? 1 : 0) + (curAdvDef2 ? 1 : 0);



          var acEl = page.querySelector("#advAttCount");



          if (acEl) acEl.textContent = attCount + "/2 activas";



          var dcEl = page.querySelector("#advDefCount");



          if (dcEl) dcEl.textContent = defCount + "/2 activas";



        }



        updateAdvCounts();







        A.$$("[data-adv-kind]", page).forEach(function(card) {



          card.addEventListener("click", function() {



            var kind = card.getAttribute("data-adv-kind");



            var code = Number(card.getAttribute("data-adv-code")) || 0;



            if (kind === "att") {



              if (curAdvAtt1 === code) { curAdvAtt1 = 0; }



              else if (curAdvAtt2 === code) { curAdvAtt2 = 0; }



              else {



                if (!curAdvAtt1) curAdvAtt1 = code;



                else if (!curAdvAtt2) curAdvAtt2 = code;



                else { curAdvAtt1 = curAdvAtt2; curAdvAtt2 = code; }



              }



              A.$$('[data-adv-kind="att"]', page).forEach(function(c) {



                var cCode = Number(c.getAttribute("data-adv-code"));



                var badge = c.querySelector(".advbadge");



                if (cCode === curAdvAtt1) {



                  c.classList.add("on");



                  if (badge) { badge.textContent = "Ataque 1"; badge.style.display = ""; }



                } else if (cCode === curAdvAtt2) {



                  c.classList.add("on");



                  if (badge) { badge.textContent = "Ataque 2"; badge.style.display = ""; }



                } else {



                  c.classList.remove("on");



                  if (badge) badge.style.display = "none";



                }



              });



            } else if (kind === "def") {



              if (curAdvDef1 === code) { curAdvDef1 = 0; }



              else if (curAdvDef2 === code) { curAdvDef2 = 0; }



              else {



                if (!curAdvDef1) curAdvDef1 = code;



                else if (!curAdvDef2) curAdvDef2 = code;



                else { curAdvDef1 = curAdvDef2; curAdvDef2 = code; }



              }



              A.$$('[data-adv-kind="def"]', page).forEach(function(c) {



                var cCode = Number(c.getAttribute("data-adv-code"));



                var badge = c.querySelector(".advbadge");



                if (cCode === curAdvDef1) {



                  c.classList.add("on-def");



                  if (badge) { badge.textContent = "Defensa 1"; badge.style.display = ""; }



                } else if (cCode === curAdvDef2) {



                  c.classList.add("on-def");



                  if (badge) { badge.textContent = "Defensa 2"; badge.style.display = ""; }



                } else {



                  c.classList.remove("on-def");



                  if (badge) badge.style.display = "none";



                }



              });



            }



            updateAdvCounts();



          });



        });







        // Sliders de rango en estrategia



        function bindSlider(sliderId, valId) {



          var s = page.querySelector(sliderId), v = page.querySelector(valId);



          if (s && v) {



            s.addEventListener("input", function () { v.textContent = s.value; });



          }



        }



        bindSlider("#stSupportRange", "#valSupportRange");



        bindSlider("#stDefLine", "#valDefLine");



        bindSlider("#stCompactness", "#valCompactness");







        // Tacticas rapidas de 1 toque



        A.$$("[data-quick-strat]", page).forEach(function (b) {



          b.addEventListener("click", function () {



            var kind = b.getAttribute("data-quick-strat");



            var patch = {};



            if (kind === "possession") {



              patch = { attackStyle: 1, buildUp: 1, attackArea: 1, positioning: 1, defensiveStyle: 0, containmentArea: 0, pressuring: 0, defLine: 8, compactness: 6, supportRange: 3 };



            } else if (kind === "counter") {



              patch = { attackStyle: 0, buildUp: 1, attackArea: 0, positioning: 1, defensiveStyle: 0, containmentArea: 1, pressuring: 0, defLine: 5, compactness: 7, supportRange: 6 };



            } else if (kind === "bus") {



              patch = { attackStyle: 0, buildUp: 0, attackArea: 0, positioning: 0, defensiveStyle: 1, containmentArea: 0, pressuring: 1, defLine: 2, compactness: 10, supportRange: 8 };



            } else if (kind === "press") {



              patch = { attackStyle: 1, buildUp: 1, attackArea: 1, positioning: 1, defensiveStyle: 0, containmentArea: 0, pressuring: 0, defLine: 10, compactness: 8, supportRange: 4 };



            }







            if (page.querySelector("#stAttackStyle")) page.querySelector("#stAttackStyle").value = patch.attackStyle;



            if (page.querySelector("#stBuildUp")) page.querySelector("#stBuildUp").value = patch.buildUp;



            if (page.querySelector("#stAttackArea")) page.querySelector("#stAttackArea").value = patch.attackArea;



            if (page.querySelector("#stPositioning")) page.querySelector("#stPositioning").value = patch.positioning;



            if (page.querySelector("#stDefensiveStyle")) page.querySelector("#stDefensiveStyle").value = patch.defensiveStyle;



            if (page.querySelector("#stContainmentArea")) page.querySelector("#stContainmentArea").value = patch.containmentArea;



            if (page.querySelector("#stPressuring")) page.querySelector("#stPressuring").value = patch.pressuring;



            if (page.querySelector("#stDefLine")) { page.querySelector("#stDefLine").value = patch.defLine; page.querySelector("#valDefLine").textContent = patch.defLine; }



            if (page.querySelector("#stCompactness")) { page.querySelector("#stCompactness").value = patch.compactness; page.querySelector("#valCompactness").textContent = patch.compactness; }



            if (page.querySelector("#stSupportRange")) { page.querySelector("#stSupportRange").value = patch.supportRange; page.querySelector("#valSupportRange").textContent = patch.supportRange; }







            rpc("setTeamStrategy", {



              teamId: d.id,



              attackStyle: patch.attackStyle,



              buildUp: patch.buildUp,



              attackArea: patch.attackArea,



              positioning: patch.positioning,



              defensiveStyle: patch.defensiveStyle,



              containmentArea: patch.containmentArea,



              pressuring: patch.pressuring,



              defensiveLine: patch.defLine,



              compactness: patch.compactness,



              supportRange: patch.supportRange



            }).then(function () {



              toast("Táctica rápida aplicada.");



            }).catch(function (e) { toast(e.message, true); });



          });



        });







        // Guardar estrategia manual e instrucciones avanzadas



        var saveStratBtn = page.querySelector("#tSaveStrategy");



        if (saveStratBtn) {



          saveStratBtn.addEventListener("click", function () {



            rpc("setTeamStrategy", {



              teamId: d.id,



              attackStyle: Number(page.querySelector("#stAttackStyle").value) || 0,



              buildUp: Number(page.querySelector("#stBuildUp").value) || 0,



              attackArea: Number(page.querySelector("#stAttackArea").value) || 0,



              positioning: Number(page.querySelector("#stPositioning").value) || 0,



              defensiveStyle: Number(page.querySelector("#stDefensiveStyle").value) || 0,



              containmentArea: Number(page.querySelector("#stContainmentArea").value) || 0,



              pressuring: Number(page.querySelector("#stPressuring").value) || 0,



              defensiveLine: Number(page.querySelector("#stDefLine").value) || 5,



              compactness: Number(page.querySelector("#stCompactness").value) || 5,



              supportRange: Number(page.querySelector("#stSupportRange").value) || 5,



              advAtt1: curAdvAtt1,



              advAtt2: curAdvAtt2,



              advDef1: curAdvDef1,



              advDef2: curAdvDef2



            }).then(function () {



              toast("Estrategia e instrucciones guardadas.");



            }).catch(function (e) { toast(e.message, true); });



          });



        }







        // Render card del jugador seleccionado



        function renderSelectedCard(idx) {



          var scBox = page.querySelector("#tPitchSelCard");



          if (!scBox) return;



          if (idx == null) {



            scBox.innerHTML =



              '<div class="selcard">' +



              '  <div class="selcard-info">' +



              '    <div class="selcard-name">🛡️ Cancha interactiva</div>' +



              '    <div class="selcard-sub">Arrastrá cualquier futbolista con el mouse para ajustar su posición. Soltalo encima de otro titular para intercambiarlos. Tocalo para cambiar su rol o sustituirlo.</div>' +



              '  </div>' +



              '</div>';



            return;



          }







          var f = (d.eleven || [])[idx];



          if (!f) return;



          var rp = rosterByPid.get(f.playerId);



          var ovr = rp ? rp.ovr : 70;



          var isGk = f.squadIndex === 0 || f.role === 0 || f.roleName === "GK";



          var kitC1 = isGk ? "#E5A910" : (d.color1 || "#2F6DB3");



          var kitC2 = isGk ? "#111111" : (d.color2 || "#ffffff");



          var photoUrl = (playerLook === "photos" && playerFaceCache.get(f.playerId)) ? playerFaceCache.get(f.playerId) : null;







          var roleOptions = [



            { id: 0, name: "GK", label: "POR · Portero" },



            { id: 1, name: "CB", label: "DFC · Central" },



            { id: 2, name: "LB", label: "LI · Lateral izquierdo" },



            { id: 3, name: "RB", label: "LD · Lateral derecho" },



            { id: 4, name: "DMF", label: "MCD · Medio centro defensivo" },



            { id: 5, name: "CMF", label: "MC · Mediocentro" },



            { id: 6, name: "LMF", label: "II · Interior izquierdo" },



            { id: 7, name: "RMF", label: "ID · Interior derecho" },



            { id: 8, name: "AMF", label: "MP · Mediapunta" },



            { id: 9, name: "LWF", label: "EI · Extremo izquierdo" },



            { id: 10, name: "RWF", label: "ED · Extremo derecho" },



            { id: 11, name: "SS", label: "SD · Segundo delantero" },



            { id: 12, name: "CF", label: "DC · Delantero centro" }



          ];







          scBox.innerHTML =



            '<div class="selcard" style="align-items:flex-start">' +



            '  <div class="fav big" style="width:58px; height:58px">' +



                 (photoUrl ? '<img src="' + photoUrl + '" class="fphoto" style="width:52px; height:52px">' : ('<span class="shirtwrap">' + (A.shirtSvg ? A.shirtSvg({ c1: kitC1, c2: kitC2 }) : '') + '</span>')) +



            '  </div>' +



            '  <div class="selcard-info" style="flex:1">' +



            '    <div style="display:flex; justify-content:space-between; align-items:center">' +



            '      <div class="selcard-name">' + esc(f.playerName) + ' <span class="mut" style="font-size:.82rem">#' + (f.shirt || (f.squadIndex + 1)) + '</span></div>' +



            '      <span class="frt v80">' + ovr + '</span>' +



            '    </div>' +



            '    <div class="selcard-sub" style="margin-bottom:8px">' + A.posLabel(f.roleName) + ' · ID ' + f.playerId + '</div>' +



            '    <div class="form-grid" style="gap:8px">' +



            '      <div class="field" style="margin:0"><label style="font-size:.78rem">Rol táctico</label><select class="input small" id="tSelRole">' +



                     roleOptions.map(function (ro) {



                       return '<option value="' + ro.id + '"' + (ro.id === f.role ? " selected" : "") + '>' + ro.label + '</option>';



                     }).join("") +



            '      </select></div>' +



            '      <div class="field" style="margin:0"><label style="font-size:.78rem">Sustituir por</label><select class="input small" id="tSelSwap">' +



            '        <option value="">Elegí un reemplazante…</option>' +



            '        <optgroup label="Suplentes">' +



                       benchPlayers.map(function (bp) {



                         return '<option value="' + bp.playerId + '">#' + bp.shirt + ' ' + esc(bp.name) + ' (' + A.posLabel(bp.pos) + ' · ' + bp.ovr + ')</option>';



                       }).join("") +



            '        </optgroup>' +



            '        <optgroup label="Otros titulares">' +



                       (d.eleven || []).filter(function (o) { return o.squadIndex !== idx; }).map(function (tp) {



                         return '<option value="' + tp.playerId + '">#' + tp.shirt + ' ' + esc(tp.playerName) + ' (' + A.posLabel(tp.roleName) + ')</option>';



                       }).join("") +



            '        </optgroup>' +



            '      </select></div>' +



            '    </div>' +



            '    <button class="btn btn-ghost xs" id="tDeselectPlayer" style="margin-top:8px">Listo</button>' +



            '  </div>' +



            '</div>';







          var deselBtn = page.querySelector("#tDeselectPlayer");



          if (deselBtn) {



            deselBtn.addEventListener("click", function () {



              selectedStarterFi = null;



              A.$$(".fm.on", pitchEl).forEach(function (el) { el.classList.remove("on"); });



              renderSelectedCard(null);



            });



          }







          var roleSel = page.querySelector("#tSelRole");



          if (roleSel) {



            roleSel.addEventListener("change", function (e) {



              var newRole = Number(e.target.value);



              rpc("setStarterRole", { teamId: d.id, squadIndex: f.squadIndex, role: newRole }).then(function () {



                f.role = newRole;



                var found = roleOptions.find(function (x) { return x.id === newRole; });



                f.roleName = found ? found.name : "JUG";



                toast("Rol actualizado: " + A.posLabel(f.roleName));



                selectTeam(d.id);



              }).catch(function (err) { toast(err.message, true); });



            });



          }







          var swapSel = page.querySelector("#tSelSwap");



          if (swapSel) {



            swapSel.addEventListener("change", function (e) {



              var targetPid = Number(e.target.value);



              if (!targetPid) return;



              rpc("swapSquadPlayers", { teamId: d.id, playerIdA: f.playerId, playerIdB: targetPid }).then(function () {



                toast("Sustitución realizada.");



                selectTeam(d.id);



              }).catch(function (err) { toast(err.message, true); });



            });



          }



        }







        // Pointer Events en Cancha: Arrastrar, soltar para intercambio o guardar coordenadas



        pitchEl.addEventListener("pointerdown", function (e) {



          var marker = e.target.closest(".fm");



          if (!marker) return;



          var idx = Number(marker.getAttribute("data-fi"));



          var starter = (d.eleven || [])[idx];



          if (!starter) return;







          activeDrag = {



            idx: idx,



            starter: starter,



            el: marker,



            startX: e.clientX,



            startY: e.clientY,



            moved: false



          };



          marker.setPointerCapture(e.pointerId);



        });







        pitchEl.addEventListener("pointermove", function (e) {



          if (!activeDrag) return;



          var dist = Math.abs(e.clientX - activeDrag.startX) + Math.abs(e.clientY - activeDrag.startY);



          if (dist > 5) activeDrag.moved = true;



          if (!activeDrag.moved) return;







          var rect = pitchEl.getBoundingClientRect();



          var dx = (e.clientX - rect.left) / rect.width * 100;



          var dy = (rect.bottom - e.clientY) / rect.height * 100;







          var normLeft = Math.max(5, Math.min(95, dx));



          var normBottom = Math.max(3, Math.min(93, dy));







          activeDrag.el.style.left = normLeft + "%";



          activeDrag.el.style.bottom = normBottom + "%";



          activeDrag.el.classList.add("dragging");







          pitchEl.querySelectorAll(".fm.drop").forEach(function (x) { x.classList.remove("drop"); });



          var elements = document.elementsFromPoint(e.clientX, e.clientY);



          var targetMarker = null;



          for (var k = 0; k < elements.length; k++) {



            var c = elements[k].closest && elements[k].closest(".fm");



            if (c && c !== activeDrag.el) { targetMarker = c; break; }



          }



          if (targetMarker) targetMarker.classList.add("drop");



        });







        pitchEl.addEventListener("pointerup", function (e) {



          if (!activeDrag) return;



          var drag = activeDrag;



          activeDrag = null;



          drag.el.classList.remove("dragging");



          pitchEl.querySelectorAll(".fm.drop").forEach(function (x) { x.classList.remove("drop"); });







          if (drag.moved) {



            var elements = document.elementsFromPoint(e.clientX, e.clientY);



            var targetMarker = null;



            for (var k = 0; k < elements.length; k++) {



              var c = elements[k].closest && elements[k].closest(".fm");



              if (c && c !== drag.el) { targetMarker = c; break; }



            }







            if (targetMarker) {



              var targetIdx = Number(targetMarker.getAttribute("data-fi"));



              var targetStarter = (d.eleven || [])[targetIdx];



              if (targetStarter) {



                rpc("swapSquadPlayers", { teamId: d.id, playerIdA: drag.starter.playerId, playerIdB: targetStarter.playerId }).then(function () {



                  toast("Puestos intercambiados: " + (drag.starter.playerName || "") + " ⇄ " + (targetStarter.playerName || ""));



                  selectTeam(d.id);



                }).catch(function (err) { toast(err.message, true); });



                return;



              }



            }







            var curLeft = parseFloat(drag.el.style.left);



            var curBottom = parseFloat(drag.el.style.bottom);







            // Conversion exacta al espacio Fox Engine



            var newX = Math.round(curLeft);



            newX = Math.max(5, Math.min(95, newX));



            var newY = Math.round(((curBottom - 3) / 94) * 50);



            newY = Math.max(3, Math.min(46, newY));







            rpc("setTacticalPosition", {



              teamId: d.id,



              squadIndex: drag.starter.squadIndex,



              depth: newY,



              width: newX,



              role: drag.starter.role



            }).then(function () {



              drag.starter.x = newX;



              drag.starter.y = newY;



              toast("Posición guardada: " + (drag.starter.playerName || "") + " (" + A.posLabel(drag.starter.roleName) + ")");



            }).catch(function (err) { toast(err.message, true); });







          } else {



            if (activeBenchPid != null) {



              var bPid = activeBenchPid;



              activeBenchPid = null;



              A.$$(".bn.on", page).forEach(function (b) { b.classList.remove("on"); });



              rpc("swapSquadPlayers", { teamId: d.id, playerIdA: bPid, playerIdB: drag.starter.playerId }).then(function () {



                toast("Cambio realizado.");



                selectTeam(d.id);



              }).catch(function (err) { toast(err.message, true); });



              return;



            }







            selectedStarterFi = selectedStarterFi === drag.idx ? null : drag.idx;



            pitchEl.querySelectorAll(".fm").forEach(function (m) { m.classList.toggle("on", m === drag.el && selectedStarterFi != null); });



            renderSelectedCard(selectedStarterFi);



          }



        });







        // Click en suplente para cambio rapido



        A.$$(".bn[data-bpid]", page).forEach(function (btn) {



          btn.addEventListener("click", function () {



            var bPid = Number(btn.getAttribute("data-bpid"));



            if (activeBenchPid === bPid) {



              activeBenchPid = null;



              btn.classList.remove("on");



            } else {



              activeBenchPid = bPid;



              A.$$(".bn", page).forEach(function (b) { b.classList.toggle("on", b === btn); });



              toast("Tocá al titular en la cancha que querés sustituir.");



            }



          });



        });







        // Balon parado



        var autoSpBtn = page.querySelector("#tAutoSetPieces");



        if (autoSpBtn) {



          autoSpBtn.addEventListener("click", function () {



            rpc("autoAssignRoles", { teamId: d.id }).then(function () {



              toast("Roles y capitanes auto-asignados según habilidades.");



              selectTeam(d.id);



            }).catch(function (err) { toast(err.message, true); });



          });



        }







        var saveSpBtn = page.querySelector("#tSaveSetPieces");



        if (saveSpBtn) {



          saveSpBtn.addEventListener("click", function () {



            var payload = {



              teamId: d.id,



              captainPid: Number(page.querySelector("#spCaptain").value) || 0,



              penaltyPid: Number(page.querySelector("#spPenalty").value) || 0,



              shortFkPid: Number(page.querySelector("#spShortFk").value) || 0,



              longFkPid: Number(page.querySelector("#spLongFk").value) || 0,



              leftCornerPid: Number(page.querySelector("#spLeftCorner").value) || 0,



              rightCornerPid: Number(page.querySelector("#spRightCorner").value) || 0



            };



            rpc("setSetPieceRoles", payload).then(function () {



              toast("Lanzadores y capitán guardados.");



              selectTeam(d.id);



            }).catch(function (err) { toast(err.message, true); });



          });



        }



      }



      initTacticsPitch();







      page.querySelector("#tPickEmblem").addEventListener("click", function () {



        rpc("pickFile", { title: "Elegí la imagen del escudo", filter: "Imágenes (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp" }).then(function (r) {



          if (!r.path) return;



          return rpc("setTeamEmblemFile", { path: r.path, id: d.id });



        }).then(function (r) { if (r) { toast("Escudo actualizado."); selectTeam(d.id); } }).catch(function (e) { toast(e.message, true); });



      });



      page.querySelector("#tEditBasic").addEventListener("click", function () { teamFormModal(d); });







            function collectFicha() {

        return {

          id: d.id,

          nameSpanish: page.querySelector("#eNameSp").value,

          nameEnglish: page.querySelector("#eNameEn").value,

          shortName: page.querySelector("#eShort").value,

          countryId: Number(page.querySelector("#eCountry").value) || 0,

          stadiumId: Number(page.querySelector("#eStadium").value) || 0,

          coachId: Number(page.querySelector("#eCoach").value) || 0,

          callnameId: Number(page.querySelector("#eCallname").value) || 0,

          isNational: page.querySelector("#eNational").checked,

          rivalIds: teamRivals.map(function (r) { return (r && r.id) ? Number(r.id) : 0; })

        };

      }



      // Link en Ficha para ir a Identidad y Rivales

      var btnGoToId = box.querySelector("#tGoToIdentity");

      if (btnGoToId) {

        btnGoToId.addEventListener("click", function () {

          activeTeamTab = "identidad";

          A.$$("[data-ttab]", box).forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-ttab") === "identidad"); });

          A.$$(".ttab-pane", box).forEach(function (p) { p.style.display = (p.id === "tPane-identidad" ? "" : "none"); });

        });

      }



      // Live Color Picker & Radar Preview

      var col1 = box.querySelector("#eColor1"), col1Hex = box.querySelector("#eColor1Hex");

      var col2 = box.querySelector("#eColor2"), col2Hex = box.querySelector("#eColor2Hex");

      var rad1 = box.querySelector("#tRadarHalf1"), rad2 = box.querySelector("#tRadarHalf2");



      if (col1 && col1Hex) {

        col1.addEventListener("input", function () {

          col1Hex.value = col1.value;

          if (rad1) rad1.style.background = col1.value;

          updateBannersPreview();

        });

        col1Hex.addEventListener("input", function () {

          if (/^#[0-9A-Fa-f]{6}$/.test(col1Hex.value)) {

            col1.value = col1Hex.value;

            if (rad1) rad1.style.background = col1Hex.value;

            updateBannersPreview();

          }

        });

      }



      if (col2 && col2Hex) {

        col2.addEventListener("input", function () {

          col2Hex.value = col2.value;

          if (rad2) rad2.style.background = col2.value;

          updateBannersPreview();

        });

        col2Hex.addEventListener("input", function () {

          if (/^#[0-9A-Fa-f]{6}$/.test(col2Hex.value)) {

            col2.value = col2Hex.value;

            if (rad2) rad2.style.background = col2Hex.value;

            updateBannersPreview();

          }

        });

      }



      // Live Stadium Banners Preview

      function updateBannersPreview() {

        var strip = box.querySelector("#tBannerPreviewStrip");

        if (!strip) return;

        var b0 = (box.querySelector("#eBanner0") ? box.querySelector("#eBanner0").value : (d.banners || [])[0]) || "";

        var b1 = (box.querySelector("#eBanner1") ? box.querySelector("#eBanner1").value : (d.banners || [])[1]) || "";

        var b2 = (box.querySelector("#eBanner2") ? box.querySelector("#eBanner2").value : (d.banners || [])[2]) || "";

        var b3 = (box.querySelector("#eBanner3") ? box.querySelector("#eBanner3").value : (d.banners || [])[3]) || "";

        var banners = [b0, b1, b2, b3];

        var c2 = (box.querySelector("#eColor2") ? box.querySelector("#eColor2").value : (d.color2 || "#00E5FF"));

        strip.innerHTML = banners.map(function (b, idx) {

          var txt = b.trim() || ("BANDERA " + (idx + 1));

          var isDef = !b.trim();

          return '<div class="banner-cloth" style="' + (isDef ? 'opacity:.35;' : '') + 'border-color:' + c2 + '55">' + esc(txt) + '</div>';

        }).join("");

      }



      [0, 1, 2, 3].forEach(function (i) {

        var inp = box.querySelector("#eBanner" + i);

        var cnt = box.querySelector("#cntBanner" + i);

        if (inp) {

          inp.addEventListener("input", function () {

            if (cnt) cnt.textContent = inp.value.length + "/15 letras";

            updateBannersPreview();

          });

        }

      });

      updateBannersPreview();



      // Modal buscador de equipos rivales

      function openTeamPickerModal(onPick) {

        rpc("listTeams", { limit: 1200 }).then(function (res) {

          var allTeams = (res.teams || []).filter(function (t) {

            if (t.id === d.id) return false;

            for (var s = 0; s < 3; s++) {

              if (teamRivals[s] && teamRivals[s].id === t.id) return false;

            }

            return true;

          });



          var modalHtml =

            '<div class="team-picker-modal">' +

              '<div class="team-picker-search-bar">' +

                '<input type="text" class="input" id="tpSearch" placeholder="🔍 Buscar equipo por nombre, sigla o ID..." autofocus>' +

                '<div class="chips" style="margin-top:8px">' +

                  '<button class="chip on" data-tp-scope="all">Todos</button>' +

                  '<button class="chip" data-tp-scope="clubs">Clubes</button>' +

                  '<button class="chip" data-tp-scope="nationals">Selecciones</button>' +

                '</div>' +

              '</div>' +

              '<div class="team-picker-list" id="tpList" style="max-height:380px; overflow-y:auto; margin-top:12px"></div>' +

            '</div>';



          A.showModal("Elegir equipo rival", modalHtml, [{ label: "Cancelar", cls: "btn-ghost" }]);



          var modal = document.getElementById("modal");

          var tpSearch = modal.querySelector("#tpSearch");

          var tpList = modal.querySelector("#tpList");

          var tpScope = "all";



          function filterAndRender() {

            var query = (tpSearch.value || "").trim().toLowerCase();

            var filtered = allTeams.filter(function (t) {

              if (tpScope === "clubs" && t.isNational) return false;

              if (tpScope === "nationals" && !t.isNational) return false;

              if (!query) return true;

              var tName = (t.name || t.nameSpanish || t.nameEnglish || "").toLowerCase();

              var sName = (t.shortName || "").toLowerCase();

              var lName = (t.leagueName || "").toLowerCase();

              var cName = (t.countryName || "").toLowerCase();

              return tName.includes(query) || sName.includes(query) || String(t.id).includes(query) || lName.includes(query) || cName.includes(query);

            });



            if (!filtered.length) {

              tpList.innerHTML = '<div class="empty-state" style="padding:20px"><p>No se encontraron equipos para "' + esc(query) + '"</p></div>';

              return;

            }



            tpList.innerHTML = filtered.slice(0, 100).map(function (t) {

              var tName = t.name || t.nameSpanish || t.nameEnglish || ("Equipo " + t.id);

              var sName = t.shortName || (tName.length >= 3 ? tName.substring(0, 3).toUpperCase() : tName);

              return '<button type="button" class="row tp-row" data-tp-id="' + t.id + '" style="width:100%; border-radius:8px; margin-bottom:4px">' +

                A.crestHtml(t.id, tName, sName) +

                '<span><span class="nm-line"><span class="nm">' + esc(tName) + '</span>' +

                (t.isNational ? '<span class="pill nat">SEL</span>' : '') +

                '</span><span class="sub">' + (t.leagueName ? esc(t.leagueName) : "Libre") + ' · ' + esc(t.countryName || "") + '</span></span>' +

                '<span class="r-meta">ID ' + t.id + '</span>' +

              '</button>';

            }).join("");



            A.$$(".tp-row", tpList).forEach(function (btn) {

              btn.addEventListener("click", function () {

                var tid = Number(btn.getAttribute("data-tp-id"));

                var found = allTeams.find(function (x) { return x.id === tid; });

                if (found) {

                  A.hideModal();

                  onPick(found);

                }

              });

            });

          }



          var tpDeb = null;

          tpSearch.addEventListener("input", function () {

            clearTimeout(tpDeb);

            tpDeb = setTimeout(filterAndRender, 150);

          });



          A.$$("[data-tp-scope]", modal).forEach(function (chip) {

            chip.addEventListener("click", function () {

              tpScope = chip.getAttribute("data-tp-scope");

              A.$$("[data-tp-scope]", modal).forEach(function (c) { c.classList.toggle("on", c === chip); });

              filterAndRender();

            });

          });



          filterAndRender();

          setTimeout(function () { if (tpSearch) tpSearch.focus(); }, 100);

        }).catch(function (err) {

          toast("Error cargando equipos: " + err.message, true);

        });

      }



      // Render de grilla de rivales

      function renderRivalsGrid() {

        var grid = box.querySelector("#tRivalsGrid");

        if (!grid) return;

        var slotTitles = [

          "Rival 1 · Clásico Máximo / Derby Principal",

          "Rival 2 · Rivalidad Provincial / Histórica",

          "Rival 3 · Rival Secundario"

        ];

        var slotBadges = ["Derby Principal", "Rival 2", "Rival 3"];

        var slotColors = ["#FF2A6D", "#00E5FF", "#FFD166"];

        grid.innerHTML = [0, 1, 2].map(function (idx) {

          var rv = teamRivals[idx] || { id: 0, name: "", shortName: "" };

          var hasRival = rv.id > 0;

          var rvName = rv.name || ("Equipo " + rv.id);

          var rvShort = rv.shortName || (rvName.length >= 3 ? rvName.substring(0, 3).toUpperCase() : rvName);



          return '<div class="rival-slot' + (hasRival ? ' filled' : ' empty') + '" data-rival-slot="' + idx + '">' +

            '<div class="rival-slot-head">' +

              '<span class="rival-slot-badge" style="background:' + slotColors[idx] + '22; color:' + slotColors[idx] + '; border-color:' + slotColors[idx] + '55">' + slotBadges[idx] + '</span>' +

              '<span class="rival-slot-title">' + slotTitles[idx] + '</span>' +

            '</div>' +

            (hasRival ?

              '<div class="rival-slot-body">' +

                A.crestHtml(rv.id, rvName, rvShort) +

                '<div class="rival-slot-info">' +

                  '<div class="rival-slot-name">' + esc(rvName) + '</div>' +

                  '<div class="rival-slot-meta"><span class="r-meta">ID ' + rv.id + '</span> ' + (rv.shortName ? '<span class="pill sm">' + esc(rv.shortName) + '</span>' : '') + '</div>' +

                '</div>' +

                '<div class="rival-slot-actions">' +

                  '<button type="button" class="btn btn-ghost xs" data-action="change-rival" data-slot="' + idx + '">🔄 Cambiar</button>' +

                  '<button type="button" class="btn danger xs" data-action="remove-rival" data-slot="' + idx + '">✕ Quitar</button>' +

                '</div>' +

              '</div>'

              :

              '<div class="rival-slot-empty">' +

                '<div class="rival-empty-icon">⚔️</div>' +

                '<div class="rival-empty-text">Sin clásico asignado en este slot</div>' +

                '<button type="button" class="btn btn-ghost sm" data-action="add-rival" data-slot="' + idx + '">+ Asignar rival</button>' +

              '</div>'

            ) +

          '</div>';

        }).join("");



        A.$$("[data-action='change-rival'], [data-action='add-rival']", grid).forEach(function (btn) {

          btn.addEventListener("click", function () {

            var slot = Number(btn.getAttribute("data-slot"));

            openTeamPickerModal(function (pickedTeam) {

              teamRivals[slot] = {

                id: pickedTeam.id,

                name: pickedTeam.name || pickedTeam.nameSpanish || pickedTeam.nameEnglish || ("Equipo " + pickedTeam.id),

                shortName: pickedTeam.shortName || ""

              };

              renderRivalsGrid();

            });

          });

        });



        A.$$("[data-action='remove-rival']", grid).forEach(function (btn) {

          btn.addEventListener("click", function () {

            var slot = Number(btn.getAttribute("data-slot"));

            teamRivals[slot] = { id: 0, name: "", shortName: "" };

            renderRivalsGrid();

          });

        });

      }



      renderRivalsGrid();



      page.querySelector("#tSaveFicha").addEventListener("click", function () {

        rpc("updateTeam", collectFicha()).then(function () {

          toast("Ficha técnica guardada.");

          loadList();

        }).catch(function (e) { toast(e.message, true); });

      });



      page.querySelector("#tSaveIdentity").addEventListener("click", function () {

        var payload = collectFicha();

        payload.color1 = page.querySelector("#eColor1").value;

        payload.color2 = page.querySelector("#eColor2").value;

        payload.banners = [0, 1, 2, 3].map(function (i) { return page.querySelector("#eBanner" + i).value; });

        payload.rivalIds = teamRivals.map(function (r) { return r.id || 0; });

        rpc("updateTeam", payload).then(function () {

          toast("Identidad y rivales guardados con éxito.");

          loadList();

        }).catch(function (e) { toast(e.message, true); });

      });



// tApplyFormation reemplazado por la botonera fluida de esquemas







      // dorsales



      A.$$("[data-shirt]", page).forEach(function (inp) {



        inp.addEventListener("change", function () {



          rpc("setShirtNumber", { teamId: d.id, playerId: Number(inp.getAttribute("data-shirt")), shirt: Number(inp.value) }).then(function (r) {



            if (r.changed) toast("Dorsal actualizado."); else { toast(r.error || "No se pudo cambiar el dorsal.", true); selectTeam(d.id); }



          }).catch(function (e) { toast(e.message, true); });



        });



      });



      A.$$("[data-release]", page).forEach(function (b) {



        b.addEventListener("click", function () {



          var pid = Number(b.getAttribute("data-release"));



          A.confirmModal("Liberar jugador", "El jugador queda como agente libre. ¿Seguro?", "Liberar", function () {



            rpc("releasePlayer", { playerId: pid, teamId: d.id }).then(function () { toast("Jugador liberado."); selectTeam(d.id); }).catch(function (e) { toast(e.message, true); });



          });



        });



      });



      A.$$("[data-player-id]", page).forEach(function (lnk) {

        lnk.addEventListener("click", function (e) {

          e.preventDefault();

          var pid = Number(this.getAttribute("data-player-id"));

          if (pid && A.goto) {

            A.goto("jugadores");

            if (typeof A.selectPlayer === "function") A.selectPlayer(pid);

          }

        });

      });



      A.$$("[data-transfer]", page).forEach(function (b) {



        b.addEventListener("click", function () {



          var pid = Number(b.getAttribute("data-transfer"));



          rpc("listTeams", { limit: 800 }).then(function (r) {



            A.showModal("Traspasar jugador",



              '<div class="field"><label>Equipo destino</label><select class="input" id="trTeam">' +



              r.teams.filter(function (t) { return t.id !== d.id; }).map(function (t) { return '<option value="' + t.id + '">' + esc(A.teamName(t)) + "</option>"; }).join("") + "</select></div>",



              [{ label: "Cancelar" }, {



                label: "Traspasar", cls: "btn-primary", onClick: function () {



                  rpc("transferPlayer", { playerId: pid, fromTeamId: d.id, toTeamId: Number(document.querySelector("#trTeam").value) }).then(function () {



                    toast("Jugador traspasado."); selectTeam(d.id); loadList();



                  }).catch(function (e) { toast(e.message, true); });



                }



              }]);



          });



        });



      });







      // fichar agente libre



      var agDeb = null;



      function refreshAgents() {



        rpc("freeAgents", { search: page.querySelector("#tAgentSearch").value, limit: 120 }).then(function (r) {



          page.querySelector("#tAgentSelect").innerHTML = r.players.map(function (p) {



            return '<option value="' + p.id + '">' + esc(p.name) + " · " + esc(p.pos) + " · " + p.ovr + "</option>";



          }).join("");



        }).catch(function () { });



      }



      page.querySelector("#tAgentSearch").addEventListener("input", function () { clearTimeout(agDeb); agDeb = setTimeout(refreshAgents, 250); });



      refreshAgents();



      page.querySelector("#tSign").addEventListener("click", function () {



        var pid = Number(page.querySelector("#tAgentSelect").value);



        if (!pid) { toast("Elegí un agente libre.", true); return; }



        rpc("signFreeAgent", { playerId: pid, teamId: d.id, shirt: 0 }).then(function () {



          toast("Jugador fichado."); selectTeam(d.id); loadList();



        }).catch(function (e) { toast(e.message, true); });



      });



      page.querySelector("#tAutoRoles").addEventListener("click", function () {



        rpc("autoAssignRoles", { teamId: d.id }).then(function () { toast("Roles asignados."); selectTeam(d.id); }).catch(function (e) { toast(e.message, true); });



      });



      loadSquadGen(box, d);



    }







    // ---------- Fase 5A: completar plantel ----------



    function loadSquadGen(box, d) {



      var host = box.querySelector("#tSquadGen");



      if (!host) return;



      if ((d.playerCount || 0) >= 23) { host.innerHTML = ""; return; }



      host.innerHTML =



        '<div class="card-sec"><h3>⚡ Plantel incompleto <span class="sub">tiene ' + (d.playerCount || 0) + ' futbolistas</span></h3>' +



        '<p class="mut">Se puede completar fichando <b>agentes libres</b> de tu base, con equilibrio por línea, formación y roles. No se le saca ningún jugador a ningún equipo.</p>' +



        '<div style="display:flex; gap:8px; align-items:center"><label class="mut">Plantel objetivo</label>' +



        '<input class="input small" type="number" id="sgTarget" value="23" min="11" max="32" style="width:80px">' +



        '<button class="btn btn-ghost" id="sgPreview">👁️ Ver previa de fichajes</button></div>' +



        '<div id="sgPreviewBox" style="margin-top:10px"></div></div>';







      host.querySelector("#sgPreview").addEventListener("click", function () {



        var target = Number(host.querySelector("#sgTarget").value) || 23;



        rpc("previewSquad", { teamId: d.id, target: target }).then(function (pv) {



          var target2 = pv.target;



          var html = '<h3>Previa: plantel de ' + pv.current + " → " + pv.final + " (objetivo " + target2 + ")</h3>";



          pv.lineas.forEach(function (l) {



            html += '<div class="kv"><span>' + esc(l.linea) + " — tiene " + l.actual + " · ideal " + l.cuota + '</span><code>ficha ' + l.fichados.length + "</code></div>";



            l.fichados.forEach(function (p) {



              html += '<div class="bk-row"><span class="tag ok">' + esc(p.pos) + '</span><div style="flex:1; min-width:0"><span class="nm">' + esc(p.nombre) + '</span></div><span class="r-meta">' + p.overall + "</span></div>";



            });



          });



          (pv.warnings || []).forEach(function (w) {



            html += '<p class="mut" style="color:var(--gold)">' + esc(w) + "</p>";



          });



          if (pv.final > pv.current) {



            html += '<div class="btn-row" style="margin-top:10px"><button class="btn btn-primary" id="sgApply">⚡ Completar el plantel</button>' +



              '<span class="mut small">Antes se crea un punto de restauración del Sider.</span></div>';



          } else {



            html += '<p class="mut">No hace falta fichar a nadie para llegar al objetivo.</p>';



          }



          host.querySelector("#sgPreviewBox").innerHTML = html;



          var btn = host.querySelector("#sgApply");



          if (btn) btn.addEventListener("click", function () {



            A.confirmModal("Completar plantel", "Se fichan los agentes libres de la previa para «" + A.teamName(d) + "». Antes se crea un punto de restauración. ¿Continuar?", "Completar", function () {



              rpc("applySquad", { teamId: d.id, target: target2 }).then(function (r) {



                toast(r.message);



                A.reloadState();



                selectTeam(d.id); loadList();



              }).catch(function (e) { toast(e.message, true); });



            });



          });



        }).catch(function (e) { toast(e.message, true); });



      });



    }







    loadList();



    return Promise.resolve();



  });



})();



