#!/usr/bin/env bash
# 云端着色器编译检查：把 mod/src/Vfx/Shaders.cs 里的每个着色器拆成 .gdshader，用真的 Godot 4.5.1（OpenGL3 + llvmpipe，xvfb）编译一遍。
# check_cs.sh 只查 C#，着色器写错（例如 fragment 里 return）要到实机才会发现 —— v0.14.77 就是这样：水全白、立绘原图直出。
set -e
cd "$(dirname "$0")/.."
GD=/tmp/godot451/Godot_v4.5.1-stable_linux.x86_64
if [ ! -x "$GD" ]; then
  mkdir -p /tmp/godot451
  curl -sSL -o /tmp/godot451/g.zip https://github.com/godotengine/godot/releases/download/4.5.1-stable/Godot_v4.5.1-stable_linux.x86_64.zip
  unzip -o -q /tmp/godot451/g.zip -d /tmp/godot451
fi
P=$(mktemp -d); cp tools/shader_check/proj/* "$P"/
python3 tools/shader_check/extract.py mod/src/Vfx/Shaders.cs "$P/sh"
OUT=$(timeout 180 xvfb-run -a "$GD" --rendering-driver opengl3 --path "$P" -s check.gd 2>&1)
if echo "$OUT" | grep -q "SHADER ERROR\|Shader compilation failed"; then
  echo "$OUT" | grep -B2 -A3 "SHADER ERROR"; exit 1
fi
echo "$OUT" | grep -q DONE && echo "着色器编译通过"
