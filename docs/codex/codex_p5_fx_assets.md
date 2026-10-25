你在用户的 Windows 电脑上工作。这是**新开的特效素材窗口**：给 P5 Joker mod 的第一批特效准备素材。**能从 P5R 原版里拿的一律用原版**（用户要求贴合 P5R），拿不到的才生图。不改 mod 代码，不启动游戏，不用 Computer Use。

**先读**：docs/p5/FX_PLAN.md（第一批 4 张牌的特效设计）、research/p5r/README.md（资源库怎么用）、docs/codex/codex_p5r_model_fix3.md（亚森模型修复的做法）、tools/p5r/fix_gfd_glb.py。
找仓库、拉取（git fetch + merge，能快进就 --ff-only）、提交推送和 docs/codex/codex_p5r_model_fix.md 一样。

**绝对不要做**：
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；不要 sparse-checkout disable / set。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码；解密密钥 / 配置不要写进仓库、README 或报告。
- 不要修改 P3R、P5R 或杀戮尖塔 2 的游戏原文件；不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- **另一个 Codex 窗口正在做 art/p5/poses 的姿势图**：不要碰 art/p5/poses、art/p5/ref/plan，也不要动它正在改的文件；提交只 add 你自己的目录（见下）。推送被拒就 fetch + merge 再推。
- art/cards 下另一个会话的已修改文件原样保留，不提交、不 stash、不还原。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 遇到没写到的情况，停下来报告。

所有原版导出都从本机资源库 C:\Users\kkk\sts2_tools\p5r_rip 和 index.csv 查，按需单项提取（总量仍在 15 GiB 限额内）。

【1. 亚森（原版模型渲 2D 序列帧）】
- 模型：MODEL/CHARACTER/PERSONA/0201/PS0201.GMD（之前确认的亚森；不是 0000 / 0001）。用 tools/p5r/fix_gfd_glb.py 同样的修复（IBM、无蒙皮网格、--flip-v），去掉 Blur 之类的替身网格。
- 动作：arsene_anim_list.csv 里的动作逐个渲一张总览，找出 **登场 / 待机 / 施法（挥手放技能）** 这三类，报告里写动作名。
- 渲染：Blender 正交相机，**3/4 朝画面右方**（和 Joker 同方向），透明底；着色用平涂 + 两阶明暗（卡通风，接近 P5 的立绘感），加 2~3px 黑色描边（反转外壳或 Freestyle 都行）；贴图用原版。
- 输出 art/p5/fx/arsene/：
  - appear/、idle/、cast/ 三个文件夹，每个是 PNG 序列帧（30fps，高 1024px，裁掉多余透明边但**同一动作所有帧画布大小和锚点一致**），附 anchor.json（脚底 / 身体中心在画布里的位置）；
  - arsene_still.png（施法动作最有张力的一帧，单张，高 1536）；
  - contact_sheet.png（三段动作每段抽 8 帧排开）。
- 渲出来的样子不好（破面、扭曲、贴图错）就停下来报告，附截图，不要硬交。

【2. 原版技能特效贴图】
BATTLE/SKILL/*.BED、BATTLE/EFFECT/*.BED、*.EPL 里嵌着特效用的贴图（DDS / TMX / GNF 等）。按下面清单找，把里面的贴图导出成 PNG（保留 alpha）：
| 用途 | 去哪找 |
|---|---|
| 咒怨 Eiha（单体）、Eigaon（大）、Maeiha（全体）| BATTLE/SKILL 下对应技能 ID 的 BED。技能 ID 从技能表（BATTLE/TABLE 或 TABLE 下的 SKILL / NAME 表，P5R 有英文技能名）查；查不到就按文件大小和贴图内容（红黑 / 紫色火焰）筛 |
| Sukukaja（敏捷提升）| 同上 |
| 枪 | BATTLE/EFFECT/BES_GUN1~10.BED（报告里写哪个看起来是 Joker 的手枪）|
| 斩击命中 | BATTLE/EFFECT/BES_HIT_ZAN.BED；打击命中 BES_HIT_DA.BED；暴击 BES_CRITICALG.BED |
| 召唤 Persona 的蓝焰 | 搜 BATTLE/EFFECT、EFFECT、BATTLE/EVENT 里名字带 PERSONA / SUMMON / KAMEN / MASK 的；也看 Joker 召唤动作的事件 / 特效引用 |
输出 research/p5r/fx/<用途>/：原始贴图 PNG + 每组一张 contact_sheet.png（深灰底上排开，下面写文件名和尺寸）+ sources.csv（来自哪个包、哪个文件、贴图名）。
特效的粒子 / 时间轴数据（EPL 里的发射器参数）能读出来的话，导出成 JSON 放旁边；读不出来就写「读不出来」，不要猜。

【3. 原版战斗 UI 字样】
BATTLE/GUI 全部 PNG 化（之前只解了少量），放 research/p5r/ui/battle_gui/ + contact_sheet；特别找：**技能名的黑底白字斜框**、伤害数字的字体图、WEAK / CRITICAL / TECHNICAL / 1 MORE / MISS 字样、增益 / 减益的箭头图标、状态异常图标。sprites.json 能导出就一起导出。

【4. 音效和语音】
从 SOUND/SKILLSE、SOUND/BATTLE、SOUND/MOTIONSE、BP01 语音里找下面这些，转 OGG 放 research/p5r/fx/audio/：
- Eiha / Eigaon / Maeiha 技能音；Sukukaja 增益音；
- Joker 的枪声、刀斩挥空 / 命中音；
- Persona 召唤（撕面具 + 蓝焰爆开）的音；
- Joker 的语音：「ペルソナ！」「アルセーヌ！」以及攻击时的短喊声（日语版）。
cue 名丢了的话：技能音按技能 ID 对应 SKILLSE 的编号去找；语音按 BP01 的流序号和时长筛出候选。**每类给 3~5 个候选**，写进 research/p5r/fx/audio/candidates.csv（文件、来源、时长、你认为是什么、可信度），让用户试听挑。

【5. 原版找不到的才生图】
只有原版确实没有的（比如切入图的红黑斜框背景）才用 imagegen，真透明底，放 art/p5/fx/gen/，报告里写清楚为什么原版没有。先列出来问用户，不要自己先生成一大批。

【提交】
分组提交推送：亚森 → "art: P5 Arsene 2D frames from original model"（只 add art/p5/fx/arsene）；特效贴图 + UI → "research: P5R skill fx textures and battle GUI"（只 add research/p5r/fx、research/p5r/ui/battle_gui）；音效 → "research: P5R fx audio candidates"。仓库新增总量不超过 300MB，单文件不超过 20MB；超了就只交 contact_sheet 和挑过的，其余留本机并在 local_only.csv 里列出。
报告：中文，research/p5r/fx/REPORT.md：每项找到了什么、来源路径、没找到的和原因、提交号。
