你在用户的 Windows 电脑上工作。任务：**只把撒旦耶尔第 10 版手里那把短管枪加长**。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。
仓库规则同 docs/codex/codex_p5_satanael_layers_v10.md（分支 claude/epic-archimedes-m9an69，先 fetch 再 merge --ff-only；网络慢就推到 codex/p5-satanael-gun-len）。

【用户意见（2026-11-09）】fix2 之后的枪（check_fix2_v10.png）**又太短了**：现在只比拳头长一点，像一把小手枪，配不上这么大的人格面具。
要介于两者之间：**枪管长度改成原版长枪的六成左右**（之前要求四成，太短）——屏幕上整支枪（枪柄到枪口）大约是**拳头宽度的 4～5 倍**，
枪管又粗一点，枪口金属环明显；保留金色扇形装饰、金色枪柄、黑金配色。手臂、手、握法、枪的方向（朝右下约 23°）**都不变**，只把枪管往前接长。
【输出】覆盖 layers_v10/arm_fore_gun.png（手 + 新枪）、gun.png（转枪用的侧面整枪，同样加长）、gun_short_side.png；
被新枪管挡住的翅膀部分不用补（枪在最前面）。更新 layers.json 的 muzzle（新的枪口中心）、barrel_dir、gun_guard、spin_finger。
【检查】check_gun_len_v10.png：fix2 的枪 / 加长后的枪并排；前臂上翻 +40°（后坐）叠图。最多重画 2 次。
【提交】只 add 改动的文件；提交信息 "art: Satanael v10 gun barrel lengthened (about 60% of the original long gun)"。
