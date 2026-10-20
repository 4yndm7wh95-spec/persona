# v0.14.91 首次打开抽牌堆卡顿：日志与推断

用户反馈：首次点击抽牌堆仍卡顿，要求截取刚才测试的日志、推断原因并推送仓库。本次仅保存日志与分析，不修改运行代码，不启动游戏。

## 来源与复现证据

- 被测代码：`dde5206`，日志确认 `[P3Makoto] init v0.14.91`；游戏为桌面目录 v0.111.0、D3D12、RTX 4060 Laptop。
- `godot.log`：本次完整主日志，原始文件内容直接复制。原文件最后写入时间标记为 2026-10-19 23:57:52。
- `p3mp_20261019_235725_pid27828.log`：同次测试的测试工具原日志，起始记录为 23:57:25.808，pid 27828。
- `test_excerpt.log`：主日志第 401～482 行的连续原文，覆盖继续游戏、预热、卡框异常和多次开牌堆。没有删行、替换或重写消息；摘录按行另存 UTF-8。
- 原日志无逐行时间戳，不能据此给出点击的绝对时刻。首次出现的 Draw build 是本次日志中首次抽牌堆构建。

## 可以确认的事实

1. 首次 `Draw build 141.9ms`，分段为 `head=70.6`、`focus=30.6`、`tail=34.0`，合计 135.2ms，约占构建时间 95.3%。装饰总体仅 6.4ms，`hero3d=0.6ms`。随后两次 Draw build 分别降为 3.6ms、3.9ms，符合首次使用才有的开销特征。
2. 首次构建后记录 `health spike pile screen 143.6ms` 和 `health hitch 335ms ... p3=146.2ms`。说明本 mod 同步界面搭建确实占了显著时间，但 335ms 不等于 141.9ms：尚有未归因的时间，不能把整次卡顿全部归给某个已计时分段。
3. 首开之前，`@hero3d` 已预热，耗时 83.1ms；`@card frame` 为 44.7ms。预热本身也并非完全无主线程成本。
4. 首开时 `idle warmup complete=False`，最后一项完成后仍有 6 项剩余。队列中地图专属的重项（5 份 Spine 数据和 `@pile chinese`）在战斗场景不会执行。本次为继续存档后直接进入战斗，因此不能保证在首次开牌堆前经过地图预热。`complete=False` 本身不等于 3D 未加载。
5. 首开前出现三组 `CardFrameSkin.Apply(NCard card)` 空引用异常及重复 WARN；当前日志无源码行号，不能定位到该方法的具体语句。首开附近另有 `Asset not cached: res://images/packed/sprite_fonts/star_icon.png`。
6. 本次没有 `hero3d unavailable` 或 `Attempt to disconnect`。首次弃牌堆仍为 77.2ms（decor 36.6ms、head 38.5ms）。

## 原因推断（不是已证实的函数级归因）

### 高优先级：2D 界面首次构建路径预热不完整

证据最强的是 head / focus / tail 的首开与复开差异。`PileFx.cs` 的 `lap("head")` 包含标题、计数、说明、名牌与 EMPTY 标签及相关节点构建；`focus` 包含 `FocusLayer` 的图形与多个标签；`tail` 包含其后所有初始化、升级按钮、列表准备和事件绑定等，不能把它只当作某个尾部函数。

当前 `IdleWarmup` 的文字预热仅覆盖一部分 Barlow 字号和 19px 中文（且中文仅地图执行）。实际界面还有 112px 计数、90px EMPTY、32px 名牌、54px 特写名称、40/34px 特写说明、20px 中文提示等，均随 k 缩放；描边和文本组合也不同。动态字体回退、字号/描边缓存、排版及节点首次调用/JIT 都是候选。现有日志不能把 70.6ms 精确拆分成字体、原生节点构建或 JIT，因此不要直接断言全是字体栅格化。

建议先把 head 拆到 title / count / note / nameplate / empty；focus 拆到 geometry / labels；tail 拆到 upgrade / list / input / audio。记录实际 k、字号、中文预热是否完成。之后按实测覆盖实际字体字号/描边和必要首次调用，或缓存可复用 UI / 延迟创建隐藏特写。避免把同等重活挪到选人或战斗主线程，造成另一处卡顿。

### 并行排查：卡框空引用与未缓存的星形图标

空引用发生在首开前，可能带来异常构造、堆栈与日志开销，或使卡框应用路径失败；但日志不能证明它就是 head/focus 的耗时来源。建议为 Apply 内部阶段加定位信息，核对卡牌生命周期和空值条件，修复后再对照首开分段。星形图标的首次读取可能贡献未计入 build 的后续时间，但目前只有一条未缓存警告，不能量化。

### 目前不支持将主要原因归于 3D 模型重新加载

hero3d 打开分段仅 0.6ms，之后 0.1ms，且此前预热完成，因而没有“点击时重载模型花了百毫秒”的证据。hitch 附近记录 gpu 1.7ms、render_cpu 0.5ms，不像持续 GPU 绘制瓶颈；不过 Health 读取的是视口渲染计时，不能覆盖所有驱动编译/等待、子视口成本及 CPU 停顿。日志中的 D3D12 PSO 缓存警告也不足以单独证明发生了 shader 编译卡顿，剩余耗时需 profiler 或更细分计时确认。

## 其他日志与边界

启动时其他模组存在 __MACOSX 清单解析失败和 RitsuLib 目标方法不匹配；退出时有 RID / 资源泄漏报告。这些应另行登记，现有时序不能证明它们造成首次抽牌堆的 141.9ms 构建。没有移除或改动其他模组。

## 关键原文

```text
[P3Makoto] perf idle warmup @hero3d 83.1ms; 15 remaining
[P3Makoto] perf idle warmup @pile title 1.3ms; 13 remaining
[P3Makoto] card frame failed: System.NullReferenceException: Object reference not set to an instance of an object.
   at P3Makoto.Scenes.CardFrameSkin.Apply(NCard card)
   at P3Makoto.Scenes.CardFrameReloadPatch.<>c__DisplayClass0_0.<Postfix>b__0()
   at P3Makoto.ModEntry.Safe(String where, Action action)
[P3Makoto] perf pile Draw build 141.9ms (decor 6.4ms; hide=0.1 water=0.2 bubbles=0.9 butterfly=3.4 hero3d=0.6 decor=0.8 head=70.6 focus=30.6 tail=34.0); idle warmup complete=False
[P3Makoto] health spike pile screen 143.6ms polys+42 labels+0 anims+1
[WARN] Asset not cached: res://images/packed/sprite_fonts/star_icon.png
[P3Makoto] health hitch 335ms gpu=1.7ms render_cpu=0.5ms p3=146.2ms process=37.6ms vfx_nodes=26 fps=191
[P3Makoto] health hitch 88ms gpu=1.1ms render_cpu=0.3ms p3=0.0ms process=6.4ms vfx_nodes=5 fps=200
[P3Makoto] perf pile Draw build 3.6ms (decor 0.7ms; hide=0.0 water=0.0 bubbles=0.3 butterfly=0.1 hero3d=0.1 decor=0.1 head=1.0 focus=0.7 tail=1.3); idle warmup complete=False
[P3Makoto] health hitch 96ms gpu=1.1ms render_cpu=0.3ms p3=0.0ms process=87.5ms vfx_nodes=4 fps=186
[P3Makoto] perf pile Discard build 77.2ms (decor 37.1ms; hide=0.0 water=0.0 bubbles=0.3 decor=36.6 head=38.5 focus=0.9 tail=0.7); idle warmup complete=False
[P3Makoto] health spike pile screen 77.3ms polys+40 labels+0 anims+1
[P3Makoto] health hitch 95ms gpu=0.6ms render_cpu=0.3ms p3=77.5ms process=87.5ms vfx_nodes=4 fps=186
[P3Makoto] perf pile Draw build 3.9ms (decor 0.7ms; hide=0.0 water=0.0 bubbles=0.3 butterfly=0.1 hero3d=0.1 decor=0.1 head=1.4 focus=0.7 tail=1.2); idle warmup complete=False
```

上面的关键行是非连续摘选；连续原文见 test_excerpt.log，完整会话见 godot.log。
