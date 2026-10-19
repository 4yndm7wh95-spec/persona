你在用户的 Windows 电脑上工作。任务：用 imagegen 把储君的卡牌卡图（card portrait）逐张重绘成 P3R / 结城理风格。只做美术，不改代码。
本文件分 7 批（见【8. 分批】），**每次会话只做一批**：用户说"做第 N 批"就做第 N 批；没说就**按 v2 画风重做第 1 批**（试点）。做完一批就提交推送并报告，然后停下。

【背景】
- P3Makoto 是杀戮尖塔 2 的 mod，把储君（Regent）换成 P3R（女神异闻录 3 Reload）的主角结城理。现在卡背已经是 P3R 的塔罗牌，卡面还是原版储君的画。
- 这次重画每张卡的卡图（卡框、费用、描述框的版式先保留原版，不用画）。升级前后用同一张图。
- **核心标准**：一眼就能认出是原版的哪张卡（构图、主体、动作、要表达的意思跟原版走），同时一眼能看出是 P3R / 结城理的风格。
- 用户的话：**重绘力度可以大一些，不需要为了像原版而刻意压制重绘和创新**。原版的储君可以换成结城理、他的手、人格面具（Persona），或者干脆只留物件和特效；不需要每张都露脸露手。

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
6. 稀疏检出缺下面用到的目录时，允许
   git -c safe.directory=<repo> sparse-checkout add research/regent_assets/images/packed/card_portraits art/p3pile art/p3pile_deck art/cards
   （路径过长时这一条命令可以加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 输入】
- 原版卡图（构图参考，每张都要喂给 imagegen 当参考图）：<repo>\research\regent_assets\images\packed\card_portraits\regent\<名字>.png
  （大部分 1000×760；the_sealed_throne、meteor_shower 是 606×852 竖版）。
- 结城理的长相、服装、配色：以 <repo>\art\p3pile\pile_base.png 和 <repo>\art\p3pile_deck\deck_cand_c.png 为准（画到他的脸 / 手 / 衣服时一起作为参考图）。
- 配色规范：<repo>\docs\STYLE_P3R.md（只读）。

【3. 画风（所有卡共用，写进每张的 imagegen 提示词）】
> **v2（第 1 批用户反馈："风格化太重，整个卡面很花哨，AI 味很重"）**：第 1 批每张都铺满碎玻璃、光点、速度线，全部刷成蓝色 —— 这些全部取消。
> 下面是 v2 的规则，以这一节为准；【5】里每张卡方案中提到的碎玻璃 / 蝴蝶 / 光点 / 速度线一律删掉不画（除非那就是这张卡的主体）。

- **少**：每张卡只放 **一个** P3R 元素（结城理 / 他的手 / 人格面具 / 暗影 / 塔罗牌 / 影时间的月亮，选【5】里写的那一个最主要的），其余都跟原版走。
- **背景简单**：两三块大的平涂色块或很缓的两色过渡，像原版那样留白；**不要**碎玻璃、碎片、粒子、光点、星星点点、镜头光晕、速度线、满屏特效、水面反光、细碎纹理。
- **底色保留原版这张卡自己的颜色**（红就还是红、绿就还是绿、紫就还是紫），只稍微降一点饱和度、偏冷一点；**不要**全部刷成蓝色。主体特效的颜色也跟原版走。
- **画法**：大色块、两层硬边阴影、粗细一致的深色描线；形状简单、轮廓清楚；**不要**金属反光、光滑渐变、发光边、细碎高光、过度细节。缩到卡牌大小（约 250×190）还要一眼看清主体。
- 两个方向，每张卡各出一个候选（这就是 a / b 的区别，不再是"贴近 / 大胆"）：
  - **a = 贴原版画法**：用 imagegen 的图像编辑，把原版卡图当底图直接改。背景、构图、配色、画法（原版的厚涂 + 粗描线）基本不动，只把储君 / 他的手 / 小怪换成【5】里写的结城理 / 他的手 / 人格面具 / 暗影，换上去的部分也用原版的画法画，和原图融为一体。
  - **b = 克制的 P3R 动画风**：参考原版构图重新画，用 P3R 的干净赛璐璐画法，但严格遵守上面的"少""背景简单""底色保留"三条。
- 结城理（需要时）：
  > a slender Japanese high school boy, Persona 3 protagonist style: dark navy-blue hair with long bangs covering one eye, calm expressionless grey eyes, black school blazer worn open over a white shirt, red armband on the upper arm, red-and-white headphones around the neck with the cord leading to an MP3 player
  - 原版储君的"手"→ 结城理的手：干净的白皙少年的手，黑色校服袖口里露出白衬衫袖口（原版是棕金色的粗糙护手，不要照抄）。
  - 原版储君的尖帽斗篷身影 → 结城理（全身 / 半身 / 背影 / 剪影都行），或者人格面具。
  - 召唤器（银色手枪形状）只能拿在手里垂着、插在腰间，或者只画召唤时碎裂的蓝色玻璃片；**不要画枪口对着头**（生图会拒绝，也不需要）。
- 人格面具（Persona），用描述，不要只写名字：
  - Orpheus：a slender mechanical humanoid guardian spirit with a pale white-and-gold metallic body, a golden lyre mounted on its back, a red scarf；
  - Thanatos：a towering black reaper-like guardian spirit with a pale skull-like mask, a ring of chained black coffins floating behind its back, a long sword；
  - Messiah：a radiant white winged guardian spirit with a coffin-shaped halo-like frame behind it。
- 敌人 / 小怪 → P3R 的 Shadow（暗影）：黑色黏稠的团块 / 伸出很多黑手，脸上戴着蓝色的阿卡纳面具。
- 场景元素（只在【5】里这张卡写到时才用，不要额外加）：塔尔塔罗斯（Tartarus，高耸的扭曲高塔）、影时间（墨绿色天空、诡异的大月亮、立着的黑色棺材）、天鹅绒房间（深蓝色的房间、蓝色天鹅绒）、塔罗牌、钟表、月相。
- 不要：文字、字母、数字、水印、签名、UI、卡框、边框（卡框由游戏画，图要满版出血）。

【4. 每张图的规格】
- 尺寸和原版一样：1000×760（the_sealed_throne、meteor_shower 为 606×852）。imagegen 出不了这个尺寸时，出最接近比例的更大尺寸，再等比缩放 + 居中裁切到目标尺寸（不要拉伸变形）。
- **满版不透明 RGB**（卡图是整张背景，这是《生图一律真透明底》规则的例外，不要透明）。
- 主体放在中间 80% 的安全区里（四边各留约 10%，游戏的卡框会盖住一点边）。
- 每张出 2 个候选：a = 贴原版画法（编辑原图），b = 克制的 P3R 动画风（见【3】）。

【5. 每张卡的重绘方案】
格式：文件名 | 阿卡纳（卡在 P3R 里对应的大阿卡纳，用来定基调，不要把文字画进图里）| 原版辨识锚点（必须保留的构图 / 主体）| 重绘方案。
"理"= 结城理，"手"= 结城理的手，"面具"= 人格面具，"暗影"= Shadow。

**基础**
- strike_regent | 0 愚者 | 斜向的发光短剑 + 三道剑光，从左上劈向右下 | 剑换成理的单手直剑（细长、单刃），金白色剑光保留，背景深蓝 + 一道红色斜切色带，碎玻璃片飞散。
- defend_regent | 0 愚者 | 储君躲在左边，一块巨大的石板盾挡住右边射来的三道光 | 理侧身举起一面由蓝色碎玻璃 / 塔罗牌拼成的巨大护盾，挡住三道光；石板的木纹换成玻璃裂纹。
- falling_star | XVII 星星 | 储君在左边伸手，右边一团砸下来的火焰陨星和碎石 | 理在左边抬手，召唤器碎片环绕；右边橙色陨星（保留火焰橙）砸进地面，碎石变成蓝色玻璃碎块。
- venerate | V 教皇 | 下方左右两只手捧着，中间一串星座连线和两颗四角星 | 两只手换成理的手，星座保留；背景加一张半透明的教皇塔罗牌剪影和几只蓝蝴蝶。

**普通**
- astral_pulse | XVIII 月亮 | 前景一大片粉色星球弧面，后面一个怪物 | 前景换成巨大的月面弧（冷白 / 青），后面是戴蓝面具的暗影被冲击波震开。
- celestial_might | XI 力量 | 中间一个被打得乱飞的敌人，后面巨大的四角星形身影 | 中间暗影被多段打击，后面巨大的身影换成 Orpheus 的剪影。
- cloak_of_stars | XVII 星星 | 一顶飘起来的巨大斗篷 + 星空内衬 + 两道火 | 斗篷换成理的黑色校服外套飘在空中，内衬是星空；两道火换成蓝色光带。
- collision_course | XVI 塔 | 一个敌人被斜着撞飞，碎片和羽毛 | 暗影被斜着撞飞，碎片换成碎玻璃和碎石，背景一角露出塔尔塔罗斯。
- cosmic_indifference | IX 隐者 | 储君双手交叉、身体半透明发光、两道光从两侧射出 | 理双手交叉护在胸前，身体边缘半透明成玻璃质感，两侧的光（保留金黄）。
- crescent_spear | XVIII 月亮 | 一只手从左下举起一把新月形长矛，品红发光 | 理的手举起新月形长柄武器，品红换成月光的冷白 + 少量品红边。
- crush_under | XI 力量 | 储君从上往下砸，四周绿色小怪被压扁 | 理（或 Orpheus）从上往下砸，四周一圈暗影被压扁，冲击波是蓝白色。
- gather_light | XIX 太阳 | 储君在右边举手，左边一颗巨大的金色四角星 + 闪电 | 理在右边举手，左边金色四角星保留，闪电变成青色。
- glitterstream | XIV 节制 | 储君俯身，手里捧着一团发光的星光漩涡 | 理俯身，手里一团青色光流，光点像水里的气泡往上飘。
- glow | XVII 星星 | 两颗四角星 + 一道白色弧光（几乎只有特效） | 保留两颗星和弧光，弧光换成月牙形，背景加一轮影时间的月亮。
- guiding_star | XVII 星星 | 一道青色光束打中一个怪物 | 青色光束保留，被打中的是暗影，光束尽头是一颗四角星。
- hidden_cache | II 女祭司 | 一个绿边宝箱被光爆开 | 宝箱换成天鹅绒房间风格的蓝色小箱子 / 公文包，爆开的光是黄白色，飞出塔罗牌。
- know_thy_place | VIII 正义 | 左上一只手指向右边的小怪，小怪缩着 | 理的手指居高临下指着缩成一团的暗影，暗影的蓝面具裂开。
- patter | XVII 星星 | 储君被一堆星星和闪电包围，双手交叠 | 理双手交叠，四周一圈小四角星和青色电光。
- photon_cut | I 魔术师 | 一把长刀横向切开，切出一团黄光，刀刃上方有一块肉块状的敌人 | 理的单手直剑横向切出黄白色光弧，被切开的是暗影。
- refine_blade | XIV 节制 | 储君双手捧着一把剑横放，剑身被锤打发光 | 理的手横捧一把剑，剑身中段亮起青色光，蓝色火星飞溅。
- solar_strike | XIX 太阳 | 左边一团被太阳光灼烧的敌人，右上一大片红白色的太阳弧 | 右上巨大的太阳弧保留（橙红），左边暗影被灼烧；像 P3R 火系技能的演出。
- spoils_of_battle | VII 战车 | 两只手捧着一团蓝色宝物，中间一把剑插下 | 理的两只手捧着一团蓝色玻璃宝珠，中间插着一把发光的剑。
- wrought_in_war | VII 战车 | 储君握着一把蓝色大剑向左斜刺，红色背景 | 理双手握剑向左斜刺，剑身是蓝色玻璃质感，红色只留在剑光边缘。
- begone | XIII 死神 | 储君在左边伸手，一个敌人被打成碎片 | 理伸手，对面的暗影被打碎成碎玻璃，背景有一具立着的黑色棺材。

**罕见**
- alignment | XVII 星星 | 两颗大四角星 + 星座连线（只有特效，绿色背景） | 保留两颗星和连线；绿色换成影时间的墨绿 + 青。
- black_hole | XIII 死神 | 一个怪物被拉进圆形的黑洞 / 漩涡，四周一圈蓝色电光 | 黑洞保留，周围是 Thanatos 的棺材环；被吸进去的是暗影。
- bulwark | VIII 正义 | 储君背对，面前竖起一排巨大的冰晶城墙 | 理背对镜头，面前竖起一排蓝色玻璃 / 冰晶墙。
- charge | VII 战车 | 一颗四角星 + 周围的岩石（几乎全是特效） | 四角星保留，四周换成塔尔塔罗斯的扭曲墙面。
- child_of_the_stars | XVII 星星 | 储君站在左边，右边一团巨大的冰晶 / 星星碎片 | 理站在左边，右边巨大的蓝色碎玻璃星形。
- conqueror | IV 皇帝 | 一只巨大的爪子手握住一把发光的剑，红色背景 | 理的手握住一把发光的剑（青色），背景红换成深蓝 + 一道红色斜切。
- constellation | VI 恋人 | 两只手在一个机械星座仪上传递星光 | 理的手和另一只（同伴的）手之间传递一团星光，中间是星座仪。
- convergence | X 命运之轮 | 储君挥手，一道星光圈绕过身体 | 理挥手，一道发光的弧线环绕身体，像钟表指针的轨迹。
- devastate | XIII 死神 | 一个怪物被斜向的黄色巨大光束打中 | 黄色光束保留，被打中的是暗影；Thanatos 的剑劈下来的方向。
- furnace | XV 恶魔 | 左边一桶熔化的金属倒下，右边一个锻造台 | 熔化的金属换成发光的蓝色液体，锻造台保留，背景深蓝。
- gamma_blast | XVI 塔 | 储君双手合拢，中间一颗白色光球 | 理双手合拢，中间一颗白色电光球（P3R 雷系的感觉）。
- glimmer | II 女祭司 | 一只手往一团蓝 / 粉色星云里伸 | 理的手伸进一团蓝色星云，星云里有一张塔罗牌。
- hegemony | IV 皇帝 | 储君的手按在发光的网格地面上，一道黄色光柱打中一只鸟 | 理的手按在青色网格地面上，黄色光柱打中暗影。
- kingly_kick | IV 皇帝 | 储君一脚踢碎一块石头 | 理一脚踢出，脚下的石块碎成蓝色玻璃。
- kingly_punch | IV 皇帝 | 储君一拳打出，拳头周围冲击光 | 理一拳打出，拳头周围白色冲击光 + 蓝色碎片。
- knockout_blow | XIII 死神 | 储君挥拳，一个敌人被电光击飞 | 理挥拳，暗影被青白色电光击飞，面具碎裂。
- largesse | III 女皇 | 一只大手把一团光递给一个小小的人 | 理的手把一团光递出，接光的是一个蓝色的小人影 / 蝴蝶。
- lunar_blast | XVIII 月亮 | 左边一颗月亮，一道光束射向右边的岩石 | 月亮保留（冷白），光束打碎右边的暗影 / 岩石（像冰系技能）。
- manifest_authority | IV 皇帝 | 储君张开手掌，一圈带刺的光环 + 红色箭头 | 理张开手掌，一圈带刺的光环保留，红色箭头换成蓝白色。
- monologue | IX 隐者 | 储君的大脸 + 手里一个骷髅，旁边飘着文字碎片 | 理拿着一个白色的面具（像 P3R 的面具）低头独白，旁边飘着音符（MP3 / 耳机的意象），不要文字。
- orbit | X 命运之轮 | 一颗蓝色光球，外面一圈轨道光环，两边岩石 | 光球和轨道环保留，轨道上有钟表刻度。
- pale_blue_dot | XVII 星星 | 储君躲在后面，前面两块蓝色玻璃板，中间一个小白点 | 理在后面，前面两块蓝色玻璃板，中间一个小小的蓝白光点。
- parry | VIII 正义 | 储君在左边，一把剑从右上斜劈下来被挡开 | 理用单手剑格挡斜劈下来的一刀，交叉处碎玻璃飞溅。
- particle_wall | XI 力量 | 一只植物怪物 + 一堵蓝色方块墙 | 暗影 + 一堵蓝色玻璃方块墙。
- plot | XV 恶魔 | 储君和一个骷髅凑在一起看一团绿光 | 理和一个戴蓝面具的身影凑在一起看一团绿光（影时间的绿）。
- pillar_of_creation | XX 审判 | 储君抱着一根巨大的发光柱子 | 理旁边竖起一根巨大的蓝色发光柱子（像塔尔塔罗斯的方尖碑）。
- prophesize | II 女祭司 | 储君低头，双手捧着一颗橙色发光的星座球 | 理低头，双手捧着一颗橙色发光球，球里浮着一张塔罗牌。
- quasar | I 魔术师 | 两只手之间一道星云漩涡 | 理的两只手之间一道蓝色星云漩涡。
- radiate | XIX 太阳 | 中间一个小小的发光人形，四周放射状光 | 中间一个小小的发光人形（理的剪影），四周放射状金光保留，背景深蓝。
- reflect | XVIII 月亮 | 储君和一个发光的半透明分身对峙 | 理和一个发光的半透明分身（玻璃 / 水的倒影）对峙，像照镜子。
- resonance | VI 恋人 | 储君和一棵树状的东西，绿黄色漩涡 | 理闭眼，耳机里传出一圈圈黄绿色的音波漩涡。
- royal_gamble | X 命运之轮 | 下方一只手握着一把扇形展开的牌 | 理的手握着一把扇形展开的塔罗牌（牌背是 P3R 塔罗牌背），上方是储君的身影换成理的剪影。
- shining_strike | XIX 太阳 | 一道巨大的金色光击中一个敌人 | 金色光保留，击中暗影，暗影碎成黑色碎片。
- spectrum_shift | XIV 节制 | 一道彩虹色的光带从左下延伸到右上 | 彩虹色光带保留，但改成碎玻璃拼成的光带。
- stardust | XVII 星星 | 一只手往下洒出星尘 | 理的手往下洒出星尘（金色保留），落下的地方变成蓝色水面。
- summon_forth | VII 战车 | 储君跪着，一把剑从空中飞来 | 理单膝跪地，一把发光的剑从空中飞来，碎玻璃片在剑身周围。
- supermassive | XXI 世界 | 一只手的手指间一个发光的黄绿色环，绿色背景 | 理的手指间一个发光的黄绿色环（保留），背景换成影时间的墨绿。
- terraforming | III 女皇 | 储君捏着一张发光的星网 | 理的手指捏着一张发光的蛛网状星图。

**稀有**
- arsenal | VII 战车 | 储君在中间，四周飘着很多武器和盾牌 | 理在中间，四周飘着很多把剑和塔罗牌。
- beat_into_shape | XV 恶魔 | 储君举起手，手里一团发光的金属在被锤打 | 理举手，手里一团蓝色发光的金属，四周电光。
- big_bang | XXI 世界 | 两只手之间爆发出太阳、剑、卡片等一堆东西 | 理的两只手之间爆发出一颗太阳、一把剑、一张塔罗牌和星星。
- bombardment | XVI 塔 | 一颗巨大的火流星斜着冲下来 | 火流星保留（橙），尾迹是蓝色玻璃碎片，背景是塔尔塔罗斯。
- bundle_of_joy | III 女皇 | 一只大手捧着一块金色的石头 | 理的手捧着一块金色的发光宝石，四周蓝蝴蝶。
- comet | XVII 星星 | 一个绿色的敌人被一道蓝白色彗星打中 | 蓝白色彗星保留，打中暗影。
- crash_landing | XVI 塔 | 储君从天上砸下来，地面裂开 | 理从天上砸下来，地面碎成碎玻璃，冲击波青白色。
- decisions_decisions | X 命运之轮 | 储君俯身，手里抛着几颗金色的球 | 理俯身，手里抛着几张塔罗牌，四周金色光点（Shuffle Time 的意象）。
- dying_star | XIII 死神 | 一颗巨大的星在爆炸，碎石飞出 | 爆炸的星保留，碎石换成碎玻璃，暗影被冲散。
- foregone_conclusion | XX 审判 | 一张卷起来的羊皮纸，上面有绿色的星图 | 卷起来的纸上是绿色的星图 + 一个钟表。
- genesis | XX 审判 | 一只手从右上伸出，指尖碰到一颗大四角星 | 理的手指尖碰到一颗大四角星（像创造亚当的构图），背景深蓝星空。
- guards | VIII 正义 | 两个大大的守卫身影，中间一颗星 | 两个人格面具身影（Orpheus 和另一个）护在两边，中间一颗星。
- hammer_time | XV 恶魔 | 储君挥舞一把大锤 | 理挥舞一把大锤，锤头是蓝色玻璃质感。
- heavenly_drill | XVI 塔 | 储君推着一个巨大的钻头 | 理推着一个巨大的螺旋光钻（青白色）。
- heirloom_hammer | XV 恶魔 | 储君举起一把巨大的锤子，锤头里有蓝色发光的东西 | 理举起一把巨大的锤子，锤头里嵌着发光的蓝色玻璃。
- i_am_invincible | XI 力量 | 一个发光的蓝色网格人形，双臂展开 | 理的发光蓝色线框剪影，双臂展开，身后是 Orpheus 的剪影。
- make_it_so | I 魔术师 | 储君挥手，一群青色飞弹飞出去 | 理挥手，一群青色光弹 / 蝴蝶飞出去。
- monarchs_gaze | IV 皇帝 | 一个绿色的球状怪物，四周橙色闪光 | 一个戴蓝面具的暗影，被四周橙色的视线 / 闪光锁定。
- neutron_aegis | VIII 正义 | 储君被一圈原子轨道包围 | 理被一圈原子轨道包围，轨道是蓝白色。
- royalties | III 女皇 | 一只大手把金币撒出去 | 理的手把金币撒出去（金色保留），背景深蓝。
- seeking_edge | VIII 正义 | 一个倒挂的人形，一道红色光线射穿 | 一个倒挂的身影（理的剪影，像倒吊人），一道红色光线射穿。
- seven_stars | XVII 星星 | 一串星星连成线，打中几个敌人 | 北斗七星连成线，打中几个暗影。
- sword_sage | VII 战车 | 储君被一圈发光的剑围绕 | 理被一圈蓝色玻璃剑围绕。
- the_smith | XV 恶魔 | 储君背对镜头，在锻造台前举起锤子 | 理背对镜头，在锻造台前举起锤子，炉火是蓝色的。
- tutor | IX 隐者 | 储君坐在书桌前，头上一颗大星 | 理坐在书桌前，头上一颗大星（像月光馆学园的教室 / 自习）。
- tyranny | IV 皇帝 | 储君坐在王座上，黑暗背景 | 理坐在王座上，背景是影时间（墨绿色天空、诡异的大月亮）。
- void_form | XXI 世界 | 一个蓝色发光的人形，头部是一颗星，四周光环 | 理的蓝色发光剪影，头部是一颗星，四周光环。

**远古（竖版 606×852）**
- meteor_shower | XVII 星星 | 一个红色的人形站在中间，四周石块，蓝色光从天而降 | 理站在中间，四周石块，蓝色光从天而降（保留蓝色光柱）。
- the_sealed_throne | IV 皇帝 | 一块悬浮的石头王座在岩洞里 | 一块悬浮的王座，背景是塔尔塔罗斯的内部。

**其他（游戏里存在、不在卡池里的图）**
- reserves | II 女祭司 | 一个金色的宝箱 / 罐子被链条锁着，金光迸出 | 一个被链条锁着的蓝色天鹅绒箱子，金光迸出。
- royal_corona | IV 皇帝 | 一顶由星星连成的王冠 | 一顶由蓝色星光连成的王冠，悬在理的剪影头上。

【6. 要交付的文件】
（v2 重做第 1 批时，文件放在 <repo>\art\cards\v2\ 下，结构同下面 —— cand\、preview\batch1.png、README.md；不要覆盖 art\cards\ 下第 1 批 v1 的文件。v2 定稿后的批次直接放 art\cards\v2\。）
1. 每张卡：
   - 候选：<repo>\art\cards\cand\<名字>_a.png 和 <名字>_b.png；
   - 选定：<repo>\art\cards\<名字>.png（从 a / b 里挑一张复制过来，按【7. 自检】挑）。
2. 每批的预览：<repo>\art\cards\preview\batch<N>.png。每张卡一行：原版 | a | b，选中的那张加一圈细边框；左上角写卡名。
3. <repo>\art\cards\README.md（中文，追加，不要覆盖之前批次的内容）：每张卡写用的提示词全文、挑了哪张以及原因、已知问题。

【7. 自检】不满足就重做那一张，最多 3 轮；仍不满足就如实写进 README：
1. 能一眼认出是原版的哪张卡：主体、构图方向、动作和原版对得上（把新图和原版并排看）。
2. 能一眼看出是 P3R：有且只有一个主要的 P3R 元素；底色和原版同一色系（没有全部变蓝）。
6. 不花哨：背景干净，没有碎玻璃 / 粒子 / 光点 / 光晕 / 细碎高光；缩小到 250×190 看主体依然清楚。
3. 画到理时：头发遮一只眼、灰色眼睛、黑色外套白衬衫、红色耳机或臂章至少一样可见；手指数量正常、没有多余的手或肢体。
4. 没有文字、字母、数字、水印、边框、UI。
5. 尺寸正确、不透明、没有被拉伸变形。

【8. 分批】
- 第 1 批（试点，先做这一批，定画风；v1 已做完被否，现在按 v2 重做第 1 批）：strike_regent, defend_regent, falling_star, venerate, big_bang, black_hole, tyranny, the_smith
- 第 2 批：astral_pulse, celestial_might, cloak_of_stars, collision_course, cosmic_indifference, crescent_spear, crush_under, gather_light, glitterstream, glow, guiding_star, hidden_cache, know_thy_place, patter
- 第 3 批：photon_cut, refine_blade, solar_strike, spoils_of_battle, wrought_in_war, begone, alignment, bulwark, charge, child_of_the_stars, conqueror, constellation, convergence, devastate
- 第 4 批：furnace, gamma_blast, glimmer, hegemony, kingly_kick, kingly_punch, knockout_blow, largesse, lunar_blast, manifest_authority, monologue, orbit, pale_blue_dot, parry
- 第 5 批：particle_wall, plot, pillar_of_creation, prophesize, quasar, radiate, reflect, resonance, royal_gamble, shining_strike, spectrum_shift, stardust, summon_forth, supermassive
- 第 6 批：terraforming, arsenal, beat_into_shape, bombardment, bundle_of_joy, comet, crash_landing, decisions_decisions, dying_star, foregone_conclusion, genesis, guards, hammer_time, heavenly_drill
- 第 7 批：heirloom_hammer, i_am_invincible, make_it_so, monarchs_gaze, neutron_aegis, royalties, seeking_edge, seven_stars, sword_sage, tutor, void_form, meteor_shower, the_sealed_throne, reserves, royal_corona
- 第 2 批起：先打开 <repo>\art\cards\README.md 看第 1 批用户 / Claude 的反馈（如果有"画风调整"一节，按那一节改提示词），画风和第 1 批选定的图保持一致（把第 1 批选定的 2~3 张一起作为风格参考图）。

【9. 提交与推送】
1. 只 add 这个目录：
   git -c safe.directory=<repo> add art/cards/v2
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R-style card portraits v2, batch <N>"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：做了第几批、每张选了哪张、预览图路径、提交号、被拒绝或重做的情况。做完这一批就停下，不要自己接着做下一批。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件（README.md 只追加）。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就跳过那张、写进 README 并在报告里说明。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
