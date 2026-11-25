你在用户的 Windows 电脑上工作。任务：给 P5Joker 生两张**角色图标**，生成完直接提交推送。只生图，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
现在两处还是猎人（原版角色）的图或者凑合用的图：
- 选人界面下方那排角色按钮里，Joker 的按钮图是从立绘直接裁的，不好看；
- 游戏里左上角顶栏的角色头像还是原版猎人的。
画风照 art/p5/poses/ 里 Joker 的立绘（同一个人：黑色卷发、白色多米诺面具、黑色立领长外套、灰色马甲、红手套），线条干净、上色是平涂加一层阴影的动画风。只取「女神异闻录 5 怪盗」这个角色形象，不要照搬游戏官方的具体某张图。
**不要**：网点（halftone 圆点）、文字、水印、多个人物、写实 / 3D 渲染质感。

## 规矩
- 只在 art/p5/ui/icons/ 下新建文件，不改仓库里其他文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西；不碰游戏目录。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立暂存区 / worktree 只提交 art/p5/ui/icons。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
基于 origin/claude/confirm-next-tasks-k5wu6t 提交。

## 2. 要画的（放到 art/p5/ui/icons/）

1. **选人按钮 joker_select_button.png**（660×975，竖向，**带背景、不透明**，游戏里会缩到 132×195 显示，所以构图要大、剪影要一眼认得出）：
   Joker 的胸像到半身，身体朝右前方、头转过来看向画面，嘴角带一点笑；右手抬到脸边正在拉左手的红手套（手套是画面里最亮的红），外套立领立着、下摆被风吹向画面左侧。
   背景：深黑红底，一道从左下到右上的斜切亮红色块衬在人物身后（P5 菜单那种利落的斜切），边上一道细黑边；人物头部在画面上 1/3 处，四边都留出背景，不要切到头顶。
2. **左上角头像 joker_top_icon.png**（1024×1024，**真透明底 RGBA**，头像以外 alpha = 0，内容离画布边至少留 6%）：
   Joker 的头像（头 + 一点点立领），正面略偏 3/4 角度，戴着白色多米诺面具、眼神锐利。游戏里只有 80 像素左右大，所以：大块的形状、清楚的黑色外轮廓线、头发的剪影要有辨识度，不要细碎的发丝和小细节。

## 3. 提交推送
生成完直接提交推送（不用自己检查、不用重画、不用写报告、不用拼检查图）：
- 只 add art/p5/ui/icons；提交信息 "art: P5 Joker character select button + top bar icon"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin <你的提交>:claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。
