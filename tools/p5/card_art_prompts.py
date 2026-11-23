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
    ('abrasive', '磨蚀', '冰棱荆棘：身边结出一圈 Goemon 的冰刺，刺向扑来的暗影', 'Replace the Silent with FOX in the same pose and place; the thorny or spiky elements around her become sharp ice spikes of GOEMON\'s ice, pointing outward at SHADOWS.', ['FOX', 'GOEMON', 'SHADOW']),
    ('accelerant', '触媒', '武见诊所的试药：一滴紫色药剂滴进烧瓶，毒一下沸腾', 'The poison / bottle element becomes a lab flask at Takemi\'s clinic; TAKEMI\'s gloved hand (sleeve of her white lab coat) drips a purple drop into it and the liquid boils violently. If the Silent appears, replace her with TAKEMI\'s hand and coat sleeve only.', ['TAKEMI']),
    ('accuracy', '精准', '小刀一口气穿过三张扑克牌，正中暗影面具的眉心', 'Replace the Silent with JOKER; the projectile or blade becomes his combat knife flying through three playing cards in a line, striking the forehead of a SHADOW mask.', ['JOKER', 'SHADOW']),
    ('acrobatics', '杂技', 'Panther 甩鞭荡过宫殿的吊灯', 'Replace the Silent with PANTHER in the same acrobatic pose, her whip wrapped around a palace chandelier as she swings.', ['PANTHER']),
    ('adrenaline', '肾上腺素', '「1 MORE」的眼神特写：Joker 的眼睛一亮', 'The central subject becomes an extreme close-up of JOKER\'s eyes behind the white mask, one eye flashing with a sharp glint, on a bold red background streak (the P5 "1 More" cut-in feel). No text.', ['JOKER']),
    ('afterimage', '余像', '怪盗步：Joker 在宫殿走廊里连闪，身后一串残影', 'Replace the Silent with JOKER in the same pose; the afterimages become a trail of red-tinted JOKER silhouettes along a palace corridor.', ['JOKER']),
    ('anticipate', '预判', 'Necronomicon 的扫描光圈罩住敌人，Oracle 提前读出动作', 'Replace the Silent with ORACLE inside NECRONOMICON; the foresight / eye element becomes a green scanning ring projected onto a SHADOW, outlining its next move.', ['ORACLE', 'NECRONOMICON', 'SHADOW']),
    ('assassinate', '刺杀', '伏击：Joker 从掩体后跃出，刀尖落向暗影后颈', 'Replace the Silent with JOKER leaping from cover in the same pose, knife pointed down at the back of a SHADOW\'s neck.', ['JOKER', 'SHADOW']),
    ('backflip', '后空翻', 'Joker 从高处后空翻落下，大衣下摆翻开', 'Replace the Silent with JOKER doing the same backflip, his long black tailcoat flaring open.', ['JOKER']),
    ('backstab', '背刺', '「撕面具」：从背后一把扯下暗影的白面具', 'Replace the Silent with JOKER behind the enemy; the stab becomes him ripping the white mask off a SHADOW from behind, red gloves gripping the mask.', ['JOKER', 'SHADOW']),
    ('blade_of_ink', '墨之刃', 'Fox 用画笔在空中画出一把墨色的刀，刀刃还在滴墨', 'Replace the Silent with FOX; he paints a knife in the air with a large brush, the ink blade still dripping (Madarame\'s gallery feel).', ['FOX']),
    ('blade_dance', '刀刃之舞', 'Violet 握细剑旋转，划出一圈刀光', 'Replace the Silent with VIOLET spinning like a gymnast with her rapier; the blades in the image become rapier slashes circling her.', ['VIOLET']),
    ('blade_symphony', '刀刃交响曲', '怪盗团每人举着自己的武器朝中间一指，刀光交在一起', 'The blades become the Phantom Thieves\' weapons (JOKER\'s knife, SKULL\'s pipe, PANTHER\'s whip, FOX\'s katana, QUEEN\'s fist) crossing at the center; figures as dark silhouettes or hands only.', ['JOKER', 'SKULL', 'PANTHER', 'FOX', 'QUEEN']),
    ('blur', '残影', 'Violet 体操翻转留下的红色残影', 'Replace the Silent with VIOLET in a gymnastic flip; the blur becomes red afterimages of her.', ['VIOLET']),
    ('bouncing_flask', '弹跳药瓶', 'Mona 扔出药瓶，在暗影之间弹来弹去', 'Replace the Silent with MONA throwing; the flask bounces between SHADOWS.', ['MONA', 'SHADOW']),
    ('bubble_bubble', '咕嘟冒泡', '卢布朗的虹吸咖啡壶咕嘟冒泡，咖啡变成毒绿色', 'The bubbling vessel becomes Leblanc\'s glass siphon coffee maker bubbling over, the coffee turned toxic green; SOJIRO\'s hand on the burner knob if a figure is needed.', ['SOJIRO']),
    ('bullet_time', '子弹时间', '「Hold Up」：时间停住，Joker 举枪，子弹悬在半空', 'Replace the Silent with JOKER aiming his pistol; bullets hang frozen in mid-air in front of the muzzle, time stopped.', ['JOKER']),
    ('burst', '爆发', 'Skull 的 Captain Kidd 船炮齐射', 'Replace the burst source with CAPTAIN KIDD firing a cannon volley, SKULL riding on it.', ['SKULL', 'CAPTAIN_KIDD']),
    ('calculated_gamble', '计算下注', '冴的赌场宫殿：轮盘飞转，Queen 押下最后一枚筹码', 'Replace the Silent with QUEEN; the gamble becomes a spinning casino roulette and she slams down a chip (Sae\'s casino palace).', ['QUEEN']),
    ('cloak_and_dagger', '斗篷与匕首', '大衣一甩，衣摆后藏着一排小刀', 'Replace the Silent with JOKER; her cloak becomes his black tailcoat swept aside, revealing a row of knives tucked inside.', ['JOKER']),
    ('corrosive_wave', '腐蚀波', 'Mementos 深处涌来的暗红认知浪潮', 'The wave becomes a dark crimson cognitive wave surging through a Mementos tunnel, SHADOW masks floating in it.', ['SHADOW']),
    ('concoct', '调制', '卢布朗吧台：Joker 冲咖啡，杯里倒出的是发光的毒药', 'Replace the Silent with JOKER behind the Leblanc counter pouring from a coffee kettle; what flows into the cup is glowing poison.', ['JOKER']),
    ('dagger_spray', '匕首雨', '小刀像扑克牌一样从指间扇形撒出', 'Replace the Silent with JOKER; the daggers fan out from his red-gloved fingers like thrown playing cards.', ['JOKER']),
    ('dagger_throw', '投掷匕首', 'Joker 侧身甩出一把小刀，拖出红色轨迹', 'Replace the Silent with JOKER throwing a knife sideways, leaving a red trail.', ['JOKER']),
    ('dash', '冲刺', '摩尔加纳巴士从宫殿出口冲出来，车灯是猫眼', 'The dashing figure becomes the MORGANA BUS bursting out of a palace exit at speed, same direction and footprint.', ['MONA_BUS']),
    ('deadly_poison', '致命毒药', 'Arsène 的爪子裹着咒怨的黑红火焰抓下来', 'The poison source becomes ARSENE\'s clawed hand wreathed in black-red curse flame striking down.', ['ARSENE']),
    ('defend_silent', '防御', 'Queen 拳套交叉架住暗影的一击', 'Replace the Silent with QUEEN blocking with crossed brass-knuckle gauntlets against a SHADOW\'s strike.', ['QUEEN', 'SHADOW']),
    ('deflect', '偏折', 'Noir 抡起大斧，把飞来的子弹弹开', 'Replace the Silent with NOIR swinging her giant axe to deflect incoming bullets with sparks.', ['NOIR']),
    ('dodge_and_roll', '闪躲翻滚', '怪盗翻滚着躲到掩体后面', 'Replace the Silent with JOKER rolling into cover in the same pose.', ['JOKER']),
    ('echoing_slash', '回响斩击', '总攻击：黑红剪影的连斩，敌人在烟里倒下', 'The slash becomes a P5 All-Out Attack: black-and-red silhouettes of the Phantom Thieves slashing through a cloud of smoke, SHADOWS falling.', ['JOKER', 'SHADOW']),
    ('envenom', '涂毒', 'Joker 用红手套抹过刀身，刀上留下紫色毒光', 'Replace the Silent with JOKER; his red-gloved fingers wipe along his knife, leaving a purple poison glow on the blade.', ['JOKER']),
    ('escape_plan', '逃脱计划', '偷到宝物后撤离：预告信在空中飘，Mona 巴士在窗外接应', 'Replace the Silent with JOKER escaping; a red-and-black calling card flutters behind him and the MORGANA BUS waits outside.', ['JOKER', 'MONA_BUS']),
    ('expertise', '独门技术', 'Oracle 在电脑前飞快敲键盘，屏幕上是宫殿的结构图', 'Replace the Silent with ORACLE typing fast; screens show a palace blueprint (no readable text).', ['ORACLE']),
    ('expose', '暴露', '面具被撕下，暗影露出原形的一瞬', 'The exposed target becomes a SHADOW whose white mask is torn away, revealing its monstrous true form bursting out.', ['SHADOW']),
    ('fade', '消影', 'Mona 贴着墙根潜行，身形淡进阴影', 'Replace the Silent with MONA sneaking along a wall, half faded into shadow.', ['MONA']),
    ('fan_of_knives', '刀扇', '一扇小刀在手上展开，像一手扑克牌', 'The knives become a fan of knives held in JOKER\'s red-gloved hand like a hand of playing cards.', ['JOKER']),
    ('finisher', '终结技', '「THE SHOW\'S OVER」：Joker 背对倒下的敌人整理手套', 'Replace the Silent with JOKER turning his back on fallen SHADOWS, pulling his red glove tight (the All-Out Attack finish pose). No text.', ['JOKER', 'SHADOW']),
    ('flanking', '夹击', 'Skull 和 Joker 一左一右夹击', 'Replace the attackers with SKULL and JOKER striking a SHADOW from both sides (a baton-pass moment).', ['SKULL', 'JOKER', 'SHADOW']),
    ('flechettes', '飞镖', 'Mona 拉满弹弓，石子连射', 'Replace the Silent with MONA firing his slingshot; the flechettes become a volley of slingshot pellets.', ['MONA']),
    ('flick_flack', '翻越撑击', 'Panther 撑地翻身一脚踢倒一片暗影', 'Replace the Silent with PANTHER doing a handspring kick through SHADOWS.', ['PANTHER', 'SHADOW']),
    ('sidestep', '侧步', 'Joker 侧身让开暗影的爪子，擦身而过', 'Replace the Silent with JOKER sidestepping a SHADOW\'s claw by a hair.', ['JOKER', 'SHADOW']),
    ('footwork', '灵动步法', 'Violet 的体操步法，脚下一串红色足迹', 'Replace the Silent with VIOLET in a light gymnastic step, a trail of red footprints behind her.', ['VIOLET']),
    ('grand_finale', '华丽收场', '撒旦耶尔举枪，大罪之弹射穿夜空', 'The finale becomes SATANAEL raising its huge gun and firing a blazing bullet through the night sky.', ['SATANAEL']),
    ('hand_trick', '手上技法', 'Joker 指间翻转一张预告信，像魔术师', 'Replace the Silent\'s hands with JOKER\'s red-gloved hands flipping a red-and-black calling card between the fingers like a magician.', ['JOKER']),
    ('haze', '迷雾', '双叶宫殿的金字塔沙暴，暗影在沙雾里看不清', 'The haze becomes a desert sandstorm around Futaba\'s pyramid palace, SHADOW masks barely visible in it.', ['SHADOW']),
    ('hidden_daggers', '隐秘匕首', 'Joker 掀开大衣内衬，挂满小刀', 'Replace the Silent with JOKER opening his tailcoat to reveal rows of knives in the lining.', ['JOKER']),
    ('infinite_blades', '无尽刀刃', '岩井店里的墙上挂满刀和模型枪', 'The blades become a wall rack full of knives and model guns in Iwai\'s airsoft shop; if a figure is needed, IWAI seen from behind.', ['IWAI']),
    ('knife_trap', '刀刃陷阱', '金城银行宫殿的安保激光网，网上挂着一排小刀', 'The trap becomes a red security-laser grid (Kaneshiro\'s bank palace) with knives hanging in it, a SHADOW caught.', ['SHADOW']),
    ('leading_strike', '先制打击', '「先发制人」：Joker 从天花板扑下抢先一击', 'Replace the Silent with JOKER dropping from above onto a SHADOW (ambush).', ['JOKER', 'SHADOW']),
    ('leg_sweep', '扫腿', 'Queen 回旋扫腿', 'Replace the Silent with QUEEN performing the same sweeping kick.', ['QUEEN']),
    ('malaise', '萎靡', 'Necronomicon 的削弱光束让暗影缩小、软下来', 'The weakening effect becomes NECRONOMICON\'s green debuff beam making a SHADOW shrink and sag.', ['NECRONOMICON', 'ORACLE', 'SHADOW']),
    ('master_planner', '谋划专家', '秀尽学园天台的作战会议，桌上摊着宫殿地图', 'The planner becomes the Phantom Thieves\' rooftop meeting at Shujin: a palace map spread out, JOKER\'s hand pointing at it.', ['JOKER']),
    ('memento_mori', '铭记死亡', 'Mementos 深处，铁链拖地的「死神」暗影出现', 'The death figure becomes THE REAPER emerging from the Mementos depths, chains dragging.', ['REAPER']),
    ('mirage', '蜃景', '宫殿主人认知里的「宝物」浮在半空，像海市蜃楼', 'The mirage becomes a palace Treasure (a glowing crown) shimmering in mid-air like a mirage. No characters.', []),
    ('murder', '谋杀', '黑面具 Crow 和 Loki，冷笑着举起光剑', 'Replace the Silent with CROW in his black mask outfit raising a red beam sword, LOKI looming behind.', ['CROW', 'LOKI']),
    ('neutralize', '中和', '子弹打碎暗影的面具', 'Replace the Silent with JOKER; his shot shatters a SHADOW\'s white mask.', ['JOKER', 'SHADOW']),
    ('nightmare', '夜魇', '天鹅绒房间的牢房：双子隔着铁栏看着，背后是伊戈尔的影子', 'The scene becomes the Velvet Room prison: CAROLINE and JUSTINE watching through blue cell bars, IGOR\'s silhouette behind them.', ['CAROLINE_JUSTINE', 'IGOR']),
    ('noxious_fumes', '毒雾', 'Mementos 隧道里翻涌的紫红毒雾', 'The fumes become purple-red toxic mist rolling through a Mementos subway tunnel, SHADOW masks in it.', ['SHADOW']),
    ('outbreak', '毒性爆发', 'Arsène 张开翅膀，黑红咒火爆开', 'The outbreak becomes ARSENE spreading his wings as black-red curse fire explodes outward.', ['ARSENE']),
    ('phantom_blades', '幻影之刃', '怪盗团的武器化成蓝色幻影悬浮着', 'The blades become ghostly blue phantom versions of the Phantom Thieves\' weapons floating (knife, pipe, whip, katana, axe).', []),
    ('piercing_wail', '尖啸', 'Carmen 的一声尖啸，周围暗影捂住耳朵', 'The wail becomes PANTHER\'s Persona CARMEN letting out a piercing shriek, SHADOWS around covering their heads; PANTHER beside her.', ['PANTHER', 'CARMEN', 'SHADOW']),
    ('pinpoint', '精密瞄准', 'Noir 架起榴弹发射器瞄准，准星锁定', 'Replace the Silent with NOIR aiming her grenade launcher, a red crosshair locked on.', ['NOIR']),
    ('poisoned_stab', '带毒刺击', '刀尖带着紫色毒光刺进暗影', 'Replace the Silent with JOKER stabbing a SHADOW with a purple-glowing poisoned knife.', ['JOKER', 'SHADOW']),
    ('pounce', '猛扑', 'Mona 像猫一样扑上去', 'Replace the Silent with MONA pouncing like a cat onto a SHADOW.', ['MONA', 'SHADOW']),
    ('precise_cut', '精确切击', 'Fox 居合：刀已收鞘，暗影才裂开', 'Replace the Silent with FOX sheathing his katana after a quick draw; the SHADOW splits apart behind him.', ['FOX', 'SHADOW']),
    ('predator', '猎杀者', '月光下 Joker 蹲在屋檐上俯视猎物', 'Replace the Silent with JOKER crouched on a rooftop edge under the moon, watching his prey.', ['JOKER']),
    ('prepared', '早有准备', '阁楼里收拾行动包：撬锁工具、烟雾弹、药', 'The preparation becomes JOKER\'s hands packing a bag in the Leblanc attic: lockpicks, smoke bombs, medicine.', ['JOKER']),
    ('reflex', '本能反应', '「第三只眼」：Joker 的视野里敌人发红', 'Replace the Silent with JOKER using his Third Eye: the world desaturated blue, the enemy glowing red.', ['JOKER']),
    ('ricochet', '连续反弹', 'Noir 的子弹在宫殿金属墙之间来回反弹', 'Replace the Silent with NOIR firing; the ricochets become bullets bouncing between metal palace walls with sparks.', ['NOIR']),
    ('serpent_form', '群蛇形态', 'Joker 身后升起咒烟化成的蛇', 'Replace the Silent with JOKER; the serpents become dark curse-smoke snakes with ivory fangs rising behind him.', ['JOKER']),
    ('shadow_step', '暗影步', 'Joker 从一片阴影里走出来', 'Replace the Silent with JOKER stepping out of a pool of darkness.', ['JOKER']),
    ('shadowmeld', '融入暗影', '漆黑里只剩 Joker 的白面具和红手套', 'Replace the Silent with JOKER almost fully swallowed by darkness: only his white mask and red gloves visible.', ['JOKER']),
    ('skewer', '串刺', 'Violet 的细剑连刺', 'Replace the Silent with VIOLET delivering rapid rapier thrusts into a SHADOW.', ['VIOLET', 'SHADOW']),
    ('slice', '切割', '小刀一切，红色斩痕', 'Replace the Silent with JOKER slicing with his knife, a red slash mark.', ['JOKER']),
    ('snakebite', '蛇咬', '咒烟蛇一口咬住暗影', 'The snake becomes a dark curse-smoke serpent with ivory fangs biting a SHADOW.', ['SHADOW']),
    ('sneaky', '鬼祟', '贴着墙潜行（怪盗的掩体移动）', 'Replace the Silent with JOKER pressed flat against a wall, sneaking in cover.', ['JOKER']),
    ('speedster', '速行者', '钩爪一拉，Joker 高速荡过宫殿大厅', 'Replace the Silent with JOKER swinging at high speed on his grappling hook across a palace hall.', ['JOKER']),
    ('storm_of_steel', '钢铁风暴', 'Skull 端着霰弹枪横扫', 'Replace the Silent with SKULL firing a shotgun spray; the storm of steel becomes the pellet spread.', ['SKULL']),
    ('strangle', '紧勒', 'Panther 的鞭子缠住暗影', 'Replace the Silent with PANTHER; the strangling cord becomes her whip coiled around a SHADOW.', ['PANTHER', 'SHADOW']),
    ('strike_silent', '打击', 'Joker 小刀斩下', 'Replace the Silent with JOKER striking with his knife.', ['JOKER']),
    ('sucker_punch', '突然一拳', 'Skull 一记偷袭拳', 'Replace the Silent with SKULL landing a sucker punch on a SHADOW.', ['SKULL', 'SHADOW']),
    ('suppress', '压制', 'Queen 骑着 Johanna 压过来', 'Replace the Silent with QUEEN riding JOHANNA, bearing down on the enemy.', ['QUEEN', 'JOHANNA']),
    ('survivor', '生存者', '伤痕累累的 Joker 单膝跪地，抬头笑着', 'Replace the Silent with a battered JOKER on one knee, looking up with a defiant smile.', ['JOKER']),
    ('tactician', '战术大师', 'Queen 推演战术，棋盘上的棋子是怪盗团', 'Replace the Silent with QUEEN moving pieces on a board whose pieces are tiny Phantom Thieves figures.', ['QUEEN']),
    ('the_hunt', '狩猎', '夺宝：Joker 伸手抓住宫殿的宝物（王冠）', 'Replace the Silent with JOKER reaching to snatch a glowing palace Treasure (a crown).', ['JOKER']),
    ('tools_of_the_trade', '必备工具', '卢布朗的桌上做撬锁工具', 'The tools become handmade lockpicks on a Leblanc attic desk, JOKER\'s red-gloved hands crafting them.', ['JOKER']),
    ('tracking', '跟踪', 'Oracle 的屏幕上锁定目标的红点', 'The tracking becomes ORACLE\'s holographic screen with a red target dot locked onto a SHADOW.', ['ORACLE', 'SHADOW']),
    ('untouchable', '触不可及', '岩井的模型枪店「Untouchable」柜台（不要招牌文字）', 'The scene becomes the counter of Iwai\'s airsoft shop "Untouchable" (no sign text): IWAI behind the counter, model guns on the wall.', ['IWAI']),
    ('up_my_sleeve', '袖里乾坤', '红手套的袖口滑出一排小刀', 'Replace the Silent\'s arm with JOKER\'s sleeve and red glove, a row of knives sliding out of the cuff.', ['JOKER']),
    ('well_laid_plans', '计划妥当', '预告信钉在宫殿主人的桌上', 'The plan becomes a red-and-black calling card pinned by a knife to a palace ruler\'s desk. No characters, no text.', []),
    ('wraith_form', '幽魂形态', 'Arsène 的半透明幽影包住 Joker', 'Replace the Silent with JOKER wrapped in the translucent wraith-like shadow of ARSENE.', ['JOKER', 'ARSENE']),
]

# 角色 → 参考图文件夹（同 docs/codex/codex_p5_card_art.md 第一步的名单）
REF_DIRS = {'MONA_BUS': ['mona'], 'CAROLINE_JUSTINE': ['caroline', 'justine'], 'SHADOW': ['shadows'], 'REAPER': ['shadows']}

COMMON = ('Use case: precise-object-edit. Image1 is the ORIGINAL Slay the Spire 2 card illustration (a Silent card) and the strict EDIT TARGET. '
          'Reinterpret the replaced subject as a specific Persona 5 Royal moment, character or object, while the card stays instantly recognizable at thumbnail size: '
          'same silhouettes, same action, same footprint, same camera. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, effects and core objects stay as in Image1 unless this prompt names them. '
          'Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything beyond what is stated. '
          'Wherever the Silent (the green-hooded huntress) appears, remove her completely with no remnants (no green hood, skull-like face, green cloak or pouches left) and refill the cleared area with the original background shapes, unless this prompt replaces her with someone. '
          'If the original composition cannot fit the idea literally, keep the composition and adapt the idea to it. '
          'Every named character MUST match the attached reference images exactly: face shape, eyes, hairstyle and hair color, mask, outfit, weapon; do not invent features or change their look; if a detail is unclear, keep it simple rather than guess. '
          'Match the original\'s rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness; no blur, no soft glow, no photoreal rendering. '
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
