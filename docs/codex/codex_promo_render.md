你在用户的 Windows 电脑上工作。任务：用仓库里的脚本渲染 P3R 风格宣传视频模板（真透明底 ProRes 4444 .mov），放到用户桌面的文件夹里（不改代码、不碰游戏）。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（用你上次成功编译的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
1. git -c safe.directory=<repo> status -sb：确认没有进行中的合并，也没有未提交的改动。有的话停下来报告。
2. fetch：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次）。
3. 合并：git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进就改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn。有冲突就停下来报告。这次不需要 push。
4. 确认这些文件在磁盘上（稀疏检出缺目录时允许 git -c safe.directory=<repo> sparse-checkout add <目录>，路径过长时单次命令加 -c core.longpaths=true；不要 sparse-checkout disable / set）：
   tools\promo\p3r_templates.py、tools\vfx\allout_ripple.py、mod\pck_stage\p3makoto\sfx\、mod\pck_stage\p3makoto\fonts\BarlowCondensed-900Italic.ttf、mod\pck_stage\p3makoto\ui\critical.png、art\v16\butterfly\butterfly_wing_l.png / butterfly_wing_r.png / butterfly_body.png

【环境】
5. 需要 Python 3（numpy、Pillow）和 ffmpeg（要有 prores_ks、libx264 编码器：ffmpeg -hide_banner -encoders 里能找到）。
   - numpy / Pillow 缺的话允许 python -m pip install --user numpy pillow。
   - 没有 ffmpeg 或缺这两个编码器：停下来报告，不要自己去下载安装别的东西。

【渲染】
6. 输出目录：C:\Users\kkk\Desktop\P3R_templates\（不存在就新建）。
7. 在 <repo> 目录下运行（会比较久，十几分钟到半小时，CPU 占满是正常的）：
   python tools\promo\p3r_templates.py C:\Users\kkk\Desktop\P3R_templates
   脚本会依次渲染 8 个模板，每个打印一行「名字 帧数 mov 大小 preview 大小」。
8. 自检：
   - 8 个 .mov、8 个 .wav、8 个 _preview.mp4 都在。
   - 对每个 .mov 跑 ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,pix_fmt,width,height,r_frame_rate -of csv <文件>：应为 prores、yuva444p10le 或 yuva444p12le（带 a 说明有透明通道）、1920、1080、60/1。
9. 把 <repo>\docs\promo\README.md 复制一份到输出目录，改名为「使用说明.md」。

【报告】中文：渲染是否成功、每个文件大小、ffprobe 结果、用时；有报错就原样贴出报错（敏感信息用 *** 代替）。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；不要 git add / commit 渲染出来的视频。
- 不要启动游戏。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
