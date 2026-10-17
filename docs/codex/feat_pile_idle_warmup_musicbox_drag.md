# 牌堆首次打开预热 + 音乐盒长按拖动

日期：2026-10-17。基于 P3Makoto 0.14.58（保留该版本号）；P3MusicBox 升为 1.1.1。

## 需求与判断

用户反馈抽牌堆、弃牌堆、消耗堆和牌组各自第一次打开会短暂停顿，并要求音乐盒图标可长按拖动、不能移出屏幕。

代码中每套立绘第一次调用 `ResourceLoader.Load`，还会首次解析 Spine、创建动画状态、上传贴图、栅格化菜单字体、编译水面着色器。重复打开可复用这些缓存。这是最可能的首次开销来源；本次没有启动游戏采集前后帧时间，因此不是经过实测的唯一根因结论。

此前启动黑屏曾与启动阶段的 Spine 并发预加载相关（见 `fix_startup_black_screen_preload.md`）。本次继续禁止在 Logo 阶段给 Spine 提交后台加载，也不把预热任务放进任何启动流程必须等待的 Task。

## 牌堆预热：实施步骤

1. `Vfx/Preload.cs` 仍只在线程加载普通字体、贴图。新增只读 `OrdinaryReady`，普通资源完成后才允许后续预热；`Start` 只注册空闲预热回调。
2. `Vfx/IdleWarmup.cs` 先判断原版加载器已存在、当前 session 完成且待处理队列为空，同时必须处于可见主菜单或非战斗地图，且没有全屏 capstone 界面。读取 `_instance`，避免原版 `Instance` getter 在启动未 Ready 时创建多余节点。
3. `Core/IdleWarmupGate.cs` 要求上述条件连续保持至少 800ms。加载任务、战斗、模态界面打断后重新计时。每 150ms 才查询一次条件，不逐帧遍历整棵场景树。
4. 逐项执行 15 个预热任务：水面 shader、三个菜单字体绘制任务、牌组/弃牌/消耗静态回退图、三张分翼蓝蝶贴图，以及抽牌/牌组/弃牌/消耗/选人五套 Spine 资源。抽牌静态立绘已由原 `Preload` 持有。
5. 每次只在主线程同步加载一个资源，强引用保留到进程结束。临时节点在屏内约 2px、alpha 0.004 处实际绘制四帧，让 GPU 参与；完全移到屏外会被裁掉，不能可靠触发首次绘制。Spine 播放 `idle_loop`，然后释放临时节点，下一项至少再隔 150ms。
6. 任务结束解绑全局回调；字段不可用时跳过预热；单项错误记录后继续其他项，最终 `complete=False` 如实反映不完整。界面的按需加载和静态回退保留。
7. 增加每项加载耗时、结束状态和每次 `PileFx.Build` 的总耗时/装饰耗时日志。原版卡牌池、命中区、卡牌数据和进入退出动画不变。

已只读检查本机 0.107.1 和 0.111.0 的 `NAssetLoader`：都有 `_instance`、`_currentSession`、`_sessions`，session 有 `IsCompleted`。这些私有字段不属于 ApiCheck 的静态 API 覆盖范围；其他游戏版本改动字段时会安全跳过，不强行加载。

资源预热完成前立即打开界面，仍可能产生首次开销；同步的单个 Spine 加载不能拆成多个 CPU 帧，可能在主菜单上产生一次短帧。强引用也会增加常驻内存。预热针对资源和首次绘制，不承诺消除游戏本身的卡牌创建、JIT、驱动或第三方 mod 开销。

## 音乐盒：实施步骤

1. 新增 `musicbox/src/IconDrag.cs`，GUI 信号和逐帧轮询共用同一个状态机，避免一次鼠标按下被记为两次。
2. 按下不再立即打开面板。450ms 内在图标上松手，且移动不超过 8px，才作为短按切换面板；达到 450ms 后进入拖动，保留鼠标在图标上的抓取偏移。长按、拖出图标再松手、短距离甩动或失焦都不会误触面板。
3. 按住时临时显示输入捕获层，避免移动鼠标时点到下面的卡牌；松开、失焦或图标因全屏界面隐藏时撤掉捕获。拖动开始关闭音乐盒面板；图标在歌单遮罩之上，面板打开时也能拖动它。
4. 限制 52px 按钮的左上坐标，并额外预留 8px，包含悬停放大后的外圈。按相对可用宽高保存 `IconX/IconY` 到现有偏好文件；旧设置缺字段时保持默认位置，分辨率变化后重新计算并限制在视口内。
5. 面板接近底部时向上打开；悬停标题和歌词位置也夹在视口内，避免移动图标后这些跟随元素按旧位置跑出屏幕。原播放、音量、歌曲扫描和歌词解析不变。
6. 音乐盒版本号与清单同步为 1.1.1；不写歌曲、歌词或歌单。

## 验证和复现

使用 `C:/Users/kkk/sts2_tools/dotnet/dotnet.exe`，三个工具无需新安装测试包：

```powershell
& <dotnet> build mod/P3Makoto.csproj -c Release '-p:GameDir=<G111>'
& <dotnet> build musicbox/P3MusicBox.csproj -c Release '-p:GameDir=<G111>'
& <dotnet> run -c Release --project tools/compat/MenuInteractionTests
& <dotnet> run -c Release --project tools/compat/ApiCheck -- <P3Makoto.dll> <G111>/data_sts2_windows_x86_64/sts2.dll <P3MusicBox.dll> <P3TestMod.dll>
& <dotnet> run -c Release --project tools/compat/ApiCheck -- <P3Makoto.dll> <G107>/data_sts2_windows_x86_64/sts2.dll <P3MusicBox.dll> <P3TestMod.dll>
```

- 两个 mod 的 Release 编译均 0 警告、0 错误。
- `MenuInteractionTests` 直接编译生产手势类和空闲门控类，28431 项检查通过。覆盖 449/450ms 分界、GUI/轮询重复按下、一次松手只触发一次、长按不点击、离开图标、四边限制、失焦取消、68×68 到 4K/超宽屏的位置保存与还原，以及原版加载重新开始后不得提前预热。
- 两个游戏版本的 ApiCheck 均“结果：全部能找到 ✓”。静态引用检查不能替代 Godot 输入捕获、渲染和真实帧时间测试。
- 不重新打包 PCK：本次没有新素材，原 PCK 已包含所需立绘、骨骼、蓝蝶和 Barlow 字体。仅备份并更新两版游戏的 P3Makoto.dll、P3MusicBox.dll/清单；音乐目录不动。

## 用户实机检查

1. 启动后主菜单仍应正常出现，停留数秒再进入战斗，依次第一次打开四种牌堆，比较停顿。
2. 日志应出现 `perf pile idle warmup finished; complete=True; failed=0`。每次打开有 `perf pile ... build ...ms (decor ...ms)`。若仍卡顿，请给出这些行及附近错误，继续区分资源/装饰构建和原版卡牌创建的耗时。
3. 音乐盒短按仍开关面板；按住约半秒再移动，图标跟随鼠标，松手不弹面板。拖到四个角，外圈都应完整可见。
4. 拖动中切出游戏或打开全屏界面，不应留下挡住卡牌的透明层；返回后可继续正常点击。
5. 重启、切换全屏/窗口和分辨率后图标仍在合理相对位置；播放/暂停、切歌、歌词和原版 BGM 淡入淡出保持正常。

本次没有启动或操作游戏；以上实机表现待用户验证。

## 本机安装结果

- 已确认无杀戮尖塔 2 进程后更新两边 DLL；安装文件与编译产物 SHA256 一致。
- 0.111.0：`C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/mods/P3Makoto/`、`mods/P3MusicBox/`。
- 0.107.1：`C:/Users/kkk/Downloads/Slay the Spire 2/mods/P3Makoto/P3Makoto/`、`mods/P3MusicBox/P3MusicBox/`。
- P3Makoto.dll 为 813568 字节，版本清单仍 0.14.58；P3MusicBox.dll 为 34304 字节，清单 1.1.1。P3TestMod 不变。
- 原 PCK 为 86927460 字节，13 条所需立绘/骨骼/蓝蝶/字体路径均找到，未覆盖 PCK。
- 两边音乐目录逐文件核对大小、修改时间、SHA256，均未变化。
- 旧文件备份：仓库 `mod/_build/backup_20261017_091512_0059_menu_warm_drag_111/` 和 `_107/`；安装核对清单保存在同目录 `menu_warm_drag_install_20261017_091512_0059.json`，均为被忽略的本机产物。
- 最终 ApiCheck：两版分别检查 P3Makoto 929、P3MusicBox 248、P3TestMod 382 处引用，均全部能找到。
