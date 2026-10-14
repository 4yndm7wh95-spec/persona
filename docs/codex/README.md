# Codex 任务 prompt

这里是交给用户本机 Codex 的任务 prompt（每个文件一个会话）。写新 prompt 时遵守这些长期规则：

- **生图一律真透明底**（用户 2026-10-14）：RGBA，轮廓外 alpha = 0，不用绿幕 / 白底再抠图；自检里写明检查轮廓外 alpha、边缘无底色残留。
  全屏背景图除外，但叠在人物前面的前景层也要真透明。
- 手势、姿势照 P3R 原版截图严格描述（参考图放 art/parts_src/ref/ 等目录并在 prompt 里列出），写清自检条目。
- 安全清单原样保留：不改代码 / 已有资源、不改写历史、不 --force、不启动游戏、不读 obs.json 和密码、游戏文件只读、不绕过安全拒绝、意外情况停下报告。
- git 命令都带 -c safe.directory=<repo>；fetch / push 带 HTTP/1.1 参数并按 5/10/20/40 秒重试；被拒绝先 fetch 再 merge --no-edit（不 rebase）。
