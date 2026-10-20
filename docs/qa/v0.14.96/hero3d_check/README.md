# v0.14.96 实测失败与热修复

## 测试结论

日志确认实际加载 v0.14.96。抽牌堆恢复 COMMU，但牌组仍回退旧版。不是装错版本，也不是 21 个水花/遮罩资源漏打包。

- v0.14.95：`hero3d unavailable; using portrait: missing / empty hero resource: anim/CampTopStart.scale.bin`，整个舞台失败。
- v0.14.96：`dive (deck pile) unavailable: missing / empty hero resource: anim/CampTopStart.scale.bin`，随后 `hero3d ready (dive unavailable)`。仅实现失败隔离，根因仍在。
- 两个安装目录的 PCK 已用 GDRE --list-files 实际列出；各自 10 个 splash、10 个 mask、warp.bin 逐一通过，清单见附件。

## 明确根因及修复

CampTopStart / CampTopLoop 的 JSON 中 scale_bones 数量均为 0，两份 scale.bin 因此都是合法的 0 字节。Clip 原先无条件对所有主角动作调用 Floats，而 Floats 先调用拒绝空字节的 Read，导致根本还没比较“预期长度也是 0”就抛错。

修复：只有 ScaleBones.Length > 0 时才读取缩放文件；否则使用空数组。仍有缩放骨骼的 COMMU 动作保持严格读取和长度检查，不放宽 GLB、JSON 或其他非空资源的校验。资源原样保留。

## 卡顿证据与改动

本次首开 Draw build 157.7ms：head=72.3、focus=34.8、tail=36.7、hero3d=1.1。首开前文字任务仅完成 #0～#4，不能保证中文已经预热；随后 Draw build 约 7.6～8.1ms。紧接首开有 374ms hitch，但不能把全部 374ms 都归给 157.7ms 构建。

两次 Deck build 75.4 / 76.8ms，decor 72.2 / 73.4ms，发生在入水加载失败回退期间。中途还有 86～185ms hitch，不能仅凭邻近记录断定全部来自文字预热。未完成预热时过早点击仍有首开成本，不能承诺此次完全消除。

修复预热调度中的一个确定缺口：此前只把“第一个中文字任务”当成重任务，鼠标活动时跳过它，却会继续运行后面的中文任务，导致后者抢先做首次初始化。现在首次中文预热成功前，所有中文任务都必须满足相同的安静/地图条件。

同时将中文大字号批次从 3 字缩为 1 字、小字号从 6 字缩为 2 字，降低单次不可中断工作；文字批次跨四帧保留绘制后，不再额外等待 150ms。原有空闲条件及牌堆打开时暂停预热仍保留。没有把 Godot 节点操作移到后台线程。

这些是控制批次大小及调度的改动，不等于已经测得性能改善。若在初始化前立刻点击，仍可能付出字体首次使用成本；需要下一次日志对照。

## 验证及安装

- Release 构建成功，0 警告、0 错误。
- 0.111.0 和 0.107.1 两次 ApiCheck 均“结果：全部能找到 ✓”。
- 核对 7 段主角动作的缩放长度：CampTop 两段为零骨骼/零字节；COMMU 五段均 16 根缩放骨骼，字节长度匹配。
- 检查 diff，非空缩放插值路径不变；文字批次仍在实际绘制数帧后回收。
- 热修复 DLL 已安装到桌面 mods/P3Makoto 与下载目录 mods/P3Makoto/P3Makoto，源/目标 SHA-256 一致。资源未变，沿用已核对的 PCK。版本号暂保留 0.14.96，使用热修复提交区分。
- 旧 DLL 备份到 mod/_build/backup_hotfix_20261020_005520_111 和对应 _107。
- 未启动游戏；尚无热修复后的实机日志。

## 附件

- v0.14.95-godot.log、v0.14.96-godot.log：修复前完整日志。
- 各版本 hero3d-dive.log：所有包含 hero3d / dive 的行及紧随的调用栈原文；完整上下文以主日志为准。
- installed-pck-111-dive.txt、installed-pck-107-dive.txt：实际安装包内 21 项资源清单。
- hotfix-api-111.txt、hotfix-api-107.txt：热修复兼容检查完整输出。

## 用户复测

重启游戏后各打开一次牌组、抽牌堆；预期 hero3d ready (dive ok)，不再出现 CampTopStart.scale.bin 的缺失/空资源错误。检查水花、倒入水人物和退场，并分别测试立刻点击及预热后首次点击的耗时。仍卡顿时用最新 build 分段和 hitch 继续定位。
