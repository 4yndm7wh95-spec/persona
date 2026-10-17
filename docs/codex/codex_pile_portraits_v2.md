你在用户的 Windows 电脑上工作。任务：用 imagegen 画三张 P3R 游戏内菜单风格的结城理立绘（牌组 / 弃牌堆 / 消耗堆界面各一张，姿势和构图都不同），并给会睁眼的两张做「半睁 / 闭眼」对齐眨眼帧。这些图给 P3Makoto mod 的三个牌堆界面用，游戏里会做成会动的立绘（呼吸、发梢、眨眼）。只做美术，不改代码。

【背景】
- P3Makoto 是杀戮尖塔 2 的 mod，把储君换成 P3R 的结城理。
- 抽牌堆界面已经做好：仿 P3R 的 COMMU 界面，立绘是 art/p3pile/pile_base.png（举塔罗牌）。
- 另外三个界面要各有不同的表现，分别仿 P3R 菜单里的 STATUS / ITEM / QUEST 子界面：
  - 牌组（DECK）：仿 STATUS（58.0s）：横躺的超大脸部特写，占满画面左半边；
  - 弃牌堆（DISCARD）：仿 ITEM（54.0s）：头朝下往水里沉；
  - 消耗堆（EXHAUST）：仿 QUEST（60.83s）：远处背对镜头、慢慢漂走的小小身影。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或未提交改动就停下来报告。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。
6. 稀疏检出缺下面用到的目录时，允许 git -c safe.directory=<repo> sparse-checkout add ref_sheets art/p3pile art/p3menu（路径过长时这一条命令可以加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 参考图】
- 人物设定和画风：以 <repo>\art\p3pile\pile_base.png 为准（同一个人、同一套配色和线条），三张新图放在一起要像同一套立绘。
- 姿势参考：<repo>\ref_sheets\ref3_overview_9.jpg 和 ref3_overview_10.jpg 是 1920×1344 的分镜表，每格 320×224，左上角黄字是时间。按格子实际边界裁出下面几格（去掉黄色时间字），各放大 3 倍，存到对应的 ref\ 子目录：
  - 牌组：ref3_overview_9 的 58.00s（第 5 行第 1 格）和 58.33s（第 5 行第 2 格）→ <repo>\art\p3pile_deck\ref\
  - 弃牌堆：ref3_overview_9 的 54.00s（第 1 行第 1 格）、54.17s（第 1 行第 2 格）→ <repo>\art\p3pile_discard\ref\
  - 消耗堆：ref3_overview_10 的 60.83s（第 1 行第 6 格）和 61.00s（第 2 行第 1 格）→ <repo>\art\p3pile_exhaust\ref\
- 参考图只用来定姿势和构图，不要照抄里面的 UI、文字和背景。

【3. 共同的画风要求（三张都照这个写 imagegen 提示词）】
- P3R 游戏内菜单立绘：平涂双色——纯白和很浅的青白做亮部，饱和的钴蓝 / 群青做暗部，深海军蓝做最暗处；黑色硬边墨线；没有渐变、没有写实光影、没有发光。
- 深蓝头发带青色高光块，头发遮住右眼；月光馆学园黑色校服外套、白衬衫、黑色细领结；脖子上红色耳机，左臂红色臂章，耳机线；只有这两处红色点缀。
- 背景真透明（alpha = 0），不要背景、文字、水印、签名、UI；人物边缘干净，没有白边、黑边、光晕。

【4. 三张图各自的要求】
A. 牌组 DECK → 输出目录 <repo>\art\p3pile_deck\
- 画布 1536×1536，RGBA 透明背景。
- 超大脸部特写，头向画面右侧横躺约 70°（脸的朝向和 58.00s 那格左右相反：他躺在画面左半边，脸朝画面右边看），平静、略带倦意地看向观众；露出的那只眼睛要大而清楚（给眨眼用）。
- 一只手（只露手和一截袖口）轻轻搭在额头的头发上，手指自然、五指正常。
- 脸、头发从画布左边、上边、下边出血（被画布边缘裁掉）；画布右边是头发和衬衫领口的外轮廓，轮廓外透明。
- 头发像在水里一样向画面右上方飘散，发梢分明（游戏里会让发梢摆动）。
B. 弃牌堆 DISCARD → 输出目录 <repo>\art\p3pile_discard\
- 画布 1024×1536，RGBA 透明背景。
- 整个人头朝下、往下沉的半身到大半身像：头在画面下方，身体向上（脚和腿被画布上边裁掉），整体向右倾斜约 15°，像 54.00s 那格。
- 平静地半睁眼，视线朝观众（倒着看过来）；露出的那只眼睛清楚（给眨眼用）。
- 头发、领结、耳机线都因为在水里往"上"（朝他的脚的方向，即画面上方）飘；两只手臂放松地向上方张开，手指自然、五指正常。
C. 消耗堆 EXHAUST → 输出目录 <repo>\art\p3pile_exhaust\
- 画布 1024×1536，RGBA 透明背景。
- 远处背对镜头（三分之二背影）的全身像，人物只占画布高度的约 65%，悬浮在水中慢慢漂走：身体微微蜷起，一只手向上方伸出，另一只手自然下垂，校服外套下摆和头发向上飘。
- 看不到眼睛（背影 / 侧后方），不需要眨眼帧。
- 全身完整，四周留透明空间，不要被画布裁掉。

【5. 要交付的文件】三个目录各自放：
1. 3 个候选：<name>_cand_a.png、<name>_cand_b.png、<name>_cand_c.png（name 分别是 deck / discard / exhaust）。
2. 挑一张最符合要求的，复制为 <name>_base.png。挑选标准：画风最像 art\p3pile\pile_base.png、人物设定一致、手没有畸形、透明边缘干净、构图符合上面的要求。
3. 牌组和弃牌堆两张：用 imagegen 的**编辑**功能，在 <name>_base.png 上只改露出的那只眼睛，做出：
   - <name>_eyes_closed.png：自然、平静地闭眼（上眼睑垂下成一条带睫毛的弧线，看不到瞳孔）；
   - <name>_eyes_half.png：半睁（眼睑盖住上半个瞳孔）。
   编辑如果改动了眼睛以外的地方：用 <name>_base.png 把眼睛椭圆区域以外的像素还原（椭圆边缘羽化约 8 像素），保证叠上去看不出接缝。眼睛以外变化的像素数要是 0。
   消耗堆不做眨眼帧。
4. meta.json（以图片左上角为原点的像素坐标）：
   - 牌组 / 弃牌堆：露出的眼睛中心、眨眼编辑用的椭圆中心和半径、头顶（或头部最外端）、下巴、主要发梢 3~5 处的坐标、手的大致中心；
   - 消耗堆：头、伸出的那只手、外套下摆左右两端、主要发梢 3~5 处的坐标；
   - 三张都加上人物整体的包围框（left, top, right, bottom）。
   游戏里给眨眼、发梢摆动、呼吸、轮廓光定位用。
5. preview.png（每个目录一张）：第一行三张候选（深蓝底上）；第二行 base / 半睁 / 闭眼（消耗堆只放 base）；第三行眼部区域 3 倍放大对比（消耗堆省略）。
6. README.md（中文）：用的提示词、每张的生成和编辑次数、选了哪张和理由、是否做了还原、已知问题。

【6. 自检】不满足就重做，最多 4 轮；仍不满足就如实报告：
- 背景必须是真透明（alpha=0），不能是白底、棋盘格或深蓝底。
- 闭眼版是真闭眼；半睁 / 闭眼叠到 base 上，眼睛以外没有可见变化。
- 手指数量正常。
- 和 art\p3pile\pile_base.png 放在一起看是同一个人、同一套画风。
- 牌组那张：头确实是横躺的大特写，左 / 上 / 下出血；弃牌堆那张：确实头朝下；消耗堆那张：确实是背影、全身完整、人物较小。

【7. 提交与推送】
1. 只 add 这三个目录：
   git -c safe.directory=<repo> add art/p3pile_deck art/p3pile_discard art/p3pile_exhaust
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R menu-style portraits for the deck / discard / exhaust screens (+ blink frames)"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：做了什么、每个目录选了哪张、结果在哪、提交号、遇到的问题。

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
