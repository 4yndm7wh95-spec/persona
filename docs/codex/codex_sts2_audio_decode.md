# 任务：把仓库里的《杀戮尖塔 2》音效库解码成音频文件（给云端预览的混音用）

## 背景
上一步已经把 `research/sts2_audio/banks/` 的 4 个 `.bank` 推上来了。里面是 FSB5 Vorbis 编码的样本，云端不能运行第三方解码器，所以请在本机解码后提交。
解出来的文件只给预览工具用（`tools/p5/fxpreview/game_sfx.py` 按文件名找），**不打进 mod**。

## 要做的
1. 下载 vgmstream 的 Windows 命令行版（https://github.com/vgmstream/vgmstream/releases ，`vgmstream-win64.zip`，解压得到 `vgmstream-cli.exe`）。
2. 拉分支：
   ```
   git fetch origin claude/blissful-clarke-rgwshm
   git checkout -B codex/sts2-audio-samples origin/claude/blissful-clarke-rgwshm
   ```
3. 对 `research/sts2_audio/banks/` 里的 `sfx.bank`、`temp_sfx.bank`、`Master.bank` 各执行一次（`Master.strings.bank` 不用），把**所有子音轨**按**音轨名**解成 wav：
   ```
   vgmstream-cli -S 0 -o "<临时目录>\?n.wav" research\sts2_audio\banks\sfx.bank
   ```
   （`-S 0` = 全部子音轨，`?n` = 音轨自己的名字，例如 `sts2_sfx_buff_layer1_rr1_v1.wav`）。三个库加起来大约 1938 个。
4. 用 ffmpeg 把这些 wav 转成 ogg（体积小很多），放进仓库 `research/sts2_audio/samples/`，**文件名和音轨名完全一致**、只是扩展名改成 `.ogg`：
   ```
   ffmpeg -loglevel error -y -i "<临时目录>\<名字>.wav" -c:a libvorbis -q:a 4 "research\sts2_audio\samples\<名字>.ogg"
   ```
   （批量用 PowerShell / bat 循环即可。）
5. 检查：`research/sts2_audio/samples/` 里应该有大约 1938 个 `.ogg`，比如 `sts2_sfx_buff_layer1_rr1_v1.ogg`、`sts2_sfx_act3_scroll_of_biting_bite_rr1_v1.ogg`、`STS2_SFX_Block_Break_Low_v1.ogg` 都在。总大小大约几十 MB。
6. 只提交 `research/sts2_audio/samples/`，推送新分支：
   ```
   git add research/sts2_audio/samples
   git commit -m "research: decoded Slay the Spire 2 SFX samples (ogg) for the cloud preview's sound mix"
   git push -u origin codex/sts2-audio-samples
   ```
7. 不要改其它文件，不要动 `claude/` 开头的分支。

## 注意
- 仓库是公开的，用户已经知道。
- 如果某个子音轨解码失败，记下名字继续，最后一起回报。

## 完成后回报
分支名、提交号、文件数、总大小、解码失败的音轨（如果有）。
