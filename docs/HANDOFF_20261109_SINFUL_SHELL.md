# 交接：华丽收场 / 大罪穿甲弹（Sinful Shell）—— 2026-11-09

## 分支

- **工作分支：`claude/beautiful-keller-4lsbk4`**（最新 `fe190758` 及之后的交接提交）。
- 起点是另一个账号的 `claude/epic-archimedes-m9an69`（`ab81f588`），fast-forward 复制过来后在本分支继续；**那条分支没有动过**。两边以后要合并时再说。

## 相关文件

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

## 已完成（本分支三次提交）

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

## 待办

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

## 预览环境（云端容器，每次新会话要重装）

- `apt-get install -y dotnet-sdk-8.0`；
- Godot 4.5.1 mono：从 GitHub releases 下载 `Godot_v4.5.1-stable_mono_linux_x86_64.zip`，解压后 `export GODOT=<…>/Godot_v4.5.1-stable_mono_linux.x86_64`；
- `pip install scipy numpy pillow`；xvfb-run、ffmpeg 系统里已有。
- 步骤：`python3 tools/p5/stage_p5.py` → `cd tools/p5/fxpreview && bash render.sh sinful_shell 6 && python3 mix.py sinful_shell` → `out/sinful_shell.mp4`。
- 预览里的时间轴：出牌在 1.6 秒（`Preview.cs` 的 `sinful_shell` 场景），所以「出牌后 t 秒」= 视频 1.6 + t 秒，第 n 帧 = 秒数 × 30。
- 游戏里的 DLL / PCK 要在用户本机编译打包（见 `p5mod/README.md`）。
