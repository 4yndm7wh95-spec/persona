# P5 Joker 战斗特效 / 毒伤 / 音效修复（0.9.11）

日期：2026-11-04。独立分支：`codex/p5-combat-fixes-20261104`。
基于 `claude/cool-pascal-mil5i8` 的 `c6dfb425c9965bba0736720a1d43ce370ad74b32`。仅下载/校验所需源码快照，不拉取巨大历史，不移动 Claude 的分支。旧 Opus 防御方案废弃，本次没有接回或重新设计防御。

## 对应用户反馈

| 要求 | 本次处理 |
|---|---|
| 大爆炸那圈光波换色用于捏碎 | 只读参考 P3Makoto 的 `mod/src/Vfx/Uniq.cs:BigBang` → `P3Fx.Shockwave` → `Shaders.Ring`。移植环形 shader 的细亮芯、宽柔光和边缘渐隐；保留五圈 40ms 间隔、600～2600px 的扩张结构，改为白红/绯红。捏碎时直接发，不带 P3 的蓄力等待、人物或其他招式。无 P3 mod 运行时依赖。 |
| 触媒多次毒伤数字回原版 | 在真实 `PoisonPower.Trigger` 状态机内标记它调用的 Damage，异步上下文记录具体 PoisonPower；只为 Joker 施毒且目标一致的真实毒伤换 P5 数字。覆盖触媒每一轮，不再依靠最近命中/毒爆 1.5 秒窗口，也不把普通攻击的命中音和顿帧叠上去。 |
| 打击换回原版音效 | 原版 AttackCommand 命中声本来保留；删掉 `KnifeFx.Hit` 另外叠上的 Hit / AllOut / SubBoom 三层。打击动作和刀光保留。 |
| 枪声候选 | `audio/` 内 A=gun_01、B=gun_04、C=gun_05、D=gun_06，X=现用 gun_10。提供原始 OGG、连续 MP3 与本地试听页；本次没有替用户选或修改现用枪声。 |
| 选中小刀牌安静 | 去掉 Gun 架势的 GunDraw，选目标时同一 Gun 架势的准星 Cursor 也静音；真正使用后的枪声保留。 |
| 毒爆伤害卡在捏碎高潮 | 每次 CardPlay 对应自己的 OutbreakPlayback。先沿用原版 PowerCmd.Apply 上毒，再放行捏碎；巨手 onCrush 释放实际结算任务，横幅碎开/舞台白闪/红环共用此刻。删掉毒爆伤害数字和落点爆炸另加的 BlastWait。上毒钩子慢时，横幅、巨手与亚森一起停在捏碎前等待，避免画面先碎伤害后到。 |
| 群体毒伤不再逐个等完 | 所有目标的原版 `PoisonPower.Trigger()` 在同一次回调中启动，再 Task.WhenAll；不再一个目标所有轮次完毕才开始下一个。仍保留原版触媒重复、毒层递减、Damage/death/hooks，不把伤害伪装成只同步数字。 |
| Boss/精英大房间切入变小 | 全屏舞台、眼神、碎片、巨手与光圈用同一屏幕空间根节点，抵消房间变换和 canvas 相机缩放/位移，尺寸取实际视口。人物/敌人的普通特效仍在战斗世界坐标。 |
| 快速出牌偶尔漏召唤 | 删除忙于其他姿势就跳过技能施法的入口；每次切入单独持有 split/seed/pinch/clock，不再让后一张牌改前一张；待结算绑定具体 CardPlay（弱引用），而非同一个 CardModel 上的旧任务队列。召唤延迟按剩余时长计算，不重复加一整段等待。 |
| 手真的抓住眼神背景 | 复用当前整体体积手 shader，按首个可见表面的局部距离划分四指/拇指前层与掌臂后层。两个互补 mask 同时采样一条姿态/时钟，掌臂在横幅后，指尖/指节和拇指在横幅前；不需要重画整只手或拆手指图片。 |

## 时间与实现

正常时序仍是：施法 → 0.04s 后眼神/舞台 → 0.30s 后亚森现身 → 亚森 AttackAt 时巨手冲出 → GrabAfter / FistAfter 攥紧 → CrushAfter 捏碎。
唯一伤害启动点是巨手 `onCrush`。横幅的旧 50ms 裂纹停顿仅在 Outbreak 移除，普通召唤保留。离线预览 `outbreak_hand` 同样改为共享 clip + 同一 onCrush，不再人为给三个敌人加 10ms 的顺序延迟。

新文件：`PoisonPlayback.cs`、`Vfx/CutInPlayback.cs`、`Vfx/CrushWave.cs`。改动只在 P5 mod、对应预览与本报告/检查工具里；没有修改 P3 mod 源码、build_rig_p5.py、PCK、游戏文件或 art/cards。

## 验证

- Release 编译通过（.NET 9 目标；SDK 10.0.401）。0 错误；已有 `ReadyFx._aura` 未赋值警告保留。
- 预览 C# 与全部 Vfx 源码用 GodotSharp 做独立编译通过；另有旧 Preview.cs:148 的可空元组警告，没有新增编译错误。
- `tools/p5/verify_combat_patches/` 在独立 .NET 9 控制台进程读取本机 sts2.dll，验证实际毒伤状态机的唯一 Damage 调用位置，成功挂载五个相关 Harmony 补丁并卸载。测试 Damage 用空任务替换，没有运行游戏伤害。
- 独立时钟测试：慢上毒等待、放行捏碎、重叠卡牌独立时间/捏合、时钟倒退不下溢。
- 并发上下文测试：两次毒伤各持有自己的 PoisonPower，逆序完成后不污染调用者。
- 普通 Godot 4.5.1 / OpenGL 3.3 实际渲染当前源码 shader 与原生眼神网格，shader stderr 为空；测试房间 scale=.7、平移(170,90)，canvas zoom=.55、平移(120,75)。根节点的视口变换仍为 identity，缩放房间与普通房间覆盖范围一致（960×540）；统一背景后平均通道差 0.0282/255，少量 GPU 浮点/噪声差异保留，没有声称像素完全相同。
- `contact_sheet.png`：抓住、攥紧、Boss 缩放、红白光圈。它是隔离 shader 检查图，不是已启动游戏的截图。手指前后遮挡可见。

原样日志：`build.log`、`preview_compile.log`、`compatibility.log`、`shader_render.log`、`visual_checks.txt`。

### 验证边界

未启动游戏、未安装此包、未做实机出牌或多人验收。这里确认了编译、挂载、局部 GPU 效果与时钟/上下文隔离；没有宣称所有实机问题已验收。

“同时”指同一捏碎回调启动所有原版 Trigger；第三方钩子/死亡反应/多人的异步等待仍可能造成内部扣血或后续触媒轮次不同步，不强行删除这些原版钩子，也没有改成原子批量扣血。快速重复出牌的资源/整场战斗状态仍需实际玩法验证。

## 版本和交付

版本常量与清单均为 **0.9.11**。由于本次改变 Joker Outbreak 的结算顺序，清单 `affects_gameplay` 如实改为 true；没有更改牌的数值、毒层公式或触媒倍率。

`P5Joker-0.9.11-update.zip` 为增量测试包，只含 DLL 和清单，复用原有 0.9.10 PCK。不自动安装；不覆盖或重新打包资源。
DLL SHA-256：`54f208d0669d70325103cb6804db1b4a2d01ee64b2aadd60d5f80d89d8c8742b`。

源码、候选声和报告推到上述独立分支，Claude 可审读后自行合并；原 Claude 分支不动。提交号在交付回复中列出。
