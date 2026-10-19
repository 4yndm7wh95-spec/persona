"""P3R 原版 PSA 动画 → Godot 渲染用的逐帧骨骼局部变换（glTF 坐标系，和 CUE4Parse 导出的 GLB 一致）。

ActorX：位置单位 cm、左手系；非根骨骼的四元数存的是共轭。换算（和 GLB 的骨骼静止姿势逐项核对过）：
  位置 (x, y, z) → (x, z, -y) / 100；四元数先（非根骨骼）取共轭，再 (x, y, z, w) → (x, z, -y, w)。
输出：<out>.json（骨骼名、帧数、帧率）+ <out>.bin（float32，帧 × 骨骼 × [px py pz qx qy qz qw]）。
用法：python3 tools/p3r/psa_to_bin.py <in.psa> <out_prefix> [只保留这些骨骼名的 json 列表文件]
"""
import json, struct, sys
import numpy as np


def chunks(d):
    i, out = 0, {}
    while i < len(d):
        cid = d[i:i + 20].split(b"\0")[0].decode()
        _, size, cnt = struct.unpack("<iii", d[i + 20:i + 32])
        out[cid] = (size, cnt, i + 32)
        i += 32 + size * cnt
    return out


def main(src, prefix, keep=None):
    d = open(src, "rb").read()
    c = chunks(d)
    s, n, o = c["BONENAMES"]
    names = [d[o + i * s:o + i * s + 64].split(b"\0")[0].decode() for i in range(n)]
    s, _, o = c["ANIMINFO"]
    rate = struct.unpack("<f", d[o + 152:o + 156])[0]
    s, cnt, o = c["ANIMKEYS"]
    keys = np.frombuffer(d, dtype=np.float32, count=cnt * 8, offset=o).reshape(-1, len(names), 8)
    frames = keys.shape[0]
    pos = keys[..., 0:3]
    q = keys[..., 3:7].copy()
    q[:, 1:, :3] *= -1                         # 非根骨骼：共轭
    out = np.empty((frames, len(names), 7), np.float32)
    out[..., 0] = pos[..., 0] / 100
    out[..., 1] = pos[..., 2] / 100
    out[..., 2] = -pos[..., 1] / 100
    out[..., 3] = q[..., 0]
    out[..., 4] = q[..., 2]
    out[..., 5] = -q[..., 1]
    out[..., 6] = q[..., 3]
    idx = list(range(len(names)))
    if keep:
        ks = set(keep)
        idx = [i for i, nm in enumerate(names) if nm in ks]
    out = out[:, idx]
    open(prefix + ".bin", "wb").write(out.tobytes())
    json.dump({"bones": [names[i] for i in idx], "frames": frames, "fps": rate}, open(prefix + ".json", "w"))
    print(src, "frames", frames, "fps", round(rate, 3), "bones", len(idx))


if __name__ == "__main__":
    keep = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else None
    main(sys.argv[1], sys.argv[2], keep)
