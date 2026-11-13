#!/usr/bin/env bash
# 云端出完整安装包（用户本机下载整个仓库老是中断时用）：DLL + JSON + PCK 打成一个 ZIP。
# 用法：GODOT=<Godot 4.5.1 mono 可执行文件> bash tools/p5/cloud/build_package.sh <输出目录>
# 产物：<输出目录>/P5Joker-<版本>-full.zip（解压后把 P5Joker 文件夹里的 3 个文件覆盖到 <游戏目录>\mods\P5Joker\）。
# 先跑 python3 tools/p5/stage_p5.py 保证 p5mod/pck_stage 是最新的。没进游戏验证过的包要如实告诉用户。
set -e
: "${GODOT:?set GODOT to the Godot 4.5.1 mono binary}"
HERE=$(cd "$(dirname "$0")" && pwd); ROOT=$(cd "$HERE/../../.." && pwd)
OUT=$(mkdir -p "${1:?output dir}" && cd "$1" && pwd)
VER=$(python3 -c "import json;print(json.load(open('$ROOT/p5mod/P5Joker.json'))['version'])")
W=$(mktemp -d); mkdir -p "$W/P5Joker" "$W/empty"
bash "$HERE/check.sh" "$W/P5Joker/P5Joker.dll"
(cd "$ROOT/p5mod/pck_stage" && find . -type f | sed 's|^\./||' | sort) > "$W/files.txt"
(cd "$W/empty" && "$GODOT" --headless --script "$HERE/pack.gd" -- "$ROOT/p5mod/pck_stage" "$W/P5Joker/P5Joker.pck" "$W/files.txt" 2>&1 | tail -1)
cp "$ROOT/p5mod/P5Joker.json" "$W/P5Joker/"
printf 'P5 Joker %s 完整包（云端编译，源码提交 %s）。\n安装：先退出游戏，把 P5Joker 文件夹里的 3 个文件复制到 <游戏目录>\\mods\\P5Joker\\ 覆盖同名文件；覆盖前先备份原文件夹。\n' "$VER" "$(cd "$ROOT" && git rev-parse --short HEAD)" > "$W/READ_ME.txt"
(cd "$W" && zip -q -r "$OUT/P5Joker-$VER-full.zip" P5Joker READ_ME.txt)
ls -la "$OUT/P5Joker-$VER-full.zip"
