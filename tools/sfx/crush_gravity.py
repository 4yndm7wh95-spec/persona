"""下砸（CrushUnder）命中音效：重力法术把敌人压进地面。
和打击类（atk_hit_strike：短促、1~3kHz 的"啪"）刻意区分 —— 没有拳套那种中高频脆响：
  - 0s：极低的压地闷响（落到 48Hz、带下滑音头），厚而长；
  - 0~0.45s：石地被压碎的颗粒碎裂声（250~2200Hz，颗粒越来越稀）；
  - 0.08~0.8s：碎石落地的细碎声（很轻）；
  - 整段垫一层低沉、往下沉的不协和低音（重力魔法的"压迫感"），0.9s 衰减；
  - 尾部 1s 左右的低频余震。
命中时播放（CardFx：下砸专属命中 Uniq.CrushHit，每次出牌只播一次）。
用法：python3 tools/sfx/crush_gravity.py <输出目录>
"""
import sys, subprocess, numpy as np
from scipy.signal import butter, sosfilt

SR = 44100
rng = np.random.default_rng(11)


def band(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def low(x, f, order=2):
    return sosfilt(butter(order, f, btype="low", fs=SR, output="sos"), x)


def crush(dur=1.5):
    n = int(SR * dur)
    t = np.arange(n) / SR
    out = np.zeros(n)
    # 压地闷响：音头 138Hz 迅速下滑到 48Hz
    f = 48 + 90 * np.exp(-t / 0.03)
    ph = 2 * np.pi * np.cumsum(f) / SR
    out += np.sin(ph) * np.minimum(t / 0.003, 1) * np.exp(-t / 0.3) * 0.7
    out += band(rng.standard_normal(n), 90, 900) * np.minimum(t / 0.004, 1) * np.exp(-t / 0.1) * 1.6
    # 石地碎裂：颗粒（短促噪声粒）密度随时间下降
    grains = np.zeros(n)
    for _ in range(140):
        at = rng.exponential(0.12)
        if at > 0.5:
            continue
        g = int(SR * rng.uniform(0.004, 0.018))
        i = int(at * SR)
        if i + g >= n:
            continue
        grains[i:i + g] += rng.standard_normal(g) * np.hanning(g) * rng.uniform(0.3, 1.0) * np.exp(-at / 0.2)
    out += band(grains, 250, 2200) * 1.4
    # 碎石落地：稀疏、轻、偏亮
    patter = np.zeros(n)
    for _ in range(26):
        at = rng.uniform(0.08, 0.8)
        g = int(SR * rng.uniform(0.003, 0.008))
        i = int(at * SR)
        patter[i:i + g] += rng.standard_normal(g) * np.hanning(g) * rng.uniform(0.2, 0.6) * np.exp(-(at - 0.08) / 0.35)
    out += band(patter, 1500, 5000) * 0.18
    # 重力魔法：低沉不协和、音高往下沉
    sag = 1 - 0.12 * np.clip(t / 0.9, 0, 1)
    drone = sum(np.sin(2 * np.pi * np.cumsum(fr * sag) / SR + rng.uniform(0, 6)) * a
                for fr, a in ((110, 0.5), (116.5, 0.35), (164.8, 0.3), (233, 0.15)))
    out += low(drone, 900) * np.minimum(t / 0.012, 1) * np.exp(-t / 0.42) * 0.5
    # 低频余震
    out += low(rng.standard_normal(n), 120, 4) * np.clip(t / 0.05, 0, 1) * np.exp(-t / 0.5) * 0.5
    return low(out, 6000, 4)


def write(path, x, peak=0.85):
    x = x / (np.abs(x).max() + 1e-9) * peak
    pcm = (x * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "6", path], input=pcm, check=True)


if __name__ == "__main__":
    write(f"{sys.argv[1]}/crush_gravity.ogg", crush())
