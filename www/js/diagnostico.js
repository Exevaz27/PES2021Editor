// ============================================================
// Sección: Diagnóstico (Fase 1 FL26) — solo lectura.
// El motor de chequeos vive en C# (HealthCheck.cs); esta pantalla
// solo muestra el informe que devuelve la acción getHealth.
// ============================================================
(function () {
  "use strict";
  var A = window.App, rpc = A.rpc, esc = A.esc, toast = A.toast;

  var NIVEL = {
    Ok: { tag: "OK", cls: "ok", ico: "✅" },
    Info: { tag: "INFO", cls: "info", ico: "ℹ️" },
    Aviso: { tag: "AVISO", cls: "aviso", ico: "⚠️" },
    Error: { tag: "ERROR", cls: "error", ico: "❌" }
  };

  function gruposDe(items) {
    var orden = [], mapa = {};
    items.forEach(function (it) {
      if (!mapa[it.grupo]) { mapa[it.grupo] = []; orden.push(it.grupo); }
      mapa[it.grupo].push(it);
    });
    return orden.map(function (g) { return { nombre: g, items: mapa[g] }; });
  }

  function pintar(page, r) {
    var vcls = r.veredicto === "Error" ? "error" : r.veredicto === "Aviso" ? "aviso" : "ok";
    var vico = r.veredicto === "Error" ? "❌" : r.veredicto === "Aviso" ? "⚠️" : "✅";
    var html =
      '<h2 class="page-title">Diagnóstico</h2>' +
      '<p class="page-sub">¿Está todo listo para jugar con tus ligas nuevas? Chequeos sobre tu instalación: rutas, sider.ini, el lua desplegado, el paquete livecpk, el log de Sider, tu base y el EDIT. Los chequeos no modifican nada; los arreglos con un clic crean antes un punto de restauración del Sider.</p>' +
      '<div class="hl-banner ' + vcls + '"><div class="hl-b-ico">' + vico + '</div>' +
      '<div style="flex:1; min-width:0"><div class="hl-b-title">' + esc(r.resumen) + '</div>' +
      (r.primerTitulo
        ? '<div class="hl-b-sub">Primer problema: ' + esc(r.primerId) + ' · ' + esc(r.primerTitulo) + '</div>'
        : '<div class="hl-b-sub">Sin problemas: todo lo chequeado está en orden.</div>') +
      '</div>' +
      '<button class="btn btn-ghost" id="hlRun">🔄 Volver a chequear</button>' +
      '<button class="btn btn-ghost" id="hlCopy">📋 Copiar informe</button></div>';

    gruposDe(r.items).forEach(function (g) {
      html += '<h3 class="hl-group">' + esc(g.nombre) + '</h3><div class="list-panel hl-panel">';
      g.items.forEach(function (it) {
        var n = NIVEL[it.nivel] || NIVEL.Info;
        html += '<div class="hl-row"><span class="hl-estado ' + n.cls + '">' + n.ico + ' ' + n.tag + '</span>' +
          '<span class="hl-id">' + esc(it.id) + '</span>' +
          '<div class="hl-main"><div class="hl-titulo">' + esc(it.titulo) + '</div>' +
          ((it.detalle || it.sugerencia)
            ? '<div class="hl-det">' + esc(it.detalle || '') + (it.sugerencia ? ' → ' + esc(it.sugerencia) : '') + '</div>'
            : '') +
          '</div>' +
          (it.fix ? '<button class="btn btn-primary hl-fix" data-fix="' + esc(it.id) + '">🔧 Arreglar</button>' : '') +
          '</div>';
      });
      html += '</div>';
    });

    page.innerHTML = html;
    page.querySelectorAll("[data-fix]").forEach(function (b) {
      b.addEventListener("click", function () {
        A.confirmModal("Arreglar " + b.getAttribute("data-fix"),
          "Se crea un punto de restauración del Sider y se aplica el arreglo. El diagnóstico se vuelve a correr solo. ¿Continuar?",
          "Arreglar", function () {
            b.disabled = true;
            rpc("fixHealthItem", { id: b.getAttribute("data-fix") }).then(function (r2) {
              if (r2.message) toast(r2.message);
              pintar(page, r2);
            }).catch(function (e) { toast(e.message, true); b.disabled = false; });
          });
      });
    });
    page.querySelector("#hlRun").addEventListener("click", function () { cargar(page); });
    page.querySelector("#hlCopy").addEventListener("click", function () {
      var done = function () { toast("Informe copiado al portapapeles."); };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(r.texto || "").then(done, function () { toast("No se pudo copiar el informe.", true); });
      } else {
        var ta = document.createElement("textarea");
        ta.value = r.texto || "";
        document.body.appendChild(ta); ta.select();
        try { document.execCommand("copy"); done(); } catch (e) { toast("No se pudo copiar el informe.", true); }
        document.body.removeChild(ta);
      }
    });
  }

  function cargar(page) {
    page.innerHTML = '<h2 class="page-title">Diagnóstico</h2><p class="page-sub">Chequeando tu instalación…</p><span class="spin"></span> Corriendo los chequeos…';
    return rpc("getHealth").then(function (r) { pintar(page, r); })
      .catch(function (e) { toast(e.message, true); });
  }

  A.register("diagnostico", cargar);
})();
