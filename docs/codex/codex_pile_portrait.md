你在用户的 Windows 电脑上工作。任务：用 imagegen 画一张 P3R 游戏内菜单风格的结城理立绘（手持一张塔罗牌），再做出「闭眼 / 半睁」两张对齐的眨眼帧。这些图给 P3Makoto mod 的「抽牌堆 / 弃牌堆」界面用，游戏里会让它眨眼、呼吸、发梢飘动。只做美术，不改代码。

【背景】
- P3Makoto 是杀戮尖塔 2 的 mod，把储君换成 P3R 的结城理。
- 牌堆界面要改成 P3R 游戏内菜单（COMMU / 社群界面）的样子：深蓝水下背景，立绘在画面右侧，卡牌沿斜线列在左侧。
- 参考的是 P3R 原版 COMMU 界面：结城理站着、手举一张塔罗牌，立绘被画面右边缘裁掉一部分。

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
6. 稀疏检出缺下面用到的目录时，允许 git -c safe.directory=<repo> sparse-checkout add ref_sheets art/p3menu（路径过长时这一条命令可以加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 准备参考图】输出目录 <repo>\art\p3pile\（新建）。先做参考图 ref\ 子目录：
1. ref\commu_frame.png：从 <repo>\ref_sheets\ref3_overview_10.jpg 裁出 61.83s 那一格（第 2 行第 6 格，约 x 1600~1920、y 224~448；以格子实际边界为准，去掉左上角黄色时间字），放大 3 倍保存。这是 P3R 原版 COMMU 界面：结城理手举塔罗牌。
2. ref\menu_frames.png：从 <repo>\ref_sheets\ref3_overview_9.jpg 裁出 55.0s、56.5s、57.67s 三格，从 ref3_overview_10.jpg 裁出 60.83s 一格，横向拼成一张。这是 P3R 菜单立绘的画风参考。
3. 角色外观参考直接用 <repo>\art\p3menu\commu_v2_a.png 和 <repo>\art\p3menu\flat\commu_v2_a.png（以前做的同一个姿势，人物设定要和它一致）。

【3. 画风和构图（照这个要求写 imagegen 提示词，可以附上面的参考图）】
- 画风：P3R 游戏内菜单立绘。
  - 平涂双色：纯白和很浅的青白做亮部，饱和的钴蓝 / 群青做暗部，深海军蓝做最暗处。
  - 黑色硬边墨线做轮廓和少量重阴影，没有渐变、没有写实光影。
  - 头发是深蓝，带青色高光块。
  - 只保留少量红色点缀：红色耳机、红色臂章。
- 人物设定和 art\p3menu\commu_v2_a.png 一致：
  - 深蓝头发遮住右眼；
  - 月光馆学园黑色校服外套、白衬衫、黑色细领结；
  - 脖子上挂红色耳机，左臂红色臂章，耳机线垂在胸前；
  - 平静、略冷淡的神情。
- 构图：
  - 1024×1536 竖图，透明背景；
  - 腰部以上的半身像，身体微微侧向画面左边（他站在画面右侧、看向左边的卡牌列）；
  - 整体向右倾斜约 8°；
  - 右手把一张塔罗牌举到脸旁边，牌面朝向观众：深蓝牌面、白色细框、中间一颗白色四角星；
  - 左手自然下垂，可以被画面下边缘裁掉。
- 不要背景、文字、水印、签名；画面里只有人物和手里的牌。

【4. 要交付的文件】都放在 <repo>\art\p3pile\：
1. 先出 3 个候选：pile_cand_a.png、pile_cand_b.png、pile_cand_c.png（1024×1536，RGBA，背景真透明）。
2. 从中挑一张最符合要求的，复制为 pile_base.png。挑选标准：
   - 画风最像 P3R 菜单立绘；
   - 人物设定和参考一致；
   - 手和牌没有畸形；
   - 透明边缘干净。
3. pile_eyes_closed.png：用 imagegen 的**编辑**功能，在 pile_base.png 上只改眼睛（露出的那只左眼；被头发遮住的右眼如果能看到一点也一起改）：
   - 双眼自然、平静地闭上（上眼睑垂下成一条带睫毛的弧线）；
   - 其余所有像素保持不变，输出仍是 1024×1536，和 pile_base.png 完全对齐。
4. pile_eyes_half.png：同样方式，眼睛半睁（眼睑盖住上半个瞳孔）。
5. 如果编辑改动了眼睛以外的地方：用 pile_base.png 把眼睛区域以外的像素还原，只保留眼睛周围的椭圆区域（边缘羽化约 8 像素），保证叠上去看不出接缝。
6. meta.json：
   - 露出的眼睛中心像素坐标；
   - 牌的四个角坐标；
   - 头顶、下巴、肩膀左右端点的大致坐标；
   - 头发左右两侧最外缘的大致坐标。
   以上都是以图片左上角为原点的像素坐标，游戏里给眨眼、牌面反光、呼吸和发梢摆动定位用。
7. preview.png：
   - 第一行：三张候选并排（深色背景上）；
   - 第二行：pile_base / 半睁 / 闭眼并排；
   - 第三行：眼部区域 3 倍放大的三张对比。
8. README.md（中文）：用的提示词、每张的生成和编辑次数、选了哪张和理由、是否做了第 5 步的还原、已知问题。

【5. 自检】不满足就重做，最多 4 轮；仍不满足就如实报告：
- 背景必须是真透明（alpha=0），不能是白底或棋盘格图案；人物边缘不能有白边或黑边。
- 闭眼版一定是真闭眼；闭眼和半睁两张叠到 pile_base 上，眼睛以外没有可见变化。
- 手指数量正常、牌是平整的长方形。
- 和 art\p3menu\commu_v2_a.png 放在一起看是同一个人。

【6. 提交与推送】
1. 只 add <repo>\art\p3pile\ 下的文件：
   git -c safe.directory=<repo> add art/p3pile
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R menu-style pile-screen portrait (Makoto with tarot card) + blink frames"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：做了什么、选了哪张、结果在哪、提交号、遇到的问题。

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
