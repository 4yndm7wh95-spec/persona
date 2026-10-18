你在用户的 Windows 电脑上工作。任务：用 imagegen **复刻** P3R 主菜单（Camp 菜单）里那张「倒挂入水的结城理」立绘，给 P3Makoto mod 重做的设置界面用。游戏里会把它做成会动的立绘（头发 / 飘带摆动、眨眼），并用着色器让人物的白色部分和水下背景融在一起。只做美术，不改代码。

【背景】
- P3Makoto 是杀戮尖塔 2 的 mod，把储君换成 P3R 的结城理。设置界面要按 P3R 主菜单（SKILL / ITEM / EQUIP / PERSONA … SYSTEM 那一页）重做。
- 这次是**复刻参考图**，不是自由发挥。之前 art\p3menu\menu_a.png / menu_b.png 也是倒挂姿势，但画风（全彩黑校服、深蓝底、完整全身）和参考图差别很大，**不要以它们为参考，也不要改动它们**。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/ 是之前保留的素材，原样保留、忽略它继续（不要移动、删除或提交它）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。
6. 稀疏检出缺目录时，允许 git -c safe.directory=<repo> sparse-checkout add art/p3menu_system art/p3pile ref_sheets（路径过长时这一条命令可以加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 参考图（都在 <repo>\art\p3menu_system\ref\）】
- menu_frame_full.jpg：P3R 主菜单整屏截图（**主要参考，照这张复刻人物**）。
- menu_frame_blend.jpg：同一画面，红框标出"人物和水融为一体"的区域（见第 3 节）。
- menu_frames_52s.jpg：52.17~53.33 秒连续 8 帧（可以看到头发 / 飘带在轻轻晃动）。
- menu_splash_51s.jpg：进入菜单时水花转场的两帧（只是了解上下文，不用画）。
- 参考图只用来复刻人物的姿势、构图、配色和画风；**不要画进任何 UI、文字（左边竖排的大字、SKILL 等菜单字、金额框）、水下背景**。

【3. 要复刻的要点（逐条对照 menu_frame_full.jpg）】
构图（画布 1920×1080，对应游戏整屏；人物位置要和截图里一致）：
- 倒挂（头朝下）入水的超大特写：**脸倒过来在画面左下方**（约 x 150~650、y 600~1080，下巴朝上、头顶朝下），脸下半部和头顶可以被画布下边裁掉；
- 身体（白衬衫、肩、胳膊）从脸往**上**延伸，被画布**上边**裁掉（看不到腿）；
- 人物整体占画面左边约 45%（x 0~约 870）；画布右边 55% 只有往右飘进水里的头发发梢和少量衣服，其余透明；
- 一只手在脸旁边（手指松开、靠近头发）；画面上方偏中间有 3~4 条**黑色细长飘带**（领结和耳机线）向上飘、弯成 S 形。
画风和配色（这是和之前那张最大的区别）：
- 整体像被水下的光冲淡了：**大面积纯白**（衬衫、皮肤亮部），阴影只用很少的**浅青 / 天青 / 钴蓝**平涂色块；
- 头发：钴蓝到天青，带浅青高光块，往画面右侧（水里）飘散，发梢越往右越浅、越接近水里光斑的颜色；
- 眼睛：露出的那只眼睛是**青色**、半睁、平静（倒过来看）；
- 线条：细的深蓝色线（不是粗黑墨线）；只有那几条飘带和少量发丝是黑色；
- 只有很小的一点粉红 / 红色点缀（耳机或嘴唇），不要大块红色；**几乎看不到黑色外套**（参考图里基本只有白衬衫）。
颜色要求（游戏里的着色器靠颜色区分，务必遵守）：
- 用平涂、有限的颜色：纯白 #FFFFFF、浅青（约 #BFF2FF）、天青（约 #4FC8FF）、钴蓝（约 #1E5CFF）、深海蓝线条（约 #0B1E66）、黑色飘带、极少的粉红；
- **白色必须是纯白 #FFFFFF**，不要偏灰、偏黄的白，不要白色渐变；
- 不要写实光影、不要发光、不要模糊、不要纸张纹理。
背景：
- 真透明（alpha = 0）。白底面板和水下背景由游戏里画，不要画进去。边缘干净，没有白边、黑边、光晕。

【4. 要交付的文件（输出目录 <repo>\art\p3menu_system\）】
1. 3 个候选：system_cand_a.png、system_cand_b.png、system_cand_c.png（1920×1080，RGBA 透明背景）。
2. 挑一张最像 menu_frame_full.jpg 的，复制为 system_base.png。挑选标准：构图位置、倒挂角度、配色（大面积纯白 + 少量青蓝）、黑色飘带、手没有畸形、透明边缘干净。
3. 眨眼帧：用 imagegen 的**编辑**功能，只改露出的那只眼睛：
   - system_eyes_half.png：半睁（比 base 更闭一点，眼睑盖住上半个瞳孔）；
   - system_eyes_closed.png：自然闭眼（倒过来的脸，闭眼的弧线方向要对）。
   编辑如果改动了眼睛以外的地方：用 system_base.png 把眼睛椭圆以外的像素还原（椭圆边缘羽化约 8 像素），眼睛以外变化的像素数要是 0。
4. 合成预览：system_on_ref.png —— 把 system_base.png 叠在 menu_frame_full.jpg 放大到 1920×1080 的画面上，半透明 50%，用来对比位置是否对齐；
   system_on_white.png —— base 叠在纯白底上（左半边）+ 深蓝底（右半边）各一张拼在一起，看白色部分和透明边缘是否干净。
5. meta.json（像素坐标，原点左上角，1920×1080）：露出的眼睛中心、眨眼椭圆中心和半径、脸的中心、下巴、头顶方向的点、每条黑色飘带的根部和末端、主要发梢 4~6 处、手的中心、人物整体包围框；
   另外标出"人物和水的分界"：沿人物右侧外轮廓（衬衫 / 胳膊 / 头发的外缘，从画布上边到下边）取 8~12 个点的折线 —— 游戏里在这条线左边保留白色、右边让白色透出水。
6. preview.png：第一行三张候选（深蓝底上）；第二行 base / 半睁 / 闭眼；第三行眼部 3 倍放大对比；第四行 system_on_ref 缩小图。
7. README.md（中文）：用的提示词、每张的生成和编辑次数、选了哪张和理由、和参考图还有哪些差别、是否做了还原、已知问题。

【5. 自检】不满足就重做，最多 4 轮；仍不满足就如实报告：
- 背景是真透明（alpha=0），不是白底、棋盘格或深蓝底；
- 白色是纯白 #FFFFFF（用程序统计：人物里接近白色的像素都应该是 255,255,255）；
- 构图和 menu_frame_full.jpg 对齐：脸在左下、倒挂、身体从上边出血、人物在左边约 45%；
- 配色是大面积纯白 + 少量青蓝，**不是**黑校服为主；
- 手指数量正常；闭眼版是真闭眼，且眼睛以外没有变化。

【6. 提交与推送】
1. 只 add 这个目录：git -c safe.directory=<repo> add art/p3menu_system
2. 提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R camp-menu style upside-down portrait for the settings screen (+ blink frames)"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：做了什么、选了哪张、和参考图的差别、结果在哪、提交号、遇到的问题。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下来报告。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
