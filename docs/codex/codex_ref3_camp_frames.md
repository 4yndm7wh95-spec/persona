你在用户的 Windows 电脑上工作。任务：把用户之前给的 P3R 演示视频（ref3）里营地菜单那一段，按原帧率导出成单帧图放进仓库，给 Claude 对照动画用。不改代码，不生图，不启动游戏。

【背景】
仓库 ref_sheets/ref3_overview_0.jpg ~ ref3_overview_10.jpg 是这段视频每 1/6 秒一格的总览图（每格左上角黄字是视频时间）。
营地菜单在 51.50s ~ 62.00s：主菜单入水 → SKILL → ITEM → EQUIP → STATUS → QUEST → COMMU。现在只有 6fps 的小图，看不清切页动画。

【1. 找到仓库并拉取】
同 docs/codex/codex_p3r_camp_pages_rip.md 第 1 节（找 <repo>、safe.directory、status 检查、fetch、merge --ff-only / merge --no-edit、冲突就停）。
稀疏检出缺目录时，允许 git -c safe.directory=<repo> sparse-checkout add ref_sheets research/p3r_video（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 找到原视频】
1. 在本机找做出 ref3_overview_*.jpg 的那个原视频：时长至少 62 秒，画面和 ref3_overview_9.jpg 的 54.00s、ref3_overview_10.jpg 的 61.83s 一致（营地菜单）。
   可以先看用户的下载 / 视频 / 桌面目录和 C:\Users\kkk\sts2_tools\ 下的工作目录；用 ffprobe 读时长、分辨率、帧率。
2. 找到后用 ffmpeg 截 54.00s、61.83s 两帧，和总览图对应格子比对，确认是同一个视频。找不到或对不上就停下来报告（写清楚找过哪些目录），不要用别的视频代替。

【3. 导出】
输出到 <repo>\research\p3r_video\ref3_camp\：
1. frames\f_<视频内时间毫秒，6 位补零>.jpg：51.50s ~ 62.00s 的**每一帧**（原帧率，不抽帧、不补帧），缩放到宽 1280（保持比例），JPEG 质量 90。
   例：ffmpeg -ss 51.5 -to 62.0 -i <视频> -vf "scale=1280:-2" -q:v 2 -start_number 0 <临时目录>\%05d.jpg，再按每帧实际时间戳改名（用 ffprobe -show_frames 取 pts_time，不要按帧号乘帧率推算）。
2. index.csv：每帧一行——文件名、pts_time（秒）、原帧号。
3. sheets\：每 2 秒一张总览图（6 列，每格宽 320，格子左上角写 pts_time 秒，两位小数），方便快速浏览。
4. README.md（中文）：原视频文件名、路径（只写到文件名，不写用户名以外的隐私）、时长、分辨率、帧率（ffprobe 的 r_frame_rate 和 avg_frame_rate 原样写）、是否可变帧率、导出帧数。
单个文件不超过 20MB；frames 总体积超过 120MB 就把 JPEG 质量降到 85 重导。

【4. 提交与推送】
1. 只 add：git -c safe.directory=<repo> add research/p3r_video/ref3_camp
2. git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: ref3 camp menu segment frames (51.5-62.0s, native frame rate)"
3. push 同 codex_p3r_camp_pages_rip.md 第 5 节（失败重试；被拒先 fetch + merge --no-edit；不要 rebase / --force）。
4. 中文简短报告：找到的视频、帧率、导出帧数、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件；不要把原视频本身提交进仓库。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动），也不要用 Computer Use。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
