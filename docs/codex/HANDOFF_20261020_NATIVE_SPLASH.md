# Codex 专用交接：原版水花修复及后续数据导出

**本文仅供后续 Codex 接手使用，Claude 无需阅读。** 它不是给 Claude 的执行指令，也不改变双方现有分工。更新日期：2026-10-20。

## 1. 当前结论与用户要求

用户认可当前水花版本，已要求并确认推送。随后要求完成本文，再拉取最新分支，严格执行 `docs/codex/codex_p3r_camp_pages_rip.md`，只读导出 ITEM、SKILL、QUEST 页面数据，写 README，提交并推送。

本次数据导出不改代码、不生图、不启动游戏；必须遵守该构建文档末尾全部禁令，遇到文档未覆盖的情况停下报告。不要自行开始子页面 UI 实现或再次安装游戏。

用户偏好：尽快交付，少问已授权的问题，简短汇报。不能把离线预览、编译或静态检查当成实机通过。安装由 Codex 完成，游戏由用户自己启动和操作。

## 2. 仓库与本机位置

- 仓库：`C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload`。
- 远端：`https://github.com/4yndm7wh95-spec/persona.git`。
- 分支：`claude/amazing-darwin-b00arn`。
- 本聊天工作区：`C:/Users/kkk/Documents/Codex/2026-10-19/claude-amazing-darwin-b00arn-docs-codex`。中间文件在 `work/`，交付文件在 `outputs/`。
- 原版 P3R 包：`G:/Gamebox/ZS15858/GAME15858/GAME15858/P3R/Content/Paks`，注意有两层 GAME15858。只读。
- 原版程序：同一 P3R 根下 `Binaries/Win64/P3R.exe`，只做过静态分析，没有运行它。
- 之前的导出缓存与 CUE4Parse 环境：`C:/Users/kkk/sts2_tools/p3r_camp_20261019`。
- 本次新数据任务中间目录应按任务文档放在 `C:/Users/kkk/sts2_tools/p3r_pages_<日期>/`。
- 游戏日志：`C:/Users/kkk/AppData/Roaming/SlayTheSpire2/logs/`，`godot.log` 与带时间的轮转日志均存在。读日志要匹配真实版本和测试时段，不凭旧日志判断新包。

所有 Git 命令带 `-c safe.directory=<仓库绝对路径>`。先 fetch，再 fast-forward 或普通 merge；不 rebase / reset / amend / force，不改全局配置。协同规则见 `docs/codex/README.md`。只提交自己创建/修改的文件，忽略目录不要移动、删除或加入提交。

当前一直保留的无关未跟踪目录：`art/treasure_hands/`、`docs/qa/v0.14.69/`、`mod_build/`、`art/p3menu_system/ref/menu_transition_frames_020-107/frames/`；`mod/_build/` 同样原样保留。

## 3. 已安装版本与提交

已安装的代码基点为 **3d01a404**（原版水花修复），包含 Claude 的 **a2854303 / v0.14.100** 抽牌堆标题调整，以及此前首次点击性能优化。

交接前又同步到 **32c1a46f**；`3d01a404..32c1a46f` 仅改 `docs/ART_SPEC.md` 和新增数据导出任务文档，没有代码或资源变更，不需要重装。

两个游戏均已安装 P3Makoto **0.14.100**：

| 游戏 | 实际 P3Makoto 目录 |
|---|---|
| v0.111.0 | `C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/mods/P3Makoto` |
| v0.107.1 | `C:/Users/kkk/Downloads/Slay the Spire 2/mods/P3Makoto/P3Makoto` |

- 两边 DLL SHA-256：`A046B4B60DFB6C90CEFA3923BADB979B2450763160876FC2F32E69B99AA914B2`。
- 两边 PCK SHA-256：`478941C988F134383E315F3CFCEF5F24FE738836724F145D3EAEFEC385A4A793`。
- PCK：127,313,444 字节；本地构建产物 `mod/_build/P3Makoto_native_splash.pck`。
- 备份：`mod/_build/backup_native100_20261020_082946_111/` 和同名 `_107/`。
- 音乐盒 1.1.3 两边更新；测试工具 1.2.2 只更新原先装有该工具的 111 版本。歌曲、歌词、其他模组和存档未动。
- 安装详情、完整 API 检查：`docs/qa/v0.14.100_native_splash_20261020.md`，证据在 `docs/qa/native_splash_20261020/`。

## 4. 已做的水花修复：不要回退

原生程序实际选帧逻辑已找到，证据不是视频采样估计：

```text
n = roundf(float32(elapsed) * 30f)
n == 0：单独复制分支，不绘制水花
0 < n < 24：sprite = clamp(n < 15 ? 140+n/2 : 147+(n-14)/3, 140,149)
0 < n < 10：mask = PLG_UI_Camp[clamp(12+n/2,12,16)]
n >= 24：结束水花覆盖层
```

这里除法为整数除法。前段大多 66.7ms/张，尾段 100ms，最后一张约 133.3ms；默认结束阈值约 0.7833s。**原作也不是每秒 60 张不同的水花。** 60 FPS 输出不等于水花形态每秒变化 60 次。

- `SPR_UI_Camp` 编号 140–149 都是 960×948 画布。旧版末两张按 908×600 裁切，第二格 x 起点也错了；已根据真实 UV 纠正。
- 每次只绘制一张水花。用户明确不喜欢相邻帧交叉淡化带来的杂乱与晕感，不要重新加回。
- 去掉自行添加的整层尾段淡出。
- `PLG_UI_Camp[12..16]` 对应五份 `In_Mask_00..04`，替换旧的水花 alpha 累计遮罩；水面与人物共用露出遮罩。
- 五份遮罩离线渲染为 1280×720 L8；后台解码、提前上传。播放中不生成几何、不解析 JSON、不新建位移纹理。
- In75 / Loop_L210 水面动画通过一张缓存 RGF 纹理衔接，不能在入场末尾将形变归零，否则又会“突然掰正”。
- 立绘黑色飘带、耳机线、眼线保护及背面法线处理保留。
- 用户已撤回“左半边非水域”实验，**不要重新加入**。

主要文件：`mod/src/Core/DiveTimeline.cs`、`mod/src/Vfx/DiveFx.cs`、`HeroStage.cs`、`Shaders.cs`；原始 UV/时间数据 `research/p3r_camp/anim/camp_splash_native.json`；资源生成 `tools/p3r/cut_splash.py`、`dive_geometry.py`、`mask_render/`。

`mask_05..09.png` 是保留的旧文件，不再加载或使用；`mask_blank.png` 为初始隐藏态。不要按旧构建文档的“21 个文件”误删新资源，当前 hero3d/dive 是 23 个文件。

日志识别：`splash=native30tick24 masks=original5`。只有 `init v0.14.100` 不足以区分同版本的水花热修复。

## 5. 已验证与尚未证明

已完成：三个模组 Release 构建均 0 警告/0 错误；两个版本 API 检查通过（1174/249/382 处引用）；26 项时间轴/遮罩/卡顿后定位/水面循环检查通过；180 个生产时间点与预览一致；十张水花和五份遮罩像素一致；D3D12 Mobile 九项透明度/裁剪/人物遮罩恢复检查通过；安装哈希一致。

本轮未重复 OpenGL 检查。引擎有 PSO caching 尚未实现的提示，未将该提示当作修复成功或失败证据。

原作全部渲染目标、公共后处理和网格插值链尚未完全还原。不要声称已经找到原作所有连续运动数据，或承诺已根治全部设备的掉帧。

预览：本聊天 `outputs/入水水花_原生时序预览/`，`native.mp4` 1280×720、60 FPS、3 秒；当前版/新版同步对照、慢放、逐帧。服务器曾在 `127.0.0.1:8788`，新会话需先检查是否仍运行，不能盲目启动或结束其他进程。

## 6. 性能与旧 BUG 状态

首次开抽牌堆：保留 6a78bb7d 及后续合并中的轻量固定文案字体、延迟构建特写/名牌、分块字形预热、资源预取。用户曾反馈明显改善；不代表所有首次卡顿都已根治。别恢复全量字体/MSDF 的首次生成路径。

以下四项见 `docs/qa/player_reported_bugs_20261020.md`，不能概称“都已修好”：

| 问题 | 当前状态 |
|---|---|
| 涅槃之怒/万向斩/高伤害后慢放 | 缺对应日志，待当前版复现；时间倍率恢复测试通过不等于已根治 |
| 战后选牌手柄左右颠倒 | 尚无针对奖励界面的确定修复 |
| 看牌强制按类型 | 抽牌堆已有原版顺序/按类型切换并记忆；整副牌组仍分类 |
| 建筑师特殊对话/CG 手柄 | 有部分确认输入处理，完整流程未验收 |

## 7. 后续页面任务的正确起点

先重新读取最新 `docs/codex/codex_p3r_camp_pages_rip.md`，按它执行；不要仅凭本文缩小范围。交接检查时 `research/p3r_camp/pages/` 尚不存在。

最新计划：弃牌堆=ITEM，消耗堆=SKILL；设置分类入口复用入水页，四类详情分别为 游戏=COMMU、视频=ITEM、音频=SKILL、操作=QUEST。此计划来自仓库文档；当前用户只授权数据导出，未授权实现这些新页面。

文档要求的范围不止三个页面目录，还包括 Root 未导出项、Textures、PartyPanel、KeyHelp、营地 BMD、多语言 zh-Hans/zh-Hant/en、Xrd777/Astrea 两套、转场相关引用及缺少的人物动作。不要把精灵切片数或 UIM 帧数当作 FPS；原数据没有时长/FPS 就明确写未提供/没找到。

本机工具：dotnet=`C:/Users/kkk/sts2_tools/dotnet/dotnet.exe`；Python=`C:/Users/kkk/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`；Godot=`C:/Users/kkk/sts2_tools/godot/Godot_v4.5.1-stable_win64_console.exe`。CUE4Parse 及 Conversion DLL 在旧导出缓存的 `bin/Release/net10.0/`，支持 UE4.27。旧缓存已导出大量纹理、UIM、PLG、人物动作，可复用，但不要覆盖已确认的数据。

严格禁止读取 `C:/Users/kkk/.p3record/obs.json`、密码或账户秘密；AES 不写入仓库、README、报告。不要为了提取密钥而读取整个个人配置。遵守新任务限定的只读资源路径、输出范围、20MB 单文件提交上限及明确的缺失项报告要求。

完成数据任务后只需中文简报：各页导出内容、各段动画时长或未找到、缺失项、提交号。不要替用户启动游戏，不把尚未实现的页面写成已交付。
