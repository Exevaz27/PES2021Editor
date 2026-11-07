// ============================================================
// Sección: Mundo — registro (Fase 3) + intercambio (Punto A FL26).
// Un mundo es la instalación completa como unidad: su pesdb en el
// paquete y su carpeta de guardado (carreras + EDIT). Cambiar de
// mundo intercambia ese contenido; el resto (Sider, lua, content)
// es compartido.
// ============================================================
(function () {
  "use strict";
  var A = window.App, rpc = A.rpc, esc = A.esc, toast = A.toast;

  function pintar(page, r) {
    if (!r.registered) {
      page.innerHTML =
        '<h2 class="page-title">🌍 Mundo</h2>' +
        '<p class="page-sub">Un mundo es tu instalación completa como una unidad con nombre: su pesdb (ligas, equipos y jugadores) y su carpeta de guardado (carreras y EDIT). Con más de un mundo, el juego muestra solo las ligas del mundo activo.</p>' +
        '<div class="card-sec"><h3>Todavía no registraste tu mundo</h3>' +
        '<p class="mut">Registrar el Mundo 1 no cambia nada de tu instalación: solo le pone nombre y le saca una foto (rutas, tus ligas custom, resumen de la base y el veredicto del diagnóstico), con un punto de restauración de ancla en tu Sider.</p>' +
        '<div class="btn-row" style="margin-top:12px"><button class="btn btn-primary" id="mwRegister">🌍 Registrar mi instalación como Mundo 1</button></div></div>';
      page.querySelector("#mwRegister").addEventListener("click", function () {
        rpc("registerWorld", {}).then(function () { cargar(page); }).catch(function (e) { toast(e.message, true); });
      });
      return;
    }

    var rows = r.worlds.map(function (w) {
      var ligasTxt = w.ligas && w.ligas.length
        ? w.ligas.map(function (l) { return esc(l.nombre) + " (" + l.equipos + ")"; }).join(" · ")
        : "sin ligas custom en la foto";
      return '<div class="bk-row">' +
        (w.activo ? '<span class="tag ok">ACTIVO</span>' : '<span class="tag warn">guardado</span>') +
        '<div style="flex:1; min-width:0"><span class="nm">' + esc(w.nombre) + '</span>' +
        '<span class="sub">' + w.totalCompeticiones + " competiciones · " + w.totalEquipos + " equipos · " + w.totalJugadores + " jugadores · actualizado " + esc(w.actualizado) + "</span>" +
        '<span class="sub">' + ligasTxt + "</span>" +
        '<span class="sub">' + (w.tieneStore ? "pesdb guardada ✓" : "sin pesdb guardada todavía") +
        (w.savesFolder ? " · carreras: " + esc(w.savesFolder) : " · sin carreras registradas") + "</span></div>" +
        '<span class="bk-actions">' +
        (w.activo ? "" : '<button class="btn btn-primary" data-act="' + w.id + '">▶️ Activar</button>') +
        '<button class="btn btn-ghost" data-ren="' + w.id + '" data-nombre="' + esc(w.nombre) + '">✏️ Renombrar</button>' +
        (w.activo ? "" : '<button class="btn danger" data-del="' + w.id + '" data-nombre="' + esc(w.nombre) + '">🗑️ Borrar</button>') +
        "</span></div>";
    }).join("");

    page.innerHTML =
      '<h2 class="page-title">🌍 Mundos</h2>' +
      '<p class="page-sub">El juego muestra solo las ligas del mundo <b>activo</b>. Al activar otro, esta app guarda primero el mundo que dejás tal como está, intercambia las carreras (la carpeta de guardado, con tu Master League y tu EDIT) y carga la pesdb del mundo entrante. <b>Cerrá el juego antes de cambiar de mundo.</b></p>' +
      '<div class="card-sec"><h3>Tus mundos <span class="sub">' + r.worlds.length + '</span></h3>' + rows +
      '<div class="btn-row" style="margin-top:12px">' +
      '<button class="btn btn-primary" id="mwNew">➕ Nuevo mundo</button>' +
      '<button class="btn btn-ghost" id="mwRefresh">📸 Actualizar la foto del activo</button>' +
      "</div></div>";

    page.querySelector("#mwRefresh").addEventListener("click", function () {
      rpc("registerWorld", {}).then(function (r2) { if (r2.message) toast(r2.message); cargar(page); }).catch(function (e) { toast(e.message, true); });
    });
    page.querySelector("#mwNew").addEventListener("click", function () {
      A.showModal("Nuevo mundo",
        '<div class="form-grid">' +
        '<div class="field"><label>Nombre</label><input class="input" id="nwNombre" placeholder="Mundo 2"></div>' +
        '<div class="field"><label>Cómo nace</label><select class="input" id="nwModo">' +
        '<option value="clon">Clonar el mundo actual (para experimentar sin romper el bueno)</option>' +
        '<option value="limpia">Desde la base limpia del juego (universo desde cero)</option>' +
        "</select></div></div>" +
        '<p class="mut small" style="margin-top:10px">El mundo nuevo nace guardado, no activo: lo activás cuando quieras. Sus carreras arrancan en blanco.</p>',
        [{ label: "Cancelar" }, {
          label: "Crear mundo", cls: "btn-primary", onClick: function () {
            rpc("createWorld", { nombre: document.querySelector("#nwNombre").value, modo: document.querySelector("#nwModo").value })
              .then(function (r2) { if (r2.message) toast(r2.message); cargar(page); })
              .catch(function (e) { toast(e.message, true); });
          }
        }]);
    });
    page.querySelectorAll("[data-act]").forEach(function (b) {
      b.addEventListener("click", function () {
        var id = b.getAttribute("data-act");
        var w = r.worlds.filter(function (x) { return x.id === id; })[0];
        A.confirmModal("¿Activar «" + (w ? w.nombre : id) + "»?",
          "Se guarda el mundo actual tal como está, se intercambian las carreras y el EDIT, y el paquete pasa a tener las ligas de «" + (w ? w.nombre : id) + "». Cerrá el juego antes de seguir.",
          "▶️ Activar mundo", function () {
            rpc("switchWorld", { id: id }).then(function (r2) {
              if (r2.message) toast(r2.message);
              A.reloadState(); cargar(page);
            }).catch(function (e) { toast(e.message, true); });
          });
      });
    });
    page.querySelectorAll("[data-ren]").forEach(function (b) {
      b.addEventListener("click", function () {
        A.showModal("Renombrar mundo",
          '<div class="field"><label>Nombre</label><input class="input" id="rnNombre" value="' + b.getAttribute("data-nombre") + '"></div>',
          [{ label: "Cancelar" }, {
            label: "Renombrar", cls: "btn-primary", onClick: function () {
              rpc("renameWorld", { id: b.getAttribute("data-ren"), nombre: document.querySelector("#rnNombre").value })
                .then(function (r2) { if (r2.message) toast(r2.message); cargar(page); })
                .catch(function (e) { toast(e.message, true); });
            }
          }]);
      });
    });
    page.querySelectorAll("[data-del]").forEach(function (b) {
      b.addEventListener("click", function () {
        A.confirmModal("¿Borrar «" + b.getAttribute("data-nombre") + "»?",
          "Su pesdb guardada y sus carreras se mueven a la papelera de editorpro-respaldos (recuperable a mano). El mundo activo no se toca.",
          "🗑️ Borrar mundo", function () {
            rpc("deleteWorld", { id: b.getAttribute("data-del") }).then(function (r2) {
              if (r2.message) toast(r2.message); cargar(page);
            }).catch(function (e) { toast(e.message, true); });
          });
      });
    });
  }

  function cargar(page) {
    rpc("listWorlds", {}).then(function (r) { pintar(page, r); }).catch(function (e) { toast(e.message, true); });
  }

  A.register("mundo", function (page) { cargar(page); return Promise.resolve(); });
})();
