#!/usr/bin/env bash
# 渲染一个特效预览：tools/p5/fxpreview/render.sh <场景> [秒数]  → out/<场景>.gif（960 宽，30fps）
# 场景见 Preview.cs 的 Scenarios。需要 Godot 4.5.1 mono（GODOT 环境变量指向可执行文件）、.NET 8 SDK、xvfb-run、ffmpeg。
set -e
cd "$(dirname "$0")"
S=${1:?scenario}; SEC=${2:-4}
: "${GODOT:?set GODOT to the Godot 4.5.1 mono binary}"
python3 prepare.py >/dev/null
# 原版特效（res://scenes/vfx/... 等）：research/sts2_vfx 的拷贝，游戏里这些路径在游戏 PCK 里
for d in scenes shaders materials images animations; do [ -d "$d" ] || cp -r ../../../research/sts2_vfx/recovered/$d $d; done
# 原版战斗背景（overgrowth）：research/sts2_preview_scene 合并进同样的 res:// 目录
if [ ! -d scenes/backgrounds/overgrowth ]; then
  cp -rn ../../../research/sts2_preview_scene/recovered/{scenes,images,shaders,themes} . 2>/dev/null || true
  # 提取时带出来的游戏 .import 指向不存在的缓存，删掉让预览自己导入
  find images scenes themes shaders -name "*.import" -newer render.sh -delete 2>/dev/null || true
  rm -f images/vfx/light.png.import images/vfx/fog_sheet_1.png.import; find images/rooms -name "*.import" -delete
  # 游戏脚本（res://src/... 和背景装饰用的 .gd）预览里没有：去掉引用，不然场景解析失败
  python3 - <<'PY'
import re, pathlib
for f in pathlib.Path("scenes/backgrounds/overgrowth").rglob("*.tscn"):
    t = f.read_text(encoding="utf-8")
    ids = re.findall(r'\[ext_resource type="Script"[^\]]* id="([^"]+)"\]\n', t)
    t = re.sub(r'\[ext_resource type="Script"[^\]]*\]\n', "", t)
    for i in ids:
        t = re.sub(r'^script = ExtResource\("' + re.escape(i) + r'"\)\n', "", t, flags=re.M)
    f.write_text(t, encoding="utf-8")
PY
fi
dotnet build -nologo -v q FxPreview.csproj | grep -E " error " && { echo "build failed"; exit 1; } || true
[ -d .godot/imported ] && [ images/vfx/light.png -ot .godot/imported ] || xvfb-run -a "$GODOT" --headless --path . --import >/dev/null 2>&1 || true
rm -rf "out/$S"; mkdir -p "out/$S"
xvfb-run -a -s "-screen 0 1920x1080x24" "$GODOT" --path . --rendering-driver opengl3 \
  --write-movie "out/$S/f.png" --fixed-fps 30 --quit-after $((SEC * 30)) -- --scenario="$S" >"out/$S/log.txt" 2>&1 || true
grep -E "ERROR|Exception|\[P5Joker\]" "out/$S/log.txt" | head -20 || true
ffmpeg -loglevel error -y -framerate 30 -i "out/$S/f%08d.png" \
  -vf "scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4" "out/$S.gif"
echo "out/$S.gif"
