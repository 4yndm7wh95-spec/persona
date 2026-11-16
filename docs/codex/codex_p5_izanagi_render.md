你在用户的 Windows 电脑上工作。任务：用本机已有的伊邪那岐大神（PS0360）模型和动作，按亚森那一套流程渲染两组素材，提交推送。不改代码、不生图（不用 imagegen）、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，仓库副本 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不删除任何已有文件或目录。
- 不改全局 git config、系统环境变量 / PATH；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不修改游戏原文件，不启动游戏。
- 原模型、GLB、原始高分辨率渲染只留在本机（C:/Users/kkk/sts2_tools/p5r_rip/fx_work/izanagi_okami/），不要提交进仓库。
- 遇到没写到的情况、任何一步失败：停下来报告，不要猜。

【参考】
仓库里 research/p5r/persona_izanagi_okami/REPORT.md 和 animation_catalog.json 记录了上一轮怎么导出的（GFDExport → tools/p5r/fix_gfd_glb.py → 按骨骼名挂动作 → 保留原始旋转、跳过 root 旋转 → Blender）。
渲染风格、相机、描边、裁切、锚点要和亚森的成品完全一致：参考 art/p5/fx/arsene/cast/anchor.json 与 art/p5/fx/arsene/sources.json
（Blender 4.5.9 LTS、EEVEE、正交相机、相机方向 (-0.8,-1,0)、3/4 朝画面右侧；原贴图乘 Diffuse → ShaderToRGB → 两阶恒定明暗；
最终尺寸用 alpha 外扩 3px 合成黑轮廓；轮廓外 alpha=0；同一动作按全帧 alpha 并集固定裁边）。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx research/p5r/persona_izanagi_okami docs/codex/reports

【2. 渲染「转杖」动作：BPS0360_000.GAP#main[6]】
- 动作：BPS0360_000_main_0006（1.667 秒，51 帧 @30fps）：长杖竖到胸前、横着转一圈、再竖持。全部 51 帧都渲。
- 输出：art/p5/fx/izanagi_okami/spin/0000.png … 0050.png，透明 PNG，高 1024，同一动作固定画布。
- 同目录写 anchor.json，字段和 art/p5/fx/arsene/cast/anchor.json 一样（canvas、fps、frame_count、source_action、anchors.foot、anchors.body_center、crop、outline_px）。
- 另存 art/p5/fx/izanagi_okami/spin_contact.png：每隔 3 帧取一帧排成联系表（浅灰底、写帧号）。

【3. 渲染「武器单独一张」】
- 只要他手里那把环形双端长兵器（长杖 + 金色圆环），身体、披风、头全部隐藏（在 Blender 里按网格 / 材质把非武器部分隐藏；分不出哪些网格是武器 → 停下来报告，并把网格名单写进报告）。
- 姿势：用 main[6] 第 41 帧（竖持那一段）里武器的姿势；相机改成正对武器（让长杖在画面里竖直、圆环正对镜头、尽量不透视变形），正交。
- 同样的两阶明暗 + 3px 黑轮廓，透明背景，长边 1536。
- 输出：art/p5/fx/izanagi_okami/weapon.png；另写 art/p5/fx/izanagi_okami/weapon.json：画布尺寸、圆环中心的像素坐标、长杖两端端点的像素坐标。

【4. 检查】
每组都检查并写进报告：帧数、画布尺寸、四角与轮廓外 alpha 是否为 0、有没有截断（身体 / 武器碰到画布边）、和亚森 cast 的风格是否一致（并排放一张对比图 art/p5/fx/izanagi_okami/compare_arsene.png）。

【5. 提交推送】
写 art/p5/fx/izanagi_okami/README.md（中文）：用了哪些源文件、每步的设置、第 4 步的检查结果。
依次单独运行：
   git -c safe.directory=* add art/p5/fx/izanagi_okami
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: Izanagi-no-Okami spin action (main[6]) and weapon render"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
