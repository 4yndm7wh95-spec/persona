/* P4R All-Out ribbon, traced from the original (S02_0743) and smoothed.
   Skeleton: the ribbon's inner edge B0, from its near end (off the right edge, along the bottom) round the curl to the arrow tip.
   Every stripe boundary is an offset along B0's outward normal; offsets (×H) are keyframed along B0's normalised arc length s
   and interpolated with monotone cubics, so widths never overshoot or wobble. */
var RB_B0 = [[1.06, 0.715], [0.925, 0.744], [0.769, 0.796], [0.691, 0.812], [0.613, 0.824], [0.534, 0.829], [0.456, 0.822], [0.378, 0.803], [0.3, 0.763],
  [0.255, 0.735], [0.230, 0.720], [0.212, 0.706], [0.194, 0.682], [0.175, 0.650], [0.165, 0.615], [0.163, 0.585], [0.169, 0.552], [0.180, 0.533],
  [0.194, 0.521], [0.207, 0.514], [0.220, 0.510], [0.236, 0.507], [0.252, 0.506], [0.272, 0.507], [0.292, 0.513], [0.312, 0.521], [0.333, 0.531]];
/* [s, orange outer, lime outer, inner-yellow outer, grey width (incl. its dark outlines), outer yellow, outer orange, outermost yellow] */
var RB_K = [
  [0.000, 0.130, 0.350, 0.640, 0.32, 0.06, 0.08, 0.08], [0.067, 0.126, 0.335, 0.615, 0.31, 0.06, 0.08, 0.08], [0.234, 0.119, 0.300, 0.560, 0.29, 0.06, 0.075, 0.075],
  [0.402, 0.097, 0.250, 0.490, 0.27, 0.055, 0.07, 0.07], [0.536, 0.079, 0.210, 0.430, 0.25, 0.05, 0.065, 0.065], [0.611, 0.074, 0.194, 0.400, 0.235, 0.048, 0.062, 0.062],
  [0.674, 0.068, 0.176, 0.370, 0.22, 0.045, 0.06, 0.06], [0.711, 0.066, 0.162, 0.350, 0.205, 0.043, 0.058, 0.058], [0.736, 0.059, 0.147, 0.330, 0.19, 0.04, 0.056, 0.056],
  [0.762, 0.052, 0.129, 0.301, 0.175, 0.037, 0.054, 0.054], [0.774, 0.051, 0.122, 0.284, 0.165, 0.035, 0.052, 0.052], [0.787, 0.049, 0.118, 0.268, 0.155, 0.033, 0.05, 0.05],
  [0.799, 0.049, 0.109, 0.232, 0.123, 0.03, 0.048, 0.046], [0.812, 0.048, 0.100, 0.205, 0.098, 0.027, 0.045, 0.043], [0.824, 0.045, 0.089, 0.170, 0.072, 0.025, 0.042, 0.04],
  [0.837, 0.040, 0.073, 0.118, 0.040, 0.022, 0.038, 0.038], [0.849, 0.028, 0.048, 0.084, 0.008, 0.0095, 0.018, 0.020], [0.862, 0.020, 0.030, 0.047, 0.008, 0.0075, 0.009, 0.0065],
  [0.874, 0.012, 0.019, 0.034, 0.008, 0.0, 0.009, 0.0], [0.887, 0.009, 0.014, 0.026, 0.0, 0.0, 0.0, 0.0], [0.900, 0.006, 0.009, 0.026, 0, 0, 0, 0],
  [0.912, 0.002, 0.003, 0.024, 0, 0, 0, 0], [0.937, 0, 0, 0.016, 0, 0, 0, 0], [0.971, 0, 0, 0.011, 0, 0, 0, 0], [1.000, 0, 0, 0.004, 0, 0, 0, 0]];
/* the separate thin yellow line hanging above the bottom arm: [s, centre offset, width] */
var RB_T1 = [[0.0, -0.047, 0.012], [0.1, -0.043, 0.011], [0.2, -0.0395, 0.0105], [0.3, -0.037, 0.0098], [0.4, -0.0335, 0.0095], [0.5, -0.031, 0.0095], [0.58, -0.026, 0.0095],
  [0.64, -0.0185, 0.0088], [0.69, -0.0115, 0.0078], [0.725, -0.006, 0.006], [0.745, -0.002, 0.0]];
/* the thin yellow line inside the orange round the curl */
var RB_T2 = [[0.585, 0.016, 0.0], [0.605, 0.017, 0.011], [0.64, 0.0195, 0.012], [0.665, 0.022, 0.011], [0.69, 0.0215, 0.012], [0.715, 0.019, 0.012], [0.74, 0.0145, 0.0115],
  [0.765, 0.0105, 0.011], [0.79, 0.0098, 0.0095], [0.812, 0.0095, 0.007], [0.83, 0.0098, 0.0035], [0.842, 0.0098, 0.0]];

function rbMono(xs, ys) { /* Fritsch–Carlson monotone cubic: returns f(x) */
  var n = xs.length, d = [], m = [];
  for (var i = 0; i < n - 1; i++) d.push((ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]));
  m.push(d[0]); for (var j = 1; j < n - 1; j++) m.push(d[j - 1] * d[j] <= 0 ? 0 : (d[j - 1] + d[j]) / 2); m.push(d[n - 2]);
  for (var k = 0; k < n - 1; k++) { if (d[k] === 0) { m[k] = 0; m[k + 1] = 0; continue; } var a = m[k] / d[k], b = m[k + 1] / d[k], h = a * a + b * b; if (h > 9) { var t = 3 / Math.sqrt(h); m[k] = t * a * d[k]; m[k + 1] = t * b * d[k]; } }
  return function (x) {
    if (x <= xs[0]) return ys[0]; if (x >= xs[n - 1]) return ys[n - 1];
    var lo = 0, hi = n - 1; while (hi - lo > 1) { var md = (lo + hi) >> 1; if (xs[md] <= x) lo = md; else hi = md; }
    var hh = xs[hi] - xs[lo], t2 = (x - xs[lo]) / hh, t3 = t2 * t2 * t2, tt = t2 * t2;
    return (2 * t3 - 3 * tt + 1) * ys[lo] + (t3 - 2 * tt + t2) * hh * m[lo] + (-2 * t3 + 3 * tt) * ys[hi] + (t3 - tt) * hh * m[hi];
  };
}
function rbBuild(W, H) {
  var P = RB_B0.map(function (p) { return [p[0] * W, p[1] * H]; });
  var Q = [[2 * P[0][0] - P[1][0], 2 * P[0][1] - P[1][1]]].concat(P, [[2 * P[P.length - 1][0] - P[P.length - 2][0], 2 * P[P.length - 1][1] - P[P.length - 2][1]]]);
  var S = [];
  function tj(ti, a, b) { return ti + Math.max(1e-6, Math.sqrt(Math.hypot(b[0] - a[0], b[1] - a[1]))); }
  for (var i = 1; i < Q.length - 2; i++) {
    var p0 = Q[i - 1], p1 = Q[i], p2 = Q[i + 1], p3 = Q[i + 2], t0 = 0, t1 = tj(t0, p0, p1), t2 = tj(t1, p1, p2), t3 = tj(t2, p2, p3);
    for (var k = 0; k < 40; k++) {
      var t = t1 + (t2 - t1) * k / 40, pt = [];
      for (var d = 0; d < 2; d++) {
        var A1 = (t1 - t) / (t1 - t0) * p0[d] + (t - t0) / (t1 - t0) * p1[d], A2 = (t2 - t) / (t2 - t1) * p1[d] + (t - t1) / (t2 - t1) * p2[d], A3 = (t3 - t) / (t3 - t2) * p2[d] + (t - t2) / (t3 - t2) * p3[d];
        var B1 = (t2 - t) / (t2 - t0) * A1 + (t - t0) / (t2 - t0) * A2, B2 = (t3 - t) / (t3 - t1) * A2 + (t - t1) / (t3 - t1) * A3;
        pt.push((t2 - t) / (t2 - t1) * B1 + (t - t1) / (t2 - t1) * B2);
      }
      S.push(pt);
    }
  }
  S.push(P[P.length - 1]);
  var L = [0]; for (var j = 1; j < S.length; j++) L.push(L[j - 1] + Math.hypot(S[j][0] - S[j - 1][0], S[j][1] - S[j - 1][1]));
  var total = L[L.length - 1], M = 720, X = [], Yv = [], NX = [], NY = [], lo = 0;
  for (var m = 0; m < M; m++) {
    var u = total * m / (M - 1); while (lo < L.length - 2 && L[lo + 1] < u) lo++;
    var f = (u - L[lo]) / ((L[lo + 1] - L[lo]) || 1); X.push(S[lo][0] + (S[lo + 1][0] - S[lo][0]) * f); Yv.push(S[lo][1] + (S[lo + 1][1] - S[lo][1]) * f);
  }
  for (var q = 0; q < M; q++) { var a2 = Math.max(0, q - 2), b2 = Math.min(M - 1, q + 2), tx = X[b2] - X[a2], ty = Yv[b2] - Yv[a2], tl = Math.hypot(tx, ty) || 1; NX.push(ty / tl); NY.push(-tx / tl); }
  var ks = RB_K.map(function (r) { return r[0]; }), F = [];
  for (var c = 1; c < 8; c++) F.push(rbMono(ks, RB_K.map(function (r) { return r[c]; })));
  function mk(tab) { var xs = tab.map(function (r) { return r[0]; }); return [rbMono(xs, tab.map(function (r) { return r[1]; })), rbMono(xs, tab.map(function (r) { return r[2]; })), xs[0], xs[xs.length - 1]]; }
  return { W: W, H: H, M: M, X: X, Y: Yv, NX: NX, NY: NY, F: F, T1: mk(RB_T1), T2: mk(RB_T2), total: total };
}
/* the boundary offsets (px) at s: e1 orange, e2 lime, e3 inner yellow, e4 grey (with outlines), e5 outer yellow, e6 outer orange, e7 outermost yellow */
function rbOffsets(R, s) {
  var H = R.H, f = R.F, d1 = f[0](s), d2 = Math.max(d1, f[1](s)), d3 = Math.max(d2, f[2](s)), e4 = d3 + Math.max(0, f[3](s)), e5 = e4 + Math.max(0, f[4](s)), e6 = e5 + Math.max(0, f[5](s)), e7 = e6 + Math.max(0, f[6](s));
  return [d1 * H, d2 * H, d3 * H, e4 * H, e5 * H, e6 * H, e7 * H];
}
function rbAt(R, s) { var u = Math.max(0, Math.min(1, s)) * (R.M - 1), i = Math.min(R.M - 2, Math.floor(u)), f = u - i; return [R.X[i] + (R.X[i + 1] - R.X[i]) * f, R.Y[i] + (R.Y[i + 1] - R.Y[i]) * f, R.NX[i] + (R.NX[i + 1] - R.NX[i]) * f, R.NY[i] + (R.NY[i + 1] - R.NY[i]) * f]; }
