# 天鹅绒房间电梯分层 · 修订 1

静止时显示暗色铁艺背景。轿厢和发光层与背景分离，供模组控制从下往上掠过。

| 文件 | 尺寸 | 用途 |
|---|---|---|
| room_front.png | 1672×941 RGBA | 原房间前景板，侧墙和细高窗开孔 |
| lantern_left.png、lantern_right.png | 1672×941 RGBA | 从底图分离的左右灯与吊链 |
| backdrop.png | 1672×941 RGBA | 不透明的静止暗背景 |
| passing_car.png、passing_car_glow.png | 392×652 RGBA | 同坐标的轿厢和内光。轿厢主体 360×620，四边各留 16px 透明边 |
| preview.png、preview_car.png | 1672×941 | 平时预览和轿厢从栏杆后经过的预览 |
| compare.png | 3344×941 | 左侧底图，右侧带轿厢预览 |

绘制顺序为暗背景、轿厢、前景板、吊灯。内光贴图的透明度代表发光强度，与轿厢共用局部坐标。轿厢预览使用左开口位置，贴图原点为 (62, -18)。吊链建议挂点按底图量取，左 (199, 75)，右 (1474, 75)。坐标原点在左上。

## 制作和自检

- 采用内置 imagegen，暗背景和轿厢分别生成一次。轿厢保留生成的原生透明度，裁切后适配目标尺寸。发光层从轿厢窗口内的蓝色亮部提取。
- 前景板和吊灯使用原图取样与透明遮罩，前景板共两轮遮罩处理，第二轮柔化了下方底座与编辑区域的交界。吊链与上拱重叠的少量像素用相邻铁艺像素补齐。
- 允许编辑区外 RGBA 变化像素数为 **0**。两盏灯的不透明像素与底图一致。轿厢预览与常态预览相比，前景板的完全不透明像素变化数为 **0**。
- 开口内暗背景的平均亮度约为原墙的 **18.3%**。轿厢与发光层画布边缘 alpha 为 0。
- 较细铁艺和吊链边缘使用抗锯齿透明度，局部仍带底图原有的蓝色反光。底部开口受 edit_region.png 边界限制，底座交界保留少量原蓝色过渡。
- 修订 1 使用静止背景，滚动井道接缝检查不再适用。

## imagegen 提示词

两张图均以 ref_p3r_velvet_room.webp 为风格参考。

**暗背景**

> Use case stylized-concept. Generate a wide 16:9 opaque 2D game art backdrop, 1672:941 composition. The reference is ONLY the style of the dark blue ornamental lattice wall behind the characters. Fill the whole image with a very distant Art Nouveau wrought-iron lattice in near-black midnight navy and violet, extremely low contrast, faint vertical bars and diamond patterns, occasional understated circular crest and vegetal scrolls, softly defocused. Colors mostly #05071A through #141A3C. Shadowy theatrical void, nearly black, subdued texture. Even, front-facing view. No foreground, no characters, no table, no floor, no windows, no signs, no light sources, no piping, no beams, no mechanical features. The shadowy texture should read almost black at a glance. No text.

**轿厢**

> Use case stylized-concept. New standalone game sprite on TRUE transparent RGBA background, a single empty Art Nouveau cage elevator in precise FRONT elevation with zero perspective, centered and fully inside the frame, 360:620 subject width-to-height ratio, tight tall framing with transparent breathing room around the entire object. Use the reference image's wrought iron language: elegant silver violet narrow vertical rods, slim diamond trellis with circular floral crest, curving arch and vegetal flourishes. The car has a solid ornamental top crown with a small lifting attachment centered at its top, straight side posts, and a solid shallow bottom iron base. A vivid cold blue #4C7DFF to #9CC4FF interior light shines THROUGH the lattice, confined to the interior windows with clearly contrasting darker metal bars; do not put a glow outside the silhouette. The inside is empty. Render hand-painted high-end anime game environment art with refined purple-silver metal highlights, no noisy texture. Never an industrial elevator or modern doors. No room, walls, rails, ground, surroundings, cast shadow, typography, words, people, extra copies, checkerboard, or motion blur. Clean transparent edges with the complete top and bottom visible.

