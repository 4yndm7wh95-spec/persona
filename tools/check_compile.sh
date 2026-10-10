#!/usr/bin/env bash
# 云端编译检查（Claude 用）：用 .NET 8 SDK 自带的 csc，对照 .NET 9 引用程序集 + 游戏 DLL 编译 mod 源码。
# 依赖：research/libs/{sts2,GodotSharp,0Harmony}.dll（由 Codex 从游戏目录复制推送）
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CACHE="${HOME}/.cache/p3makoto"
REF="$CACHE/ref9/ref/net9.0"
if [ ! -d "$REF" ]; then
  mkdir -p "$CACHE/ref9"
  curl -sSL -o "$CACHE/ref9.nupkg" https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.ref/9.0.0/microsoft.netcore.app.ref.9.0.0.nupkg
  (cd "$CACHE/ref9" && unzip -q -o ../ref9.nupkg)
fi
CSC="$(find /usr/lib/dotnet /usr/share/dotnet -name csc.dll 2>/dev/null | head -1)"
LIBS="$ROOT/research/libs"
ARGS=(-noconfig -nostdlib -nologo -target:library -langversion:13.0 -nullable:enable -unsafe -optimize
      -out:"$CACHE/P3Makoto.dll"
      -r:"$LIBS/sts2.dll" -r:"$LIBS/GodotSharp.dll" -r:"$LIBS/0Harmony.dll")
for f in "$REF"/*.dll; do ARGS+=(-r:"$f"); done
mapfile -t SRC < <(find "$ROOT/mod/src" -name '*.cs')
dotnet "$CSC" "${ARGS[@]}" "${SRC[@]}"
echo "compile OK -> $CACHE/P3Makoto.dll"
