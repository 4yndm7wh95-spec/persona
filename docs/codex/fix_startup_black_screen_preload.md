# 启动主菜单前黑屏：资源预加载调查及规避修复

## 症状和目前结论

2026-10-17 用户更新本地牌堆蓝蝶功能后，连续两次启动停在全黑画面，测试按钮和音乐盒仍能显示；用户确认主菜单尚未出现。当前启动为桌面目录下的 0.111.0，进程仍响应。

**已确认**：启动流程没有记下 `Preloading 'IntroLogo' Complete`，三个 P3Makoto Spine 资源也没有 ready 记录。

**推断而非已证实根因**：启动时同时提交多个 Spine 及其图集/骨骼依赖的后台加载，可能使共享资源加载任务长期等待。没有线程堆栈或修复后实机复测，不能把“并发加载死锁”当作已证实结论。

本次蓝蝶的创建入口在牌堆界面 Build，结局仍有独立入口，启动时不会创建蓝蝶；日志没有出现蓝蝶/牌堆 Build 的异常。新 DLL 安装后的启动时序仍可能让已有的预加载竞争更容易暴露，因此也不能仅凭入口位置断言本次更新与故障无关。

## 对照资料

摘录：`research/build_reports/startup_black_screen_20261017.txt`。仅收集启动时间、预加载和相关初始化记录，不上传完整日志；输出先做敏感字段过滤。

- 正常运行记录：`godot2026-10-17T07.55.27.log` 内部时间戳 07:23:45。五个 Spine 都有 ready，`IntroLogo` 在 29 ms 后完成。
- 第一次黑屏：`godot2026-10-17T07.56.26.log` 内部时间戳 07:55:28。10 条资源 ready 后，选人、弃牌堆、消耗堆的三条 ready 缺失；`IntroLogo` 无完成记录。
- 第二次黑屏：调查时的 `godot.log`，内部时间戳 07:56:27，同样缺失上述三条 ready，`IntroLogo` 无完成记录。
- 日志归档文件名是轮转时间，不能直接当作那次游戏的启动时间，以上以内文 Timestamp 为准。
- RitsuLib 选用了 0.107.0 兼容分支，在 0.111.0 上有两个 critical 补丁失败并回滚。但相同报错在正常游玩的旧日志中也存在，因此没有据此改动、禁用或移动其他 mod。
- P3Makoto 初始化为 65 ok / 0 failed；没有新的托管异常能直接解释黑屏。

## 修复步骤

1. 保留 `Vfx/Preload.cs` 的资源清单和普通贴图/字体预热，但在提交线程加载前跳过所有 `_skel_data.tres`。
2. 普通资源改用 `LoadThreadedRequest(..., useSubThreads:false)`，不再额外并行加载其依赖。
3. Spine 数据留给已有界面按需加载。牌堆原有 `SpinePortrait` / `Portrait` 仍使用 Spine 待机动画和静态贴图回退，没有删除立绘或禁用其动画。
4. 启动日志新增：`[P3Makoto] perf preload: Spine resources load on demand; startup threaded Spine warmup disabled`，便于确认运行的是修复后的 DLL。
5. Release 编译 0 警告、0 错误。ApiCheck 对 0.107.1 和 0.111.0 均通过；P3Makoto 922、音乐盒 241、测试工具 382 处引用全部能找到。
6. 用户正常退出黑屏进程后才安装。先备份旧 DLL，再将新 DLL 更新到两版游戏原有 P3Makoto 安装目录，SHA256 与构建产物一致；PCK、manifest、其他 mod、存档、设置和游戏原文件均未改动。

版本号仍为 0.14.58，不另行声明新发布版本。已更新的位置：

- `C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/mods/P3Makoto/P3Makoto.dll`
- `C:/Users/kkk/Downloads/Slay the Spire 2/mods/P3Makoto/P3Makoto/P3Makoto.dll`

## 后续验收和未确定项

未由 Codex 启动游戏，修复效果等待用户复测。验收应看新增日志、`IntroLogo ... Complete` 和主菜单是否出现，而不是只看 DLL 编译或 API 检查通过。

若仍黑屏，需保存该次最新日志并继续调查原版 NAssetLoader 的当前会话及三个 Logo 资源的加载状态，再考虑对备份 DLL 做对照测试；不要强行结束游戏、自动清空缓存或存档、禁用其他 mod、宣称已证明是 RitsuLib 导致。

潜在代价：Spine 首次按需加载可能带来短暂卡顿。后续若恢复预热，应在主菜单稳定后逐个加载并记录耗时，不能重新在启动 Logo 前并行提交全部 Spine。
