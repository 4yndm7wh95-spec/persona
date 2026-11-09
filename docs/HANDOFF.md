# 交接文档（两个会话共用，最新的在最上面）

协作规则见 `docs/COLLAB_RULES.md`（草案，用户还没最终确认）。用户说过：交接文档**只在用户要求交接时写**，不用每次做完都写。

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
**1. 翅膀：等 Codex 出图（最先处理）**
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
