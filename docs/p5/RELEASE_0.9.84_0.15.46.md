# P5Joker 0.9.84 / P3Makoto 0.15.46（基于 codex 的 0.9.83 / 0.15.45 快照）

## 打包注意（给 codex）
1. P5：先 `python3 tools/p5/stage_mobile.py`（已经生成并提交在 `p5mod/pck_stage_mobile/`，改了骨骼或牌堆立绘才需要重跑），
   打包时**额外**打一个 `P5Joker.mobile.pck` 放在 `P5Joker.pck` 旁边（`tools/p5/cloud/build_package.sh` 已经会打）。
   手机上检测到这个文件才挂载（半分辨率战斗骨骼 + 0.75 倍牌堆立绘）；PC 不挂；不带这个文件时一切和以前一样。
2. 头像已换（`art/p5/ui/icons/joker_top_icon.png`，旧的是 `joker_top_icon_v1.png`），`p5mod/pck_stage` 里的 `icon_top` / `icon_top_outline` 已重新生成。
3. 两份 json 的 author / description 不要动（启动时会校验，不一致 mod 直接不加载）。`has_pck` 保持 false。
4. `mod/dist/P3Makoto.dll` 还是 0.15.45 的，按源码重新编一次。

## 改了什么
### 两个 mod
- **SkinChanger**：装了 SkinChanger 且 json 已切到 `has_pck=true`（SkinChanger 按皮肤接管）时，不再自己全局挂资源包
  （之前会把原版角色的资源全局换掉，选别的皮肤也会混进 Joker / 结城理）。只认真正启用的 SkinChanger（设置里关掉的不算）。
- **游戏 0.107 兼容**：0.9.83 新加的启动代码直接读 `Mod.workshopId`，这个字段 0.107 没有 → mod 在 0.107 上初始化失败、完全不生效。改成反射读。
  已用仓库里 0.107.1 的游戏程序集把两个 mod 都编过一遍：P3 无错；P5 剩下的两处都隔离在 try / 补丁类里，0.107 上自动退回。

### P5Joker
- 华丽收场的等待补丁同时挂 `Cmd.Wait` 的两个重载（游戏版本之间签名不同；转发版很小，可能被内联绕过补丁）。
- 毒必杀预告信：0.107 上自动改用原版的毒伤预测；类加回 `partial`（预览工具的编译需要，游戏里不影响）。
- 手机端：牌堆入场的 3D Joker 用 0.6 倍分辨率 + 2 倍多重采样（原来全屏 4 倍）；`P5Joker.mobile.pck`（见上）。
- 头像换成红底剪影。

### P3Makoto
- **iOS 开放加载**：之前要求运行时能把动态代码编译成机器码，iOS（解释执行）不满足 → 全部补丁和特效被关掉、只剩皮肤图。
  现在只要支持动态代码就逐个补丁尝试（和 P5Joker 在 iOS 上一样），挂不上的单独跳过、写日志。

## 玩家日志里看到的问题（不是我们的）
- `应用程序控制策略已阻止此文件 (0x800711C7)`（P5Joker.dll 加载失败）：Windows 11「智能应用控制」拦截了没有签名的 DLL。
  解决：右键 P5Joker.dll → 属性 → 勾「解除锁定」；还不行就在「Windows 安全中心 → 应用和浏览器控制 → 智能应用控制」里关掉（关掉后不能再打开）。
- `Method not found: Creature.DamageBlockInternal(Decimal, ValueProp)`（出牌卡住）：「诺亚」角色 mod（STS2mahonomajiyoBeta）不兼容 0.114。
- `NSpineAutoPlayer ... must have exactly 1 animation`、`Can not find animation: Intro / Idle`：别的静默猎手皮肤 / Mesugaki mod。
- `[AttackHitHook] ... could not be resolved`、`RitsuLib runtime initialization did not complete`：别的 mod 的框架。
- 旧版 P3Makoto 0.15.03 在 0.114 上 `DamagePatch` / `CharSelectInfoPatch` 失败：0.15.4x 已修。

## 需要实机确认
- 手机：装上 `P5Joker.mobile.pck` 后战斗里 Joker 的大小 / 位置和 PC 一样（预览里对比过，一样）；日志里有 `mobile textures mounted`。
- iOS：P3 日志里 `runtime: os=iOS ...` 和 `harmony patches applied: N ok`。
- SkinChanger：第一次启动后重启一次；Joker 不选时，选别的静默猎手皮肤不再混进 Joker。
- 0.107：两个 mod 能加载（日志 `init v0.9.84` 之后没有 `MissingFieldException`）。
