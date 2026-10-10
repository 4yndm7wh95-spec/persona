"""最小 Spine 求值器 + 渲染器（只支持本项目用到的子集：骨骼的平移/旋转/缩放，区域附件，线性插值）。
游戏里 spine-godot 用同一套规则计算，所以这里渲染出来的画面就是游戏里的画面（不含特效）。
坐标：Spine 世界坐标 y 向上；渲染时翻转成图像坐标。"""
import math
import numpy as np
from PIL import Image


def mat(x, y, rot_deg, sx=1.0, sy=1.0):
    r = math.radians(rot_deg)
    c, s = math.cos(r), math.sin(r)
    return np.array([[c * sx, -s * sy, x], [s * sx, c * sy, y], [0, 0, 1]], dtype=np.float64)


def sample(keys, t, fields):
    """keys: [{'time':..,field:..}] 线性插值；超出范围取端点。"""
    if not keys:
        return None
    if t <= keys[0]["time"]:
        return [keys[0].get(f, 0.0) for f in fields]
    for a, b in zip(keys, keys[1:]):
        if a["time"] <= t <= b["time"]:
            u = 0.0 if b["time"] == a["time"] else (t - a["time"]) / (b["time"] - a["time"])
            return [a.get(f, 0.0) + (b.get(f, 0.0) - a.get(f, 0.0)) * u for f in fields]
    return [keys[-1].get(f, 0.0) for f in fields]


class Skeleton:
    def __init__(self, data, images):
        self.data = data
        self.images = images  # region name -> PIL RGBA（已按附件尺寸）
        self.bones = {b["name"]: b for b in data["bones"]}
        self.order = [b["name"] for b in data["bones"]]

    def world(self, anim=None, t=0.0):
        tl = (self.data.get("animations", {}).get(anim, {}).get("bones", {})) if anim else {}
        W = {}
        for name in self.order:
            b = self.bones[name]
            x, y = b.get("x", 0.0), b.get("y", 0.0)
            rot = b.get("rotation", 0.0)
            sx, sy = b.get("scaleX", 1.0), b.get("scaleY", 1.0)
            k = tl.get(name, {})
            if "rotate" in k:
                rot += sample(k["rotate"], t, ["value"])[0]
            if "translate" in k:
                dx, dy = sample(k["translate"], t, ["x", "y"])
                x += dx
                y += dy
            if "scale" in k:
                ks = sample(k["scale"], t, ["x", "y"])
                sx *= ks[0] if ks[0] != 0 else 1.0
                sy *= ks[1] if ks[1] != 0 else 1.0
            local = mat(x, y, rot, sx, sy)
            W[name] = W[b["parent"]] @ local if "parent" in b else local
        return W

    def attachments(self, anim=None, t=0.0):
        """返回 [(slot, bone, attachment_name, alpha)]，按绘制顺序。"""
        st = (self.data.get("animations", {}).get(anim, {}).get("slots", {})) if anim else {}
        out = []
        for s in self.data["slots"]:
            name = s.get("attachment")
            keys = st.get(s["name"], {}).get("attachment")
            if keys:
                for k in keys:
                    if k["time"] <= t + 1e-6:
                        name = k["name"]
            alpha = 1.0
            ck = st.get(s["name"], {}).get("rgba")
            if ck:
                ak = [{"time": k["time"], "a": int(k["color"][6:8], 16) / 255.0} for k in ck]
                alpha = sample(ak, t, ["a"])[0]
            if name and alpha > 0.003:
                out.append((s["name"], s["bone"], name, alpha))
        return out

    def render(self, anim=None, t=0.0, canvas=(900, 1300), origin=(450, 1200), bg=(0, 0, 0, 0)):
        """origin = Spine 世界原点在画布上的像素位置。"""
        W = self.world(anim, t)
        img = Image.new("RGBA", canvas, bg)
        skin = self.data["skins"][0]["attachments"]
        flip = np.array([[1, 0, origin[0]], [0, -1, origin[1]], [0, 0, 1]], dtype=np.float64)
        for slot, bone, att, alpha in self.attachments(anim, t):
            a = skin[slot][att]
            src = self.images[a.get("path", att)]
            if alpha < 0.999:
                src = src.copy()
                src.putalpha(src.getchannel("A").point(lambda v: int(v * alpha)))
            if a.get("type") == "mesh":
                self._draw_mesh(img, src, a, W, flip)
                continue
            w, h = a["width"], a["height"]
            # 附件局部：中心在 (x,y)，图像像素 → 附件单位，图像 y 向下 → Spine y 向上
            A = mat(a.get("x", 0), a.get("y", 0), a.get("rotation", 0), a.get("scaleX", 1), a.get("scaleY", 1))
            to_att = np.array([[w / src.width, 0, -w / 2], [0, -h / src.height, h / 2], [0, 0, 1]])
            M = flip @ W[bone] @ A @ to_att  # 源图像素 → 画布像素
            inv = np.linalg.inv(M)
            layer = src.transform(canvas, Image.AFFINE, data=tuple(inv[:2].flatten()), resample=Image.BILINEAR)
            img.alpha_composite(layer)
        return img

    def mesh_world(self, a, W):
        """加权网格顶点 → 世界坐标。vertices 格式：[骨骼数, (骨骼索引, x, y, 权重)×骨骼数] × 顶点数。"""
        names = self.order
        v = a["vertices"]
        out = []
        i = 0
        while i < len(v):
            n = int(v[i])
            i += 1
            x = y = 0.0
            for _ in range(n):
                b, bx, by, w = int(v[i]), v[i + 1], v[i + 2], v[i + 3]
                i += 4
                p = W[names[b]] @ np.array([bx, by, 1.0])
                x += p[0] * w
                y += p[1] * w
            out.append((x, y))
        return out

    def _draw_mesh(self, img, src, a, W, flip):
        pts = self.mesh_world(a, W)
        uvs = a["uvs"]
        tri = a["triangles"]
        for k in range(0, len(tri), 3):
            ids = tri[k:k + 3]
            dst = [flip @ np.array([pts[j][0], pts[j][1], 1.0]) for j in ids]
            srcp = [(uvs[2 * j] * src.width, uvs[2 * j + 1] * src.height) for j in ids]
            xs = [d[0] for d in dst]
            ys = [d[1] for d in dst]
            x0, y0 = int(math.floor(min(xs))) - 1, int(math.floor(min(ys))) - 1
            x1, y1 = int(math.ceil(max(xs))) + 1, int(math.ceil(max(ys))) + 1
            if x1 <= x0 or y1 <= y0 or x1 <= 0 or y1 <= 0 or x0 >= img.width or y0 >= img.height:
                continue
            # 目标像素 → 源像素 的仿射
            A = np.array([[d[0] - x0, d[1] - y0, 1.0] for d in dst])
            S = np.array(srcp)
            try:
                coef = np.linalg.solve(A, S)  # 3x2
            except np.linalg.LinAlgError:
                continue
            data = (coef[0, 0], coef[1, 0], coef[2, 0], coef[0, 1], coef[1, 1], coef[2, 1])
            patch = src.transform((x1 - x0, y1 - y0), Image.AFFINE, data=data, resample=Image.BILINEAR)
            mask = Image.new("L", patch.size, 0)
            from PIL import ImageDraw
            ImageDraw.Draw(mask).polygon([(d[0] - x0, d[1] - y0) for d in dst], fill=255)
            # 相邻三角形共享边：mask 外扩 1px 避免接缝
            from PIL import ImageFilter
            mask = mask.filter(ImageFilter.MaxFilter(3))
            alpha = Image.fromarray((np.asarray(patch.getchannel("A")).astype(np.uint16) * np.asarray(mask) // 255).astype(np.uint8))
            patch.putalpha(alpha)
            img.alpha_composite(patch, dest=(max(x0, 0), max(y0, 0)), source=(max(-x0, 0), max(-y0, 0)))

    def point(self, bone, local=(0, 0), anim=None, t=0.0):
        W = self.world(anim, t)
        p = W[bone] @ np.array([local[0], local[1], 1.0])
        return p[0], p[1]
