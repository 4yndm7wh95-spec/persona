你在用户的 Windows 电脑上工作。任务：把伊戈尔商店背景（天鹅绒房间）**两侧的墙面重画成 P3R 原版那种从地到顶的铁艺格栅墙**，并导出一张逐像素对齐的格栅遮罩，供模组让电梯的光从格栅缝里一闪而上。只做美术，不改代码，不编译，不启动游戏。

【背景】
- 现在的背景 `mod\pck_stage\p3makoto\shop\velvet_room.png`（1672×941）两侧是细银色拱门 + 齐腰扶手栏杆 + 天鹅绒软包墙，像花园凉亭。用户判定"栏杆不像原版"。
- 原版参考：`art\velvet_elevator\ref_p3r_velvet_room.webp`（P3R 实机截图）。原版的墙本身就是电梯笼：**整面从地到顶的铁艺格栅板**，一块块重复排列，竖条密排，有菱形格、圆形纹章、百合花饰，金属是暗紫蓝、哑光、低对比。
- 上一轮的教训（不要再犯）：工业风（工字钢、铆钉、管道、防爆灯、现代电梯门）被否；卡通平涂、粗描边、饱和淡紫色被否；软遮罩抠图后放到黑底上发虚、带光晕被否。
- 伊戈尔的眨眼贴片、选中描边、点击热区都按现有像素位置对齐，所以**编辑区域以外一个像素都不能变**。

【1. 仓库】
沿用独立干净工作区 <wt>（`<repo>-elevator`；已删除就用 `git worktree add` 重建）。原工作区里其他会话的改动一律不要碰。
git -c safe.directory=<wt> -c http.version=HTTP/1.1 fetch origin claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次），再 `merge --ff-only origin/claude/admiring-knuth-lqj1x7`；不能快进就停下来报告。

【2. 素材（都在 <wt>\art\velvet_elevator\，1672×941）】
- base_room.png：编辑底图（就是现在游戏里的 velvet_room.png）。
- lattice_edit_region.png：允许修改的区域（白 = 可改，黑 = 不准动）。左右两侧从画面顶部到地面反光带（y≈690），不含玫瑰窗及两侧细高窗、椅子、伊戈尔、圆桌。
- lattice_guide.png：红色 = 可改区域，黄框 = 吊灯位置。
- lantern_left.png / lantern_right.png：已经从底图原样抠好的两盏吊灯（含吊链），模组会把它们作为独立图层画在最上面并让它们摆动。
- ref_p3r_velvet_room.webp：原版参考。

【3. 要画的：room_lattice.png】
在红色区域内，把现有的拱门、扶手栏杆和软包墙**全部替换**成格栅墙：
- **结构**：从画面顶部一直到地面的整面格栅墙，由若干块格栅板组成，板与板之间是竖直的方柱。每块板分上中下三段：上段是拱形或尖拱框，中间一枚圆形纹章（百合花饰）；中段是菱形格；下段是卷草花饰；全板贯穿密排的细竖条。花样照参考图的墙，左右两侧对称。
- **透视**：房间是圆形的，墙沿弧线向两侧绕开。格栅板顶部的横线沿现有拱门顶部那条弧线走，竖柱保持竖直，越靠画面边缘的板越窄（透视压缩）。墙底落在现在扶手底座所在的地面线上。
- **格栅缝**：格栅之间的空隙画成很深的暗色（约 #04050F～#0B0E26），像笼子外面就是黑暗的井道，可以有一点点远处的层次，但不要画任何具体物体。缝隙要占墙面面积的大约 35%～50%，不能太密，不然电梯的光透不出来。
- **金属**：暗紫蓝、哑光（约 #1E2148～#3A3F78），只在少数边缘有柔和的淡紫银色高光（约 #8A8FC0），**不要亮银镀铬感，不要强烈的蓝色轮廓光**。整体比现在两侧暗，焦点仍是伊戈尔，但格栅花样要能看清。
- **画法**：和房间其余部分一样的厚涂动画背景画风，柔和光影，线条干净锐利，不要粗描边平涂，不要模糊。
- **吊灯**：两盏吊灯**不要画进去**，吊灯所在位置后面照常画格栅（模组用 lantern_left/right.png 盖在上面）。
- **地面**：地面反光带（y≈620～690）里，画出格栅墙底部淡淡的倒影，替换掉原来扶手栏杆的倒影。
- **边界**：红色区域靠近玫瑰窗那一侧的边界，尽量落在一根竖柱上，和不改动的部分自然衔接，不能出现直线切边或亮度断层。圆桌（桌面银边、桌布边缘）即使落在红色区域里也要保持原样。
- 画完后，用底图把 lattice_edit_region 以外的像素还原（区域外变化像素数 = 0）。输出不透明 RGB/RGBA PNG，1672×941。

【4. 要导出的：lattice_mask.png】
- 1672×941 灰度 PNG，和 room_lattice.png **逐像素对齐**：白 = 实心（格栅铁条、竖柱、框，以及红色区域以外的一切），黑 = 格栅缝隙（电梯光会出现的地方），铁条边缘允许 1～2px 灰度抗锯齿。
- 直接从你画好的 room_lattice.png 生成（按缝隙的暗色阈值 + 红色区域），不要另外重画；生成后人工检查，确保铁条上没有误判成缝隙的暗部、缝隙里没有误判成铁条的亮点。
- 吊灯不需要体现在遮罩里（模组单独处理）。
- 遮罩在 y 550～690 之间（墙底和地面倒影）可以逐渐过渡成白色，让光不落到地面上。

【5. 交付（<wt>\art\velvet_elevator\）】
1. room_lattice.png、lattice_mask.png。
2. preview_lattice.png：room_lattice.png 上再盖两盏吊灯（平时的样子）。
3. preview_flash.png：同上，再在左侧格栅后面模拟一道冷蓝光（例如在遮罩为黑的地方加一块 #6E9CFF 的亮光块，宽约 450px、高约 500px），看光从缝里透出来的效果。
4. compare_lattice.png：三联 —— 左 = base_room.png，中 = preview_lattice.png，右 = ref_p3r_velvet_room.webp（缩放到同高）。
5. README_lattice.md（中文）：提示词、编辑次数、区域外变化像素数、遮罩阈值和缝隙面积占比、已知问题。

【6. 自检】最多 4 轮，仍不满足就如实报告：
- room_lattice.png 在编辑区外与底图逐像素一致；伊戈尔、椅子、桌子、玫瑰窗、细高窗、地毯完全没变；
- 两侧是整面从地到顶的格栅墙，花样和参考图一致，没有凉亭式拱门、扶手栏杆、工业元素或现代元素；
- 金属暗而哑光、低对比，焦点仍是伊戈尔；线条清晰不糊，没有粗描边；
- 边界没有直线切边或亮度断层；吊灯位置后面是完整的格栅；
- lattice_mask.png 与画面对齐（把遮罩半透明叠在 room_lattice.png 上检查），缝隙占比在 35%～50%；
- preview_flash.png 里光只出现在格栅缝里，铁条完整挡在光前面。

【7. 提交与推送】
git -c safe.directory=<wt> add art/velvet_elevator
git -c safe.directory=<wt> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R-style lattice cage walls for the velvet room with aligned lattice mask"
git -c safe.directory=<wt> -c http.version=HTTP/1.1 push origin HEAD:claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次）
不要改 mod\ 下的任何文件，不要改版本号，不需要编译和打包（接入由 Claude 做）。

---

【修订 1 · 修桌子两侧的旧墙残影，然后打包 0.15.26】
格栅墙通过，已接入代码（0.15.26：房间换成 room_lattice.png，电梯只从格栅缝里透出，吊灯改为独立图层）。只剩一个问题：
**圆桌左右上方各有一条半透明的旧墙残影**（你 README 里提到的"桌旁窄带"）：左侧约 x 331～565、右侧约 x 1107～1341，从格栅墙底部那条弧线到桌面银边、以及桌布左右边缘外侧到地面线之间，还是原来的浅蓝软包墙和扶手栏杆，并且有一条渐变过渡的弧形亮边。这是我给的编辑区遮罩把桌子保护得太宽造成的。

要求：
1. 用你上次 imagegen 的原生成图（那块区域里应该已经有格栅墙），或者重新编辑，把这两条残影换成和两边连续的格栅墙底部 / 地面倒影，一直画到**桌子真实的轮廓**（桌面银边上沿、桌布左右边缘）为止。
2. 桌子本身（桌面、银边、桌布、桌布花纹）必须和 base_room.png 逐像素一致：先精确抠出桌子轮廓做保护遮罩（可以人工修边），合成后再用底图把桌子像素还原。
3. 不允许出现弧形亮边、渐变过渡带或直线切边。
4. 同步更新 lattice_mask.png（新补的格栅区域也要有正确的缝隙），然后把修好的两张图**同时**复制到 `mod\pck_stage\p3makoto\shop\room_lattice.png` 和 `mod\pck_stage\p3makoto\shop\lattice_mask.png`（覆盖），art\velvet_elevator\ 下的同名文件也一起更新。
5. 交一张 fix_table_band.png：桌子左右两侧局部放大，修前 / 修后上下对比。

然后编译、打包、安装：
- 代码已在 `claude/admiring-knuth-lqj1x7` 上，版本号已是 0.15.26，不要再改。
- `dotnet build mod/P3Makoto.csproj -c Release '-p:GameDir=<游戏目录>'`，0 错误。
- 按 BUILD.md 重新打包 PCK。`p3makoto/shop/elevator_front.png` 和 `elevator_backdrop.png` 已删除，pck 里不要有；必须有 `room_lattice.png`、`lattice_mask.png`、`lantern_left.png`、`lantern_right.png`、`elevator_car.png`、`elevator_car_glow.png`，以及 `p3makoto/bgm/velvet_room.ogg`、`velvet_room.loop.txt`。打包后列出 pck 里 `p3makoto/shop/` 和 `p3makoto/bgm/` 的文件写进报告。
- 备份后安装进游戏，按惯例生成 dist ZIP。
- 提交并推回 `claude/admiring-knuth-lqj1x7`。
