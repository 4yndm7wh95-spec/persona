你在用户的 Windows 电脑上工作。任务分两部分：A 把用户给的网易云音乐 .ncm 歌曲和 .lrc 歌词转换成音乐盒 mod 能播放的格式；B 编译安装音乐盒 mod「P3MusicBox」。只处理用户自己的文件，不改代码、不碰游戏原文件。

【用户给的文件夹】
歌曲和歌词在：<把这里换成你的文件夹路径>（下文记作 <SRC>）。里面是 .ncm 和 .lrc 文件。只读，不要修改、移动、删除 <SRC> 里的任何文件。

【格式选择（已经定好，照做）】
- 音乐盒用的是游戏引擎 Godot 自带的解码器，支持 Ogg Vorbis 和 MP3（不支持 Opus / AAC / FLAC）。
- .ncm 解出来如果是 **MP3**：**保持 MP3 原样不重新编码**（MP3 本身已经是有损格式，再转一次会二次损失音质），只是改名、去掉内嵌封面图（封面会让文件变大，音乐盒用不到）。
- 解出来如果是 **FLAC**（无损）：转成 **Ogg Vorbis，-q:a 6**（约 190kbps 可变码率，绝大多数人听不出和无损的区别，体积约为 FLAC 的 1/5），保留原采样率（高于 48kHz 的降到 48kHz）、立体声，去掉封面图，保留标题 / 歌手标签。
  命令示例：ffmpeg -i in.flac -map 0:a -c:a libvorbis -q:a 6 -ar 48000（原本 ≤48kHz 就去掉 -ar）-map_metadata 0 out.ogg
- 歌词 .lrc：转成 UTF-8（无 BOM）、换行统一为 \n，内容不改。

【A：转换】
1. 工作目录：C:\Users\kkk\sts2_tools\musicbox_work\（新建；中间文件都放这里）。输出目录：C:\Users\kkk\sts2_tools\musicbox_music\（新建）。
2. 解 .ncm 用开源工具 ncmdump：
   - 先看本机有没有 ncmdump 命令；没有的话允许执行 python -m pip install --user ncmdump（只装这一个包）。装不上就停下来报告，不要去别处下载可执行文件。
   - 把 <SRC> 里的 .ncm **复制**到工作目录，在工作目录里解（不要在 <SRC> 里解）。解出来的是 .mp3 或 .flac。
   - 有解不开的文件：跳过并记下文件名，最后报告。
3. 需要 ffmpeg / ffprobe（用户电脑上之前用过）。没有就停下来报告。
4. 对每首歌：
   - 用 ffprobe 读标题、歌手标签；没有标签就从文件名猜（网易云的文件名一般是「歌手 - 歌名」）。
   - 按上面的格式规则输出到 musicbox_music\，文件名用「两位序号_歌名」（去掉 Windows 不允许的字符，序号按 <SRC> 里文件名排序）。
   - 找对应的 .lrc：先找同名（不含扩展名）的；没有就找文件名里包含同样「歌名」的；还找不到就不配歌词，记下来报告。配上的转成 UTF-8 存成和歌曲同名的 .lrc。
5. 生成 musicbox_music\playlist.json（UTF-8），按序号排：
   [{"file": "01_歌名.mp3", "lrc": "01_歌名.lrc", "title": "歌名", "artist": "歌手"}, …]（没歌词的那首 "lrc": null）
6. 自检：每个输出文件用 ffprobe 能读出时长；.ogg 的编码是 vorbis；.mp3 的码率和解出来的原文件一致（说明没重新编码）。统计：原 .ncm 总大小、输出总大小。

【B：编译安装音乐盒 mod】
7. 在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（用你上次成功编译的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
   - git -c safe.directory=<repo> status -sb：有进行中的合并或未提交改动就停下来报告。
   - fetch：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次）。
   - 合并：git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进就 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn。有冲突就停下来报告。这次不需要 push。
   - 稀疏检出缺 musicbox 目录时允许 git -c safe.directory=<repo> sparse-checkout add musicbox（不要 sparse-checkout disable / set）。
8. 先确认没有杀戮尖塔 2 在运行（有就停下来报告，不要强行结束）。
9. 在 <repo>\musicbox 里 dotnet build -c Release -p:GameDir="<游戏安装目录>"（游戏目录用编译 P3Makoto 时用的那个）。
10. 安装到 <游戏安装目录>\mods\P3MusicBox\：P3MusicBox.dll（bin\Release\net9.0\）、P3MusicBox.json（<repo>\musicbox\），再把 musicbox_music\ 里的全部文件复制到 <游戏安装目录>\mods\P3MusicBox\music\。
11. 提醒用户（写在报告里）：第一次装新 mod 后，要在游戏「模组」设置里确认 P3MusicBox 是启用状态（之前 P3TestMod 就因为被设成禁用没加载）。

【报告】中文：转换了几首、跳过 / 解不开的有哪些、没配上歌词的有哪些、每首的格式（MP3 原样 / Vorbis）和大小、前后总大小对比；mod 的提交号、编译结果、安装位置。

【绝对不要做】
- 不要修改、移动、删除 <SRC> 里的文件；不要把歌曲或歌词 git add / commit 到仓库（有版权，也太大）。
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 除了 pip install --user ncmdump 以外不要安装别的东西；不要从网上下载可执行文件。
- 不要启动游戏。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只往 mods\P3MusicBox\ 里放文件）。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
