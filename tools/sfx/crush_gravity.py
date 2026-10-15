"""下砸（CrushUnder）命中音效：符文石板砸在敌人头顶、碎裂四散。
v0.14.41（用户：太闷了；打击感有些塑料）：
  - 闷：以前 95% 的能量在 150Hz 以下（48Hz 闷响 + 低沉嗡鸣）。
  - 塑料：噪声堆出来的声音缺少"实心物体"的共振。
现在用模态合成：撞击瞬间同时激发几十个不规则（非整数倍）频率的振动、各自很快衰减 —— 这是石头 / 实心重物的质感；
低频只留一记被压饱和的短促重量；碎块落地每块也是一小组模态；最后过一个很短的房间混响，再轻微软饱和让声音更"实"。
  - 0s：1ms 的撞击瞬态 + 44 个模态（180~2600Hz，低的衰减慢、高的衰减快，25~120ms）；
  - 0s：重量（62→46Hz + 二次谐波，过 tanh 压饱和，0.22s）；
  - 0~0.25s：石板碎裂的嘎吱声（400~4000Hz，颗粒越来越稀）；
  - 0.1~0.9s：十几块碎块落地（每块 5 个 800~4000Hz 的短模态，越来越轻、越来越稀）；
  - 0.35s 的房间混响（湿声 -14dB）。
命中时播放（Uniq.CrushUnderImpact → Shatter，每次出牌只播一次）。
用法：python3 tools/sfx/crush_gravity.py <输出目录>
"""
import sys, subprocess, numpy as np
from scipy.signal import butter, sosfilt, fftconvolve

SR = 44100
rng = np.random.default_rng(29)


def band(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def low(x, f, order=2):
    return sosfilt(butter(order, f, btype="low", fs=SR, output="sos"), x)


def place(buf, clip, at):
    i = int(at * SR)
    if i >= len(buf):
        return
    n = min(len(clip), len(buf) - i)
    buf[i:i + n] += clip[:n]


def modes(n_modes, f_lo, f_hi, dur, decay_lo, decay_hi, tilt=0.5):
    """一组不规则模态：频率按对数随机分布，低频衰减慢、高频衰减快，振幅 ∝ f^-tilt。"""
    m = int(SR * dur)
    t = np.arange(m) / SR
    out = np.zeros(m)
    fs = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi), n_modes))
    for f in fs:
        k = (np.log(f) - np.log(f_lo)) / (np.log(f_hi) - np.log(f_lo))
        tau = decay_lo * (1 - k) + decay_hi * k
        out += np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / tau) * (f / f_lo) ** -tilt * rng.uniform(0.5, 1.0)
    return out


def crush(dur=1.3):
    n = int(SR * dur)
    t = np.arange(n) / SR
    out = np.zeros(n)
    # 撞击瞬态
    out += low(rng.standard_normal(n), 8000) * np.exp(-t / 0.0012) * 1.2
    # 石板本体的模态
    place(out, modes(44, 180, 2600, 0.6, 0.12, 0.025, tilt=-0.1) * 0.9, 0.0)
    # 重量：压饱和的短促低音
    f = 46 + 16 * np.exp(-t / 0.03)
    ph = 2 * np.pi * np.cumsum(f) / SR
    thump = (np.sin(ph) + 0.35 * np.sin(2 * ph)) * np.minimum(t / 0.0015, 1) * np.exp(-t / 0.22)
    out += np.tanh(thump * 2.2) * 0.9
    # 碎裂的嘎吱声
    grains = np.zeros(n)
    for _ in range(160):
        at = rng.exponential(0.06)
        if at > 0.25:
            continue
        g = int(SR * rng.uniform(0.003, 0.012))
        i = int(at * SR)
        grains[i:i + g] += rng.standard_normal(g) * np.hanning(g) * rng.uniform(0.3, 1.0) * np.exp(-at / 0.1)
    out += band(grains, 400, 4000) * 2.8
    # 碎块落地
    for k in range(14):
        at = 0.1 + 0.8 * (k / 14) ** 1.25 + rng.uniform(-0.025, 0.025)
        chunk = modes(5, 800, 4000, 0.08, 0.02, 0.008, tilt=0.3)
        chunk[: int(SR * 0.0008)] += rng.standard_normal(int(SR * 0.0008)) * 0.8
        place(out, chunk * rng.uniform(0.18, 0.32) * (1 - 0.55 * k / 14), at)
    # 很短的房间混响
    ir_n = int(SR * 0.35)
    ir_t = np.arange(ir_n) / SR
    ir = rng.standard_normal(ir_n) * np.exp(-ir_t / 0.09)
    ir = low(ir, 5000)
    wet = fftconvolve(out, ir)[:n]
    wet *= np.abs(out).max() / (np.abs(wet).max() + 1e-9) * 0.2
    out = out + wet
    # 轻微软饱和：更"实"
    out = np.tanh(out / (np.abs(out).max() + 1e-9) * 1.6)
    return low(out, 12000, 4)


def write(path, x, peak=0.9):
    x = x / (np.abs(x).max() + 1e-9) * peak
    pcm = (x * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "6", path], input=pcm, check=True)


if __name__ == "__main__":
    write(f"{sys.argv[1]}/crush_gravity.ogg", crush())
