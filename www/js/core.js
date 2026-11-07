// ============================================================
// PES 2021 EDITOR PRO — nucleo de la interfaz
// Puente con C# (window.chrome.webview), estado global, router,
// toast/modal y utilidades compartidas por las secciones.
// ============================================================
(function () {
  "use strict";

  var seq = 0;
  var pending = new Map();

  function rpc(action, payload) {
    return new Promise(function (resolve, reject) {
      if (!(window.chrome && window.chrome.webview)) {
        reject(new Error("Sin puente: abrí la app desde el ejecutable."));
        return;
      }
      var id = ++seq;
      var timer = setTimeout(function () {
        if (pending.has(id)) { pending.delete(id); reject(new Error("El núcleo no respondió a tiempo (" + action + ").")); }
      }, 120000);
      pending.set(id, { resolve: resolve, reject: reject, timer: timer });
      window.chrome.webview.postMessage(Object.assign({ action: action, _id: id }, payload || {}));
    });
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function (ev) {
      var m = ev.data;
      if (!m || m._replyTo == null) return;
      var p = pending.get(m._replyTo);
      if (!p) return;
      pending.delete(m._replyTo);
      clearTimeout(p.timer);
      if (m.ok === false) p.reject(new Error(m.error || "Error del núcleo"));
      else p.resolve(m);
    });
  }

  // ---------- estado global ----------
  var S = {
    config: null,
    dbLoaded: false,
    stats: null,
    dbPath: "",
    editLoaded: false,
    countries: [],           // cache de paises (se llena con createLeagueOptions/getTeam)
    sectionsLoaded: {}
  };

  // ---------- helpers DOM ----------
  function el(html) {
    var t = document.createElement("template");
    t.innerHTML = html.trim();
    return t.content.firstElementChild;
  }
  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function $(sel, root) { return (root || document).querySelector(sel); }
  function $$(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

  // Pares de colores por equipo (escudo bitono diagonal, sistema del PER)
  var CREST_PAIRS = [["#12285E", "#B8860B"], ["#5E1220", "#8A2438"], ["#0D4A5E", "#127A8C"], ["#14532D", "#1E7A46"], ["#3B2A63", "#5B439E"], ["#6B3410", "#9A5A1E"], ["#374151", "#5B6B82"], ["#7A1F3D", "#A62B52"]];
  function crestPair(id) { return CREST_PAIRS[Math.abs(Number(id) || 0) % CREST_PAIRS.length]; }
  function crestColor(id) { return crestPair(id)[0]; }
  function crestColor2(id) { return crestPair(id)[1]; }
  // Mapeo exhaustivo de posiciones PES 2021 (Inglés Internacional <-> Español Oficial)
  var POS_LIST = [
    { en: "GK",  es: "PT",  idx: 0,  line: "gk", desc: "Portero", descEn: "Goalkeeper" },
    { en: "CB",  es: "CT",  idx: 1,  line: "df", desc: "Central", descEn: "Center Back" },
    { en: "LB",  es: "LI",  idx: 2,  line: "df", desc: "Lateral Izquierdo", descEn: "Left Back" },
    { en: "RB",  es: "LD",  idx: 3,  line: "df", desc: "Lateral Derecho", descEn: "Right Back" },
    { en: "DMF", es: "MCD", idx: 4,  line: "mf", desc: "Mediocentro Defensivo", descEn: "Defensive Midfielder" },
    { en: "CMF", es: "MC",  idx: 5,  line: "mf", desc: "Mediocentro", descEn: "Central Midfielder" },
    { en: "LMF", es: "II",  idx: 6,  line: "mf", desc: "Interior Izquierdo", descEn: "Left Midfielder" },
    { en: "RMF", es: "ID",  idx: 7,  line: "mf", desc: "Interior Derecho", descEn: "Right Midfielder" },
    { en: "AMF", es: "MP",  idx: 8,  line: "mf", desc: "Mediapunta", descEn: "Attacking Midfielder" },
    { en: "LWF", es: "EI",  idx: 9,  line: "fw", desc: "Extremo Izquierdo", descEn: "Left Winger" },
    { en: "RWF", es: "ED",  idx: 10, line: "fw", desc: "Extremo Derecho", descEn: "Right Winger" },
    { en: "SS",  es: "SD",  idx: 11, line: "fw", desc: "Segundo Delantero", descEn: "Second Striker" },
    { en: "CF",  es: "DC",  idx: 12, line: "fw", desc: "Delantero Centro", descEn: "Center Forward" }
  ];

  var POS_EN_TO_ES = {};
  var POS_ES_TO_EN = {};
  var POS_DESC_ES = {};
  var POS_DESC_EN = {};
  POS_LIST.forEach(function (p) {
    POS_EN_TO_ES[p.en] = p.es;
    POS_ES_TO_EN[p.es] = p.en;
    POS_DESC_ES[p.en] = p.desc;
    POS_DESC_ES[p.es] = p.desc;
    POS_DESC_EN[p.en] = p.descEn;
    POS_DESC_EN[p.es] = p.descEn;
  });

  function getPosLang() {
    var l = localStorage.getItem("pes_pos_lang");
    return (l === "en") ? "en" : "es"; // Español PES 2021 por defecto
  }

  function setPosLang(lang) {
    lang = (lang === "en") ? "en" : "es";
    localStorage.setItem("pes_pos_lang", lang);
    var toggles = document.querySelectorAll(".pos-lang-toggle .plt-btn");
    toggles.forEach(function (btn) {
      btn.classList.toggle("active", btn.getAttribute("data-poslang") === lang);
    });
    window.dispatchEvent(new CustomEvent("poslangchange", { detail: { lang: lang } }));
  }

  function posLabel(pos, targetLang) {
    if (!pos) return "";
    var l = targetLang || getPosLang();
    var p = String(pos).trim().toUpperCase();
    if (l === "es") {
      return POS_EN_TO_ES[p] || p;
    } else {
      return POS_ES_TO_EN[p] || p;
    }
  }

  function posDesc(pos, targetLang) {
    if (!pos) return "";
    var l = targetLang || getPosLang();
    var p = String(pos).trim().toUpperCase();
    return (l === "es" ? POS_DESC_ES[p] : POS_DESC_EN[p]) || p;
  }

  function posLine(pos) {
    if (!pos) return "fw";
    var p = String(pos).trim().toUpperCase();
    var en = POS_ES_TO_EN[p] || p;
    if (en === "GK") return "gk";
    if (["CB", "LB", "RB"].indexOf(en) >= 0) return "df";
    if (["DMF", "CMF", "LMF", "RMF", "AMF"].indexOf(en) >= 0) return "mf";
    return "fw";
  }

  function posColor(pos) {
    var l = posLine(pos);
    return l === "gk" ? "#B8860B" : l === "df" ? "#2F6DB3" : l === "mf" ? "#2E8B57" : "#C0392B";
  }

  function posBadge(pos, big) {
    var lbl = posLabel(pos);
    var d = posDesc(pos);
    return '<span class="posb ' + posLine(pos) + (big ? " big" : "") + '" title="' + esc(d) + '">' + esc(lbl) + "</span>";
  }

  function renderPosToggle() {
    var cur = getPosLang();
    return '<div class="pos-lang-toggle" title="Formato de siglas de posiciones (Español PES 2021 / Internacional)">' +
      '<span class="plt-label">Siglas:</span>' +
      '<div class="plt-group">' +
      '<button type="button" class="plt-btn' + (cur === "es" ? " active" : "") + '" data-poslang="es" title="Español PES 2021 (PT, CT, LI, LD, MCD, MC, II, ID, MP, EI, ED, SD, DC)">🇪🇸 ES</button>' +
      '<button type="button" class="plt-btn' + (cur === "en" ? " active" : "") + '" data-poslang="en" title="Internacional (GK, CB, LB, RB, DMF, CMF, LMF, RMF, AMF, LWF, RWF, SS, CF)">🇬🇧 EN</button>' +
      '</div></div>';
  }

  function bindPosToggle(scopeEl, onToggle) {
    var root = scopeEl || document;
    var btns = root.querySelectorAll(".pos-lang-toggle .plt-btn");
    btns.forEach(function (btn) {
      btn.addEventListener("click", function (e) {
        e.preventDefault();
        e.stopPropagation();
        var l = btn.getAttribute("data-poslang");
        if (l && l !== getPosLang()) {
          setPosLang(l);
          if (typeof onToggle === "function") onToggle(l);
        }
      });
    });
  }
  function teamName(t) {
    if (!t) return "";
    if (typeof t === "string") return t;
    return t.name || t.nameSpanish || t.nameEnglish || (t.id ? "Equipo " + t.id : "");
  }

  function initials(name, sigla) {
    if (sigla) return sigla;
    var str = typeof name === "object" ? teamName(name) : String(name || "?");
    var w = str.trim().split(/\s+/);
    if (w.length === 1) return w[0].substring(0, 2).toUpperCase();
    return (w[0].charAt(0) + (w[1] ? w[1].charAt(0) : "")).toUpperCase();
  }
  function crestHtml(id, name, sigla, big) {
    var pr = crestPair(id);
    return '<div class="crest' + (big ? " big" : "") + '" style="--ca:' + pr[0] + ";--cb:" + pr[1] + '">' + esc(initials(name, sigla)) + "</div>";
  }

  // ---------- toast y modal ----------
  var toastTimer = null;
  function toast(msg, isErr) {
    var t = document.getElementById("toast");
    t.textContent = msg;
    t.className = "show" + (isErr ? " err" : "");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.className = ""; }, 4200);
  }
  function showModal(title, bodyHtml, footButtons) {
    document.getElementById("modal-title").textContent = title;
    document.getElementById("modal-body").innerHTML = bodyHtml;
    var foot = document.getElementById("modal-foot");
    foot.innerHTML = "";
    (footButtons || [{ label: "Cerrar" }]).forEach(function (b) {
      var btn = el('<button class="btn ' + (b.cls || "btn-ghost") + '">' + esc(b.label) + "</button>");
      btn.addEventListener("click", function () {
        if (b.onClick) b.onClick();
        if (!b.keepOpen) hideModal();
      });
      foot.appendChild(btn);
    });
    document.getElementById("modal-backdrop").classList.remove("hidden");
  }
  function hideModal() { document.getElementById("modal-backdrop").classList.add("hidden"); }
  document.getElementById("modal-close").addEventListener("click", hideModal);
  document.getElementById("modal-backdrop").addEventListener("click", function (e) { if (e.target === this) hideModal(); });

  function confirmModal(title, text, okLabel, onOk) {
    showModal(title, "<p>" + esc(text) + "</p>", [
      { label: "Cancelar" },
      { label: okLabel || "Confirmar", cls: "btn-primary", onClick: onOk }
    ]);
  }

  // ---------- galeria de logos oficiales del juego (dt15_x64.cpk) ----------
  function pickGameEmblem(onPick) {
    rpc("listGameEmblems").then(function (r) {
      if (!r.cpkFound) { toast("No se encontró dt15_x64.cpk subiendo desde la carpeta de tu base: la galería del juego no está disponible.", true); return; }
      if (!r.items || !r.items.length) { toast("La galería del juego está vacía.", true); return; }
      var html = '<p class="mut" style="margin-bottom:10px">Logos oficiales de PES 2021 (dt15_x64.cpk). Elegí uno para asignarlo a esta competición.</p>' +
        '<div class="emblem-grid">' + r.items.map(function (it, i) {
          return '<button class="emblem-cell" data-gi="' + i + '">' + (it.png ? '<img src="data:image/png;base64,' + it.png + '" alt="">' : '<span class="mut">sin imagen</span>') + '<span class="emblem-cell-name">' + esc(it.name || ("Competición " + it.compId)) + '</span><span class="sub">ID ' + it.compId + '</span></button>';
        }).join("") + "</div>";
      showModal("🖼️ Galería de Logos Oficiales del Juego", html, [{ label: "Cancelar" }]);
      $$(".emblem-cell").forEach(function (b) {
        b.addEventListener("click", function () { var it = r.items[Number(b.getAttribute("data-gi"))]; hideModal(); onPick(it); });
      });
    }).catch(function (e) { toast(e.message, true); });
  }

  // ---------- router ----------
  var renderers = {};
  function goto(section) {
    $$(".nav-item").forEach(function (b) { b.classList.toggle("active", b.getAttribute("data-section") === section); });
    $$(".page").forEach(function (p) { p.classList.toggle("active", p.id === "page-" + section); });
    document.getElementById("main").scrollTop = 0;
    var page = document.getElementById("page-" + section);
    if (renderers[section]) {
      renderers[section](page).catch(function (e) { /* los errores ya se muestran con toast */ });
    }
  }
  $$(".nav-item").forEach(function (b) {
    b.addEventListener("click", function () { goto(b.getAttribute("data-section")); });
  });

  // ---------- estado de la base (pie lateral) ----------
  function refreshDbStatus() {
    var dot = document.getElementById("dbDot");
    var title = document.getElementById("dbStatusTitle");
    var sub = document.getElementById("dbStatusSub");
    var btn = document.getElementById("btnSave");
    if (S.dbLoaded && S.stats) {
      dot.className = "dot dot-on";
      title.textContent = "Base cargada";
      sub.textContent = S.stats.competitions + " competiciones · " + S.stats.teams + " equipos · " + S.stats.players + " jugadores" + (S.stats.hasUnsavedChanges ? " · Cambios sin guardar" : "");
      btn.disabled = false;
      document.getElementById("countLigas").textContent = S.stats.competitions;
      document.getElementById("countEquipos").textContent = S.stats.teams;
      document.getElementById("countJugadores").textContent = Math.round(S.stats.players / 100) / 10 + "k";
    } else {
      dot.className = "dot dot-off";
      title.textContent = "Sin base cargada";
      sub.textContent = "Configurá tus rutas para empezar";
      btn.disabled = true;
    }
  }

  function applyState(st) {
    if (!st) return;
    if (st.config) S.config = st.config;
    if (typeof st.dbLoaded === "boolean") S.dbLoaded = st.dbLoaded;
    if (st.stats) S.stats = st.stats;
    if (st.dbPath) S.dbPath = st.dbPath;
    if (typeof st.editLoaded === "boolean") S.editLoaded = st.editLoaded;
    refreshDbStatus();
  }

  function reloadState() {
    return rpc("getState").then(function (st) { applyState(st); return st; });
  }

  function loadDb(folder) {
    return rpc("loadDb", { folder: folder || "" }).then(function (r) {
      S.dbLoaded = true; S.stats = r.stats; S.dbPath = r.dbPath;
      refreshDbStatus();
      if (r.warnings && r.warnings.length) {
        showModal("Avisos al cargar la base", '<div class="warn-box">' + r.warnings.map(esc).join("<br>") + "</div>", [{ label: "Entendido" }]);
      }
      return r;
    });
  }

  document.getElementById("btnSave").addEventListener("click", function () {
    var btn = this;
    btn.disabled = true;
    btn.innerHTML = '<span class="save-ico spin">⏳</span> <span class="btn-save-text">Guardando…</span>';
    rpc("saveDb").then(function (r) {
      toast("Guardado en " + (r.packageDir || r.folder) + (r.filesCopied ? " · " + r.filesCopied + " archivos sincronizados a livecpk" : "") + (r.luaSynced ? " · Lua actualizado" : ""));
      return reloadState();
    }).catch(function (e) { toast(e.message, true); })
      .then(function () {
        btn.disabled = false;
        btn.innerHTML = '<span class="save-ico">💾</span> <span class="btn-save-text">Guardar (.bin)</span>';
      });
  });

  function setupSidebar() {
    var sidebar = document.getElementById("sidebar");
    var toggleBtn = document.getElementById("sidebarToggle");
    if (!sidebar || !toggleBtn) return;

    function setCollapsed(c) {
      sidebar.classList.toggle("collapsed", c);
      toggleBtn.textContent = c ? "▶" : "◀";
      toggleBtn.title = c ? "Expandir barra lateral" : "Minimizar barra lateral";
      try { localStorage.setItem("pes_sidebar_collapsed", c ? "1" : "0"); } catch (e) {}
    }

    toggleBtn.addEventListener("click", function () {
      var isCol = sidebar.classList.contains("collapsed");
      setCollapsed(!isCol);
    });

    try {
      if (localStorage.getItem("pes_sidebar_collapsed") === "1") {
        setCollapsed(true);
      }
    } catch (e) {}
  }

  // ---------- arranque ----------
  function init() {
    setupSidebar();
    var bridge = document.getElementById("bridgeStatus");
    if (!(window.chrome && window.chrome.webview)) {
      bridge.textContent = "Abrí la app desde el ejecutable (no en un navegador).";
      return;
    }
    rpc("ping").then(function (r) {
      bridge.textContent = "● Núcleo C# conectado · " + r.shell;
      bridge.classList.add("ok");
      return reloadState();
    }).then(function (st) {
      // Carga automatica como la app actual: config completa + pesdb existe.
      if (st && !st.dbLoaded && st.config && st.config.isComplete && st.config.pesdbFolder) {
        return loadDb(st.config.pesdbFolder).catch(function () { /* queda para carga manual */ });
      }
    }).then(function () {
      if (renderers.inicio) renderers.inicio(document.getElementById("page-inicio"));
    }).catch(function (e) { bridge.textContent = "Error de conexión: " + e.message; });
  }

  window.App = {
    rpc: rpc, S: S, el: el, esc: esc, $: $, $$: $$,
    toast: toast, showModal: showModal, hideModal: hideModal, confirmModal: confirmModal,
    goto: goto, register: function (name, fn) { renderers[name] = fn; }, pickGameEmblem: pickGameEmblem,
    refreshDbStatus: refreshDbStatus, reloadState: reloadState, loadDb: loadDb,
    crestHtml: crestHtml, crestColor: crestColor, crestColor2: crestColor2,
    teamName: teamName,
    posBadge: posBadge, posColor: posColor, posLine: posLine, posLabel: posLabel, posDesc: posDesc,
    getPosLang: getPosLang, setPosLang: setPosLang, POS_LIST: POS_LIST,
    renderPosToggle: renderPosToggle, bindPosToggle: bindPosToggle
  };

  document.addEventListener("DOMContentLoaded", init);
})();
