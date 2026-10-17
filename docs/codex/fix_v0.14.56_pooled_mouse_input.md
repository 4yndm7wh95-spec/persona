# v0.14.56：牌堆关闭后部分卡牌不能悬停/选择

## 现象与根因

v0.14.55 新增退场动画，关闭牌堆时把每个 NGridCardHolder.Hitbox.MouseFilter 改为 Ignore。NGridCardHolder 是游戏对象池资源，奖励、升级等界面也会使用它。检查本机 v0.111.0 的程序集确认：OnReturnedFromPool 重置位置、缩放、可点击标志和光标，却不重置 MouseFilter；SetClickable(true) 只设置 _isClickable。因此借到受影响节点的牌不响应悬停/点击，借到正常节点的牌仍可选；整组借到受影响节点就全部失灵。这种状态不一定抛异常。

当前日志为 v0.14.55，未记录相应选卡异常。以上来自源码、程序集检查和独立引擎测试，不是对实时游戏场景树的读取。归档 v0.14.53 的 Reparent/RefreshFocusState 异常属于另一轮日志，未误用作本次直接证据。

## 修复策略

只在即将销毁的 NCardPileScreen 上设置 MouseBehaviorRecursive = Disabled。这样退场的整棵界面树忽略鼠标，但不改卡牌 Hitbox、本体或返回按钮后代的 MouseFilter 属性。可复用子节点离开该祖先后自然恢复自己的输入行为。

移除了退场时对 BackButton 及其后代、各张牌 Hitbox 的 MouseFilter 写入。保留 ProcessMode.Always、0.42 秒动画、隐藏捕获层和名牌、原有最终释放及 1 秒兜底。NCardPileScreen 通过场景 Instantiate 新建，不进卡牌对象池，因此界面级设置无需写回复用节点。没有新增全局 Harmony 输入补丁，也没有强制把所有卡牌都设成 Stop。

此前已经污染的旧进程对象池不通过热修改修复；更新 DLL 后必须完全退出并重开游戏。

## 改动

- mod/src/Vfx/PileFx.cs：退场只禁用祖先界面的鼠标行为。
- mod/P3Makoto.json、mod/src/ModEntry.cs：版本与启动日志同步为 0.14.56。
- tools/compat/tests/pile_exit_mouse_filter.gd：无界面输入继承/复用回归测试。

## 验证

- P3Makoto Release：0 Warning(s)、0 Error(s)。
- 独立 Godot/GDRE 无界面测试：先证明旧方案在复用后保留 Ignore，再对 Stop、Pass 两种原始值各循环 32 次；退场时卡牌和返回按钮有效过滤为 Ignore，复用后恢复原始有效过滤，子节点属性从未改写。64 次全部通过。
- 测试宿主是本机 GDRE 的 Godot 4.8.dev，并非启动 StS2；两套游戏 GodotSharp 均确认有 MouseBehaviorRecursive 接口。
- StS2 v0.111.0：ApiCheck，P3Makoto 906、P3MusicBox 241、P3TestMod 382 处引用，结果全部能找到 ✓。
- StS2 v0.107.1：同上，结果全部能找到 ✓。
- PCK 重打包 75,347,716 字节，90 个 ogg。两个游戏均安装 0.14.56，DLL/PCK/JSON 与构建来源哈希一致；旧版三个文件均先备份。音乐盒、测试工具和歌曲未改动。
- 未启动游戏，因此奖励、升级、重抽、快速开关和特写中的实际鼠标行为还需要用户复测，不能把 API 检查等同于完整运行验证。

测试命令（Godot 可执行文件可替换为本机 GDRE Tools）：

```powershell
& '<godot.exe>' --headless --path '<仓库外测试工作目录>' --script '<repo>/tools/compat/tests/pile_exit_mouse_filter.gd'
```

## 用户回归步骤

1. 完全退出并重开，确认日志 init v0.14.56。
2. 连续打开关闭抽牌、弃牌、消耗牌堆，分别用返回按钮、Esc，含特写中途关闭。
3. 战后奖励逐张悬停并选牌；休息点升级、事件删牌/变换、牌库等界面均应响应。
4. 至少重复十次，包括快速再次打开与重抽；确认退场期间不能再次点牌、退场后战斗恢复。

## 玩家“追踪之刃后君王之剑无动画”的另案检查

用户提供 Downloads/godot.log：游戏 v0.107.1，P3Makoto init v0.14.44，且由 SkinChanger 转换接口并接管视觉会话。日志明确报 GetGlobalBoneTransform、CardPlay.get_Player、GetGameVersion 缺失，47 ok / 3 failed。最新代码已经通过 Compat.BoneTransform、play.Card.Owner、Compat.GameVersion 规避这些直接依赖，双版本 ApiCheck 通过；用户也反馈 v0.14.56/0.107.1 未复现。此轮不为旧包重复修改动画。

限制：当前 SovereignTimingPatch 在追踪之刃生效时等待 Vorpal.Gate，最长 3.5 秒；若其他原因让 Persona/Vorpal 动画未启动，仍可能走超时兜底。API 检查不能排除资源缺失、SkinChanger 接管或其他运行时原因。因此结论是旧日志的接口问题在新版已有适配，不是保证任何条件下都不会等待。若最新版复现，需新版日志（含 init/version、CardPlayed/Persona/Spine/TriggerAnim 错误）再定位；目前没有足够证据修改正常伤害同步时序。
