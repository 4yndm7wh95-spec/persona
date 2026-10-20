你在用户的 Windows 电脑上工作。任务：从本机 P3R 资源里**只读地**导出营地菜单 4 个子页面（ITEM、SKILL、SYSTEM 设置 CONFIG、背景音乐 CONFIG BGM）复刻 UI 动画要用的全部原版数据，放进仓库。不改代码，不生图，不启动游戏。
（工具、AES 配置和方法沿用 research/p3r_camp/ 那次导出和 art/p3r_tex/README_v14.md；中间文件放 C:\Users\kkk\sts2_tools\p3r_pages_<日期>\。导出格式照 research/p3r_camp/commu_card/README.md 那次。）

【背景】
用户定的分工：弃牌堆 = 原版 ITEM 页，消耗堆 = 原版 SKILL 页；ESC 设置沿用已做好的入水动画，设置里四个分类各一套 UI 动画：
游戏设置 = 原版 SYSTEM → CONFIG 页，音频设置 = CONFIG BGM 页，视频设置复用 ITEM 页，操作设置复用 SKILL 页（或抽牌堆的 COMMU 页）。
现在仓库里只有这几页的人物动作（research/p3r_camp/hero3d/）、页级镜头偏移（DT_CampCameraTable）和版式 DataTable（Layout/），
缺页面本身的 UIM 动画、贴图、PLG 字形和文字。research/p3r_camp/index.csv 里这些目录标的是 "no"（没导出），而且 index 每个目录只列了 60 行，实际文件可能更多。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. git -c safe.directory=<repo> status -sb（协同规则见 docs/codex/README.md「和 Claude 同时改代码」）：
   - 有进行中的合并 / rebase：停下来报告。
   - 有你（Codex）在别的任务里改了、还没提交的代码：先按 README 的规则提交，再继续；说不清来源的改动：停下来报告。
   - 未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/、mod_build/、art/p3menu_system/ref/menu_transition_frames_020-107/frames/ 原样保留、忽略。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先 git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；
   不能快进时改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn；有冲突就停下来报告。
6. 稀疏检出缺 research/p3r_camp 时，允许 git -c safe.directory=<repo> sparse-checkout add research/p3r_camp（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 要导出的范围（大小写不敏感，全部已挂载内容，Xrd777 和 Astrea 两套都要；L10N 只要 zh-Hans / zh-Hant / en）】
1. 目录（整个目录递归，每个资源都导）：
   - P3R/Content/Xrd777/UI/Camp/Item/
   - P3R/Content/Xrd777/UI/Camp/Skill/（已导出的 3 个 Gun UIM 不用重复）
   - P3R/Content/Xrd777/UI/Camp/System/
   - P3R/Content/Xrd777/UI/Camp/Config/
   - P3R/Content/Xrd777/UI/Camp/Root/ 里还没导出的（对照 index.csv 的 repo_exported 列）
   - P3R/Content/Xrd777/UI/Camp/Textures/、P3R/Content/Xrd777/UI/PartyPanel/、营地用到的 KeyHelp（按键提示）
   - 营地页面文字：BMD_CampSystem、BMD_CampSystemExt（及 L10N 版本）
2. 再按名字搜（任何目录）：含 Camp 且含 Item / Skill / System / Config / Bgm / Sound / Party / Member / Tab / Cursor / Scroll / List / In / Out / Change / Select 的 UIM、PLG、材质、贴图、WidgetBlueprint、DataTable、CurveFloat / CurveLinearColor；
   以及营地主菜单 → 子页面、子页面 → 营地主菜单、子页面之间 LB / RB 切换时调用的 UIM（在 BP / WBP 的函数里找引用）。
3. 营地 UI 的逻辑：营地菜单是哪个 Widget / Actor 类驱动的（WBP_Camp*、BP_Camp*、原生类名），把它们和 ITEM / SKILL / SYSTEM / CONFIG 相关的函数、时间轴、动画轨（UWidgetAnimation 的 MovieScene 轨道：位置 / 缩放 / 透明度 / 材质参数的关键帧原值和插值方式）完整属性 JSON 原样保存。
4. 已有的人物动作（research/p3r_camp/hero3d/animations*/ 的 Skill / Item / SYSTEMTOP / SYSTEMCONFIG / SYSTEMCONFIGBGM 及各自 Loop / Cancel）不用重导；
   如果还有这些页的其他动作（例如 ItemCancel、SkillCancel、SYSTEM 返回）没在仓库里，补导 PSA + AnimSequence JSON + 缩放轨（格式同 hero3d/scale/）。

【3. 输出（<repo>\research\p3r_camp\pages\）】
- uim\<名字>.json：UIM 原始 JSON + 按 research/p3r_camp/anim/ 的格式整理的版本（时间、顶点 / 位置、缩放、旋转、透明度、颜色、插值原值都放进去；frameNum / fps 原样记录，原始资产没有就写"未提供"）。
- tex\<原路径>\<名字>.png：贴图原尺寸、保留透明；同名 .json 存贴图属性（尺寸、压缩、SRGB、寻址）。图集（一张图里拼很多小块）要连同 UV 的来源（哪个 UIM / PLG / 材质用了哪块）一起导。
- plg\：PLG 原始 JSON + 按 research/p3r_camp/plg/ 的方式渲染的 PNG。
- widget\：WBP / BP / UWidgetAnimation / 曲线 / DataTable 的完整属性 JSON。
- text\：BMD 导成 UTF-8 JSON（消息 ID → 文本），zh-Hans / zh-Hant / en 各一份。
- manifest.csv：每个导出文件一行——原 pak 路径、类型、导出后路径、大小、属于哪一页（ITEM / SKILL / SYSTEM / CONFIG / CONFIG_BGM / 公用）。
单个文件超过 20MB 的不提交，在 README 里列出。

【4. README（<repo>\research\p3r_camp\pages\README.md，中文）】
按页写（ITEM / SKILL / SYSTEM / CONFIG / CONFIG BGM / 公用）：
1. 这一页由哪些 UIM / 贴图 / PLG / Widget 组成；
2. 进入、待机循环、光标移动、LB/RB 切换、退出各用哪段动画，各自时长、帧率、关键帧时间点（找得到就写原值，找不到写"没找到"并写清楚搜过哪里）；
3. 人物动作和 UI 动画谁先谁后（有延时 / 事件的写原值）；
4. 没导出 / 导不出来的资源和原因。
工具和版本照写；AES 只写"沿用 README_v14 的 FModel 配置"，不要写密钥。

【5. 提交与推送】
1. 只 add：git -c safe.directory=<repo> add research/p3r_camp/pages research/p3r_camp/hero3d
2. 提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P3R camp ITEM / SKILL / SYSTEM / CONFIG / CONFIG BGM page UI data"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 中文简短报告：每页导出了什么、动画时长、缺了什么；提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动），也不要用 Computer Use。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码；AES 密钥只在本机工具配置里用，不要写进仓库、README 或报告。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（pak 只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
