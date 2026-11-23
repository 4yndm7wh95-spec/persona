"""P5Joker 卡图生图提示词（91 张静默猎手卡）：生成 docs/codex/codex_p5_card_art_prompts.md + art/p5/cards/prompts.json。
思路见 docs/codex/codex_p5_card_art.md（照 P3 卡图第三 / 五轮：Image1 = 原版卡图，局部编辑，构图不变，把被替换的东西演绎成具体的 P5R 时刻 / 角色 / 物件）。
用法：python3 tools/p5/card_art_prompts.py"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

# 角色设定（每张提示词里只附用到的；参考图在 art/p5/cards/refs/<键名小写>/）
DESIGNS = {
    'JOKER': 'JOKER (Ren Amamiya, P5R protagonist): messy black curly hair, white domino mask with black edging, black high-collared long tailcoat over a grey vest, bright red gloves, dark trousers, brown boots; a short combat knife and a black pistol',
    'SKULL': 'SKULL (Ryuji Sakamoto): bleached blond spiky hair, white skull-shaped bone mask, black leather biker jacket with high collar, yellow gloves, red scarf; a metal pipe or a shotgun',
    'MONA': 'MONA (Morgana): a small cartoon cat-like mascot standing upright, oversized round black head with a black-and-white face pattern like a mask, big blue eyes, yellow neck scarf, utility belt; a curved scimitar or a slingshot',
    'MONA_BUS': 'MORGANA BUS: a rounded cartoon black-and-white minibus whose headlights are big cat eyes and whose grille is a cat mouth, little cat ears on the roof',
    'PANTHER': 'PANTHER (Ann Takamaki): blonde twin tails, glossy red full-body catsuit with cat ears and a tail, pink-red cat mask; a whip',
    'FOX': 'FOX (Yusuke Kitagawa): dark blue straight hair, white fox mask with red markings, black bodysuit with blue sash and fox tail; a katana',
    'QUEEN': 'QUEEN (Makoto Niijima): short brown bob, black metal riveted mask, black-and-silver biker suit with spiked shoulders; brass knuckle gauntlets',
    'ORACLE': 'ORACLE (Futaba Sakura): long orange hair, black-and-green bodysuit, green-lens goggles pushed up, headphones',
    'NOIR': 'NOIR (Haru Okumura): wavy short light-brown hair, wide black bandit hat with a feather, black mask, purple-black ruffled outfit; a giant axe and a grenade launcher',
    'CROW': 'CROW in his black mask outfit (Goro Akechi): black-and-dark striped fitted suit, full black helmet-like mask with red eye slits; a red beam sword',
    'VIOLET': 'VIOLET (Kasumi Yoshizawa): long red hair, red-and-black fencing-style outfit with a short cape, red butterfly-like mask; a rapier',
    'ARSENE': 'ARSENE (Joker\'s Persona): a tall dark demon gentleman with black wings, crimson-and-black tailored suit, a tall top hat, chains, glowing red-flame eyes',
    'CAPTAIN_KIDD': 'CAPTAIN KIDD (Skull\'s Persona): a skeleton pirate riding a ship-shaped body with cannons',
    'CARMEN': 'CARMEN (Panther\'s Persona): a flamenco woman in a red dress with a long cigar-like holder, leading two chained men',
    'GOEMON': 'GOEMON (Fox\'s Persona): a kabuki-faced figure in a giant ornate robe with a long pipe, ice element',
    'JOHANNA': 'JOHANNA (Queen\'s Persona): a sleek motorcycle Persona with a female figure fused into it',
    'NECRONOMICON': 'NECRONOMICON (Oracle\'s Persona): a round UFO-like Persona with tentacle arms, glowing green scanning lights, Oracle seated inside',
    'LOKI': 'LOKI (Crow\'s Persona): a tall dark jester-like figure with a long jagged blade',
    'SATANAEL': 'SATANAEL: a gigantic winged dark god with a halo of blades, holding a huge gun',
    'CAROLINE_JUSTINE': 'CAROLINE and JUSTINE: identical small twin wardens with platinum hair in blue Velvet Room warden uniforms and eyepatches (Caroline\'s on the right eye with a baton, Justine\'s on the left with a clipboard)',
    'IGOR': 'IGOR: an old man with an extremely long nose, bloodshot eyes, wide grin, black suit, white gloves',
    'SOJIRO': 'SOJIRO SAKURA: middle-aged man with a short goatee, glasses, a pink shirt and a cap',
    'TAKEMI': 'TAE TAKEMI: a punk doctor with a short dark blue-black bob, black choker, black dress under a white lab coat',
    'IWAI': 'MUNEHISA IWAI: a stern man with a flat cap, a shark tattoo on his neck and a lollipop',
    'SHADOW': 'SHADOWS (P5R enemies): black sludge-like bodies wearing white oval masks with narrow eye slits, or the masked monster forms they burst into',
    'REAPER': 'THE REAPER (Mementos): a tall hooded figure wrapped in chains, holding two long revolvers',
}

# 卡（蛇形名, 中文名, 创意（中文，给用户看）, 改法（英文）, 用到的角色）
CARDS = [
    # 2026-11-23 对着导出的原版卡图（research/silent_assets/card_portraits）逐张写：哪个元素换成什么，其余照原图
    ('abrasive', '磨蚀', 'Fox 握着结满 Goemon 冰刺的手臂', 'Replace the hooded Silent with FOX in the same place and pose (his white fox mask facing the same way). The spiky thorn-covered arm she holds becomes FOX\'s arm encased in jagged pale-blue ice spikes (GOEMON\'s ice), same shape, size and angle; the glowing yellow-green of the thorns becomes icy cyan-white. Keep the teal background and the braid shapes as dark rope.', ['FOX', 'GOEMON']),
    ('accelerant', '触媒', '武见的滴管往紫药瓶里滴一滴', 'Replace the green figure and its hands with TAKEMI\'s hands and white lab-coat sleeves (black nail polish) holding the same dropper over the same purple flask; the drop and the flask stay. The green-hooded body in the background becomes the blurred white coat and her dark bob, face cropped out of frame above.', ['TAKEMI']),
    ('accuracy', '精准', '小刀正中靶心，靶上贴着预告信', 'Keep the knives, target and red background. The knives become JOKER\'s combat knives (dark grip, short silver blade); the bullseye target gets a red-and-black calling card pinned at its center where the knife hits. No characters.', []),
    ('acrobatics', '杂技', 'Panther 甩鞭荡过半空', 'Replace the Silent with PANTHER in the same mid-air acrobatic pose and place; the blades in her hands become her whip unfurling in a long arc; the magenta streaks stay.', ['PANTHER']),
    ('adrenaline', '肾上腺素', '心电波形变成「1 MORE」的红闪', 'Keep the jagged waveform and its shape, but recolor the line to P5 red and black with a white core, and put a small close-up of JOKER\'s eyes behind his white mask glinting in the dark gap below the peaks. Background becomes deep red-black instead of blue-purple. No text.', ['JOKER']),
    ('afterimage', '余像', 'Joker 冲刺，身后一串红色残影', 'Replace the Silent with JOKER in the same lunging pose, coat tails flying; the chain of yellow-green afterimages trailing behind becomes red-tinted JOKER afterimages. Keep the slanted streak background but shift it to red and black.', ['JOKER']),
    ('anticipate', '预判', 'Oracle 双手捧着 Necronomicon 的扫描光球', 'The two hands become ORACLE\'s gloved hands (black-and-green suit cuffs); the swirling orange orb becomes a green holographic scanning sphere with rotating rings and a red target mark inside, like Necronomicon\'s analysis. Keep the purple background.', ['ORACLE', 'NECRONOMICON']),
    ('assassinate', '刺杀', '小刀刺进暗影的面具', 'Replace the hand with JOKER\'s red-gloved hand gripping his combat knife; the round purple-orange target becomes the white oval mask of a SHADOW (black sludge body behind it) cracking as the knife pierces it. Keep the blue background and the leafy hand above as a dark shadow claw.', ['JOKER']),
    ('backflip', '后空翻', 'Joker 后空翻，大衣翻开', 'Replace the Silent with JOKER doing the same backflip, long black tailcoat flaring open, red gloves; keep the orange-yellow light rays and red background.', ['JOKER']),
    ('backstab', '背刺', 'Joker 从背后把刀藏在身后', 'Replace the hooded Silent with JOKER seen from behind in the same position, his red-gloved hand holding the glinting knife behind his back; the chain becomes the hem of his coat. Keep the dark green and red background.', ['JOKER']),
    ('blade_of_ink', '墨之刃', 'Fox 的毛笔画出的墨刀', 'Keep the dripping ink knife and its shape and the orange sunset; add FOX\'s hand from the right edge holding a large calligraphy brush that is just finishing the blade stroke. The blade drips black ink as now.', ['FOX']),
    ('blade_dance', '刀刃之舞', 'Violet 旋转，细剑划出黄色弧光', 'Replace the Silent with VIOLET spinning in the same place, her rapier leading the yellow crescent slashes; keep the purple background and yellow arcs.', ['VIOLET']),
    ('blade_symphony', '刀刃交响曲', '怪盗团并肩一排，最前面的是 Joker', 'The three figures standing in a row become (front to back) JOKER, SKULL and MONA, each raising their weapon (knife, pipe, scimitar); keep the green background, sparkle and layout.', ['JOKER', 'SKULL', 'MONA']),
    ('blur', '残影', 'Joker 疾冲，身后青色残影', 'Replace the dashing Silent with JOKER in the same dash pose; the cyan blur trail becomes a blue-white afterimage of his coat. Keep the orange light beams.', ['JOKER']),
    ('bouncing_flask', '弹跳药瓶', 'Mona 扔出的药瓶砸在暗影头上', 'The creature with tentacle hair becomes a SHADOW (black sludge with a white oval mask, dark tendrils); the flask stays and becomes a small glass potion bottle with a cat-face label. Keep the purple background and green splash.', []),
    ('bubble_bubble', '咕嘟冒泡', '卢布朗的咖啡杯里冒出毒泡', 'The green crystal and orbs become a white Leblanc coffee cup tipped over, spilling glowing green bubbling poison and green bubble orbs. Keep the dark purple background and layout. No characters.', []),
    ('bullet_time', '子弹时间', 'Joker 举手，子弹悬停在空中', 'Replace the orange figure with JOKER in the same pose, one red-gloved palm raised, the bullets frozen in mid-air around him exactly as now. Keep the purple background.', ['JOKER']),
    ('burst', '爆发', 'Skull 的侧脸，眼里一道黄光', 'Replace the hooded Silent\'s head with SKULL\'s profile in the same place and angle (white skull mask, blond spiky hair), the yellow-green glint in his eye; the horn becomes his raised jacket collar. Keep the teal background and motion streaks.', ['SKULL']),
    ('calculated_gamble', '计算下注', '赌场的骰子（冴的宫殿）', 'Keep the two dice and composition; the dice become casino dice in P5 red-and-black with white pips, and a few casino chips scatter behind them. Background becomes the blue-red of a casino carpet. No characters.', []),
    ('cloak_and_dagger', '斗篷与匕首', 'Joker 的红手套从大衣里抽出小刀', 'The gauntleted hand becomes JOKER\'s red-gloved hand pulling his knife out from inside his black tailcoat; the cloak becomes the coat lining. Keep the blue-green background.', ['JOKER']),
    ('corrosive_wave', '腐蚀波', 'Mementos 的红黑浪潮冲过暗影', 'Keep the wave shape; the yellow-green poison wave becomes a black-red cognitive tide, and the small creatures riding it become small SHADOWS with white masks. Keep the orange-red background.', []),
    ('concoct', '调制', '怪盗团的手往一锅药里加料', 'Keep the cauldron of green bubbling liquid; the hands become JOKER\'s red-gloved hand and MONA\'s paw dropping ingredients in. Keep the purple background.', ['JOKER', 'MONA']),
    ('dagger_spray', '匕首雨', '一片小刀扎向暗影', 'Keep the spray of daggers; the daggers become JOKER\'s combat knives; the mushroom creature becomes a SHADOW (black sludge, white mask). Keep the red-orange background.', []),
    ('dagger_throw', '投掷匕首', 'Joker 的红手套甩出小刀', 'Replace the hand with JOKER\'s red-gloved hand and black coat sleeve in the same throwing pose; the knife and its pink glint stay. Keep the magenta background.', ['JOKER']),
    ('dash', '冲刺', '摩尔加纳巴士冲出去', 'The boot dashing forward becomes the MORGANA BUS (front wheel and cat-eye headlight) speeding forward in the same direction and footprint, with the same yellow lightning streaks. Keep the green background.', ['MONA_BUS']),
    ('deadly_poison', '致命毒药', '药瓶上方冒出 Arsène 的咒怨骷髅', 'Keep the bottle and the glowing skull emblem; the skull becomes ARSENE\'s face (dark demon, top hat brim, red-flame eyes) formed from the poison cloud. Keep the yellow-green glow and purple background.', ['ARSENE']),
    ('defend_silent', '防御', 'Queen 双拳交叉格挡', 'Replace the hooded Silent with QUEEN in the same place, forearms crossed with brass-knuckle gauntlets blocking the lightning strikes. Keep the blue background and white lightning.', ['QUEEN']),
    ('deflect', '偏折', 'Noir 的手握斧柄弹开攻击', 'Replace the two hands with NOIR\'s gloved hands (purple-black frilled cuffs) gripping two axe handles crossed, the white glints deflecting. Keep the blue background and yellow beams.', ['NOIR']),
    ('dodge_and_roll', '闪躲翻滚', 'Joker 翻滚躲开', 'Replace the rolling Silent with JOKER in the same rolling pose, black coat swirling; keep the red-orange background and the flame arc.', ['JOKER']),
    ('echoing_slash', '回响斩击', '总攻击：交叉的刀斩碎一群暗影', 'Keep the crossed arms and the slash ring; the arms become JOKER\'s black coat sleeves and red gloves; the small creatures being sliced become small SHADOWS with white masks. Keep the red-yellow background.', ['JOKER']),
    ('envenom', '涂毒', '刀身上一滴毒，毒滴里是暗影面具', 'Keep the knife and the drop; the knife becomes JOKER\'s combat knife; the skull inside the poison drop becomes a white SHADOW mask. Keep the dark purple background.', []),
    ('escape_plan', '逃脱计划', 'Joker 的手指按下迷宫里的出口', 'The hand becomes JOKER\'s red-gloved hand pressing the glowing exit; the maze below becomes a palace floor plan in cyan lines. Keep the blue background.', ['JOKER']),
    ('expertise', '独门技术', 'Oracle 的电脑和工具', 'The pouch and tools become ORACLE\'s gear: a rugged laptop with a green screen glow, her headphones and cables. Keep the purple background. No characters.', []),
    ('expose', '暴露', '放大镜照出暗影的原形', 'Keep the magnifying glass and hand; the hand becomes MONA\'s paw (white glove) holding it; the creature seen through the lens becomes a SHADOW whose mask is cracked, revealing glowing eyes. Keep the green-yellow background.', ['MONA']),
    ('fade', '消影', 'Mona 在草丛里潜伏，只露眼睛', 'Replace the hooded Silent hiding in the leaves with MONA crouched in the same place, only his big blue eyes and mask-like face visible. Keep the green leaves background.', ['MONA']),
    ('fan_of_knives', '刀扇', 'Joker 的红手套握着一扇小刀', 'Replace the hand with JOKER\'s red-gloved hand holding the same fan of knives; keep the red background and orange outline.', ['JOKER']),
    ('finisher', '终结技', 'Joker 冷笑着亮出刀', 'Replace the hooded Silent with JOKER in the same place and angle (white mask, smirk), holding the glinting knife. Keep the red background and white smoke curl.', ['JOKER']),
    ('flanking', '夹击', '暗影被吊起，Skull 和 Mona 一左一右', 'Keep the hanging round creature but make it a SHADOW with a white mask; the hooded shapes on both sides become SKULL (left) and MONA (right) in silhouette, weapons raised. Keep the purple background.', ['SKULL', 'MONA']),
    ('flechettes', '飞镖', 'Mona 的弹弓石子', 'Keep the composition; the darts become slingshot pellets with motion streaks and the large dart becomes MONA\'s slingshot drawn back. Keep the red-orange background.', ['MONA']),
    ('flick_flack', '翻越撑击', 'Panther 一脚踩下', 'Replace the Silent with PANTHER in the same pose, stomping down on the creature below (make it a SHADOW with a white mask). Keep the purple background and flame burst.', ['PANTHER']),
    ('sidestep', '侧步', 'Joker 侧身，靴子滑过地面', 'Replace the legs with JOKER\'s legs and black coat tail in the same sidestep; keep the green background and glow on the ground.', ['JOKER']),
    ('footwork', '灵动步法', 'Violet 的轻快步伐', 'Replace the bandaged legs with VIOLET\'s legs in red-and-black tights and boots, in the same light gymnastic step. Keep the blue background.', ['VIOLET']),
    ('grand_finale', '华丽收场', '撒旦耶尔立于红色花瓣中', 'Replace the robed figure with SATANAEL in the same central pose, arms spread, gun in hand; keep the falling red petals and the piled bodies below as fallen SHADOWS. Keep the red background.', ['SATANAEL']),
    ('hand_trick', '手上技法', 'Joker 指间夹着预告信', 'The hand becomes JOKER\'s red-gloved hand; the shuriken becomes a red-and-black calling card flicked between his fingers. Keep the blue background and sparkle.', ['JOKER']),
    ('haze', '迷雾', '暗影在青绿色迷雾里伸出手', 'Keep the swirling haze; the hooded figure becomes a SHADOW reaching out of the cyan-green mist (white mask, black body). Keep the colors.', []),
    ('hidden_daggers', '隐秘匕首', 'Joker 从大衣里亮出小刀', 'The hand and sleeve become JOKER\'s red glove and black coat sleeve pulling a knife from his coat; keep the yellow-orange background and the glint.', ['JOKER']),
    ('infinite_blades', '无尽刀刃', '岩井店里挂满的刀', 'Keep the composition; the rows of blades behind become knives and model guns hanging on the wall of Iwai\'s shop; the hand becomes IWAI\'s hand (rolled sleeve) taking one down. Keep the dark background.', ['IWAI']),
    ('knife_trap', '刀刃陷阱', '暗影被刀刃陷阱夹住', 'Keep the spiked trap; the blue blob creature caught in it becomes a SHADOW (black body, white mask). Keep the red-orange background.', []),
    ('leading_strike', '先制打击', 'Joker 甩出小刀抢先一击', 'The hand becomes JOKER\'s red-gloved hand and black sleeve releasing the knife; keep the red-orange background and the swirl.', ['JOKER']),
    ('leg_sweep', '扫腿', 'Queen 低身扫腿', 'Replace the Silent with QUEEN in the same low sweeping kick; keep the teal-yellow background and the sweep arc.', ['QUEEN']),
    ('malaise', '萎靡', '两只手压住一只缩起来的暗影', 'The two hands become JOKER\'s red gloves pressing down; the creature becomes a small SHADOW shrinking (white mask, black body). Keep the red background and cyan splash.', ['JOKER']),
    ('master_planner', '谋划专家', '怪盗团的作战装备一字排开', 'Keep the grid layout of items; the items become Phantom Thieves gear: a red-and-black calling card, lockpicks, a smoke bomb, a SP Adhesive bottle, a medicine bottle, a grappling hook, JOKER\'s knife and mask. Keep the purple background. No characters.', []),
    ('memento_mori', '铭记死亡', '死神从漩涡里伸出枪', 'The creature on the right becomes THE REAPER emerging from the swirl, revolver pointed; the red flower creature becomes a cluster of SHADOW masks. Keep the purple background.', ['REAPER']),
    ('mirage', '蜃景', 'Arsène 的手从光里伸出来', 'The figure becomes ARSENE reaching out of the yellow beam in the same pose (dark wing, red-flame eye). Keep the orange background and beam.', ['ARSENE']),
    ('murder', '谋杀', '黑面具 Crow 的手握刀', 'The black arm becomes CROW\'s black gloved arm (black mask outfit) gripping the same jagged blade, now with a faint red glow. Keep the red background.', ['CROW']),
    ('neutralize', '中和', '天平变成宫殿的宝物', 'Keep the golden scales and composition (golden scales are a palace treasure). No characters.', []),
    ('nightmare', '夜魇', '天鹅绒房间的手伸向一团绿色的梦魇', 'Keep the green blob and the clawed hands; the hands become black gloved hands in blue Velvet Room warden cuffs (CAROLINE and JUSTINE\'s sleeves). Keep the red background.', ['CAROLINE_JUSTINE']),
    ('noxious_fumes', '毒雾', '暗影在毒雾里戴着防毒面具', 'Keep the yellow-green fumes; the masked creature becomes a SHADOW whose white mask is shaped like a gas mask. Keep the colors.', []),
    ('outbreak', '毒性爆发', 'Arsène 的咒火从 Joker 身上炸开', 'The figure in the center becomes JOKER with ARSENE\'s dark shape bursting behind him; the magenta flames stay. Keep the teal background.', ['JOKER', 'ARSENE']),
    ('phantom_blades', '幻影之刃', '蓝色幻影小刀', 'Keep the blades; the blades become JOKER\'s combat knives in ghostly blue; the dragon shape becomes ARSENE\'s wing silhouette. Keep the red background.', ['ARSENE']),
    ('piercing_wail', '尖啸', 'Panther 的 Carmen 张嘴尖啸', 'Replace the Silent with CARMEN in the same place and angle, mouth open screaming, the orange shockwave staying. Keep the purple background.', ['CARMEN']),
    ('pinpoint', '精密瞄准', 'Noir 端着榴弹枪瞄准', 'The hand and slingshot become NOIR\'s gloved hand holding her grenade launcher aimed in the same direction; keep the red background.', ['NOIR']),
    ('poisoned_stab', '带毒刺击', '小刀沾着绿毒刺出', 'Keep the knife and the green poison; the knife becomes JOKER\'s combat knife. Keep the red background. No characters.', []),
    ('pounce', '猛扑', 'Mona 扑向张开大嘴的暗影', 'Replace the leaping Silent with MONA pouncing with his scimitar; the shark-mouthed creature becomes a SHADOW monster with a white mask over its jaws. Keep the teal background and orange arc.', ['MONA']),
    ('precise_cut', '精确切击', 'Fox 的居合斩切开暗影的爪', 'Keep the clawed gauntlet being sliced; the gauntlet becomes a SHADOW\'s claw with a white mask fragment; the slash becomes FOX\'s katana stroke (thin white-blue line). Keep the red background.', ['FOX']),
    ('predator', '猎杀者', 'Joker 握刀，眼神锁定猎物', 'Replace the hooded Silent with JOKER in the same place, gripping the glowing knife; keep the dark red background and floating shards.', ['JOKER']),
    ('prepared', '早有准备', 'Joker 握住刀柄准备出手', 'The hands become JOKER\'s red-gloved hands gripping the knife grip; keep the magenta background.', ['JOKER']),
    ('reflex', '本能反应', 'Joker 的第三只眼看穿暗影的攻击', 'Replace the hooded Silent with JOKER in the same place; the orange orb creature becomes a SHADOW; the cyan lightning stays. Keep the purple background.', ['JOKER']),
    ('ricochet', '连续反弹', '子弹在宫殿拱门间来回反弹', 'Keep the arch and the zigzag ricochet lines; the arch becomes a palace archway (dark stone with red ornaments). Keep the red background. No characters.', []),
    ('serpent_form', '群蛇形态', '群蛇缠绕的 Joker', 'Replace the hooded figure in the center with JOKER in the same place; the snakes coil around him as now. Keep the green background.', ['JOKER']),
    ('shadow_step', '暗影步', 'Joker 从暗影中跃出', 'Replace the leaping Silent with JOKER in the same pose; the purple smoke trail stays. Keep the purple background.', ['JOKER']),
    ('shadowmeld', '融入暗影', '黑暗里只剩 Joker 的白面具', 'Replace the hooded Silent with JOKER in the same place, his body sinking into darkness, only his white mask and red gloves standing out; the green glow becomes red. Keep the dark background.', ['JOKER']),
    ('skewer', '串刺', 'Violet 的细剑刺穿暗影', 'Keep the spear and the impaled creature; the spear becomes VIOLET\'s rapier; the creature becomes a SHADOW with a white mask. Keep the red background.', ['VIOLET']),
    ('slice', '切割', '交叉斩切开暗影', 'Keep the X slash; the sliced creatures become SHADOWS with white masks. Keep the red background. No characters.', []),
    ('snakebite', '蛇咬', '红蛇张嘴，后面是 Joker 的剪影', 'Keep the red snake head; the hooded figure behind becomes JOKER (blurred, as now). Keep the green background.', ['JOKER']),
    ('sneaky', '鬼祟', 'Joker 贴着墙溜过去', 'Replace the hooded Silent with JOKER sneaking in the same pose; keep the dark green background and the yellow flash.', ['JOKER']),
    ('speedster', '速行者', 'Joker 从高处一跃而下', 'Replace the leaping figure with JOKER in the same pose, coat flying, debris falling. Keep the yellow-green background.', ['JOKER']),
    ('storm_of_steel', '钢铁风暴', '刀刃风暴卷成旋涡', 'Keep the storm; the blades become JOKER\'s combat knives spinning in a tornado. Keep the red background. No characters.', []),
    ('strangle', '紧勒', 'Panther 的鞭子勒住暗影', 'Keep the composition; the vines become PANTHER\'s whip coiled around a SHADOW mask in the center; the green hands become PANTHER\'s gloved hands. Keep the red background.', ['PANTHER']),
    ('strike_silent', '打击', 'Joker 的小刀', 'Keep the knife and composition; the knife becomes JOKER\'s combat knife (dark grip, short silver blade). Keep the red background. No characters.', []),
    ('sucker_punch', '突然一拳', 'Skull 一拳打在暗影脸上', 'The fist becomes SKULL\'s yellow-gloved fist; the creature becomes a SHADOW with a white mask taking the punch. Keep the red background and cyan arrow.', ['SKULL']),
    ('suppress', '压制', 'Queen 骑着 Johanna 撞过来', 'Replace the robed figure with QUEEN riding JOHANNA in the same place and direction; the thrown basket becomes a flying SHADOW. Keep the red background.', ['QUEEN', 'JOHANNA']),
    ('survivor', '生存者', 'Joker 的红手套握拳', 'The fist becomes JOKER\'s red-gloved fist with the black coat sleeve; keep the blue background and cyan lightning.', ['JOKER']),
    ('tactician', '战术大师', '地图上的棋子', 'Keep the map and pieces; the pieces become small chess-like pieces shaped like the Phantom Thieves\' masks. Keep the colors. No characters.', []),
    ('the_hunt', '狩猎', '暗影面具下滴着毒液', 'Keep the composition; the skull face becomes a SHADOW\'s white mask with green poison dripping; the hand becomes JOKER\'s red glove pressing it. Keep the red background.', ['JOKER']),
    ('tools_of_the_trade', '必备工具', '怪盗的工具散在空中', 'Keep the composition of scattered tools; the tools become Phantom Thieves tools: JOKER\'s knife, lockpicks, a grappling hook, calling cards. Keep the blue background. No characters.', []),
    ('tracking', '跟踪', 'Mona 的猫爪印', 'Keep the hand and the glowing paw print; the hand becomes JOKER\'s red-gloved hand; the paw print becomes a cat paw print (MONA\'s). Keep the blue background.', ['JOKER']),
    ('untouchable', '触不可及', 'Joker 闪身躲开飞来的攻击', 'Replace the Silent with JOKER in the same leaning-away pose, the arrows passing by. Keep the purple background and orange beam.', ['JOKER']),
    ('up_my_sleeve', '袖里乾坤', '红手套袖口滑出小刀', 'The hand and sleeve become JOKER\'s red glove and black coat sleeve, the knives sliding out of the cuff. Keep the yellow background.', ['JOKER']),
    ('well_laid_plans', '计划妥当', '羽毛笔写下预告信的签名', 'Keep the quill and hand; the hand becomes JOKER\'s red-gloved hand; the glowing cyan mark becomes the Phantom Thieves\' top-hat logo traced in red light. Keep the blue background.', ['JOKER']),
    ('wraith_form', '幽魂形态', 'Arsène 的幽影在绿光里展开', 'Replace the ghostly figure with ARSENE in the same place, wings spread, translucent and wreathed in the green glow. Keep the green background.', ['ARSENE']),
]


# 角色 → 参考图文件夹（同 docs/codex/codex_p5_card_art.md 第一步的名单）
REF_DIRS = {'MONA_BUS': ['morgana_bus'], 'CAROLINE_JUSTINE': ['caroline', 'justine'], 'REAPER': ['shadows']}

COMMON = ('Use case: precise-object-edit. Image1 is the ORIGINAL Slay the Spire 2 card illustration (a Silent card) and the strict EDIT TARGET. '
          'Reinterpret the replaced subject as a specific Persona 5 Royal moment, character or object, while the card stays instantly recognizable at thumbnail size: '
          'same silhouettes, same action, same footprint, same camera. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, effects and core objects stay as in Image1 unless this prompt names them. '
          'Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything beyond what is stated. '
          'Wherever the Silent (the green-hooded huntress) appears, remove her completely with no remnants (no green hood, skull-like face, green cloak or pouches left) and refill the cleared area with the original background shapes, unless this prompt replaces her with someone. '
          'If the original composition cannot fit the idea literally, keep the composition and adapt the idea to it. '
          'Every named character MUST match the attached reference images exactly: face shape, eyes, hairstyle and hair color, mask, outfit, weapon; do not invent features or change their look; if a detail is unclear, keep it simple rather than guess. '
          'Match the original\'s rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness; no blur, no soft glow, no photoreal rendering. '
          'A SHADOW (P5R enemy, no reference image) is drawn as: a hunched black sludge-like body with drippy edges and a smooth white oval mask with two narrow slanted eye slits; keep it simple and consistent. '
          'Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, the same size as Image1, no text, letters, numbers, frame, UI or watermark.')


def main():
    out, items = [], []
    out.append('# P5Joker 卡图提示词（91 张）\n')
    out.append('思路和创意总表见 `docs/codex/codex_p5_card_art.md`；这份由 `tools/p5/card_art_prompts.py` 生成（改了脚本再跑一次），机器可读版 `art/p5/cards/prompts.json`。\n')
    out.append('## 给 Codex 的执行方式\n')
    out.append('- **先做完 `docs/codex/codex_p5_card_art.md` 的第一步**（导出原版卡图 + 收集人物参考图）；这一步的素材是下面每张的 Image1 和参考图。')
    out.append('- 每批 5 张，生成后停下等用户审；**不要自动重试、不要自己判断通过**（用户审）。')
    out.append('- 输入：Image1 = `research/silent_assets/card_portraits/<卡名>.png`（原版）；参考图 = 每张列出的角色在 `art/p5/cards/refs/<角色>/` 里的图，全部附上。参考图缺的角色（`art/p5/cards/refs/MISSING.md` 里有的）那张先跳过，写进批次的说明。')
    out.append('- 输出：和原版同尺寸的不透明 PNG，放 `art/p5/cards/round1/batch_NN/<卡名>.png`，另存 `comparison.png`（原版 | 新图）和 `prompts_and_sources.json`（提示词、输入图路径），状态 `awaiting_user_review`。')
    out.append('- 只 add `art/p5/cards/round1`，提交推送到 `claude/confirm-next-tasks-k5wu6t`；不改代码、不接入游戏。')
    out.append('- 规矩同其他任务单：不读取或输出 C:\\Users\\kkk\\.p3record\\obs.json 和任何密码 / 密钥；不 rebase / reset / amend / stash / --force；不改全局 git config、环境变量；不安装任何东西；不碰游戏目录。\n')
    for i, (key, zh, idea, edit, chars) in enumerate(CARDS):
        if i % 5 == 0:
            out.append(f'## 第 {i // 5 + 1} 批\n')
        designs = ' '.join(f'[{c}] = {DESIGNS[c]}.' for c in chars)
        prompt = COMMON + ' ' + edit + (' Character designs (follow the attached references): ' + designs if designs else '')
        folders = []
        for c in chars:
            for f in REF_DIRS.get(c, [c.lower()]):
                if f not in folders: folders.append(f)
        refs = [f'art/p5/cards/refs/{f}/' for f in folders]
        out.append(f'### {i + 1}. `{key}` {zh}\n')
        out.append(f'- 创意：{idea}')
        out.append(f'- Image1（原版）：`research/silent_assets/card_portraits/{key}.png`')
        out.append(f'- 参考图：' + ('、'.join(f'`{r}`' for r in refs) if refs else '无（没有人物）'))
        out.append(f'\n```\n{prompt}\n```\n')
        items.append({'card': key, 'zh': zh, 'idea': idea, 'image1': f'research/silent_assets/card_portraits/{key}.png', 'refs': refs, 'batch': i // 5 + 1, 'prompt': prompt})
    (ROOT / 'docs/codex/codex_p5_card_art_prompts.md').write_text('\n'.join(out), encoding='utf-8')
    p = ROOT / 'art/p5/cards'; p.mkdir(parents=True, exist_ok=True)
    (p / 'prompts.json').write_text(json.dumps(items, ensure_ascii=False, indent=1), encoding='utf-8')
    print(len(items), 'prompts')


if __name__ == '__main__':
    main()
