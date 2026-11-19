你在用户的 Windows 电脑上工作。任务：给 P5Joker 的总攻击补三样 P5R 原版资源（喷血特效数据、碎玻璃 / 乱斗特效贴图、总攻击音效和语音）。只读游戏文件，不改任何已有代码，不生图，不启动游戏。

**先读 docs/codex/README.md（长期规则、合并规则），再读 research/p5r/README.md（资源库 C:\Users\kkk\sts2_tools\p5r_rip\、工具 C:\Users\kkk\sts2_tools\p5r_tools\、P5RAssetTool 按包内路径补解单个文件的方法）。**
**再读 docs/p5/ALLOUT_IMPLEMENTATION.md 和 tools/p5/allout_native/RESEARCH_LOG.md**：上一轮你把 BKSK_HERO_CTE.EPL（收尾 THE SHOW'S OVER）解成了 tools/p5/allout_native/decoded/layer_*.json，用的是 GFDLibrary（GFDStudio 0.10.3）+ 在内存副本里把 EPL 文件头偏移 8 的资源类型改成 Epl 再读。这次照同样的办法解另外几个文件。

【0. 绝对不要做】
- 不修改仓库里任何已有文件；只在下面指定的新目录里新建文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量；不安装需要管理员权限的东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 不删除任何目录或文件；不动游戏目录。
- 遇到没写到的情况或报错：停下来，把情况和完整报错写进报告，推送报告，结束。

【1. 仓库】
1. 用你上次成功推送的那个仓库（remote = github.com/4yndm7wh95-spec/persona），记作 <repo>；所有 git 命令加 -c safe.directory=<repo>。
2. git status -sb：有进行中的合并 / rebase 就停下来报告；说不清来源的改动停下来报告。
3. fetch 并切到分支 claude/exciting-babbage-lm8ubb：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 fetch origin claude/exciting-babbage-lm8ubb:refs/remotes/origin/claude/exciting-babbage-lm8ubb
   （失败等 5 / 10 / 20 / 40 秒重试，最多 4 次）。本地没有这个分支就 git switch -c claude/exciting-babbage-lm8ubb origin/claude/exciting-babbage-lm8ubb；有就 switch 过去再 merge --ff-only。不能快进就停下来报告。
4. 确认 tools\p5\allout_native\source\BKSK_SPLATTER.EPL 存在（稀疏检出缺目录时允许 sparse-checkout add tools/p5/allout_native research/p5r）。

【2. 喷血特效 BKSK_SPLATTER（最重要）】
BKSK_SPLATTER.EPL 里嵌着 be0cmn_bokosuka_splatterBlood.GFS（节点 Plane01–Plane04、SPARK0 等，贴图 be0cmn_upaura_uv_d.dds / smoke.dds / test.dds，还引用 be0cmn_smk_nml.EPD）。
1. 用和上一轮同样的方法读 tools\p5\allout_native\source\BKSK_SPLATTER.EPL：外层 EPL（节点树、启停时间、颜色 / 透明度轨道、各嵌套资源）和嵌套的 splatterBlood.GFS（全部节点、网格的顶点 / UV / 三角形、材质、贴图绑定、UV 变换、全部动画轨道，包括 UV 滚动 / 材质动画如果有）。
   输出到 tools\p5\allout_native\splatter\：outer.json、splatterBlood.json（照 decoded\layer_*.json 的格式，用 GFDLibrary 对象原样序列化）、nested\ 下原始 GFS 片段。
2. be0cmn_smk_nml.EPD 如果在 EPL 里或者包里能找到（按 research/p5r/index.csv 搜包内路径，需要的话 P5RAssetTool 补解），原样放 splatter\source\，能解析就同样导 JSON。
3. 如果 tools\p5\allout_native\bake 能直接复用（改输入路径即可），就也导出每节点 240 帧（60fps）局部矩阵 splatter\native_matrices.json；不能复用就跳过这条，在 README 写原因（不要改已有的 bake 代码，要改就复制一份到 splatter\bake\ 再改）。
4. 能的话用 Godot 4.5.1 渲一组预览：照 tools\p5\allout_native\godot\capture.gd 复制一份到 splatter\godot\ 改成播 splatterBlood（白底、黑色渲染即可，看形状和动态），60fps 输出后做逐 0.1 秒的拼图 splatter\contact_sheet.png 和一个 gif。渲不出来就跳过，写原因。

【3. 碎玻璃 / 乱斗 / 跳入特效】
按 research/p5r/index.csv 找这些包内文件（BATTLE/EVENT/BCD/GOODBYE/ 下或别处，按文件名搜），补解出来：
BKSK_JUMP_CTE.EPL、BKSK_JUMP_SMOKE.EPL、BKSK_ATTACK_CTE.EPL、BKSK_ATTACK.BCD、BKSK_JUMP.BCD，以及它们引用到的 bksk_attack_do.EPL、bokosuka_matte_attack.GFS、bksk_attack_kira.GMD（能找到多少算多少）。
1. 原始文件原样放 tools\p5\allout_native\source_more\。
2. 每个文件里内嵌的全部 DDS 导成原尺寸 PNG（保留透明通道，文件名 = <源文件名>__<贴图名>.png）放 tools\p5\allout_native\source_more\textures\，再做一张 contact_sheet.png（每张贴图配文件名，透明处用棋盘格）。重点是 glassBr_anim（碎玻璃）、black_jmp_bksk、剪影遮罩、kira 闪光、烟尘。
3. 能用同样方法解析的 EPL / GFS，节点树 + 网格 + 动画轨道导 JSON 放 source_more\decoded\（解不了的写原因，不要硬猜）。

【4. 总攻击的音效和语音】
1. 在 BKSK_HERO.BCD、BKSK_ATTACK.BCD、BKSK_JUMP.BCD 和上面的 EPL 里找声音引用（cue 编号、ACB 名、字符串）。BCD 的指令格式不确定就把疑似声音相关的原始字节段（十六进制 + 偏移）列出来，不要编结论。
2. 列出 research/p5r/audio_catalog.csv 里所有 cue 名包含 all_out / allout / bksk / bokosuka / finish 的 cue（SYSTEM.ACB 的 #24–29、#164 已经有了，不用重复导），以及 BATTLE / SKILLSE / 别的 ACB 里同类名字的 cue。
3. 雨宫莲（Joker）战斗语音的 ACB：把它的**全部 cue 名**列成表（cue 编号、cue 名、波形编号、时长）。cue 名里能看出是总攻击开始 / 收尾（例如带 bksk、allout、finish、soukou 之类）的，转 OGG。
4. 以上要导的音频转 OGG 放 research\p5r\audio\allout\，附 catalog.csv（文件名、来源 ACB、cue 编号、cue 名、时长、为什么认为相关）。cue 名完全看不出来的不要乱导，只把清单写进 README。

【5. 提交】
只 add 这几个新目录：tools/p5/allout_native/splatter、tools/p5/allout_native/source_more、research/p5r/audio/allout，和报告 docs/codex/reports/p5r_allout2.md。新增总量不超过 80MB（超了就把大的预览 gif 去掉，写进报告）。
README：每个新目录各一份 README.md（中文：是什么、怎么导的、解析到哪一步、哪些没解出来）。
git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P5R all-out splatterBlood data, jump/attack effect textures, all-out audio"
git -c safe.directory=<repo> -c http.version=HTTP/1.1 push origin claude/exciting-babbage-lm8ubb
推送被拒：先 pull --no-rebase --no-edit origin claude/exciting-babbage-lm8ubb 再推；有冲突就停下来报告。
最后在对话里回复：提交号 + 报告全文。
