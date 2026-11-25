# 摩尔加纳商人：生图 prompt（给 codex）

目标：只换掉商店房间里的商人形象 + 商店页面里指商品的那只手。摩尔加纳坐在商人原来的位置，耳朵、眼睛、尾巴、围巾要有明显的动态（运行时程序动画：
耳朵抖 / 转、眨眼、眼珠跟着鼠标或被指的商品、尾巴摆、呼吸起伏、买东西时开心的表情）。所以图要**分层**，每层单独动。

## 通用要求（每张都附上）

```
Style: hand-painted 2D game illustration matching Slay the Spire 2 (soft painterly shading, visible brush texture,
warm candle-lit shop lighting from the upper left, thick dark outlines with slight color variation), character design
faithful to Morgana from Persona 5 (cat form): black-and-white tuxedo cat, oversized round head, huge sky-blue eyes with
large black pupils and white highlights, white muzzle, white chest, white paw tips, black body, yellow bandana/neckerchief
tied around the neck, small utility belt optional, cartoonish chibi proportions (head about 45% of the body height).
Transparent background (real alpha, no checkerboard, no ground shadow, no floor, no props), centered, no text, no watermark,
no frame. Clean silhouette edges. Same lighting, same palette, same line weight in every image.
```

## 1. 坐着的摩尔加纳（主图，先出这张）

```
[通用要求] Full-body Morgana sitting upright on the floor facing the viewer, slightly turned 3/4 to the right, front paws
together resting on the ground in front of him, tail curled out to the left side, ears up and alert, eyes open looking
at the viewer, confident friendly smirk with mouth closed. Canvas 2048x2048, character fills about 80% of the height,
feet at the bottom 10% of the canvas.
```

## 2. 分层零件（用第 1 张做参考图，**同一个角色同一个角度同一个尺寸**，零件按在主图里的位置原样画，方便叠回去）

一张一层，画布都是 2048x2048、透明底、位置和主图完全对齐（叠回主图要严丝合缝）：

| 文件名 | prompt 追加内容 |
|---|---|
| `morgana_body.png` | `Only the body: torso, sitting legs, front paws, bandana. NO head, NO tail. Fill the neck area cleanly under where the head sits.` |
| `morgana_head.png` | `Only the head WITHOUT ears and WITHOUT the eyes' pupils: head shape, muzzle, mouth closed smirk, white eye sockets (empty white eyes, no pupils). Fill the top of the head smoothly where the ears attach.` |
| `morgana_ear_l.png` / `morgana_ear_r.png` | `Only the left (right) ear, full shape including the part hidden by the head, inner ear pink-grey.` |
| `morgana_pupil.png` | `One pupil only: large black oval pupil with two white highlights, iris ring sky-blue, on transparent background, 256x256.` |
| `morgana_lids_closed.png` | `Only the closed eyelids for both eyes (black fur lids with a curved lash line, happy closed-eye shape), aligned to the eye sockets.` |
| `morgana_lids_half.png` | `Only half-closed upper eyelids for both eyes (sleepy/smug), aligned to the eye sockets.` |
| `morgana_mouth_open.png` | `Only the mouth area: open happy mouth (laughing "nya" shape) with small fang, to be placed over the closed mouth.` |
| `morgana_tail.png` | `Only the tail, extended straight up-left from its base in a gentle S curve, full length including the base hidden behind the body.` |
| `morgana_bandana_tail.png` | `Only the two loose knot ends of the yellow bandana, so they can flutter.` |

（如果分层不好对齐：只出第 1 张 + 第 1 张改「闭眼」「半闭眼」「张嘴」三个版本也行，耳朵 / 尾巴我在代码里抠出来补背后的缺口。）

## 3. 商店页面的手 → 摩尔加纳的前爪

原版是一只大手在地毯上飘、指向鼠标停留的商品。换成摩尔加纳的前爪，从画面上方伸下来：

```
[通用要求] A single cartoon cat foreleg of Morgana reaching down from the top edge of the canvas: black fur forearm,
white fluffy paw at the end, paw tilted forward as if pointing / tapping at an item below, soft pink paw pads slightly
visible, the arm is cut off cleanly at the top edge of the canvas. Canvas 1024x1536, arm enters from the top center,
paw tip near the bottom 15%.
```

再出两个同尺寸同位置的变体（运行时切换）：
- `paw_idle.png`：`same foreleg, paw relaxed and slightly curled, hanging loosely`
- `paw_tap.png`：`same foreleg, paw pressed down flat as if tapping/stamping an item, pads spread`

## 交付

全部放 `art/p5/merchant/`（PNG，带透明通道）。我这边做：替换商店房间的商人和商店页的手，耳朵 / 眼睛 / 尾巴 / 围巾 / 呼吸的程序动画，
眼睛跟着鼠标和被指的商品，买东西时闭眼笑 + 耳朵抖一下。
