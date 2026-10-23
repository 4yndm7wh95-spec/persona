# Codex 任务 prompt

这里是交给用户本机 Codex 的任务 prompt（每个文件一个会话）。写新 prompt 时遵守这些长期规则：

- **生图一律真透明底**（用户 2026-10-14）：RGBA，轮廓外 alpha = 0，不用绿幕 / 白底再抠图；自检里写明检查轮廓外 alpha、边缘无底色残留。
  全屏背景图除外，但叠在人物前面的前景层也要真透明。
- 手势、姿势照 P3R 原版截图严格描述（参考图放 art/parts_src/ref/ 等目录并在 prompt 里列出），写清自检条目。
- 安全清单原样保留：不改代码 / 已有资源、不改写历史、不 --force、不启动游戏、不读 obs.json 和密码、游戏文件只读、不绕过安全拒绝、意外情况停下报告。
- **编译 / 安装类任务不做实机验收**（用户 2026-10-19）：Codex 不启动游戏、不用 Computer Use，用户自己验收；build 文档里不写实机复验步骤。
- **发布时自动递增版本号**（用户 2026-10-23）：每次发布修改包由 Codex 自动递增补丁版本号，同步运行常量、模组清单、ZIP 文件名和版本记录，不再询问。用户明确指定版本号时以用户指定为准。
- 允许忽略的未跟踪目录（原样保留，不移动 / 删除 / 提交）：art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/、mod_build/、art/p3menu_system/ref/menu_transition_frames_020-107/frames/，以及别的会话正在生成、还没提交的目录（例如 research/p3r_camp/）。
- git 命令都带 -c safe.directory=<repo>；fetch / push 带 HTTP/1.1 参数并按 5/10/20/40 秒重试；被拒绝先 fetch 再 merge --no-edit（不 rebase）。


## 和 Claude 同时改代码（v0.14.98 起）

用户让 Codex 和 Claude 都改 mod 代码，分支都是 claude/amazing-darwin-b00arn。为了不互相覆盖：
1. **改代码的任务**：开始前先 fetch + merge 远端；改完立刻提交并 push（被拒就 fetch + merge --no-edit 再 push），提交信息写清楚改了什么、为什么。不要攒着不推。
2. **编译安装的任务**：本地有你在别的任务里改了还没提交的代码 → 先提交；然后 fetch + merge 远端 → 编译 → 编译和兼容检查都通过后，如果本地多了提交就 push。报告里列出编进去的你自己的提交。
3. **冲突**：同一处两边都改了、拿不准保留哪边 → 停下来报告（文件、两边的改动），交给 Claude 合并；不要自己挑一边，不要 rebase / --force。
4. Claude 每次开始写代码前也会先 fetch + merge，并审读你的提交；你的改动会写进 docs/ART_SPEC.md 的版本记录。
