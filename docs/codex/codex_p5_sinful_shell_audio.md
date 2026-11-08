你在用户的 Windows 电脑上工作。任务：在本机的 P5R 里找到**撒旦耶尔「大罪穿甲弹」（Sinful Shell）**相关的原版音效 / 语音，转成 ogg 交上来。不改 mod 代码，不编译，不生图，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（仓库副本 C:\Users\kkk\sts2_tools\p5_build\repo 已经在这个分支上：先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-sinful-shell-audio，报告分支名。
游戏文件只读；提取出来的原始文件、中间文件只留在本机 C:\Users\kkk\sts2_tools\p5r_rip\ 下，**仓库里只提交挑出来的 ogg 和说明**。

【背景】用户要给 mod 里的「华丽收场」这张牌做大罪穿甲弹的演出（撒旦耶尔在 Joker 身后举起巨枪，和 Joker 同步开枪）。
Claude 在仓库已有的 research/p5r/audio/sfx/（BASE.CPK SOUND/SKILLSE/*.ACB 每个文件只提了第 0 个 cue）里按用户视频的声音做了互相关，**没找到可信的匹配**，
说明大罪穿甲弹的声音要么在这些 ACB 的其他 cue 里，要么在别的音频包里。

【比对目标】用户录的原版视频的声音（已放进仓库）：
- research/p5r/audio/sinful_shell/target_battle_11.0-16.2s.wav：战斗里放大罪穿甲弹那一段（Joker 拔枪特写 → 撒旦耶尔开枪，有战斗 BGM 混在里面）；
- research/p5r/audio/sinful_shell/target_event_full.wav：剧情里撒旦耶尔开出大罪穿甲弹那一段（3.9 秒）。

【要找的东西】
1. **大罪穿甲弹的技能音效**（最重要）：开枪的巨响、蓄力、子弹飞出去和命中的声音。
2. **撒旦耶尔现身 / 召唤的音效**（如果和普通召唤不一样）。
3. **Joker 战斗中喊人格面具 / 撒旦耶尔、或者放大技时的语音**（日语和英语各一份；有喊「サタナエル」/「Satanael」的最好）。

【建议的找法】（找不到就换下一条，每条都写进报告）
a. 解出技能名表（BATTLE/TABLE.PAC 里的 SKILL.TBL / NAME.TBL 一类），找到「大罪穿甲弹 / Sinful Shell」的技能编号，再看 SOUND/SKILLSE/ 下对应编号的 ACB，**把这个 ACB 里所有 cue 都提出来**（不只第 0 个）。
b. 撒旦耶尔的模型编号是 PS0170（仓库 art/p5/fx/v4/satanael/README.md 里有记录），找名字里带 0170 / satanael 的音频包、或技能动作文件里引用的音效名。
c. 都不行：把 SOUND/SKILLSE/ 下所有 ACB 的**全部 cue** 提出来，用上面两段目标音频做互相关（建议：转单声道 11025Hz，32 段对数频带能量做滑动相关），列出得分最高的 20 个；
   也查一下 SOUND/ 下别的目录（事件音效、BGM 以外的包）。
d. 语音：SOUND/ 下 Joker 战斗语音（BP01 一类）全部 cue，看文件内 cue 名 / 字幕表；找不到名字就按时长和目标音频比对。

【输出】research/p5r/audio/sinful_shell/：
- 挑出来的 ogg（原样转码，不改音色、不剪辑），文件名写清楚来源，例如 SKILLSE_0xxx_cue03.ogg、BP01_0123_ja.ogg；
- catalog.csv：文件名、来源包和 cue 编号、cue 名 / 流名、时长、和目标音频的相关得分（如果做了）、你判断它是什么（开枪 / 蓄力 / 命中 / 现身 / 语音）、把握程度；
- README.md（中文）：用了哪种找法、找到的技能编号、每个文件是什么、哪些只是候选。

【提交】只 add research/p5r/audio/sinful_shell/ 里的新文件；提交信息 "assets: P5R Sinful Shell / Satanael audio candidates"。
【报告】中文：找到了什么、把握多大、提交号或分支名。没找到也要报告试了哪些方法。
