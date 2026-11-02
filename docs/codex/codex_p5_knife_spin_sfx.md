你在用户的 Windows 电脑上工作。任务：给 Joker 的**转刀**找一组更有分量、更精良的候选音效，做成带编号的试听包，用户听完挑。
只读游戏文件，不改 mod 代码，不生图，不启动游戏，不编译。

**先读 docs/codex/README.md** 和 research/p5r/README.md（P5R 资源库、P5RAssetTool 按包内路径补解、vgmstream 用法）。
找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-knife-spin-sfx，报告分支名。

【背景】
拿起打击类牌时 Joker 会把刀在手里转一圈再握住（动画约 0.14 秒）。用户反馈：现在的声音像在空转，没分量。
Claude 先做了个过渡版（低沉挥动 SYSTEM #149 + 收刀金属声 SYSTEM #48），但最好找到原作里真正的刀具 / 转刀声。
想要的感觉：短促的金属旋转「嗖-锵」，最后有一下利落的握住 / 刀刃定住的落点；不要长风声、不要剑鸣拖尾。

【1. 找候选】（每个来源都写清楚找到什么、没找到什么）
1. **P5R Joker 自己的战斗动作音效**：research/p5r/audio/sfx/catalog.csv 里 C0001_051_B.DAT 解析失败。在本机资源库找到它，
   试着解开：先直接 vgmstream -m 探测；不行就在字节里找 "@UTF"、"AFS2"、"CPK " 这类头，切出内嵌的 ACB / AWB 再交给 vgmstream；
   也可以用 PersonaEditor 打开看看。同目录或 index.csv 里其他 C0001_*_B.DAT / Joker 战斗相关的音频包一起试。解出来的流全部转 OGG。
2. **P5R 战斗音效库**：research/p5r/audio_catalog.csv 里 SOUND/BATTLE/ 下除 BP01（语音）外的音效包，按名字 / 时长挑像刀具、金属、挥动的。
3. **P3R 本机音效库**：research/p3r_se/catalog.csv 里 cue 名含 draw / sheath / spin / flip / catch / metal / blade / knife / sword 的（例如 em0032_sheath、
   em0081_draw、em0081_sheath、em0063_draw、em0083_metal、sc0255_sword_whoosh*），以及主角武器相关的（cue 名或库名带 pc / wp / weapon 的）。
4. 每个候选：裁掉前后静音，单个不超过 1.2 秒；明显是长风声、爆炸、语音的不要。最多挑 24 个，宁缺毋滥。

【2. 输出】research/sfx_knife_spin/
- candidates/NN_<来源简写>_<原 cue 或流名>.ogg（NN 从 01 开始）。
- audition.mp3：按编号依次播放，每个之间空 0.8 秒；**开头先放一遍现在的过渡版**（编号 00：SYSTEM #149 在 0.02s + SYSTEM #48 在 0.052s 起播，音量 0.45 / 0.5），方便对比。
- README.md（中文）：一张表——编号、在 audition.mp3 里的起始秒数、来源（包 / 路径 / 流序号或 cue 名）、时长、你一句话的听感描述；以及第 1 步每个来源的尝试结果。
- 总量不超过 15MB。

【3. 提交】只 add research/sfx_knife_spin；提交信息 "research: knife spin SFX candidates (P5R Joker motion SE, P5R battle, P3R) with audition"。

【4. 报告】中文：每个来源找到多少个、C0001_051_B.DAT 解开没有（怎么解的或卡在哪）、你自己最推荐的 3 个编号和理由、提交号或分支名。

【绝对不要做】照 docs/codex/codex_p5r_model_fix.md 的清单；游戏目录只读；不编译、不安装、不启动游戏。
