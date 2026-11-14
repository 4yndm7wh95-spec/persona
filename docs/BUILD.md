# 构建与安装

## 产物
`游戏目录/mods/P3Makoto/` 下三个文件：
- `P3Makoto.json`：清单（`mod/P3Makoto.json`）
- `P3Makoto.dll`：特效与补丁代码（`mod/` 下 `dotnet build -c Release`）
- `P3Makoto.pck`：骨骼动画资源（`mod/pck_project` 用 Godot 4.5.1 导出）

从 0.15.34 起，P3Makoto 对游戏 0.111.0 和 0.113.0 发行同一个 ZIP。用 0.111.0 作为编译基线，伤害、商店和快捷键在游戏运行时选择实际接口。构建一次，将同一份 DLL 对照两套游戏程序集验证后打包。

```powershell
dotnet build mod/P3Makoto.csproj -c Release '-p:GameDir=<0.111.0 的游戏目录>'
```

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
| 休息处坐姿 `animations/rest_site/regent/*` | `tools/rig/build_restsite.py`（沿用原版 uid） |
| Persona Thanatos `p3makoto/persona/thanatos*` | `tools/rig/build_persona.py`；`mod/src/Vfx/PersonaFx.cs` 出君王之刃时召唤 |

## 冲突
Mesugaki（另一个储君换皮 mod）覆盖同一批文件，测试本 mod 时需要把它移出 mods 文件夹。

## 独立卡面 Mod（P3CardArt）

主 Mod 自 0.15.32 起不含卡面替换。独立卡面源码、清单和 PCK 源分别在 `cardart/src/`、`cardart/P3CardArt.json` 和 `cardart/pck_stage/`。编译 `cardart/P3CardArt.csproj`，用相同 GDRE 参数将源副本打包为 `P3CardArt.pck`。包内必须只有 `p3cardart/card_art/` 下 94 张 JPG 与 index.txt，原样保留，无 .import。安装到 `mods/P3CardArt/`，可单独启停，不依赖 P3Makoto。主包不得再含 `p3makoto/card_art/` 或 `p3cardart/`。详见 `cardart/README.md`。
