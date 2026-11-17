# 任务：把《杀戮尖塔 2》的 FMOD 音效库提交到仓库（给云端预览工具解出原版音效用）

## 背景
mod 里的技能牌、群蛇形态等音效已经改成播放游戏自带的 FMOD 事件（`event:/sfx/...`），云端的预览工具没有这些音频，视频里不出声。
云端会话要从音效库里解出样本，接进预览工具的混音（只在预览里用，**不打进 mod**）。

## 要做的
1. 找到本机的游戏目录（之前的记录是 `C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\`），里面有 `SlayTheSpire2.pck`。
2. 从 PCK 里取出 `res://banks/desktop/` 下面这 4 个文件（原样，不要转换）：
   - `sfx.bank`（约 42 MB）
   - `temp_sfx.bank`（约 1.8 MB）
   - `Master.bank`（约 2 MB）
   - `Master.strings.bank`（约 30 KB）

   取法任选其一：
   - 如果本机以前用 gdre 解包过整个游戏，解包目录里的 `banks\desktop\` 已经有这些文件，直接复制；
   - 否则用 gdre_tools 只解这一个目录，例如：
     `gdre_tools --headless --extract="<游戏目录>\SlayTheSpire2.pck" --output-dir="<临时目录>" --include="res://banks/desktop/*"`
     （如果你的 gdre 版本不支持 `--include`，就整个解包，再只拿这 4 个文件）。
3. 核对大小和上面的大致一致。`Master.strings.bank` 必须有，没有它就对不上事件名。
4. 放进仓库的 `research/sts2_audio/banks/`（目录不存在就新建），文件名保持原样。
5. 在仓库里从远端分支 `claude/blissful-clarke-rgwshm` 拉一个新分支 `codex/sts2-audio-banks`，只提交这 4 个文件，推送这个新分支：
   ```
   git fetch origin claude/blissful-clarke-rgwshm
   git checkout -b codex/sts2-audio-banks origin/claude/blissful-clarke-rgwshm
   mkdir research\sts2_audio\banks   (Windows) / mkdir -p research/sts2_audio/banks
   （复制 4 个文件进去）
   git add research/sts2_audio/banks
   git commit -m "research: Slay the Spire 2 FMOD banks (sfx, temp_sfx, Master, Master.strings) for the cloud preview's sound mix"
   git push -u origin codex/sts2-audio-banks
   ```
6. 不要改仓库里的任何其他文件，不要动 `claude/` 开头的分支。

## 注意
- 单个文件都小于 GitHub 的 100 MB 上限，不需要 Git LFS。
- 这个仓库是**公开的**：推上去的音效库任何人都能下载。用户已经知道并决定这样做。
- 推送如果因为网络中断失败，重试几次即可（文件较大）。

## 完成后回报
新分支名、提交号、4 个文件各自的大小。
