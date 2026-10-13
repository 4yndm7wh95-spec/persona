"""用 art/sfx_src 里的 CC0 素材叠层合成 mod 的招牌音效。
每个音效 = 若干层（素材, 起始ms, 增益dB, 高通Hz, 低通Hz, 变调倍率, 截取ms）+ 混响量；输出 48kHz 单声道。
python3 tools/sfx/build_sfx.py [输出目录]  → 每个变体一个 .ogg，外加一条 _audition.ogg（全部变体依次播放，中间隔 0.8s）。"""
import sys, subprocess
from pathlib import Path
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve, resample
ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "art/sfx_src"
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "art/sfx_v2"
SR = 48000


def load(name):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(SRC / name), "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


def filt(x, hp=None, lp=None):
    if hp: x = sosfilt(butter(4, hp, "highpass", fs=SR, output="sos"), x)
    if lp: x = sosfilt(butter(4, lp, "lowpass", fs=SR, output="sos"), x)
    return x


def layer(name, at=0, db=0, hp=None, lp=None, pitch=1.0, cut=None, fade=8):
    x = load(name)
    if cut: x = x[: int(SR * cut / 1000)]
    if pitch != 1.0: x = resample(x, max(1, int(len(x) / pitch)))
    x = filt(x, hp, lp) * 10 ** (db / 20)
    n = int(SR * fade / 1000)
    if 0 < n < len(x): x[-n:] *= np.linspace(1, 0, n)
    return int(SR * at / 1000), x


def reverb(x, amount, size=0.6, dark=6000):
    if amount <= 0: return x
    t = np.arange(int(SR * size)) / SR
    ir = np.random.default_rng(7).standard_normal(len(t)) * np.exp(-t * 6.9 / size)
    ir = filt(ir, 300, dark); ir /= np.sqrt(np.sum(ir ** 2))
    wet = fftconvolve(x, ir)[: len(x) + len(ir)]
    y = np.zeros(len(wet)); y[: len(x)] += x
    return y + wet * amount


def mix(layers, verb=0.0, size=0.6, dark=6000, comp=True):
    parts = [layer(**l) for l in layers]
    n = max(s + len(x) for s, x in parts)
    y = np.zeros(n)
    for s, x in parts: y[s: s + len(x)] += x
    y = reverb(y, verb, size, dark)
    if comp:                                  # 柔和的削峰压缩，让叠层更"紧"
        y = np.tanh(y * 1.6) / 1.6
    y = y / max(1e-9, np.max(np.abs(y))) * 0.89   # 峰值 -1dB
    # 去掉末尾静音
    idx = np.where(np.abs(y) > 0.003)[0]
    return y[: idx[-1] + int(SR * 0.05)] if len(idx) else y


L = lambda name, **k: dict(name=name, **k)
DESIGNS = {
    # 召唤器：A 带一点枪感（只取枪声开头 30ms、切掉低频）；B 完全没有枪感（咔哒 + 剪刀合上）；C = B + 上扬风声（接 Persona 现身）
    "evoker_A": dict(layers=[L("gun_02_1911_a_42p.ogg", cut=30, hp=1200, db=-4), L("snap_03_switch_001.ogg", db=-6),
                             L("glass_02_heavy_002.ogg", at=12), L("glass_06_crystal_glass_001.ogg", at=55, db=-3, hp=2000),
                             L("impact_04_soft_heavy_000.ogg", lp=220, db=-8)], verb=0.25),
    "evoker_B": dict(layers=[L("snap_06_scissors_close_01.ogg", cut=60, hp=800, db=-2), L("snap_01_click_001.ogg", db=-4),
                             L("glass_01_heavy_000.ogg", at=10), L("glass_05_medium_001.ogg", at=30, db=-4),
                             L("glass_07_crystal_glass_003.ogg", at=70, db=-4, hp=2500), L("impact_04_soft_heavy_000.ogg", lp=200, db=-10)], verb=0.25),
    "evoker_C": dict(layers=[L("snap_06_scissors_close_01.ogg", cut=60, hp=800, db=-2), L("snap_01_click_001.ogg", db=-4),
                             L("glass_01_heavy_000.ogg", at=10), L("glass_05_medium_001.ogg", at=30, db=-4),
                             L("glass_07_crystal_glass_003.ogg", at=70, db=-4, hp=2500), L("impact_04_soft_heavy_000.ogg", lp=200, db=-10),
                             L("whoosh_07_long_wind.ogg", at=120, db=-9, hp=400, pitch=1.15)], verb=0.3),
    # WEAK! / 玻璃碎裂：三层玻璃错开 + 一点上扬的数字闪光
    "glass_weak_A": dict(layers=[L("glass_05_medium_001.ogg"), L("glass_07_crystal_glass_003.ogg", at=25, db=-3), L("glass_08_crystal_glass_006.ogg", at=60, db=-5),
                                 L("shimmer_01_highup.ogg", at=40, db=-14, hp=1500)], verb=0.3),
    "glass_weak_B": dict(layers=[L("glass_02_heavy_002.ogg"), L("glass_04_light_002.ogg", at=30, db=-2), L("glass_06_crystal_glass_001.ogg", at=55, db=-4)], verb=0.35, size=0.8),
    # 塔罗牌碎裂：更大、更亮、尾巴更长
    "tarot_shatter": dict(layers=[L("glass_01_heavy_000.ogg"), L("glass_05_medium_001.ogg", at=20, db=-2), L("glass_06_crystal_glass_001.ogg", at=50, db=-3),
                                  L("glass_07_crystal_glass_003.ogg", at=110, db=-6), L("shimmer_03_phaserup1.ogg", at=30, db=-16, hp=1200),
                                  L("impact_05_soft_heavy_002.ogg", lp=180, db=-10)], verb=0.45, size=1.0),
    # 塔纳托斯斩击：风啸 + 压低的剑鸣 + 重击，暗混响
    "thanatos_slash": dict(layers=[L("whoosh_07_long_wind.ogg", pitch=1.8, db=-4, hp=300), L("whoosh_04_swish_10.ogg", at=120, pitch=0.7, db=-2),
                                   L("slash_05_sword_clash_03.ogg", at=160, pitch=0.8, db=-3), L("impact_01_punch_heavy_000.ogg", at=165, lp=900, db=-1)],
                           verb=0.4, size=1.0, dark=3000),
    # 眼神切入：短促的刷 + 塑料管风声 + 低沉的重音
    "cutin": dict(layers=[L("whoosh_06_tube_plastic_whoosh_02.ogg", db=-2), L("whoosh_03_swish_6.ogg", at=60, pitch=0.85),
                          L("impact_05_soft_heavy_002.ogg", at=180, lp=300, db=-5)], verb=0.2),
    # 普通斩击命中：剑鸣 + 刷
    "slash_hit": dict(layers=[L("whoosh_02_swish_3.ogg"), L("slash_04_seax_unsheathe_01.ogg", at=40, db=-6, hp=1500, cut=250), L("impact_02_punch_heavy_002.ogg", at=50, db=-8, lp=1200)], verb=0.15),
}


def write(path, y):
    pcm = (np.clip(y, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "1", "-i", "-", "-c:a", "libvorbis", "-q:a", "6", str(path)], input=pcm, check=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    gap = np.zeros(int(SR * 0.8)); allx = []
    for name, d in DESIGNS.items():
        y = mix(d["layers"], d.get("verb", 0), d.get("size", 0.6), d.get("dark", 6000))
        write(OUT / f"{name}.ogg", y)
        allx += [y, gap]
        print(f"{name}: {len(y) / SR:.2f}s")
    write(OUT / "_audition.ogg", np.concatenate(allx))


if __name__ == "__main__":
    main()
