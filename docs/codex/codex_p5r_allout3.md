你在用户的 Windows 电脑上工作。任务：把 P5R 总攻击的原版资源**真正导出来并提交进仓库**（上一轮 docs/codex/reports/p5r_allout2.md 只交了索引和偏移，这一轮要实物）。只读游戏文件，不改任何已有代码，不生图，不启动游戏。

**授权说明（用户确认）**：和以前 research/p5r/ 下扒出来的其他资源（立绘 PNG、SYSTEM.AWB 的 OGG、日历音效等）一样，这一轮导出的贴图 PNG、音频 OGG、解码 JSON、渲染序列帧**都要提交进仓库**，这是这个任务的目的，不需要再自己判断要不要发布。原始容器文件（EPL / BCD / ACB / AWB）不用提交。

**先读 docs/codex/README.md、research/p5r/README.md、docs/p5/ALLOUT_PLAN.md（这次要什么、为什么要）、docs/codex/reports/p5r_allout2.md（你上一轮查到的偏移和 cue）、docs/p5/ALLOUT_IMPLEMENTATION.md（上一轮怎么解 BKSK_HERO_CTE 的）。**

【0. 绝对不要做】
- 不修改仓库里任何已有文件；只在下面指定的新目录里新建文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量；不安装需要管理员权限的东西（便携版工具可以放 C:\Users\kkk\sts2_tools\p5r_tools\ 下）。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 不删除任何目录或文件；不动游戏目录。
- 遇到没写到的情况或报错：停下来，把情况和完整报错写进报告，推送报告，结束。

【1. 仓库】
用上一轮你建的隔离工作树（从 claude/exciting-babbage-lm8ubb 建的那个）。所有 git 命令加 -c safe.directory=<repo>。
git -c safe.directory=<repo> -c http.version=HTTP/1.1 fetch origin claude/exciting-babbage-lm8ubb:refs/remotes/origin/claude/exciting-babbage-lm8ubb（失败等 5 / 10 / 20 / 40 秒重试，最多 4 次），再 merge --ff-only origin/claude/exciting-babbage-lm8ubb。不能快进就停下来报告。

【2. 音频（最先做，最重要）】
日版 SOUND_J/SOUND/SYSTEM.ACB 对应的 SYSTEM.AWB：流式第 11 号（battle_all_out_atk 的 7.35s 长音轨）、第 13 号（battle_all_out_atk_finish 的 7.2s）、第 14 号（battle_all_out_atk_finish_b 的 2.69s）转 OGG（用以前转 SYSTEM.AWB 第 37 / 82 号日历音效同样的工具和参数）。
放 research/p5r/audio/allout/：system_awb_011.ogg、system_awb_013.ogg、system_awb_014.ogg；在 catalog.csv 旁边新建 exported.csv（文件名、cue 编号、cue 名、AWB 编号、时长、采样率）。
英文版同号如果内容不同（比如带语音），也各导一份，文件名加 _en。

【3. 跳入 / 乱斗 / 烟团的贴图】
BKSK_JUMP_CTE.EPL、BKSK_ATTACK_CTE.EPL、BKSK_JUMP_SMOKE.EPL（上一轮 package_index.csv 里有包内路径）里内嵌的**全部 DDS**（上一轮 dds_headers.csv 列了 16 个头）导成原尺寸 PNG（保留透明通道），文件名 = <源文件名>__<贴图名或序号>.png，放 tools/p5/allout_native/source_more/textures/；再做一张 contact_sheet.png（每张配文件名，透明处棋盘格）。
能按上一轮解 BKSK_HERO_CTE 的办法（GFDLibrary + 内存副本改资源类型）读出来的嵌套 GFS / GMD（glassBr_anim、bokosuka_matte_attack、bksk_attack_kira 等），把节点树、网格、材质、动画轨道原样序列化成 JSON 放 source_more/decoded/。读不出来的写原因。

【4. 喷血 splatterBlood】
同样的办法解 BKSK_SPLATTER.EPL 里的 be0cmn_bokosuka_splatterBlood.GFS（Plane01–04、SPARK0）：外层 EPL 的节点树和轨道 → splatter/outer.json；嵌套 GFS 的网格、材质、贴图绑定、UV 变换、全部动画轨道 → splatter/splatterBlood.json；三张贴图 PNG → splatter/textures/。

【5. Joker 收尾的 3D 动作（序列帧）】
原版总攻击收尾前 Joker 从上方落下、单膝落地、外套甩开、站起、扯手套（约 1.7s），由 BKSK_HERO.BCD 调用。
1. 在 BKSK_HERO.BCD（651 字节）里找角色动作调用：把全部字节按 4 字节一组列成十六进制表放 tools/p5/allout_native/source_more/bksk_hero_bcd_dump.txt，标出疑似动作编号 / GAP 名的位置。
2. 对照 research/p5r/joker_battle_anim_list.csv（3064 条，名字是 GAP 索引）和你之前渲动作用的流程（research/p5r/REPORT_glove_*.md 等），找出时长约 1.5–2.5s、内容是"落下 → 落地 → 站起 → 扯手套"的那一段（可以先把时长 1.4–2.6s 的候选各渲一张 6 格缩略图拼起来比对，放 research/p5r/allout_motion/candidates.png）。
3. 找到以后用修好的模型（research/p5r/model/joker/fixed/joker_thief.glb，戴面具版）渲 30fps 序列帧，放 research/p5r/allout_motion/<动作名>/：
   - color/：全彩，透明背景，1024×1024，正交相机，人物面朝画面右侧的 3/4 侧身，落地点在画面下方 1/4 处（和以前渲动作的统一设置一样）；
   - sil/：同一相机的纯黑剪影版（透明背景）。
   再做一张逐帧拼图 sheet.png。找不到确定的那一段就只交候选拼图，不要硬选。

【6. 提交】
只 add：research/p5r/audio/allout、tools/p5/allout_native/source_more、tools/p5/allout_native/splatter、research/p5r/allout_motion，和报告 docs/codex/reports/p5r_allout3.md。新增总量不超过 120MB（超了先把序列帧缩到 768，再不行写进报告）。
每个新目录的 README.md 补上这一轮新增的内容。
git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P5R all-out audio streams, effect textures, splatterBlood data, Joker finish motion frames"
git -c safe.directory=<repo> -c http.version=HTTP/1.1 push origin claude/exciting-babbage-lm8ubb
推送被拒：先 pull --no-rebase --no-edit origin claude/exciting-babbage-lm8ubb 再推；有冲突就停下来报告。
最后在对话里回复：提交号 + 报告全文。
