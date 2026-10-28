#!/usr/bin/env bash
# 渲染一个特效预览：tools/p5/fxpreview/render.sh <场景> [秒数]  → out/<场景>.gif（960 宽，30fps）
# 场景见 Preview.cs 的 Scenarios。需要 Godot 4.5.1 mono（GODOT 环境变量指向可执行文件）、.NET 8 SDK、xvfb-run、ffmpeg。
set -e
cd "$(dirname "$0")"
S=${1:?scenario}; SEC=${2:-4}
: "${GODOT:?set GODOT to the Godot 4.5.1 mono binary}"
python3 prepare.py >/dev/null
dotnet build -nologo -v q FxPreview.csproj >/dev/null
[ -d .godot ] || xvfb-run -a "$GODOT" --headless --path . --import >/dev/null 2>&1 || true
rm -rf "out/$S"; mkdir -p "out/$S"
xvfb-run -a -s "-screen 0 1920x1080x24" "$GODOT" --path . --rendering-driver opengl3 \
  --write-movie "out/$S/f.png" --fixed-fps 30 --quit-after $((SEC * 30)) -- --scenario="$S" >"out/$S/log.txt" 2>&1 || true
grep -E "ERROR|Exception|\[P5Joker\]" "out/$S/log.txt" | head -20 || true
ffmpeg -loglevel error -y -framerate 30 -i "out/$S/f%08d.png" \
  -vf "scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4" "out/$S.gif"
echo "out/$S.gif"
