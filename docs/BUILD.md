# 构建与安装

## 产物
`游戏目录/mods/P3Makoto/` 下三个文件：
- `P3Makoto.json`：清单（`mod/P3Makoto.json`）
- `P3Makoto.dll`：特效与补丁代码（`mod/` 下 `dotnet build -c Release`）
- `P3Makoto.pck`：骨骼动画资源（`mod/pck_project` 用 Godot 4.5.1 导出）

## 骨骼资源流程
1. Claude 在云端运行 `python3 tools/rig/build_rig.py`：切零件、生成 Spine JSON / atlas、自动校验、导出 GIF 预览到 `art/rig_preview/`，并同步到 `mod/pck_project/animations/characters/regent/`。
2. 本地（Codex）：
   - 安装 Godot 4.5.1 标准版（不需要 .NET 版）。
   - 把游戏解包目录 `addons/spine` 整个复制到 `mod/pck_project/addons/spine`（spine-godot 扩展，含编辑器 dll）。
   - `Godot --headless --path mod/pck_project --import` 导入资源。
   - `Godot --headless --path mod/pck_project --export-pack "PCK" <输出>/P3Makoto.pck` 导出资源包。

## 冲突
Mesugaki（另一个储君换皮 mod）覆盖同一批文件，测试本 mod 时需要把它移出 mods 文件夹。
