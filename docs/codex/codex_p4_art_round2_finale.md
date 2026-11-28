# Codex 生图任务：总攻击用的素材（悠的收尾立绘、伊邪那岐出招、3 台电视机、演播室背景）

> 用途：总攻击（Series Finale）。敌人被打死后碎成雪花拼成一台小电视 → 彩带钻进电视、镜头跟进去 → 演播室里三台电视（左：伊邪那岐出招，右：标题画面，中间：黑屏，最后闪亮出结算画面）→ 结算画面里悠占画面左半边做主体。
> 用户（2026-11-28）：电视机用代码画不够好看、不够还原，要照 P4G / P4R 里的电视机生成素材；立绘和其他需要的素材也一起生成。
> **最重要：人物不能画走样（OOC）。** 脸、发型、眼镜、校服、武器都照参考图画。电视机照参考图的设计语言画，不要画成现代液晶电视。

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图 / 图像编辑模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/`、`art/p4/tv/`、`art/p4/studio/` 下的文件；工作区里别的未提交改动原样保留。

## 通用要求
- 画风：P4R 官方插画的风格——干净的线条、赛璐璐硬阴影、平涂为主，和下面列的官方图一致。
- **真透明底**：PNG RGBA，轮廓外 alpha = 0，不要背景、地面阴影、光晕、文字、边框（演播室背景 D 除外，它就是一整张背景）。四周留白至少 40px，不要被裁掉。
- 电视机的**屏幕玻璃区域也要真透明**（alpha = 0）：屏幕里面以后由程序放画面。屏幕是一个四边微微外鼓的圆角矩形（显像管），宽高比约 16:9，屏幕边缘干净，不要留反光、雪花、画面。

## A. 悠的收尾立绘 → `art/p4/finale/yu_finale.png`（1024×1536）
**姿势（用户要求：设计一个帅的姿势，不要伸手）——扛刀、推眼镜**：
- 3/4 身（头顶到大腿中部），身体 3/4 侧向画面右边，重心在后脚，肩膀放松。
- 右手（画面左侧）握太刀，刀背轻轻搭在右肩上，刀身从肩后斜着伸向画面左上方；刀柄在胸前。**刀身和刀尖完整留在画面里，不要被裁掉。**
- 左手（画面右侧）抬到脸前，食指和中指并拢轻推眼镜的鼻梁，手背朝外，手肘自然往外打开。手指要画对（五根、关节自然）。
- 头微微低下，眼睛从镜片上方直视镜头，嘴角很淡地一挑：冷静、自信，不要夸张的笑。
- 外套敞开，衣摆、立领和刘海被风往后吹起，像刚从电视里冲出来。
- 剪影要清楚好读：刀、推眼镜的手、外套下摆形成一个往右上打开的构图。
**参考**：
- 人物设计照 `research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`（全身）、`research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`（眼镜、外套、推眼镜的手）：银灰色头发、细框眼镜、黑色立领校服外套敞开、白衬衫。
- 太刀照 `research/p4r/allout/S01_4918_series_finale_prompt_cancel_go_yu_katana_yellow_stage.jpg`、`research/p4r/yu_battle/S02_0214_yu_idle_katana_yellow_outline_rear_3q.jpg`：黑色刀柄、银色刀身。
**提示词（可直接用，配上面的参考图）**：
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows. Three-quarter body shot (head to mid-thigh), body turned three-quarters to the viewer's right, weight on the back foot. His right hand holds a katana with the back of the blade resting on his right shoulder, the blade reaching diagonally up and back toward the upper left, the whole blade and tip inside the frame. His left hand is raised in front of his face, index and middle fingers together pushing up the bridge of his thin-framed glasses, back of the hand outward, elbow out. Head slightly lowered, eyes looking straight at the viewer over the lenses, a very faint confident smile. Silver-grey hair, black high-collared Yasogami school jacket worn open over a white shirt; the jacket hem, collar and bangs blown back by wind. Transparent background, no text, no frame.

## B. 伊邪那岐出招图 → `art/p4/finale/izanagi_strike.png`（1536×1024）
**参考**：
- 设计照 `art/p4/persona/izanagi/izanagi_full_v2.png`（用户确认过的身体和第二版薙刀：刀身宽直、刀背锯齿、镂空槽、大长方形护手）。
- 动作照 `research/p4r/izanagi/S07_0144_izanagi_blue_naginata_slash.jpg`：双手握薙刀斜着劈下，大衣下摆和身后的白色长飘带被动作带起来。
**画面**：全身，3/4 侧身，动作幅度大（动作可以有自己的发挥，设计不能走样）；薙刀刀刃上带一层蓝白色的电光（只贴着刀刃，不要满屏闪电，背景透明）。
**提示词**：
> Izanagi, the Persona from Persona 4 Revival, exactly the design of the reference (black long coat with red lining, silver helmet mask with yellow eyes, white belts on the legs, long white streamers behind), full body, three-quarter view, a big diagonal downward slash with the naginata held in both hands (wide straight blade with a serrated back, a cut-out slot near the guard, a large rectangular guard). Coat hem and streamers swept by the motion. A thin layer of blue-white electricity hugging only the blade edge. P4R anime illustration style, cel shading. Transparent background, no text.

## C. 电视机（3 台）→ `art/p4/tv/`
**设计参考**（同一种设计语言：P4 的复古显像管电视）：
- `research/p4g/movie_frames/golden_opening/03.jpg`：P4G 片头那台标志性的电视——方正的机身、四角圆，左边一块大屏幕，右边一条竖的面板（几道横向的喇叭栅格 + 两个圆旋钮），顶上两根 V 字形天线，底下两只短腿。
- `research/p4r/ui_transition/S02_0819_battle_end_tv_static_dim_tv_icon.jpg`：P4R 战斗界面右下角的小电视图标（黄色描边、两根天线）。
- `research/p4r/keyart/S05_1758_limited_box_promo_p4r_logo_cover_art_yu_box_illustration_artbook_yu_katana.jpg`：P4R 限定版里那台红色复古电视（光亮的红色塑料机身、米白色屏幕框、右侧喇叭栅格和旋钮）。
**共同要求**：正面平视（屏幕正对镜头，不要透视歪斜），P4R 赛璐璐画风（干净线条、硬阴影、少量高光），机身有体积感但不写实。屏幕玻璃区域真透明（alpha = 0），宽高比约 16:9，约占画面宽度的 60%；屏幕外面先是一圈黑色的窄边框（约屏幕宽的 3%），再是机身。不要画屏幕里的内容、反光、雪花、文字、标志。

### C1. 主电视 → `art/p4/tv/tv_main.png`（1536×1024）
用在战场上那台小电视和演播室中间那台大电视（最后镜头推满它，所以屏幕框要干净）。
- 机身：近黑色（深炭灰，带一点冷色高光），P4G 片头那台的造型；右侧竖面板：6–8 道横向喇叭栅格 + 两个黄色圆旋钮（P4R 的黄 `#f8f40e`）；顶上两根细天线呈 V 字，末端小圆球；底下两只短腿；机身外缘一圈细细的黄色描边（像 P4R 界面里那个电视图标）。
**提示词**：
> A retro CRT television in the iconic Persona 4 style, front view, flat-on, no perspective distortion. Boxy body with rounded corners, near-black charcoal plastic with subtle cool highlights and a thin lemon-yellow outline around the body. A large slightly bulging rounded-rectangle screen (16:9) on the left, about 60% of the image width, surrounded by a narrow black bezel; the screen glass area is fully transparent (empty). A vertical panel on the right with horizontal speaker slots and two round yellow dial knobs. Two thin antennas in a V shape on top with small ball tips, two short stubby legs. Persona 4 Revival anime style, clean lines, cel shading. Transparent background, no text, no logos, nothing on the screen.

### C2. 左边电视 → `art/p4/tv/tv_side_red.png`（1536×1024）
- 照 P4R 限定版那台红色电视：光亮的红色机身（`#e8242e` 左右）、米白色的屏幕外框、右侧喇叭栅格和两个旋钮（白 / 黄），顶上一根提手或两根短天线，四只小脚。屏幕同上真透明、16:9。
**提示词**：
> A retro portable CRT television in glossy red plastic, inspired by the red TV of the Persona 4 Revival limited box, front view, flat-on. Cream-white frame around a slightly bulging rounded-rectangle 16:9 screen (about 60% of the image width) with a narrow black bezel; the screen glass is fully transparent (empty). Speaker grille and two dial knobs on the right side, a carrying handle on top, four small feet. Persona 4 Revival anime style, clean lines, cel shading. Transparent background, no text, no logos, nothing on the screen.

### C3. 右边电视 → `art/p4/tv/tv_side_white.png`（1536×1024）
- 和 C1 同一个造型家族，但机身是象牙白（带一点暖色阴影），旋钮和天线球是黄色、喇叭栅格深灰；屏幕同上真透明、16:9。
**提示词**：
> A retro CRT television in ivory-white plastic with warm shading, same iconic Persona 4 TV design (boxy, rounded corners, big slightly bulging 16:9 screen on the left about 60% of the image width with a narrow black bezel, the screen glass fully transparent and empty; vertical right panel with dark grey horizontal speaker slots and two yellow dial knobs; two V antennas with yellow ball tips; short legs), front view, flat-on. Persona 4 Revival anime style, clean lines, cel shading. Transparent background, no text, no logos, nothing on the screen.

## D. 演播室背景 → `art/p4/studio/studio_bg.png`（1536×1024，不透明）
「午夜电视台」的演播室，放在三台电视后面当背景（参考 `research/p4r/allout/S05_0804_series_finale_intro_yu_raises_katana_under_spotlight_dark_stage_red_carpet.jpg` 的暗场舞台和聚光灯，`research/p4r/allout/S02_0740_allout_chance_prompt_party_dynamic_cam.jpg` 的黑白格地面）。
- 深藏青到黑的暗场；后墙是暗色的幕布或竖条面板，有几盏很小的黄色 / 青色舞台灯点；两束聚光灯从左上、右上斜着打到舞台中间；黑白格子的亮面地板往远处透视收拢，有淡淡的倒影和一点薄雾。
- **画面中间（宽度的中间 70%、高度 20%–75%）要留空**，不要放任何物体，以后电视放在这里。不要人物、电视、文字。
**提示词**：
> An empty dark TV studio stage at midnight, Persona 4 Revival anime background style. Deep navy-to-black space, a dark curtain or vertical panel back wall with a few tiny yellow and teal stage lights, two spotlight beams slanting in from the upper left and upper right onto the middle of the stage, a glossy black-and-white checkered floor receding in perspective with faint reflections and light haze. The central area is left completely empty (no objects there). No people, no televisions, no text, no logos.

提交信息：`art(p4): All-Out art (Yu finale, Izanagi strike, three TVs, studio)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
