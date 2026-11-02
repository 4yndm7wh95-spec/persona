# 亚森草图：官方形象校正版（待确认）

此前两版被用户否定，已保留v1和v2文件。本次是第3次生成：重新查看P5R官网的官方立绘，直接附给内置imagegen作为主要设计和姿势参考，不使用此前生成图作为输入。

官方来源：https://p5r.jp/character/hero/

原图URL：https://p5r.jp/resources/img/character/hero/persona_aa0c6566c4bd27dd93fc4f7a5444bc7b.png

官方参考保存为arsene_official_reference.png，仅作设计参考。重点纠正：黑色羽翼而不是红色内翼；原版红色腿部尖角和刀刃鞋跟；黑色贴身躯干、白色领巾、原版头帽轮廓；去掉此前臆造的长燕尾与多余锁链。姿势贴近官方立绘，不继续自行编排怪异悬浮动作。

当前arsene_draft.png仍待用户确认，尚未拆层。原始输出为arsene_draft_raw.png。

## 完整提示词

```text
Create a faithful 2D game character illustration of Arsene from Persona 5 using the ATTACHED OFFICIAL ART as the sole primary character design and pose reference. This is a correction of inaccurate earlier drawings; do not invent or simplify the costume. Study and match the official silhouette, head and body proportions, facial structure, thin elongated tilted black top hat, angular red face and long lateral head projections, high red collar, white layered lace cravat, red fitted jacket sleeves, black close-fitting central torso and short black hip garment, small gold trim/chains exactly as seen. CRITICAL legs: reproduce the official sculptural red pointed thigh structures and narrow red lower legs with dramatic curved metallic KNIFE-BLADE HEELS extending downward behind the feet; these are NOT ordinary tall fashion boots. CRITICAL wings: two huge organic BLACK feathered wings, charcoal gray and subdued brown/purple feather shading as in the reference, ragged naturally layered feather silhouette. NO broad crimson/red wing lining. NO invented long red coat tails or extra draped chains. Match the attached official pose closely: upright airborne stance, head subtly bowed, one arm extended outward/up with elegantly curved black claw fingers, other arm lowered relaxed, two legs naturally separated with one knee slightly bent, no crossed legs and no twisting contortion. Keep the reference's theatrical but balanced character proportions, do not stretch or bulk up the anatomy independently. Full body including blade heels and both wings visible, arrange wider wings within portrait 1536x2048 canvas with clear transparent margin on all four sides. Clean official P5 hand-drawn cel-shaded aesthetic, precise dark contours and graphic red/black/white shapes, not 3D rendering, no photoreal texture. The reference website's gold silhouette outline, decorative border/background and any backdrop artifacts MUST NOT appear. Genuine transparent RGBA PNG, alpha zero outside character, no ground, shadows cast on background, effects, text, other characters or props. This is a design-faithful approval draft.
```

## 检查与处理

- 原始尺寸[1086, 1448]，交付1536×2048 RGBA；只归一尺寸、清alpha<=2残点及全透明像素RGB，没有局部结构修图。
- 四角alpha：[0, 0, 0, 0]；上/下/左/右边缘最大alpha：[4, 0, 4, 4]。边缘仍有极低alpha像素，照实记录，不自动重画。
- 深灰检查：check_arsene.png；棋盘格检查：check_arsene_checker.png。
- 本轮按用户要求重画1次，总计3次，未自行追加生成。外形、姿态、比例由用户验收。
