# 美术规格

## 阶段 1：定画风（本轮）

生成 3 张风格候选的**全身定妆图**，用户挑选一张作为后续所有分件的母版。

通用要求（3 张都必须满足）：
- 尺寸 1024×1536，竖版。背景透明；不支持透明时用纯色 `#00FF00`。
- 只有一个人物，全身完整入画（头顶到鞋底都在画面内，四周留白至少 60px）。
- **朝向画面右侧**的 3/4 侧身站姿（游戏里玩家在左边，面朝右边的敌人）。
- 绑骨用的中性站姿：离观众**远**的那只手插在裤兜里；离观众**近**的那只手臂自然下垂，和身体之间留出空隙（不要贴住躯干）；两腿微微分开，膝盖不交叠。
- 不拿武器。召唤器插在腰侧枪套里，剑不出现。
- 不要文字、水印、地面阴影、背景元素。

角色要素（英文描述，用于 imagegen）：
> a slender Japanese high school boy, Persona 3 protagonist style: dark navy-blue hair with long bangs covering one eye, calm expressionless grey eyes, black school blazer worn open over a white shirt, red armband on the upper arm, black trousers, brown loafers, red-and-white headphones hanging around the neck with the cord leading to an MP3 player clipped at the waist, a silver gun-shaped "Evoker" holstered at the hip

| 候选 | 文件名 | 风格描述（追加在角色要素后） |
|---|---|---|
| A | `art/concept/style_A_sts2.png` | in the art style of Slay the Spire 2: hand-painted digital illustration, bold dark ink outlines with varying thickness, painterly cel shading, slightly exaggerated proportions (larger head and hands), muted palette with a cool blue accent |
| B | `art/concept/style_B_anime.png` | clean modern anime cel shading like Persona 3 Reload key art, crisp thin outlines, cool blue and cyan lighting accents |
| C | `art/concept/style_C_blend.png` | hybrid: Persona 3 Reload anime face and hair, but painted with Slay the Spire 2's thick ink outlines and painterly texture |

## 阶段 2：分件（选定画风后）
待定。会以选中的母版为参考，逐件生成：后发、头、刘海、躯干、上臂 ×2、前臂 ×2、手 ×3（自然 / 握召唤器 / 握剑）、大腿 ×2、小腿 + 鞋 ×2、耳机、召唤器、单手剑。每件都有固定的画布大小和锚点，具体规格在选定母版后写入本文件。

## 阶段 3：Persona 立绘
Orpheus、Thanatos、Messiah 各一张半透明召唤立绘，用于 S 级卡牌演出。
