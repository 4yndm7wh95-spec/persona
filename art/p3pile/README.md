# P3R 牌堆界面结城理立绘与眨眼帧

## 交付
- pile_cand_a/b/c.png：三个候选，均为 1024×1536 RGBA，imagegen 内置工具生成，原始图像直接保存。
- pile_base.png：逐字节复制候选 B。
- pile_eyes_half.png / pile_eyes_closed.png：分别在底图上用 imagegen 编辑眼睛，再按下述蒙版还原。
- meta.json：左上角为原点的像素坐标，包含露出左眼中心、牌四角、头顶、下巴、左右肩端与头发外缘；手遮挡的牌角为估计值。
- preview.png：首行候选、第二行底图/半睁/闭眼、第三行眼部原尺寸 3 倍对比。深蓝色底仅供预览，立绘本身无背景。
- ref/commu_frame.png：overview_10 第二行第六格；实际格宽 320、高 224。去除时间戳带后裁取 (1600,264)-(1920,425)，放大 3 倍。
- ref/menu_frames.png：55.0、56.5、57.67、60.83 秒对应格横拼，去掉时间戳带。

## 选择
选择 B：深蓝头发、蓝白硬边色块更接近菜单参考；脸型、遮右眼刘海、耳机、制服和红臂章延续 commu_v2_a。牌是平整四边形，只有细白边和一颗四角星；握牌手目检未见多指或缺指。A 头发偏电蓝，C 肤色偏暖，故未选。

## 次数
| 文件 | 生成次数 | 编辑次数 | 采用情况 |
|---|---:|---:|---|
| pile_cand_a | 1 | 0 | 保留候选 |
| pile_cand_b | 1 | 1 | 保留原生成版；额外背景清理编辑未采用 |
| pile_cand_c | 1 | 0 | 保留候选 |
| pile_base | 0 | 0 | 原样复制 B |
| pile_eyes_half | 0 | 1 | 采用编辑眼部 |
| pile_eyes_closed | 0 | 1 | 采用编辑眼部 |

没有安全拒绝。共 3 次候选生成、1 次未采用的背景清理编辑、2 次眨眼编辑。

## 对齐与透明度自检
已执行任务第 5 步的还原：只保留中心 (578,274)、半径 (51,35) 的椭圆内编辑，边缘约 8 px 羽化；其余 RGBA 像素恢复底图，alpha 整张直接复用底图。半睁和闭眼各自眼部区域外变化像素数均为 **0**。被刘海遮住的右眼不可见，无需第二块蒙版。闭眼帧无可见虹膜或瞳孔，半睁帧上眼睑覆盖上半虹膜。

6 张立绘均 1024×1536、RGBA。B 和两张眨眼帧各有 506682 个 alpha=0 像素。实际 alpha 合成到深底检查没有背景晕光。生成文件透明像素的 RGB 中仍存有原本不可见的色彩，某些忽略 alpha 的预览器会把它显示成光晕；正常 alpha 合成不显示这些颜色。保留原生成 PNG，不做绿幕、抠色或缩放。下方衣身按半身构图自然越出画布，因此下角未必都是透明像素。

## 已知差异与使用建议
- 站姿的侧向与右倾是视觉近似，未用几何旋转强制校正为精确 8°。
- 主体是蓝白硬边色块，但耳机、金属扣与眼睛仍有少量细小高光，非严格限定为两种 RGB 值。
- 本图仅人物与牌；水下背景、卡牌列、呼吸和发梢动画由后续代码完成。
- 坐标为手工目测近似定位，游戏裁切和变形时可微调。
- 未改动代码、已有资源或游戏文件，未启动游戏。

## 实际提示词
### 候选 A
Use case: stylized-concept. Create candidate A of a Persona 3 Reload in-game COMMU menu portrait for a mod pile screen, a single transparent RGBA PNG EXACTLY 1024x1536.
Reference roles: commu_v2_a.png defines Makoto Yuki's identity, hair and school uniform; flat/commu_v2_a.png and menu_frames.png define a crisp flat menu illustration palette, not their backgrounds; commu_frame.png defines the raised tarot-card pose.
Draw only Makoto and one tarot card, waist-up. Calm slightly aloof face, gaze toward screen left. Body slightly turned screen left, whole silhouette leaning 8 degrees toward screen right. Face and head fully visible, leave a little clear space above hair. His anatomical right hand (screen left) raises a flat rectangular tarot card beside his face, facing viewer, deep navy face with a thin white border and ONE white four-point star centered, no other marks. Natural five-finger anatomy and convincing grip. His left arm rests down and may exit bottom edge. Deep blue asymmetric fringe covers his anatomical right eye, visible left eye is on screen right. Dark Gekkoukan school jacket, white shirt, narrow black ribbon tie, red headphones around neck, red armband on anatomical left upper arm, headphone cable down chest.
Style: Persona 3 Reload menu illustration, bold elegant black hard-edged ink contour, pure white and pale cyan-white highlights, saturated cobalt/ultramarine flat shadow shapes, midnight navy darkest areas, dark blue hair with cyan highlight blocks. ONLY red headphones and armband as small accents. NO gradients, NO realistic lighting, NO painterly rendering, NO glow. High quality clean silhouette. Actual transparent background with alpha zero, never a colored or checkerboard background. No scenery, UI, text, signature, watermark. Not a screenshot, no floating graphics or extra props.

### 候选 B
Use case: stylized-concept. Create candidate B of a Persona 3 Reload in-game COMMU menu portrait for a mod pile screen, a single transparent RGBA PNG EXACTLY 1024x1536.
Reference roles: commu_v2_a.png defines Makoto Yuki's identity, hair and school uniform; flat/commu_v2_a.png and menu_frames.png define a crisp flat menu illustration palette, not their backgrounds; commu_frame.png defines the raised tarot-card pose.
Draw only Makoto and one tarot card, waist-up. Calm slightly aloof face, gaze toward screen left. Body slightly turned screen left, whole silhouette leaning 8 degrees toward screen right. Face and head fully visible, leave a little clear space above hair. His anatomical right hand (screen left) raises a flat rectangular tarot card beside his face, facing viewer, deep navy face with a thin white border and ONE white four-point star centered, no other marks. Natural five-finger anatomy and convincing grip. His left arm rests down and may exit bottom edge. Deep blue asymmetric fringe covers his anatomical right eye, visible left eye is on screen right. Dark Gekkoukan school jacket, white shirt, narrow black ribbon tie, red headphones around neck, red armband on anatomical left upper arm, headphone cable down chest.
Style: Persona 3 Reload menu illustration, bold elegant black hard-edged ink contour, pure white and pale cyan-white highlights, saturated cobalt/ultramarine flat shadow shapes, midnight navy darkest areas, dark blue hair with cyan highlight blocks. ONLY red headphones and armband as small accents. NO gradients, NO realistic lighting, NO painterly rendering, NO glow. High quality clean silhouette. Actual transparent background with alpha zero, never a colored or checkerboard background. No scenery, UI, text, signature, watermark. Not a screenshot, no floating graphics or extra props.
CRITICAL: The reference's navy fill is NOT a background to copy. Leave ALL negative space genuinely transparent alpha=0. No halo, vignette, atmospheric color, fog or glow behind the silhouette. Solid opaque character, antialiasing only at outline. Keep corners transparent. Hair should mostly midnight blue rather than electric blue. Restrained large flat color blocks rather than many small facets.

### 候选 C
Use case: stylized-concept. Create candidate C of a Persona 3 Reload in-game COMMU menu portrait for a mod pile screen, a single transparent RGBA PNG EXACTLY 1024x1536.
Reference roles: commu_v2_a.png defines Makoto Yuki's identity, hair and school uniform; flat/commu_v2_a.png and menu_frames.png define a crisp flat menu illustration palette, not their backgrounds; commu_frame.png defines the raised tarot-card pose.
Draw only Makoto and one tarot card, waist-up. Calm slightly aloof face, gaze toward screen left. Body slightly turned screen left, whole silhouette leaning 8 degrees toward screen right. Face and head fully visible, leave a little clear space above hair. His anatomical right hand (screen left) raises a flat rectangular tarot card beside his face, facing viewer, deep navy face with a thin white border and ONE white four-point star centered, no other marks. Natural five-finger anatomy and convincing grip. His left arm rests down and may exit bottom edge. Deep blue asymmetric fringe covers his anatomical right eye, visible left eye is on screen right. Dark Gekkoukan school jacket, white shirt, narrow black ribbon tie, red headphones around neck, red armband on anatomical left upper arm, headphone cable down chest.
Style: Persona 3 Reload menu illustration, bold elegant black hard-edged ink contour, pure white and pale cyan-white highlights, saturated cobalt/ultramarine flat shadow shapes, midnight navy darkest areas, dark blue hair with cyan highlight blocks. ONLY red headphones and armband as small accents. NO gradients, NO realistic lighting, NO painterly rendering, NO glow. High quality clean silhouette. Actual transparent background with alpha zero, never a colored or checkerboard background. No scenery, UI, text, signature, watermark. Not a screenshot, no floating graphics or extra props.
CRITICAL: The reference's navy fill is NOT a background to copy. Leave ALL negative space genuinely transparent alpha=0. No halo, vignette, atmospheric color, fog or glow behind the silhouette. Solid opaque character, antialiasing only at outline. Keep corners transparent. Hair should mostly midnight blue rather than electric blue. Restrained large flat color blocks rather than many small facets.
Use reference only for identity; simplify into true two-tone graphic screenprint, white/cobalt blocks and navy ink. Do not copy the dark backdrop. Transparent cutout asset. The card must be beside cheek height rather than below shoulder. Lean upper body gently screen right.

### 背景清理尝试（未采用）
Use case: background-extraction. EDIT this exact portrait. Remove ALL background and the diffuse blue, white and red halos around the character. Actual clean transparent PNG alpha=0 everywhere outside the sharp character/card silhouette. Preserve character pose, face, outfit, card, crop and 1024x1536 canvas exactly. The body itself must be opaque. No soft glow anywhere. Do not repaint the subject. No white/black backdrop, no checkerboard, no vignette. This is a game sprite cutout.

### 闭眼编辑
Use case: precise-object-edit. Edit this exact game portrait to make a CLOSED-EYES blink frame. Only change the visible anatomical left eye at approximately pixel (575,270) on screen right, and any visible sliver of the other eye. Both eyes naturally gently fully CLOSED: upper lid lowered into a single graceful dark eyelash curve, NO visible iris or pupil. Calm expression, no smile. Preserve exact 1024x1536 canvas, position, face outline, eyebrows, hair, skin colors, pose, hands, card, every clothing detail and actual transparent alpha background. Everything outside the small eye region stays pixel-identical. Do not move or resize anything. Output true transparent RGBA PNG.

### 半睁编辑
Use case: precise-object-edit. Edit the INPUT open-eye portrait into a HALF-OPEN blink frame. Change ONLY visible anatomical left eye at pixel (575,270), screen right. Upper eyelid lowered to cover upper HALF of iris/pupil, eye remains half-open with lower half blue iris visible. Natural calm relaxed blink. Do NOT close it completely. Keep eyebrow and all surrounding face/hair fixed. Preserve exact canvas 1024x1536, character position, pose, hand, card, colors, clothing, silhouette and transparent background. Everything outside small eye region pixel-identical, no moving, scaling or reframing. True transparent RGBA PNG.
