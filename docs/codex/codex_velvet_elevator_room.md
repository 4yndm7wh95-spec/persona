你在用户的 Windows 电脑上工作。任务：把伊戈尔商店背景（天鹅绒房间）的**两侧后墙**改成能看到电梯井道的开口，并按图层交付，供模组做"电梯一直上升"的动态背景。只做美术，不改代码，不编译，不启动游戏。

【背景】
P3 的天鹅绒房间是一部一直上升的电梯。现在的背景 `mod\pck_stage\p3makoto\shop\velvet_room.png`（1672×941）两侧栏杆后面是封死的天鹅绒软包墙，看不出在上升。用户确认：**伊戈尔、椅子、圆桌、地毯、玫瑰窗、铁艺栏杆/柱子/拱都不动**，只把栏杆后面的软包墙挖成开口，开口后面放一张可以竖向无缝滚动的井道图，由模组的 shader 让井道持续向下滚，营造上升感。
伊戈尔的眨眼贴片、选中描边、点击热区都按现有像素位置对齐，所以**编辑区域以外一个像素都不能变**。

【1. 找到仓库并拉取】
1. 找到你上次成功推送的仓库 <repo>（remote 是 github.com/4yndm7wh95-spec/persona，里面有 mod\P3Makoto.json）。所有 git 命令都加 -c safe.directory=<repo>。
2. git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪文件原样保留、忽略。
3. git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次）。
4. git -c safe.directory=<repo> merge --ff-only origin/claude/admiring-knuth-lqj1x7；不能快进时改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/admiring-knuth-lqj1x7；有冲突就停下来报告。

【2. 素材（都在 <repo>\art\velvet_elevator\，1672×941）】
- base_room.png：**编辑底图**，就是现在游戏里的 velvet_room.png。
- edit_region.png：允许修改的区域（白 = 可改，黑 = 不准动）。覆盖左右两侧栏杆后面的软包墙、玫瑰窗两侧的细高窗、两盏吊灯；不含玫瑰窗、椅子、伊戈尔、圆桌和地毯。
- guide.png：红色 = 可改区域；黄色框 = 吊灯和吊链所在的位置（要单独抠成图层，见下文）。

【3. 要做的】
A. **前景板 room_front.png**
- 红色区域里的天鹅绒软包墙（包括栏杆底部立柱之间露出来的墙）全部挖空，alpha = 0，变成通往井道的开口。玫瑰窗两侧的细高窗也挖空。
- **铁艺栏杆、柱子、顶部拱形花饰、底部扶手和立柱必须完整保留、保持不透明**，像站在笼式电梯里隔着栏杆往外看。栏杆像素直接沿用底图，不要重画；被挖空的地方栏杆边缘要干净，不要残留蓝色软包的毛边或光晕（需要的话去色边）。
- 两盏吊灯连吊链从前景板里**拿掉**，它们后面同样是开口和栏杆（栏杆被灯挡住的部分照两侧的花样补全）。
- edit_region 以外的像素必须和 base_room.png 完全一致（变化像素数 = 0）。

B. **吊灯图层 lantern_left.png、lantern_right.png**
- 尺寸 1672×941 RGBA，只有吊链 + 灯是不透明的，其余透明，位置和底图完全一致。
- 灯和吊链的像素**从底图原样抠出来**，不要重画；边缘抗锯齿干净。
- 吊链顶端挂点大约在 (192,75) 和 (1474,75)，模组会以挂点为轴让灯摆动；如果实际挂点不同，在 README 里写出你量到的坐标。

C. **近景井道 shaft_near.png**
- 尺寸 1672×2048 RGBA（不透明），**上下必须无缝衔接**（第一行和最后一行能接上，竖向平铺看不出接缝），左右不需要平铺。
- 内容：电梯井道的内壁，离栏杆很近。深蓝、近黑的石墙或铁板墙，带竖向导轨和钢缆；每隔约 512px 有一道横向钢梁和铆钉；每隔 1024px 有一个楼层口（门框 + 一盏冷蓝色小灯），这样滚动时每隔一段就有一层楼经过。
- 色调和房间统一（钴蓝、天青、少量银色金属反光），整体**明显比原来的软包墙暗**，平均亮度约为原墙的 40%～55%，不能抢伊戈尔的视觉焦点。不要文字、数字或标志。
- 横向构图注意：开口只在画面左右两侧（大约 x 0～630 和 1040～1672），中间那段会被玫瑰窗和椅子挡住，楼层口和小灯放在两侧开口能看到的位置。

D. **远景井道 shaft_far.png（可选，做了更好）**
- 尺寸 1672×1024 RGBA，上下无缝。比近景更暗、更模糊：远处的钢缆、配重块、零星的冷光点。模组会用比近景慢的速度滚，做出纵深视差。如果做这一层，就在 shaft_near.png 的井壁上留一些镂空（alpha < 1 的缝隙、格栅孔），让远景能透出来。

E. **楼层表盘（可选）**
- floor_dial.png（512×512，透明背景）：P3 风格的半圆楼层指示盘，银色铁艺外框配深蓝底，刻度不写具体数字或写罗马数字。
- floor_needle.png（512×512，透明背景）：单独的指针，转轴放在图片正中心。
- 位置由模组决定，你不需要合成进背景。

【4. 交付（<repo>\art\velvet_elevator\）】
1. room_front.png、lantern_left.png、lantern_right.png、shaft_near.png；可选：shaft_far.png、floor_dial.png、floor_needle.png。
2. preview.png：1672×941，按"远景 → 近景（从 y=0 开始截取 941 高）→ 前景板 → 两盏吊灯"的顺序合成的静态效果。
3. preview_scroll.gif：同样的合成，近景每帧向下移动 8px、远景 3px，至少 128 帧，用来检查无缝滚动和楼层经过的感觉（可以降到 836×470）。
4. compare.png：左右并排，左 = base_room.png，右 = preview.png。
5. README.md（中文）：提示词、编辑次数、区域外变化像素数、shaft 上下接缝检查结果、量到的吊灯挂点、已知问题。

【5. 自检】最多 4 轮，仍不满足就如实报告：
- room_front.png 在 edit_region 以外和底图逐像素一致；伊戈尔、椅子、桌子、玫瑰窗、地毯完全没变；
- 栏杆、柱子、拱形花饰完整不透明，没有被挖断；开口边缘没有蓝色毛边、光晕或残留的软包碎块；
- 吊灯图层叠回前景板上和底图位置完全重合（把 room_front + 两盏灯合成，与底图在吊灯区域对比）；
- shaft_near.png（以及 shaft_far.png）竖向平铺两张时看不出接缝；
- preview.png 里井道比原来的墙暗，画面焦点仍然是伊戈尔。

【6. 提交与推送】
git -c safe.directory=<repo> add art/velvet_elevator
git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: velvet room elevator layers (front plate with shaft openings, lanterns, scrolling shaft)"
git -c safe.directory=<repo> -c http.version=HTTP/1.1 push origin HEAD:claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次）
不要改 mod\ 下的任何文件，不要改版本号。

---

【修订 1 · 改成"栏杆后面偶尔一闪而过的电梯"，不再做滚动井道】
用户看过第一版井道（已存为 art\velvet_elevator\rejected_shaft_v1.webp），判定不通过。原版参考：art\velvet_elevator\ref_p3r_velvet_room.webp（P3R 天鹅绒房间实机截图）。
用户补充了原版的表现：**背景栏杆后面平时是暗的，过一会儿就有一部电梯从下往上一闪而过。**所以不要持续滚动的井道墙；第 3 节 C（shaft_near）和 D（shaft_far）**作废**，改为下面的 C'、D'。A（room_front）、B（吊灯）、E（楼层表盘，可选）不变；已经做好并通过自检的不用重做。

第一版的问题（避免再犯）：
1. 风格错了：工字钢横梁、铆钉、管道、防爆灯罩、现代电梯门，像工业厂房或写字楼。原版天鹅绒房间的墙是**新艺术风格的铁艺格栅**（竖条、菱形格、圆形纹章、卷草花饰），深海军蓝偏紫，低对比，光线很暗，像剧场，不是机械感。
2. 没有无缝：底部电梯门在边缘被直线切断，尺寸也不是要求的 1672×2048。
3. 太亮太规整，会抢伊戈尔。

C'. **backdrop.png（开口后面的暗背景，静止）**
- 1672×941，不透明，和 room_front.png 对齐，放在它后面。
- 近乎全黑的深海军蓝到暗紫（约 #05071A～#141A3C），上面隐约能看到更远处的铁艺格栅剪影（照参考图墙面的花样），模糊、低对比。
- 平均亮度不超过原软包墙的 35%：它要暗，电梯经过时的亮光才显得出来。
- 不要灯具、工业结构、文字、数字。

D'. **passing_car.png（一闪而过的电梯轿厢）**
- 透明背景 RGBA，正面平视，单独一部电梯轿厢，不带井道。高约 620px、宽约 360px（这是 1672×941 背景里的尺寸，开口高度约 600px，轿厢大致能撑满开口），画布四周留 16px 透明边。
- 造型：天鹅绒房间风格的**笼式电梯**：铁艺格栅外壁（竖条、菱形格、顶部拱形花饰和纹章），银紫色金属；轿厢内部亮着冷蓝色的光（约 #4C7DFF～#9CC4FF），光从格栅缝里透出来。它是整个背景里最亮的东西，但内部不要画人物。
- 轿厢顶部和底部各有一块实心的铁艺底板 / 顶冠，挂钢缆的顶冠可以略高出顶板。
- 我会在代码里做向上的高速位移、竖向运动模糊、经过时照亮栏杆的光，以及左右开口随机出现，**你不需要做运动模糊或动图帧**。
- 另交一张 passing_car_glow.png：同尺寸，只有轿厢内部透出的光（灰度或蓝色，黑底或透明皆可），用来做加亮叠加。

preview 改为：
- preview.png：backdrop → room_front → 两盏吊灯 的合成（平时的样子）。
- preview_car.png：同上，再把 passing_car.png 放在左侧开口中间、room_front 之下（栏杆要挡在轿厢前面），看轿厢的大小和风格。
- 不需要 preview_scroll.gif；compare.png 改为 base_room.png 和 preview_car.png 并排。

自检改为：
- room_front / 吊灯 的原有自检不变；
- preview.png 里背景比原来的墙暗，焦点仍是伊戈尔；
- preview_car.png 里栏杆完整挡在轿厢前面，轿厢风格和栏杆、参考图一致，没有工业或现代元素。

交付文件：room_front.png、lantern_left.png、lantern_right.png、backdrop.png、passing_car.png、passing_car_glow.png、preview.png、preview_car.png、compare.png、README.md（可选：floor_dial.png、floor_needle.png）。之前生成的 shaft_near / shaft_far 不要提交。
