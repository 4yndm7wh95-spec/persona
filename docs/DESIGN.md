# P3 结城理 × 储君 换皮 Mod 设计文档

> 状态：v0.1 草案。卡牌特效表基于卡名初拟，拿到完整源码和卡牌描述后会逐张修订。

## 1. 分工

| 角色 | 职责 |
|---|---|
| Claude（云端） | 设计、写全部代码（C# / 着色器 / Spine JSON 生成脚本）、写给 Codex 的指令 |
| Codex（本地） | 只执行：跑命令、用 imagegen 出图、编译打包、安装、截图录屏、推送仓库 |
| 用户 | 进游戏验收，反馈 |

## 2. 已确认的技术事实（v0.111.0）

- 引擎：Godot 4 + C#，主程序集 `data_sts2_windows_x86_64/sts2.dll`。
- 官方 mod 加载：`游戏目录/mods/<Mod>/` 下放 `<Mod>.json`（清单）+ `<Mod>.dll` + `<Mod>.pck`。
  DLL 中用 `[ModInitializer("方法名")]` 标记入口，内部用 Harmony 打补丁（见 Mesugaki 参考 mod）。
- PCK 中与原版同路径的资源会覆盖原版（Mesugaki 就是覆盖 `animations/characters/regent/*`）。
- 角色动画：spine-godot GDExtension，Spine **4.2.43**。
  - 战斗场景 `scenes/creature_visuals/regent.tscn`：根 `Node2D` → `Visuals`(SpineSprite, scale 0.29) + 武器 SpineSprite + 骨骼挂点粒子 + `NRegentVfx`。
  - 储君动画名：`idle_loop`、`relaxed_loop`、`attack`、`attack_sovereign`、`cast`、`hurt`、`die`。
  - 刘川 mod 证明：mod PCK 里放 `.spine-json` 能被游戏正常加载。
- 战斗中角色包围盒约 230×335 px（`Bounds`），中心点 (0,-178)，意图位置 (24,-414)。
- Godot 版本：**4.5.1**（GodotSharp.dll），目标框架 net9.0，Harmony 为游戏自带 `0Harmony.dll`。
- mod 加载顺序：先加载 `<id>.dll`，再加载 `<id>.pck`；有 `[ModInitializer]` 时只调用初始化方法，Harmony 需自行 `PatchAll`。

### 2.1 源码确认的挂钩点
| 用途 | 位置 | 说明 |
|---|---|---|
| 出牌特效 | `Hook.BeforeCardPlayed(ICombatState, CardPlay)` | 每次打出（含多次打出）在 `OnPlay` 前调用；`CardPlay` 带 Card / Player / Target |
| 单体选目标开始 | `NTargetManager.StartTargeting(TargetType, Control, …)` | `Control` 参数就是被拖动的 `NCard` |
| 群体牌进入出牌区 | `NCardPlay.ShowMultiCreatureTargetingVisuals()` | |
| 悬停 / 离开目标 | `NTargetManager.OnCreatureHovered/Unhovered(NCreature)` | |
| 选目标结束 | `NTargetManager.FinishTargeting(bool cancel)` | |
| 出牌流程清理 | `NCardPlay.HideTargetingVisuals()` | 取消和成功都会走到 |
| 角色动画状态机 | `CharacterModel.GenerateAnimator` | 触发器 Cast / Attack / Hit / Dead / Idle / Relaxed，对应 Spine 动画 cast / attack / hurt / die / idle_loop / relaxed_loop，低血量 `low_health_loop` |
| 直接切 Spine 动画 | `NCreature.Visuals.SpineBody.GetAnimationState().SetAnimation(name, loop)` | 用于预备姿势 |

### 2.2 新骨骼必须满足的兼容约定（原版 regent.tscn 和 NRegentVfx 依赖）
- 动画：`idle_loop`、`low_health_loop`、`relaxed_loop`、`attack`、`attack_sovereign`、`cast`、`hurt`、`die`，另加 `aim_loop`、`ready_sword_loop`。
- 骨骼：`arm_particle_attach`、`chest_particle_attach`、`leg_particle_attach`、`leg_particle_attach_l`；新增 `evoker_tip`、`sword_tip` 供特效定位。
- 插槽：`shadow`（君王之刃的武器 Spine 挂在这里）。
- 事件：`death_particles_start`、`death_particles_end`、`explode_dead`、`explode_end`（die 动画中触发）。

### 2.3 代码约束
- mod 程序集是运行时加载的，**不定义自定义 Node 子类**。特效只用引擎自带节点，用 Tween / Timer / lambda 驱动，着色器用代码字符串生成。
- 所有补丁都包在 `ModEntry.Safe` 里，特效出错不影响游戏逻辑。

## 3. 总体方案

### 3.1 骨骼动画（不用 Spine 编辑器）
1. **美术**：Codex 用 imagegen 生成"分件立绘"（头、刘海、后发、躯干、上臂、前臂、手、召唤器、剑、大腿、小腿、耳机、领带等），透明背景，同一画风。
2. **拼装**：Claude 写 `tools/build_spine.py`：
   - 读取各部件 PNG 和 `art/rig.json`（每个部件的锚点/父骨骼/层级）。
   - 打包成 Spine atlas（`.atlas` + `.png`）。
   - 生成 Spine 4.2 JSON（骨骼、插槽、区域附件，以及全部动画关键帧和事件）。
3. **替换**：PCK 覆盖 `animations/characters/regent/regent_skel_data.tres`，让它指向新的 `.spine-json` 和 `.atlas`。动画名与原版保持一致，原版代码无需改动就能驱动。
4. 角色选择、篝火、商店三处的 Spine 用同一套部件和不同的动画生成。

| 动画名 | 表现 |
|---|---|
| `idle_loop` | 单手插兜，呼吸起伏，耳机线和刘海轻摆 |
| `relaxed_loop` | 双手插兜，略低头 |
| `attack` | 单手剑横斩 |
| `attack_sovereign` | 召唤器抵太阳穴 → 扣扳机 → 玻璃碎裂，Persona 出击 |
| `cast` | 召唤器举起扣扳机（技能 / 能力牌通用） |
| `hurt` | 后仰，刘海甩动 |
| `die` | 单膝跪地，召唤器落地 |
| `aim_loop`（新增） | 预备出牌：召唤器抵住太阳穴并保持，头发被气流吹动 |
| `ready_sword_loop`（新增） | 预备出攻击牌：剑尖斜指目标 |

### 3.2 特效（游戏内实时渲染）
全部用 Godot 节点实时生成：`GPUParticles2D`、自定义 `.gdshader`、`Line2D`、`Polygon2D`、Tween，尽量不用序列帧。
纹理只需少量基础素材：柔光点、玻璃碎片、蝴蝶、塔罗牌框，以及 Orpheus / Thanatos / Messiah 剪影立绘（imagegen 生成）。

C# 侧结构（`mod/src/`）：
- `P3Vfx/VfxLibrary.cs`：可复用的特效积木（玻璃碎裂、蓝色蝴蝶群、Persona 浮现、斩击弧、元素魔法 Agi/Bufu/Zio/Garu/Hama/Mudo、Megidolaon、护盾六边形、塔罗牌翻转、影时间滤镜）。
- `P3Vfx/CardVfxTable.cs`：卡牌 ID → 特效组合 的映射表。
- `Patches/CardPlayPatch.cs`：Harmony 挂到出牌流程，在原版动画时机触发对应特效。
- `Patches/TargetingPatch.cs`：挂到拖牌 / 选目标流程，处理预备出牌的姿势和特效。
- `Preview/VfxPreview.tscn`：独立预览场景。Codex 可用 Godot `--write-movie` 录屏推回仓库，不进游戏也能迭代特效。

### 3.3 预备出牌（拖牌选目标）
- 开始拖牌：角色切到 `aim_loop`（技能 / 能力牌）或 `ready_sword_loop`（攻击牌）；脚下浮现蓝色法阵，周身飘起蓝色蝴蝶。
- 指向敌人：目标脚下出现 P3 风格的锁定框，靶心缓慢旋转；指针线换成蓝色光带。
- 能力牌：身后浮现半透明塔罗牌。
- 取消：法阵收拢消散，回到 `idle_loop`。
- 打出：衔接出牌特效。

## 4. 卡牌特效分级（草案）

原则：华丽程度按稀有度和卡牌费用分配；表现不好做的牌只用通用特效。

| 等级 | 含义 | 卡牌（暂定） |
|---|---|---|
| S 专属大招 | 召唤 Persona + 全屏演出 | BigBang（Megidolaon）、BlackHole（Thanatos 降临）、Supermassive、Genesis（Messiah）、MeteorShower、DyingStar（Mudoon 棺椁）、TheSealedThrone（大封印）、VoidForm（影时间降临：绿月 + 全屏滤镜）、SevenStars、Hegemony、Tyranny |
| A 专属 | 有独立设计的单体 / 群体魔法 | SolarStrike（Agidyne）、LunarBlast（Bufudyne）、GammaBlast（Ziodyne）、Comet、FallingStar、Quasar、Bombardment、PillarOfCreation（Hamaon 光柱）、HeavenlyDrill、CrescentSpear、CelestialMight、Radiate、Convergence、IAmInvincible、Conqueror、SovereignBlade（Persona 之刃） |
| B 套用模板 | 积木组合加换色 | 攻击类（斩击 / 拳 / 踢 + 元素色）、防御类（六边形护盾；Reflect = Makarakarn，Parry = Tetrakarn）、星辉获取类（蝴蝶汇聚） |
| C 通用 | 一个通用"召唤器释放"小特效 | 抽牌、过牌、杂项技能 |
| — 不做 | 保留原版 | 若某牌原版特效依赖机制提示，保留原版 |

## 5. 迭代流程
1. Codex 推送资料 → Claude 设计和编码。
2. Codex 编译 → 跑预览场景录屏 → 推送截图 / 视频。
3. Claude 根据截图调参。
4. 打包安装 → 用户进游戏验收。
