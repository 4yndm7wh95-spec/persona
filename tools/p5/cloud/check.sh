#!/usr/bin/env bash
# 云端类型检查 / 编译（容器里只有 .NET 8 SDK）：用 SDK 自带的 csc + NuGet 上的 .NET 9 引用程序集 + research/libs 里的游戏程序集编译 p5mod/src。
# 用法：bash tools/p5/cloud/check.sh            只检查，产物丢到 /tmp
#       bash tools/p5/cloud/check.sh <输出.dll>  编出可以安装的 DLL（优化、带内嵌资源 eye_cutin.json）
# 注意：research/libs 是 2026-11-02 的游戏程序集；C# 语言版本按 12 编（项目文件写的是 13，目前代码没用到 13 的语法）。
set -e
cd "$(dirname "$0")/../../.."
R=~/.nuget/packages/microsoft.netcore.app.ref/9.0.0/ref/net9.0
if [ ! -d "$R" ]; then
  T=$(mktemp -d); printf '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageDownload Include="Microsoft.NETCore.App.Ref" Version="[9.0.0]" /></ItemGroup></Project>' > "$T/r.csproj"
  dotnet restore "$T/r.csproj" >/dev/null
fi
CSC=$(ls -d /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | tail -1)
OUT=${1:-/tmp/p5_check.dll}
EXTRA=""; [ -n "$1" ] && EXTRA="-optimize+ -debug- -deterministic"
dotnet "$CSC" -nologo -target:library -nullable:enable -unsafe -langversion:12 $EXTRA -out:"$OUT" \
  -resource:p5mod/data/eye_cutin.json,P5Joker.EyeCutIn.json \
  $(for f in "$R"/*.dll; do echo -n "-r:$f "; done) -r:research/libs/sts2.dll -r:research/libs/0Harmony.dll -r:research/libs/GodotSharp.dll \
  $(find p5mod/src -name "*.cs") 2>&1 | grep -E " error " && exit 1 || echo "类型检查通过"
