# 玩家日志：万向斩后 0.08 倍速残留（2026-10-20）

## 证据与判断

用户提供 logs.zip，包含 5 份日志。本次相关日志为 `godot2026-10-18T22.26.18.log`，启动记录为 P3Makoto 0.14.76 / 游戏 v0.111.0 / D3D12 / GTX 1080。其余日志不能作为本 mod 相同问题的直接证据。

本文件中共有 27 次 `stuck at 0.08` 恢复记录，没有 `hit stop ... was never released` 或 `freeze ... was never released` 记录。说明看门狗当时已认为两个 mod 减速来源都结束，但实际全局倍率仍为 0.08。旧兜底要等 2.5 秒才恢复，反复残留可能拉长体感慢放；现有日志不足以确定每次慢放持续时间，更不能据此认定某个其他 mod 是写入者。

以下为不含玩家身份信息的日志原文节选，行号按原文件计：

```text
L35259 [P3Makoto] trace card done CARD.OMNISLICE (t=2168785ms, timescale=0.08)
L35275 [P3Makoto] health fps=125 timescale=0.08 nodes=5538 orphans=1631 root_children=14 sfx_players=0
L35276 [P3Makoto] health gpu_ms avg=1.8 max=2.6 render_cpu_ms avg=0.9 max=2.1 quality=1
L35278 [P3Makoto] timescale: stuck at 0.08 for 2.5s with no P3Makoto slow-down active -> back to 1
```

该采样段支持“游戏时间倍率异常”，不支持“GTX 1080 性能不足导致低帧率”。不能据此排除其他时段存在性能问题。完整日志含玩家标识及其他 mod 输出，不提交到公开仓库。

只读检查本机 v0.111.0 游戏程序集：NHitStop 从 0.1 渐变到 1，最长 Forever 为 2 秒，不会自行产生 0.08。没有发现它保存并恢复旧倍率的逻辑，因此不能把“原版恢复旧值”当成已确认根因。

## 本次修改

- EvokerShot 的命中顿帧按实际真实时间到期，不再依赖额外 SceneTreeTimer 回调，也不再在丢失回调时额外慢放 0.3 秒。保留连续命中的 350ms 冷却。
- 顿帧、时停和 Reset 的状态与引擎倍率一起在主线程更新；后台排队保留原请求截止时间，过期请求不补播。
- 本会话确实发生过 mod 顿帧后，若没有有效命中/时停来源，但倍率又停在本 mod 的 0.08 特征值，持续 100ms 后恢复到 1。仍受下一次主线程帧调度限制，不能承诺阻塞期间也能恢复。
- 不提前终止有效的 0.08 顿帧、建筑师时停或原版从 0.1 起的顿帧。其他异常倍率沿用原有 2.5 秒兜底。
- 快速恢复打印 `timescale: stale hit stop 0.08 with no active lease for 100ms -> back to 1`，便于后续区分残留与正常演出。

这是针对日志所证实故障的恢复修复及线程/计时修正，尚未定位所有写入者，也不是“所有慢放全部根治”的证明。

## 验证与安装状态

- TimeScaleTests 直接链接生产 TimeScale.cs：26 项通过，覆盖短顿帧到期、丢失回调、两种减速重叠、旧值再次写入、原版 2 秒顿帧、后台排队过期及 Reset。
- Release 编译：0 警告、0 错误。
- v0.111.0、v0.107.1 两套游戏 API 兼容检查各 1174 处引用通过。
- 用户明确要求“还不用安装，再等等”：本次不安装、不启动游戏。构建产物为 mod/bin/Release/net9.0/P3Makoto.dll。
- 尚待用户在当前版重复万向斩、涅槃之怒及多段高伤害，确认是否还出现慢放；日志属于旧版本，当前版实际改善尚未经实机验证。

手柄奖励左右方向、建筑师 CG 手柄适配仍按 player_reported_bugs_20261020.md 跟踪，不能把本次倍率修复当成这两项已完成。
