"""Curves built by integrating a smooth curvature profile along the arc length (px of a 1280x720 frame, y down)."""
import numpy as np
def integrate(x0, y0, th0, L, kfun, n=4000):
    s = np.linspace(0, L, n); ds = s[1] - s[0]; k = kfun(s)
    th = th0 + np.concatenate([[0], np.cumsum((k[1:] + k[:-1]) / 2 * ds)])
    x = x0 + np.concatenate([[0], np.cumsum((np.cos(th[1:]) + np.cos(th[:-1])) / 2 * ds)])
    y = y0 + np.concatenate([[0], np.cumsum((np.sin(th[1:]) + np.sin(th[:-1])) / 2 * ds)])
    return s, x, y, th, k
def unimodal(ka, kp, sp, wr, kt, wf):
    """base ka -> peak kp at sp (gaussian shoulder, width wr) -> tail kt (width wf); C1 at the peak, smooth elsewhere"""
    def f(s):
        return np.where(s <= sp, ka + (kp - ka) * np.exp(-0.5 * ((s - sp) / wr) ** 2), kt + (kp - kt) * np.exp(-0.5 * ((s - sp) / wf) ** 2))
    return f
def nearest(REF, x, y):
    P = np.stack([x, y], 1)
    d2 = ((REF[:, None, :] - P[None, :, :]) ** 2).sum(-1)
    i = d2.argmin(1)
    return np.sqrt(d2[np.arange(len(REF)), i]), i
def unimodal2(ka0, ka1, kp, sp, wr, kt, wf):
    """bottom arm: curvature ramps ka0 -> ka1 (quadratic in s/sp); gaussian shoulder up to the peak kp at sp; falls to kt.
    Increasing before the peak, decreasing after it."""
    def f(s):
        base = ka0 + (ka1 - ka0) * np.clip(s / sp, 0, 1) ** 2
        up = base + (kp - ka1) * np.exp(-0.5 * ((s - sp) / wr) ** 2)
        dn = kt + (kp - kt) * np.exp(-0.5 * ((s - sp) / wf) ** 2)
        return np.where(s <= sp, up, dn)
    return f
