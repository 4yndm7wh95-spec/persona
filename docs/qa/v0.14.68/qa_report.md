# v0.14.68 设置界面实机复验

基线：db43e33f63ecc67abb3f222da095786ee6a089a5。2026-10-18，G111 v0.111.0，正常启动入口 SlayTheSpire2.exe。指定编译会话完成构建、安装及双版本ApiCheck后已空闲，本会话串行使用 Computer Use 验收。截图为工具直接捕获的原始JPEG，未改图；文档的PNG命名以同名JPEG保存。共26张截图。

结论：不通过。整屏持续压暗、语言下拉框混入和分页筛选还原已有改善；内容没有移动到右侧，右侧面板仍盖住控件，分页点击区域还抢占滑条操作。原版外观回退有标签颜色残留。

## 逐项结果

| 项目 | 结果 | 实际观察与证据 |
| --- | --- | --- |
| R1 | 不通过（明暗部分通过） | R1.jpg：背景、白面板、人物已清晰明亮。设置行仍横跨约x350至1190，右侧深蓝面板约x910至1520覆盖控件，分页大字仍压在设置行上。不是转场：停留多次稳定截图同样。 |
| R2 | 部分通过 | 五个分页可点击，R2_*.jpg。P3MAKOTO不再出现中文语言下拉框，标题正确。P3MAKOTO返回GENERAL后语言、屏幕抖动、加速模式、计时等原版行恢复。每一页仍有R1布局/面板遮挡问题。INPUT内容亦跨中部。 |
| R3 | 部分通过 | R3_hover.jpg可见白底红框，但在分页下方。分辨率下拉可打开；部分选项也被深蓝面板遮挡。点外面x1380,y600只关下拉框、设置保留；再次打开后Esc退出整个设置（R3_dropdown_escape.jpg）。未选择新分辨率，仍1920x1080。 |
| R4 音量 | 不通过 | SOUND原音乐0%、全局48%。从可见音乐滑条左端x904,y256拖到x932,y256，实际切换到GENERAL，音乐没有调节。R4_slider_change.jpg为误触后的转场帧，不是成功调值；R4_slider_hit_general.jpg为稳定GENERAL证据。重新返回SOUND确认音乐仍0%（R4_music_still_zero.jpg），全局仍48%。因分页点击区抢占，不继续盲拖遮挡控件。 |
| R4 计时复选框 | 功能通过，显示不通过 | 原“始终显示局内计时”关闭。点击x1035,y451开启，出现“现在局内计时将一直被显示”提示；关闭并重开仍开启。再点同位置，出现“现在局内计时只有在暂停时才会显示”，恢复原关闭。R4_timer_*.jpg；框变化和文字受面板遮挡，不能算视觉验收通过。R5_off.jpg原版框已为空进一步确认恢复。 |
| R5 | 内容恢复通过，颜色还原不通过 | P3MAKOTO底部皮肤完整→关闭→关闭设置→重开，回到原版。语言、屏幕抖动、加速等原版项在顶部（R5_off.jpg），滚到底部模组项也在（R5_off_bottom.jpg）。但曾悬停/操作过的计时器、分辨率、皮肤开关标签留深蓝字，原版暗背景几乎不可读。原版分辨率下拉+Esc同样退出整个设置（R5_original_dropdown.jpg、R5_original_escape_stable.jpg）；与皮肤开启时一致，不能据此定性为新皮肤独有回归。皮肤已由关闭恢复完整（R5_toggle_restore.jpg），关再开恢复P3R；GENERAL原版项仍完整（R5_on.jpg），滚动顶部已恢复。 |
| R6 | 微动可见，眨眼待确认 | 连续操作过程中人物边缘/飘带有微小变化，水纹明显变化；另等待8秒取样R6_after8s.jpg。离散截图未可靠捕获完整眨眼周期，不声称通过，也不据此断言眨眼损坏。 |
| R7 | 通过 | settings_tree.log原样保存全部匹配settings layout/tree行140行；runtime_filtered.log保存P3Makoto且匹配settings/skin/error/failed/health spike等行，并包含init。 |
| R8 | 本次提交 | 只新增本目录截图、日志、报告。 |

## 优先修复

1. P1：内容容器没有移动/缩放，深蓝内容背景画在原版控件之上，既布局错又盖暗控件（R1、所有R2）。请根据完整settings_tree.log核对真正被移动的节点和绘制层级。
2. P1：分页点击区覆盖控件，音乐滑条左端拖动触发GENERAL。复现坐标是1536x864窗口的x904,y256→x932,y256；所有坐标仅为本次窗口。修好后重测滑条数值变化和恢复。
3. P2：关闭皮肤后曾操作/悬停行标签仍为深蓝，原版暗背景不可读。计时器、分辨率、设置界面皮肤标签均有证据。请还原主题/富文本颜色以及可见状态。

日志布局第一行：source=scroll (-338, -72), (1920, 1080)，manager (0, 0), (1244, 90)，view (1920,1080)，manager parent Control。这是实机日志，不是代码推测。完整节点结构见附档。

## 构建与限制

指定编译会话报告：P3Makoto编译6警告0错误，均Popups.cs：348行CS8602；357、360、363、364、365行CS8604。MusicBox/TestMod 0警告0错误。两版ApiCheck通过。与Claude“警告消除”说明存在差异，供下一轮处理；本会话未改代码。

只操作主菜单设置；未继续、新建或放弃现有游戏，未修改存档。局内暂停、0.107.1实机、手柄按新说明延后。没有改分辨率、语言、按键或其他复选框。音乐拖动未改变数值，计时恢复关闭，皮肤恢复完整。游戏通过主菜单退出及确认框正常退出。未读取obs.json、密码。treasure_hands原样未动。整体完成标准仍未达成。

## 筛选运行日志（完整节点结构另见settings_tree.log）

```text
[P3Makoto] init v0.14.68
[P3Makoto] harmony patches applied: 72 ok, 0 failed
[P3Makoto] health spike asset guard 288.9ms polys+0 labels+0 anims+0
[P3Makoto] cursor skin applied: 64x64, hotspot=(14,5), attempt=1
[P3Makoto] health spike shader warmup 314.3ms polys+2 labels+6 anims+0
[P3Makoto] health spike health 6.4ms polys+0 labels+0 anims+0
[P3Makoto] health spike settings section 26.0ms polys+0 labels+0 anims+0
[P3Makoto] perf pile idle warmup finished; complete=True; failed=0
[P3Makoto] health spike settings skin visible 664.2ms polys+31 labels+18 anims+0
[P3Makoto] settings layout: source=scroll (-338, -72), (1920, 1080) (scroll (-338, -72), (1920, 1080), manager (0, 0), (1244, 90)), view (1920, 1080), manager parent Control
[P3Makoto] health p3_ms_per_frame=0.36 over 3650 frames; top: settings skin visible 0.18ms×1 (max 664.2), shader warmup 0.09ms×1 (max 314.3), asset guard 0.08ms×2 (max 288.9), charselect info warmup 0.07ms×1 (max 241.5), settings section 0.01ms×1 (max 26.0), timescale watchdog 0.00ms×3651 (max 0.2), health 0.00ms×3649 (max 6.4), star orbit 0.00ms×3650 (max 0.4)
[P3Makoto] health p3_ms_per_frame=0.01 over 4497 frames; top: timescale watchdog 0.00ms×4497 (max 0.1), star orbit 0.00ms×4497 (max 0.1), health 0.00ms×4497 (max 0.4), clip Sys_Cursor 0.00ms×4 (max 1.6), settings tab word 0.00ms×19 (max 0.7), clip ShuffleTime_Whoosh 0.00ms×3 (max 0.5), clip Btl_Ok 0.00ms×3 (max 0.5)
[P3Makoto] health p3_ms_per_frame=0.01 over 4494 frames; top: timescale watchdog 0.00ms×4494 (max 0.1), star orbit 0.00ms×4494 (max 0.1), health 0.00ms×4494 (max 0.4), clip Sys_Cursor 0.00ms×2 (max 0.5), clip ShuffleTime_Whoosh 0.00ms×2 (max 0.5), settings tab word 0.00ms×18 (max 0.4), clip Btl_Ok 0.00ms×2 (max 0.4)
[P3Makoto] health p3_ms_per_frame=0.01 over 4501 frames; top: timescale watchdog 0.00ms×4501 (max 0.1), star orbit 0.00ms×4501 (max 0.1), health 0.00ms×4501 (max 0.3), clip Sys_Cursor 0.00ms×2 (max 0.5), settings tab word 0.00ms×12 (max 0.5), clip Btl_Ok 0.00ms×2 (max 0.5), clip ShuffleTime_Whoosh 0.00ms×2 (max 0.5)
[P3Makoto] health p3_ms_per_frame=0.01 over 4501 frames; top: timescale watchdog 0.00ms×4501 (max 0.1), star orbit 0.00ms×4501 (max 0.1), health 0.00ms×4501 (max 0.4), settings skin visible 0.00ms×2 (max 0.1), settings tab word 0.00ms×1 (max 0.0)
[P3Makoto] health p3_ms_per_frame=0.01 over 4499 frames; top: timescale watchdog 0.00ms×4499 (max 0.2), star orbit 0.00ms×4499 (max 0.1), health 0.00ms×4499 (max 0.2), settings skin visible 0.00ms×2 (max 0.0)
[P3Makoto] health p3_ms_per_frame=0.01 over 4501 frames; top: timescale watchdog 0.00ms×4501 (max 0.1), star orbit 0.00ms×4501 (max 0.1), health 0.00ms×4501 (max 0.3), settings toggle 0.00ms×1 (max 1.6), settings tab word 0.00ms×6 (max 0.5), clip Btl_Ok 0.00ms×1 (max 0.5), clip ShuffleTime_Whoosh 0.00ms×1 (max 0.5), clip Sys_Cursor 0.00ms×1 (max 0.5)
[P3Makoto] health spike settings skin visible 65.0ms polys+0 labels+0 anims+0
[P3Makoto] health p3_ms_per_frame=0.02 over 4497 frames; top: settings skin visible 0.01ms×2 (max 65.0), timescale watchdog 0.00ms×4497 (max 0.1), star orbit 0.00ms×4497 (max 0.1), health 0.00ms×4497 (max 0.3)
[P3Makoto] health p3_ms_per_frame=0.01 over 4501 frames; top: timescale watchdog 0.00ms×4501 (max 0.1), star orbit 0.00ms×4501 (max 0.0), health 0.00ms×4501 (max 0.3), settings skin visible 0.00ms×2 (max 0.0)
[P3Makoto] health p3_ms_per_frame=0.01 over 4501 frames; top: timescale watchdog 0.00ms×4501 (max 0.1), star orbit 0.00ms×4501 (max 0.1), health 0.00ms×4501 (max 0.5), settings toggle 0.00ms×1 (max 1.1)
[P3Makoto] health spike settings skin visible 14.9ms polys+31 labels+18 anims+0
[P3Makoto] settings layout: source=scroll (-338, -72), (1920, 1080) (scroll (-338, -72), (1920, 1080), manager (0, 0), (1244, 90)), view (1920, 1080), manager parent Control
[P3Makoto] health p3_ms_per_frame=0.01 over 4500 frames; top: settings skin visible 0.00ms×2 (max 14.9), timescale watchdog 0.00ms×4500 (max 0.1), star orbit 0.00ms×4500 (max 0.1), health 0.00ms×4500 (max 0.3)
[P3Makoto] health p3_ms_per_frame=0.01 over 4501 frames; top: timescale watchdog 0.00ms×4501 (max 0.1), star orbit 0.00ms×4501 (max 0.1), health 0.00ms×4501 (max 0.3), clip Sys_Cursor 0.00ms×1 (max 0.5), clip ShuffleTime_Whoosh 0.00ms×1 (max 0.5), settings tab word 0.00ms×6 (max 0.5), clip Btl_Ok 0.00ms×1 (max 0.5)
[P3Makoto] health p3_ms_per_frame=0.01 over 4501 frames; top: timescale watchdog 0.00ms×4501 (max 0.1), star orbit 0.00ms×4501 (max 0.1), health 0.00ms×4501 (max 0.3), settings skin visible 0.00ms×1 (max 0.0), settings tab word 0.00ms×2 (max 0.0)
```

