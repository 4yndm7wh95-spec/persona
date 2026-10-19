你在用户的 Windows 电脑上工作。任务：用 imagegen 画 P3R 塔罗牌风格的新卡框（卡框本体、卡名横幅、卡图边框、类型标签、阿卡纳数字牌），再用仓库里的脚本对齐到原版的准确形状、出预览。只做美术，不改代码。
（第一步 codex_card_frame_extract.md 已经把原版卡框小图放进了 research/card_frame_src/。）

【背景】
- P3Makoto 是杀戮尖塔 2 的 mod，把储君换成 P3R（女神异闻录 3 Reload）的结城理。卡背已经是 P3R 的塔罗牌，现在把卡牌正面的卡框也换成 P3R 塔罗牌风格。版式（卡图、卡名、描述、费用的位置）完全不变，只换卡框的画。
- 游戏里卡框是几张小图拼起来的。新图必须和原版小图形状、尺寸完全一致 —— 这一点由脚本保证：你画好以后运行 tools/ui/frame_postprocess.py，它会把你的图缩放到原版尺寸、透明度直接套用原版的形状。你只要保证 **画的内容和原版小图的布局对得上**（边框在原版边框的位置、描述框在原版描述框的位置）。
- 用户的话：要 P3R 塔罗牌的风格，纹理细节要有，最好带点创新；之前 Claude 用 P 图做的效果图被否了（有残留、细节不够）。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略它们继续（不要移动、删除或提交）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。
6. 稀疏检出缺下面用到的目录时，允许 git -c safe.directory=<repo> sparse-checkout add research/card_frame_src art/p3r_tex art/cards tools/ui mod/pck_stage/p3makoto/fonts（单次命令可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 参考图】
- **风格基准**：<repo>\art\p3r_tex\shuffle\T_FX_Arcana_01_Col.png —— P3R 原版的阿卡纳牌（XXI 世界）。要学它：
  - 边框是带石纹 / 磨损质感的 **石板钢蓝**（深处约 #10243a，主色 #29435f ~ #416589，高光 #8cbede），表面有很细的 **凯尔特结 / 格纹浮雕**；
  - 边框内侧一道 **冰白细线**（#efffff），再一道 **浅钢蓝细线**，有一点倒角；
  - 底部正中一块 **上沿拱形的数字牌**：钢蓝底、冰白描边，里面是白色衬线罗马数字。
- 每张要画的原版小图（布局参考，必须作为 imagegen 的参考图 / 编辑底图）：<repo>\research\card_frame_src\sprites\ 下：
  - card_frame_attack_s.png / card_frame_skill_s.png / card_frame_power_s.png（598×844）：外圈红色是**边框**，下半棕色是**描述框**（三种的区别只是描述框上沿的形状：攻击是 V 形、能力是梯形缺口、技能是平的），上半黑色是**卡图窗口**（透明）。
  - card_portrait_border_attack_s.png / _skill_s.png / _power_s.png（551×420）：压在卡图下半圈的**卡图边框**（U 形 / V 形 / 圆角），盖住卡图和描述框的接缝。
  - card_banner.png（653×145）：**卡名横幅**（缎带形）。
  - card_portrait_border_plaque_s.png（123×75）：**类型标签**（写"攻击 / 技能 / 能力"的小牌子，游戏按九宫格拉伸，左右各 13px 不拉伸）。
- 拿不准原版在游戏里拼起来的样子，看 <repo>\art\cards\ref\card_attack_solar_strike.png（用户的游戏截图）。

【3. 要画的图和要求】
共同：P3R 塔罗牌的质感，干净、精致、不花哨；**不要文字、字母、数字、水印**（罗马数字由游戏另外写）；不要发光雾、粒子、镜头光晕。缩到游戏里的大小（卡宽约 300px）看要清楚。
每张先做 imagegen 的输入图：把原版小图放大 2 倍、铺在 50% 灰（#808080）底上，作为编辑底图 / 参考图，让新图的布局和它一一对应。

1. **卡框本体** card_frame_attack_s / card_frame_skill_s / card_frame_power_s：
   - 边框（原版红色那一圈）→ 石板钢蓝 + 细凯尔特结 / 格纹浮雕 + 内侧冰白细线和浅钢蓝细线（照风格基准）；四个角加一点小装饰（例如小的四角星或钟表刻度般的短线），要克制。
   - 描述框（原版棕色那块）→ **近黑的深海军蓝**（约 #060c1c ~ #0d1830），中间非常淡的一个大圆环（像钟表盘，带 12 个短刻度），上沿一排很淡的月相小圆（满月 → 新月）；淡到不影响上面的白色文字（对比度：图案最亮处不超过 #1f3050）。
   - 卡图窗口（原版黑色、透明的那块）画什么都行（脚本会把它挖空），但边界要和原版对齐。
   - 三张的边框、描述框画法完全一致，只有描述框上沿形状随原版不同。
2. **卡名横幅** card_banner：同样的缎带形，画成 **银白色金属**（P3R 原版牌底部数字牌那种冰白 / 银），表面有很细的刻线和一点磨损；**只画银白一种**（脚本会自动做出 银白 / 冰蓝 / 暗金 三种稀有度配色，所以要有明暗层次，不要纯白一片）。
3. **卡图边框** card_portrait_border_attack_s / _skill_s / _power_s：同一种银白金属，形状照原版；内沿一道细的深色刻线。也只画银白一种。
4. **类型标签** card_portrait_border_plaque_s：银白金属小牌，左右两头各 13px 是端头（九宫格不拉伸的部分），中间平整（会被拉长、上面写字）。只画银白一种。
5. **阿卡纳数字牌**（新增，原版没有）arcana_tab.png：照风格基准底部那块拱形数字牌，**空白（不要画数字）**，钢蓝石纹底 + 冰白描边，上沿拱形、下沿平直；
   这张直接画成 **真透明底 RGBA**，最终尺寸 168×64（imagegen 出更大的比例图后等比缩放；形状外 alpha = 0、边缘干净无底色残留）。

【4. 后处理（必须做）】
1. imagegen 的原始输出（任意尺寸）存到 <repo>\art\card_frame\gen\，文件名和上面一致：
   card_frame_attack_s.png、card_frame_skill_s.png、card_frame_power_s.png、card_banner.png、card_portrait_border_attack_s.png、card_portrait_border_skill_s.png、card_portrait_border_power_s.png、card_portrait_border_plaque_s.png。
2. 运行（需要 Pillow 和 numpy）：
   python <repo>\tools\ui\frame_postprocess.py art\card_frame\gen art\card_frame\sprites
   （在 <repo> 目录下运行）。它会输出原版尺寸、原版形状的卡框，以及横幅 / 边框 / 标签的 _common / _uncommon / _rare 三种配色。
3. 把 arcana_tab.png（168×64 真透明）直接放到 <repo>\art\card_frame\sprites\。
4. 罗马数字字体：从 Google Fonts 下载 **Cinzel**（SIL Open Font License，免费可再发布）的 Bold 字重，存为 <repo>\mod\pck_stage\p3makoto\fonts\Cinzel-Bold.ttf，许可证存为同目录 OFL-Cinzel.txt。下载不了就跳过并在报告里说明（预览会退回用别的衬线字体）。
5. 出预览：
   python <repo>\tools\ui\card_frame_preview.py art\card_frame\sprites art\card_frame\preview.png
   （3 种类型 × 3 种稀有度，套 art/cards/v3 的卡图、带阿卡纳数字牌）。

【5. 自检】不满足就重画那一张，最多 3 轮；仍不满足就如实写进 README：
1. 打开 preview.png：卡框、横幅、边框、标签拼在一起没有错位、缺口、残留的原版红色 / 棕色 / 青色。
2. 一眼像 P3R 的阿卡纳牌（石板钢蓝 + 冰白细线 + 银白金属），细节（石纹、浮雕、刻线）缩小后还能看出来，但不花哨。
3. 描述框够暗，白色文字放上去清楚（可以在预览图描述框里随便写两行白字目测）。
4. 三种稀有度的横幅 / 边框 / 标签分得清（银白 / 冰蓝 / 暗金），而且都好看。
5. 没有文字、数字、水印。

【6. 交付】
- art/card_frame/gen/（原始输出）、art/card_frame/sprites/（后处理结果 + arcana_tab.png）、art/card_frame/preview.png；
- art/card_frame/README.md（中文）：每张用的 imagegen 提示词全文、重画了几轮、已知问题；
- 下载成功的话：mod/pck_stage/p3makoto/fonts/Cinzel-Bold.ttf 和 OFL-Cinzel.txt。

【7. 提交与推送】
1. 只 add：
   git -c safe.directory=<repo> add art/card_frame mod/pck_stage/p3makoto/fonts/Cinzel-Bold.ttf mod/pck_stage/p3makoto/fonts/OFL-Cinzel.txt
   （字体没下载成功就不 add 那两个文件）
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R tarot-style card frame sprites (frame, banner, portrait border, type plaque, arcana tab)"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：每张画了几轮、预览图路径、字体是否下载成功、提交号、遇到的问题。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件（脚本只运行，不修改）。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下来报告。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
