# 摩尔加纳商人：生图 prompt（给 codex）

用户 2026-11-25：第一版出成了现实里的猫形态 → 要**异世界（宫殿）里的摩尔加纳**：两脚站立的吉祥物猫、大头、头顶黑色像戴了面罩、
下半张脸白、大蓝眼、黄围巾、白手套、腰带、靴子。坐在商人原来的位置（商店帐篷右边那块绿地毯上，原版商人是盘腿坐的蓝色兜帽人），
画风要和杀戮尖塔2 的商店场景一致（平涂水粉感、颗粒纸纹、细的深棕描边、紫色灯笼 + 暖烛光）。生图时把商店截图一起给 codex 当画风参考。

尺寸参考（1080p 截图）：原版商人连地毯上的身子约 260×320 px（x 1210–1460，y 475–790），摩尔加纳做到差不多大，头可以大一点。

## ③ 坐着的摩尔加纳（主图）

```
Use the attached Slay the Spire 2 shop screenshot as the STYLE reference: flat gouache-like painting, soft grainy paper
texture, limited muted palette, thin dark-brown outlines, simple shapes, warm candle light from below-left and purple lantern
light from the upper right, soft cast shading. Match that style exactly; it must look like it belongs in that tent.

Character: Morgana from Persona 5 in his Metaverse / Palace form — a bipedal anthropomorphic cartoon cat mascot (NOT a real
cat): oversized round head (about half of his height), the top of the head is black and wraps around the eyes like a
thief's mask, the lower half of the face and the muzzle are white, huge round sky-blue eyes with large black pupils and
white highlights, big pointed black ears with pale inner ear, confident cheeky grin, yellow neckerchief tied at the neck
with the knot ends sticking out, black body, white gloved hands, a brown utility belt with a round buckle and small pouches,
small black boots, long black tail with a curl at the tip.

Pose: sitting cross-legged on the floor like a street merchant (same pose idea as the hooded merchant in the screenshot),
hands resting on his knees, body turned 3/4 to the LEFT (towards the player who stands on the left side of the shop),
head tilted slightly, looking at the viewer, tail curling out behind him to the right side.

Output: transparent background (real alpha, no checkerboard), no floor, no rug, no shadow, no props, no text, no watermark.
Canvas 2048x2048, character fills about 85% of the canvas height, bottom of the crossed legs at the bottom 8% of the canvas.
```

## ③-b 分层零件（耳朵 / 眼睛 / 尾巴 / 围巾要动）

主图当参考图，每张都加这句：
`Same character, same pose, same size and EXACTLY the same position as the reference image, 2048x2048, transparent background, same style.`
再加对应的一句：

```
morgana_body.png: Only the body: crossed legs, torso, arms and gloved hands, belt, neckerchief knot. NO head, NO tail. Fill the neck area cleanly where the head sits.
morgana_head.png: Only the head WITHOUT ears and WITHOUT pupils: black mask-shaped top, white lower face, grin, empty white eyes. Fill the top of the head smoothly where the ears attach.
morgana_ear_l.png: Only the left ear, full shape including the base normally hidden by the head.
morgana_ear_r.png: Only the right ear, full shape including the base normally hidden by the head.
morgana_pupil.png: One pupil only: large black oval pupil with two white highlights and a sky-blue iris ring, 256x256 canvas.
morgana_lids_closed.png: Only the closed eyes for both eyes (happy closed "^ ^" shape in black, covering the white eyes), aligned to the eyes.
morgana_lids_half.png: Only half-closed black upper lids for both eyes (smug look), aligned to the eyes.
morgana_mouth_open.png: Only the mouth area: wide open happy laughing mouth with a small fang, placed over the grin.
morgana_tail.png: Only the tail, full length including the base normally hidden behind the body, gentle S curve with curled tip.
morgana_scarf_ends.png: Only the two loose knot ends of the yellow neckerchief, so they can flutter.
```

退路：分层对不齐的话，只出主图 + 主图的「闭眼笑」「半闭眼」「张嘴笑」三个版本，耳朵和尾巴我在代码里抠。

## ④ 商店页面的手 → 摩尔加纳的白手套

```
Same style as the attached Slay the Spire 2 shop screenshot (flat gouache, grainy texture, thin dark-brown outlines,
warm light). Morgana's arm from Persona 5 Metaverse form reaching down from the top edge of the canvas: short black furry
arm, big white cartoon glove at the end, index finger pointing down at an item below, the arm cut off cleanly at the top edge.
Transparent background (real alpha), no text, no watermark. Canvas 1024x1536, arm enters from the top center, fingertip near
the bottom 15%.
```
同尺寸同位置的两个变体：
```
paw_idle.png: same arm, same position, glove relaxed and slightly open, hanging loosely.
paw_tap.png: same arm, same position, glove pressed flat as if tapping an item.
```

## 交付
全部放 `art/p5/merchant/`：主图 `morgana_full.png`、零件按上面文件名、手 `paw_point.png` / `paw_idle.png` / `paw_tap.png`。
