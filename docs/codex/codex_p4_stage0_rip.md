# Codex 任务：P4Yu 第 0 阶段资料（原版铁甲战士 + P4G 解包）

你在用户的 Windows 电脑上工作。任务分两部分：
- **A**：从已有的杀戮尖塔 2 解包里复制原版铁甲战士的资源；
- **B**：解包 Persona 4 Golden（Steam 版），拿参考资料。

不改 mod 代码，不启动任何游戏。先做 A，A 提交推送以后再做 B。

背景：下一个 mod 把杀戮尖塔 2 的**铁甲战士**换成 P4 的**鸣上悠**（暂定 mod id `P4Yu`），Persona 是伊邪那岐。人物美术照 P5Joker 走生图路线，P4G 的模型和动作只当姿势参考。P4G 比较老，资料只作部分参考；P4R（Persona 4 Revival）的视频截图由另一个会话去找。

## 拉取和提交
1. 用你上次成功推送的那个仓库，记作 `<repo>`：remote 指向 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`。所有 git 命令都加 `-c safe.directory=<repo>`。
2. 本任务的提交放在分支 `claude/ecstatic-hawking-s9j6es`：
   - 先 fetch：`git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/ecstatic-hawking-s9j6es`，失败等 5 / 10 / 20 / 40 秒重试。
   - 切分支的方式你自己选：`git switch`，或者在同一个盘上 `git worktree add` 一个稀疏检出。
   - 切过去以后先 `merge --ff-only`，不能快进再 `merge --no-edit`。
3. 工作区里别的任务留下的未提交改动原样保留：不提交、不 stash、不还原。只 `git add` 本任务新增的目录（`research/ironclad_assets`、`research/p4g`）。
4. 每做完一块就提交并推送。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。

## A. 原版铁甲战士资源 → `research\ironclad_assets\`
来源：`C:\Users\kkk\sts2_extract`（已有的只读解包，不要重跑 GDRE）。复制时保持原来的 res:// 相对路径，做法照 `research\silent_assets\README.md`。
1. 整个目录复制：
   - `animations\characters\ironclad`
   - `animations\merchant\ironclad`
   - `animations\rest_site\ironclad`
   - `animations\character_select\ironclad`
2. 场景：
   - `scenes\creature_visuals\ironclad.tscn`
   - `scenes\merchant\characters\ironclad_merchant.tscn`
   - `scenes\rest_site\characters\ironclad_rest_site.tscn`
   - `scenes\screens\char_select\char_select_bg_ironclad.tscn`
   - `scenes\ui\character_icons\ironclad_icon.tscn`
   - `scenes\combat\energy_counters\ironclad_energy_counter.tscn`
   - `scenes\vfx\` 下文件名带 ironclad 的全部 .tscn
   - 这些场景引用到的、文件名带 ironclad 的 .tres / .png / 材质
3. UI 图：
   - `images\packed\character_select\char_select_ironclad*.png`
   - `images\ui\top_panel\character_icon_ironclad*.png`
   - `images\packed\map\icons\map_marker_ironclad.png`
   - `images\ui\combat\energy_counters\ironclad\*`
   - `images\ui\transitions\ironclad_transition.png`
   - `materials\transitions\ironclad_transition_mat.tres`
   - `images\vfx\characters\ironclad_*.png`
   - `images\ui\hands\multiplayer_hand_ironclad_*.png`
   - `images\packed\sprite_fonts\ironclad_energy_icon.png`
4. 卡图：`images\packed\card_portraits\ironclad\` 整个目录（含 beta 子目录）。
5. 写 `research\ironclad_assets\README.md`，格式照 silent_assets 那份：游戏版本、来源、复制命令、文件表（路径、字节数、文件里出现的 uid）、场景之间的引用关系。
6. 提交信息：`research: export vanilla Ironclad assets for P4Yu`，提交后推送。

## B. P4G 解包 → `research\p4g\`
**P4G 位置**：在 Steam 库里找 `steamapps\common\Persona 4 Golden`，找不到就搜 `P4G.exe`。

**游戏文件只读**：工具直接读原文件，输出全部放到仓库外面同一个盘上的临时目录，不往游戏目录写任何东西。用户磁盘很紧：只解需要的部分；工具只能整包解的话，解完马上删掉用不到的部分。任务结束时把临时目录删掉。

**工具**：只用便携版或源码直接跑，从 GitHub 官方发布页下载，不运行安装程序，不用管理员权限。下面只是建议，具体用哪个你自己挑能用的：
- CPK：PC 版是 PreApp 的格式，和其他 Persona 不一样，用 TGE 的 preappfile（P4G32 Tool）；
- PAC / BIN 里的 TMX 贴图、RMD 模型：用 Amicitia 或同类工具；
- 音频 ACB / AWB / HCA：用 vgmstream 转 ogg；
- 过场 USM：用 Unp4cker、vgmstream 或 ffmpeg。

**要做的事**（按优先级）：
1. **文件清单**（最重要，以后我按它点名要别的文件）：`research\p4g\file_list.txt`，每个 cpk 里所有文件的完整路径和大小。
2. **2D 美术**：TMX 转成 PNG，保留透明通道。每个子目录写一个 `index.csv`（文件名、原始路径、尺寸、一句话内容）。

   | 目录 | 内容 |
   |---|---|
   | `bustup\yu\` | 鸣上悠所有立绘 / 头像，全部表情、全部服装 |
   | `persona\` | 伊邪那岐、伊邪那岐大神的所有 2D 图（图鉴图、召唤 cut-in 等） |
   | `cutin\` | 战斗 cut-in：1 More、总攻击、暴击、主角切入头像等 |
   | `tarot\` | 塔罗牌：召唤用的牌、Shuffle Time 的牌、愚者牌 |
   | `ui\battle\` | HP / SP 条、指令菜单、技能列表、目标指示、伤害数字、状态和属性图标 |
   | `ui\menu\` | 主菜单、暂停菜单、天气日历画面、天鹅绒房间界面、读档画面 |
   | `fx_tex\` | 技能特效贴图：雷、斩击、召唤时的蓝光和卡牌碎片、火花、尘土 |

3. **音效**：转成 ogg，放 `sfx\`，附 `index.csv`（文件名、原始路径、cue 名或编号、时长）。BGM 先不要。
   - 要这几类：
     - Persona 召唤（卡牌出现、碎裂、喊 "Persona!"）；
     - 主角挥刀（挥空 / 命中 / 暴击）；
     - 雷系魔法（Zio、Mazio、Ziodyne、Maziodyne）；
     - 斩击类物理技能；
     - 受击、倒地、防御；
     - 1 More 和暴击提示音、总攻击、Shuffle Time；
     - 战斗胜利、升级；
     - 菜单操作（光标移动、确认、取消、打开菜单）；
     - 天鹅绒房间进出。
   - 按 cue 名能分出类别的就分进子目录；分不出来的整库转出来放 `sfx\unsorted\`，在 index 里保留 cue 名。
   - 鸣上悠的战斗语音（召唤、攻击、受伤、暴击、胜利）放 `voice\yu\`，日语和英语都有就都要。
4. **3D**（尽力而为，导不出来就跳过）：鸣上悠的战斗模型和武器、伊邪那岐的模型。
   - 导出 glb，能带上骨骼和动作就带上，放 `model\`。
   - 每个模型渲一张正面和一张 3/4 的透明底预览图。
   - 姿势总览这次先不做。
5. **过场动画**：视频不提交。
   - 每段 USM 均匀截 8 张宽 320 的缩略图拼成一行，全部拼成 `movie_frames\overview_XX.jpg`。
   - 开场动画和"觉醒、第一次召唤伊邪那岐"那段，另外按原分辨率截 30 张关键帧，放 `movie_frames\<段名>\`。
6. **说明文件** `research\p4g\README.md`：写游戏版本（exe 版本号）、用了哪些工具和版本、每一步的命令、各目录是什么内容。不写任何密钥。
7. **体积限制**：本部分新增到仓库的总量控制在 600MB 以内，单个文件小于 50MB。超出时优先保留文件清单、2D 美术和音效，其他部分减量。
8. **提交**：按块分开提交并推送，提交信息分别用：
   - `research: P4G file list and 2D art for P4Yu`
   - `research: P4G sfx and voice for P4Yu`
   - `research: P4G models and movie frames for P4Yu`

## 做完以后
在对话里简单说一下：每个目录有多少个文件；哪些没拿到，为什么。不用另写报告文件。遇到这里没写到的情况，按你自己的理解选最合理的做法继续，最后说明怎么处理的。下面的安全要求是硬底线。

## 安全要求（绝对不要做）
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件；不要把密钥写进仓库、README 或对话。
- P4G、杀戮尖塔 2 的游戏文件和 `sts2_extract` 都只读：不修改、不删除、不往里面写东西。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。
- 不要启动任何游戏（杀戮尖塔 2、P4G、P3R、P5R 都不要启动），也不要用 Computer Use。
- 只用便携版工具，不运行安装程序，不用管理员权限；不要绕过安全拒绝或系统权限保护。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`，缺目录可以 `sparse-checkout add`。
- 临时目录用完就删（用户磁盘紧）。
