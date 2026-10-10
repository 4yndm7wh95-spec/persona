"""单张立绘 → 加权网格骨骼。

做法：在抠好的图上铺三角网格（只保留有像素的格子），每根骨骼用"区域多边形 + 羽化距离"决定它对顶点的影响，
按优先级（越具体越靠前）分配权重，剩下的给 residual 骨骼（默认 root）—— 交界处平滑过渡，转动时像布料一样弯折。
会大幅移动、且压在别的部件上面的部分（例如挥舞的剑臂）用 cut() 切成独立的一块：切口在身体图上用扩散填补（不露洞），
切下来的那块根部羽化，静止时与身体完全重合。
动画按 30fps 烘焙成线性关键帧，预览和游戏看到同一组数据。

坐标约定：输入都是**原图像素**（x 向右、y 向下）；导出时以 origin 为原点、乘以 unit 换算成 Spine 单位（y 向上）。
贴图可以缩小打包（tex_scale），网格坐标不受影响（UV 是相对的）。
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

FPS = 30


def _poly_mask(size, polys=(), circles=()):
    m = Image.new("L", size, 0)
    d = ImageDraw.Draw(m)
    for poly in polys:
        d.polygon([tuple(p) for p in poly], fill=255)
    for (cx, cy, r) in circles:
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    return m


def inpaint(rgba: np.ndarray, hole: np.ndarray, iters=400) -> np.ndarray:
    """扩散填补：hole 内的像素反复取四邻域平均（预乘 alpha），从边缘向内渗透。透明背景处自然保持透明。"""
    out = rgba.astype(np.float32)
    a = out[..., 3:4] / 255
    pm = np.concatenate([out[..., :3] * a, a], -1)
    pm[hole] = 0
    known = ~hole
    for _ in range(iters):
        acc = np.zeros_like(pm)
        cnt = np.zeros(hole.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            acc += np.roll(pm, (dy, dx), (0, 1))
            cnt += np.roll(known.astype(np.float32), (dy, dx), (0, 1))
        upd = hole & (cnt > 0)
        pm[upd] = acc[upd] / 4.0
        known = known | upd
    alpha = np.clip(pm[..., 3:4], 1e-6, 1)
    res = np.concatenate([pm[..., :3] / alpha, alpha * 255], -1)
    res[hole & (pm[..., 3] < 1e-3)] = 0
    out[hole] = res[hole]
    return np.clip(out, 0, 255).astype(np.uint8)


class Piece:
    def __init__(self, name, img, only=None, residual="root"):
        bbox = img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
        pad = 6
        self.box = (max(bbox[0] - pad, 0), max(bbox[1] - pad, 0), min(bbox[2] + pad, img.width), min(bbox[3] + pad, img.height))
        self.name = name
        self.img = img.crop(self.box)
        self.only = only
        self.residual = residual


class MeshRig:
    def __init__(self, img: Image.Image, origin, unit=1.0, step=22, tex_scale=0.7, name="body"):
        self.full = img.convert("RGBA")
        self.body = np.asarray(self.full).copy()
        self.origin = origin
        self.unit = unit
        self.step = step
        self.tex_scale = tex_scale
        self.name = name
        self.bones = [("root", None, origin)]             # (名字, 父骨骼, 原图像素位置)
        self.regions = {}                                  # 骨骼 -> 权重图（原图大小，0~1）
        self.priority = None                               # 权重分配顺序（默认按骨骼添加顺序）
        self.cuts = []                                     # [(Piece)] 画在身体之上的独立块
        self.body_only = None                              # 身体块允许受哪些骨骼影响（默认全部）

    # ── 坐标 ──
    def sp(self, p):
        return ((p[0] - self.origin[0]) * self.unit, (self.origin[1] - p[1]) * self.unit)

    def bone(self, name, parent, pos, polys=None, feather=30, circles=None, grow=0):
        """新增骨骼（pos 为原图像素），polys/circles 是它的影响区域（原图像素）。
        grow：区域先向外扩张的像素 —— 细长的刚体（剑）要扩大约一个网格步长，保证压在边缘上的顶点也完全属于它，不然会撕裂。"""
        self.bones.append((name, parent, pos))
        if polys or circles:
            m = _poly_mask(self.full.size, polys or [], circles or [])
            if grow > 0:
                m = m.filter(ImageFilter.MaxFilter(grow * 2 + 1))
            if feather > 0:
                m = m.filter(ImageFilter.GaussianBlur(feather / 2))
            self.regions[name] = np.asarray(m).astype(np.float32) / 255

    def pos(self, name):
        return next(p for n, _, p in self.bones if n == name)

    def cut(self, name, polys, base, fade=(15, 70), only=None, residual="root"):
        """把 polys 内的像素切成独立一块（画在身体上面）。base = 根部（如肩）位置：块的 alpha 从根部向外
        在 fade 距离内由 0 渐变到 1；完全不透明的部分从身体上移除并扩散填补，渐变部分身体保留原样（静止时完全重合）。"""
        m = np.asarray(_poly_mask(self.full.size, polys)) > 0
        h, w = m.shape
        ys, xs = np.mgrid[0:h, 0:w]
        d = np.hypot(xs - base[0], ys - base[1])
        ramp = np.clip((d - fade[0]) / (fade[1] - fade[0]), 0, 1)
        src = np.asarray(self.full)
        piece = src.copy()
        piece[..., 3] = (src[..., 3] * m * ramp).astype(np.uint8)
        hole = m & (ramp >= 1) & (src[..., 3] > 0)
        hole = np.asarray(Image.fromarray(hole.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(3))) > 0
        self.body = inpaint(self.body, hole)
        self.cuts.append(Piece(name, Image.fromarray(piece, "RGBA"), only, residual))

    def pieces(self):
        return [Piece(self.name, Image.fromarray(self.body, "RGBA"), self.body_only)] + self.cuts

    # ── 网格 ──
    def _weights(self, x, y, only, residual):
        h, w = self.body.shape[:2]
        xi, yi = min(max(int(x), 0), w - 1), min(max(int(y), 0), h - 1)
        remaining = 1.0
        ws = []
        for name in self.priority or [b[0] for b in self.bones[1:]]:
            if name not in self.regions or (only and name not in only) or name == residual:
                continue
            v = float(self.regions[name][yi, xi]) * remaining
            if v > 1e-3:
                ws.append((name, v))
                remaining -= v
        ws.append((residual, remaining))
        ws = sorted(ws, key=lambda t: -t[1])[:3]
        s = sum(v for _, v in ws)
        return [(n, v / s) for n, v in ws if v / s > 1e-3]

    def mesh(self, pc: Piece):
        a = np.asarray(pc.img.getchannel("A")) > 8
        h, w = a.shape
        s = self.step
        xs = list(range(0, w, s)) + ([w] if (w - 1) % s else [])
        ys = list(range(0, h, s)) + ([h] if (h - 1) % s else [])
        index, verts_px, tris = {}, [], []

        def vid(i, j):
            if (i, j) not in index:
                index[(i, j)] = len(verts_px)
                verts_px.append((xs[i], ys[j]))
            return index[(i, j)]

        for j in range(len(ys) - 1):
            for i in range(len(xs) - 1):
                if a[ys[j]:ys[j + 1], xs[i]:xs[i + 1]].any():
                    v00, v10, v01, v11 = vid(i, j), vid(i + 1, j), vid(i, j + 1), vid(i + 1, j + 1)
                    tris += [v00, v10, v01, v10, v11, v01]
        bone_index = {n: k for k, (n, _, _) in enumerate(self.bones)}
        world = {n: self.sp(p) for n, _, p in self.bones}
        uvs, verts = [], []
        for (x, y) in verts_px:
            uvs += [round(x / w, 5), round(y / h, 5)]
            fx, fy = x + pc.box[0], y + pc.box[1]
            ws = self._weights(fx, fy, pc.only, pc.residual)
            wx, wy = self.sp((fx, fy))
            verts.append(len(ws))
            for b, v in ws:
                bx, by = world[b]
                verts += [bone_index[b], round(wx - bx, 2), round(wy - by, 2), round(v, 4)]
        return {"type": "mesh", "uvs": uvs, "triangles": tris, "vertices": verts, "hull": 0,
                "width": round(w * self.unit), "height": round(h * self.unit)}

    # ── 导出 ──
    def setup(self, events=()):
        world = {n: self.sp(p) for n, _, p in self.bones}
        bones = []
        for n, parent, _ in self.bones:
            b = {"name": n}
            if parent:
                b.update(parent=parent, x=round(world[n][0] - world[parent][0], 2), y=round(world[n][1] - world[parent][1], 2))
            bones.append(b)
        self._pieces = self.pieces()
        return {
            "skeleton": {"spine": "4.2.43", "images": "./", "audio": ""},
            "bones": bones,
            "slots": [{"name": pc.name, "bone": "root", "attachment": pc.name} for pc in self._pieces],
            "skins": [{"name": "default", "attachments": {pc.name: {pc.name: self.mesh(pc)} for pc in self._pieces}}],
            "events": {e: {} for e in events},
            "animations": {},
        }

    def slot_names(self):
        return [pc.name for pc in self._pieces]

    def images(self):
        return {pc.name: pc.img for pc in self._pieces}

    def texture_and_atlas(self, png_name):
        """各块缩放后竖向堆叠在一张图集页里。"""
        texs = []
        for pc in self._pieces:
            im = pc.img
            if self.tex_scale != 1.0:
                im = im.resize((max(1, round(im.width * self.tex_scale)), max(1, round(im.height * self.tex_scale))), Image.LANCZOS)
            texs.append((pc.name, im))
        W = max(im.width for _, im in texs)
        H = sum(im.height for _, im in texs) + 2 * (len(texs) - 1)
        page = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        lines = [png_name, f"size:{W},{H}", "filter:Linear,Linear"]
        y = 0
        for name, im in texs:
            page.alpha_composite(im, (0, y))
            lines += [name, f"bounds:0,{y},{im.width},{im.height}"]
            y += im.height + 2
        return page, "\n".join(lines) + "\n"


def bake(data, name, duration, pose_fn, events=(), slot_alpha=None, slot="body", loop=False):
    slots = [slot] if isinstance(slot, str) else list(slot)
    """pose_fn(t) -> {骨骼: {"r":度, "x":, "y":, "sx":, "sy":}}（相对 setup，Spine 单位，逆时针为正）。
    slot_alpha(t) -> 0~1。按 30fps 烘焙线性关键帧；loop=True 时最后一帧强制等于第一帧。"""
    n = max(1, round(duration * FPS))
    times = [round(i / FPS, 4) for i in range(n + 1)]
    times[-1] = round(duration, 4)
    tl = {}
    for k, t in enumerate(times):
        pose = pose_fn(0.0 if (loop and k == n) else t)
        for b, ch in pose.items():
            e = tl.setdefault(b, {"rotate": [], "translate": [], "scale": []})
            e["rotate"].append({"time": t, "value": round(ch.get("r", 0.0), 3)})
            e["translate"].append({"time": t, "x": round(ch.get("x", 0.0), 2), "y": round(ch.get("y", 0.0), 2)})
            e["scale"].append({"time": t, "x": round(ch.get("sx", 1.0), 4), "y": round(ch.get("sy", 1.0), 4)})
    bones = {}
    for b, e in tl.items():
        bones[b] = {k: v for k, v in e.items() if any(any(abs(kf.get(f, d) - d) > 1e-4 for f, d in
                                                          (("value", 0), ("x", 1 if k == "scale" else 0), ("y", 1 if k == "scale" else 0)))
                                                      for kf in v)}
    anim = {"bones": {b: e for b, e in bones.items() if e}}
    if slot_alpha:
        rgba = [{"time": t, "color": "ffffff%02x" % round(255 * max(0, min(1, slot_alpha(t))))} for t in times]
        anim["slots"] = {s: {"rgba": rgba} for s in slots}
    if events:
        anim["events"] = [{"time": round(t, 4), "name": e} for t, e in events]
    data["animations"][name] = anim


# ── 缓动 ──
def smooth(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * (3 - 2 * u)


def out_cubic(u):
    u = min(max(u, 0.0), 1.0)
    return 1 - (1 - u) ** 3


def in_cubic(u):
    u = min(max(u, 0.0), 1.0)
    return u ** 3


def out_back(u, s=1.7):
    u = min(max(u, 0.0), 1.0) - 1
    return u * u * ((s + 1) * u + s) + 1


def keys(t, ks):
    """分段插值：ks = [(时间, 值, 缓动=smooth), ...]。"""
    if t <= ks[0][0]:
        return ks[0][1]
    for k0, k1 in zip(ks, ks[1:]):
        if k0[0] <= t <= k1[0]:
            ease = k1[2] if len(k1) > 2 else smooth
            u = (t - k0[0]) / max(k1[0] - k0[0], 1e-6)
            return k0[1] + (k1[1] - k0[1]) * ease(u)
    return ks[-1][1]


def write(out_dir: Path, base: str, data, rig: MeshRig):
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / f"{base}.spine-json").write_text(json.dumps(data, separators=(",", ":")))
    page, atlas = rig.texture_and_atlas(f"{base}.png")
    (out_dir / f"{base}.atlas").write_text(atlas)
    page.save(out_dir / f"{base}.png")


def render_frame(sk, data, anim, t, canvas, origin, bg):
    """渲染一帧，并按第一个带 rgba 的插槽应用整体透明度（Persona 各块同步淡入淡出）。"""
    im = sk.render(anim, t, canvas=canvas, origin=origin, bg=(0, 0, 0, 0))
    base = Image.new("RGBA", canvas, bg)
    base.alpha_composite(im)
    return base


def preview(data, rig: MeshRig, anim, out_gif: Path, canvas, origin, scale=0.4, bg=(28, 32, 48, 255), every=3):
    from spine_eval import Skeleton
    sk = Skeleton(data, rig.images())
    a = data["animations"][anim]
    times = sorted({k["time"] for bk in a.get("bones", {}).values() for tl in bk.values() for k in tl}) or [0.0]
    times = times[::every]                                                     # 网格逐三角形软渲染很慢，预览抽帧
    frames = [render_frame(sk, data, anim, t, canvas, origin, bg).convert("RGB")
              .resize((int(canvas[0] * scale), int(canvas[1] * scale)), Image.LANCZOS)
              .convert("P", palette=Image.ADAPTIVE, colors=200) for t in times]
    out_gif.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(out_gif, save_all=True, append_images=frames[1:], duration=33 * every, loop=0, optimize=True)
