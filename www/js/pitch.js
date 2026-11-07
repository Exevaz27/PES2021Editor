// ============================================================
// Generador y caché de texturas fotorrealistas de cancha de fútbol
// Proporciones reglamentarias (68 x 105 m), césped procedural,
// degradado de corte de podadora, desgaste de áreas y reflectores.
// ============================================================
(function () {
  "use strict";

  var PITCH_CACHE = new Map();
  var PITCH_THEMES = {
    clasico: { g1: '#2f8648', g2: '#2a7a41', line: 'rgba(255,255,255,.88)', light: .16, vig: .42, noise: 18 },
    noche: { g1: '#1d5233', g2: '#194a2d', line: 'rgba(235,248,255,.85)', light: .30, vig: .62, noise: 14, pools: true },
    atardecer: { g1: '#4f8a39', g2: '#477f34', line: 'rgba(255,246,225,.9)', light: .22, vig: .45, noise: 18, warm: true },
    pizarra: { g1: '#22414f', g2: '#22414f', line: 'rgba(235,242,245,.78)', light: .08, vig: .35, noise: 26, chalk: true },
    retro: { g1: '#7d8b4f', g2: '#748249', line: 'rgba(250,244,222,.85)', light: .1, vig: .5, noise: 30, sepia: true },
    equipo: { g1: null, g2: null, line: 'rgba(255,255,255,.85)', light: .16, vig: .5, noise: 16 }
  };

  function mixHex(hex, other, t) {
    var p = function (h) {
      if (!h || h[0] !== "#" || h.length < 7) return [40, 100, 50];
      return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
    };
    var a = p(hex), b = p(other);
    return '#' + a.map(function (v, i) {
      return Math.round(v + (b[i] - v) * t).toString(16).padStart(2, '0');
    }).join('');
  }

  function pitchTexture(theme, k1, k2) {
    theme = theme || "clasico";
    k1 = k1 || "#2f8648";
    k2 = k2 || "#2a7a41";
    var key = theme + (theme === 'equipo' ? k1 + k2 : '');
    if (PITCH_CACHE.has(key)) return PITCH_CACHE.get(key);

    var T = Object.assign({}, PITCH_THEMES[theme] || PITCH_THEMES.clasico);
    if (theme === 'equipo') {
      T.g1 = mixHex(k1, '#103a22', .55);
      T.g2 = mixHex(k1, '#0c2e1b', .62);
    }
    var W = 680, H = 960;
    var c = document.createElement('canvas');
    c.width = W; c.height = H;
    var g = c.getContext('2d');

    // 1. Franjas de corte con degradado suave
    var bands = 14, bh = H / bands;
    for (var i = 0; i < bands; i++) {
      g.fillStyle = i % 2 ? T.g2 : T.g1;
      g.fillRect(0, i * bh, W, bh + 1);
    }
    if (!T.chalk) {
      for (var j = 1; j < bands; j++) {
        var gr = g.createLinearGradient(0, j * bh - 6, 0, j * bh + 6);
        gr.addColorStop(0, 'rgba(0,0,0,0)');
        gr.addColorStop(.5, 'rgba(0,0,0,.05)');
        gr.addColorStop(1, 'rgba(0,0,0,0)');
        g.fillStyle = gr;
        g.fillRect(0, j * bh - 6, W, 12);
      }
    }

    // 2. Grano de césped con ruido procedural y briznas
    var id = g.getImageData(0, 0, W, H), dd = id.data;
    var seed = 7;
    var rnd = function () {
      seed = (seed * 16807) % 2147483647;
      return seed / 2147483647;
    };
    for (var k = 0; k < dd.length; k += 4) {
      var n = (rnd() - .5) * T.noise;
      dd[k] += n;
      dd[k + 1] += n * 1.1;
      dd[k + 2] += n * .8;
    }
    g.putImageData(id, 0, 0);

    if (!T.chalk) {
      g.lineWidth = 1;
      for (var b = 0; b < 9000; b++) {
        var rx = rnd() * W, ry = rnd() * H, rl = 2 + rnd() * 4;
        g.strokeStyle = rnd() > .5 ? 'rgba(255,255,255,.035)' : 'rgba(0,0,0,.05)';
        g.beginPath();
        g.moveTo(rx, ry);
        g.lineTo(rx + (rnd() - .5) * 2, ry - rl);
        g.stroke();
      }
    }

    // 3. Desgaste en áreas chicas y círculo central
    var spots = [[W / 2, H * .955, 70], [W / 2, H * .045, 70], [W / 2, H / 2, 60]];
    for (var s = 0; s < spots.length; s++) {
      var sp = spots[s];
      var sgr = g.createRadialGradient(sp[0], sp[1], 4, sp[0], sp[1], sp[2]);
      sgr.addColorStop(0, T.chalk ? 'rgba(255,255,255,.04)' : 'rgba(120,100,60,.22)');
      sgr.addColorStop(1, 'rgba(0,0,0,0)');
      g.fillStyle = sgr;
      g.fillRect(sp[0] - sp[2], sp[1] - sp[2], sp[2] * 2, sp[2] * 2);
    }

    // 4. Marcaciones oficiales reglamentarias (escala real 68 x 105 m)
    var sx = W / 68, sy = H / 105, m = 1.5 * sx;
    g.strokeStyle = T.line;
    g.lineWidth = 3;
    g.shadowColor = 'rgba(255,255,255,.35)';
    g.shadowBlur = 2;
    g.lineCap = 'round';
    var L = function (x1, y1, x2, y2) {
      g.beginPath(); g.moveTo(x1, y1); g.lineTo(x2, y2); g.stroke();
    };

    g.strokeRect(m, m, W - 2 * m, H - 2 * m);
    L(m, H / 2, W - m, H / 2);
    g.beginPath(); g.arc(W / 2, H / 2, 9.15 * sx, 0, Math.PI * 2); g.stroke();
    g.fillStyle = T.line; g.beginPath(); g.arc(W / 2, H / 2, 4, 0, Math.PI * 2); g.fill();

    // Áreas de ambos lados
    [true, false].forEach(function (top) {
      var y0 = top ? m : H - m, dir = top ? 1 : -1;
      var pw = 40.32 * sx, pd = 16.5 * sy, gw = 18.32 * sx, gd = 5.5 * sy;
      g.strokeRect(W / 2 - pw / 2, top ? y0 : y0 - pd, pw, pd);
      g.strokeRect(W / 2 - gw / 2, top ? y0 : y0 - gd, gw, gd);
      var spot = y0 + dir * 11 * sy;
      g.beginPath(); g.arc(W / 2, spot, 4, 0, Math.PI * 2); g.fill();
      var a = Math.acos((16.5 - 11) / 9.15);
      g.beginPath();
      top ? g.arc(W / 2, spot, 9.15 * sx, a, Math.PI - a) : g.arc(W / 2, spot, 9.15 * sx, Math.PI + a, -a);
      g.stroke();

      for (var cidx = 0; cidx < 2; cidx++) {
        var cx = cidx === 0 ? m : W - m;
        g.beginPath();
        g.arc(cx, y0, 1.2 * sx, top ? (cx === m ? 0 : Math.PI / 2) : (cx === m ? -Math.PI / 2 : Math.PI), top ? (cx === m ? Math.PI / 2 : Math.PI) : (cx === m ? 0 : -Math.PI / 2));
        g.stroke();
      }

      // Redes de los arcos
      var gwid = 7.32 * sx, gdep = 2.2 * sy, gy = top ? y0 - gdep : y0;
      g.save();
      g.shadowBlur = 0;
      g.fillStyle = 'rgba(255,255,255,.12)';
      g.fillRect(W / 2 - gwid / 2, gy, gwid, gdep);
      g.strokeStyle = 'rgba(255,255,255,.25)';
      g.lineWidth = 1;
      for (var lx = W / 2 - gwid / 2; lx <= W / 2 + gwid / 2; lx += 6) L(lx, gy, lx, gy + gdep);
      for (var ly = gy; ly <= gy + gdep; ly += 6) L(W / 2 - gwid / 2, ly, W / 2 + gwid / 2, ly);
      g.strokeStyle = '#fff';
      g.lineWidth = 4;
      g.strokeRect(W / 2 - gwid / 2, gy, gwid, gdep);
      g.restore();
    });

    g.shadowBlur = 0;

    // 5. Iluminación de estadio, focos y viñeta
    var lt = g.createLinearGradient(0, 0, 0, H);
    lt.addColorStop(0, 'rgba(255,255,255,' + T.light + ')');
    lt.addColorStop(.5, 'rgba(255,255,255,0)');
    lt.addColorStop(1, 'rgba(255,255,255,' + (T.light * .6) + ')');
    g.fillStyle = lt;
    g.fillRect(0, 0, W, H);

    if (T.pools) {
      [[0, 0], [W, 0], [0, H], [W, H]].forEach(function (corner) {
        var grp = g.createRadialGradient(corner[0], corner[1], 10, corner[0], corner[1], W * .8);
        grp.addColorStop(0, 'rgba(220,240,255,.18)');
        grp.addColorStop(1, 'rgba(0,0,0,0)');
        g.fillStyle = grp;
        g.fillRect(0, 0, W, H);
      });
    }

    if (T.warm) {
      var wgr = g.createLinearGradient(0, 0, W, H);
      wgr.addColorStop(0, 'rgba(255,150,60,.20)');
      wgr.addColorStop(1, 'rgba(120,60,255,.12)');
      g.fillStyle = wgr;
      g.fillRect(0, 0, W, H);
    }
    if (T.sepia) {
      g.fillStyle = 'rgba(120,90,40,.12)';
      g.fillRect(0, 0, W, H);
    }

    var vg = g.createRadialGradient(W / 2, H / 2, H * .35, W / 2, H / 2, H * .75);
    vg.addColorStop(0, 'rgba(0,0,0,0)');
    vg.addColorStop(1, 'rgba(0,0,0,' + T.vig + ')');
    g.fillStyle = vg;
    g.fillRect(0, 0, W, H);

    var url = c.toDataURL('image/jpeg', .88);
    PITCH_CACHE.set(key, url);
    return url;
  }

  // Camisetas vectoriales SVG
  function shirtSvg(kit, size) {
    kit = kit || {};
    var c1 = kit.c1 || "#2F6DB3", c2 = kit.c2 || "#ffffff";
    var pBody = "M11 4 L6 6 L1.5 12.5 L6 15.5 L8 13 V29 H24 V13 L26 15.5 L30.5 12.5 L26 6 L21 4 Q16 8 11 4 Z";
    var pSl = "M6 6 L1.5 12.5 L6 15.5 L8.2 12.6 L9 7.5 Z";
    var pSr = "M26 6 L30.5 12.5 L26 15.5 L23.8 12.6 L23 7.5 Z";
    var pCol = "M11 4 Q16 8 21 4";

    return '<svg class="kit-svg" viewBox="0 0 32 32" width="' + (size || "52px") + '" height="' + (size || "52px") + '" role="img">' +
      '<path d="' + pBody + '" fill="' + c1 + '"/>' +
      '<path d="' + pSl + '" fill="' + c2 + '"/>' +
      '<path d="' + pSr + '" fill="' + c2 + '"/>' +
      '<path d="' + pCol + '" fill="none" stroke="' + c2 + '" stroke-width="2"/>' +
      '<path d="' + pBody + '" fill="none" stroke="rgba(0,0,0,0.4)" stroke-width="1" stroke-linejoin="round"/>' +
      '</svg>';
  }

  window.App.pitchTexture = pitchTexture;
  window.App.shirtSvg = shirtSvg;
  window.App.PITCH_THEMES = PITCH_THEMES;
})();
