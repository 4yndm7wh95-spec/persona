# 四个牌堆界面的蓝蝶环绕（本地 v0.14.58 基础）

## 用户要求和表现

- 抽牌堆、弃牌堆、消耗堆、牌组各一只蓝蝶。
- 复用伟大封印结局的分翼贴图、6 Hz 振翅、约 1.6 秒一次的滑翔、蓝色柔光和下落亮粉。
- 宽松地围绕立绘飞行，允许绕远；有明显升降和远近变化。人物始终在蓝蝶、柔光、亮粉之上。
- 保留之前的牌组立绘调整：参考 1920×1080 时中心 (350,590)，缩放 0.78，避免斜边切到脸和下巴。

## 实现

- `ButterflyVisual.cs`：从 `GreatSeal.Butterfly` 提取共用外观。结局的原样条、速度曲线、朝向平滑和总时长不变；最后一批亮粉仍保留约一秒收尾。
- `ButterflyOrbit.cs`：带二次谐波和深度的闭合投影曲线，采样后按屏幕弧长查位置；轻微速度变化连续积分，不在每圈开头重新加速。纵深控制大小和透明度。
- `PileButterfly.cs`：四套轨迹，约 100–120 参考像素/秒，绕一圈约 18–19 秒；单独适配较小的消耗堆立绘。轨迹不是永久画在界面上的线，实际只显示蓝蝶和短暂亮粉。
- `PileFx.cs`：装饰底板 → 蓝蝶及亮粉 → 立绘 → 卡牌/原有交互控件。牌组把立绘裁切单独放到一个仅裁切子节点的 Polygon2D 中，让蓝蝶可以越过斜分界线，又不改变立绘裁切边界。
- 所有新增视觉节点都是 Node2D/Sprite2D/Polygon2D，没有 Control 命中区，不修改卡牌 holder、Hitbox 或对象池的 MouseFilter。

## 进入、退出和清理

- 跟随原有立绘组滑入，同时沿轨迹切线靠近并约 0.9 秒淡入。
- 退出时记录当前局部位置和朝向，停止发射新亮粉，短距离顺势飞离；整组在现有 0.42 秒退出内淡出，不追加关闭等待。
- 使用牌堆既有 Tick；不注册额外的全局 ProcessFrame、不使用无限 Tween。根节点销毁时一起释放蓝蝶、亮粉和绑定在节点上的 Tween。
- 原有关闭/回滚逻辑仍负责解绑 Tick、回收卡牌；原生界面设置关闭时不创建本效果。

## 已做的验证

- Release 编译：0 警告、0 错误。
- ApiCheck：0.111.0 和 0.107.1 均“结果：全部能找到 ✓”；P3Makoto 检查 922 处引用。
- 直接编译生产用 `ButterflyOrbit.cs` 的本地验证：30/60/144 fps、每种 180 秒，合计 174636 项轨迹检查通过，覆盖闭环接缝、边界、速度、纵向行程及深度。可复现测试现已保存到 `tools/compat/ButterflyOrbitTests/`；测试直接编译生产轨迹类，结果 CSV 写入被忽略的测试 bin 目录。
- 纵向行程约 606–723 参考像素；放宽了最初过于贴近人物的轨迹，减少长期完全遮挡。
- 现有 PCK 已含 butterfly_wing_l / butterfly_wing_r / butterfly_body，无新增图片资源。

## 实机验收仍需用户进行

本次没有启动游戏，静态构图与数学/API 检查不能替代实际动画观感。请检查四个界面的蓝蝶大小、在白底上的可见度、人物遮挡、快速进入/退出和卡牌悬停/点击。应只有一只蓝蝶，退出后不能残留；牌组立绘的脸和下巴不应被斜边切掉。

版本号保持 0.14.58；这是该版本基础上的源码更新，不另行宣布版本号。启动黑屏的新增调查和规避修复见 `fix_startup_black_screen_preload.md`。

## 逐文件实施步骤（供 Claude 接续）

1. 在 `Core/ButterflyOrbit.cs` 中建立 768 段闭合曲线采样，累计二维投影弧长；`At(distance)` 用二分查找和线性插值返回位置、深度。速度轻微变化取积分，避免把时间直接乘变化速度造成位置跳跃。
2. 将 `GreatSeal.Butterfly` 中的分翼、身体、柔光、亮粉代码提取到 `Vfx/ButterflyVisual.cs`。`GreatSeal` 仍计算自己的原 Catmull-Rom 路线和渐入渐出，仅把位置/朝向/时间传给共用外观。结局的尺寸仍为屏高的 0.085。
3. 新增 `Vfx/PileButterfly.cs`，四种界面各创建一个实例。基准为 1920×1080，位置和尺寸乘视口高缩放 `k`；右侧立绘轨迹还加宽屏偏移 `rx`。参数如下：

| 界面 | 中心 | 横/纵范围参数 | 相位 | 平均速度（参考 px/s） | 尺寸参数 |
|---|---|---|---|---|---|
| 抽牌堆 | 1480,500 | 370,400 | 0.3 | 118 | 30 |
| 弃牌堆 | 1510,490 | 350,400 | 0.8 | 120 | 30 |
| 消耗堆 | 1530,530 | 260,340 | 2.6 | 100 | 23 |
| 牌组 | 490,490 | 350,410 | 2.2 | 118 | 30 |

4. 深度归一到 0–1；蓝蝶缩放从 0.72 到 1.08、亮度/透明度从 0.72 到 1。朝向按 `1-exp(-8*dt)` 平滑跟随切线，避免依赖帧率。维持人物在前，不根据深度切换到人物上层。
5. 在 `PileFx.Build` 的四个分支中，于底板完成后、立绘创建前创建蓝蝶。Deck 的底板仍独立绘制，新增 `ClipChildrenMode.Only` 的立绘裁切节点，防止蓝蝶被整块白底或斜边一起裁掉。
6. 在现有牌堆 Tick 中调用 `Update(now,dt)`，在 Exits 回调内记录 `BeginExit(now)`。进入时先沿切线接近、淡入；退出停止新粒子，并在已有 0.42 秒流程中离开、淡出。原 TreeExiting 清理自然销毁整组特效。
7. 牌组立绘中心由 (514,546) 调为 (350,590)，缩放由 0.90 调为 0.78，沿用原图、Spine 动画和斜裁形状。
8. 运行下面验证，确认通过后备份并更新两版游戏的 P3Makoto.dll。已有 PCK 的 SHA256 与此前安装包一致且含三张蓝蝶贴图，因此这次无需重打资源包；不改音乐盒、测试工具、存档或游戏设置。

## 验证复现命令

在仓库根目录运行，dotnet 使用本机已安装的 `C:/Users/kkk/sts2_tools/dotnet/dotnet.exe`：

```powershell
& <dotnet> build mod/P3Makoto.csproj -c Release '-p:GameDir=C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2'
& <dotnet> run -c Release --project tools/compat/ButterflyOrbitTests
& <dotnet> run -c Release --project tools/compat/ApiCheck -- <P3Makoto.dll> <G111>/data_sts2_windows_x86_64/sts2.dll <P3MusicBox.dll> <P3TestMod.dll>
& <dotnet> run -c Release --project tools/compat/ApiCheck -- <P3Makoto.dll> <G107>/data_sts2_windows_x86_64/sts2.dll <P3MusicBox.dll> <P3TestMod.dll>
```

测试覆盖的是轨迹连续性、速度和参考布局范围，并不验证真实 Godot 绘制、Spine 遮挡或输入交互。后者仍需人工进入游戏确认。
