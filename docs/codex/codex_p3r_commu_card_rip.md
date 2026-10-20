你在用户的 Windows 电脑上工作。任务：从本机 P3R 资源里**只读地**补导出营地菜单 COMMU 页缺的东西（结城理手里那张牌的道具、原版动作的缩放轨、COMMU 页的 UIM 动画），放进仓库。不改代码，不生图，不启动游戏。
（上一次的营地导出在 research/p3r_camp/，工具、AES 配置和方法沿用那次和 art/p3r_tex/README_v14.md，中间文件放 C:\Users\kkk\sts2_tools\p3r_commu_<日期>\。）

【背景】
- mod 的抽牌堆界面要换成原版 COMMU 页：结城理手持一张牌 → 松手，牌浮在空中自转。Claude 已经用 research/p3r_camp/hero3d/ 的原版模型 + 原版动作 + 原版镜头把人物渲染对了，但发现三处缺口：
  1. **那张牌不在模型里。** SK_PC0051_C002 里挂在手上的道具（材质 MI_PC0051_C002_03_CaFiOp，整块绑在骨骼 Jnt_L_AttachHand01_00）是召唤器，不是牌；另外材质 04 / 05 / 06 的三块道具分别绑在 Jnt_C_AttachRoot_04 / _05_06 / _07 上，在 COMMU 动作里会出现在画面里（像椅子 / 圆垫），原版截图里没有。
  2. **PSA 丢了缩放轨。** AnimSequence JSON 显示 ScaleCompressionFormat = ACF_Identity（有缩放轨），但 ActorX PSA 只存位置和旋转。原版很可能是用骨骼缩放 = 0 来藏道具，所以要把缩放轨导出来。
  3. **COMMU 页的 UI 动画没导。** research/p3r_camp/anim/ 里只有 6 个 UIM，COMMU 页只有 WaterLight 的 UIM 在 index.csv 里，没有导出成 anim 格式。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/、mod_build/、art/p3menu_system/ref/menu_transition_frames_020-107/frames/ 原样保留、忽略它们继续（不要移动、删除或提交）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。
6. 稀疏检出缺 research/p3r_camp 时，允许 git -c safe.directory=<repo> sparse-checkout add research/p3r_camp（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 缩放轨（必须做）】
对 P3R/Content/Xrd777/Characters/Player/PC0051/AnmUI/ 下全部 26 个 AnimSequence：
1. 用 CUE4Parse 解压出逐帧骨骼变换（和导 PSA 用的是同一份数据，只是这次把 Scale 也拿出来）。
2. 输出到 <repo>\research\p3r_camp\hero3d\scale\：
   - <动画名>.bin：float32，帧 × 骨骼 × [sx, sy, sz]，UE 原始值不换算；骨骼顺序和同名 PSA 的 BONENAMES 一致（PSA 里是 309 根）；
   - <动画名>.json：{"bones": [...], "frames": NumFrames, "sequence_length": SequenceLength}。
3. 汇总 <repo>\research\p3r_camp\hero3d\scale\summary.csv：每个动画里**缩放偏离 1 超过 0.001 的骨骼**一行 —— 动画名、骨骼名、最小值、最大值、第一次 / 最后一次偏离的帧号。（重点看 Jnt_*_Attach*、Jnt_C_AttachRoot_0x。）
4. 自检：随便挑 1 个动画，把同一帧同一骨骼的位置 / 旋转和对应 PSA 比一下，确认骨骼顺序对得上，写进 README。

【3. 那张牌（必须做，找不到也要写清楚找过哪里）】
找 COMMU 页结城理手里那张牌是哪个资源、怎么挂上去的：
1. 搜索范围（大小写不敏感，全部已挂载内容）：
   - 路径：Xrd777/Characters/Player/PC0051/、Xrd777/Characters/Weapon/ 或含 Prop / Item / Acc 的目录、Xrd777/UI/Camp/、Xrd777/Blueprints/UI/Community/、Xrd777/Blueprints/Camp/、Xrd777/Effects/ 或含 Niagara / FX 的目录；
   - 关键词：Card、Tarot、Arcana、Commu、Cmm、Social、Camp、Attach、Hand、Prop、SM_、SK_、NS_、P_；
   - 重点打开：BP_CampCharaModel（Xrd777 和 Astrea 两个版本）的 SimpleConstructionScript / 组件和函数里有没有挂道具、切材质（MI_CH_Hide）、设骨骼缩放的地方；BP_UICmmCardCapture、BP_UICmmRankUp、BP_UICmmRankUPAnim（它们里面的牌网格可能就是同一张）；营地菜单 COMMU 相关的蓝图里对 Jnt_R_AttachHand01_00 / Jnt_L_AttachHand01_00 的引用。
2. 找到牌的网格就导出到 <repo>\research\p3r_camp\commu_card\：
   - 网格 GLB（+ 同名 PSK / PSKX）；
   - 用到的贴图 PNG（原尺寸、保留透明）、材质 / 材质实例的属性 JSON；
   - 挂载方式：挂在哪个骨骼 / 插槽、相对变换（位置 / 旋转 / 缩放原值）、出现 / 消失 / 浮起 / 旋转是谁驱动的（动画骨骼？蓝图时间轴？曲线？Niagara？）—— 相关蓝图 / 曲线 / 时间轴的完整属性 JSON 原样保存，再在 README 里用中文说清楚。
   - 牌在空中转动如果是蓝图或曲线驱动的，把转速、轴向、曲线关键帧原值列出来。
3. 也说明：召唤器（材质 03）和材质 04 / 05 / 06 那三块道具在营地菜单里是怎么藏起来的（缩放轨 / MI_CH_Hide 换材质 / 组件隐藏 / 别的），找不到就写"没找到"。

【4. COMMU 页 UI 动画】
1. 把 P3R/Content/Xrd777/UI/Camp/Commu/ 下全部 UIM 和贴图，以及营地根菜单切到 COMMU 页时用到的 UIM（名字含 Commu / Cmm / Camp_In / Camp_Out / Change / Select 的），按上次 research/p3r_camp/anim/ 的格式整理成 anim\<名字>__<名字>.json（时间、顶点 / 位置、透明度、插值原值都放进去），贴图导出到 tex\ 对应目录。上次导出过的不用重复。
2. index.csv 里 COMMU 页的 Bustup_XXXX（2048×2048）是社群对象的立绘，不用导。

【5. README】
<repo>\research\p3r_camp\commu_card\README.md（中文）：
- 第 2 步：输出格式、自检结果、summary.csv 里看到的规律（例如"COMMU 动作里 Jnt_C_AttachRoot_04 整段缩放为 0"）；
- 第 3 步：牌是哪个资源、挂在哪、怎么动；召唤器和那三块道具怎么藏；找不到的写清楚搜过的路径和关键词；
- 第 4 步：导出了哪些 UIM、各自时长；
- 用的工具和版本；AES 只写"沿用 README_v14 的 FModel 配置"，不要写密钥。
体积：单个文件超过 20MB 的不提交，在 README 里列出。

【6. 提交与推送】
1. 只 add：
   git -c safe.directory=<repo> add research/p3r_camp/hero3d/scale research/p3r_camp/commu_card research/p3r_camp/anim research/p3r_camp/tex
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P3R camp COMMU card prop, anim scale tracks, COMMU UIM"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告（简短）：牌找到没有、是什么、怎么挂怎么动；道具怎么藏；缩放轨有哪些发现；提交号。

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
