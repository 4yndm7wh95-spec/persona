#!/usr/bin/env bash
# 云端类型检查（没有 .NET 9 SDK 时）：用 .NET 8 SDK 自带的 csc + NuGet 上的 .NET 9 引用程序集 + research/libs 里的游戏程序集编译 mod/src。
# 只做检查，不产出要安装的 DLL（正式 DLL 由本机 .NET 9 SDK 构建）。
set -e
cd "$(dirname "$0")/.."
R=~/.nuget/packages/microsoft.netcore.app.ref/9.0.0/ref/net9.0
if [ ! -d "$R" ]; then
  T=$(mktemp -d); printf '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageDownload Include="Microsoft.NETCore.App.Ref" Version="[9.0.0]" /></ItemGroup></Project>' > "$T/r.csproj"
  dotnet restore "$T/r.csproj" >/dev/null
fi
CSC=$(ls -d /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | tail -1)
dotnet "$CSC" -nologo -target:library -nullable:enable -unsafe -langversion:12 -out:/tmp/p3makoto_check.dll \
  $(for f in "$R"/*.dll; do echo -n "-r:$f "; done) -r:research/libs/sts2.dll -r:research/libs/0Harmony.dll -r:research/libs/GodotSharp.dll \
  $(find mod/src -name "*.cs") 2>&1 | grep -E " error " && exit 1 || echo "类型检查通过"
