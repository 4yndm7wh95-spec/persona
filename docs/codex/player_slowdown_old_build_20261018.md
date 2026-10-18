# 玩家旧版持续慢放：日志与视频调查

## 样本和版本

用户提供本机 `Downloads/godot.zip`，内有 `godot.log` 和另一个 ZIP 中的 30.005 秒、1280×720、约 59.94 fps 的 MP4。原压缩包只读，分析文件保存在仓库外 `sts2_tools/player_slowdown_20261018/`；未把原始玩家日志或视频提交到仓库。下列摘录不包含账户标识、令牌或用户目录。

口述版本为 0.14.59，但这份日志实际初始化的是 **P3Makoto 0.14.48 / 游戏 v0.111.0**。不能把此样本当作 0.14.59 的复现证据。

已核对仓库历史：真正 v0.14.59 的 `ModEntry` 输出 `init v0.14.59`，不是沿用旧字符串。仍需区分「用户更新了文件」和「本次进程实际加载了哪一份 DLL」；也可能用户提供的是更新前日志。

### 视频和日志的时间对应

- MP4 原文件名记录 `2026.10.18 - 19.41.33.01`；视频内部 `creation_time` 为 `2026-10-18T11:42:04Z`，按录制者 UTC+8 换算为 19:42:04，与约 30 秒视频的结束时刻吻合。
- 日志第 2232 行有启动阶段的绝对时间 `Timestamp: 10/18/2026 19:10:46`。末段 trace 的引擎计时已超过 31 分钟，与视频的 19:41～19:42 时段大体对应。
- 这两个时间来源没有明显冲突，但日志没有逐行绝对时间，启动阶段时间戳也不是引擎零时刻，不能假装已完成逐秒同步。外层 ZIP 的修改时间不含时区且可被打包软件重写，不用于判断旧日志。
- 因此不能仅凭版本较旧就断言「日志与视频不是同一局」。可靠结论是：这份日志中实际初始化了旧版代码，且有长期 0.08 倍速的证据。

```text
810: [P3Makoto] init v0.14.48
811: [P3Makoto] game version v0.111.0
812: [P3Makoto] harmony patches applied: 61 ok, 0 failed
817: [P3Makoto] P3R sfx loaded 89/89
9325: [P3Makoto] trace card done CARD.MANIFEST_AUTHORITY (t=1565248ms, timescale=1)
9346: [P3Makoto] trace card done CARD.ULTIMATE_STRIKE (t=1568298ms, timescale=0.08)
9362: [P3Makoto] trace card done CARD.COSMIC_INDIFFERENCE (t=1729887ms, timescale=0.08)
9389: [P3Makoto] trace card done CARD.FALLING_STAR (t=1760511ms, timescale=0.08)
9475: [P3Makoto] trace card done CARD.GLOW (t=1858249ms, timescale=0.08)
9482: [P3Makoto] trace card done CARD.VENERATE (t=1859818ms, timescale=0.08)
9486: [P3Makoto] trace card done CARD.DEFEND_REGENT (t=1865232ms, timescale=0.08)
```

## 结论与证据边界

- 从上述攻击后到最后一条记录相隔 **296.934 秒**，多次不同牌结算仍记录 `timescale=0.08`。这是长期停留在约正常 1/12.5 速度的直接证据，不能只当作 GPU 掉帧。
- 视频总览可见出牌、卡牌飞行和敌方回合动画明显拖长，与慢放方向一致。文件的编码帧率不是游戏实际 FPS，不能据此断言显卡流畅或没有掉帧。
- v0.14.48 的 `EvokerShot.HitStop` 将时间倍率设为 0.08，仅靠定时器恢复；`TimeScale` 没有超时恢复。一旦恢复回调遗漏，或旧 `_hitStopActive` 没清理，就可能长期停在 0.08。日志值和此路径一致，但没有定时器内部记录，不能确定是哪一条回调为何丢失。
- 玩家同时加载了 SpeedX、BulletTimeAnime、BetterSovereignBlade 等其他 mod；它们的存在是兼容排查背景，不是已证实的肇因。不能依据名称断言某个 mod 造成问题。
- 日志还有 `AssetGuard.Apply` 访问已释放 `CompressedTexture2D` 的异常（3 次，另有重复警告），新版 v0.14.65 已对资源失效做修复。退出时的资源泄漏提示本身不能证明游玩中的累计泄漏导致卡顿。

## 当前版本是否覆盖

| 改动 | 作用与判断 |
| --- | --- |
| v0.14.51 时间恢复看门狗（`7be7fc1`） | 每帧使用真实时间检查，命中减速超时自动恢复；无本 mod 减速来源但倍率仍低于 1 达 2.5 秒，也恢复为 1。直接针对本样本的长期 0.08 类型。 |
| v0.14.60 音效生存上限（`655b7b8`） | 音效结束信号丢失时按时长兜底释放，防止播放器堆积；本样本没有运行期数量证据，不能认定发生了音效泄漏。 |
| v0.14.62 多段格挡与数组优化（`523687b`） | 避免反复堆 BLOCK 字和完整冲击效果，减少每帧临时数组分配。降低卡顿，不能替代时间倍率恢复。 |
| v0.14.63 命中冷却/绘制优化（`e1dfa35`） | 顿帧后增加 350ms 冷却，减少多段攻击反复减速；圆形抗锯齿走快速路径，数量型特效按帧率减量。 |
| v0.14.71 显卡侧保护（`6767c16`） | 限制折射波数量（正常最多 3、低帧时最多 1），低帧时对复杂多边形降低 AA 负担，并记录 GPU/CPU 耗时。 |

**对这份实际加载 0.14.48 的样本，新版已有直接覆盖的恢复机制，不需要凭同一证据重复修改时间控制代码。** 0.14.59 本身也已含 v0.14.51 看门狗，且该文件至当前未变；如果另一份确实运行 0.14.59 的日志仍长期慢放，需要单独调查，不能把结论外推为所有卡顿已根治。

当前仍有未纳入统一限流的大面积水流/透明特效，性能优化不等于所有机器都不会掉帧。应使用新版日志中的 `timescale`、`health LOW`、`health hitch`、`gpu_ms`、节点数及 `sfx_players` 区分倍速异常、CPU/GPU 开销和资源堆积。

## 本次回归检查

新增 `tools/compat/TimeScaleTests`，直接链接当前生产 `Core/TimeScale.cs`。使用假的 Godot 时钟/帧事件模拟恢复定时器永不触发，不启动游戏、不读写存档。

11 项检查通过：重复安装只有一个帧监听；有效顿帧不提前结束；丢失回调后 0.08 超时恢复；命中减速结束不破坏仍有效的 0.02 时停；时停自身超时恢复；正常释放保持 1；无来源的 0.08 在 2.5 秒宽限期后恢复；Reset 清除两个来源；恢复有日志证据。

```text
[P3Makoto] timescale: hit stop 0.08 was never released -> back to 1
[P3Makoto] timescale: hit stop 0.08 was never released -> back to 1
[P3Makoto] timescale: freeze 0.02 was never released -> back to 1
[P3Makoto] timescale: stuck at 0.08 for 2.5s with no P3Makoto slow-down active -> back to 1
PASS: 11 production TimeScale checks; lost timer, overlapping sources, orphan slowdown, reset, single subscription
```

这是生产恢复逻辑的确定性回归检查，不是玩家原 mod 组合的实机复现，也不证明其他 mod 不会不断重写时间倍率。
