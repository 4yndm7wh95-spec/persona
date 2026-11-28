/* P4R All-Out ribbon, seventh draft: designed curves (the original frame and the user's outline are only references).
   - Inner edge B0: a spiral, x/y integrated from a curvature that rises smoothly along the bottom arm, peaks in the curl and
     relaxes towards the arrow (one peak, never wiggles), fitted to the user's red outline.
   - Total width D(s): ln D integrated from a one-peak slope, so it only ever shrinks towards the far end; on screen it meets
     the user's outer edge.
   - Stripe boundaries: B0 + share·D along B0's outward normal; shares blend smoothly from the near proportions (left: wide
     yellow and dark grey) to the far ones (top: orange wider, grey only a dark line), then into the yellow shaft.
   Units: px of a 1280x720 frame, s = arc length of B0 (negative s: the run-up off the right edge). */
var RIB7 = (function () {
  var B0 = [499.724, 2.90064, 1442.57, 0.000192204, 0.00202459, 0.0211792, 1224.91, 57.7392, 0.00260097, 30.0051], X0 = 1340;
  var WD = [6.70727, 3.66693e-05, 0.00211809, 0.0360441, 1223.2, 25.5935, 0.012479, 18.0058];
  /* cumulative shares of D: orange | lime | inner yellow | grey (with dark outlines) | outer yellow | outer orange | outermost yellow */
  var NEAR = [0.11, 0.23, 0.50, 0.74, 0.83, 0.91, 1.0], FAR = [0.27, 0.42, 0.60, 0.67, 0.78, 0.90, 1.0], END = [0, 0, 0.62, 0.62, 0.8, 0.8, 1.0];
  var C = { Y: "#f8f40e", O: "#feb30c", L: "#aef01a", G: "#333037", K: "#151417" };
  var COLS = [C.O, C.L, C.Y, C.G, C.Y, C.O, C.Y];
  var EXT = 420, SN = 1365, BARB = [343, 338];
  function uni(a0, a1, kp, sp, wr, kt, wf, s) {
    if (s <= sp) { var q = Math.max(0, Math.min(1, s / sp)); return a0 + (a1 - a0) * q * q + (kp - a1) * Math.exp(-0.5 * Math.pow((s - sp) / wr, 2)); }
    return kt + (kp - kt) * Math.exp(-0.5 * Math.pow((s - sp) / wf, 2));
  }
  function sstep(a, b, x) { var t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * t * (t * (6 * t - 15) + 10); }
  function build() {
    var L = B0[2], n = Math.round(L) + EXT + 1, S = [], X = [], Y = [], TH = [], i;
    var kf = function (s) { return uni(B0[3], B0[4], B0[5], B0[6], B0[7], B0[8], B0[9], s); };
    var ds = L / Math.round(L);
    /* forward from s = 0 (the fitted start, just off the right edge) ... */
    var th = B0[1], x = X0, y = B0[0], fx = [x], fy = [y], fth = [th];
    for (i = 1; i <= Math.round(L); i++) { var s0 = (i - 1) * ds, k0 = kf(s0), k1 = kf(s0 + ds), th1 = th + (k0 + k1) / 2 * ds; x += (Math.cos(th) + Math.cos(th1)) / 2 * ds; y += (Math.sin(th) + Math.sin(th1)) / 2 * ds; th = th1; fx.push(x); fy.push(y); fth.push(th); }
    /* ... and backwards for the run-up (EXT px further off the right edge) */
    th = B0[1]; x = X0; y = B0[0]; var bx = [], by = [], bth = [];
    for (i = 1; i <= EXT; i++) { var th0 = th - kf(-i * ds) * ds; x -= (Math.cos(th) + Math.cos(th0)) / 2 * ds; y -= (Math.sin(th) + Math.sin(th0)) / 2 * ds; th = th0; bx.push(x); by.push(y); bth.push(th); }
    for (i = EXT - 1; i >= 0; i--) { S.push(-(i + 1) * ds); X.push(bx[i]); Y.push(by[i]); TH.push(bth[i]); }
    for (i = 0; i < fx.length; i++) { S.push(i * ds); X.push(fx[i]); Y.push(fy[i]); TH.push(fth[i]); }
    var N = S.length, NX = [], NY = [], D = [], E = [[], [], [], [], [], [], []], lnD = WD[0], gf = function (s) { return uni(WD[1], WD[2], WD[3], WD[4], WD[5], WD[6], WD[7], s); };
    /* ln D: integrate from s = 0 both ways */
    var LD = new Array(N); LD[EXT] = lnD;
    for (i = EXT + 1; i < N; i++) LD[i] = LD[i - 1] - (gf(S[i - 1]) + gf(S[i])) / 2 * ds;
    for (i = EXT - 1; i >= 0; i--) LD[i] = LD[i + 1] + (gf(S[i + 1]) + gf(S[i])) / 2 * ds;
    for (i = 0; i < N; i++) {
      NX.push(Math.sin(TH[i])); NY.push(-Math.cos(TH[i])); D.push(Math.exp(LD[i]));
      var b1 = sstep(1150, 1290, S[i]), b2 = sstep(1270, 1360, S[i]);
      for (var k = 0; k < 7; k++) { var f = NEAR[k] * (1 - b1) + FAR[k] * b1; f = f * (1 - b2) + END[k] * b2; E[k].push(f * D[i]); }
    }
    /* "visual" arc length: along the curve a third of the way across the ribbon (the curl counts for what it sweeps) */
    var VL = [0];
    for (i = 1; i < N; i++) { var ax = X[i - 1] + NX[i - 1] * D[i - 1] * 0.3, ay = Y[i - 1] + NY[i - 1] * D[i - 1] * 0.3, cx = X[i] + NX[i] * D[i] * 0.3, cy = Y[i] + NY[i] * D[i] * 0.3; VL.push(VL[i - 1] + Math.hypot(cx - ax, cy - ay)); }
    var R = { S: S, X: X, Y: Y, TH: TH, NX: NX, NY: NY, D: D, E: E, VL: VL, N: N, ds: ds, EXT: EXT, L: L, SN: SN, BARB: BARB, C: C, COLS: COLS };
    /* the barbed arrowhead: notch on the ribbon's outer edge at SN, barb tip, tip (the end of B0), back along B0 */
    var iN = idx(R, SN), notch = [X[iN] + NX[iN] * E[6][iN], Y[iN] + NY[iN] * E[6][iN]], AH = [notch, BARB.slice()];
    for (i = N - 1; i >= iN; i--) AH.push([X[i], Y[i]]);
    R.AH = AH; R.notch = notch;
    return R;
  }
  function idx(R, s) { return Math.max(0, Math.min(R.N - 1, Math.round(s / R.ds) + R.EXT)); }
  function fidx(R, s) { return Math.max(0, Math.min(R.N - 1, s / R.ds + R.EXT)); }
  /* s at a given visual length */
  function sAtV(R, v) {
    var V = R.VL, lo = 0, hi = R.N - 1; if (v <= 0) return R.S[0]; if (v >= V[hi]) return R.S[hi];
    while (hi - lo > 1) { var m = (lo + hi) >> 1; if (V[m] <= v) lo = m; else hi = m; }
    return R.S[lo] + (R.S[hi] - R.S[lo]) * (v - V[lo]) / ((V[hi] - V[lo]) || 1);
  }
  function vAtS(R, s) { var u = fidx(R, s), i = Math.min(R.N - 2, Math.floor(u)); return R.VL[i] + (R.VL[i + 1] - R.VL[i]) * (u - i); }
  /* point on B0 at s, offset o along the normal */
  function pt(R, s, o) { var u = fidx(R, s), i = Math.min(R.N - 2, Math.floor(u)), f = u - i, x = R.X[i] + (R.X[i + 1] - R.X[i]) * f, y = R.Y[i] + (R.Y[i + 1] - R.Y[i]) * f, nx = R.NX[i] + (R.NX[i + 1] - R.NX[i]) * f, ny = R.NY[i] + (R.NY[i + 1] - R.NY[i]) * f; return [x + nx * o, y + ny * o, nx, ny]; }
  function lerpA(A, R, s) { var u = fidx(R, s), i = Math.min(R.N - 2, Math.floor(u)), f = u - i; return A[i] + (A[i + 1] - A[i]) * f; }
  /* one stripe between offset arrays A (inner) and B (outer), from s0 (tail) to s1 (head), round ends */
  function strip(ctx, R, A, B, s0, s1, capT, capH, inset) {
    inset = inset || 0; if (s1 - s0 < 0.5) return false;
    var i0 = Math.ceil(fidx(R, s0)), i1 = Math.floor(fidx(R, s1)), a0 = lerpA(A, R, s0) + inset, b0 = lerpA(B, R, s0) - inset, a1 = lerpA(A, R, s1) + inset, b1 = lerpA(B, R, s1) - inset;
    if (Math.max(b0 - a0, b1 - a1) < 0.3 && i1 - i0 < 2) return false;
    var p = pt(R, s0, a0); ctx.beginPath(); ctx.moveTo(p[0], p[1]);
    for (var i = i0; i <= i1; i++) { var ai = A[i] + inset, bi = B[i] - inset; if (bi < ai) ai = bi = (A[i] + B[i]) / 2; ctx.lineTo(R.X[i] + R.NX[i] * ai, R.Y[i] + R.NY[i] * ai); }
    var h = pt(R, s1, a1); ctx.lineTo(h[0], h[1]);
    var rH = Math.max(0, (b1 - a1) / 2), cH = pt(R, s1, (a1 + b1) / 2), th = Math.atan2(h[3], h[2]);
    if (capH && rH > 0.3) ctx.arc(cH[0], cH[1], rH, th + Math.PI, th, true); else { var ho = pt(R, s1, b1); ctx.lineTo(ho[0], ho[1]); }
    for (var j = i1; j >= i0; j--) { var aj = A[j] + inset, bj = B[j] - inset; if (bj < aj) aj = bj = (A[j] + B[j]) / 2; ctx.lineTo(R.X[j] + R.NX[j] * bj, R.Y[j] + R.NY[j] * bj); }
    var t1 = pt(R, s0, b0); ctx.lineTo(t1[0], t1[1]);
    var rT = Math.max(0, (b0 - a0) / 2), cT = pt(R, s0, (a0 + b0) / 2), tt = Math.atan2(t1[3], t1[2]);
    if (capT && rT > 0.3) ctx.arc(cT[0], cT[1], rT, tt, tt + Math.PI, true);
    ctx.closePath(); return true;
  }
  var ZERO = null;
  /* o.tail[k], o.head[k]: per stripe s range; o.capTail: draw the tail's round end (false while it is off screen) */
  function draw(ctx, R, o) {
    if (!ZERO) { ZERO = []; for (var z = 0; z < R.N; z++) ZERO.push(0); }
    var ol = function (k) { return 2.6; };
    for (var k = 6; k >= 0; k--) {
      var A = k ? R.E[k - 1] : ZERO, B = R.E[k], s0 = o.tail[k], s1 = Math.min(o.head[k], R.SN);
      if (s1 <= s0) continue;
      if (k === 3) {
        if (strip(ctx, R, A, B, s0, s1, o.capTail, true)) { ctx.fillStyle = C.K; ctx.fill(); }
        if (strip(ctx, R, A, B, s0, s1, o.capTail, true, 2.4)) { ctx.fillStyle = C.G; ctx.fill(); }
      } else if (strip(ctx, R, A, B, s0, s1, o.capTail, true)) { ctx.fillStyle = COLS[k]; ctx.fill(); }
    }
  }

  /* one stripe with slanted straight ends: the inner boundary A runs over [ta, ha], the outer boundary B over [tb, hb] */
  function cutStrip(ctx, R, A, B, ta, ha, tb, hb, inset) {
    inset = inset || 0; if (ha - ta < 0.5 && hb - tb < 0.5) return false;
    var i0 = Math.ceil(fidx(R, ta)), i1 = Math.floor(fidx(R, ha)), j0 = Math.ceil(fidx(R, tb)), j1 = Math.floor(fidx(R, hb));
    function oa(i) { var a = A[i] + inset, b = B[i] - inset; return b < a ? (A[i] + B[i]) / 2 : a; }
    function ob(i) { var a = A[i] + inset, b = B[i] - inset; return b < a ? (A[i] + B[i]) / 2 : b; }
    var p = pt(R, ta, lerpA(A, R, ta) + inset); ctx.beginPath(); ctx.moveTo(p[0], p[1]);
    for (var i = i0; i <= i1; i++) ctx.lineTo(R.X[i] + R.NX[i] * oa(i), R.Y[i] + R.NY[i] * oa(i));
    var h = pt(R, ha, lerpA(A, R, ha) + inset); ctx.lineTo(h[0], h[1]);
    var h2 = pt(R, hb, lerpA(B, R, hb) - inset); ctx.lineTo(h2[0], h2[1]);
    for (var j = j1; j >= j0; j--) ctx.lineTo(R.X[j] + R.NX[j] * ob(j), R.Y[j] + R.NY[j] * ob(j));
    var t2 = pt(R, tb, lerpA(B, R, tb) - inset); ctx.lineTo(t2[0], t2[1]);
    ctx.closePath(); return true;
  }
  /* the ribbon between a slanted tail cut and a slanted head cut.
     o.sH: leading point on B0; o.lh: head slant (px of B0 per px across); o.st: extra step back per stripe (px)
     o.sT: tail point on B0 (inner side leads); o.lt: tail slant; o.end: last s the stripes reach (SN once landed) */
  function drawCut(ctx, R, o) {
    if (!ZERO) { ZERO = []; for (var z = 0; z < R.N; z++) ZERO.push(0); }
    var end = o.end === undefined ? R.SN : o.end;
    for (var k = 6; k >= 0; k--) {
      var A = k ? R.E[k - 1] : ZERO, B = R.E[k];
      var aH = lerpA(A, R, Math.min(o.sH, end)), bH = lerpA(B, R, Math.min(o.sH, end)), aT = lerpA(A, R, o.sT), bT = lerpA(B, R, o.sT);
      var ha = Math.min(end, o.sH - o.lh * aH - k * o.st), hb = Math.min(end, o.sH - o.lh * bH - k * o.st);
      var ta = Math.max(R.S[0], o.sT - o.lt * aT - k * o.st * 0.5), tb = Math.max(R.S[0], o.sT - o.lt * bT - k * o.st * 0.5);
      if (ha <= ta && hb <= tb) continue;
      ha = Math.max(ha, ta); hb = Math.max(hb, tb);
      if (k === 3) {
        if (cutStrip(ctx, R, A, B, ta, ha, tb, hb)) { ctx.fillStyle = C.K; ctx.fill(); }
        if (cutStrip(ctx, R, A, B, ta, ha, tb, hb, 2.4)) { ctx.fillStyle = C.G; ctx.fill(); }
      } else if (cutStrip(ctx, R, A, B, ta, ha, tb, hb)) { ctx.fillStyle = COLS[k]; ctx.fill(); }
    }
  }

  /* the ribbon between a tapered tail and a tapered head: every boundary is pulled in towards B0 near the ends, so both ends
     are points (a blade in front, a thin brush tail behind) instead of cut-offs.
     o.sT, o.sH: tail / head point on B0; o.LT, o.LH: taper lengths (px of B0, 0 = no taper); o.end: last s the bands reach */
  function taperW(o, s) {
    var w = 1;
    if (o.LH > 0.5 && s > o.sH - o.LH) { var x = Math.max(0, (o.sH - s) / o.LH); w = Math.min(w, 1 - Math.pow(1 - x, 2.2)); }
    if (o.LT > 0.5 && s < o.sT + o.LT) { var y = Math.max(0, (s - o.sT) / o.LT); w = Math.min(w, Math.pow(y, 1.5)); }
    return w;
  }
  function drawTaper(ctx, R, o) {
    if (!ZERO) { ZERO = []; for (var z = 0; z < R.N; z++) ZERO.push(0); }
    var end = o.end === undefined ? R.SN : o.end, s0 = Math.max(R.S[0], o.sT), s1 = Math.min(end, o.sH);
    if (s1 - s0 < 1) return;
    var i0 = Math.ceil(fidx(R, s0)), i1 = Math.floor(fidx(R, s1)), W = [];
    for (var i = i0; i <= i1; i++) W.push(taperW(o, R.S[i]));
    function band(A, B, inset, col) {
      ctx.beginPath(); var first = true;
      for (var i = i0; i <= i1; i++) { var w = W[i - i0], a = A[i] * w, b = B[i] * w, ins = Math.min(inset, (b - a) / 2), oa = a + ins; if (first) { ctx.moveTo(R.X[i] + R.NX[i] * oa, R.Y[i] + R.NY[i] * oa); first = false; } else ctx.lineTo(R.X[i] + R.NX[i] * oa, R.Y[i] + R.NY[i] * oa); }
      for (var j = i1; j >= i0; j--) { var w2 = W[j - i0], a2 = A[j] * w2, b2 = B[j] * w2, ins2 = Math.min(inset, (b2 - a2) / 2), ob = b2 - ins2; ctx.lineTo(R.X[j] + R.NX[j] * ob, R.Y[j] + R.NY[j] * ob); }
      ctx.closePath(); ctx.fillStyle = col; ctx.fill();
    }
    for (var k = 6; k >= 0; k--) {
      var A = k ? R.E[k - 1] : ZERO, B = R.E[k];
      if (k === 3) { band(A, B, 0, C.K); band(A, B, 2.4, C.G); } else band(A, B, 0, COLS[k]);
    }
  }
  /* a thin line along B0: centre offset cf(s, D), width wf(s, D), round ends */
  function line(ctx, R, s0, s1, cf, wf, col) {
    if (s1 - s0 < 1) return;
    var n = Math.max(8, Math.ceil((s1 - s0) / 3)), P = [], Q = [];
    for (var i = 0; i <= n; i++) { var s = s0 + (s1 - s0) * i / n, D = lerpA(R.D, R, s), c = cf(s, D), w = wf(s, D) / 2, a = pt(R, s, c - w), b = pt(R, s, c + w); P.push(a); Q.push(b); }
    ctx.beginPath(); ctx.moveTo(P[0][0], P[0][1]); for (var j = 1; j <= n; j++) ctx.lineTo(P[j][0], P[j][1]);
    var e1 = pt(R, s1, cf(s1, lerpA(R.D, R, s1))), r1 = wf(s1, lerpA(R.D, R, s1)) / 2, th1 = Math.atan2(e1[3], e1[2]);
    ctx.arc(e1[0], e1[1], r1, th1 + Math.PI, th1, true);
    for (var q = n; q >= 0; q--) ctx.lineTo(Q[q][0], Q[q][1]);
    var e0 = pt(R, s0, cf(s0, lerpA(R.D, R, s0))), r0 = wf(s0, lerpA(R.D, R, s0)) / 2, th0 = Math.atan2(e0[3], e0[2]);
    ctx.arc(e0[0], e0[1], r0, th0, th0 + Math.PI, true);
    ctx.closePath(); ctx.fillStyle = col; ctx.fill();
  }
  return { build: build, draw: draw, drawCut: drawCut, drawTaper: drawTaper, taperW: taperW, idx: idx, fidx: fidx, line: line, strip: strip, pt: pt, sAtV: sAtV, vAtS: vAtS, lerpA: lerpA, sstep: sstep, C: C };
})();
