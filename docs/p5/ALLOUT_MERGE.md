# 总攻击：合并说明（给负责合并的会话）

分支：`claude/exciting-babbage-lm8ubb`，从主线 `claude/confident-hypatia-nropgi` 的 `84dd02dd` 分出来。截至写这份说明时，主线没有新提交，试合并（git merge-tree）没有冲突。
状态：用户在预览里逐版确认过，最后一版是 `docs/p5/preview/allout/allout_v13.mp4`。**还没进游戏验收过。**

## 做了什么
精英 / Boss 战里，Joker 打出最后一击后：
1. 慢放 + 压暗；
2. P5 风格的裂纹分三下裂开，画面真的分块错开；
3. 碎开，P5R 原版收尾卡（THE SHOW'S OVER）分层切入；
4. 本局怪物的纯黑剪影砸进左下，持续喷黑色血柱；
5. 收尾照 P3 mod：一层红色的波盖过来，再一层透明的波露出结算。

总长约 4.5 秒。结算推迟到透明波扩开时，只推迟表现，不改结果。设计记录见 `docs/p5/ALLOUT_PLAN.md`。

## 新文件（直接带过去）
- `p5mod/src/AllOutPatches.cs`：游戏接入。
  - 在 `NCreature.StartDeathAnim` 前截怪物剪影；最后一只倒下时开播。
  - 在 `Hook.AfterCombatEnd` 后推迟结算。
  - 快速模式"瞬间"下不播。
- `p5mod/src/Vfx/AllOut.cs`：主时间轴（`partial class`）。
- `p5mod/src/Vfx/AllOutGlass.cs`：裂开的玻璃（`partial class`）。
- `p5mod/src/Vfx/AllOutFinale.cs`：原版收尾层的实时重放。
- `tools/p5/allout_native/export_runtime.py`：生成 `hero.bin`。
- `tools/p5/allout_native/trace_cracks.py`：生成裂纹折线。
- `tools/p5/allout_native/cracks/*.json`：裂纹折线数据。
- 打包文件夹（`pck_stage`）里的新文件：
  - `p5joker/allout/`：`hero.bin`、`cracks_a.json`、`cracks_b.json`；
  - `p5joker/fx/aoa_*` 及对应的 `.godot/imported/aoa_*`；
  - `p5joker/sfx/aoa_*.ogg`。
- 素材来源：
  - `art/p5/fx/gen_codex/allout_cracks_v2/`：Codex 生图；
  - `art/sfx_p5/aoa_crack.*`；
  - `research/p5r/audio/allout/battle_all_out_atk_finish_b.ogg`：用户导出的原版喷溅声。

## 改到的共用文件（只有追加，合并时注意保留）
- `p5mod/src/Vfx/Sfx.cs`：在 `AllOut = ...` 后面加了 `AoaHit1/2/3`、`AoaSlash1/2/3`、`AoaSplatter`、`AoaCrack`、`AoaZoom`。
- `tools/p5/stage_p5.py`，四处追加：
  1. SFX 表里加了 `aoa_*`（在 `battle_all_out_atk` 后面）；
  2. `fx_textures()` 里在 `yokoku` 后面加了 `aoa_` 系列贴图，包括 `allout_native/tools/p5/allout_native/godot/tex`、`splatter_textures()`、`allout_cracks_v2`；
  3. 打包时导出 `hero.bin`，并拷贝 `cracks_*.json`；
  4. 加了 `from allout_native.export_runtime import ...`（两处）。
- `tools/p5/fxpreview/Preview.cs`：在 `embark` 前面加了一个 `allout` 预览场景。

## 合并后必须做
1. 重新跑 `python3 tools/p5/stage_p5.py`。合并后的打包文件夹需要同时包含双方的新贴图。注意：这边提交时没有带别的会话的墨之刃、咒怨贴图，那些以主线为准。
2. 类型检查：`bash tools/p5/cloud/check.sh`。
3. 预览：`GODOT=... bash tools/p5/fxpreview/render.sh allout 6`，再跑 `python3 tools/p5/fxpreview/mix.py allout`。

## 和性能优化相关、需要留意的点
- **慢放**：碎开之前用了 `Engine.TimeScale = 0.12`。以下几处都会恢复速度，只在我们自己改过时才恢复：
  - 碎开时；
  - 节点退出时；
  - 一个真实时间计时器；
  - `OnDeath` 和 `OnCombatEnd`。

  如果性能优化动了 `Engine.TimeScale` 或顿帧（`HitStop`），要确认两边不互相覆盖。
- **一次性开销**：
  - 读屏截图 `GetViewport().GetTexture().GetImage()`，碎开时一次；
  - 原版收尾层用 4 个 `SubViewport`（3D，MSAA 4x），只在播放期间存在；
  - 截每只怪物剪影各开一个小 `SubViewport`，3 帧后释放。
- **`hero.bin` 约 1.27 MB**，第一次播放时解析，之后缓存在内存。

## 已知未验证（需要进游戏看）
- 截剪影的区域取的是怪物的碰撞框。大体型 Boss 的剪影可能被截掉一部分。
- Boss 房镜头拉远时，剪影和撞击点的位置需要确认。
- 联机时只在 Joker 玩家这边播放。
