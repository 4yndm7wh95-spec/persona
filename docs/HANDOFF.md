# 交接文档（两个会话共用，最新的在最上面）

协作规则见 `docs/COLLAB_RULES.md`（草案，用户还没最终确认）。用户说过：交接文档**只在用户要求交接时写**，不用每次做完都写。

---

## 2026-11-11 · 会话 B（`claude/beautiful-keller-4lsbk4`）交接 —— 群蛇形态（Serpent Form）做到一半，从这里接

### 分支和状态
- 工作分支 `claude/beautiful-keller-4lsbk4`，最新提交 `b2c266c1`，工作区干净、已推送。开始时已合并会话 A 的 `ce7be92a`（之后 A 分支没有新提交）。
- 版本号仍是 **0.9.18**，这一轮所有改动**都没出安装包、没进游戏实测**，只在预览工具里渲染过。类型检查（Roslyn + .NET 9 引用 + `research/libs`）通过。
- 出包：升 0.9.19 → `python3 tools/p5/stage_p5.py` → `tools/p5/cloud/build_package.sh`（见会话 A 的交接）。

### 群蛇形态现在的样子（用户逐项确认过的）
- 代码：`p5mod/src/Vfx/SerpentMantle.cs`（五条咒烟大蛇 + 场景 + 攻击，全部着色器）、`p5mod/src/Vfx/EclipseSky.cs`（日蚀天色，Orochi 也用）、
  `p5mod/src/SerpentFormPlay.cs`（接进游戏：藏原版 `NSerpentFormVfx`、能力挂上时开始、`OnEffectTriggered` 之后那次 `vfx_attack_blunt` 换成蛇咬、能力移除时淡出）、
  `CardSkillFx.SerpentForm`（打出时播出场）。音效 `serpent_maw`（只有咬合声，`tools/p5/sfx/synth_serpent.py`）。说明：`docs/p5/SKILL_FX.md`「群蛇形态」一节。
- 预览：场景 `serpent_form`（`tools/p5/fxpreview/Preview.cs`），最新视频 `docs/p5/preview/serpent_form_v14.mp4`。
- **场景**：会话 A 的日蚀天色，参数 `desat 0.3 / cover 0.62 / disk_r 0.07 / bleed 1 / blend 1`（黑只留中心一小块往外过渡成暗红；云保留但不挡光；光调弱过一次；天地之间不断层）；出场时从 Joker 身边漫开。
- **蛇身**：一根粗细连续变化的管子（脊线 0～27 的圆头小段取并集），比例照 `docs/p5/preview/serpent_body_plan.png` 蓝线（中段 1.1 W、颈根 1.0 W、后 55% 收成细长尾巴；W = 脖子粗细 = 1.16 girth）。
- **蛇头**：照 `docs/p5/preview/serpent_head_plan.png`（点在 `tools/p5/diagrams/serpent_head_plan.py`，C# 里 `Upper/Mouth/JawTop/JawUnder` 同一组点），
  多边形距离场，沿脖子末端方向放在颈根、和管子 smin 成一个形状；下颚绕颚角铰点转（平时 12°，扑咬 40° + 上颚抬 8°，咬住合上）；
  口腔嘴角最深往吻尖淡掉；四颗模糊烟獠牙（上下各一对，近亮远暗）；隐约的分叉舌头；暖红雾光眼睛。头永远顺着脖子（转向只能弯脖子 `BendNeck`）。
- **攻击**：轮到的蛇先猛窜（自己的时间 ×5 再慢下来）边窜边散成烟 → 敌人斜上方一团咒烟里聚出头和前半身（放大 1.45 倍）往后一缩、嘴张大 → 0.46s 扑咬合上 → 敌人闪白（`BodyTint.Pulse`）+ 咒烟炸开 + 闪光火星震屏顿帧 → 散掉、回原位重新聚起。
- **出场**：咒烟一收一炸，五条蛇从 Joker 胸口一条接一条冲出到各自位置；**不再一起转头**（用户说怪），只是眼睛亮一下。
- 一体感：Joker 身后一团「蛇巢」咒烟，五条的尾巴都收进去。

### 正在讨论、还没做的
1. **五条蛇的分布和轨迹**（用户：现在有点乱、不美观）。方案图 `docs/p5/preview/serpent_layout_plan.png`（脚本 `tools/p5/diagrams/serpent_layout_plan.py`）：
   五条从身后蛇巢伸出来展成扇子（左上最高 / 正上 / 右上朝敌人 在身后；右下、左下从他身前横过去挡住腰），头在各自的小范围里慢慢漂、身体是 S 形轻轻波动，不再沿 8 字兜圈打结，各自节奏。
   **已发给用户，还没得到确认**，先问用户这个方案行不行。实现时改的是 `Orbits` + `PoseSerpent` 里「身体沿头走过的路铺开」那段（改成从蛇巢到头的 S 形脊线）；出场的 `grow` 和攻击的 `StrikePose` 要跟着对上。
2. 之后按顺序：**3. Joker 和蛇周围的迷雾**（加强一体感）→ **4. 场景切入动画**（更自然、层层叠进来，大约 1 秒）。
3. 全部过关后：升 0.9.19 出包，用户进游戏试。实机要看：群蛇形态是稀有牌，打出时还有亚森召唤和眼神特写，出场节奏会比预览长；`SerpentFormPlay.TakeHit` 靠 600ms 内的 `vfx_attack_blunt` 认出是群蛇触发。

### 其它这一轮做的（都已推送）
- 技能 / 能力牌通用特效 `Vfx/SkillFx.cs` + `SkillPlay.cs`（会话 A 之后又按牌名做了专属的 `CardSkillFx*.cs`，那些优先）。小刀牌枪声加大。
- 苦无（`KunaiPierce.cs`）**用户说已废弃**，没接进任何牌。Orochi（`Orochi.cs`）只剩天色被复用，大蛇本体不用。

### 用户这一轮的要求 / 教训（接手一定要照做）
- **做一点推一点**：每一小步都提交推送，预览视频和图放进 `docs/p5/preview/`，不要留在本地。
- **一个一个做，做完一个给用户看，过关了再做下一个。** 改形状之前**先画示意图**给用户确认（头、身体、布局都是这样过的），做进游戏要**准确还原示意图**。
- **不要在旧代码上反复打补丁**：头和身体就是因为补丁越改越错位，才整个重写成一个形状的。发现结构不对就重写。
- 不要发调试画面给用户（彩色的距离 / 噪声图会吓到人）；查错自己看。
- 不要粗糙地「涂一块颜色」充当形状（嘴就是这样被骂的）；形状要来自轮廓本身。
- 蛇：暗、像烟、有气势；不要艳、不要光蛇、不要正面大脸、不要上下两排锯齿、不要亮斑白线；獠牙要模糊不要硬三角。日蚀不要煎蛋样的白环。
- 着色器里别用 1e9 这种哨兵值直接进 `smin`（显卡精度会把真实距离吃掉，整条蛇变成半透明带子）。

### 环境（这个容器里）
- Godot 4.5.1 mono：`/opt/tools/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64`；预览：`tools/p5/fxpreview/render.sh <场景> <秒>`（xvfb + `--write-movie`），配音 `python3 tools/p5/fxpreview/mix.py <场景>`。
- 类型检查要用 .NET 9 引用程序集 + `research/libs`（**不是** `research/libs_0.107.1`，那是别的游戏版本）；容器里只有 .NET 8 SDK，用它的 Roslyn `csc.dll` 编。

---

## 2026-11-10 · 会话 A（`claude/epic-archimedes-m9an69`）交接 —— 用户中途叫停「一次做完剩余卡牌特效」，从这里接

### 分支和状态
- 工作分支 `claude/epic-archimedes-m9an69`，从本交接提交接着做。版本号还是 **0.9.18**（`p5mod/P5Joker.json`、`ModEntry.cs`）。
- 仓库里有一个云端编出来的完整安装包：`docs/codex/reports/p5_build_0.9.18_cloud/P5Joker-0.9.18-full.zip`（源码 `48c8a8c0`）。
  **它不包含之后的改动**（蓄力 / 时停、枪声调大）。用户有没有装、实机怎么样，没有反馈。
- 类型检查通过；预览 `sinful_shell` 能渲染。没有未提交的半成品代码。

### 这一轮做完的
1. **大罪穿甲弹（华丽收场）补细节**（`p5mod/src/Vfx/SinfulShell.cs`、`SatanaelPuppet.cs`）
   - 命中区里一串越来越小的光圈（用户手绘的草图）；绕弹道的螺旋气流（`Spiral`）；夜空沿弹道裂开再从两头合上（`SkyCrack`）；
     子弹穿过时敌人整只过曝成冷白（`Setup.EnemyVisuals`，不是描边）。
   - 场景展开 / 收回的边缘、撒旦耶尔的烧线：从四档色阶的粗蓝焰带改成细的热白芯 + 柔光 + 细丝（用户：原来像卡通、漫画）。
2. **蓄力 → 时停 → 开枪**（用户 2026-11-10 要的，推翻了 11-09「不做时停」）
   - 时序常量：`ChargeAt = CatchAt + 0.06`、`ChargeTime = 0.95`、`FreezeTime = 0.3`、`FireAt = FreezeAt + FreezeTime`（≈ 出牌后 2.25s）。整段演出比原来长约 1.1s。
   - 蓄力（`Charge` + `ChargeShader` / `HushShader`）：发丝细光被吸进枪口、套在枪管上的光圈往里收、细电弧、画面渐暗、其他声音压低、地面轻抖。
   - 时停：先闪白，然后场景里自己会动的东西全停（`Fx.HoldClocks` 停着色器时钟和粒子；撒旦耶尔用一条不走的时钟 `pg`；敌人 `ProcessMode = Disabled`）。
     **世界停、能量不停**：一圈光从外面收向枪口、正好在开枪那一刻合上；悬着的细光极慢地往里爬；画面一路变暗。
     第一版（0.42s、完全静止、无声）用户说「像卡住了，不像停顿，太久」→ 改成现在这样后用户说 OK。
   - 开枪：白底黑线 → 黑底白线两帧冲击帧 + 一层白退掉；`MuzzleBlast`（前喷 + 两侧气焰）；两圈大冲击波；灯光加强；连震三下；云层余光。
   - 声音（`tools/p5/sfx/synth_sinful_blast.py`，一条脚本管这两条）：`sinful_charge` = 0.95s 电流「滋」+ 时停里被拖慢掉下去（磁带停转）+ 往上提的气声，
     开枪前 20ms 收住；`sinful_shot` 第三版 = 从静默直接炸开，冲击在 0.02s（`Sfx.Lead`）。**`CHARGE` / `FREEZE` 要和代码里的常量一致。**
   - 预览：`docs/preview/sinful_shell_charge_freeze2_small.mp4`。
3. **云端出安装包的办法**（用户本机下载整个仓库老是中断）：`tools/p5/cloud/check.sh`（类型检查 / 编 DLL）、`pack.gd` + `build_package.sh`（打 PCK、出 ZIP）。
   DLL 是用 .NET 8 SDK 的编译器 + .NET 9 引用程序集 + `research/libs`（11 月 2 日的游戏程序集）编的，和本机 `dotnet build` 不完全一样，**没进游戏验证过**。
   每个完整包约 64 MB，会让仓库变大；只改代码没改资源时只出 DLL + JSON 的增量包（参考 `docs/codex/reports/p5_outbreak_perf_fix_20261107/`）。
4. **小刀牌的枪声调大**：`tools/p5/sfx/five_cards_shots.py` 加了 `SHOT_GAIN = 2.0`（游戏里播放音量已经到顶，只能提高文件本身），
   `gun_shot_1..4` / `gun_shot_final` 峰值约 -8 dB → -3 dB。命中声没动。**用户还没听。**
5. **致命毒药、蛇咬的新特效做了又撤了**（`f402e05c` 做，`446c866a` 撤回）：用户看完说「太糟糕了，还是用之前的」。
   现在这两张牌和以前一样：亚森爪尖的咒怨光束 + 脚下咒火（`PoisonFx.Apply`），蛇咬另有原版獠牙。详见下面「教训」。

### 没做完：剩余卡牌的特效（用户原话：「尽量一次性把剩余的卡牌特效一次性生成完吧，还没做的那些，音效也加上」）
我只做完了盘点，**一行特效代码都还没写**，用户就叫停写交接了。盘点结果（91 张，卡池见 `research/src_full/.../SilentCardPool.cs`）：

**已经有特效的**
- 所有攻击牌：出刀刀光 `KnifeFx.Swing` + 命中 `KnifeFx.Hit` + 打击分档 `Impact`（`HitWeights.cs`）。中和另有 `CardFx.Weaken`。
- 小刀牌（开枪 `GunFx`）、刀扇（转枪 `ShivFx.FanOfKnives` + 弹射）、刀刃陷阱（举枪不收）、华丽收场（大罪穿甲弹）。
- 毒系：致命毒药、蛇咬、咕嘟冒泡、弹跳烧瓶、毒性爆发、腐蚀波；所有上毒（`PoisonApplyPatch`）和每回合毒伤（`PoisonTickPatch`）。
  毒雾、涂毒、带毒刺击、迷雾走通用的上毒落点。

**只有 Joker 的动作、没有特效的（约 50 张）**
- 格挡：防御、偏折、闪躲翻滚、后空翻、逃脱计划、残影（Blur）、生存者、斗篷与匕首、触不可及、扫腿、侧步、预判、手上技法、蜃景、冲刺的格挡部分。
  `JokerFx.OnBlock` 现在是空的（用户 11-04：碎玻璃墙塑料感太强，要用特效直接做）。
- 过牌 / 弃牌 / 能量：杂技、早有准备、独门技术、计算下注、本能反应、战术大师、调制、肾上腺素。
- 生成小刀：刀刃之舞、斗篷与匕首、墨之刃、隐秘匕首、袖里乾坤、钢铁风暴、刀刃交响曲、先制打击（只有弹巢 UI 装弹 `AmmoView`）。
- 减益：萎靡、尖啸、暴露、紧勒、夹击、突然一拳 / 压制 / 扫腿的虚弱、刺杀的易伤。
- 能力：磨蚀、催化剂、精准、余像、涂毒、灵动步法、无尽刀刃、谋划专家、毒雾、幻影之刃、鬼祟、速行者、必备工具、跟踪、计划妥当；爆发、消影、暗影步。
- 稀有：夜魇、子弹时间、融入暗影、幽魂形态、群蛇形态。

**写好但没接进游戏、用户没验收过的半成品**（先渲染出来看质量再决定用不用）
- `Vfx/HexGuard.cs`（六边形蜂巢护盾，着色器版，用户 11-08 要求照结城理 mod 的 Tetrakarn）、`Vfx/GuardWall.cs`、`Vfx/GuardFx.cs`。预览场景 `hex_guard`、`guard_wall`。
- `Vfx/ShivFx.cs` 里的 `BulletTime`、`Afterimage`、`CallingCard`（计划妥当）、`Shadowmeld`。预览场景 `batch2`。

**现状里不合理的地方**（接手时要处理）
- `Casts.cs` 没列出的非能力技能牌默认「扶面具」；罕见及以上会召唤亚森、稀有还有红色舞台 + 眼神特写（`Aim.PlayCast`）。
  所以萎靡、夜魇、肾上腺素（0 费）、爆发、蜃景现在会把亚森召出来出手，但敌人 / 自己身上什么都不发生；尖啸是白卡，只有扶面具的动作。
  肾上腺素和爆发大概应该改成扯手套、不召唤。

**我的打算（只是我的想法，用户没看过、没同意）**：不逐张做，按族做，每族一个函数加参数——格挡一族、过牌弃牌一族、装弹一族、减益一族、能力一族，稀有牌单独处理；
材质只用 `FX_RULES.md` 认可的那一套；白卡要短、简单。素材里有几张没用上的 P5R 原版贴图（`buff_column`、`buff_line`、`chain`、`weak`、`joker_ghost`）。

### 其他待办
- 蓄力 / 时停 + 枪声调大还没出安装包。出包前升版本号到 0.9.19，`python3 tools/p5/stage_p5.py`，再 `tools/p5/cloud/build_package.sh`。
- 时停里「敌人的待机动画停住」只在代码里做了，预览验证不了（预览的敌人是静止的），要实机看。没生效的后果只是敌人照常轻微晃动。
- 0.9.18 实机没反馈的项目：华丽收场接进游戏的整条流程、出牌时卡牌淡出、只响一声的命中、光照、场景圈、各音量。
- `docs/COLLAB_RULES.md` 还是草案。

### 教训（这一轮踩的）
- **平面形状填颜色，怎么柔化边缘都像贴图。** 蛇咬我先做了平面的獠牙（描边版 → 发光版 → 火焰版），用户连说三次不像特效；
  用户给的标杆是**毒性爆发眼神特写背景里那只巨手**（`Vfx/CrushHand.cs`）：着色器里摆 3D 形状、沿视线步进穿过咒烟，主体近乎纯黑，只有最外一层薄壳透红光，
  边缘是烟自己变薄亮出来的。我照这个重做了 3D 的颚，用户仍然说太糟糕（合上后牙床连成一个方框，读不出「咬」）。要做有形状的东西，先想清楚形状本身成不成立。
- **白卡不要复杂。** 用户同意过「一滴 + 脉络 / 颚 + 脉络」的方向，但做出来以后整体否掉了，宁可用原来通用的上毒效果。
- **时停不能是一张静止的画面加静音**，那和卡顿没区别；必须有东西还在动、声音有变化，并且要短。
- 时停时别往画面上加新图案；十字光会被看成准心（用户反感准心、锁定框）。
- `Fx.After`（定时器）比 `Fx.Animate`（补间）晚一帧：闪光这类要和回调里的特效同一帧出现的，从回调记下的时刻算（`SinfulShell` 的 `shotAt`）。
- `render.sh` 的秒数只能是整数；`GODOT` 要在同一条命令里 export；重跑合成脚本会把同一脚本里别的 ogg 也重写（编码不是确定性的），不想动的要 `git checkout`。
- 预览视频要小（854 宽、1 MB 以内），放 `docs/preview/`，大的用户打不开。

### 用户这一轮的决定（已追加到下面的总清单）
- 华丽收场：转完枪先蓄力，开枪前时停，然后一枪要特别响、特别炸裂。时停 0.3s，世界停、能量不停。
- 时停、蓄力里不要准心一样的图案。
- 场景边缘不要卡通 / 漫画感；敌人被光打透不要描边。
- 致命毒药、蛇咬用回之前的效果。
- 小刀牌的枪声大一点。
- 剩余没做特效的牌希望一次做完，带音效。

---

## 2026-11-09 · 会话 A（`claude/epic-archimedes-m9an69`）→ 交给会话 B

### 分支
- **接着做请从 `claude/epic-archimedes-m9an69` 的 `e69381bc`（及本交接提交）开始**：它已经合并了 B 分支（`claude/beautiful-keller-4lsbk4` 到 `1297e6e5` 为止的全部提交），合并冲突都解决了。
- 会话 B 开工：`git fetch origin`，把 `origin/claude/epic-archimedes-m9an69` 合并进自己的分支（B 分支自 `1297e6e5` 以后如果没动过，就是 fast-forward），跑类型检查 + 预览确认能编译、能渲染再动手。
- 合并时踩过的坑（规则 5）：两边各加了 `Sfx.SinfulPierce`、两套场景遮罩（云着色器里两个 `mask_r`），合并后编译不过 / 着色器报错，雷也因此没正常显示。现在只留 B 的 `SceneMask`（云 + 余烬）并加上了撒旦耶尔本体的遮罩。

### 这一轮做完的（华丽收场 / 大罪穿甲弹，都只在预览里，还没接进游戏）
1. **音效时间轴整理**（用户：别叠在一起、不要每个敌人放一次）。从出牌算：
   - 0.05 场景圈展开 `sinful_sweep_open`（轻）→ 0.12 召唤雷 `sinful_thunder` → 0.33 撒旦耶尔转枪 `sinful_twirl` → 0.84 开枪 `sinful_shot`（倒吸 → 静音 → 冲击在 1.14）→ ≈1.27 **打中只响一声** `sinful_pierce_last`（`SinfulShell.HitSound`，落在子弹碰到第一个敌人）→ 2.24 退场雷 → ≈2.74 场景圈收回 `sinful_sweep_close`（轻，末尾一顿）。
   - 去掉了：Joker 转枪声（文件也删了）、接枪金属声 `sinful_catch` 的排期、子弹飞过呼啸 `sinful_flyby` 的排期、`Pierce()` 里每个敌人的受击声。
2. **受击音二选一**：用户听了对比，选 **B 做的 `sinful_pierce_last`**；A 的那版已删。
3. **转枪音效第三版**（`tools/p5/sfx/synth_sinful_twirl.py`，覆盖了 B 的纯风声版）：用户说之前太重、声音怪、太刻意。现在是每半圈一声短促清脆的「咻」、越转越轻，接住时一声很小的「嗒」，代码音量 0.55。**用户还没听这一版**。
4. **子弹 / 命中的小细节**（用户：光波之外加一点层次，不要太多）：`BulletDetails`（弹道上掉落闪烁的小光点、两侧发丝细的气流线）、`HitDetails`（蓝白四角星光 `BlueGlint`、蓝色碎片 `BlueShard`、往上飘的小余烬）。
5. **交接待办 A / B / C 都做了**：
   - A：雷 `_riftX = origin.X + 987 * scale`（body_center），劈在撒旦耶尔身上。
   - B：场景展开 / 收回音效 `tools/p5/sfx/synth_sinful_sweep.py`。
   - C：`AppearAt` 0.3 → 0.12，雷和场景刚烧开同时劈下；抬枪 / 接枪 / 开枪时刻没动。
6. 预览视频：`tools/p5/fxpreview/out/sinful_shell.mp4`（`render.sh sinful_shell 8` + `mix.py`）。

### 没做完 / 待办
**1. 翅膀：Codex 的翼根羽毛已经推上来了（`b233f7a1`，已合并进 A 分支），还没接进游戏、用户还没看**
- 新文件：`layers_v10/wing_root_feathers.png`、`wing_root_feathers_front.png`、`check_wing_roots_v10.png`，`layers.json` 加了两层（z 顺序和父节点看 layers.json）。
- 第一步：把 check 图给用户看，确认翼根羽毛的样子对不对；对了再按下面的步骤接进游戏。
- 用户指出：原版**不是六片鸟类羽毛翅膀**。B 写的 `codex_p5_satanael_feather_wings.md` 已标作废（用户贴过一张按它生成的「六片全羽毛」图，是错的）。
- 正确理解（对照 model_ref/turn_front_cel、turn_back_cel 和原版切入）：六片大翼本身是**硬的蝙蝠 / 镰刀翼**（翼骨 + 翼膜 + 金色骷髅，= 现在 layers_v10，结构对）；**羽毛只在翼根**——肩胛 / 背上一大簇层层叠叠、末端撕裂状的黑色羽毛，盖住每片翼的根部；原版镜头里显得全是蓝色羽毛，是因为近景拍的是翼根、蓝光把金色照成深蓝、翼骨边打了青色亮线。
- 新任务单：`docs/codex/codex_p5_satanael_wing_roots.md`（只新画 `wing_root_feathers.png`，可能还有 `wing_root_feathers_front.png`；六片翼和身体不动；推到 A 分支）。参考 `art/p5/fx/v4/satanael/ref_wing_root_back.png`。
- 已经给用户的 Codex 提示词：「读 docs/codex/codex_p5_satanael_wing_roots.md 并照做……先出整体效果图给用户确认，再抠出羽毛层……」。**Codex 会推到 `claude/epic-archimedes-m9an69`**，B 要去这条分支取。
- 图到了以后：
  1. `tools/p5/satanael/game_layers.py v10 0.42` 重出 `game/`；它会跳过空层，新层要确认被输出了。
  2. `SatanaelPuppet.Layers` 加新层（offset / pivot 抄 `game.json`，父节点 `body`），z 顺序按任务单（wings_back_near 之上、wings_front 之下；front 那层在 wings_front 之上、手臂之下），跟着翅膀鼓动（可以跟 `wings_back_near` 同角度）。
  3. `stage_p5.py`，渲染看翼根接缝和退场收拢。
- **还要做的着色器**（不用生图）：翅膀配色接近原版镜头——金色压成深蓝、翼骨 / 翼边加一道青色亮边。可以在 `PersonaShader` 里按层加一个 uniform（只给翅膀层），或者给翅膀单独一个材质。

**2. 把华丽收场接进游戏（最大的一块，还没开始）**
- 打出华丽收场时调用 `SinfulShell.Play`；伤害延到 `Play` 返回的各敌人贯穿时刻再结算（参考毒性爆发的伤害闸门做法）。
- **游戏自己的受击音要关掉**：`Patches.cs` 的 `DamagePatch` 对枪类伤害会给每个敌人放 `GunHit` / `GunHitFinal`。华丽收场期间要屏蔽，保证整发只响 `sinful_pierce_last` 一声。
- Joker：施法动作 `cast_sinful_from_mask`（骨骼里已补结束帧 `HOLD_TO_END`，持枪保持到开枪），`OnJokerShoot` 播 `shiv_from_aim`。
- `Setup` 里的 `JokerFeet / JokerHead / JokerMuzzle / Enemies / FloorY / Actors` 换成真实节点坐标；演出期间锁输入。
- 游戏里读屏幕的着色器读不到画面（P3 mod 的教训），这一段已经全部改成不读屏幕；接的时候别再加读屏幕的效果。
- 接完升版本号（`P5Joker.json` + `ModEntry.cs`，现在 0.9.17），写 `docs/codex/codex_p5_build_0.9.18.md`（照 0.9.17 那份），让用户在本机打包实测（看光照、场景圈、音量）。

**3. 等用户确认的**
- 转枪音效第三版。
- 小细节的量合不合适。
- `docs/COLLAB_RULES.md` 协作规则草案（用户还在和两边商量）。

### 用户这一轮的决定（追加到下面的总清单）
- 受击音用 B 的 `sinful_pierce_last`；整发子弹只响一声，不按敌人数重复。
- Joker 的转枪音效去掉，只留撒旦耶尔的；音效不要叠在一起。
- 转枪声要干脆、简单、轻，不要刻意。
- 子弹、命中特效加一点小细节，不要太多。
- 原版翅膀不是六片羽毛翼：六片硬翼 + 翼根黑色羽毛。
- 交接文档只在用户要求时写。

---

## 用户决定（总清单，日期新的为准）
- 2026-11-10 华丽收场：蓄力 → 时停 → 开枪（推翻 11-09 的「不做时停」）。时停要短（0.3s）、有东西在动、有来由；开枪要特别响亮炸裂。
- 2026-11-10 不要准心 / 十字光这类图案；场景边缘不要卡通感；敌人被光打透不描边。
- 2026-11-10 致命毒药、蛇咬保持原来的通用上毒效果（新做的两版被否）。「没有明确轮廓、纯靠渲染」的标杆是毒性爆发的巨手（`CrushHand`）。
- 2026-11-10 小刀牌枪声调大。
- 2026-11-10 剩余卡牌特效希望一次做完，带音效；白卡不用复杂。
- 2026-11-09 不做时停（做过两版，用户说「做时停还是怪」）；震撼感靠吸气压暗、冲击帧、场景反应、声音。
- 2026-11-09 子弹保持光束式特效，不做弹头。
- 2026-11-09 不做镜头推近。
- 2026-11-09 面具冲天的两道蓝焰去掉。
- 2026-11-09 场景切入 / 收回以 Joker 脚下为圆心，云、撒旦耶尔一起收。
- 2026-11-09 受击音 `sinful_pierce_last`，只响一声；Joker 转枪声去掉；音效不叠。
- 2026-11-09 原版翅膀 = 六片硬翼 + 翼根黑色羽毛（不是全羽毛）。
- 2026-11-08 大罪穿甲弹是人格面具打出来的，Joker 只是同步开枪、不抢戏；开枪声合成，不要普通手枪声。
- 2026-11-08 特效用引擎 / 着色器渲染，不要贴图感、廉价感。

---

## （更早）2026-11-09 · 会话 B（`claude/beautiful-keller-4lsbk4`）的交接 —— 其中待办 A/B/C 已由会话 A 完成，D 改见上面「翅膀」

### 分支

- **工作分支：`claude/beautiful-keller-4lsbk4`**（最新 `fe190758` 及之后的交接提交）。
- 起点是另一个账号的 `claude/epic-archimedes-m9an69`（`ab81f588`），fast-forward 复制过来后在本分支继续；**那条分支没有动过**。两边以后要合并时再说。

### 相关文件

| 文件 | 作用 |
| --- | --- |
| `p5mod/src/Vfx/SinfulShell.cs` | 整段演出：场景（烧穿圈）、时序常量、开枪、子弹、命中区、余韵、音效排期 |
| `p5mod/src/Vfx/SatanaelPuppet.cs` | 撒旦耶尔部件动画：登场 / 退场烧线、闪雷（Rift）、光环、云海、转枪、后坐 |
| `p5mod/src/Vfx/Sfx.cs` | 音效名 |
| `tools/p5/build_rig_p5.py` | Joker 骨骼动画（`cast_sinful_from_mask`） |
| `tools/p5/sfx/synth_sinful*.py` | 合成音效（雷、开枪、贯穿、转枪……） |
| `tools/p5/stage_p5.py` | 写 `p5mod/pck_stage/`（PCK 内容，音效从 `art/sfx_p5/` 拷过去） |
| `tools/p5/fxpreview/` | 预览：`render.sh sinful_shell 6` 出帧和 GIF，`python3 mix.py sinful_shell` 混音出 mp4 |
| `docs/codex/codex_p5_satanael_feather_wings.md` | 给 Codex 的翅膀重画任务 + 可复制的生图提示词 |

### 已完成（本分支三次提交）

1. `e0def21a`
   - 贯穿音效换成合成的 `sinful_pierce`，最后一个敌人用更重的 `sinful_pierce_last`。
   - Joker 转枪到开枪之间那一帧待机修好了：`cast_sinful_from_mask` 补了结束帧（`HOLD_END`），骨骼重建，只有这一个动画变了。
   - 面具冲天的两道蓝焰去掉了。
   - 场景以 Joker 脚下为圆心展开和收回；云海、余烬用同一个圈遮住，跟着一起收回（`SinfulShell.MaskMaterial`、`SatanaelPuppet.SceneMask`）。
2. `0fd8c35a`
   - 撒旦耶尔整体下移 150px（`Setup.PersonaOrigin = (-297, -52)`），光环进了画面；光环跟着烧线出现、消失。
   - 转枪音效：撒旦耶尔用 `sinful_twirl`，Joker 用 `sinful_twirl_small`（`JokerSpinAt = 0.36`）。
   - 敌人那边的命中区（`HitZone`）：三圈冲击波、地面波纹、尘土、碎光、闪光，各贯穿点的灯光调暗了。
   - 羽毛翅膀的 Codex 任务单和参考总览 `art/p5/fx/v4/satanael/ref_feather_wings.png`。
3. `fe190758`
   - 转枪音效改成只有风声：第一版的嗡鸣和金属颤音用户嫌太机械。**用户还没确认新版**。
   - 光环改成原版战斗镜头里的蓝白色：暗色细环，刺身深蓝、亮青边。

### 待办

### A. 召唤 / 退场的闪雷在屏幕外（用户 2026-11-09）
- 现象：召唤撒旦耶尔和他退场时劈下的那道雷几乎看不到。
- 原因：雷的位置 `_riftX = origin.X + 400 * scale`（`SatanaelPuppet` 构造函数）= -297 + 280 ≈ **x -17**，在屏幕左边外面。这是撒旦耶尔下移、画布加宽之前留下的数。
- 要做：雷要劈在人格面具的位置中间。
  - 用 landmarks 里的 `body_center`（2350,2000）×0.42（game_layers 的 K）×scale + origin 换算，约 x ≈ 394；或者用头 / 胸口的屏幕位置。
  - 同时检查 `RiftShader` 的竖向范围：要从屏幕上方一路劈到他身上，再盖进云海。
  - 渲染后看第 1.9～2.1 秒（登场）和第 4.0 秒左右（退场）的帧。

### B. 场景展开 / 收回加音效（用户：不用太重，有那个感觉就行）
- 展开：`RevealAt = 0.05`，持续 `RevealTime = 0.7`，半径先快后慢。
- 收回：`FireAt + ExitAfter + 0.5` 开始，持续 `ExitTime = 0.6`。
- 建议：合成一条轻的「呼——」的火焰卷过声。
  - 低通的噪声，带一点蓝焰的噼啪；展开是由响到弱，收回是反过来、由弱到响收进脚下，最后轻轻一顿。
  - 新脚本放 `tools/p5/sfx/`，在 `Sfx.cs` 和 `stage_p5.py` 里登记，在 `SinfulShell.Play` 里按上面的时刻排期。
  - 音量要压在雷声和开枪声下面。

### C. 时序：人格面具出现太晚，前半段空（用户 2026-11-09）
- 现象：Joker 扶面具 → 面具火消失 → 放下手这一段，场景在扩张，但撒旦耶尔一点要出现的意思都没有；从出牌到场景扩张完显得空。
- 现在的时序（从出牌算）：
  - 场景展开：0.05～0.75 秒。
  - 召唤雷：`AppearAt = 0.3`。
  - 撒旦耶尔烧线：再晚 `RevealDelay 0.05`，用 `RevealTime 0.55`，从云线往上扫到翅尖。
  - 抬枪：`AimAt = 0.45`。
  - 接枪：`CatchAt ≈ 0.94`；开枪：`FireAt ≈ 1.14`。
- 原因：雷在屏幕外（A），烧线又是从低处的云线开始往上扫，所以前 0.6 秒左右基本看不到他。
- 建议：
  1. 先修 A，雷落在他身上，有明确的「来了」。
  2. `AppearAt` 提前到约 0.1，跟场景刚烧开同时。烧线也可以更早露出轮廓线（`ghost_amt`）、翅膀先展开。
  3. 抬枪 / 接枪 / 开枪的绝对时刻尽量不动。Joker 的转枪是骨骼里写死的，接枪要和撒旦耶尔同一刻（`SINFUL_SPIN`、`CatchAt` 两边对齐）。如果要动，两边一起改，并重建骨骼（`python3 tools/p5/build_rig_p5.py && python3 tools/p5/stage_p5.py`）。
- 改完渲染整段，确认 0～1 秒内画面一直有东西在动。

### D. 撒旦耶尔的翅膀改成黑色羽毛（等用户生图）
- 原版战斗镜头里是黑色羽毛翅膀：翼根是密密的鳞片状小羽毛，往外是分开、末端尖而外弯的长飞羽，配蓝色亮边，没有金色骷髅。
- 任务单和提示词：`docs/codex/codex_p5_satanael_feather_wings.md`；参考：`art/p5/fx/v4/satanael/ref_feather_wings.png`。
- 只换 `layers_v10/` 的 `wings_back_far`、`wings_back_near`、`wings_front` 三层；画布 5400×3000、翼根坐标不变。
- 新图到了以后：
  1. `python3 tools/p5/satanael/game_layers.py`（重出 `art/p5/fx/v4/satanael/game/`）；
  2. `python3 tools/p5/stage_p5.py`；
  3. 渲染预览，看翅膀鼓动（±1.5°）时翼根露不露洞，退场时翅膀收拢好不好看。

### E. 其他
- 转枪音效新版等用户试听。
- 本分支和 `claude/epic-archimedes-m9an69` 已经分叉；如果那个账号继续在它的分支上改，以后要合并。

### 预览环境（云端容器，每次新会话要重装）

- `apt-get install -y dotnet-sdk-8.0`；
- Godot 4.5.1 mono：从 GitHub releases 下载 `Godot_v4.5.1-stable_mono_linux_x86_64.zip`，解压后 `export GODOT=<…>/Godot_v4.5.1-stable_mono_linux.x86_64`；
- `pip install scipy numpy pillow`；xvfb-run、ffmpeg 系统里已有。
- 步骤：`python3 tools/p5/stage_p5.py` → `cd tools/p5/fxpreview && bash render.sh sinful_shell 6 && python3 mix.py sinful_shell` → `out/sinful_shell.mp4`。
- 预览里的时间轴：出牌在 1.6 秒（`Preview.cs` 的 `sinful_shell` 场景），所以「出牌后 t 秒」= 视频 1.6 + t 秒，第 n 帧 = 秒数 × 30。
- 游戏里的 DLL / PCK 要在用户本机编译打包（见 `p5mod/README.md`）。
