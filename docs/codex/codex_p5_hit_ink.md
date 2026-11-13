你在用户的 Windows 电脑上工作。任务：用 imagegen 生成 9 张 Persona 5 风格的特效素材图（打击 / 枪击的笔触墨迹 + 防御的玻璃屏障），清理、保存、提交推送。不改代码、不编译、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，副本目录 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不要删除任何已有文件。
- 不改全局 git config、系统环境变量 / PATH；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 每张图只生成一次（生成失败、空图、背景不透明、方向不对才重试一次）；被安全系统拒绝：不重试，停下来报告。不要自己挑毛病重画。
- 尺寸不要求精确：imagegen 给出多大就保存多大，横 / 竖和大致比例对就行。不要缩放、裁剪或用脚本处理图片；本机没有 python 也不需要它。
- 遇到没写到的情况：停下来报告。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx/gen_codex docs/codex/reports
新建目录 repo\art\p5\fx\gen_codex\ink\（已存在就直接用）。
上一个会话已经生成过第一张斩击（C:\Users\kkk\ChatGPT-Accounts\Account2\codex\generated_images\01a196d0-1cab-7cf0-afac-c0099f2f3e0c\exec-5672aee7-e32c-43f3-a004-ffdc017332d7.png，2172×724）：
打开看一眼，背景透明、笔触水平，就把它复制成 ink\ink_slash_1.png，不用重新生成；否则按下面重新生成这一张。

【2. 生成 9 张图】
共同要求（每张都写进提示词）：
- 风格：Persona 5 官方插画 / UI 的那种锐利、时髦、高反差的墨笔质感：干脆的毛笔 / 刮刀笔触，边缘锐利、带飞白和细小墨点飞溅。
  颜色只用：正红（#e5191c）、近黑（#0b0205）、白（#ffffff）。不要儿童漫画 / 美式卡通的胖爆炸星、不要粗黑描边的圆润造型、不要 Q 版、不要文字、不要发光柔光、不要渐变、不要 3D 渲染。
- 真透明背景：PNG RGBA，图形以外 alpha = 0；不要白底、灰底、绿幕、阴影、地面或任何别的东西。
- 一张图只有一个图形，居中，四周留出约 5% 透明边。

各图（文件名 → 画布 → 内容）：
- ink_slash_1.png、ink_slash_2.png、ink_slash_3.png、ink_slash_4.png → 横幅，宽约为高的 3～4 倍：
  一道从左往右、一笔挥出的斩击笔触，横贯整张画布宽度（左端起笔、右端收笔成尖），中段最粗约占画布高度 30%。
  红色笔触为主，内侧夹一道细白芯，外沿一点黑色；收笔那头飞出几滴墨点和细丝。四张的弯曲度和粗细各不相同
  （1 直而锐；2 微微上弧；3 较粗、带两股分叉；4 细长、带断续飞白）。笔触整体必须是水平方向，不要斜放。
- ink_splat_1.png、ink_splat_2.png、ink_splat_3.png → 正方形：
  被击中时的一团墨迹飞溅：中心一块不规则的红墨，朝画面右侧（+x 方向）甩出放射状的尖刺、墨点和细丝，左侧短、右侧长；
  局部压一点黑墨和白色细痕。三张形状各不相同。
- guard_barrier.png → 竖幅（高约为宽的 1.5 倍）：
  一面竖着的「玻璃屏障」，Persona 5 风格：半透明的深红到黑色厚玻璃板，外形是锐利的不规则多边形（像一块斜切的碎玻璃大板，
  不是盾牌、不是徽章、不要任何图案或标志），边缘有锐利的白色高光线和红色亮边，表面有少量斜向的白色反光条。
  玻璃板本身要半透明（玻璃区域 alpha 约 150～210），边缘高光不透明。占画布约 85% 高。
- guard_cracks.png → 竖幅，和 guard_barrier 同样大小：
  和上一张同样外形范围内的「裂纹」叠加层：只有白色的锐利裂纹线（从中间偏上一点放射出去的蛛网状裂纹 + 几道长裂缝），
  线条细而锐，其他地方全透明。不要画玻璃板本身。

【3. 检查（只用眼睛看，不用脚本处理图片）】
每张图打开看一眼，把下面几项写进报告：实际尺寸、背景是否透明（不是白底 / 灰底 / 棋盘格画进图里）、是否只有一个图形、
颜色是否只有红黑白、斩击笔触是否水平。杂点清理、缩放、预览拼图都不用做（云端处理）。

【4. 提交推送】
在 repo\art\p5\fx\gen_codex\ink\ 里写 README.md（中文）：每张图的完整提示词、实际尺寸、第 3 步的检查结果。
然后依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/ink
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: P5 ink slashes, splatters and guard glass barrier"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
