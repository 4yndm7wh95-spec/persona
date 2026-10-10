# 构建与安装

## 产物
`游戏目录/mods/P3Makoto/` 下三个文件：
- `P3Makoto.json`：清单（`mod/P3Makoto.json`）
- `P3Makoto.dll`：特效与补丁代码（`mod/` 下 `dotnet build -c Release`）
- `P3Makoto.pck`：骨骼动画资源（`mod/pck_project` 用 Godot 4.5.1 导出）

## 骨骼资源流程
1. Claude 在云端运行 `python3 tools/rig/build_rig.py`：切零件、生成 Spine JSON / atlas、自动校验、导出 GIF 预览到 `art/rig_preview/`，并同步到 `mod/pck_project/animations/characters/regent/`。
2. 本地（Codex）——不需要 Godot，也不需要 Spine 编辑器插件：
   - `mod/pck_stage/` 已经是完整的 res:// 结构：Spine 资源是"导入后格式"（`.godot/imported/*.spatlas`、`*.spjson` + `.import` 映射），
     贴图也已经由 `tools/pck/godot_tex.py` 生成好了 `.import` 和 `.godot/imported/*.ctex`（无损 WebP，沿用原版 uid）。
   - 只需用 GDRE Tools 打包：`gdre_tools --headless --pck-create=<pck_stage 的副本> --output=P3Makoto.pck --pck-version=3 --pck-engine-version=4.5.1`。

## PCK 里替换了什么
| 资源 | 生成方式 |
|---|---|
| 战斗骨骼 `animations/characters/regent/*` | `tools/rig/build_rig_v4.py` |
| 商店人物 `animations/merchant/regent/regent_merchant_skel_data.tres` | 手写：改指向战斗骨骼（原版商店也是复用战斗骨骼 + 另一套图集） |
| 左上角头像 / 外轮廓、地图标记、选人按钮头像（含未解锁剪影） | `tools/ui/build_ui_assets.py` |
| 选人界面背景 | 不在 PCK 里：`mod/src/Scenes/CharSelectBg.cs` 运行时渲染 |
| 君王之刃配色 | 不在 PCK 里：`mod/src/Scenes/BladeReskin.cs` 运行时换色 |

## 冲突
Mesugaki（另一个储君换皮 mod）覆盖同一批文件，测试本 mod 时需要把它移出 mods 文件夹。
