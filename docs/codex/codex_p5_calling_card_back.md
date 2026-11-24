你在用户的 Windows 电脑上工作。任务：从本机 P5R 资源里**只读地**找出「预告信」的原版贴图（剧情里怪盗团真的发给目标的那封信：剪报拼贴字写的正文，例如「致色欲之罪人 鸭志田卓……我们将夺走你扭曲的欲望……心之怪盗团」），导出原质量 PNG 进仓库。写报告。不改代码，不生图，不启动游戏。

**先读 docs/codex/README.md，按里面的长期规则来。**

【背景】
- 牌堆界面里有一张悬浮旋转的预告信卡。正面已经用原版 CAMP/P5_CARD02（红黑同心圆 + 礼帽 + TAKE YOUR HEART）。
- **背面要用原版预告信正文那一面**。暂停菜单资源里没有，要在别的地方找。最可能在：
  1. **MODEL/ITEM/**（1208 个道具模型 GMD，共约 510MB）：剧情里拿在手上 / 贴在墙上的预告信道具，贴图嵌在 GMD 里；
  2. **MYPALACE/GALLERY/**（「我的宫殿」画廊，200 张缩略图 THM/THMxxx.DDS + 200 张大图 IMG/IMGxxx.DDS）：可能有预告信的图；
  3. **MODEL/FIELD_TEX/TEXTURES/**（场景贴图，非常大，只读目录不解贴图）：比如学校布告栏上贴的预告信。
- 已知格式：GMD 里的贴图是嵌入的 DDS / TMX，PersonaEditor / GFD 工具能读；上一轮 README（research/p5r/README.md）写过工具和命令。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confident-hypatia-nropgi**。
P5R 资源库：C:\Users\kkk\sts2_tools\p5r_rip\；工具：C:\Users\kkk\sts2_tools\p5r_tools\（直接用上一轮下好的）。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/confident-hypatia-nropgi:refs/remotes/origin/claude/confident-hypatia-nropgi
失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。基于 origin/claude/confident-hypatia-nropgi 提交。
工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立 worktree 只提交本任务的文件。

## 2. 找（按顺序，找到可信的就可以停，但第 1、2 项都要做完）
解出来的原始文件都放 p5r_rip\raw\ 保留包内路径（不进仓库）。
1. **MODEL/ITEM/**：按 research/p5r/index.csv 的路径把 MODEL/ITEM/ 下全部 GMD 解出来，读出每个 GMD 里嵌入的贴图名和尺寸，写 <repo>\research\p5r\calling_card\item_textures.csv（GMD 名、贴图名、宽、高、格式）。
   - 把所有嵌入贴图转成 PNG 存在 p5r_rip\item_tex\（不进仓库），再拼缩略图联系表（每格 160px、写 GMD 名 + 贴图名，深灰蓝底）放仓库 calling_card\item_sheets\（每张联系表不超过 8MB，拆成多张）。
   - 挑出看起来是预告信的：红黑白配色、剪报拼贴字、心之怪盗团礼帽标志、TAKE YOUR HEART、整段正文那种，或者贴图名含 card / yokoku / yokokujo / kokuchi / letter / tegami 的。
2. **MYPALACE/GALLERY/THM/**：200 张缩略图转 PNG，拼联系表放 calling_card\gallery_thumbs.png（每格写编号）。有预告信的，把对应的 IMG/IMGxxx.DDS 解出来。
3. 前两项都没找到可信的，再做这一项：**MODEL/FIELD_TEX/TEXTURES/** 下的 BIN 只读内部文件目录（不转贴图），把内部文件名含上面关键词的列进 calling_card\field_candidates.csv；有候选就只解那几张贴图。这一步超过 40 分钟还没做完就停下来，把做到哪写进报告。
4. 找到的预告信贴图：**原尺寸、原质量 RGBA PNG（不量化不压色）**放 <repo>\research\p5r\calling_card\found\，文件名写清来源（例如 IT0123_000__yokoku_a.png）。同一封信有不同目标（鸭志田、斑目、金城……）的都要；同一张图有正面 / 背面 / 正文的都要。
5. 写 calling_card\README.md（中文）：每张找到的图的来源包内路径、尺寸、你认为它是什么（哪一封信、正面还是正文）；没找到的话写清楚找过哪些、用的什么关键词和方法。

体积：单个文件不超过 20MB；本次新增到仓库的合计超过 200MB 就停下来报告。

## 3. 提交推送
- 只 add research/p5r/calling_card；提交信息 "research: P5R calling card textures (item models / gallery search)"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin <你的提交>:claude/confident-hypatia-nropgi
  失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。

## 4. 报告
中文：找到了什么（文件名、尺寸、是哪封信）；解了多少文件、本机占多大、仓库新增多大；没找到的原因；提交号。

【绝对不要做】
- 不要修改或删除仓库里已有的代码和资源，只新增 research/p5r/calling_card 下的文件。
- 不要 rebase / reset / amend / stash；不要 --force；不要改全局 git config、系统环境变量 / PATH。
- 只用 C:\Users\kkk\sts2_tools\p5r_tools\ 里已有的工具；真缺工具时，只允许从 GitHub 官方 release 页下载开源工具的便携版放进 p5r_tools\（不运行安装程序、不用管理员权限），报告里写名字、版本、地址。除此之外不要安装任何东西。
- 不要启动游戏（杀戮尖塔 2、P5R 都不要），不要用 Computer Use，不做实机验收，不生图。
- 游戏文件（CPK）只读，不要往游戏目录写任何东西。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，不要读取或输出任何密码；解密密钥 / 配置不要写进仓库、README 或报告。
- 不要进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 每条命令单独执行；不要 Remove-Item -Recurse / -Force；不要删除任何目录或文件。
- 遇到上面没写到的情况、或任何命令出错，停下来写报告。
