"""逐帧拟合工具的公共部分：仓库路径、原版逐帧截图、输出目录、Godot 路径。
原版截图 docs/p5/preview/p5r-20261121-223943-frames-02-12/（2560×1440，约 27.5fps）。
「序号」= 按文件名排序后的下标 = 文件帧号 − 1（例：SKILL 页切 2D 立绘那一帧是序号 123 = frame_0124）。"""
import glob, os, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / 'tools/p5/menu3d'))
FRAMES = sorted(glob.glob(str(ROOT / 'docs/p5/preview/p5r-20261121-223943-frames-02-12/frame_*')))
OUT = Path(os.environ.get('P5FIT_OUT', '/tmp/p5fit')); OUT.mkdir(parents=True, exist_ok=True)
GODOT = os.environ.get('GODOT', '/root/godot/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64')
_cache = {}

def orig(idx, size=(1920, 1080)):
    """原版第 idx 帧（序号），缩到 size，RGB"""
    from PIL import Image
    key = (idx, size)
    if key not in _cache:
        _cache[key] = Image.open(FRAMES[idx]).convert('RGB').resize(size, Image.LANCZOS)
    return _cache[key]
