# 构建与安装

## 产物
`游戏目录/mods/P3Makoto/` 下三个文件：
- `P3Makoto.json`：清单（`mod/P3Makoto.json`）
- `P3Makoto.dll`：特效与补丁代码（`mod/` 下 `dotnet build -c Release`）
- `P3Makoto.pck`：骨骼动画资源（`mod/pck_project` 用 Godot 4.5.1 导出）

## 骨骼资源流程
1. Claude 在云端运行 `python3 tools/rig/build_rig.py`：切零件、生成 Spine JSON / atlas、自动校验、导出 GIF 预览到 `art/rig_preview/`，并同步到 `mod/pck_project/animations/characters/regent/`。
2. 本地（Codex）——不使用 Spine 编辑器插件（游戏自带的只是运行时插件，没有导入器）：
   - `mod/pck_stage/` 已经是 res:// 结构，Spine 资源以"导入后格式"直接给出（`.godot/imported/*.spatlas`、`*.spjson` 和对应的 `.import` 映射）。
   - 只有 `regent.png` 需要标准 Godot 4.5.1 导入：建一个只含这张图的临时工程，`--import` 后把生成的 `regent.png.import` 和 `.godot/imported/regent.png-*.ctex` 复制进打包目录。
   - 用 GDRE Tools 打包：`gdre_tools --headless --pck-create=<打包目录> --output=P3Makoto.pck --pck-version=<与游戏一致> --pck-engine-version=4.5.1`。

## 冲突
Mesugaki（另一个储君换皮 mod）覆盖同一批文件，测试本 mod 时需要把它移出 mods 文件夹。
