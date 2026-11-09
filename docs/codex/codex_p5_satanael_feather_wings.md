你在用户的 Windows 电脑上工作。任务：**把撒旦耶尔第 10 版分层里的三层翅膀重画成原版那种黑色羽毛翅膀**。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/beautiful-keller-4lsbk4**
（先 fetch origin claude/beautiful-keller-4lsbk4:refs/remotes/origin/claude/beautiful-keller-4lsbk4，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-feather-wings，报告分支名。

【用户的意见（2026-11-09）】
「这个人格面具的翅膀其实不是这样子的。它的翅膀是类似那种**黑色的羽毛**的翅膀，不是这种偏硬的风格。以原版的那个样子为准。」
现在 layers_v10 的六片翼是**硬的镰刀形翼膜 + 大块金色骷髅图案**（照模型背面的贴图画的），看起来像金属板 / 蝙蝠翼。
原版战斗镜头里（P5R 大罪穿甲弹的切入画面）他的翅膀是**一大片黑色羽毛**：翼根是密密的小鳞片状羽毛，往外是**一根根分开的长飞羽**，羽尖锋利、往外弯（还保留镰刀一样的轮廓），
黑色 / 深藏青，边缘被蓝光勾出一道冷色亮边；**没有金色骷髅图案**。

【参考图（都在 art/p5/fx/v4/satanael/）】
- **ref_feather_wings.png**：Claude 拼的参考总览——原版切入镜头三张（低机位持枪、举枪、开枪）+ 模型贴图里的羽毛区域；**以这张为准**；
- ref_original_satanael.png、ref_raise.png、ref_fire.png：原图；
- model_ref/turn_front_cel.png：原模型的翅膀分组（左右各三片、几乎一样大）——只参考**分组、位置、大小**，不参考它的金色图案和硬质感；
- layers_v10/check_layers_v10.png：现在的分层（要替换的就是 wings_back_far / wings_back_near / wings_front 三层）。

【要做的】
1. 只重画 **wings_back_far.png、wings_back_near.png、wings_front.png** 三层；body、head、arm_*、gun、finger_tip、eyes_glow **一像素都不改**。
2. 画布、坐标系不变：5400×3000，透明底；三层的**翼根位置不变**（layers.json 的 wing_far_root (2400,1400)、wing_near_root (2650,1600)、wing_front_root (2730,1650)），
   每组的整体外轮廓（张开的范围、朝向）和现在**大致相同**，游戏里的构图、云海、光环位置都按这个算好了。允许羽尖比现在的翼尖稍微长一点、更张开。
3. 每组翅膀的结构：翼根（贴着身体那一截）是**一层层叠起来的小羽毛**（鳞片状，参考贴图）；往外是**覆羽**（中等大小，排成几排）；最外面是**7～10 根分开的长飞羽**，
   长、窄、末端尖并且往外弯（整组的轮廓仍然像三把镰刀那样的三片，只是每片由羽毛组成）；羽毛之间有空隙能透出后面。
4. 画风和其他层一致：**P5 风格**，清楚的黑色描边、两阶硬明暗（一层暗面、一层亮面，不要写实渐变），颜色以**黑 / 深藏青**为主，每根羽毛的上缘一道**冷蓝色的亮边**（原版的蓝光）。
   不要金色、不要骷髅、不要星星图案。
5. 被身体、手臂挡住的翼根部分也要画完整（游戏里翅膀会单独小幅鼓动，会露出来）。羽毛在原生分辨率下画，不要整片放大。

【可直接复制的生图提示词】
先用第一条出整体效果（拿 layers_v10 叠回的整张图 + ref_feather_wings.png 做参考 / 图生图），用户认可后再按第二条逐层出（每层一张，透明底）。

整体（图生图，在第 10 版整图上只改翅膀）：
```
Edit this illustration: replace ONLY the six wings of the winged demon (three on each side) with huge BLACK FEATHERED WINGS. Keep the body, armor, horned head, halo, arm, gun, pose, camera, canvas size and composition exactly the same.
Wings: dense overlapping small scale-like black feathers at the wing roots, rows of medium covert feathers, and 7-10 long separate flight feathers per wing that are narrow, sharp-tipped and curve outward like scythe blades; each side still reads as three large scythe-shaped wing groups made of feathers, with gaps between the long feathers.
Colors: black and deep navy only, a crisp cold blue rim light along the upper edge of every feather, no gold, no skulls, no stars, no membrane panels.
Style: Persona 5 anime cel shading, bold black outlines, two-tone hard shadows, high detail, clean edges, matching the existing illustration.
```

逐层（每层一张，透明底，和 wings_back_far / wings_back_near / wings_front 同画布同位置）：
```
One layer of a 2D cutout for animation, transparent background, 5400x3000 canvas: a single group of huge BLACK FEATHERED demon wings (three scythe-shaped feather wings fanning out from one root), seen slightly from below at a 3/4 angle.
Feather structure: dense overlapping small scale-like feathers at the root, rows of medium coverts, then 7-10 long separate flight feathers per wing, narrow, sharp-tipped and curving outward, with gaps between them.
Colors: black and deep navy, crisp cold blue rim light on the top edge of each feather, no gold, no skulls, no patterns. Persona 5 anime cel shading, bold black outlines, two-tone hard shadows.
Nothing else in the image: no body, no head, no arm, no background. The wing root sits at the same position and the overall silhouette covers the same area as the reference layer.
```
（逐层时把对应的旧层 wings_*.png 一起给生图工具当位置 / 轮廓参考；wings_front 是盖在身体前面那组，翼根在右胸前。）

【输出】覆盖 art/p5/fx/v4/satanael/layers_v10/ 的三张翅膀层；layers.json 只在翼根坐标确实变了时才改（尽量不变）。
另存 art/p5/fx/v4/satanael/layers_v10/feather_full.png（整体效果图）。
【检查】check_feather_v10.png：原 v10 整图 / 换羽毛翅膀后叠回 / 三层各自单独 / 每组翅膀 ±3° 叠图（游戏里只转这么多，看翼根有没有露洞）/ 和 ref_feather_wings.png 并排。
露洞或画风不一致就补画，最多 2 次。
【提交】只 add 改动的翅膀层、feather_full.png 和 check 图；提交信息 "art: Satanael v10 black feathered wings (from the original cut-in)"。
【报告】中文：三层的变化、翼根坐标有没有变、原生分辨率、检查结果、提交号或分支名。Claude 收到后跑 tools/p5/satanael/game_layers.py 换进游戏。
